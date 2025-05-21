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
            var user = await _context.Users.FindAsync(userId);
            if (user == null || user.BrandId != null) return false;

            var brands = await _context.Brands.Where(b => b.OwnerId == userId).ToListAsync();
            return brands.Any();
        }

        private async Task<bool> CanManageStaff(int userId, int staffId)
        {
            var staff = await _context.Users.FindAsync(staffId);
            if (staff == null || staff.BrandId == null || staff.ApprovalStatus != "Approved") return false;

            var brand = await _context.Brands.FindAsync(staff.BrandId);
            if (brand == null) return false;

            return brand.OwnerId == userId;
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<StaffPermission>>> GetAll()
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

            _logger.LogInformation("GetAll staff roles called");
            var staffPermissions = await _context.StaffPermissions.ToListAsync();
            return Ok(staffPermissions);
        }

        [HttpPost("assign")]
        public async Task<IActionResult> AssignRoles([FromBody] AssignRoleDto dto)
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

            _logger.LogInformation("AssignRoles called for staff ID: {StaffId}", dto.StaffId);

            var staff = await _context.Users.FindAsync(dto.StaffId);
            if (staff == null || (staff.Role != "Staff" && staff.Role != "InventoryManager" && staff.Role != "BrandManager"))
            {
                _logger.LogWarning("Staff not found or invalid role for ID: {StaffId}", dto.StaffId);
                return BadRequest(new { message = "Nhân viên không tồn tại hoặc không hợp lệ." });
            }

            // Validate the roles being assigned
            var validRoles = new List<string> { "Staff", "InventoryManager", "BrandManager" };
            if (dto.Roles.Any(role => !validRoles.Contains(role)))
            {
                _logger.LogWarning("Invalid role assignment for staff ID: {StaffId}", dto.StaffId);
                return BadRequest(new { message = "Một hoặc nhiều vai trò không hợp lệ." });
            }

            // Remove existing role assignments for this staff member
            var existingRoles = await _context.StaffPermissions
                .Where(sp => sp.StaffId == dto.StaffId)
                .ToListAsync();
            _context.StaffPermissions.RemoveRange(existingRoles);

            // Assign new roles
            foreach (var role in dto.Roles)
            {
                _context.StaffPermissions.Add(new StaffPermission { StaffId = dto.StaffId, Role = role });
            }

            await _context.SaveChangesAsync();
            _logger.LogInformation("Assigned roles to staff ID: {StaffId}", dto.StaffId);
            return Ok(new { message = "Phân công vai trò thành công." });
        }

        [HttpGet("check")]
        public async Task<IActionResult> CheckRole(string roleName)
        {
            var userId = GetUserId();
            if (!userId.HasValue)
            {
                _logger.LogWarning("Invalid userId from token");
                return Unauthorized(new { message = "Token không hợp lệ." });
            }

            _logger.LogInformation("CheckRole called for role: {RoleName}", roleName);

            var hasRole = await _context.StaffPermissions
                .AnyAsync(sp => sp.StaffId == userId.Value && sp.Role == roleName);

            // Brand owners have full access, no need to check roles
            var isBrandOwner = await IsBrandOwner(userId.Value);
            if (hasRole || isBrandOwner)
            {
                return Ok(new { hasAccess = true });
            }

            _logger.LogWarning("User {UserId} does not have role: {RoleName}", userId, roleName);
            return StatusCode(403, new { message = "Bạn không được phân công vai trò này." });
        }
    }

    public class AssignRoleDto
    {
        public int StaffId { get; set; }
        public List<string> Roles { get; set; }  // List of roles like ["Staff", "InventoryManager"]
    }
}