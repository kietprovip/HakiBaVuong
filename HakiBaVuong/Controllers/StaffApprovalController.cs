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

        [HttpPost("apply/{brandId}")]
        [Authorize(Roles = "Staff")]
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
        [Authorize(Roles = "Admin,Staff")]
        public async Task<ActionResult<IEnumerable<UserDTO>>> GetPendingApplications()
        {
            var userId = GetUserId();
            if (!userId.HasValue)
            {
                _logger.LogWarning("Invalid userId from token");
                return Unauthorized(new { message = "Token không hợp lệ." });
            }

            var owner = await _context.Users.FindAsync(userId.Value);
            if (owner == null)
            {
                _logger.LogWarning("User not found: {UserId}", userId);
                return NotFound(new { message = "Người dùng không tồn tại." });
            }

            // Lấy tất cả các brand mà owner sở hữu
            var brands = await _context.Brands.Where(b => b.OwnerId == userId.Value).ToListAsync();
            if (brands == null || !brands.Any())
            {
                _logger.LogWarning("No brands found for owner: {UserId}", userId);
                return NotFound(new { message = "Bạn không phải là chủ của bất kỳ thương hiệu nào." });
            }

            // Lấy tất cả các ứng viên pending từ các brand của owner
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
        [Authorize(Roles = "Admin,Staff")]
        public async Task<ActionResult<IEnumerable<UserDTO>>> GetApprovedStaff()
        {
            var userId = GetUserId();
            if (!userId.HasValue)
            {
                _logger.LogWarning("Invalid userId from token");
                return Unauthorized(new { message = "Token không hợp lệ." });
            }

            var owner = await _context.Users.FindAsync(userId.Value);
            if (owner == null)
            {
                _logger.LogWarning("User not found: {UserId}", userId);
                return NotFound(new { message = "Người dùng không tồn tại." });
            }

            var brand = await _context.Brands.FirstOrDefaultAsync(b => b.OwnerId == userId.Value);
            if (brand == null)
            {
                _logger.LogWarning("Brand not found for owner: {UserId}", userId);
                return NotFound(new { message = "Bạn không phải là chủ thương hiệu." });
            }

            var approvedStaff = await _context.Users
                .Where(u => u.BrandId == brand.BrandId && u.ApprovalStatus == "Approved")
                .ToListAsync();

            var userDTOs = _mapper.Map<List<UserDTO>>(approvedStaff);
            return Ok(userDTOs);
        }

        [HttpPost("approve/{userId}")]
        [Authorize(Roles = "Admin,Staff")]
        public async Task<IActionResult> ApproveApplication(int userId)
        {
            var ownerId = GetUserId();
            if (!ownerId.HasValue)
            {
                _logger.LogWarning("Invalid ownerId from token");
                return Unauthorized(new { message = "Token không hợp lệ." });
            }

            var owner = await _context.Users.FindAsync(ownerId.Value);
            if (owner == null)
            {
                _logger.LogWarning("Owner not found: {OwnerId}", ownerId);
                return NotFound(new { message = "Người dùng không tồn tại." });
            }

            var brand = await _context.Brands.FirstOrDefaultAsync(b => b.OwnerId == ownerId.Value);
            if (brand == null)
            {
                _logger.LogWarning("Brand not found for owner: {OwnerId}", ownerId);
                return NotFound(new { message = "Bạn không phải là chủ thương hiệu." });
            }

            var user = await _context.Users.FindAsync(userId);
            if (user == null)
            {
                _logger.LogWarning("User not found: {UserId}", userId);
                return NotFound(new { message = "Người dùng không tồn tại." });
            }

            if (user.BrandId != brand.BrandId || user.ApprovalStatus != "Pending")
            {
                _logger.LogWarning("User {UserId} is not a pending applicant for brand {BrandId}", userId, brand.BrandId);
                return BadRequest(new { message = "Người dùng không có đơn ứng tuyển hợp lệ." });
            }

            user.ApprovalStatus = "Approved";
            _context.Users.Update(user);
            await _context.SaveChangesAsync();

            _logger.LogInformation("User {UserId} approved for brand {BrandId}", userId, brand.BrandId);
            return Ok(new { message = "Duyệt nhân viên thành công." });
        }

        [HttpPost("reject/{userId}")]
        [Authorize(Roles = "Admin,Staff")]
        public async Task<IActionResult> RejectApplication(int userId)
        {
            var ownerId = GetUserId();
            if (!ownerId.HasValue)
            {
                _logger.LogWarning("Invalid ownerId from token");
                return Unauthorized(new { message = "Token không hợp lệ." });
            }

            var owner = await _context.Users.FindAsync(ownerId.Value);
            if (owner == null)
            {
                _logger.LogWarning("Owner not found: {OwnerId}", ownerId);
                return NotFound(new { message = "Người dùng không tồn tại." });
            }

            var brand = await _context.Brands.FirstOrDefaultAsync(b => b.OwnerId == ownerId.Value);
            if (brand == null)
            {
                _logger.LogWarning("Brand not found for owner: {OwnerId}", ownerId);
                return NotFound(new { message = "Bạn không phải là chủ thương hiệu." });
            }

            var user = await _context.Users.FindAsync(userId);
            if (user == null)
            {
                _logger.LogWarning("User not found: {UserId}", userId);
                return NotFound(new { message = "Người dùng không tồn tại." });
            }

            if (user.BrandId != brand.BrandId || user.ApprovalStatus != "Pending")
            {
                _logger.LogWarning("User {UserId} is not a pending applicant for brand {BrandId}", userId, brand.BrandId);
                return BadRequest(new { message = "Người dùng không có đơn ứng tuyển hợp lệ." });
            }

            user.BrandId = null;
            user.ApprovalStatus = null;
            _context.Users.Update(user);
            await _context.SaveChangesAsync();

            _logger.LogInformation("User {UserId} rejected for brand {BrandId}", userId, brand.BrandId);
            return Ok(new { message = "Đã từ chối đơn ứng tuyển." });
        }

        [HttpPut("update/{userId}")]
        [Authorize(Roles = "Admin,Staff")]
        public async Task<IActionResult> UpdateStaff(int userId, [FromBody] UserDTO userDto)
        {
            var ownerId = GetUserId();
            if (!ownerId.HasValue) return Unauthorized(new { message = "Token không hợp lệ." });

            var owner = await _context.Users.FindAsync(ownerId.Value);
            if (owner == null) return NotFound(new { message = "Người dùng không tồn tại." });

            var brand = await _context.Brands.FirstOrDefaultAsync(b => b.OwnerId == ownerId.Value);
            if (brand == null) return NotFound(new { message = "Bạn không phải là chủ thương hiệu." });

            var user = await _context.Users.FindAsync(userId);
            if (user == null) return NotFound(new { message = "Nhân viên không tồn tại." });

            if (user.BrandId != brand.BrandId || user.ApprovalStatus != "Approved")
                return BadRequest(new { message = "Nhân viên không thuộc thương hiệu hoặc chưa được duyệt." });

            user.Name = userDto.Name;
            user.Email = userDto.Email;
            user.Role = userDto.Role;
            _context.Users.Update(user);
            await _context.SaveChangesAsync();

            _logger.LogInformation("User {UserId} updated by {OwnerId}", userId, ownerId);
            return Ok(new { message = "Cập nhật nhân viên thành công." });
        }

        [HttpDelete("delete/{userId}")]
        [Authorize(Roles = "Admin,Staff")]
        public async Task<IActionResult> DeleteStaff(int userId)
        {
            var ownerId = GetUserId();
            if (!ownerId.HasValue) return Unauthorized(new { message = "Token không hợp lệ." });

            var owner = await _context.Users.FindAsync(ownerId.Value);
            if (owner == null) return NotFound(new { message = "Người dùng không tồn tại." });

            var brand = await _context.Brands.FirstOrDefaultAsync(b => b.OwnerId == ownerId.Value);
            if (brand == null) return NotFound(new { message = "Bạn không phải là chủ thương hiệu." });

            var user = await _context.Users.FindAsync(userId);
            if (user == null) return NotFound(new { message = "Nhân viên không tồn tại." });

            if (user.BrandId != brand.BrandId || user.ApprovalStatus != "Approved")
                return BadRequest(new { message = "Nhân viên không thuộc thương hiệu hoặc chưa được duyệt." });

            _context.Users.Remove(user);
            await _context.SaveChangesAsync();

            _logger.LogInformation("User {UserId} deleted by {OwnerId}", userId, ownerId);
            return Ok(new { message = "Xóa nhân viên thành công." });
        }

        private int? GetUserId()
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            return int.TryParse(userIdClaim, out int userId) ? userId : null;
        }
    }
}