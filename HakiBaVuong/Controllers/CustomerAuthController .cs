using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using BCrypt.Net;
using HakiBaVuong.Models;
using System;
using sexthu.Data;
using Microsoft.EntityFrameworkCore;
using HakiBaVuong.DTOs;
using AutoMapper;

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

        var user = _mapper.Map<Customer>(model);
        user.Password = BCrypt.Net.BCrypt.EnhancedHashPassword(model.Password);
        user.CreatedAt = DateTime.UtcNow;

        _context.Customers.Add(user);
        await _context.SaveChangesAsync();

        return Ok(new { message = "Đăng ký thành công" });
    }


    [HttpPost("loginCustomer")]
    public async Task<IActionResult> Login([FromBody] LoginCustomerDTO model)
    {
        var user = await _context.Customers.FirstOrDefaultAsync(u => u.Email == model.Email);
        if (user == null || !BCrypt.Net.BCrypt.EnhancedVerify(model.Password, user.Password))
        {
            return Unauthorized(new { message = "Sai email hoặc mật khẩu" });
        }

        var token = GenerateJwtToken(user);

        return Ok(new
        {
            message = "Đăng nhập thành công."/*,
            token,
            userId = user.CustomerId*/
        });
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
            new Claim(ClaimTypes.Email, customer.Email),
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
