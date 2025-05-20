using HakiBaVuong.Data;
using HakiBaVuong.DTOs;
using HakiBaVuong.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace HakiBaVuong.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class BrandController : ControllerBase
    {
        private readonly DataContext _context;
        private readonly ILogger<BrandController> _logger;
        private readonly IWebHostEnvironment _environment;

        public BrandController(DataContext context, ILogger<BrandController> logger, IWebHostEnvironment environment)
        {
            _context = context;
            _logger = logger;
            _environment = environment;
        }

        [HttpGet]
        [Authorize(Roles = "Admin,Staff")]
        public async Task<ActionResult<IEnumerable<Brand>>> GetAll()
        {
            _logger.LogInformation("GetAll brands called");

            var userId = GetUserId();
            if (!userId.HasValue)
            {
                _logger.LogWarning("Invalid userId from token");
                return BadRequest(new { message = "Không thể xác định userId từ token." });
            }

            IQueryable<Brand> query;
            if (User.IsInRole("Admin"))
            {
                query = _context.Brands;
            }
            else
            {
                query = _context.Brands.Where(b => b.OwnerId == userId.Value);
            }

            var brands = await query
                .Include(b => b.Owner)
                .Include(b => b.Products)
                .ToListAsync();

            _logger.LogInformation("Retrieved {Count} brands", brands.Count);
            return Ok(brands);
        }

        [HttpGet("{id}")]
        [Authorize(Roles = "Admin,Staff")]
        public async Task<ActionResult<Brand>> GetById(int id)
        {
            _logger.LogInformation("GetById called for brand {BrandId}", id);

            var brand = await _context.Brands
                .Include(b => b.Owner)
                .Include(b => b.Products)
                .FirstOrDefaultAsync(b => b.BrandId == id);

            if (brand == null)
            {
                _logger.LogWarning("Brand not found: {BrandId}", id);
                return NotFound(new { message = "Brand không tồn tại." });
            }

            if (User.IsInRole("Staff"))
            {
                var userId = GetUserId();
                if (!userId.HasValue || brand.OwnerId != userId.Value)
                {
                    _logger.LogWarning("Staff user {UserId} does not have access to brand {BrandId}", userId, id);
                    return Forbid();
                }
            }

            return Ok(brand);
        }

        [HttpPost]
        [Authorize(Roles = "Admin,Staff")]
        public async Task<ActionResult<Brand>> Create(BrandDTO brandDto)
        {
            _logger.LogInformation("Create brand called with name {Name}", brandDto.Name);

            var userId = GetUserId();
            if (!userId.HasValue)
            {
                _logger.LogWarning("Invalid userId from token");
                return BadRequest(new { message = "Không thể xác định userId từ token." });
            }

            var user = await _context.Users.FindAsync(userId.Value);
            if (user == null)
            {
                _logger.LogWarning("User not found: {UserId}", userId);
                return NotFound(new { message = "Người dùng không tồn tại." });
            }

            if (user.BrandId.HasValue)
            {
                _logger.LogWarning("User {UserId} is already associated with a brand", userId);
                return BadRequest(new { message = "Bạn đã thuộc một thương hiệu, không thể tạo thương hiệu mới." });
            }

            var brand = new Brand
            {
                Name = brandDto.Name,
                OwnerId = User.IsInRole("Admin") ? brandDto.OwnerId : userId.Value,
                CreatedAt = DateTime.UtcNow,
                BackgroundColor = brandDto.BackgroundColor,
                BackgroundImageUrl = brandDto.BackgroundImageUrl
            };

            _context.Brands.Add(brand);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Created brand {BrandId}", brand.BrandId);
            return CreatedAtAction(nameof(GetById), new { id = brand.BrandId }, brand);
        }

        [HttpPut("{id}")]
        [Authorize(Roles = "Admin,Staff")]
        public async Task<IActionResult> Update(int id, BrandDTO brandDto)
        {
            _logger.LogInformation("Update called for brand {BrandId}", id);

            var brand = await _context.Brands.FindAsync(id);
            if (brand == null)
            {
                _logger.LogWarning("Brand not found: {BrandId}", id);
                return NotFound(new { message = "Brand không tồn tại." });
            }

            var userId = GetUserId();
            if (!userId.HasValue)
            {
                _logger.LogWarning("Invalid userId from token");
                return Unauthorized(new { message = "Token không hợp lệ." });
            }

            var user = await _context.Users.FindAsync(userId.Value);
            if (user == null)
            {
                _logger.LogWarning("User not found: {UserId}", userId);
                return NotFound(new { message = "Người dùng không tồn tại." });
            }

            if (user.BrandId.HasValue)
            {
                _logger.LogWarning("User {UserId} is already associated with a brand", userId);
                return BadRequest(new { message = "Bạn đã thuộc một thương hiệu, không thể sửa thương hiệu." });
            }

            if (User.IsInRole("Staff"))
            {
                if (brand.OwnerId != userId.Value)
                {
                    _logger.LogWarning("Staff user {UserId} does not have access to brand {BrandId}", userId, id);
                    return Forbid();
                }
            }

            brand.Name = brandDto.Name;
            if (User.IsInRole("Admin"))
            {
                brand.OwnerId = brandDto.OwnerId;
            }

            await _context.SaveChangesAsync();
            _logger.LogInformation("Updated brand {BrandId}", id);
            return NoContent();
        }

        [HttpDelete("{id}")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Delete(int id)
        {
            _logger.LogInformation("Delete called for brand {BrandId}", id);

            var brand = await _context.Brands.FindAsync(id);
            if (brand == null)
            {
                _logger.LogWarning("Brand not found: {BrandId}", id);
                return NotFound(new { message = "Brand không tồn tại." });
            }

            _context.Brands.Remove(brand);
            await _context.SaveChangesAsync();
            _logger.LogInformation("Deleted brand {BrandId}", id);
            return NoContent();
        }

        [HttpPut("{id}/background")]
        [Authorize(Roles = "Admin,Staff")]
        public async Task<IActionResult> UpdateBrandBackground(int id, [FromForm] UpdateBrandBackgroundDTO model)
        {
            _logger.LogInformation("UpdateBrandBackground called for brand {BrandId}", id);

            var userId = GetUserId();
            if (!userId.HasValue)
            {
                _logger.LogWarning("Invalid userId from token");
                return Unauthorized(new { message = "Token không hợp lệ." });
            }

            var brand = await _context.Brands.FindAsync(id);
            if (brand == null)
            {
                _logger.LogWarning("Brand not found: {BrandId}", id);
                return NotFound(new { message = "Brand không tồn tại." });
            }

            var user = await _context.Users.FindAsync(userId.Value);
            if (user == null)
            {
                _logger.LogWarning("User not found: {UserId}", userId);
                return NotFound(new { message = "Người dùng không tồn tại." });
            }

            if (user.BrandId.HasValue)
            {
                _logger.LogWarning("User {UserId} is already associated with a brand", userId);
                return BadRequest(new { message = "Bạn đã thuộc một thương hiệu, không thể cập nhật background." });
            }

            if (User.IsInRole("Staff") && brand.OwnerId != userId.Value)
            {
                _logger.LogWarning("Staff user {UserId} does not have access to brand {BrandId}", userId, id);
                return Forbid();
            }

            if (!string.IsNullOrEmpty(model.BackgroundColor))
            {
                if (!IsValidHexColor(model.BackgroundColor))
                {
                    _logger.LogWarning("Invalid hex color: {BackgroundColor}", model.BackgroundColor);
                    return BadRequest(new { message = "Mã màu hex không hợp lệ." });
                }
                brand.BackgroundColor = model.BackgroundColor;
            }

            if (model.BackgroundImage != null)
            {
                var allowedExtensions = new[] { ".jpg", ".jpeg", ".png" };
                var extension = Path.GetExtension(model.BackgroundImage.FileName).ToLowerInvariant();
                if (!allowedExtensions.Contains(extension))
                {
                    _logger.LogWarning("Invalid file extension {Extension} for brand {BrandId}", extension, id);
                    return BadRequest(new { message = "Định dạng file không hợp lệ. Chỉ hỗ trợ .jpg, .jpeg, .png." });
                }

                var maxFileSize = 5 * 1024 * 1024;
                if (model.BackgroundImage.Length > maxFileSize)
                {
                    _logger.LogWarning("File size {Size} exceeds limit for brand {BrandId}", model.BackgroundImage.Length, id);
                    return BadRequest(new { message = "Kích thước file vượt quá 5MB." });
                }

                var uploadsFolder = Path.Combine(Directory.GetCurrentDirectory(), "Images", "brands");
                if (!Directory.Exists(uploadsFolder))
                {
                    Directory.CreateDirectory(uploadsFolder);
                }

                var fileName = $"brand_{id}{extension}";
                var filePath = Path.Combine(uploadsFolder, fileName);

                if (!string.IsNullOrEmpty(brand.BackgroundImageUrl))
                {
                    var oldImagePath = Path.Combine(Directory.GetCurrentDirectory(), brand.BackgroundImageUrl.TrimStart('/'));
                    if (System.IO.File.Exists(oldImagePath))
                    {
                        System.IO.File.Delete(oldImagePath);
                        _logger.LogInformation("Deleted old background image for brand {BrandId}", id);
                    }
                }

                using (var stream = new FileStream(filePath, FileMode.Create))
                {
                    await model.BackgroundImage.CopyToAsync(stream);
                }

                brand.BackgroundImageUrl = $"/images/brands/{fileName}";
            }

            _context.Brands.Update(brand);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Updated background for brand {BrandId}", id);
            return Ok(brand);
        }

        [HttpDelete("{id}/background")]
        [Authorize(Roles = "Admin,Staff")]
        public async Task<IActionResult> DeleteBrandBackground(int id)
        {
            _logger.LogInformation("DeleteBrandBackground called for brand {BrandId}", id);

            var userId = GetUserId();
            if (!userId.HasValue)
            {
                _logger.LogWarning("Invalid userId from token");
                return Unauthorized(new { message = "Token không hợp lệ." });
            }

            var brand = await _context.Brands.FindAsync(id);
            if (brand == null)
            {
                _logger.LogWarning("Brand not found: {BrandId}", id);
                return NotFound(new { message = "Brand không tồn tại." });
            }

            var user = await _context.Users.FindAsync(userId.Value);
            if (user == null)
            {
                _logger.LogWarning("User not found: {UserId}", userId);
                return NotFound(new { message = "Người dùng không tồn tại." });
            }

            if (user.BrandId.HasValue)
            {
                _logger.LogWarning("User {UserId} is already associated with a brand", userId);
                return BadRequest(new { message = "Bạn đã thuộc một thương hiệu, không thể xóa background." });
            }

            if (User.IsInRole("Staff") && brand.OwnerId != userId.Value)
            {
                _logger.LogWarning("Staff user {UserId} does not have access to brand {BrandId}", userId, id);
                return Forbid();
            }

            if (!string.IsNullOrEmpty(brand.BackgroundImageUrl))
            {
                var imagePath = Path.Combine(Directory.GetCurrentDirectory(), brand.BackgroundImageUrl.TrimStart('/'));
                if (System.IO.File.Exists(imagePath))
                {
                    System.IO.File.Delete(imagePath);
                    _logger.LogInformation("Deleted background image for brand {BrandId}", id);
                }
                brand.BackgroundImageUrl = null;
            }

            brand.BackgroundColor = "#FFFFFF";

            _context.Brands.Update(brand);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Reset background for brand {BrandId}", id);
            return Ok(brand);
        }

        private int? GetUserId()
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            return int.TryParse(userIdClaim, out int userId) ? userId : null;
        }

        private bool IsValidHexColor(string color)
        {
            return System.Text.RegularExpressions.Regex.IsMatch(color, "^#[0-9A-Fa-f]{6}$");
        }
    }
}