using HakiBaVuong.DTOs;
using HakiBaVuong.Models;
using Microsoft.AspNetCore.Authorization; // Thêm để kiểm tra phân quyền
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using HakiBaVuong.Data;
using System.Security.Claims; // Thêm để lấy thông tin từ JWT

namespace HakiBaVuong.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize] // Yêu cầu tất cả API trong controller phải có token
    public class BrandController : ControllerBase
    {
        private readonly DataContext _context;

        public BrandController(DataContext context)
        {
            _context = context;
        }

        [HttpGet]
        [Authorize(Roles = "Admin")] // Chỉ Admin được phép lấy danh sách
        public async Task<ActionResult<IEnumerable<Brand>>> GetAll()
        {
            return await _context.Brands.ToListAsync();
        }

        [HttpGet("{id}")]
        [Authorize(Roles = "Admin,Staff")] // Cả Admin và Staff được phép lấy thông tin chi tiết
        public async Task<ActionResult<Brand>> GetById(int id)
        {
            var brand = await _context.Brands.FindAsync(id);
            if (brand == null)
                return NotFound();

            // Staff chỉ được phép xem Brand của chính mình
            if (User.IsInRole("Staff"))
            {
                var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value);
                if (brand.OwnerId != userId)
                {
                    return Forbid(); // Trả về 403 nếu không có quyền
                }
            }

            return brand;
        }

        [HttpPost]
        [Authorize(Roles = "Admin")] // Chỉ Admin được phép tạo mới Brand
        public async Task<ActionResult<Brand>> Create(BrandDTO brandDto)
        {
            var brand = new Brand
            {
                Name = brandDto.Name,
                OwnerId = brandDto.OwnerId,
                CreatedAt = DateTime.UtcNow
            };

            _context.Brands.Add(brand);
            await _context.SaveChangesAsync();
            return CreatedAtAction(nameof(GetById), new { id = brand.BrandId }, brand);
        }

        [HttpPut("{id}")]
        [Authorize(Roles = "Admin,Staff")] // Cả Admin và Staff được phép sửa
        public async Task<IActionResult> Update(int id, BrandDTO brandDto)
        {
            var brand = await _context.Brands.FindAsync(id);
            if (brand == null)
                return NotFound();

            // Staff chỉ được phép sửa Brand của chính mình
            if (User.IsInRole("Staff"))
            {
                var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value);
                if (brand.OwnerId != userId)
                {
                    return Forbid(); // Trả về 403 nếu không có quyền
                }
            }

            brand.Name = brandDto.Name;
            brand.OwnerId = brandDto.OwnerId;

            await _context.SaveChangesAsync();
            return NoContent();
        }

        [HttpDelete("{id}")]
        [Authorize(Roles = "Admin")] // Chỉ Admin được phép xóa
        public async Task<IActionResult> Delete(int id)
        {
            var brand = await _context.Brands.FindAsync(id);
            if (brand == null)
                return NotFound();

            _context.Brands.Remove(brand);
            await _context.SaveChangesAsync();
            return NoContent();
        }
    }
}