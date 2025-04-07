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
using Microsoft.AspNetCore.Http;

[Route("api/auth")]
[ApiController]
public class AuthController : ControllerBase
{
    private readonly DataContext _context;
    private readonly IConfiguration _config;
    private readonly IMapper _mapper;

    public AuthController(DataContext context, IConfiguration config, IMapper mapper)
    {
        _context = context;
        _config = config;
        _mapper = mapper;
    }


    [HttpPost("register")]
    public async Task<IActionResult> Register([FromBody] RegisterDTO model)
    {
        if (await _context.Users.AnyAsync(u => u.Email == model.Email))
        {
            return BadRequest(new { message = "Email đã tồn tại." });
        }
        if (model.Password != model.ConfirmPassword)
        {
            return BadRequest(new { message = "Mật khẩu xác nhận không khớp." });
        }
        if (model.Password.Length < 6)
        {
            return BadRequest(new { message = "Mật khẩu phải có ít nhất 6 ký tự." });
        }

        var user = _mapper.Map<User>(model);
        user.Password = BCrypt.Net.BCrypt.EnhancedHashPassword(model.Password);
        user.CreatedAt = DateTime.UtcNow;
        user.IsEmailVerified = false;

        _context.Users.Add(user);
        await _context.SaveChangesAsync();


        string otp = GenerateOtp();
        HttpContext.Session.SetString($"RegisterOTP_{user.Email}", otp);
        await SendOtpEmail(user.Email, otp, "Xác thực email đăng ký");

        return Ok(new { message = "Đăng ký thành công. Vui lòng kiểm tra email để xác thực." });
    }


    [HttpPost("verify-email")]
    public async Task<IActionResult> VerifyEmail([FromBody] VerifyOtpDTO model)
    {
        var user = await _context.Users.FirstOrDefaultAsync(u => u.Email == model.Email && !u.IsEmailVerified);
        if (user == null)
        {
            return BadRequest(new { message = "Email không tồn tại hoặc đã được xác thực." });
        }

        var storedOtp = HttpContext.Session.GetString($"RegisterOTP_{model.Email}");
        if (string.IsNullOrEmpty(storedOtp) || storedOtp != model.Otp)
        {
            return BadRequest(new { message = "Mã OTP không đúng hoặc đã hết hạn." });
        }

        user.IsEmailVerified = true;
        _context.Users.Update(user);
        await _context.SaveChangesAsync();

        HttpContext.Session.Remove($"RegisterOTP_{model.Email}");
        return Ok(new { message = "Xác thực email thành công. Bạn có thể đăng nhập." });
    }


    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginDTO model)
    {
        var user = await _context.Users.FirstOrDefaultAsync(u => u.Email == model.Email);
        if (user == null || !BCrypt.Net.BCrypt.EnhancedVerify(model.Password, user.Password))
        {
            return Unauthorized(new { message = "Sai email hoặc mật khẩu." });
        }

        if (!user.IsEmailVerified)
        {
            return BadRequest(new { message = "Email chưa được xác thực." });
        }


        string otp = GenerateOtp();
        HttpContext.Session.SetString($"2FA_OTP_{user.Email}", otp);
        await SendOtpEmail(user.Email, otp, "Mã OTP đăng nhập 2FA");

        return Ok(new { message = "Vui lòng nhập mã OTP đã gửi đến email của bạn." });
    }


    [HttpPost("verify-2fa")]
    public async Task<IActionResult> Verify2FA([FromBody] VerifyOtpDTO model)
    {
        var user = await _context.Users.FirstOrDefaultAsync(u => u.Email == model.Email);
        if (user == null)
        {
            return BadRequest(new { message = "Email không tồn tại." });
        }

        var storedOtp = HttpContext.Session.GetString($"2FA_OTP_{model.Email}");
        if (string.IsNullOrEmpty(storedOtp) || storedOtp != model.Otp)
        {
            return BadRequest(new { message = "Mã OTP không đúng hoặc đã hết hạn." });
        }

        var token = GenerateJwtToken(user);
        HttpContext.Session.Remove($"2FA_OTP_{model.Email}");

        return Ok(new
        {
            message = "Đăng nhập thành công.",
            token,
            userId = user.UserId,
            role = user.Role
        });
    }


    [HttpPost("forgot-password")]
    public async Task<IActionResult> ForgotPassword([FromBody] ForgotPasswordDTO model)
    {
        var user = await _context.Users.FirstOrDefaultAsync(u => u.Email == model.Email);
        if (user == null)
        {
            return BadRequest(new { message = "Email không tồn tại." });
        }

        string otp = GenerateOtp();
        HttpContext.Session.SetString($"ResetPasswordOTP_{user.Email}", otp);
        await SendOtpEmail(user.Email, otp, "Mã OTP đặt lại mật khẩu");

        return Ok(new { message = "Mã OTP đã được gửi đến email của bạn." });
    }


    [HttpPost("reset-password")]
    public async Task<IActionResult> ResetPassword([FromBody] ResetPasswordDTO model)
    {
        var user = await _context.Users.FirstOrDefaultAsync(u => u.Email == model.Email);
        if (user == null)
        {
            return BadRequest(new { message = "Email không tồn tại." });
        }

        var storedOtp = HttpContext.Session.GetString($"ResetPasswordOTP_{model.Email}");
        if (string.IsNullOrEmpty(storedOtp) || storedOtp != model.Otp)
        {
            return BadRequest(new { message = "Mã OTP không đúng hoặc đã hết hạn." });
        }

        if (model.NewPassword != model.ConfirmPassword)
        {
            return BadRequest(new { message = "Mật khẩu xác nhận không khớp." });
        }
        if (model.NewPassword.Length < 6)
        {
            return BadRequest(new { message = "Mật khẩu phải có ít nhất 6 ký tự." });
        }

        user.Password = BCrypt.Net.BCrypt.EnhancedHashPassword(model.NewPassword);
        _context.Users.Update(user);
        await _context.SaveChangesAsync();

        HttpContext.Session.Remove($"ResetPasswordOTP_{model.Email}");
        return Ok(new { message = "Đặt lại mật khẩu thành công." });
    }


    private string GenerateOtp()
    {
        return new Random().Next(100000, 999999).ToString();
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
        var tokenDescriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(new Claim[]
            {
                new Claim(ClaimTypes.NameIdentifier, user.UserId.ToString()),
                new Claim(ClaimTypes.Email, user.Email),
                new Claim(ClaimTypes.Role, user.Role)
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