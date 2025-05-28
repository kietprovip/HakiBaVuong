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
        public async Task<ActionResult<RevenueResponseDTO>> GetRevenueByBrand(int brandId, [FromQuery] string periodType = "Day")
        {
            _logger.LogInformation("GetRevenueByBrand called for brand {BrandId}, periodType: {PeriodType}", brandId, periodType);

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
                var response = new RevenueResponseDTO { BrandId = brandId };

                var today = DateTime.UtcNow.Date;
                var currentMonth = new DateTime(today.Year, today.Month, 1);
                var currentYear = new DateTime(today.Year, 1, 1);

                // Reset Revenue if new period
                var revenues = await _context.Revenues
                    .Include(r => r.Order)
                    .Where(r => r.Order.BrandId == brandId && r.Order.Status == "Đã thanh toán")
                    .ToListAsync();

                foreach (var revenue in revenues)
                {
                    if (revenue.Date.Date < today)
                    {
                        revenue.DailyRevenue = 0;
                    }
                    if (revenue.Date < currentMonth)
                    {
                        revenue.MonthlyRevenue = 0;
                    }
                    if (revenue.Date < currentYear)
                    {
                        revenue.YearlyRevenue = 0;
                    }
                    _context.Revenues.Update(revenue);
                }
                await _context.SaveChangesAsync();

                // Get Revenue for current period
                var currentRevenue = await _context.Revenues
                    .Include(r => r.Order)
                    .Where(r => r.Order.BrandId == brandId && r.Order.Status == "Đã thanh toán")
                    .GroupBy(r => r.Order.BrandId)
                    .Select(g => new
                    {
                        TotalDailyRevenue = g.Sum(r => r.DailyRevenue),
                        TotalMonthlyRevenue = g.Sum(r => r.MonthlyRevenue),
                        TotalYearlyRevenue = g.Sum(r => r.YearlyRevenue),
                        OrderCount = g.Count()
                    })
                    .FirstOrDefaultAsync();

                response.TotalRevenue = periodType switch
                {
                    "Month" => currentRevenue?.TotalMonthlyRevenue ?? 0,
                    "Year" => currentRevenue?.TotalYearlyRevenue ?? 0,
                    _ => currentRevenue?.TotalDailyRevenue ?? 0
                };
                response.OrderCount = currentRevenue?.OrderCount ?? 0;

                // Get RevenueHistory for chart
                var revenueHistory = await _context.RevenueHistories
                    .Where(rh => rh.BrandId == brandId && rh.PeriodType == periodType)
                    .OrderBy(rh => rh.Date)
                    .ToListAsync();

                response.RevenueGroups = revenueHistory.Select(rh => new RevenueGroupDTO
                {
                    GroupKey = periodType switch
                    {
                        "Month" => rh.Date.ToString("yyyy-MM"),
                        "Year" => rh.Date.ToString("yyyy"),
                        _ => rh.Date.ToString("yyyy-MM-dd")
                    },
                    TotalRevenue = periodType switch
                    {
                        "Month" => rh.MonthlyRevenue,
                        "Year" => rh.YearlyRevenue,
                        _ => rh.DailyRevenue
                    },
                    TotalCost = 0, // Tạm thời không tính TotalCost
                    TotalProfit = 0, // Tạm thời không tính TotalProfit
                    OrderCount = 0 // Có thể cập nhật nếu cần
                }).ToList();

                _logger.LogInformation("Retrieved revenue data for brand {BrandId} with {OrderCount} orders", brandId, response.OrderCount);
                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred while fetching revenue for brand {BrandId}", brandId);
                return StatusCode(500, new { message = "Lỗi khi lấy dữ liệu doanh thu. Vui lòng thử lại sau." });
            }
        }

        [HttpGet("brand/{brandId}/all")]
        public async Task<ActionResult<RevenueAllPeriodsDTO>> GetAllRevenueByBrand(int brandId)
        {
            _logger.LogInformation("GetAllRevenueByBrand called for brand {BrandId}", brandId);

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
                var today = DateTime.UtcNow.Date;
                var currentMonth = new DateTime(today.Year, today.Month, 1);
                var currentYear = new DateTime(today.Year, 1, 1);

                // Reset Revenue if new period
                var revenues = await _context.Revenues
                    .Include(r => r.Order)
                    .Where(r => r.Order.BrandId == brandId && r.Order.Status == "Đã thanh toán")
                    .ToListAsync();

                foreach (var revenue in revenues)
                {
                    if (revenue.Date.Date < today)
                    {
                        revenue.DailyRevenue = 0;
                    }
                    if (revenue.Date < currentMonth)
                    {
                        revenue.MonthlyRevenue = 0;
                    }
                    if (revenue.Date < currentYear)
                    {
                        revenue.YearlyRevenue = 0;
                    }
                    _context.Revenues.Update(revenue);
                }
                await _context.SaveChangesAsync();

                // Get aggregated revenue
                var currentRevenue = await _context.Revenues
                    .Include(r => r.Order)
                    .Where(r => r.Order.BrandId == brandId && r.Order.Status == "Đã thanh toán")
                    .GroupBy(r => r.Order.BrandId)
                    .Select(g => new
                    {
                        TotalDailyRevenue = g.Sum(r => r.DailyRevenue),
                        TotalMonthlyRevenue = g.Sum(r => r.MonthlyRevenue),
                        TotalYearlyRevenue = g.Sum(r => r.YearlyRevenue),
                        OrderCount = g.Count()
                    })
                    .FirstOrDefaultAsync();

                // Get RevenueHistory for all periods
                var revenueHistories = await _context.RevenueHistories
                    .Where(rh => rh.BrandId == brandId)
                    .OrderBy(rh => rh.Date)
                    .ToListAsync();

                var response = new RevenueAllPeriodsDTO
                {
                    BrandId = brandId,
                    TotalDailyRevenue = currentRevenue?.TotalDailyRevenue ?? 0,
                    TotalMonthlyRevenue = currentRevenue?.TotalMonthlyRevenue ?? 0,
                    TotalYearlyRevenue = currentRevenue?.TotalYearlyRevenue ?? 0,
                    OrderCount = currentRevenue?.OrderCount ?? 0,
                    RevenueGroups = new Dictionary<string, List<RevenueGroupDTO>>
                    {
                        ["Day"] = revenueHistories
                            .Where(rh => rh.PeriodType == "Day")
                            .Select(rh => new RevenueGroupDTO
                            {
                                GroupKey = rh.Date.ToString("yyyy-MM-dd"),
                                TotalRevenue = rh.DailyRevenue,
                                TotalCost = 0,
                                TotalProfit = 0,
                                OrderCount = 0
                            }).ToList(),
                        ["Month"] = revenueHistories
                            .Where(rh => rh.PeriodType == "Month")
                            .Select(rh => new RevenueGroupDTO
                            {
                                GroupKey = rh.Date.ToString("yyyy-MM"),
                                TotalRevenue = rh.MonthlyRevenue,
                                TotalCost = 0,
                                TotalProfit = 0,
                                OrderCount = 0
                            }).ToList(),
                        ["Year"] = revenueHistories
                            .Where(rh => rh.PeriodType == "Year")
                            .Select(rh => new RevenueGroupDTO
                            {
                                GroupKey = rh.Date.ToString("yyyy"),
                                TotalRevenue = rh.YearlyRevenue,
                                TotalCost = 0,
                                TotalProfit = 0,
                                OrderCount = 0
                            }).ToList()
                    }
                };

                _logger.LogInformation("Retrieved all revenue data for brand {BrandId} with {OrderCount} orders", brandId, response.OrderCount);
                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred while fetching all revenue for brand {BrandId}", brandId);
                return StatusCode(500, new { message = "Lỗi khi lấy dữ liệu doanh thu. Vui lòng thử lại sau." });
            }
        }

        private int? GetUserId()
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            return int.TryParse(userIdClaim, out int userId) ? userId : null;
        }
    }

    // DTO mới để hỗ trợ cả ba kỳ
    public class RevenueAllPeriodsDTO
    {
        public int BrandId { get; set; }
        public decimal TotalDailyRevenue { get; set; }
        public decimal TotalMonthlyRevenue { get; set; }
        public decimal TotalYearlyRevenue { get; set; }
        public int OrderCount { get; set; }
        public Dictionary<string, List<RevenueGroupDTO>> RevenueGroups { get; set; }
    }
}