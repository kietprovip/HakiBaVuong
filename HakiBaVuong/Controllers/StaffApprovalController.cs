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
        [Authorize(Roles = "Staff,InventoryManager,BrandManager")]
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
        [Authorize(Roles = "Admin,Staff,BrandManager")]
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

            var brands = await _context.Brands.Where(b => b.OwnerId == userId.Value).ToListAsync();
            if (brands == null || !brands.Any())
            {
                _logger.LogWarning("No brands found for owner: {UserId}", userId);
                return NotFound(new { message = "Bạn không phải là chủ của bất kỳ thương hiệu nào." });
            }

            if (User.IsInRole("Staff") || User.IsInRole("BrandManager"))
            {
                var user = await _context.Users.FindAsync(userId.Value);
                if (user == null)
                {
                    _logger.LogWarning("User not found: {UserId}", userId);
                    return NotFound(new { message = "Người dùng không tồn tại." });
                }

                int effectiveOwnerId;
                if (user.BrandId.HasValue)
                {
                    var userBrand = await _context.Brands.FindAsync(user.BrandId.Value);
                    if (userBrand == null)
                    {
                        _logger.LogWarning("Brand not found for BrandId: {BrandId}", user.BrandId);
                        return NotFound(new { message = "Thương hiệu không tồn tại." });
                    }
                    effectiveOwnerId = userBrand.OwnerId;
                }
                else
                {
                    effectiveOwnerId = userId.Value;
                }

                brands = brands.Where(b => b.OwnerId == effectiveOwnerId).ToList();
                if (!brands.Any())
                {
                    _logger.LogWarning("No brands found for user {UserId} after permission check", userId);
                    return NotFound(new { message = "Bạn không có quyền truy cập thương hiệu nào." });
                }
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
        [Authorize(Roles = "Admin,Staff,BrandManager")]
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

            var brands = await _context.Brands.Where(b => b.OwnerId == userId.Value).ToListAsync();
            if (brands == null || !brands.Any())
            {
                _logger.LogWarning("No brands found for owner: {UserId}", userId);
                return NotFound(new { message = "Bạn không phải là chủ của bất kỳ thương hiệu nào." });
            }

            if (User.IsInRole("Staff") || User.IsInRole("BrandManager"))
            {
                var user = await _context.Users.FindAsync(userId.Value);
                if (user == null)
                {
                    _logger.LogWarning("User not found: {UserId}", userId);
                    return NotFound(new { message = "Người dùng không tồn tại." });
                }

                int effectiveOwnerId;
                if (user.BrandId.HasValue)
                {
                    var userBrand = await _context.Brands.FindAsync(user.BrandId.Value);
                    if (userBrand == null)
                    {
                        _logger.LogWarning("Brand not found for BrandId: {BrandId}", user.BrandId);
                        return NotFound(new { message = "Thương hiệu không tồn tại." });
                    }
                    effectiveOwnerId = userBrand.OwnerId;
                }
                else
                {
                    effectiveOwnerId = userId.Value;
                }

                brands = brands.Where(b => b.OwnerId == effectiveOwnerId).ToList();
                if (!brands.Any())
                {
                    _logger.LogWarning("No brands found for user {UserId} after permission check", userId);
                    return NotFound(new { message = "Bạn không có quyền truy cập thương hiệu nào." });
                }
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
        [Authorize(Roles = "Admin,Staff,BrandManager")]
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

            var brands = await _context.Brands.Where(b => b.OwnerId == ownerId.Value).ToListAsync();
            if (brands == null || !brands.Any())
            {
                _logger.LogWarning("No brands found for owner: {OwnerId}", ownerId);
                return NotFound(new { message = "Bạn không phải là chủ thương hiệu." });
            }

            if (User.IsInRole("Staff") || User.IsInRole("BrandManager"))
            {
                var user = await _context.Users.FindAsync(ownerId.Value);
                if (user == null)
                {
                    _logger.LogWarning("User not found: {UserId}", ownerId);
                    return NotFound(new { message = "Người dùng không tồn tại." });
                }

                int effectiveOwnerId;
                if (user.BrandId.HasValue)
                {
                    var userBrand = await _context.Brands.FindAsync(user.BrandId.Value);
                    if (userBrand == null)
                    {
                        _logger.LogWarning("Brand not found for BrandId: {BrandId}", user.BrandId);
                        return NotFound(new { message = "Thương hiệu không tồn tại." });
                    }
                    effectiveOwnerId = userBrand.OwnerId;
                }
                else
                {
                    effectiveOwnerId = ownerId.Value;
                }

                brands = brands.Where(b => b.OwnerId == effectiveOwnerId).ToList();
                if (!brands.Any())
                {
                    _logger.LogWarning("No brands found for user {UserId} after permission check", ownerId);
                    return NotFound(new { message = "Bạn không có quyền truy cập thương hiệu nào." });
                }
            }

            var userToApprove = await _context.Users.FindAsync(userId);
            if (userToApprove == null)
            {
                _logger.LogWarning("User not found: {UserId}", userId);
                return NotFound(new { message = "Người dùng không tồn tại." });
            }

            if (!brands.Any(b => b.BrandId == userToApprove.BrandId) || userToApprove.ApprovalStatus != "Pending")
            {
                _logger.LogWarning("User {UserId} is not a pending applicant for any brand owned by {OwnerId}", userId, ownerId);
                return BadRequest(new { message = "Người dùng không có đơn ứng tuyển hợp lệ." });
            }

            userToApprove.ApprovalStatus = "Approved";
            _context.Users.Update(userToApprove);
            await _context.SaveChangesAsync();

            _logger.LogInformation("User {UserId} approved for brand {BrandId}", userId, userToApprove.BrandId);
            return Ok(new { message = "Duyệt nhân viên thành công." });
        }

        [HttpPost("reject/{userId}")]
        [Authorize(Roles = "Admin,Staff,BrandManager")]
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

            var brands = await _context.Brands.Where(b => b.OwnerId == ownerId.Value).ToListAsync();
            if (brands == null || !brands.Any())
            {
                _logger.LogWarning("No brands found for owner: {OwnerId}", ownerId);
                return NotFound(new { message = "Bạn không phải là chủ thương hiệu." });
            }

            if (User.IsInRole("Staff") || User.IsInRole("BrandManager"))
            {
                var user = await _context.Users.FindAsync(ownerId.Value);
                if (user == null)
                {
                    _logger.LogWarning("User not found: {UserId}", ownerId);
                    return NotFound(new { message = "Người dùng không tồn tại." });
                }

                int effectiveOwnerId;
                if (user.BrandId.HasValue)
                {
                    var userBrand = await _context.Brands.FindAsync(user.BrandId.Value);
                    if (userBrand == null)
                    {
                        _logger.LogWarning("Brand not found for BrandId: {BrandId}", user.BrandId);
                        return NotFound(new { message = "Thương hiệu không tồn tại." });
                    }
                    effectiveOwnerId = userBrand.OwnerId;
                }
                else
                {
                    effectiveOwnerId = ownerId.Value;
                }

                brands = brands.Where(b => b.OwnerId == effectiveOwnerId).ToList();
                if (!brands.Any())
                {
                    _logger.LogWarning("No brands found for user {UserId} after permission check", ownerId);
                    return NotFound(new { message = "Bạn không có quyền truy cập thương hiệu nào." });
                }
            }

            var userToReject = await _context.Users.FindAsync(userId);
            if (userToReject == null)
            {
                _logger.LogWarning("User not found: {UserId}", userId);
                return NotFound(new { message = "Người dùng không tồn tại." });
            }

            if (!brands.Any(b => b.BrandId == userToReject.BrandId) || userToReject.ApprovalStatus != "Pending")
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
        [Authorize(Roles = "Admin,Staff,BrandManager")]
        public async Task<IActionResult> UpdateStaff(int userId, [FromBody] UserDTO userDto)
        {
            var ownerId = GetUserId();
            if (!ownerId.HasValue) return Unauthorized(new { message = "Token không hợp lệ." });

            var owner = await _context.Users.FindAsync(ownerId.Value);
            if (owner == null) return NotFound(new { message = "Người dùng không tồn tại." });

            var brands = await _context.Brands.Where(b => b.OwnerId == ownerId.Value).ToListAsync();
            if (brands == null || !brands.Any()) return NotFound(new { message = "Bạn không phải là chủ thương hiệu." });

            if (User.IsInRole("Staff") || User.IsInRole("BrandManager"))
            {
                var user = await _context.Users.FindAsync(ownerId.Value);
                if (user == null)
                {
                    _logger.LogWarning("User not found: {UserId}", ownerId);
                    return NotFound(new { message = "Người dùng không tồn tại." });
                }

                int effectiveOwnerId;
                if (user.BrandId.HasValue)
                {
                    var userBrand = await _context.Brands.FindAsync(user.BrandId.Value);
                    if (userBrand == null)
                    {
                        _logger.LogWarning("Brand not found for BrandId: {BrandId}", user.BrandId);
                        return NotFound(new { message = "Thương hiệu không tồn tại." });
                    }
                    effectiveOwnerId = userBrand.OwnerId;
                }
                else
                {
                    effectiveOwnerId = ownerId.Value;
                }

                brands = brands.Where(b => b.OwnerId == effectiveOwnerId).ToList();
                if (!brands.Any())
                {
                    _logger.LogWarning("No brands found for user {UserId} after permission check", ownerId);
                    return NotFound(new { message = "Bạn không có quyền truy cập thương hiệu nào." });
                }
            }

            var userToUpdate = await _context.Users.FindAsync(userId);
            if (userToUpdate == null) return NotFound(new { message = "Nhân viên không tồn tại." });

            if (!brands.Any(b => b.BrandId == userToUpdate.BrandId) || userToUpdate.ApprovalStatus != "Approved")
                return BadRequest(new { message = "Nhân viên không thuộc thương hiệu hoặc chưa được duyệt." });

            userToUpdate.Name = userDto.Name;
            userToUpdate.Email = userDto.Email;
            userToUpdate.Role = userDto.Role;
            _context.Users.Update(userToUpdate);
            await _context.SaveChangesAsync();

            _logger.LogInformation("User {UserId} updated by {OwnerId}", userId, ownerId);
            return Ok(new { message = "Cập nhật nhân viên thành công." });
        }

        [HttpDelete("delete/{userId}")]
        [Authorize(Roles = "Admin,Staff,BrandManager")]
        public async Task<IActionResult> DeleteStaff(int userId)
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

            var brands = await _context.Brands.Where(b => b.OwnerId == ownerId.Value).ToListAsync();
            if (brands == null || !brands.Any())
            {
                _logger.LogWarning("No brands found for owner: {OwnerId}", ownerId);
                return NotFound(new { message = "Bạn không phải là chủ thương hiệu." });
            }

            if (User.IsInRole("Staff") || User.IsInRole("BrandManager"))
            {
                var user = await _context.Users.FindAsync(ownerId.Value);
                if (user == null)
                {
                    _logger.LogWarning("User not found: {UserId}", ownerId);
                    return NotFound(new { message = "Người dùng không tồn tại." });
                }

                int effectiveOwnerId;
                if (user.BrandId.HasValue)
                {
                    var userBrand = await _context.Brands.FindAsync(user.BrandId.Value);
                    if (userBrand == null)
                    {
                        _logger.LogWarning("Brand not found for BrandId: {BrandId}", user.BrandId);
                        return NotFound(new { message = "Thương hiệu không tồn tại." });
                    }
                    effectiveOwnerId = userBrand.OwnerId;
                }
                else
                {
                    effectiveOwnerId = ownerId.Value;
                }

                brands = brands.Where(b => b.OwnerId == effectiveOwnerId).ToList();
                if (!brands.Any())
                {
                    _logger.LogWarning("No brands found for user {UserId} after permission check", ownerId);
                    return NotFound(new { message = "Bạn không có quyền truy cập thương hiệu nào." });
                }
            }

            var userToDelete = await _context.Users.FindAsync(userId);
            if (userToDelete == null)
            {
                _logger.LogWarning("User not found: {UserId}", userId);
                return NotFound(new { message = "Nhân viên không tồn tại." });
            }

            if (!brands.Any(b => b.BrandId == userToDelete.BrandId) || userToDelete.ApprovalStatus != "Approved")
            {
                _logger.LogWarning("User {UserId} is not an approved staff for any brand owned by {OwnerId}", userId, ownerId);
                return BadRequest(new { message = "Nhân viên không thuộc thương hiệu hoặc chưa được duyệt." });
            }

            // Remove permissions from StaffPermission table
            var staffPermissions = await _context.StaffPermissions
                .Where(sp => sp.StaffId == userId)
                .ToListAsync();
            if (staffPermissions.Any())
            {
                _context.StaffPermissions.RemoveRange(staffPermissions);
            }

            // Update user's BrandId and ApprovalStatus to null
            userToDelete.BrandId = null;
            userToDelete.ApprovalStatus = null;
            _context.Users.Update(userToDelete);
            await _context.SaveChangesAsync();

            _logger.LogInformation("User {UserId} removed from brand by {OwnerId}", userId, ownerId);
            return Ok(new { message = "Đã xóa nhân viên khỏi thương hiệu thành công." });
        }

        private int? GetUserId()
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            return int.TryParse(userIdClaim, out int userId) ? userId : null;
        }
    }
}