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
        public async Task<ActionResult<RevenueResponseDTO>> GetRevenueByBrand(int brandId, [FromQuery] DateTime? startDate, [FromQuery] DateTime? endDate)
        {
            _logger.LogInformation("GetRevenueByBrand called for brand {BrandId}, startDate: {StartDate}, endDate: {EndDate}",
                brandId, startDate?.ToString("yyyy-MM-dd"), endDate?.ToString("yyyy-MM-dd"));

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

                var totalRevenue = orders.Sum(o => o.TotalAmount);
                var totalProfit = orders.Sum(o =>
                    o.TotalAmount - o.OrderItems.Sum(i => (i.Product.PriceCost ?? 0) * i.Quantity));

                var orderDtos = orders.Select(o => new OrderDTO
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

                var response = new RevenueResponseDTO
                {
                    BrandId = brandId,
                    TotalRevenue = totalRevenue,
                    TotalProfit = totalProfit,
                    Orders = orderDtos,
                    StartDate = startDate,
                    EndDate = endDate
                };

                _logger.LogInformation("Retrieved revenue {TotalRevenue}, profit {TotalProfit} for brand {BrandId} with {OrderCount} orders",
                    totalRevenue, totalProfit, brandId, orders.Count);
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