using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using BCrypt.Net;
using HakiBaVuong.Models;
using HakiBaVuong.Data;
using HakiBaVuong.DTOs;
using System.Net.Mail;
using System.Net;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.AspNetCore.Authentication;

namespace HakiBaVuong.Controllers
{
    [Route("api/shipper-auth")]
    [ApiController]
    public class ShipperAuthController : ControllerBase
    {
        private readonly DataContext _context;
        private readonly IConfiguration _config;
        private readonly IDistributedCache _cache;
        private readonly ILogger<ShipperAuthController> _logger;

        public ShipperAuthController(DataContext context, IConfiguration config, IDistributedCache cache, ILogger<ShipperAuthController> logger)
        {
            _context = context;
            _config = config;
            _cache = cache;
            _logger = logger;
        }


        [HttpPost("register")]
        public async Task<IActionResult> Register([FromBody] ShipperRegisterDTO model)
        {
            if (model == null || string.IsNullOrWhiteSpace(model.Email) || string.IsNullOrWhiteSpace(model.Password))
                return BadRequest(new { message = "Vui lòng nhập đầy đủ email và mật khẩu." });

            if (await _context.Shippers.AnyAsync(s => s.Email == model.Email))
                return BadRequest(new { message = "Email đã được sử dụng bởi shipper khác." });


            var otp = new Random().Next(100000, 999999).ToString();
            var otpExpiry = DateTime.UtcNow.AddMinutes(10);

  
            await _cache.SetStringAsync($"shipper_otp_{model.Email}", otp, new DistributedCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(10)
            });

            await SendOtpEmail(model.Email, otp);


            var tempData = System.Text.Json.JsonSerializer.Serialize(model);
            await _cache.SetStringAsync($"shipper_register_{model.Email}", tempData, new DistributedCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(15)
            });

            _logger.LogInformation("Gửi OTP đăng ký shipper đến: {Email}", model.Email);

            return Ok(new { message = "OTP đã được gửi đến email của bạn!", email = model.Email });
        }


        [HttpPost("verify-otp-register")]
        public async Task<IActionResult> VerifyOtpRegister([FromBody] VerifyOtpDTO model)
        {
            var storedOtp = await _cache.GetStringAsync($"shipper_otp_{model.Email}");
            if (storedOtp != model.Otp)
                return BadRequest(new { message = "Mã OTP không đúng hoặc đã hết hạn." });

            var tempDataJson = await _cache.GetStringAsync($"shipper_register_{model.Email}");
            if (string.IsNullOrEmpty(tempDataJson))
                return BadRequest(new { message = "Phiên đăng ký đã hết hạn." });

            var registerData = System.Text.Json.JsonSerializer.Deserialize<ShipperRegisterDTO>(tempDataJson);

            var shipper = new Shipper
            {
                Name = registerData.Name ?? registerData.Email.Split('@')[0],
                Email = registerData.Email,
                Password = BCrypt.Net.BCrypt.EnhancedHashPassword(registerData.Password),
                Phone = registerData.Phone ?? "",
                VehicleType = registerData.VehicleType ?? "Xe máy",
                LicensePlate = registerData.LicensePlate ?? "",
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            _context.Shippers.Add(shipper);
            await _context.SaveChangesAsync();

  
            await _cache.RemoveAsync($"shipper_otp_{model.Email}");
            await _cache.RemoveAsync($"shipper_register_{model.Email}");

            var token = GenerateJwtToken(shipper);

            _logger.LogInformation("Shipper đăng ký thành công: {Email}", model.Email);

            return Ok(new
            {
                message = "Đăng ký thành công! Chào mừng shipper mới!",
                token,
                shipperId = shipper.ShipperId,
                name = shipper.Name
            });
        }

        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] ShipperLoginDTO model)
        {
            var shipper = await _context.Shippers.FirstOrDefaultAsync(s => s.Email == model.Email);
            if (shipper == null || !BCrypt.Net.BCrypt.EnhancedVerify(model.Password, shipper.Password))
                return Unauthorized(new { message = "Email hoặc mật khẩu không đúng." });

            var token = GenerateJwtToken(shipper);
            return Ok(new
            {
                message = "Đăng nhập thành công!",
                token,
                shipperId = shipper.ShipperId,
                name = shipper.Name,
                rating = shipper.Rating
            });
        }

        [HttpPost("forgot-password")]
        public async Task<IActionResult> ForgotPassword([FromBody] ForgotPasswordDTO model)
        {
            var shipper = await _context.Shippers.FirstOrDefaultAsync(s => s.Email == model.Email);
            if (shipper == null)
                return Ok(new { message = "Nếu email tồn tại, chúng tôi đã gửi mã OTP đến bạn." }); // Không tiết lộ tồn tại

            var otp = new Random().Next(100000, 999999).ToString();
            await _cache.SetStringAsync($"shipper_reset_otp_{model.Email}", otp, new DistributedCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(10)
            });

            await SendOtpEmail(model.Email, otp, isReset: true);

            return Ok(new { message = "Mã OTP đã được gửi đến email của bạn!" });
        }

        [HttpPost("reset-password")]
        public async Task<IActionResult> ResetPassword([FromBody] ResetPasswordDTO model)
        {
            var storedOtp = await _cache.GetStringAsync($"shipper_reset_otp_{model.Email}");
            if (storedOtp != model.Otp)
                return BadRequest(new { message = "Mã OTP không đúng hoặc đã hết hạn." });

            var shipper = await _context.Shippers.FirstOrDefaultAsync(s => s.Email == model.Email);
            if (shipper == null)
                return NotFound(new { message = "Không tìm thấy tài khoản." });

            shipper.Password = BCrypt.Net.BCrypt.EnhancedHashPassword(model.NewPassword);
            shipper.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();
            await _cache.RemoveAsync($"shipper_reset_otp_{model.Email}");

            return Ok(new { message = "Đổi mật khẩu thành công! Bạn có thể đăng nhập lại." });
        }

      
        [HttpGet("login-google")]
        public IActionResult LoginWithGoogle()
        {
            var redirectUrl = Url.Action("GoogleResponse", "ShipperAuth", null, Request.Scheme);
            var properties = new Microsoft.AspNetCore.Authentication.AuthenticationProperties { RedirectUri = redirectUrl };
            return Challenge(properties, "Google");
        }

        [HttpGet("google-response")]
        public async Task<IActionResult> GoogleResponse()
        {
            var result = await HttpContext.AuthenticateAsync(Microsoft.AspNetCore.Authentication.Cookies.CookieAuthenticationDefaults.AuthenticationScheme);

            if (!result.Succeeded)
                return BadRequest(new { message = "Xác thực Google thất bại." });

            var email = result.Principal.FindFirst(ClaimTypes.Email)?.Value;
            var name = result.Principal.FindFirst(ClaimTypes.Name)?.Value;

            if (string.IsNullOrEmpty(email))
                return BadRequest(new { message = "Không lấy được email từ Google." });

            var shipper = await _context.Shippers.FirstOrDefaultAsync(s => s.Email == email);

            if (shipper == null)
            {
                shipper = new Shipper
                {
                    Name = name ?? "Shipper Google",
                    Email = email,
                    Password = BCrypt.Net.BCrypt.EnhancedHashPassword(Guid.NewGuid().ToString()),
                    Phone = "",
                    VehicleType = "Xe máy",
                    LicensePlate = "",
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };
                _context.Shippers.Add(shipper);
                await _context.SaveChangesAsync();
            }

            var token = GenerateJwtToken(shipper);

            // Redirect về trang shipper
            var redirectUrl = $"http://localhost:5174/shipper-dashboard?token={Uri.EscapeDataString(token)}&shipperId={shipper.ShipperId}";
            return Redirect(redirectUrl);
        }

        private async Task SendOtpEmail(string toEmail, string otp, bool isReset = false)
        {
            var fromAddress = new MailAddress(_config["Email:From"], "HakiBaVuong Shipper");
            var toAddress = new MailAddress(toEmail);
            var subject = isReset ? "Đặt lại mật khẩu tài khoản Shipper" : "Xác minh tài khoản Shipper HakiBaVuong";
            var body = isReset
                ? $"<h3>Mã OTP đặt lại mật khẩu của bạn là:</h3><h1 style='color:#e91e63'>{otp}</h1><p>Hiệu lực trong 10 phút.</p>"
                : $"<h3>Xin chào shipper mới!</h3><p>Mã OTP xác minh tài khoản của bạn là:</p><h1 style='color:#e91e63'>{otp}</h1><p>Hiệu lực trong 10 phút.</p>";

            var smtp = new SmtpClient
            {
                Host = _config["Email:Host"],
                Port = int.Parse(_config["Email:Port"]),
                EnableSsl = true,
                DeliveryMethod = SmtpDeliveryMethod.Network,
                UseDefaultCredentials = false,
                Credentials = new NetworkCredential(_config["Email:Username"], _config["Email:Password"])
            };

            using var message = new MailMessage(fromAddress, toAddress)
            {
                Subject = subject,
                Body = body,
                IsBodyHtml = true
            };

            await smtp.SendMailAsync(message);
        }


        private string GenerateJwtToken(Shipper shipper)
        {
            var claims = new[]
            {
                new Claim(ClaimTypes.NameIdentifier, shipper.ShipperId.ToString()),
                new Claim(ClaimTypes.Email, shipper.Email),
                new Claim(ClaimTypes.Name, shipper.Name),
                new Claim(ClaimTypes.Role, "Shipper")
            };

            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_config["Jwt:Key"]!));
            var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

            var token = new JwtSecurityToken(
                issuer: _config["Jwt:Issuer"],
                audience: _config["Jwt:Audience"],
                claims: claims,
                expires: DateTime.Now.AddDays(7),
                signingCredentials: creds
            );

            return new JwtSecurityTokenHandler().WriteToken(token);
        }
    }
}