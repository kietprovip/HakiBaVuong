using HakiBaVuong.Data;
using HakiBaVuong.DTOs;
using HakiBaVuong.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.Net.Http;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace HakiBaVuong.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class PayOSController : ControllerBase
    {
        private readonly DataContext _context;
        private readonly ILogger<PayOSController> _logger;
        private readonly HttpClient _httpClient;
        private static readonly string PayOSBaseUrl = "https://api.payos.vn/v2/payment-requests"; // Endpoint chính thức từ tài liệu
        private readonly string WebhookUrl = "https://c37f-14-167-171-205.ngrok-free.app"; // Cập nhật ngrok ID khi chạy

        public PayOSController(DataContext context, ILogger<PayOSController> logger, IHttpClientFactory httpClientFactory)
        {
            _context = context;
            _logger = logger;
            _httpClient = httpClientFactory.CreateClient();
            _httpClient.BaseAddress = new Uri(PayOSBaseUrl);
        }

        [HttpPost("create")]
        [Authorize(Roles = "Customer")]
        public async Task<ActionResult<PaymentResponseDTO>> CreatePayment([FromBody] PaymentRequestDTO request)
        {
            _logger.LogInformation("CreatePayment called for order {OrderId}", request.OrderId);

            var customerId = GetCustomerId();
            if (!customerId.HasValue)
            {
                _logger.LogWarning("Invalid customerId from token");
                return Unauthorized(new { message = "Token không hợp lệ." });
            }

            var order = await _context.Orders
                .Include(o => o.Payment)
                .FirstOrDefaultAsync(o => o.OrderId == request.OrderId && o.CustomerId == customerId.Value);
            if (order == null)
            {
                _logger.LogWarning("Order {OrderId} not found or not owned by customer {CustomerId}", request.OrderId, customerId);
                return NotFound(new { message = "Đơn hàng không tồn tại." });
            }

            if (order.Status != "Chờ thanh toán")
            {
                _logger.LogWarning("Order {OrderId} has invalid status: {Status}", request.OrderId, order.Status);
                return BadRequest(new { message = "Đơn hàng không ở trạng thái chờ thanh toán." });
            }

            if (order.Payment != null)
            {
                _logger.LogWarning("Order {OrderId} already has a payment", request.OrderId);
                return BadRequest(new { message = "Đơn hàng đã có thông tin thanh toán." });
            }

            try
            {
                var orderCode = (int)(DateTime.UtcNow.Ticks % 100000000); // Unique PayOS order code
                var payOSRequest = new
                {
                    orderCode,
                    amount = (int)(order.TotalAmount * 100), // PayOS yêu cầu amount là số tiền nhỏ nhất (VND, nhân 100)
                    description = $"Thanh toán đơn hàng #{order.OrderId}",
                    returnUrl = "http://localhost:3000/success",
                    cancelUrl = "http://localhost:3000/cancel",
                    buyerName = order.FullName,
                    buyerPhone = order.Phone,
                    buyerEmail = order.Customer.Email, // Thêm email nếu có
                    items = await _context.OrderItems
                        .Where(oi => oi.OrderId == order.OrderId)
                        .Select(oi => new { name = oi.ProductName, quantity = oi.Quantity, price = (int)(oi.Price * 100) })
                        .ToListAsync(),
                    webhookUrl = WebhookUrl
                };

                var signature = GeneratePayOSSignature(payOSRequest, PayOS.checksumKey);
                var requestBody = new
                {
                    data = payOSRequest,
                    signature
                };

                var content = new StringContent(JsonSerializer.Serialize(requestBody), Encoding.UTF8, "application/json");
                var authToken = GenerateAuthToken(PayOS.clientId, PayOS.apiKey, PayOS.checksumKey);
                _httpClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", authToken);

                var response = await _httpClient.PostAsync("", content); // Sử dụng BaseAddress đã thiết lập

                if (!response.IsSuccessStatusCode)
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    _logger.LogError("PayOS API error: {StatusCode}, {Content}", response.StatusCode, errorContent);
                    return StatusCode(500, new { message = "Lỗi khi tạo liên kết thanh toán." });
                }

                var responseJson = await response.Content.ReadAsStringAsync();
                var payOSResponse = JsonSerializer.Deserialize<PayOSResponse>(responseJson);

                if (payOSResponse.Code != "00")
                {
                    _logger.LogError("PayOS API failed: {Code}, {Desc}", payOSResponse.Code, payOSResponse.Desc);
                    return StatusCode(500, new { message = "Lỗi từ PayOS: " + payOSResponse.Desc });
                }

                var payment = new Payment
                {
                    OrderId = order.OrderId,
                    Amount = order.TotalAmount,
                    Method = "PayOS_QR",
                    Status = "PENDING",
                    TransactionId = payOSResponse.Data.PaymentLinkId,
                    PaymentLink = payOSResponse.Data.QrCode ?? payOSResponse.Data.CheckoutUrl // Ưu tiên QR nếu có
                };

                _context.Payments.Add(payment);
                order.Status = "Đang xử lý";
                await _context.SaveChangesAsync();

                _logger.LogInformation("Created payment for order {OrderId}, paymentLinkId: {PaymentLinkId}", order.OrderId, payment.TransactionId);
                return Ok(new PaymentResponseDTO
                {
                    PaymentId = payment.PaymentId,
                    OrderId = order.OrderId,
                    Amount = payment.Amount,
                    Method = payment.Method,
                    Status = payment.Status,
                    QrCodeUrl = payment.PaymentLink,
                    PaymentLink = payOSResponse.Data.CheckoutUrl
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating payment for order {OrderId}", request.OrderId);
                return StatusCode(500, new { message = "Lỗi khi tạo thanh toán. Vui lòng thử lại sau." });
            }
        }

        [HttpPost("webhook")]
        [AllowAnonymous]
        public async Task<IActionResult> Webhook([FromBody] PayOSWebhookDTO webhook)
        {
            _logger.LogInformation("Webhook received with orderCode: {OrderCode}", webhook.Data.OrderCode);

            try
            {
                var isValidSignature = ValidateWebhookSignature(webhook, PayOS.checksumKey);
                if (!isValidSignature)
                {
                    _logger.LogWarning("Invalid webhook signature for orderCode: {OrderCode}", webhook.Data.OrderCode);
                    return BadRequest(new { message = "Chữ ký không hợp lệ." });
                }

                var payment = await _context.Payments
                    .Include(p => p.Order)
                    .FirstOrDefaultAsync(p => p.TransactionId == webhook.Data.PaymentLinkId);
                if (payment == null)
                {
                    _logger.LogWarning("Payment not found for paymentLinkId: {PaymentLinkId}", webhook.Data.PaymentLinkId);
                    return NotFound(new { message = "Thanh toán không tồn tại." });
                }

                payment.Status = webhook.Data.Status switch
                {
                    "PAID" => "COMPLETED",
                    "CANCELLED" => "FAILED",
                    _ => payment.Status
                };
                payment.UpdatedAt = DateTime.Now;

                payment.Order.Status = webhook.Data.Status switch
                {
                    "PAID" => "Đã thanh toán",
                    "CANCELLED" => "Đã hủy",
                    _ => payment.Order.Status
                };

                await _context.SaveChangesAsync();
                _logger.LogInformation("Updated payment {PaymentId} to status {Status} for order {OrderId}", payment.PaymentId, payment.Status, payment.OrderId);
                return Ok(new { message = "Webhook processed successfully." });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing webhook for orderCode: {OrderCode}", webhook.Data.OrderCode);
                return StatusCode(500, new { message = "Lỗi khi xử lý webhook." });
            }
        }

        private int? GetCustomerId()
        {
            var customerIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            return int.TryParse(customerIdClaim, out int customerId) ? customerId : null;
        }

        private string GeneratePayOSSignature(object data, string checksumKey)
        {
            var sortedJson = JsonSerializer.Serialize(data, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
            var sortedDict = JsonSerializer.Deserialize<Dictionary<string, object>>(sortedJson);
            var sortedString = string.Join("", sortedDict.OrderBy(kv => kv.Key).Select(kv => $"{kv.Key}{kv.Value}"));
            using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(checksumKey));
            var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(sortedString));
            return BitConverter.ToString(hash).Replace("-", "").ToLower();
        }

        private string GenerateAuthToken(string clientId, string apiKey, string checksumKey)
        {
            var timestamp = DateTime.UtcNow.ToString("yyyyMMddHHmmss");
            var message = $"{clientId}{apiKey}{timestamp}";
            using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(checksumKey));
            var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(message));
            var token = Convert.ToBase64String(hash);
            return token;
        }

        private bool ValidateWebhookSignature(PayOSWebhookDTO webhook, string checksumKey)
        {
            var receivedSignature = webhook.Signature;
            var data = webhook.Data;
            var sortedJson = JsonSerializer.Serialize(data, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
            var sortedDict = JsonSerializer.Deserialize<Dictionary<string, object>>(sortedJson);
            var sortedString = string.Join("", sortedDict.OrderBy(kv => kv.Key).Select(kv => $"{kv.Key}{kv.Value}"));
            using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(checksumKey));
            var computedHash = hmac.ComputeHash(Encoding.UTF8.GetBytes(sortedString));
            var computedSignature = BitConverter.ToString(computedHash).Replace("-", "").ToLower();
            return receivedSignature == computedSignature;
        }
    }

    internal class PayOSResponse
    {
        public string Code { get; set; }
        public string Desc { get; set; }
        public PayOSData Data { get; set; }
    }

    internal class PayOSData
    {
        public string PaymentLinkId { get; set; }
        public string CheckoutUrl { get; set; }
        public string QrCode { get; set; }
    }
}