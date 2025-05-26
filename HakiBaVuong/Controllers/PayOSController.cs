using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using HakiBaVuong.Data;
using Microsoft.EntityFrameworkCore;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Security.Cryptography;
using Microsoft.Extensions.Configuration;

namespace HakiBaVuong.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class PayOSController : ControllerBase
    {
        private readonly DataContext _context;
        private readonly ILogger<PayOSController> _logger;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IConfiguration _configuration;

        public PayOSController(DataContext context, ILogger<PayOSController> logger, IHttpClientFactory httpClientFactory, IConfiguration configuration)
        {
            _context = context;
            _logger = logger;
            _httpClientFactory = httpClientFactory;
            _configuration = configuration;
        }

        [HttpPost("create")]
        public async Task<IActionResult> CreatePaymentLink([FromBody] CreatePaymentDTO model)
        {
            _logger.LogInformation("Gọi CreatePaymentLink với OrderId: {OrderId}", model.OrderId);

            var customerId = GetCustomerId();
            if (customerId == null)
            {
                _logger.LogWarning("CustomerId không hợp lệ từ token");
                return Unauthorized(new { message = "Token không hợp lệ." });
            }
            _logger.LogInformation("CustomerId từ token: {CustomerId}", customerId);

            var order = await _context.Orders
                .Include(o => o.Payment)
                .FirstOrDefaultAsync(o => o.OrderId == model.OrderId && o.CustomerId == customerId);

            if (order == null)
            {
                _logger.LogWarning("Không tìm thấy đơn hàng: OrderId {OrderId}, CustomerId {CustomerId}", model.OrderId, customerId);
                return NotFound(new { message = "Đơn hàng không tồn tại." });
            }

            _logger.LogInformation("Đã tìm thấy đơn hàng: OrderId {OrderId}, Status: {Status}, CustomerId: {CustomerId}",
                order.OrderId, order.Status, order.CustomerId);

            if (order.Status != "Chưa thanh toán")
            {
                _logger.LogWarning("Đơn hàng {OrderId} không thể thanh toán, trạng thái hiện tại: {Status}", model.OrderId, order.Status);
                return BadRequest(new { message = $"Đơn hàng không ở trạng thái Chưa thanh toán. Trạng thái hiện tại: {order.Status}" });
            }

            try
            {
                // Lấy thông tin PayOS từ appsettings.json
                var clientId = _configuration["PayOS:ClientId"];
                var apiKey = _configuration["PayOS:ApiKey"];
                var checksumKey = _configuration["PayOS:ChecksumKey"];
                var payOsApiUrl = _configuration["PayOS:endpoint"];

                // Tạo yêu cầu thanh toán PayOS
                var payOsRequest = new
                {
                    orderCode = order.OrderId,
                    amount = (int)order.TotalAmount,
                    description = $"Thanh toán đơn hàng {order.OrderId}",
                    returnUrl = "https://b1d5-14-167-171-205.ngrok-free.app/success", // URL ngrok cho thanh toán thành công
                    cancelUrl = "https://b1d5-14-167-171-205.ngrok-free.app/cancel",  // URL ngrok cho hủy thanh toán
                    buyerName = order.FullName,
                    buyerPhone = order.Phone,
                    items = order.OrderItems.Select(i => new
                    {
                        name = i.ProductName,
                        quantity = i.Quantity,
                        price = (int)i.Price
                    }).ToList()
                };

                // Tạo checksum theo tài liệu PayOS
                var rawData = $"amount={payOsRequest.amount}&cancelUrl={payOsRequest.cancelUrl}&description={payOsRequest.description}&orderCode={payOsRequest.orderCode}&returnUrl={payOsRequest.returnUrl}";
                var checksum = GenerateChecksum(rawData, checksumKey);

                var requestBody = new
                {
                    payOsRequest.orderCode,
                    payOsRequest.amount,
                    payOsRequest.description,
                    payOsRequest.returnUrl,
                    payOsRequest.cancelUrl,
                    payOsRequest.buyerName,
                    payOsRequest.buyerPhone,
                    payOsRequest.items,
                    signature = checksum
                };

                var client = _httpClientFactory.CreateClient();
                client.DefaultRequestHeaders.Add("x-client-id", clientId);
                client.DefaultRequestHeaders.Add("x-api-key", apiKey);

                var content = new StringContent(JsonSerializer.Serialize(requestBody), Encoding.UTF8, "application/json");
                var response = await client.PostAsync(payOsApiUrl, content);

                if (!response.IsSuccessStatusCode)
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    _logger.LogError("Lỗi khi gọi API PayOS cho đơn hàng {OrderId}. Status: {StatusCode}, Error: {ErrorContent}",
                        model.OrderId, response.StatusCode, errorContent);
                    return StatusCode(500, new { message = "Lỗi khi tạo liên kết thanh toán PayOS. Vui lòng thử lại." });
                }

                var responseData = await response.Content.ReadAsStringAsync();
                var payOsResponse = JsonSerializer.Deserialize<PayOsResponse>(responseData);

                if (payOsResponse == null || string.IsNullOrEmpty(payOsResponse.data?.paymentLink))
                {
                    _logger.LogError("Không nhận được paymentLink từ PayOS cho đơn hàng {OrderId}", model.OrderId);
                    return StatusCode(500, new { message = "Không thể tạo liên kết thanh toán. Phản hồi từ PayOS không hợp lệ." });
                }

                // Cập nhật trạng thái thanh toán
                order.Payment.Status = "Pending";
                order.UpdatedAt = DateTime.UtcNow;
                await _context.SaveChangesAsync();

                _logger.LogInformation("Đã tạo liên kết thanh toán cho đơn hàng {OrderId}. PaymentLink: {PaymentLink}, QRCodeUrl: {QRCodeUrl}",
                    model.OrderId, payOsResponse.data.paymentLink, payOsResponse.data.qrCode);

                return Ok(new
                {
                    paymentLink = payOsResponse.data.paymentLink,
                    qrCodeUrl = payOsResponse.data.qrCode
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi tạo liên kết thanh toán cho đơn hàng {OrderId}. Chi tiết: {Message}", model.OrderId, ex.Message);
                return StatusCode(500, new { message = "Lỗi khi tạo liên kết thanh toán. Vui lòng thử lại." });
            }
        }

        private string GenerateChecksum(string data, string checksumKey)
        {
            using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(checksumKey));
            var hashBytes = hmac.ComputeHash(Encoding.UTF8.GetBytes(data));
            return Convert.ToHexString(hashBytes).ToLower();
        }

        private int? GetCustomerId()
        {
            var customerIdClaim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            _logger.LogInformation("CustomerIdClaim từ token: {CustomerIdClaim}", customerIdClaim ?? "null");
            return int.TryParse(customerIdClaim, out int customerId) ? customerId : null;
        }
    }

    public class CreatePaymentDTO
    {
        public int OrderId { get; set; }
    }

    public class PayOsResponse
    {
        public string code { get; set; }
        public PayOsData data { get; set; }
    }

    public class PayOsData
    {
        public string paymentLink { get; set; }
        public string qrCode { get; set; }
    }
}