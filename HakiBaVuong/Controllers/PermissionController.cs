using HakiBaVuong.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using HakiBaVuong.Data;
using System.Security.Claims;

namespace HakiBaVuong.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class PermissionController : ControllerBase
    {
        private readonly DataContext _context;
        private readonly ILogger<PermissionController> _logger;

        public PermissionController(DataContext context, ILogger<PermissionController> logger)
        {
            _context = context;
            _logger = logger;
        }

        private int? GetUserId()
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            return int.TryParse(userIdClaim, out int userId) ? userId : null;
        }

        private async Task<bool> IsBrandOwner(int userId)
        {
            // Kiểm tra xem user có phải là chủ brand không: BrandId = null và có brand mà user sở hữu (OwnerId khớp với UserId)
            var user = await _context.Users.FindAsync(userId);
            if (user == null || user.BrandId != null) return false;

            var brands = await _context.Brands.Where(b => b.OwnerId == userId).ToListAsync();
            return brands.Any();
        }

        private async Task<bool> CanManageStaff(int userId, int staffId)
        {
            // Kiểm tra xem user có quyền quản lý nhân viên này không
            var staff = await _context.Users.FindAsync(staffId);
            if (staff == null || staff.BrandId == null || staff.ApprovalStatus != "Approved") return false;

            var brand = await _context.Brands.FindAsync(staff.BrandId);
            if (brand == null) return false;

            return brand.OwnerId == userId;
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<Permission>>> GetAll()
        {
            var userId = GetUserId();
            if (!userId.HasValue)
            {
                _logger.LogWarning("Invalid userId from token");
                return Unauthorized(new { message = "Token không hợp lệ." });
            }

            if (!await IsBrandOwner(userId.Value))
            {
                _logger.LogWarning("User {UserId} is not a brand owner", userId);
                return StatusCode(403, new { message = "Bạn không có quyền truy cập API này." });
            }

            _logger.LogInformation("GetAll permissions called");
            var permissions = await _context.Permissions.ToListAsync();
            return Ok(permissions);
        }

        [HttpPost("assign")]
        public async Task<IActionResult> AssignPermissions([FromBody] AssignPermissionDto dto)
        {
            var userId = GetUserId();
            if (!userId.HasValue)
            {
                _logger.LogWarning("Invalid userId from token");
                return Unauthorized(new { message = "Token không hợp lệ." });
            }

            if (!await IsBrandOwner(userId.Value))
            {
                _logger.LogWarning("User {UserId} is not a brand owner", userId);
                return StatusCode(403, new { message = "Bạn không có quyền truy cập API này." });
            }

            if (!await CanManageStaff(userId.Value, dto.StaffId))
            {
                _logger.LogWarning("User {UserId} cannot manage staff {StaffId}", userId, dto.StaffId);
                return StatusCode(403, new { message = "Bạn không có quyền quản lý nhân viên này." });
            }

            _logger.LogInformation("AssignPermissions called for staff ID: {StaffId}", dto.StaffId);

            var staff = await _context.Users.FindAsync(dto.StaffId);
            if (staff == null || staff.Role != "Staff")
            {
                _logger.LogWarning("Staff not found or invalid role for ID: {StaffId}", dto.StaffId);
                return BadRequest(new { message = "Nhân viên không tồn tại hoặc không hợp lệ." });
            }

            var existingPermissions = await _context.StaffPermissions
                .Where(sp => sp.StaffId == dto.StaffId)
                .ToListAsync();
            _context.StaffPermissions.RemoveRange(existingPermissions);

            foreach (var permissionId in dto.PermissionIds)
            {
                var permission = await _context.Permissions.FindAsync(permissionId);
                if (permission == null)
                {
                    _logger.LogWarning("Permission not found: {PermissionId}", permissionId);
                    return BadRequest(new { message = "Quyền không tồn tại." });
                }
                _context.StaffPermissions.Add(new StaffPermission { StaffId = dto.StaffId, PermissionId = permissionId });
            }

            await _context.SaveChangesAsync();
            _logger.LogInformation("Assigned permissions to staff ID: {StaffId}", dto.StaffId);
            return Ok(new { message = "Phân công quyền thành công." });
        }

        [HttpGet("check")]
        public async Task<IActionResult> CheckPermission(string permissionName)
        {
            var userId = GetUserId();
            if (!userId.HasValue)
            {
                _logger.LogWarning("Invalid userId from token");
                return Unauthorized(new { message = "Token không hợp lệ." });
            }

            _logger.LogInformation("CheckPermission called for permission: {PermissionName}", permissionName);

            var hasPermission = await _context.StaffPermissions
                .AnyAsync(sp => sp.StaffId == userId.Value && sp.Permission.Name == permissionName);

            // Chủ brand không cần kiểm tra quyền, họ có toàn quyền
            var isBrandOwner = await IsBrandOwner(userId.Value);
            if (hasPermission || isBrandOwner)
            {
                return Ok(new { hasAccess = true });
            }

            _logger.LogWarning("User {UserId} does not have permission: {PermissionName}", userId, permissionName);
            return StatusCode(403, new { message = "Bạn không được phân công quyền này." });
        }
    }

    public class AssignPermissionDto
    {
        public int StaffId { get; set; }
        public List<int> PermissionIds { get; set; }
    }
}