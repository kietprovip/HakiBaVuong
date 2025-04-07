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
using Microsoft.AspNetCore.Http;

[Route("api/auth")]
[ApiController]
public class CustomerAuthController : ControllerBase
{
    private readonly DataContext _context;
    private readonly IConfiguration _config;
    private readonly IMapper _mapper;

    public CustomerAuthController(DataContext context, IConfiguration config, IMapper mapper)
    {
        _context = context;
        _config = config;
        _mapper = mapper;
    }

    [HttpPost("registerCustomer")]
    public async Task<IActionResult> Register([FromBody] RegisterCustomerDTO model)
    {

        if (!await ValidateCaptcha(model.CaptchaResponse))
        {
            return BadRequest(new { message = "Xác thực CAPTCHA thất bại." });
        }

        if (await _context.Customers.AnyAsync(u => u.Email == model.Email))
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

        var customer = _mapper.Map<Customer>(model);
        customer.Password = BCrypt.Net.BCrypt.EnhancedHashPassword(model.Password);
        customer.CreatedAt = DateTime.UtcNow;
        customer.IsEmailVerified = false;

        _context.Customers.Add(customer);
        await _context.SaveChangesAsync();

        string otp = GenerateOtp();
        HttpContext.Session.SetString($"RegisterOTP_{customer.Email}", otp);
        await SendOtpEmail(customer.Email, otp, "Xác thực email đăng ký");

        return Ok(new { message = "Đăng ký thành công. Vui lòng kiểm tra email để xác thực." });
    }

    [HttpPost("loginCustomer")]
    public async Task<IActionResult> Login([FromBody] LoginCustomerDTO model)
    {

        if (!await ValidateCaptcha(model.CaptchaResponse))
        {
            return BadRequest(new { message = "Xác thực CAPTCHA thất bại." });
        }

        var customer = await _context.Customers.FirstOrDefaultAsync(u => u.Email == model.Email);
        if (customer == null || !BCrypt.Net.BCrypt.EnhancedVerify(model.Password, customer.Password))
        {
            return Unauthorized(new { message = "Sai email hoặc mật khẩu." });
        }

        if (!customer.IsEmailVerified)
        {
            return BadRequest(new { message = "Email chưa được xác thực." });
        }

        string otp = GenerateOtp();
        HttpContext.Session.SetString($"2FA_OTP_{customer.Email}", otp);
        await SendOtpEmail(customer.Email, otp, "Mã OTP đăng nhập 2FA");

        return Ok(new { message = "Vui lòng nhập mã OTP đã gửi đến email của bạn." });
    }

    [HttpPost("verify-email-customer")]
    public async Task<IActionResult> VerifyEmail([FromBody] VerifyOtpDTO model)
    {
        var customer = await _context.Customers.FirstOrDefaultAsync(u => u.Email == model.Email && !u.IsEmailVerified);
        if (customer == null)
        {
            return BadRequest(new { message = "Email không tồn tại hoặc đã được xác thực." });
        }

        var storedOtp = HttpContext.Session.GetString($"RegisterOTP_{model.Email}");
        if (string.IsNullOrEmpty(storedOtp) || storedOtp != model.Otp)
        {
            return BadRequest(new { message = "Mã OTP không đúng hoặc đã hết hạn." });
        }

        customer.IsEmailVerified = true;
        _context.Customers.Update(customer);
        await _context.SaveChangesAsync();

        HttpContext.Session.Remove($"RegisterOTP_{model.Email}");
        return Ok(new { message = "Xác thực email thành công. Bạn có thể đăng nhập." });
    }

    [HttpPost("verify-2fa-customer")]
    public async Task<IActionResult> Verify2FA([FromBody] VerifyOtpDTO model)
    {
        var customer = await _context.Customers.FirstOrDefaultAsync(u => u.Email == model.Email);
        if (customer == null)
        {
            return BadRequest(new { message = "Email không tồn tại." });
        }

        var storedOtp = HttpContext.Session.GetString($"2FA_OTP_{model.Email}");
        if (string.IsNullOrEmpty(storedOtp) || storedOtp != model.Otp)
        {
            return BadRequest(new { message = "Mã OTP không đúng hoặc đã hết hạn." });
        }

        var token = GenerateJwtToken(customer);
        HttpContext.Session.Remove($"2FA_OTP_{model.Email}");

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
        var customer = await _context.Customers.FirstOrDefaultAsync(u => u.Email == model.Email);
        if (customer == null)
        {
            return BadRequest(new { message = "Email không tồn tại." });
        }

        string otp = GenerateOtp();
        HttpContext.Session.SetString($"ResetPasswordOTP_{customer.Email}", otp);
        await SendOtpEmail(customer.Email, otp, "Mã OTP đặt lại mật khẩu");

        return Ok(new { message = "Mã OTP đã được gửi đến email của bạn." });
    }

    [HttpPost("reset-password-customer")]
    public async Task<IActionResult> ResetPassword([FromBody] ResetPasswordDTO model)
    {
        var customer = await _context.Customers.FirstOrDefaultAsync(u => u.Email == model.Email);
        if (customer == null)
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

        customer.Password = BCrypt.Net.BCrypt.EnhancedHashPassword(model.NewPassword);
        _context.Customers.Update(customer);
        await _context.SaveChangesAsync();

        HttpContext.Session.Remove($"ResetPasswordOTP_{model.Email}");
        return Ok(new { message = "Đặt lại mật khẩu thành công." });
    }


    private async Task<bool> ValidateCaptcha(string captchaResponse)
    {
        var secretKey = _config["GoogleReCaptcha:SecretKey"];
        if (string.IsNullOrEmpty(captchaResponse))
        {
            return false;
        }

        var client = new HttpClient();
        var result = await client.PostAsync(
            "https://www.google.com/recaptcha/api/siteverify",
            new FormUrlEncodedContent(new[]
            {
                new KeyValuePair<string, string>("secret", secretKey),
                new KeyValuePair<string, string>("response", captchaResponse)
            })
        );

        var responseString = await result.Content.ReadAsStringAsync();
        return responseString.Contains("\"success\": true");
    }

    private string GenerateOtp()
    {
        return new Random().Next(100000, 999999).ToString();
    }

    private async Task SendOtpEmail(string email, string otp, string subject)
    {
        var fromAddress = new MailAddress("....", "Shop Haki Bá Vương");// add gmail
        var toAddress = new MailAddress(email);
        const string fromPassword = "....";// add app password
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
