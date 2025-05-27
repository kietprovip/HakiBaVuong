using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using HakiBaVuong.Data;
using Microsoft.EntityFrameworkCore;
using System;
using System.Text;
using System.Threading.Tasks;
using System.Security.Cryptography;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Globalization;
using System.Numerics;


namespace HakiBaVuong.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class VNPayController : ControllerBase
    {
        private readonly DataContext _context;
        private readonly ILogger<VNPayController> _logger;
        private readonly IConfiguration _configuration;

        public VNPayController(DataContext context, ILogger<VNPayController> logger, IConfiguration configuration)
        {
            _context = context;
            _logger = logger;
            _configuration = configuration;
        }

        [HttpPost("create")]
        public async Task<IActionResult> CreateVNPayOrder([FromBody] CreateVNPayDTO model)
        {
            _logger.LogInformation("CreateVNPayOrder called with orderId {OrderId}", model.OrderId);

            var customerId = GetCustomerId();
            if (customerId == null)
            {
                return Unauthorized(new { message = "Token không hợp lệ." });
            }

            var order = await _context.Orders
                .Include(o => o.Payment)
                .FirstOrDefaultAsync(o => o.OrderId == model.OrderId && o.CustomerId == customerId);

            if (order == null)
            {
                return NotFound(new { message = "Đơn hàng không tồn tại." });
            }

            if (order.Status != "Chờ thanh toán" && order.Status != "Chưa thanh toán")
            {
                return BadRequest(new { message = "Đơn hàng không ở trạng thái có thể thanh toán." });
            }

            try
            {
                var tmnCode = _configuration["Authentication:VNPay:TmnCode"];
                var hashSecret = _configuration["Authentication:VNPay:HashSecret"];
                var vnpUrl = _configuration["Authentication:VNPay:Url"];
                var returnUrl = _configuration["Authentication:VNPay:ReturnUrl"];

                var vnpOrderId = $"ORDER_{order.OrderId}_{DateTime.Now:yyyyMMddHHmmss}";
                var amount = (long)(model.Amount * 100);
                var orderDesc = Uri.EscapeDataString(RemoveDiacritics(model.Description));
                var createDate = DateTime.Now.ToString("yyyyMMddHHmmss");
                var expireDate = DateTime.Now.AddMinutes(15).ToString("yyyyMMddHHmmss");
                var ipAddr = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "127.0.0.1";

                _logger.LogInformation("vnp_Amount: {Amount}", amount);

                var vnpParams = new Dictionary<string, string>
                {
                    { "vnp_Version", "2.1.0" },
                    { "vnp_Command", "pay" },
                    { "vnp_TmnCode", tmnCode },
                    { "vnp_Amount", amount.ToString() },
                    { "vnp_CreateDate", createDate },
                    { "vnp_ExpireDate", expireDate },
                    { "vnp_CurrCode", "VND" },
                    { "vnp_IpAddr", ipAddr },
                    { "vnp_Locale", "vn" },
                    { "vnp_OrderInfo", orderDesc },
                    { "vnp_OrderType", "billpayment" },
                    { "vnp_ReturnUrl", returnUrl },
                    { "vnp_TxnRef", vnpOrderId }
                };

                var sortedParams = vnpParams.OrderBy(kvp => kvp.Key).ToList();
                var signData = string.Join("&", sortedParams.Select(kvp => $"{kvp.Key}={Uri.EscapeDataString(kvp.Value)}"));
                _logger.LogInformation("Sign data: {SignData}", signData);
                var vnpSecureHash = ComputeHmacSha512(signData, hashSecret);
                _logger.LogInformation("Computed vnp_SecureHash: {SecureHash}", vnpSecureHash);
                vnpParams.Add("vnp_SecureHash", vnpSecureHash);

                var paymentUrl = vnpUrl + "?" + string.Join("&", vnpParams.Select(kvp => $"{kvp.Key}={Uri.EscapeDataString(kvp.Value)}"));
                order.Payment.Status = "Pending";
                await _context.SaveChangesAsync();

                _logger.LogInformation("Payment URL: {PaymentUrl}", paymentUrl);
                return Ok(new { paymentUrl });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi tạo đơn hàng VNPay");
                return StatusCode(500, new { message = "Lỗi tạo đơn hàng VNPay." });
            }
        }

        [HttpGet("callback")]
        [AllowAnonymous]
        public async Task<IActionResult> HandleVNPayCallback()
        {
            _logger.LogInformation("VNPay callback received with query: {Query}", Request.QueryString.Value);

            try
            {
                var vnpParams = Request.Query.ToDictionary(k => k.Key, k => k.Value.ToString());
                var receivedHash = vnpParams["vnp_SecureHash"];
                vnpParams.Remove("vnp_SecureHash");

                var hashSecret = _configuration["Authentication:VNPay:HashSecret"];
                var sortedParams = vnpParams.OrderBy(kvp => kvp.Key).ToList();
                var signData = string.Join("&", sortedParams.Select(kvp => $"{kvp.Key}={Uri.EscapeDataString(kvp.Value)}"));
                var computedHash = ComputeHmacSha512(signData, hashSecret);

                if (computedHash != receivedHash)
                {
                    _logger.LogWarning("Sai chữ ký callback từ VNPay. Computed: {ComputedHash}, Received: {ReceivedHash}", computedHash, receivedHash);
                    return BadRequest(new { message = "Chữ ký không hợp lệ." });
                }

                var orderIdStr = vnpParams["vnp_TxnRef"].Split('_')[1];
                if (!int.TryParse(orderIdStr, out int orderId))
                {
                    return BadRequest(new { message = "Mã đơn hàng không hợp lệ." });
                }

                var order = await _context.Orders.Include(o => o.Payment).FirstOrDefaultAsync(o => o.OrderId == orderId);
                if (order == null)
                {
                    return NotFound(new { message = "Đơn hàng không tồn tại." });
                }

                var responseCode = vnpParams["vnp_ResponseCode"];
                if (responseCode == "00")
                {
                    order.Status = "Đã thanh toán";
                    order.Payment.Status = "Completed";
                    await _context.SaveChangesAsync();
                    return Redirect("http://localhost:5173/order-confirmation");
                }
                else
                {
                    order.Status = "Thanh toán thất bại";
                    order.Payment.Status = "Failed";
                    await _context.SaveChangesAsync();

                    return Redirect("http://localhost:5173/");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi xử lý callback VNPay");
                return StatusCode(500, new { message = "Lỗi xử lý callback VNPay." });
            }
        }

        private string ComputeHmacSha512(string data, string key)
        {
            using (var hmac = new HMACSHA512(Encoding.UTF8.GetBytes(key)))
            {
                var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(data));
                return BitConverter.ToString(hash).Replace("-", "").ToLower();
            }
        }

        private string RemoveDiacritics(string text)
        {
            var normalizedString = text.Normalize(NormalizationForm.FormD);
            var stringBuilder = new StringBuilder();

            foreach (var c in normalizedString)
            {
                var unicodeCategory = CharUnicodeInfo.GetUnicodeCategory(c);
                if (unicodeCategory != UnicodeCategory.NonSpacingMark)
                {
                    stringBuilder.Append(c);
                }
            }

            return stringBuilder.ToString().Normalize(NormalizationForm.FormC);
        }

        private int? GetCustomerId()
        {
            var customerIdClaim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            return int.TryParse(customerIdClaim, out int customerId) ? customerId : null;
        }
    }

    public class CreateVNPayDTO
    {
        public int OrderId { get; set; }
        public decimal Amount { get; set; }
        public string Description { get; set; }
    }
}