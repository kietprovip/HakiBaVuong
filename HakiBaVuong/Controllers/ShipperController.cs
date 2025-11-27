using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using HakiBaVuong.Data;
using HakiBaVuong.Models;
using HakiBaVuong.DTOs;

namespace HakiBaVuong.Controllers
{
    [Route("api/shipper")]
    [ApiController]
    [Authorize(Roles = "Shipper")]  // Tất cả endpoint đều cần token Shipper
    public class ShipperController : ControllerBase
    {
        private readonly DataContext _context;
        private readonly ILogger<ShipperController> _logger;

        public ShipperController(DataContext context, ILogger<ShipperController> logger)
        {
            _context = context;
            _logger = logger;
        }

        private int GetCurrentShipperId()
        {
            var claim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            return int.TryParse(claim, out var id) ? id : throw new UnauthorizedAccessException("Không xác định được shipper.");
        }

        // ==================== 1. LẤY THÔNG TIN CÁ NHÂN ====================
        [HttpGet("profile")]
        public async Task<ActionResult<ShipperDTO>> GetProfile()
        {
            var shipperId = GetCurrentShipperId();

            var shipper = await _context.Shippers
                .Where(s => s.ShipperId == shipperId)
                .Select(s => new ShipperDTO
                {
                    ShipperId = s.ShipperId,
                    Name = s.Name,
                    Email = s.Email,
                    Phone = s.Phone,
                    VehicleType = s.VehicleType,
                    LicensePlate = s.LicensePlate,
                    Rating = s.Rating,
                    CreatedAt = s.CreatedAt
                })
                .FirstOrDefaultAsync();

            if (shipper == null) return NotFound(new { message = "Không tìm thấy shipper." });

            return Ok(shipper);
        }

        // ==================== 2. CẬP NHẬT HỒ SƠ ====================
        [HttpPut("profile")]
        public async Task<IActionResult> UpdateProfile([FromBody] ShipperUpdateDTO model)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);

            var shipperId = GetCurrentShipperId();
            var shipper = await _context.Shippers.FindAsync(shipperId);
            if (shipper == null) return NotFound();

            shipper.Name = model.Name ?? shipper.Name;
            shipper.Phone = model.Phone ?? shipper.Phone;
            shipper.VehicleType = model.VehicleType ?? shipper.VehicleType;
            shipper.LicensePlate = model.LicensePlate ?? shipper.LicensePlate;
            shipper.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            _logger.LogInformation("Shipper {ShipperId} đã cập nhật hồ sơ", shipperId);
            return Ok(new { message = "Cập nhật hồ sơ thành công!" });
        }

        // ==================== 3. DANH SÁCH ĐƠN HÀNG CẦN GIAO (chờ shipper nhận) ====================
        [HttpGet("pending-orders")]
        public async Task<ActionResult<IEnumerable<OrderDTO>>> GetPendingOrders()
        {
            var orders = await _context.Orders
                .Where(o => o.DeliveryStatus == "Đợi giao hàng" && o.ShipperId == null)
                .Include(o => o.OrderItems)
                .ThenInclude(i => i.Product)
                .OrderBy(o => o.CreatedAt)
                .Take(20)
                .Select(o => new OrderDTO
                {
                    OrderId = o.OrderId,
                    BrandId = o.BrandId,
                    CustomerId = o.CustomerId,
                    FullName = o.FullName,
                    Phone = o.Phone,
                    Address = o.Address,
                    Status = o.Status,
                    DeliveryStatus = o.DeliveryStatus,
                    TotalAmount = o.TotalAmount,
                    CreatedAt = o.CreatedAt,
                    EstimatedDeliveryDate = o.EstimatedDeliveryDate,
                    OrderItems = o.OrderItems.Select(oi => new OrderItemDTO
                    {
                        ItemId = oi.ItemId,
                        OrderId = oi.OrderId,
                        ProductId = oi.ProductId,
                        ProductName = oi.ProductName,
                        Quantity = oi.Quantity,
                        Price = oi.Price
                    }).ToList()
                })
                .ToListAsync();

            return Ok(orders);
        }

        // ==================== 4. NHẬN GIAO ĐƠN HÀNG ====================
        [HttpPost("accept-order/{orderId}")]
        public async Task<IActionResult> AcceptOrder(int orderId)
        {
            var shipperId = GetCurrentShipperId();

            var order = await _context.Orders.FindAsync(orderId);
            if (order == null) return NotFound(new { message = "Đơn hàng không tồn tại." });

            if (order.ShipperId.HasValue)
                return BadRequest(new { message = "Đơn hàng đã được shipper khác nhận." });

            if (order.DeliveryStatus != "Đợi giao hàng")
                return BadRequest(new { message = "Đơn hàng chưa sẵn sàng để giao." });

            order.ShipperId = shipperId;
            order.DeliveryStatus = "Đang giao hàng";
            order.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            _logger.LogInformation("Shipper {ShipperId} nhận giao đơn {OrderId}", shipperId, orderId);
            return Ok(new { message = "Nhận giao đơn thành công!", orderId });
        }

        // ==================== 5. DANH SÁCH ĐƠN HÀNG ĐANG GIAO CỦA TÔI ====================
        [HttpGet("deliveries-in-progress")]
        public async Task<ActionResult<IEnumerable<OrderDTO>>> GetDeliveriesInProgress()
        {
            var shipperId = GetCurrentShipperId();

            var orders = await _context.Orders
                .Where(o => o.ShipperId == shipperId && o.DeliveryStatus == "Đang giao hàng")
                .Include(o => o.OrderItems)
                .ThenInclude(i => i.Product)
                .Include(o => o.Payment)
                .Select(o => new OrderDTO
                {
                    OrderId = o.OrderId,
                    BrandId = o.BrandId,
                    CustomerId = o.CustomerId,
                    FullName = o.FullName,
                    Phone = o.Phone,
                    Address = o.Address,
                    Status = o.Status,
                    DeliveryStatus = o.DeliveryStatus,
                    TotalAmount = o.TotalAmount,
                    EstimatedDeliveryDate = o.EstimatedDeliveryDate,
                    CreatedAt = o.CreatedAt,
                    OrderItems = o.OrderItems.Select(oi => new OrderItemDTO
                    {
                        ItemId = oi.ItemId,
                        OrderId = oi.OrderId,
                        ProductId = oi.ProductId,
                        ProductName = oi.ProductName,
                        Quantity = oi.Quantity,
                        Price = oi.Price
                    }).ToList(),
                    Payment = o.Payment != null ? new PaymentDTO
                    {
                        PaymentId = o.Payment.PaymentId,
                        Amount = o.Payment.Amount,
                        Method = o.Payment.Method,
                        Status = o.Payment.Status
                    } : null
                })
                .ToListAsync();

            return Ok(orders);
        }

        // ==================== 6. HOÀN THÀNH GIAO HÀNG ====================
        [HttpPost("complete-delivery/{orderId}")]
        public async Task<IActionResult> CompleteDelivery(int orderId)
        {
            var shipperId = GetCurrentShipperId();

            var order = await _context.Orders
                .Include(o => o.OrderItems)
                .ThenInclude(i => i.Product)
                .Include(o => o.Payment)
                .FirstOrDefaultAsync(o => o.OrderId == orderId && o.ShipperId == shipperId);
            
            if (order == null) return NotFound(new { message = "Không tìm thấy đơn hàng." });

            if (order.DeliveryStatus != "Đang giao hàng")
                return BadRequest(new { message = "Đơn hàng chưa ở trạng thái đang giao." });

            order.DeliveryStatus = "Đã giao hàng";
            order.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            _logger.LogInformation("Shipper {ShipperId} hoàn thành giao đơn {OrderId}", shipperId, orderId);
            
            var completedOrder = new OrderDTO
            {
                OrderId = order.OrderId,
                BrandId = order.BrandId,
                CustomerId = order.CustomerId,
                FullName = order.FullName,
                Phone = order.Phone,
                Address = order.Address,
                Status = order.Status,
                DeliveryStatus = order.DeliveryStatus,
                TotalAmount = order.TotalAmount,
                EstimatedDeliveryDate = order.EstimatedDeliveryDate,
                CreatedAt = order.CreatedAt,
                OrderItems = order.OrderItems.Select(oi => new OrderItemDTO
                {
                    ItemId = oi.ItemId,
                    OrderId = oi.OrderId,
                    ProductId = oi.ProductId,
                    ProductName = oi.ProductName,
                    Quantity = oi.Quantity,
                    Price = oi.Price
                }).ToList(),
                Payment = order.Payment != null ? new PaymentDTO
                {
                    PaymentId = order.Payment.PaymentId,
                    Amount = order.Payment.Amount,
                    Method = order.Payment.Method,
                    Status = order.Payment.Status
                } : null
            };

            return Ok(new { message = "Giao hàng thành công!", order = completedOrder });
        }

        // ==================== 7. DANH SÁCH ĐƠN HÀNG ĐÃ GIAO ====================
        [HttpGet("completed-deliveries")]
        public async Task<ActionResult<IEnumerable<OrderDTO>>> GetCompletedDeliveries([FromQuery] int page = 1, [FromQuery] int pageSize = 20)
        {
            var shipperId = GetCurrentShipperId();

            var orders = await _context.Orders
                .Where(o => o.ShipperId == shipperId && o.DeliveryStatus == "Đã giao hàng")
                .Include(o => o.OrderItems)
                .ThenInclude(i => i.Product)
                .Include(o => o.Payment)
                .OrderByDescending(o => o.UpdatedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(o => new OrderDTO
                {
                    OrderId = o.OrderId,
                    BrandId = o.BrandId,
                    CustomerId = o.CustomerId,
                    FullName = o.FullName,
                    Phone = o.Phone,
                    Address = o.Address,
                    Status = o.Status,
                    DeliveryStatus = o.DeliveryStatus,
                    TotalAmount = o.TotalAmount,
                    EstimatedDeliveryDate = o.EstimatedDeliveryDate,
                    CreatedAt = o.CreatedAt,
                    OrderItems = o.OrderItems.Select(oi => new OrderItemDTO
                    {
                        ItemId = oi.ItemId,
                        OrderId = oi.OrderId,
                        ProductId = oi.ProductId,
                        ProductName = oi.ProductName,
                        Quantity = oi.Quantity,
                        Price = oi.Price
                    }).ToList(),
                    Payment = o.Payment != null ? new PaymentDTO
                    {
                        PaymentId = o.Payment.PaymentId,
                        Amount = o.Payment.Amount,
                        Method = o.Payment.Method,
                        Status = o.Payment.Status
                    } : null
                })
                .ToListAsync();

            return Ok(new { data = orders, page, pageSize, total = await _context.Orders.CountAsync(o => o.ShipperId == shipperId && o.DeliveryStatus == "Đã giao hàng") });
        }

        // ==================== 8. CẬP NHẬT VỊ TRÍ GPS (dùng cho map realtime) ====================
        [HttpPost("update-location")]
        [Authorize(Roles = "Shipper")]
        public async Task<IActionResult> UpdateLocation([FromBody] LocationDTO model)
        {
            var shipperId = GetCurrentShipperId();

            var loc = new ShipperLocation
            {
                ShipperId = shipperId,
                Latitude = (decimal)model.Lat,
                Longitude = (decimal)model.Long,
                UpdatedAt = DateTime.UtcNow
            };

            var existing = await _context.ShipperLocations.FindAsync(shipperId);
            if (existing != null)
            {
                existing.Latitude = loc.Latitude;
                existing.Longitude = loc.Longitude;
                existing.UpdatedAt = loc.UpdatedAt;
                _context.ShipperLocations.Update(existing);
            }
            else
            {
                _context.ShipperLocations.Add(loc);
            }

            await _context.SaveChangesAsync();
            return Ok(new { message = "Cập nhật vị trí thành công!" });
        }

        // ==================== 9. THỐNG KÊ DOANH THU (tùy chọn) ====================
        [HttpGet("stats")]
        public async Task<ActionResult<object>> GetStats()
        {
            var shipperId = GetCurrentShipperId();

            var totalDelivered = await _context.Orders.CountAsync(o => o.ShipperId == shipperId && o.DeliveryStatus == "Đã giao hàng");
            var todayDelivered = await _context.Orders.CountAsync(o => o.ShipperId == shipperId && o.DeliveryStatus == "Đã giao hàng" && o.UpdatedAt >= DateTime.UtcNow.Date);

            return Ok(new
            {
                totalDelivered,
                todayDelivered,
                averageRating = await _context.ShipperRatings.Where(r => r.ShipperId == shipperId).AverageAsync(r => (double?)r.Score) ?? 0
            });
        }
    }
}