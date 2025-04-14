using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using BCrypt.Net;
using HakiBaVuong.Models;
using HakiBaVuong.Data;
using HakiBaVuong.DTOs;
using AutoMapper;
using System.Net.Mail;
using System.Net;
using Microsoft.AspNetCore.Http;

[Route("api/profile")]
[ApiController]
[Authorize]
public class ProfileController : ControllerBase
{
    private readonly DataContext _context;
    private readonly IMapper _mapper;

    public ProfileController(DataContext context, IMapper mapper)
    {
        _context = context;
        _mapper = mapper;
    }

    [HttpGet("info")]
    public async Task<IActionResult> GetProfile()
    {
        var customerIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(customerIdClaim))
        {
            return Unauthorized(new { message = "Token không chứa thông tin khách hàng (NameIdentifier)." });
        }

        if (!int.TryParse(customerIdClaim, out int customerId))
        {
            return Unauthorized(new { message = "CustomerId trong token không hợp lệ." });
        }

        var customer = await _context.Customers
            .Include(c => c.Addresses)
            .FirstOrDefaultAsync(c => c.CustomerId == customerId);

        if (customer == null)
        {
            return NotFound(new { message = "Không tìm thấy khách hàng." });
        }

        var customerDto = _mapper.Map<CustomerDTO>(customer);
        return Ok(customerDto);
    }

    [HttpPut("update")]
    public async Task<IActionResult> UpdateProfile([FromBody] UpdateCustomerDTO model)
    {
        var customerId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value);
        var customer = await _context.Customers.FirstOrDefaultAsync(c => c.CustomerId == customerId);

        if (customer == null)
        {
            return NotFound(new { message = "Không tìm thấy khách hàng." });
        }

        customer.Name = model.Name ?? customer.Name;
        _context.Customers.Update(customer);
        await _context.SaveChangesAsync();

        return Ok(new { message = "Cập nhật thông tin cá nhân thành công." });
    }

    [HttpPost("request-reset-password")]
    public async Task<IActionResult> RequestResetPassword()
    {
        var customerId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value);
        var customer = await _context.Customers.FirstOrDefaultAsync(c => c.CustomerId == customerId);

        if (customer == null)
        {
            return NotFound(new { message = "Không tìm thấy khách hàng." });
        }

        string otp = GenerateOtp();
        HttpContext.Session.SetString($"ResetPasswordOTP_{customer.Email}", otp);
        await SendOtpEmail(customer.Email, otp, "Mã OTP đặt lại mật khẩu");

        return Ok(new { message = "Mã OTP đã được gửi đến email của bạn." });
    }

    [HttpPost("reset-password")]
    public async Task<IActionResult> ResetPassword([FromBody] ResetPasswordProfileDTO model)
    {
        var customerId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value);
        var customer = await _context.Customers.FirstOrDefaultAsync(c => c.CustomerId == customerId);

        if (customer == null)
        {
            return NotFound(new { message = "Không tìm thấy khách hàng." });
        }

        var storedOtp = HttpContext.Session.GetString($"ResetPasswordOTP_{customer.Email}");
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

        HttpContext.Session.Remove($"ResetPasswordOTP_{customer.Email}");
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
        const string fromPassword = ".....";// add app password
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
}
