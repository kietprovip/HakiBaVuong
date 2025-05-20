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

[Route("api/auth")]
[ApiController]
public class AuthController : ControllerBase
{
    private readonly DataContext _context;
    private readonly IConfiguration _config;
    private readonly IMapper _mapper;
    private readonly ILogger<AuthController> _logger;
    private readonly IDistributedCache _cache;

    public AuthController(
        DataContext context,
        IConfiguration config,
        IMapper mapper,
        ILogger<AuthController> logger,
        IDistributedCache cache)
    {
        _context = context;
        _config = config;
        _mapper = mapper;
        _logger = logger;
        _cache = cache;

        InitializeDefaultAdminAndBrand();
    }

    private void InitializeDefaultAdminAndBrand()
    {
        if (!_context.Users.Any(u => u.Email == "admin@example.com"))
        {
            var adminUser = new User
            {
                Name = "Admin",
                Email = "admin@example.com",
                Password = BCrypt.Net.BCrypt.EnhancedHashPassword("Admin@123"),
                Role = "Admin",
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
                IsEmailVerified = true,
                BrandId = null
            };

            _context.Users.Add(adminUser);
            _context.SaveChanges();
            _logger.LogInformation("Tài khoản admin mặc định đã được tạo: admin@example.com");

            var defaultBrand = new Brand
            {
                Name = "Chưa có brand",
                OwnerId = adminUser.UserId,
                CreatedAt = DateTime.UtcNow
            };

            _context.Brands.Add(defaultBrand);
            _context.SaveChanges();
            _logger.LogInformation("Brand mặc định 'Chưa có brand' đã được tạo với OwnerId: {OwnerId}", adminUser.UserId);
        }
    }

    [HttpGet("brands")]
    public async Task<ActionResult<IEnumerable<Brand>>> GetBrands()
    {
        try
        {
            _logger.LogInformation("Gọi API lấy danh sách thương hiệu từ AuthController");
            var brands = await _context.Brands.ToListAsync();
            return Ok(brands);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Lỗi khi lấy danh sách thương hiệu");
            return StatusCode(500, new { message = $"Lỗi server: {ex.Message}" });
        }
    }

    [HttpPost("register")]
    public async Task<IActionResult> Register([FromBody] RegisterDTO model)
    {
        _logger.LogInformation("Đăng ký được gọi cho email: {Email}", model?.Email);

        if (model == null || string.IsNullOrEmpty(model.Email) || string.IsNullOrEmpty(model.Password) || string.IsNullOrEmpty(model.ConfirmPassword))
        {
            _logger.LogWarning("Dữ liệu đầu vào không hợp lệ: Email={Email}",
                model?.Email ?? "null");
            return BadRequest(new { message = "Dữ liệu đầu vào không hợp lệ." });
        }

        if (await _context.Users.AnyAsync(u => u.Email == model.Email))
        {
            _logger.LogWarning("Email đã tồn tại: {Email}", model.Email);
            return BadRequest(new { message = "Email đã tồn tại." });
        }

        if (model.Password != model.ConfirmPassword)
        {
            _logger.LogWarning("Xác nhận mật khẩu không khớp cho email: {Email}", model.Email);
            return BadRequest(new { message = "Mật khẩu xác nhận không khớp." });
        }

        if (model.Password.Length < 6)
        {
            _logger.LogWarning("Mật khẩu quá ngắn cho email: {Email}", model.Email);
            return BadRequest(new { message = "Mật khẩu phải có ít nhất 6 ký tự." });
        }

        var user = _mapper.Map<User>(model);
        user.Password = BCrypt.Net.BCrypt.EnhancedHashPassword(model.Password);
        user.CreatedAt = DateTime.UtcNow;
        user.IsEmailVerified = false;
        user.Role = "Staff";
        user.BrandId = null; // Mặc định BrandId là NULL

        try
        {
            _context.Users.Add(user);
            await _context.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Lỗi khi lưu user vào database: {Email}", model.Email);
            return StatusCode(500, new { message = "Lỗi server khi lưu thông tin user." });
        }

        string otp = GenerateOtp();
        await StoreOtpInCache(user.Email, otp, "RegisterOTP");
        _logger.LogInformation("Đã tạo OTP cho {Email}: {Otp}", user.Email, otp);

        try
        {
            await SendOtpEmail(user.Email, otp, "Xác thực email đăng ký");
            _logger.LogInformation("Đã gửi email OTP đến {Email}", user.Email);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Lỗi khi gửi email OTP đến {Email}", user.Email);
            return StatusCode(500, new { message = "Đăng ký thành công nhưng không thể gửi email OTP. Vui lòng thử lại sau." });
        }

        return Ok(new { message = "Đăng ký thành công. Vui lòng kiểm tra email để xác thực." });
    }

    [HttpPost("verify-email")]
    public async Task<IActionResult> VerifyEmail([FromBody] VerifyOtpDTO model)
    {
        _logger.LogInformation("Xác thực email được gọi cho email: {Email}, OTP: {Otp}", model.Email, model.Otp);

        var user = await _context.Users.FirstOrDefaultAsync(u => u.Email == model.Email && !u.IsEmailVerified);
        if (user == null)
        {
            _logger.LogWarning("Email không tồn tại hoặc đã được xác thực: {Email}", model.Email);
            return BadRequest(new { message = "Email không tồn tại hoặc đã được xác thực." });
        }

        var cachedOtp = await _cache.GetStringAsync($"RegisterOTP_{model.Email}");
        if (string.IsNullOrEmpty(cachedOtp) || cachedOtp.Trim() != model.Otp.Trim())
        {
            _logger.LogWarning("OTP không hợp lệ hoặc đã hết hạn cho email: {Email}, Nhận được: {Received}, Đã lưu: {Cached}",
                model.Email, model.Otp, cachedOtp);
            return BadRequest(new { message = "Mã OTP không đúng hoặc đã hết hạn." });
        }

        user.IsEmailVerified = true;
        _context.Users.Update(user);
        await _cache.RemoveAsync($"RegisterOTP_{model.Email}");
        await _context.SaveChangesAsync();

        _logger.LogInformation("Email đã được xác thực thành công cho {Email}", model.Email);
        return Ok(new { message = "Xác thực email thành công. Bạn có thể đăng nhập." });
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginDTO model)
    {
        _logger.LogInformation("Đăng nhập được gọi cho email: {Email}", model.Email);

        var user = await _context.Users.FirstOrDefaultAsync(u => u.Email == model.Email);
        if (user == null || !BCrypt.Net.BCrypt.EnhancedVerify(model.Password, user.Password))
        {
            _logger.LogWarning("Thông tin đăng nhập không hợp lệ cho email: {Email}", model.Email);
            return Unauthorized(new { message = "Sai email hoặc mật khẩu." });
        }

        if (!user.IsEmailVerified)
        {
            _logger.LogWarning("Email chưa được xác thực cho email: {Email}", model.Email);
            return BadRequest(new { message = "Email chưa được xác thực." });
        }

        var token = GenerateJwtToken(user);
        _logger.LogInformation("Đăng nhập thành công cho email: {Email}", model.Email);
        return Ok(new
        {
            message = "Đăng nhập thành công.",
            token,
            userId = user.UserId,
            role = user.Role,
            brandId = user.BrandId
        });
    }

    [HttpPost("forgot-password")]
    public async Task<IActionResult> ForgotPassword([FromBody] ForgotPasswordDTO model)
    {
        _logger.LogInformation("Quên mật khẩu được gọi cho email: {Email}", model.Email);

        var user = await _context.Users.FirstOrDefaultAsync(u => u.Email == model.Email);
        if (user == null)
        {
            _logger.LogWarning("Email không tồn tại: {Email}", model.Email);
            return BadRequest(new { message = "Email không tồn tại." });
        }

        string otp = GenerateOtp();
        await StoreOtpInCache(user.Email, otp, "ResetPasswordOTP");
        _logger.LogInformation("Đã tạo OTP để đặt lại mật khẩu: {Otp} cho {Email}", otp, user.Email);

        await SendOtpEmail(user.Email, otp, "Mã OTP đặt lại mật khẩu");
        _logger.LogInformation("Đã gửi email OTP để đặt lại mật khẩu đến {Email}", user.Email);

        return Ok(new { message = "Mã OTP đã được gửi đến email của bạn." });
    }

    [HttpPost("reset-password")]
    public async Task<IActionResult> ResetPassword([FromBody] ResetPasswordDTO model)
    {
        _logger.LogInformation("Đặt lại mật khẩu được gọi cho email: {Email}", model.Email);

        var user = await _context.Users.FirstOrDefaultAsync(u => u.Email == model.Email);
        if (user == null)
        {
            _logger.LogWarning("Email không tồn tại: {Email}", model.Email);
            return BadRequest(new { message = "Email không tồn tại." });
        }

        var cachedOtp = await _cache.GetStringAsync($"ResetPasswordOTP_{model.Email}");
        if (string.IsNullOrEmpty(cachedOtp) || cachedOtp.Trim() != model.Otp.Trim())
        {
            _logger.LogWarning("OTP không hợp lệ hoặc đã hết hạn cho email: {Email}, Nhận được: {Received}, Đã lưu: {Cached}",
                model.Email, model.Otp, cachedOtp);
            return BadRequest(new { message = "Mã OTP không đúng hoặc đã hết hạn." });
        }

        if (model.NewPassword != model.ConfirmPassword)
        {
            _logger.LogWarning("Xác nhận mật khẩu không khớp cho email: {Email}", model.Email);
            return BadRequest(new { message = "Mật khẩu xác nhận không khớp." });
        }
        if (model.NewPassword.Length < 6)
        {
            _logger.LogWarning("Mật khẩu mới quá ngắn cho email: {Email}", model.Email);
            return BadRequest(new { message = "Mật khẩu phải có ít nhất 6 ký tự." });
        }

        user.Password = BCrypt.Net.BCrypt.EnhancedHashPassword(model.NewPassword);
        _context.Users.Update(user);
        await _cache.RemoveAsync($"ResetPasswordOTP_{model.Email}");
        await _context.SaveChangesAsync();

        _logger.LogInformation("Đặt lại mật khẩu thành công cho {Email}", model.Email);
        return Ok(new { message = "Đặt lại mật khẩu thành công." });
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

    private string GenerateJwtToken(User user)
    {
        var key = Encoding.UTF8.GetBytes(_config["Jwt:Key"]);
        var tokenHandler = new JwtSecurityTokenHandler();
        var claims = new List<Claim>
        {
            new Claim(ClaimTypes.NameIdentifier, user.UserId.ToString()),
            new Claim(ClaimTypes.Email, user.Email),
            new Claim(ClaimTypes.Role, user.Role)
        };

        if (user.BrandId.HasValue)
        {
            claims.Add(new Claim("BrandId", user.BrandId.Value.ToString()));
        }

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