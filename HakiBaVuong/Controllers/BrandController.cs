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

            var userId = GetUserId();
            if (!userId.HasValue)
            {
                _logger.LogWarning("Invalid userId from token");
                return BadRequest(new { message = "Không thể xác định userId từ token." });
            }

            var brand = await _context.Brands
                .Include(b => b.Owner)
                .Include(b => b.Products)
                .FirstOrDefaultAsync(b => b.BrandId == id);

            if (brand == null)
            {
                _logger.LogWarning("Brand not found: {BrandId}", id);
                return NotFound(new { message = "Brand không tồn tại." });
            }

            if (User.IsInRole("Staff") && brand.OwnerId != userId.Value)
            {
                var hasPermission = await _context.StaffPermissions
                    .AnyAsync(sp => sp.StaffId == userId.Value && sp.Permission.Name == "ManageBrand");
                if (!hasPermission)
                {
                    _logger.LogWarning("Staff user {UserId} does not have ManageBrand permission for brand {BrandId}", userId.Value, id);
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

            if (User.IsInRole("Staff") && brandDto.OwnerId != userId.Value)
            {
                var hasPermission = await _context.StaffPermissions
                    .AnyAsync(sp => sp.StaffId == userId.Value && sp.Permission.Name == "ManageBrand");
                if (!hasPermission)
                {
                    _logger.LogWarning("Staff user {UserId} does not have ManageBrand permission to create brand", userId.Value);
                    return Forbid();
                }
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

            var userId = GetUserId();
            if (!userId.HasValue)
            {
                _logger.LogWarning("Invalid userId from token");
                return BadRequest(new { message = "Không thể xác định userId từ token." });
            }

            var brand = await _context.Brands.FindAsync(id);
            if (brand == null)
            {
                _logger.LogWarning("Brand not found: {BrandId}", id);
                return NotFound(new { message = "Brand không tồn tại." });
            }

            if (User.IsInRole("Staff") && brand.OwnerId != userId.Value)
            {
                var hasPermission = await _context.StaffPermissions
                    .AnyAsync(sp => sp.StaffId == userId.Value && sp.Permission.Name == "ManageBrand");
                if (!hasPermission)
                {
                    _logger.LogWarning("Staff user {UserId} does not have ManageBrand permission for brand {BrandId}", userId.Value, id);
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

            if (User.IsInRole("Staff") && brand.OwnerId != userId.Value)
            {
                var hasPermission = await _context.StaffPermissions
                    .AnyAsync(sp => sp.StaffId == userId.Value && sp.Permission.Name == "ManageBrand");
                if (!hasPermission)
                {
                    _logger.LogWarning("Staff user {UserId} does not have ManageBrand permission for brand {BrandId}", userId.Value, id);
                    return Forbid();
                }
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

            if (User.IsInRole("Staff") && brand.OwnerId != userId.Value)
            {
                var hasPermission = await _context.StaffPermissions
                    .AnyAsync(sp => sp.StaffId == userId.Value && sp.Permission.Name == "ManageBrand");
                if (!hasPermission)
                {
                    _logger.LogWarning("Staff user {UserId} does not have ManageBrand permission for brand {BrandId}", userId.Value, id);
                    return Forbid();
                }
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

        [HttpPost("{brandId}/staff")]
        [Authorize(Roles = "Admin,Staff")]
        public async Task<IActionResult> AddStaff(int brandId, [FromBody] AddStaffDTO model)
        {
            _logger.LogInformation("AddStaff called for brand {BrandId}, staff {StaffId}", brandId, model.StaffId);

            var userId = GetUserId();
            if (!userId.HasValue)
            {
                _logger.LogWarning("Invalid userId from token");
                return Unauthorized(new { message = "Token không hợp lệ." });
            }

            var brand = await _context.Brands.FindAsync(brandId);
            if (brand == null)
            {
                _logger.LogWarning("Brand not found: {BrandId}", brandId);
                return NotFound(new { message = "Brand không tồn tại." });
            }

            if (User.IsInRole("Staff") && brand.OwnerId != userId.Value)
            {
                var hasPermission = await _context.StaffPermissions
                    .AnyAsync(sp => sp.StaffId == userId.Value && sp.Permission.Name == "ManageStaff");
                if (!hasPermission)
                {
                    _logger.LogWarning("Staff user {UserId} does not have ManageStaff permission for brand {BrandId}", userId.Value, brandId);
                    return Forbid();
                }
            }

            var staff = await _context.Users.FindAsync(model.StaffId);
            if (staff == null || staff.Role != "Staff")
            {
                _logger.LogWarning("Staff user not found or invalid role: {StaffId}", model.StaffId);
                return NotFound(new { message = "Nhân viên không tồn tại hoặc không phải Staff." });
            }

            if (staff.BrandId.HasValue)
            {
                _logger.LogWarning("Staff {StaffId} already assigned to brand {BrandId}", model.StaffId, staff.BrandId);
                return BadRequest(new { message = "Nhân viên đã được gán cho một brand." });
            }

            staff.BrandId = brandId;
            staff.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            _logger.LogInformation("Added staff {StaffId} to brand {BrandId}", model.StaffId, brandId);
            return Ok(new { message = "Thêm nhân viên vào brand thành công." });
        }

        [HttpDelete("{brandId}/staff/{staffId}")]
        [Authorize(Roles = "Admin,Staff")]
        public async Task<IActionResult> RemoveStaff(int brandId, int staffId)
        {
            _logger.LogInformation("RemoveStaff called for brand {BrandId}, staff {StaffId}", brandId, staffId);

            var userId = GetUserId();
            if (!userId.HasValue)
            {
                _logger.LogWarning("Invalid userId from token");
                return Unauthorized(new { message = "Token không hợp lệ." });
            }

            var brand = await _context.Brands.FindAsync(brandId);
            if (brand == null)
            {
                _logger.LogWarning("Brand not found: {BrandId}", brandId);
                return NotFound(new { message = "Brand không tồn tại." });
            }

            if (User.IsInRole("Staff") && brand.OwnerId != userId.Value)
            {
                var hasPermission = await _context.StaffPermissions
                    .AnyAsync(sp => sp.StaffId == userId.Value && sp.Permission.Name == "ManageStaff");
                if (!hasPermission)
                {
                    _logger.LogWarning("Staff user {UserId} does not have ManageStaff permission for brand {BrandId}", userId.Value, brandId);
                    return Forbid();
                }
            }

            var staff = await _context.Users.FindAsync(staffId);
            if (staff == null || staff.Role != "Staff" || staff.BrandId != brandId)
            {
                _logger.LogWarning("Staff user not found, invalid role, or not in brand: {StaffId}, BrandId: {BrandId}", staffId, brandId);
                return NotFound(new { message = "Nhân viên không tồn tại, không phải Staff, hoặc không thuộc brand này." });
            }

            staff.BrandId = null;
            staff.UpdatedAt = DateTime.UtcNow;

            var permissions = await _context.StaffPermissions
                .Where(sp => sp.StaffId == staffId)
                .ToListAsync();
            _context.StaffPermissions.RemoveRange(permissions);

            await _context.SaveChangesAsync();

            _logger.LogInformation("Removed staff {StaffId} from brand {BrandId}", staffId, brandId);
            return Ok(new { message = "Xóa nhân viên khỏi brand thành công." });
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

    public class AddStaffDTO
    {
        public int StaffId { get; set; }
    }
}