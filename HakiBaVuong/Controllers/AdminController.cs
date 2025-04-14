using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authorization;
using HakiBaVuong.Models;
using HakiBaVuong.Data;
using HakiBaVuong.DTOs;
using AutoMapper;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Linq;

[Route("api/admin")]
[ApiController]
[Authorize(Roles = "Admin")]
public class AdminController : ControllerBase
{
    private readonly DataContext _context;
    private readonly IMapper _mapper;

    public AdminController(DataContext context, IMapper mapper)
    {
        _context = context;
        _mapper = mapper;
    }

    // Quản lý User
    [HttpGet("users")]
    public async Task<IActionResult> GetUsers()
    {
        var users = await _context.Users
        .Include(u => u.Brand)
        .ToListAsync();
        var userDTOs = _mapper.Map<List<UserDTO>>(users);
        return Ok(userDTOs);
    }

    [HttpGet("users/{id}")]
    public async Task<IActionResult> GetUser(int id)
    {
        var user = await _context.Users
        .Include(u => u.Brand)
        .FirstOrDefaultAsync(u => u.UserId == id);
        if (user == null)
        {
            return NotFound(new { message = "Không tìm thấy user." });
        }
        var userDTO = _mapper.Map<UserDTO>(user);
        return Ok(userDTO);
    }

    [HttpPut("users/{id}")]
    public async Task<IActionResult> UpdateUser(int id, [FromBody] UserDTO model)
    {
        var user = await _context.Users.FindAsync(id);
        if (user == null)
        {
            return NotFound(new { message = "Không tìm thấy user." });
        }

        if (await _context.Users.AnyAsync(u => u.Email == model.Email && u.UserId != id))
        {
            return BadRequest(new { message = "Email đã tồn tại." });
        }

        user.Name = model.Name;
        user.Email = model.Email;
        user.Role = model.Role;
        user.BrandId = model.BrandId;
        user.UpdatedAt = DateTime.UtcNow;

        _context.Users.Update(user);
        await _context.SaveChangesAsync();

        return Ok(new { message = "Cập nhật user thành công." });
    }

    [HttpDelete("users/{id}")]
    public async Task<IActionResult> DeleteUser(int id)
    {
        var user = await _context.Users.FindAsync(id);
        if (user == null)
        {
            return NotFound(new { message = "Không tìm thấy user." });
        }

        if (user.Role == "Admin")
        {
            return BadRequest(new { message = "Không thể xóa tài khoản Admin." });
        }

        _context.Users.Remove(user);
        await _context.SaveChangesAsync();

        return Ok(new { message = "Xóa user thành công." });
    }

    // Quản lý Customer
    [HttpGet("customers")]
    public async Task<IActionResult> GetCustomers()
    {
        var customers = await _context.Customers
        .Include(c => c.Addresses)
        .ToListAsync();
        var customerDTOs = _mapper.Map<List<CustomerDTO>>(customers);
        return Ok(customerDTOs);
    }

    [HttpGet("customers/{id}")]
    public async Task<IActionResult> GetCustomer(int id)
    {
        var customer = await _context.Customers
        .Include(c => c.Addresses)
        .FirstOrDefaultAsync(c => c.CustomerId == id);
        if (customer == null)
        {
            return NotFound(new { message = "Không tìm thấy customer." });
        }
        var customerDTO = _mapper.Map<CustomerDTO>(customer);
        return Ok(customerDTO);
    }

    [HttpPut("customers/{id}")]
    public async Task<IActionResult> UpdateCustomer(int id, [FromBody] CustomerDTO model)
    {
        var customer = await _context.Customers.FindAsync(id);
        if (customer == null)
        {
            return NotFound(new { message = "Không tìm thấy customer." });
        }

        if (await _context.Customers.AnyAsync(c => c.Email == model.Email && c.CustomerId != id))
        {
            return BadRequest(new { message = "Email đã tồn tại." });
        }

        customer.Name = model.Name;
        customer.Email = model.Email;
        customer.IsEmailVerified = model.IsEmailVerified;

        _context.Customers.Update(customer);
        await _context.SaveChangesAsync();

        return Ok(new { message = "Cập nhật customer thành công." });
    }

    [HttpDelete("customers/{id}")]
    public async Task<IActionResult> DeleteCustomer(int id)
    {
        var customer = await _context.Customers.FindAsync(id);
        if (customer == null)
        {
            return NotFound(new { message = "Không tìm thấy customer." });
        }

        _context.Customers.Remove(customer);
        await _context.SaveChangesAsync();

        return Ok(new { message = "Xóa customer thành công." });
    }
}