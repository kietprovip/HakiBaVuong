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

        // ==================== 3. DANH SÁCH ĐƠN HÀNG CẦN GIAO (gần nhất, đang chờ) ====================
        [HttpGet("pending-orders")]
        public async Task<ActionResult<IEnumerable<OrderDTO>>> GetPendingOrders()
        {
            var orders = await _context.Orders
                .Where(o => o.DeliveryStatus == "Đã thanh toán" && o.ShipperId == null) // Chưa có shipper
                .OrderBy(o => o.CreatedAt)
                .Take(20)
                .Select(o => new OrderDTO
                {
                    OrderId = o.OrderId,
                    FullName = o.FullName,
                    Phone = o.Phone,
                    Address = o.Address,
                    TotalAmount = o.TotalAmount,
                    CreatedAt = o.CreatedAt,
                    EstimatedDeliveryDate = o.EstimatedDeliveryDate
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

            if (order.DeliveryStatus != "Đã thanh toán")
                return BadRequest(new { message = "Đơn hàng chưa sẵn sàng để giao." });

            order.ShipperId = shipperId;
            order.DeliveryStatus = "Đang giao";
            order.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            _logger.LogInformation("Shipper {ShipperId} nhận giao đơn {OrderId}", shipperId, orderId);
            return Ok(new { message = "Nhận giao đơn thành công!", orderId });
        }

        // ==================== 5. DANH SÁCH ĐƠN HÀNG ĐANG GIAO CỦA TÔI ====================
        [HttpGet("my-deliveries")]
        public async Task<ActionResult<IEnumerable<OrderDTO>>> GetMyDeliveries()
        {
            var shipperId = GetCurrentShipperId();

            var orders = await _context.Orders
                .Where(o => o.ShipperId == shipperId && o.DeliveryStatus == "Đang giao")
                .Select(o => new OrderDTO
                {
                    OrderId = o.OrderId,
                    FullName = o.FullName,
                    Phone = o.Phone,
                    Address = o.Address,
                    TotalAmount = o.TotalAmount,
                    EstimatedDeliveryDate = o.EstimatedDeliveryDate,
                    OrderItems = o.OrderItems.Select(oi => new OrderItemDTO
                    {
                        ProductName = oi.ProductName,
                        Quantity = oi.Quantity
                    }).ToList()
                })
                .ToListAsync();

            return Ok(orders);
        }

        // ==================== 6. HOÀN THÀNH GIAO HÀNG ====================
        [HttpPost("complete-delivery/{orderId}")]
        public async Task<IActionResult> CompleteDelivery(int orderId)
        {
            var shipperId = GetCurrentShipperId();

            var order = await _context.Orders.FirstOrDefaultAsync(o => o.OrderId == orderId && o.ShipperId == shipperId);
            if (order == null) return NotFound(new { message = "Không tìm thấy đơn hàng." });

            if (order.DeliveryStatus != "Đang giao")
                return BadRequest(new { message = "Đơn hàng chưa ở trạng thái đang giao." });

            order.DeliveryStatus = "Hoàn thành";
            order.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            _logger.LogInformation("Shipper {ShipperId} hoàn thành giao đơn {OrderId}", shipperId, orderId);
            return Ok(new { message = "Giao hàng thành công! Cảm ơn bạn!" });
        }

        // ==================== 7. CẬP NHẬT VỊ TRÍ GPS (dùng cho map realtime) ====================
        [HttpPost("update-location")]
        public async Task<IActionResult> UpdateLocation([FromBody] LocationDTO model)
        {
            if (model.Lat < -90 || model.Lat > 90 || model.Long < -180 || model.Long > 180)
                return BadRequest(new { message = "Tọa độ không hợp lệ." });

            var shipperId = GetCurrentShipperId();

            // Lưu vào cache (Redis) để frontend poll realtime
            var locationJson = System.Text.Json.JsonSerializer.Serialize(new { model.Lat, model.Long, UpdatedAt = DateTime.UtcNow });
            await _context.Database.ExecuteSqlRawAsync(
                $"INSERT INTO ShipperLocations (ShipperId, Latitude, Longitude, UpdatedAt) VALUES ({shipperId}, {model.Lat}, {model.Long}, GETUTCDATE()) " +
                $"ON DUPLICATE KEY UPDATE Latitude = {model.Lat}, Longitude = {model.Long}, UpdatedAt = GETUTCDATE()"
            ); // Nếu dùng MySQL, thay bằng cách khác nếu SQL Server

            // Hoặc dùng DistributedCache nếu không có bảng
            // await _cache.SetStringAsync($"shipper_loc_{shipperId}", locationJson, TimeSpan.FromMinutes(10));

            return Ok(new { message = "Cập nhật vị trí thành công!" });
        }

        // ==================== 8. THỐNG KÊ DOANH THU (tùy chọn) ====================
        [HttpGet("stats")]
        public async Task<ActionResult<object>> GetStats()
        {
            var shipperId = GetCurrentShipperId();

            var totalDelivered = await _context.Orders.CountAsync(o => o.ShipperId == shipperId && o.DeliveryStatus == "Hoàn thành");
            var todayDelivered = await _context.Orders.CountAsync(o => o.ShipperId == shipperId && o.DeliveryStatus == "Hoàn thành" && o.UpdatedAt >= DateTime.UtcNow.Date);

            return Ok(new
            {
                totalDelivered,
                todayDelivered,
                averageRating = await _context.ShipperRatings.Where(r => r.ShipperId == shipperId).AverageAsync(r => (double?)r.Score) ?? 0
            });
        }
    }
}