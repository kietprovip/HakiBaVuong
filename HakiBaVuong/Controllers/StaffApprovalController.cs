using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authorization;
using HakiBaVuong.Models;
using HakiBaVuong.Data;
using HakiBaVuong.DTOs;
using AutoMapper;
using System.Security.Claims;

namespace HakiBaVuong.Controllers
{
    [Route("api/staff-approval")]
    [ApiController]
    [Authorize]
    public class StaffApprovalController : ControllerBase
    {
        private readonly DataContext _context;
        private readonly IMapper _mapper;
        private readonly ILogger<StaffApprovalController> _logger;

        public StaffApprovalController(DataContext context, IMapper mapper, ILogger<StaffApprovalController> logger)
        {
            _context = context;
            _mapper = mapper;
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

        [HttpPost("apply/{brandId}")]
        public async Task<IActionResult> ApplyToBrand(int brandId)
        {
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

            if (user.BrandId != null || user.ApprovalStatus != null)
            {
                _logger.LogWarning("User already belongs to a brand or has a pending application: {UserId}", userId);
                return BadRequest(new { message = "Bạn đã thuộc về một thương hiệu hoặc đang có đơn ứng tuyển." });
            }

            var brand = await _context.Brands.FindAsync(brandId);
            if (brand == null)
            {
                _logger.LogWarning("Brand not found: {BrandId}", brandId);
                return NotFound(new { message = "Thương hiệu không tồn tại." });
            }

            user.BrandId = brandId;
            user.ApprovalStatus = "Pending";
            _context.Users.Update(user);
            await _context.SaveChangesAsync();

            _logger.LogInformation("User {UserId} applied to brand {BrandId}", userId, brandId);
            return Ok(new { message = "Đã gửi yêu cầu tham gia thương hiệu. Vui lòng chờ duyệt." });
        }

        [HttpGet("pending-applications")]
        public async Task<ActionResult<IEnumerable<UserDTO>>> GetPendingApplications()
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

            var brands = await _context.Brands.Where(b => b.OwnerId == userId.Value).ToListAsync();
            if (brands == null || !brands.Any())
            {
                _logger.LogWarning("No brands found for owner: {UserId}", userId);
                return NotFound(new { message = "Bạn không phải là chủ của bất kỳ thương hiệu nào." });
            }

            var pendingUsers = new List<User>();
            foreach (var brand in brands)
            {
                var users = await _context.Users
                    .Where(u => u.BrandId == brand.BrandId && u.ApprovalStatus == "Pending")
                    .ToListAsync();
                pendingUsers.AddRange(users);
            }

            var userDTOs = _mapper.Map<List<UserDTO>>(pendingUsers);
            return Ok(userDTOs);
        }

        [HttpGet("approved-staff")]
        public async Task<ActionResult<IEnumerable<UserDTO>>> GetApprovedStaff()
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

            var brands = await _context.Brands.Where(b => b.OwnerId == userId.Value).ToListAsync();
            if (brands == null || !brands.Any())
            {
                _logger.LogWarning("No brands found for owner: {UserId}", userId);
                return NotFound(new { message = "Bạn không phải là chủ của bất kỳ thương hiệu nào." });
            }

            var approvedStaff = new List<User>();
            foreach (var brand in brands)
            {
                var staff = await _context.Users
                    .Where(u => u.BrandId == brand.BrandId && u.ApprovalStatus == "Approved")
                    .ToListAsync();
                approvedStaff.AddRange(staff);
            }

            var userDTOs = _mapper.Map<List<UserDTO>>(approvedStaff);
            return Ok(userDTOs);
        }

        [HttpPost("approve/{userId}")]
        public async Task<IActionResult> ApproveApplication(int userId)
        {
            var ownerId = GetUserId();
            if (!ownerId.HasValue)
            {
                _logger.LogWarning("Invalid ownerId from token");
                return Unauthorized(new { message = "Token không hợp lệ." });
            }

            if (!await IsBrandOwner(ownerId.Value))
            {
                _logger.LogWarning("User {UserId} is not a brand owner", ownerId);
                return StatusCode(403, new { message = "Bạn không có quyền duyệt nhân viên." });
            }

            var userToApprove = await _context.Users.FindAsync(userId);
            if (userToApprove == null)
            {
                _logger.LogWarning("User not found: {UserId}", userId);
                return NotFound(new { message = "Người dùng không tồn tại." });
            }

            var brand = await _context.Brands.FindAsync(userToApprove.BrandId);
            if (brand == null || brand.OwnerId != ownerId.Value || userToApprove.ApprovalStatus != "Pending")
            {
                _logger.LogWarning("User {UserId} is not a pending applicant for any brand owned by {OwnerId}", userId, ownerId);
                return BadRequest(new { message = "Người dùng không có đơn ứng tuyển hợp lệ." });
            }

            userToApprove.ApprovalStatus = "Approved";
            userToApprove.Role = "InventoryManager";
            _context.Users.Update(userToApprove);
            await _context.SaveChangesAsync();

            _logger.LogInformation("User {UserId} approved for brand {BrandId}", userId, userToApprove.BrandId);
            return Ok(new { message = "Duyệt nhân viên thành công." });
        }

        [HttpPost("reject/{userId}")]
        public async Task<IActionResult> RejectApplication(int userId)
        {
            var ownerId = GetUserId();
            if (!ownerId.HasValue)
            {
                _logger.LogWarning("Invalid ownerId from token");
                return Unauthorized(new { message = "Token không hợp lệ." });
            }

            if (!await IsBrandOwner(ownerId.Value))
            {
                _logger.LogWarning("User {UserId} is not a brand owner", ownerId);
                return StatusCode(403, new { message = "Bạn không có quyền từ chối nhân viên." });
            }

            var userToReject = await _context.Users.FindAsync(userId);
            if (userToReject == null)
            {
                _logger.LogWarning("User not found: {UserId}", userId);
                return NotFound(new { message = "Người dùng không tồn tại." });
            }

            var brand = await _context.Brands.FindAsync(userToReject.BrandId);
            if (brand == null || brand.OwnerId != ownerId.Value || userToReject.ApprovalStatus != "Pending")
            {
                _logger.LogWarning("User {UserId} is not a pending applicant for any brand owned by {OwnerId}", userId, ownerId);
                return BadRequest(new { message = "Người dùng không có đơn ứng tuyển hợp lệ." });
            }

            userToReject.BrandId = null;
            userToReject.ApprovalStatus = null;
            _context.Users.Update(userToReject);
            await _context.SaveChangesAsync();

            _logger.LogInformation("User {UserId} rejected for brand {BrandId}", userId, userToReject.BrandId);
            return Ok(new { message = "Đã từ chối đơn ứng tuyển." });
        }

        [HttpPut("update/{userId}")]
        public async Task<IActionResult> UpdateStaff(int userId, [FromBody] UserDTO userDto)
        {
            var ownerId = GetUserId();
            if (!ownerId.HasValue)
            {
                _logger.LogWarning("Invalid ownerId from token");
                return Unauthorized(new { message = "Token không hợp lệ." });
            }

            if (!await IsBrandOwner(ownerId.Value))
            {
                _logger.LogWarning("User {UserId} is not a brand owner", ownerId);
                return StatusCode(403, new { message = "Bạn không có quyền cập nhật nhân viên." });
            }

            var userToUpdate = await _context.Users.FindAsync(userId);
            if (userToUpdate == null)
            {
                _logger.LogWarning("User not found: {UserId}", userId);
                return NotFound(new { message = "Nhân viên không tồn tại." });
            }

            var brand = await _context.Brands.FindAsync(userToUpdate.BrandId);
            if (brand == null || brand.OwnerId != ownerId.Value || userToUpdate.ApprovalStatus != "Approved")
            {
                _logger.LogWarning("User {UserId} is not an approved staff for any brand owned by {OwnerId}", userId, ownerId);
                return BadRequest(new { message = "Nhân viên không thuộc thương hiệu hoặc chưa được duyệt." });
            }

            var validRoles = new List<string> { "InventoryManager", "BrandManager" };
            if (!validRoles.Contains(userDto.Role))
            {
                _logger.LogWarning("Invalid role for user ID: {UserId}", userId);
                return BadRequest(new { message = "Vai trò không hợp lệ." });
            }

            userToUpdate.Name = userDto.Name;
            userToUpdate.Email = userDto.Email;
            userToUpdate.Role = userDto.Role;
            _context.Users.Update(userToUpdate);
            await _context.SaveChangesAsync();

            _logger.LogInformation("User {UserId} updated by {OwnerId}", userId, ownerId);
            return Ok(new { message = "Cập nhật nhân viên thành công." });
        }

        [HttpDelete("delete/{userId}")]
        public async Task<IActionResult> DeleteStaff(int userId)
        {
            var ownerId = GetUserId();
            if (!ownerId.HasValue)
            {
                _logger.LogWarning("Invalid ownerId from token");
                return Unauthorized(new { message = "Token không hợp lệ." });
            }

            if (!await IsBrandOwner(ownerId.Value))
            {
                _logger.LogWarning("User {UserId} is not a brand owner", ownerId);
                return StatusCode(403, new { message = "Bạn không có quyền xóa nhân viên." });
            }

            var userToDelete = await _context.Users.FindAsync(userId);
            if (userToDelete == null)
            {
                _logger.LogWarning("User not found: {UserId}", userId);
                return NotFound(new { message = "Nhân viên không tồn tại." });
            }

            var brand = await _context.Brands.FindAsync(userToDelete.BrandId);
            if (brand == null || brand.OwnerId != ownerId.Value || userToDelete.ApprovalStatus != "Approved")
            {
                _logger.LogWarning("User {UserId} is not an approved staff for any brand owned by {OwnerId}", userId, ownerId);
                return BadRequest(new { message = "Nhân viên không thuộc thương hiệu hoặc chưa được duyệt." });
            }

            userToDelete.BrandId = null;
            userToDelete.ApprovalStatus = null;
            userToDelete.Role = "Staff";
            _context.Users.Update(userToDelete);
            await _context.SaveChangesAsync();

            _logger.LogInformation("User {UserId} removed from brand by {OwnerId}", userId, ownerId);
            return Ok(new { message = "Đã xóa nhân viên khỏi thương hiệu thành công." });
        }
    }
}