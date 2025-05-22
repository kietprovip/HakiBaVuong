using HakiBaVuong.Data;
using HakiBaVuong.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace HakiBaVuong.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize(Roles = "Admin,Staff")]
    public class RevenueController : ControllerBase
    {
        private readonly DataContext _context;
        private readonly ILogger<RevenueController> _logger;

        public RevenueController(DataContext context, ILogger<RevenueController> logger)
        {
            _context = context;
            _logger = logger;
        }

        [HttpGet("brand/{brandId}")]
        public async Task<ActionResult<RevenueDTO>> GetRevenueByBrand(int brandId)
        {
            _logger.LogInformation("GetRevenueByBrand called for brand {BrandId}", brandId);

            var userId = GetUserId();
            if (userId == null)
            {
                _logger.LogWarning("Invalid userId from token");
                return Unauthorized(new { message = "Token không hợp lệ." });
            }

            var brand = await _context.Brands.FindAsync(brandId);
            if (brand == null)
            {
                _logger.LogWarning("Brand not found: {BrandId}", brandId);
                return BadRequest(new { message = "Thương hiệu không tồn tại." });
            }

            if (User.IsInRole("Staff"))
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

                if (brand.OwnerId != effectiveOwnerId)
                {
                    _logger.LogWarning("User {UserId} does not have access to brand {BrandId}", userId, brandId);
                    return Forbid();
                }
            }

            var orders = await _context.Orders
                .Where(o => o.BrandId == brandId && o.Status == "Đã thanh toán")
                .Include(o => o.OrderItems)
                .ThenInclude(i => i.Product)
                .ToListAsync();

            decimal totalRevenue = 0;
            decimal totalProfit = 0;
            int totalItemsSold = 0;

            foreach (var order in orders)
            {
                foreach (var item in order.OrderItems)
                {
                    if (item.ProductId.HasValue)
                    {
                        totalRevenue += item.Price * item.Quantity;
                        totalItemsSold += item.Quantity;
                        if (item.Product?.PriceCost.HasValue == true)
                        {
                            totalProfit += (item.Price - item.Product.PriceCost.Value) * item.Quantity;
                        }
                    }
                }
            }

            var revenueDto = new RevenueDTO
            {
                BrandId = brandId,
                BrandName = brand.Name,
                TotalRevenue = totalRevenue,
                TotalProfit = totalProfit,
                TotalItemsSold = totalItemsSold
            };

            _logger.LogInformation("Retrieved revenue stats for brand {BrandId}: Revenue={Revenue}, Profit={Profit}, ItemsSold={ItemsSold}",
                brandId, totalRevenue, totalProfit, totalItemsSold);
            return Ok(revenueDto);
        }

        private int? GetUserId()
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            return int.TryParse(userIdClaim, out int userId) ? userId : null;
        }
    }
}