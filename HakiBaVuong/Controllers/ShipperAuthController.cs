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
using AutoMapper;
using System.Net.Mail;
using System.Net;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;

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
        private readonly IMapper _mapper;

        public ShipperAuthController(
            DataContext context,
            IConfiguration config,
            IDistributedCache cache,
            ILogger<ShipperAuthController> logger,
            IMapper mapper)
        {
            _context = context;
            _config = config;
            _cache = cache;
            _logger = logger;
            _mapper = mapper;
        }

        [HttpPost("register")]
        public async Task<IActionResult> Register([FromBody] ShipperRegisterDTO model)
        {
            _logger.LogInformation("Shipper register called for email: {Email}", model?.Email);

            if (model == null || string.IsNullOrEmpty(model.Email) || string.IsNullOrEmpty(model.Password) || string.IsNullOrEmpty(model.ConfirmPassword))
            {
                _logger.LogWarning("Invalid input data: Email={Email}", model?.Email ?? "null");
                return BadRequest(new { message = "Dữ liệu đầu vào không hợp lệ." });
            }

            if (await _context.Shippers.AnyAsync(s => s.Email == model.Email))
            {
                _logger.LogWarning("Email already exists: {Email}", model.Email);
                return BadRequest(new { message = "Email đã tồn tại." });
            }

            if (model.Password != model.ConfirmPassword)
            {
                _logger.LogWarning("Password confirmation mismatch for email: {Email}", model.Email);
                return BadRequest(new { message = "Mật khẩu xác nhận không khớp." });
            }

            if (model.Password.Length < 6)
            {
                _logger.LogWarning("Password too short for email: {Email}", model.Email);
                return BadRequest(new { message = "Mật khẩu phải có ít nhất 6 ký tự." });
            }

            var shipper = new Shipper
            {
                Name = model.Name ?? model.Email.Split('@')[0],
                Email = model.Email,
                Password = BCrypt.Net.BCrypt.EnhancedHashPassword(model.Password),
                Phone = model.Phone ?? "",
                VehicleType = model.VehicleType ?? "Xe máy",
                LicensePlate = model.LicensePlate ?? "",
                CreatedAt = DateTime.UtcNow,
                IsEmailVerified = false
            };

            try
            {
                _context.Shippers.Add(shipper);
                await _context.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error saving shipper to database: {Email}", model.Email);
                return StatusCode(500, new { message = "Lỗi server khi lưu thông tin shipper." });
            }

            string otp = GenerateOtp();
            await StoreOtpInCache(shipper.Email, otp, "RegisterOTP");
            _logger.LogInformation("Generated OTP for {Email}: {Otp}", shipper.Email, otp);

            try
            {
                await SendOtpEmail(shipper.Email, otp, "Xác thực email đăng ký");
                _logger.LogInformation("Sent OTP email to {Email}", shipper.Email);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending OTP email to {Email}", shipper.Email);
                return StatusCode(500, new { message = "Đăng ký thành công nhưng không thể gửi email OTP. Vui lòng thử lại sau." });
            }

            return Ok(new { message = "Đăng ký thành công. Vui lòng kiểm tra email để xác thực." });
        }

        [HttpPost("verify-email")]
        public async Task<IActionResult> VerifyEmail([FromBody] VerifyOtpDTO model)
        {
            _logger.LogInformation("Verify email called for email: {Email}, OTP: {Otp}", model.Email, model.Otp);

            var shipper = await _context.Shippers.FirstOrDefaultAsync(s => s.Email == model.Email && !s.IsEmailVerified);
            if (shipper == null)
            {
                _logger.LogWarning("Email not found or already verified: {Email}", model.Email);
                return BadRequest(new { message = "Email không tồn tại hoặc đã được xác thực." });
            }

            var cachedOtp = await _cache.GetStringAsync($"RegisterOTP_{model.Email}");
            if (string.IsNullOrEmpty(cachedOtp) || cachedOtp.Trim() != model.Otp.Trim())
            {
                _logger.LogWarning("Invalid or expired OTP for email: {Email}, Received: {Received}, Cached: {Cached}",
                    model.Email, model.Otp, cachedOtp);
                return BadRequest(new { message = "Mã OTP không đúng hoặc đã hết hạn." });
            }

            shipper.IsEmailVerified = true;
            _context.Shippers.Update(shipper);
            await _cache.RemoveAsync($"RegisterOTP_{model.Email}");
            await _context.SaveChangesAsync();

            _logger.LogInformation("Email verified successfully for {Email}", model.Email);
            return Ok(new { message = "Xác thực email thành công. Bạn có thể đăng nhập." });
        }

        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] ShipperLoginDTO model)
        {
            _logger.LogInformation("Shipper login called for email: {Email}", model.Email);

            var shipper = await _context.Shippers.FirstOrDefaultAsync(s => s.Email == model.Email);
            if (shipper == null || !BCrypt.Net.BCrypt.EnhancedVerify(model.Password, shipper.Password))
            {
                _logger.LogWarning("Invalid credentials for email: {Email}", model.Email);
                return Unauthorized(new { message = "Sai email hoặc mật khẩu." });
            }

            if (!shipper.IsEmailVerified)
            {
                _logger.LogWarning("Email not verified for email: {Email}", model.Email);
                return BadRequest(new { message = "Email chưa được xác thực." });
            }

            var token = GenerateJwtToken(shipper);
            _logger.LogInformation("Login successful for email: {Email}", model.Email);
            return Ok(new
            {
                message = "Đăng nhập thành công.",
                token,
                shipperId = shipper.ShipperId,
                name = shipper.Name,
                rating = shipper.Rating
            });
        }

        [HttpPost("forgot-password")]
        public async Task<IActionResult> ForgotPassword([FromBody] ForgotPasswordDTO model)
        {
            _logger.LogInformation("Forgot password called for email: {Email}", model.Email);

            var shipper = await _context.Shippers.FirstOrDefaultAsync(s => s.Email == model.Email);
            if (shipper == null)
            {
                _logger.LogWarning("Email not found: {Email}", model.Email);
                return BadRequest(new { message = "Email không tồn tại." });
            }

            string otp = GenerateOtp();
            await StoreOtpInCache(shipper.Email, otp, "ResetPasswordOTP");
            _logger.LogInformation("Generated OTP for password reset: {Otp} for {Email}", otp, shipper.Email);

            await SendOtpEmail(shipper.Email, otp, "Mã OTP đặt lại mật khẩu");
            _logger.LogInformation("Sent OTP email for password reset to {Email}", shipper.Email);

            return Ok(new { message = "Mã OTP đã được gửi đến email của bạn." });
        }

        [HttpPost("reset-password")]
        public async Task<IActionResult> ResetPassword([FromBody] ResetPasswordDTO model)
        {
            _logger.LogInformation("Reset password called for email: {Email}", model.Email);

            var shipper = await _context.Shippers.FirstOrDefaultAsync(s => s.Email == model.Email);
            if (shipper == null)
            {
                _logger.LogWarning("Email not found: {Email}", model.Email);
                return BadRequest(new { message = "Email không tồn tại." });
            }

            var cachedOtp = await _cache.GetStringAsync($"ResetPasswordOTP_{model.Email}");
            if (string.IsNullOrEmpty(cachedOtp) || cachedOtp.Trim() != model.Otp.Trim())
            {
                _logger.LogWarning("Invalid or expired OTP for email: {Email}, Received: {Received}, Cached: {Cached}",
                    model.Email, model.Otp, cachedOtp);
                return BadRequest(new { message = "Mã OTP không đúng hoặc đã hết hạn." });
            }

            if (model.NewPassword != model.ConfirmPassword)
            {
                _logger.LogWarning("Password confirmation mismatch for email: {Email}", model.Email);
                return BadRequest(new { message = "Mật khẩu xác nhận không khớp." });
            }
            if (model.NewPassword.Length < 6)
            {
                _logger.LogWarning("New password too short for email: {Email}", model.Email);
                return BadRequest(new { message = "Mật khẩu phải có ít nhất 6 ký tự." });
            }

            shipper.Password = BCrypt.Net.BCrypt.EnhancedHashPassword(model.NewPassword);
            _context.Shippers.Update(shipper);
            await _cache.RemoveAsync($"ResetPasswordOTP_{model.Email}");
            await _context.SaveChangesAsync();

            _logger.LogInformation("Password reset successful for {Email}", model.Email);
            return Ok(new { message = "Đặt lại mật khẩu thành công." });
        }

        [HttpGet("user-info")]
        [Authorize]
        public async Task<IActionResult> GetShipperInfo()
        {
            var shipperIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(shipperIdClaim) || !int.TryParse(shipperIdClaim, out int shipperId))
            {
                _logger.LogWarning("Shipper ID not found or invalid in token.");
                return Unauthorized(new { message = "Token không hợp lệ." });
            }

            var shipper = await _context.Shippers.FirstOrDefaultAsync(s => s.ShipperId == shipperId);
            if (shipper == null)
            {
                _logger.LogWarning("Shipper not found with ID: {ShipperId}", shipperId);
                return NotFound(new { message = "Không tìm thấy shipper." });
            }

            return Ok(new
            {
                shipperId = shipper.ShipperId,
                email = shipper.Email,
                name = shipper.Name,
                phone = shipper.Phone,
                vehicleType = shipper.VehicleType,
                licensePlate = shipper.LicensePlate,
                rating = shipper.Rating
            });
        }

        [HttpGet("login-google")]
        public IActionResult LoginWithGoogle()
        {
            _logger.LogInformation("Initiating Google login");
            var redirectUrl = Url.Action("GoogleResponse", "ShipperAuth", null, Request.Scheme);
            var properties = new AuthenticationProperties { RedirectUri = redirectUrl };
            return Challenge(properties, "Google");
        }

        [HttpGet("google-response")]
        public async Task<IActionResult> GoogleResponse()
        {
            _logger.LogInformation("Processing Google response");
            var authenticateResult = await HttpContext.AuthenticateAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            if (!authenticateResult.Succeeded)
            {
                _logger.LogWarning("Google authentication failed");
                return Unauthorized(new { message = "Xác thực Google thất bại." });
            }

            var email = authenticateResult.Principal.FindFirst(ClaimTypes.Email)?.Value;
            if (string.IsNullOrEmpty(email))
            {
                _logger.LogWarning("No email found in Google response");
                return BadRequest(new { message = "Không tìm thấy email từ Google." });
            }

            var shipper = await _context.Shippers.FirstOrDefaultAsync(s => s.Email == email);
            if (shipper == null)
            {
                shipper = new Shipper
                {
                    Name = authenticateResult.Principal.FindFirst(ClaimTypes.Name)?.Value ?? "Shipper Google",
                    Email = email,
                    Password = BCrypt.Net.BCrypt.EnhancedHashPassword(Guid.NewGuid().ToString()),
                    Phone = "",
                    VehicleType = "Xe máy",
                    LicensePlate = "",
                    CreatedAt = DateTime.UtcNow,
                    IsEmailVerified = true
                };
                _context.Shippers.Add(shipper);
                await _context.SaveChangesAsync();
                _logger.LogInformation("New shipper registered via Google: {Email}", email);
            }

            var token = GenerateJwtToken(shipper);
            _logger.LogInformation("Google login successful for email: {Email}", email);

            return Redirect($"http://localhost:5174/shipper-dashboard?token={Uri.EscapeDataString(token)}&shipperId={shipper.ShipperId}");
        }

        [HttpGet("login-facebook")]
        public IActionResult LoginWithFacebook()
        {
            _logger.LogInformation("Initiating Facebook login");
            var redirectUrl = Url.Action("FacebookResponse", "ShipperAuth", null, Request.Scheme);
            var properties = new AuthenticationProperties { RedirectUri = redirectUrl };
            return Challenge(properties, "Facebook");
        }

        [HttpGet("facebook-response")]
        public async Task<IActionResult> FacebookResponse()
        {
            _logger.LogInformation("Processing Facebook response");
            var authenticateResult = await HttpContext.AuthenticateAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            if (!authenticateResult.Succeeded)
            {
                _logger.LogWarning("Facebook authentication failed: {Error}", authenticateResult.Failure?.Message);
                return Unauthorized(new { message = "Xác thực Facebook thất bại.", error = authenticateResult.Failure?.Message });
            }

            var email = authenticateResult.Principal.FindFirst(ClaimTypes.Email)?.Value;
            if (string.IsNullOrEmpty(email))
            {
                _logger.LogWarning("No email found in Facebook response");
                return BadRequest(new { message = "Không tìm thấy email từ Facebook." });
            }

            var shipper = await _context.Shippers.FirstOrDefaultAsync(s => s.Email == email);
            if (shipper == null)
            {
                shipper = new Shipper
                {
                    Name = authenticateResult.Principal.FindFirst(ClaimTypes.Name)?.Value ?? "Shipper Facebook",
                    Email = email,
                    Password = BCrypt.Net.BCrypt.EnhancedHashPassword(Guid.NewGuid().ToString()),
                    Phone = "",
                    VehicleType = "Xe máy",
                    LicensePlate = "",
                    CreatedAt = DateTime.UtcNow,
                    IsEmailVerified = true
                };
                _context.Shippers.Add(shipper);
                await _context.SaveChangesAsync();
                _logger.LogInformation("New shipper registered via Facebook: {Email}", email);
            }

            var token = GenerateJwtToken(shipper);
            _logger.LogInformation("Facebook login successful for email: {Email}", email);

            return Redirect($"http://localhost:5174/shipper-dashboard?token={Uri.EscapeDataString(token)}&shipperId={shipper.ShipperId}");
        }

        private string GenerateOtp()
        {
            return new Random().Next(100000, 999999).ToString();
        }

        private async Task StoreOtpInCache(string email, string otp, string otpType)
        {
            var cacheKey = $"{otpType}_{email}";
            var cacheOptions = new DistributedCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(30)
            };
            await _cache.SetStringAsync(cacheKey, otp, cacheOptions);
        }

        private async Task SendOtpEmail(string email, string otp, string subject)
        {
            var fromAddress = new MailAddress("dabada911@gmail.com", "Shop Haki Bá Vương");
            var toAddress = new MailAddress(email);
            const string fromPassword = "cpixzanizbhrovko";
            string body = $"Mã OTP của bạn là: <strong>{otp}</strong>. Vui lòng sử dụng mã này để hoàn tất quá trình.";

            var smtp = new SmtpClient
            {
                Host = "smtp.gmail.com",
                Port = 587,
                EnableSsl = true,
                DeliveryMethod = SmtpDeliveryMethod.Network,
                UseDefaultCredentials = false,
                Credentials = new NetworkCredential(fromAddress.Address, fromPassword)
            };

            using (var message = new MailMessage(fromAddress, toAddress)
            {
                Subject = subject,
                Body = body,
                IsBodyHtml = true
            })
            {
                await smtp.SendMailAsync(message);
            }
        }

        private string GenerateJwtToken(Shipper shipper)
        {
            var key = Encoding.UTF8.GetBytes(_config["Jwt:Key"]);
            var tokenHandler = new JwtSecurityTokenHandler();
            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, shipper.ShipperId.ToString()),
                new Claim(ClaimTypes.Email, shipper.Email),
                new Claim(ClaimTypes.Role, "Shipper")
            };

            var tokenDescriptor = new SecurityTokenDescriptor
            {
                Subject = new ClaimsIdentity(claims),
                Expires = DateTime.UtcNow.AddHours(3),
                Issuer = _config["Jwt:Issuer"],
                Audience = _config["Jwt:Audience"],
                SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(key), SecurityAlgorithms.HmacSha256Signature)
            };

            var token = tokenHandler.CreateToken(tokenDescriptor);
            return tokenHandler.WriteToken(token);
        }
    }
}