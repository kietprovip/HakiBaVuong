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
    [Authorize(Roles = "Admin,Staff")]
    public class PermissionController : ControllerBase
    {
        private readonly DataContext _context;
        private readonly ILogger<PermissionController> _logger;

        public PermissionController(DataContext context, ILogger<PermissionController> logger)
        {
            _context = context;
            _logger = logger;
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<PermissionDTO>>> GetAllPermissions()
        {
            _logger.LogInformation("GetAllPermissions called");

            var permissions = await _context.Permissions
                .Select(p => new PermissionDTO
                {
                    Name = p.Name,
                    Description = p.Description
                })
                .ToListAsync();

            _logger.LogInformation("Retrieved {Count} permissions", permissions.Count);
            return Ok(permissions);
        }

        [HttpGet("brand/{brandId}/staff/{staffId}")]
        public async Task<ActionResult<IEnumerable<PermissionDTO>>> GetStaffPermissions(int brandId, int staffId)
        {
            _logger.LogInformation("GetStaffPermissions called for brand {BrandId}, staff {StaffId}", brandId, staffId);

            var currentUserId = GetUserId();
            if (currentUserId == null)
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

            if (User.IsInRole("Staff") && brand.OwnerId != currentUserId)
            {
                _logger.LogWarning("Staff user {UserId} does not have access to brand {BrandId}", currentUserId, brandId);
                return Forbid();
            }

            var staff = await _context.Users.FindAsync(staffId);
            if (staff == null || staff.Role != "Staff" || staff.BrandId != brandId)
            {
                _logger.LogWarning("Staff user not found, invalid role, or not in brand: {StaffId}, BrandId: {BrandId}", staffId, brandId);
                return NotFound(new { message = "Nhân viên không tồn tại, không phải Staff, hoặc không thuộc brand này." });
            }

            var permissions = await _context.StaffPermissions
                .Where(sp => sp.StaffId == staffId)
                .Include(sp => sp.Permission)
                .Select(sp => new PermissionDTO
                {
                    Name = sp.Permission.Name,
                    Description = sp.Permission.Description
                })
                .ToListAsync();

            _logger.LogInformation("Retrieved {Count} permissions for staff {StaffId} in brand {BrandId}", permissions.Count, staffId, brandId);
            return Ok(permissions);
        }

        [HttpPost("brand/{brandId}/staff/{staffId}")]
        public async Task<IActionResult> AssignPermission(int brandId, int staffId, [FromBody] AssignPermissionDTO model)
        {
            _logger.LogInformation("AssignPermission called for brand {BrandId}, staff {StaffId}, permission {PermissionName}", brandId, staffId, model.PermissionName);

            var currentUserId = GetUserId();
            if (currentUserId == null)
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

            if (User.IsInRole("Staff") && brand.OwnerId != currentUserId)
            {
                _logger.LogWarning("Staff user {UserId} does not have access to brand {BrandId}", currentUserId, brandId);
                return Forbid();
            }

            var staff = await _context.Users.FindAsync(staffId);
            if (staff == null || staff.Role != "Staff" || staff.BrandId != brandId)
            {
                _logger.LogWarning("Staff user not found, invalid role, or not in brand: {StaffId}, BrandId: {BrandId}", staffId, brandId);
                return NotFound(new { message = "Nhân viên không tồn tại, không phải Staff, hoặc không thuộc brand này." });
            }

            var permission = await _context.Permissions
                .FirstOrDefaultAsync(p => p.Name == model.PermissionName);
            if (permission == null)
            {
                _logger.LogWarning("Permission not found: {PermissionName}", model.PermissionName);
                return NotFound(new { message = "Quyền không tồn tại." });
            }

            var existingPermission = await _context.StaffPermissions
                .FirstOrDefaultAsync(sp => sp.StaffId == staffId && sp.PermissionId == permission.PermissionId);
            if (existingPermission != null)
            {
                _logger.LogWarning("Permission {PermissionName} already assigned to staff {StaffId}", model.PermissionName, staffId);
                return BadRequest(new { message = "Quyền đã được gán cho nhân viên." });
            }

            var staffPermission = new StaffPermission
            {
                StaffId = staffId,
                PermissionId = permission.PermissionId
            };

            _context.StaffPermissions.Add(staffPermission);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Assigned permission {PermissionName} to staff {StaffId} in brand {BrandId}", model.PermissionName, staffId, brandId);
            return Ok(new { message = "Gán quyền thành công." });
        }

        [HttpDelete("brand/{brandId}/staff/{staffId}/permission/{permissionId}")]
        public async Task<IActionResult> RemovePermission(int brandId, int staffId, int permissionId)
        {
            _logger.LogInformation("RemovePermission called for brand {BrandId}, staff {StaffId}, permission {PermissionId}", brandId, staffId, permissionId);

            var currentUserId = GetUserId();
            if (currentUserId == null)
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

            if (User.IsInRole("Staff") && brand.OwnerId != currentUserId)
            {
                _logger.LogWarning("Staff user {UserId} does not have access to brand {BrandId}", currentUserId, brandId);
                return Forbid();
            }

            var staff = await _context.Users.FindAsync(staffId);
            if (staff == null || staff.Role != "Staff" || staff.BrandId != brandId)
            {
                _logger.LogWarning("Staff user not found, invalid role, or not in brand: {StaffId}, BrandId: {BrandId}", staffId, brandId);
                return NotFound(new { message = "Nhân viên không tồn tại, không phải Staff, hoặc không thuộc brand này." });
            }

            var staffPermission = await _context.StaffPermissions
                .FirstOrDefaultAsync(sp => sp.StaffId == staffId && sp.PermissionId == permissionId);
            if (staffPermission == null)
            {
                _logger.LogWarning("Permission {PermissionId} not found for staff {StaffId}", permissionId, staffId);
                return NotFound(new { message = "Quyền không được gán cho nhân viên." });
            }

            _context.StaffPermissions.Remove(staffPermission);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Removed permission {PermissionId} from staff {StaffId} in brand {BrandId}", permissionId, staffId, brandId);
            return Ok(new { message = "Xóa quyền thành công." });
        }

        private int? GetUserId()
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            return int.TryParse(userIdClaim, out int userId) ? userId : null;
        }
    }

    public class AssignPermissionDTO
    {
        public string PermissionName { get; set; }
    }
}