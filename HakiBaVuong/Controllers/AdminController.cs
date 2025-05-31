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
using Microsoft.Extensions.Logging;

[Route("api/admin")]
[ApiController]
[Authorize(Roles = "Admin")]
public class AdminController : ControllerBase
{
    private readonly DataContext _context;
    private readonly IMapper _mapper;
    private readonly ILogger<AdminController> _logger;

    public AdminController(DataContext context, IMapper mapper, ILogger<AdminController> logger)
    {
        _context = context;
        _mapper = mapper;
        _logger = logger;
    }


    [HttpGet("users")]
    public async Task<IActionResult> GetUsers()
    {
        try
        {
            var users = await _context.Users
                .Include(u => u.Brand)
                .ToListAsync();
            var userDTOs = _mapper.Map<List<UserDTO>>(users);
            return Ok(userDTOs);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Lỗi khi lấy danh sách users.");
            return StatusCode(500, new { message = "Lỗi server khi lấy danh sách users.", error = ex.Message });
        }
    }

    [HttpGet("users/{id}")]
    public async Task<IActionResult> GetUser(int id)
    {
        try
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
        catch (Exception ex)
        {
            _logger.LogError(ex, "Lỗi khi lấy user.");
            return StatusCode(500, new { message = "Lỗi server khi lấy user.", error = ex.Message });
        }
    }

    [HttpPut("users/{id}")]
    public async Task<IActionResult> UpdateUser(int id, [FromBody] UpdateUserDTO model)
    {
        try
        {
            if (model == null || string.IsNullOrWhiteSpace(model.Name))
            {
                return BadRequest(new { message = "Tên user không được để trống." });
            }

            var user = await _context.Users.FindAsync(id);
            if (user == null)
            {
                return NotFound(new { message = "Không tìm thấy user." });
            }

            user.Name = model.Name.Trim();
            user.UpdatedAt = DateTime.UtcNow;

            _context.Users.Update(user);
            await _context.SaveChangesAsync();

            return Ok(new { message = "Cập nhật user thành công." });
        }
        catch (DbUpdateException dbEx)
        {
            _logger.LogError(dbEx, "Lỗi khi cập nhật user vào database.");
            return StatusCode(500, new { message = "Lỗi khi lưu user vào database.", error = dbEx.InnerException?.Message ?? dbEx.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Lỗi server khi cập nhật user.");
            return StatusCode(500, new { message = "Lỗi server khi cập nhật user.", error = ex.Message });
        }
    }

    [HttpDelete("users/{id}")]
    public async Task<IActionResult> DeleteUser(int id)
    {
        try
        {
            var user = await _context.Users
                .Include(u => u.Brand)
                .FirstOrDefaultAsync(u => u.UserId == id);
            if (user == null)
            {
                return NotFound(new { message = "Không tìm thấy user." });
            }

            if (user.Role == "Admin")
            {
                return BadRequest(new { message = "Không thể xóa tài khoản Admin." });
            }

            var brandToDelete = await _context.Brands
                .Include(b => b.Products)
                .Include(b => b.Orders)
                .ThenInclude(o => o.OrderItems)
                .FirstOrDefaultAsync(b => b.OwnerId == user.UserId);

            if (brandToDelete != null)
            {

                foreach (var order in brandToDelete.Orders.ToList())
                {
                    _context.OrderItems.RemoveRange(order.OrderItems);
                    _context.Orders.Remove(order);
                }


                _context.Products.RemoveRange(brandToDelete.Products);


                _context.Brands.Remove(brandToDelete);
            }


            _context.Users.Remove(user);
            await _context.SaveChangesAsync();

            return Ok(new { message = "Xóa user thành công." });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Lỗi server khi xóa user.");
            return StatusCode(500, new { message = "Lỗi server khi xóa user.", error = ex.Message });
        }
    }


    [HttpGet("customers")]
    public async Task<IActionResult> GetCustomers()
    {
        try
        {
            var customers = await _context.Customers
                .Include(c => c.Addresses)
                .ToListAsync();
            var customerDTOs = _mapper.Map<List<CustomerDTO>>(customers);
            return Ok(customerDTOs);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Lỗi khi lấy danh sách customers.");
            return StatusCode(500, new { message = "Lỗi server khi lấy danh sách customers.", error = ex.Message });
        }
    }

    [HttpGet("customers/{id}")]
    public async Task<IActionResult> GetCustomer(int id)
    {
        try
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
        catch (Exception ex)
        {
            _logger.LogError(ex, "Lỗi khi lấy customer.");
            return StatusCode(500, new { message = "Lỗi server khi lấy customer.", error = ex.Message });
        }
    }

    [HttpPut("customers/{id}")]
    public async Task<IActionResult> UpdateCustomer(int id, [FromBody] UpdateCustomerDTO model)
    {
        try
        {
            if (model == null || string.IsNullOrWhiteSpace(model.Name))
            {
                return BadRequest(new { message = "Tên khách hàng không được để trống." });
            }

            var customer = await _context.Customers.FindAsync(id);
            if (customer == null)
            {
                return NotFound(new { message = "Không tìm thấy customer." });
            }

            customer.Name = model.Name.Trim();

            _context.Customers.Update(customer);
            await _context.SaveChangesAsync();

            return Ok(new { message = "Cập nhật customer thành công." });
        }
        catch (DbUpdateException dbEx)
        {
            _logger.LogError(dbEx, "Lỗi khi cập nhật customer vào database.");
            return StatusCode(500, new { message = "Lỗi khi lưu customer vào database.", error = dbEx.InnerException?.Message ?? dbEx.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Lỗi server khi cập nhật customer.");
            return StatusCode(500, new { message = "Lỗi server khi cập nhật customer.", error = ex.Message });
        }
    }

    [HttpDelete("customers/{id}")]
    public async Task<IActionResult> DeleteCustomer(int id)
    {
        try
        {
            var customer = await _context.Customers
                .Include(c => c.Orders)
                .ThenInclude(o => o.OrderItems)
                .FirstOrDefaultAsync(c => c.CustomerId == id);
            if (customer == null)
            {
                return NotFound(new { message = "Không tìm thấy customer." });
            }


            foreach (var order in customer.Orders.ToList())
            {
                _context.OrderItems.RemoveRange(order.OrderItems);
                _context.Orders.Remove(order);
            }


            _context.Customers.Remove(customer);
            await _context.SaveChangesAsync();

            return Ok(new { message = "Xóa customer thành công." });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Lỗi server khi xóa customer.");
            return StatusCode(500, new { message = "Lỗi server khi xóa customer.", error = ex.Message });
        }
    }
}