using HakiBaVuong.Data;
using HakiBaVuong.DTOs;
using HakiBaVuong.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;

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
        public async Task<ActionResult<RevenueResponseDTO>> GetRevenueByBrand(int brandId, [FromQuery] DateTime? startDate, [FromQuery] DateTime? endDate, [FromQuery] string? groupBy, [FromQuery] decimal? minProfit, [FromQuery] decimal? maxProfit, [FromQuery] decimal? minRevenue, [FromQuery] decimal? maxRevenue, [FromQuery] int? productId)
        {
            _logger.LogInformation("GetRevenueByBrand called for brand {BrandId}, startDate: {StartDate}, endDate: {EndDate}, groupBy: {GroupBy}, minProfit: {MinProfit}, maxProfit: {MaxProfit}, minRevenue: {MinRevenue}, maxRevenue: {MaxRevenue}, productId: {ProductId}",
                brandId, startDate?.ToString("yyyy-MM-dd"), endDate?.ToString("yyyy-MM-dd"), groupBy, minProfit, maxProfit, minRevenue, maxRevenue, productId);

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
                    .AnyAsync(sp => sp.StaffId == userId.Value);
                if (!hasPermission)
                {
                    _logger.LogWarning("Staff user {UserId} does not have ManageOrders permission for brand {BrandId}", userId.Value, brandId);
                    return Forbid();
                }
            }

            try
            {
                var query = _context.Orders
                    .Where(o => o.BrandId == brandId && o.Status == "Đã thanh toán")
                    .Include(o => o.OrderItems)
                    .ThenInclude(i => i.Product)
                    .Include(o => o.Payment)
                    .AsQueryable();

                if (startDate.HasValue)
                {
                    query = query.Where(o => o.CreatedAt >= startDate.Value);
                }

                if (endDate.HasValue)
                {
                    query = query.Where(o => o.CreatedAt <= endDate.Value.AddDays(1).AddTicks(-1));
                }

                var orders = await query.ToListAsync();
                var response = new RevenueResponseDTO { BrandId = brandId, StartDate = startDate, EndDate = endDate };


                switch (groupBy?.ToLower())
                {
                    case "day":
                        var dailyRevenue = orders
                            .Where(o => !productId.HasValue || o.OrderItems.Any(i => i.ProductId == productId.Value))
                            .GroupBy(o => o.CreatedAt.Date)
                            .Select(g => new RevenueGroupDTO
                            {
                                GroupKey = g.Key.ToString("yyyy-MM-dd"),
                                TotalRevenue = g.Sum(o => o.OrderItems.Where(i => !productId.HasValue || i.ProductId == productId.Value).Sum(i => i.Price * i.Quantity)),
                                TotalCost = g.Sum(o => o.OrderItems.Where(i => !productId.HasValue || i.ProductId == productId.Value).Sum(i => (i.Product.PriceCost ?? 0) * i.Quantity)),
                                TotalProfit = g.Sum(o => o.OrderItems.Where(i => !productId.HasValue || i.ProductId == productId.Value).Sum(i => (i.Price - (i.Product.PriceCost ?? 0)) * i.Quantity)),
                                OrderCount = g.Count(o => o.OrderItems.Any(i => !productId.HasValue || i.ProductId == productId.Value))
                            })
                            .ToList();

                        if (minProfit.HasValue || maxProfit.HasValue || minRevenue.HasValue || maxRevenue.HasValue)
                        {
                            dailyRevenue = dailyRevenue
                                .Where(r => (!minProfit.HasValue || r.TotalProfit >= minProfit.Value) &&
                                            (!maxProfit.HasValue || r.TotalProfit <= maxProfit.Value) &&
                                            (!minRevenue.HasValue || r.TotalRevenue >= minRevenue.Value) &&
                                            (!maxRevenue.HasValue || r.TotalRevenue <= maxRevenue.Value))
                                .ToList();
                        }
                        response.RevenueGroups = dailyRevenue;
                        break;

                    case "month":
                        var monthlyRevenue = orders
                            .Where(o => !productId.HasValue || o.OrderItems.Any(i => i.ProductId == productId.Value))
                            .GroupBy(o => new { o.CreatedAt.Year, o.CreatedAt.Month })
                            .Select(g => new RevenueGroupDTO
                            {
                                GroupKey = new DateTime(g.Key.Year, g.Key.Month, 1).ToString("yyyy-MM"),
                                TotalRevenue = g.Sum(o => o.OrderItems.Where(i => !productId.HasValue || i.ProductId == productId.Value).Sum(i => i.Price * i.Quantity)),
                                TotalCost = g.Sum(o => o.OrderItems.Where(i => !productId.HasValue || i.ProductId == productId.Value).Sum(i => (i.Product.PriceCost ?? 0) * i.Quantity)),
                                TotalProfit = g.Sum(o => o.OrderItems.Where(i => !productId.HasValue || i.ProductId == productId.Value).Sum(i => (i.Price - (i.Product.PriceCost ?? 0)) * i.Quantity)),
                                OrderCount = g.Count(o => o.OrderItems.Any(i => !productId.HasValue || i.ProductId == productId.Value))
                            })
                            .ToList();

                        if (minProfit.HasValue || maxProfit.HasValue || minRevenue.HasValue || maxRevenue.HasValue)
                        {
                            monthlyRevenue = monthlyRevenue
                                .Where(r => (!minProfit.HasValue || r.TotalProfit >= minProfit.Value) &&
                                            (!maxProfit.HasValue || r.TotalProfit <= maxProfit.Value) &&
                                            (!minRevenue.HasValue || r.TotalRevenue >= minRevenue.Value) &&
                                            (!maxRevenue.HasValue || r.TotalRevenue <= maxRevenue.Value))
                                .ToList();
                        }
                        response.RevenueGroups = monthlyRevenue;
                        break;

                    case "year":
                        var yearlyRevenue = orders
                            .Where(o => !productId.HasValue || o.OrderItems.Any(i => i.ProductId == productId.Value))
                            .GroupBy(o => o.CreatedAt.Year)
                            .Select(g => new RevenueGroupDTO
                            {
                                GroupKey = g.Key.ToString(),
                                TotalRevenue = g.Sum(o => o.OrderItems.Where(i => !productId.HasValue || i.ProductId == productId.Value).Sum(i => i.Price * i.Quantity)),
                                TotalCost = g.Sum(o => o.OrderItems.Where(i => !productId.HasValue || i.ProductId == productId.Value).Sum(i => (i.Product.PriceCost ?? 0) * i.Quantity)),
                                TotalProfit = g.Sum(o => o.OrderItems.Where(i => !productId.HasValue || i.ProductId == productId.Value).Sum(i => (i.Price - (i.Product.PriceCost ?? 0)) * i.Quantity)),
                                OrderCount = g.Count(o => o.OrderItems.Any(i => !productId.HasValue || i.ProductId == productId.Value))
                            })
                            .ToList();

                        if (minProfit.HasValue || maxProfit.HasValue || minRevenue.HasValue || maxRevenue.HasValue)
                        {
                            yearlyRevenue = yearlyRevenue
                                .Where(r => (!minProfit.HasValue || r.TotalProfit >= minProfit.Value) &&
                                            (!maxProfit.HasValue || r.TotalProfit <= maxProfit.Value) &&
                                            (!minRevenue.HasValue || r.TotalRevenue >= minRevenue.Value) &&
                                            (!maxRevenue.HasValue || r.TotalRevenue <= maxRevenue.Value))
                                .ToList();
                        }
                        response.RevenueGroups = yearlyRevenue;
                        break;

                    case "product":
                        var productRevenue = orders
                            .SelectMany(o => o.OrderItems)
                            .Where(i => !productId.HasValue || i.ProductId == productId.Value)
                            .GroupBy(i => new { i.ProductId, i.ProductName })
                            .Select(g => new RevenueGroupDTO
                            {
                                GroupKey = $"{g.Key.ProductId} - {g.Key.ProductName}",
                                TotalRevenue = g.Sum(i => i.Price * i.Quantity),
                                TotalCost = g.Sum(i => (i.Product.PriceCost ?? 0) * i.Quantity),
                                TotalProfit = g.Sum(i => (i.Price - (i.Product.PriceCost ?? 0)) * i.Quantity),
                                OrderCount = g.Count()
                            })
                            .ToList();
                        // Lọc theo minProfit, maxProfit, minRevenue, maxRevenue
                        if (minProfit.HasValue || maxProfit.HasValue || minRevenue.HasValue || maxRevenue.HasValue)
                        {
                            productRevenue = productRevenue
                                .Where(r => (!minProfit.HasValue || r.TotalProfit >= minProfit.Value) &&
                                            (!maxProfit.HasValue || r.TotalProfit <= maxProfit.Value) &&
                                            (!minRevenue.HasValue || r.TotalRevenue >= minRevenue.Value) &&
                                            (!maxRevenue.HasValue || r.TotalRevenue <= maxRevenue.Value))
                                .ToList();
                        }
                        response.RevenueGroups = productRevenue;
                        break;

                    case "brand":
                    default:
                        var totalRevenue = orders.Sum(o => o.TotalAmount);
                        var totalCost = orders.Sum(o => o.OrderItems.Sum(i => (i.Product.PriceCost ?? 0) * i.Quantity));
                        var totalProfit = totalRevenue - totalCost;

                        var hasMissingPriceCost = orders.Any(o => o.OrderItems.Any(i => i.Product.PriceCost == null || i.Product.PriceCost == 0));
                        if (hasMissingPriceCost)
                        {
                            _logger.LogWarning("Missing or zero PriceCost values detected for some products in brand {BrandId}. TotalCost may be inaccurate.", brandId);
                        }

                        if (totalRevenue == totalProfit && totalRevenue > 0)
                        {
                            _logger.LogWarning("TotalRevenue equals TotalProfit for brand {BrandId}. TotalCost is {TotalCost}. Verify PriceCost data.", brandId, totalCost);
                        }

                        // Lọc tổng doanh thu và lợi nhuận
                        if (minProfit.HasValue || maxProfit.HasValue || minRevenue.HasValue || maxRevenue.HasValue)
                        {
                            if ((minProfit.HasValue && totalProfit < minProfit.Value) ||
                                (maxProfit.HasValue && totalProfit > maxProfit.Value) ||
                                (minRevenue.HasValue && totalRevenue < minRevenue.Value) ||
                                (maxRevenue.HasValue && totalRevenue > maxRevenue.Value))
                            {
                                return Ok(response); // Trả về response rỗng nếu không khớp điều kiện
                            }
                        }

                        response.TotalRevenue = totalRevenue;
                        response.TotalProfit = totalProfit;
                        response.OrderCount = orders.Count;
                        response.Orders = orders.Select(o => new OrderDTO
                        {
                            OrderId = o.OrderId,
                            BrandId = o.BrandId,
                            CustomerId = o.CustomerId,
                            FullName = o.FullName,
                            Phone = o.Phone,
                            Address = o.Address,
                            Status = o.Status,
                            DeliveryStatus = o.DeliveryStatus,
                            EstimatedDeliveryDate = o.EstimatedDeliveryDate,
                            TotalAmount = o.TotalAmount,
                            CreatedAt = o.CreatedAt,
                            OrderItems = o.OrderItems.Select(i => new OrderItemDTO
                            {
                                ItemId = i.ItemId,
                                OrderId = i.OrderId,
                                ProductId = i.ProductId,
                                ProductName = i.ProductName,
                                Quantity = i.Quantity,
                                Price = i.Price
                            }).ToList(),
                            Payment = o.Payment != null ? new PaymentDTO
                            {
                                PaymentId = o.Payment.PaymentId,
                                Amount = o.Payment.Amount,
                                Method = o.Payment.Method,
                                Status = o.Payment.Status
                            } : null
                        }).ToList();
                        break;
                }

                _logger.LogInformation("Retrieved revenue data for brand {BrandId} with {OrderCount} orders", brandId, orders.Count);
                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred while fetching revenue and profit for brand {BrandId}", brandId);
                return StatusCode(500, new { message = "Lỗi khi tính doanh thu và lợi nhuận. Vui lòng thử lại sau." });
            }
        }

        private int? GetUserId()
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            return int.TryParse(userIdClaim, out int userId) ? userId : null;
        }
    }
}