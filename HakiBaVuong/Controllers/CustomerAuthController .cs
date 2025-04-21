using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using BCrypt.Net;
using HakiBaVuong.Models;
using HakiBaVuong.Data;
using Microsoft.EntityFrameworkCore;
using HakiBaVuong.DTOs;
using AutoMapper;
using System.Net.Mail;
using System.Net;
using Microsoft.Extensions.Caching.Distributed;

[Route("api/auth")]
[ApiController]
public class CustomerAuthController : ControllerBase
{
    private readonly DataContext _context;
    private readonly IConfiguration _config;
    private readonly IMapper _mapper;
    private readonly ILogger<CustomerAuthController> _logger;
    private readonly IDistributedCache _cache;

    public CustomerAuthController(
        DataContext context,
        IConfiguration config,
        IMapper mapper,
        ILogger<CustomerAuthController> logger,
        IDistributedCache cache)
    {
        _context = context;
        _config = config;
        _mapper = mapper;
        _logger = logger;
        _cache = cache;
    }

    [HttpPost("registerCustomer")]
    public async Task<IActionResult> Register([FromBody] RegisterCustomerDTO model)
    {
        _logger.LogInformation("RegisterCustomer called for email: {Email}", model.Email);

        if (await _context.Customers.AnyAsync(u => u.Email == model.Email))
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

        var customer = _mapper.Map<Customer>(model);
        customer.Password = BCrypt.Net.BCrypt.EnhancedHashPassword(model.Password);
        customer.CreatedAt = DateTime.UtcNow;
        customer.IsEmailVerified = false;

        _context.Customers.Add(customer);
        await _context.SaveChangesAsync();

        string otp = GenerateOtp();
        await StoreOtpInCache(customer.Email, otp, "RegisterOTP");
        _logger.LogInformation("Generated OTP for {Email}: {Otp}", customer.Email, otp);

        await SendOtpEmail(customer.Email, otp, "Xác thực email đăng ký");
        _logger.LogInformation("Sent OTP email to {Email}", customer.Email);

        return Ok(new { message = "Đăng ký thành công. Vui lòng kiểm tra email để xác thực." });
    }

    [HttpPost("verify-email-customer")]
    public async Task<IActionResult> VerifyEmail([FromBody] VerifyOtpDTO model)
    {
        _logger.LogInformation("VerifyEmail called for email: {Email}, OTP: {Otp}", model.Email, model.Otp);

        var customer = await _context.Customers.FirstOrDefaultAsync(u => u.Email == model.Email && !u.IsEmailVerified);
        if (customer == null)
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

        customer.IsEmailVerified = true;
        _context.Customers.Update(customer);
        await _cache.RemoveAsync($"RegisterOTP_{model.Email}");
        await _context.SaveChangesAsync();

        _logger.LogInformation("Email verified successfully for {Email}", model.Email);
        return Ok(new { message = "Xác thực email thành công. Bạn có thể đăng nhập." });
    }

    [HttpPost("loginCustomer")]
    public async Task<IActionResult> Login([FromBody] LoginCustomerDTO model)
    {
        _logger.LogInformation("LoginCustomer called for email: {Email}", model.Email);

        var customer = await _context.Customers.FirstOrDefaultAsync(u => u.Email == model.Email);
        if (customer == null || !BCrypt.Net.BCrypt.EnhancedVerify(model.Password, customer.Password))
        {
            _logger.LogWarning("Invalid credentials for email: {Email}", model.Email);
            return Unauthorized(new { message = "Sai email hoặc mật khẩu." });
        }

        if (!customer.IsEmailVerified)
        {
            _logger.LogWarning("Email not verified for email: {Email}", model.Email);
            return BadRequest(new { message = "Email chưa được xác thực." });
        }

        var token = GenerateJwtToken(customer);
        _logger.LogInformation("Login successful for email: {Email}", model.Email);
        return Ok(new
        {
            message = "Đăng nhập thành công.",
            token,
            customerId = customer.CustomerId
        });
    }

    [HttpPost("forgot-password-customer")]
    public async Task<IActionResult> ForgotPassword([FromBody] ForgotPasswordDTO model)
    {
        _logger.LogInformation("ForgotPassword called for email: {Email}", model.Email);

        var customer = await _context.Customers.FirstOrDefaultAsync(u => u.Email == model.Email);
        if (customer == null)
        {
            _logger.LogWarning("Email not found: {Email}", model.Email);
            return BadRequest(new { message = "Email không tồn tại." });
        }

        string otp = GenerateOtp();
        await StoreOtpInCache(customer.Email, otp, "ResetPasswordOTP");
        _logger.LogInformation("Generated OTP for password reset: {Otp} for {Email}", otp, customer.Email);

        await SendOtpEmail(customer.Email, otp, "Mã OTP đặt lại mật khẩu");
        _logger.LogInformation("Sent OTP email for password reset to {Email}", customer.Email);

        return Ok(new { message = "Mã OTP đã được gửi đến email của bạn." });
    }

    [HttpPost("reset-password-customer")]
    public async Task<IActionResult> ResetPassword([FromBody] ResetPasswordDTO model)
    {
        _logger.LogInformation("ResetPassword called for email: {Email}", model.Email);

        var customer = await _context.Customers.FirstOrDefaultAsync(u => u.Email == model.Email);
        if (customer == null)
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

        customer.Password = BCrypt.Net.BCrypt.EnhancedHashPassword(model.NewPassword);
        _context.Customers.Update(customer);
        await _cache.RemoveAsync($"ResetPasswordOTP_{model.Email}");
        await _context.SaveChangesAsync();

        _logger.LogInformation("Password reset successful for {Email}", model.Email);
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

    private string GenerateJwtToken(Customer customer)
    {
        var key = Encoding.UTF8.GetBytes(_config["Jwt:Key"]);
        var tokenHandler = new JwtSecurityTokenHandler();
        var tokenDescriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(new Claim[]
            {
                new Claim(ClaimTypes.NameIdentifier, customer.CustomerId.ToString()),
                new Claim(ClaimTypes.Email, customer.Email)
            }),
            Expires = DateTime.UtcNow.AddHours(3),
            Issuer = _config["Jwt:Issuer"],
            Audience = _config["Jwt:Audience"],
            SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(key), SecurityAlgorithms.HmacSha256Signature)
        };

        var token = tokenHandler.CreateToken(tokenDescriptor);
        return tokenHandler.WriteToken(token);
    }
}