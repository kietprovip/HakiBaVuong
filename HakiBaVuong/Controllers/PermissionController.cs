using HakiBaVuong.DTOs;
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
        public async Task<ActionResult<IEnumerable<UserDTO>>> GetAll()
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

            _logger.LogInformation("GetAll staff called");
            var staff = await _context.Users
                .Where(u => u.BrandId != null && u.ApprovalStatus == "Approved")
                .Select(u => new UserDTO
                {
                    UserId = u.UserId,
                    Name = u.Name,
                    Email = u.Email,
                    Role = u.Role,
                    BrandId = u.BrandId,
                    ApprovalStatus = u.ApprovalStatus,
                    CreatedAt = u.CreatedAt
                })
                .ToListAsync();
            return Ok(staff);
        }

        [HttpPost("assign")]
        public async Task<IActionResult> AssignRole([FromBody] AssignRoleDto dto)
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

            _logger.LogInformation("AssignRole called for staff ID: {StaffId}", dto.StaffId);

            var staff = await _context.Users.FindAsync(dto.StaffId);
            if (staff == null)
            {
                _logger.LogWarning("Staff not found for ID: {StaffId}", dto.StaffId);
                return BadRequest(new { message = "Nhân viên không tồn tại." });
            }

            var validRoles = new List<string> { "InventoryManager", "BrandManager" };
            if (!validRoles.Contains(dto.Role))
            {
                _logger.LogWarning("Invalid role assignment for staff ID: {StaffId}", dto.StaffId);
                return BadRequest(new { message = "Vai trò không hợp lệ." });
            }

            staff.Role = dto.Role;
            await _context.SaveChangesAsync();
            _logger.LogInformation("Assigned role to staff ID: {StaffId}", dto.StaffId);
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

            var user = await _context.Users.FindAsync(userId.Value);
            if (user == null)
            {
                _logger.LogWarning("User not found for ID: {UserId}", userId);
                return Unauthorized(new { message = "Người dùng không tồn tại." });
            }

            var isBrandOwner = await IsBrandOwner(userId.Value);
            var hasRole = user.Role == roleName;

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
        public string Role { get; set; }
    }
}