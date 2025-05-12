using HakiBaVuong.Data;
using HakiBaVuong.DTOs;
using HakiBaVuong.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace HakiBaVuong.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize(Roles = "Admin,Staff")]
    public class OrderManagementController : ControllerBase
    {
        private readonly DataContext _context;
        private readonly ILogger<OrderManagementController> _logger;

        public OrderManagementController(DataContext context, ILogger<OrderManagementController> logger)
        {
            _context = context;
            _logger = logger;
        }

        [HttpGet("brand/{brandId}")]
        public async Task<ActionResult<IEnumerable<OrderDTO>>> GetOrdersByBrand(int brandId, [FromQuery] FilterOrdersDTO filter)
        {
            _logger.LogInformation("GetOrdersByBrand called for brand {BrandId}", brandId);

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
                return BadRequest(new { message = "Brand không tồn tại." });
            }

            if (User.IsInRole("Staff") && brand.OwnerId != userId)
            {
                _logger.LogWarning("Staff user {UserId} does not have access to brand {BrandId}", userId, brandId);
                return Forbid();
            }

            var query = _context.Orders
                .Where(o => o.BrandId == brandId)
                .Include(o => o.OrderItems)
                .ThenInclude(i => i.Product)
                .Include(o => o.Payment)
                .AsQueryable();

            if (!string.IsNullOrEmpty(filter.PaymentStatus))
            {
                query = query.Where(o => o.PaymentStatus == filter.PaymentStatus);
            }

            if (!string.IsNullOrEmpty(filter.DeliveryStatus))
            {
                query = query.Where(o => o.DeliveryStatus == filter.DeliveryStatus);
            }

            if (filter.StartDate.HasValue)
            {
                query = query.Where(o => o.CreatedAt >= filter.StartDate.Value);
            }

            if (filter.EndDate.HasValue)
            {
                query = query.Where(o => o.CreatedAt <= filter.EndDate.Value);
            }

            var orders = await query.ToListAsync();

            var orderDtos = orders.Select(o => new OrderDTO
            {
                OrderId = o.OrderId,
                BrandId = o.BrandId,
                CustomerId = o.CustomerId,
                FullName = o.FullName,
                Phone = o.Phone,
                Address = o.Address,
                PaymentStatus = o.PaymentStatus,
                DeliveryStatus = o.DeliveryStatus,
                TotalAmount = o.TotalAmount,
                CreatedAt = o.CreatedAt,
                EstimatedDeliveryDate = o.EstimatedDeliveryDate,
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

            _logger.LogInformation("Retrieved {Count} orders for brand {BrandId}", orders.Count, brandId);
            return Ok(orderDtos);
        }

        [HttpPost("{id}/pay")]
        public async Task<IActionResult> PayOrder(int id, [FromBody] PayOrderDTO model)
        {
            _logger.LogInformation("PayOrder called for order {OrderId}", id);

            var userId = GetUserId();
            if (userId == null)
            {
                _logger.LogWarning("Invalid userId from token");
                return Unauthorized(new { message = "Token không hợp lệ." });
            }

            var order = await _context.Orders
                .Include(o => o.OrderItems)
                .ThenInclude(i => i.Product)
                .Include(o => o.Payment)
                .FirstOrDefaultAsync(o => o.OrderId == id);

            if (order == null)
            {
                _logger.LogWarning("Order not found: {OrderId}", id);
                return NotFound(new { message = "Đơn hàng không tồn tại." });
            }

            var brand = await _context.Brands.FindAsync(order.BrandId);
            if (brand == null || (User.IsInRole("Staff") && brand.OwnerId != userId))
            {
                _logger.LogWarning("User {UserId} does not have access to brand {BrandId}", userId, order.BrandId);
                return Forbid();
            }

            if (order.PaymentStatus != "Pending")
            {
                _logger.LogWarning("Order {OrderId} cannot be paid, paymentStatus: {PaymentStatus}", id, order.PaymentStatus);
                return BadRequest(new { message = "Đơn hàng không ở trạng thái thanh toán Pending." });
            }

            if (order.Payment == null)
            {
                _logger.LogWarning("Payment not found for order {OrderId}", id);
                return BadRequest(new { message = "Không tìm thấy thông tin thanh toán." });
            }

            if (order.Payment.Status != "Pending")
            {
                _logger.LogWarning("Payment for order {OrderId} is not pending, status: {Status}", id, order.Payment.Status);
                return BadRequest(new { message = "Thanh toán không ở trạng thái Pending." });
            }

            if (order.OrderItems.Any(i => i.Product != null && i.Product.BrandId != order.BrandId))
            {
                _logger.LogWarning("Order {OrderId} contains items from different brand", id);
                return BadRequest(new { message = "Đơn hàng chứa sản phẩm từ brand khác." });
            }

            if (!string.IsNullOrEmpty(model.PaymentMethod))
            {
                order.Payment.Method = model.PaymentMethod;
            }

            order.Payment.Status = model.PaymentMethod == "BankCard" ? "Completed" : "Pending";
            order.PaymentStatus = model.PaymentMethod == "BankCard" ? "Completed" : "Pending";
            order.DeliveryStatus = model.PaymentMethod == "BankCard" ? "Processing" : "Pending";
            order.UpdatedAt = DateTime.UtcNow;

            if (model.PaymentMethod == "BankCard")
            {
                foreach (var item in order.OrderItems)
                {
                    if (item.ProductId.HasValue)
                    {
                        var inventory = await _context.Inventories.FirstOrDefaultAsync(i => i.ProductId == item.ProductId.Value);
                        if (inventory == null || inventory.StockQuantity < item.Quantity)
                        {
                            _logger.LogWarning("Insufficient stock for product {ProductId} in order {OrderId}", item.ProductId, id);
                            return BadRequest(new { message = $"Sản phẩm {item.ProductName} không đủ tồn kho." });
                        }
                        inventory.StockQuantity -= item.Quantity;
                        inventory.LastUpdated = DateTime.UtcNow;
                        _context.Inventories.Update(inventory);
                    }
                }

                var cart = await _context.Carts
                    .Include(c => c.Items)
                    .FirstOrDefaultAsync(c => c.CustomerId == order.CustomerId);
                if (cart != null)
                {
                    _context.CartItems.RemoveRange(cart.Items);
                    _context.Carts.Remove(cart);
                }
            }

            await _context.SaveChangesAsync();

            var orderDto = new OrderDTO
            {
                OrderId = order.OrderId,
                BrandId = order.BrandId,
                CustomerId = order.CustomerId,
                FullName = order.FullName,
                Phone = order.Phone,
                Address = order.Address,
                PaymentStatus = order.PaymentStatus,
                DeliveryStatus = order.DeliveryStatus,
                TotalAmount = order.TotalAmount,
                CreatedAt = order.CreatedAt,
                EstimatedDeliveryDate = order.EstimatedDeliveryDate,
                OrderItems = order.OrderItems.Select(i => new OrderItemDTO
                {
                    ItemId = i.ItemId,
                    OrderId = i.OrderId,
                    ProductId = i.ProductId,
                    ProductName = i.ProductName,
                    Quantity = i.Quantity,
                    Price = i.Price
                }).ToList(),
                Payment = new PaymentDTO
                {
                    PaymentId = order.Payment.PaymentId,
                    Amount = order.Payment.Amount,
                    Method = order.Payment.Method,
                    Status = order.Payment.Status
                }
            };

            _logger.LogInformation("Payment completed for order {OrderId} by user {UserId}", id, userId);
            return Ok(orderDto);
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateOrder(int id, [FromBody] UpdateOrderDTO model)
        {
            _logger.LogInformation("UpdateOrder called for order {OrderId}", id);

            var userId = GetUserId();
            if (userId == null)
            {
                _logger.LogWarning("Invalid userId from token");
                return Unauthorized(new { message = "Token không hợp lệ." });
            }

            var order = await _context.Orders
                .Include(o => o.Payment)
                .Include(o => o.OrderItems)
                .FirstOrDefaultAsync(o => o.OrderId == id);

            if (order == null)
            {
                _logger.LogWarning("Order not found: {OrderId}", id);
                return NotFound(new { message = "Đơn hàng không tồn tại." });
            }

            var brand = await _context.Brands.FindAsync(order.BrandId);
            if (brand == null || (User.IsInRole("Staff") && brand.OwnerId != userId))
            {
                _logger.LogWarning("Staff user {UserId} does not have access to brand {BrandId}", userId, order.BrandId);
                return Forbid();
            }

            var validPaymentStatuses = new[] { "Pending", "Completed", "Cancelled" };
            var validDeliveryStatuses = new[] { "Pending", "Processing", "Shipped", "Delivered", "Cancelled" };

            if (!string.IsNullOrEmpty(model.PaymentStatus) && !validPaymentStatuses.Contains(model.PaymentStatus))
            {
                _logger.LogWarning("Invalid payment status: {PaymentStatus}", model.PaymentStatus);
                return BadRequest(new { message = "Trạng thái thanh toán không hợp lệ." });
            }

            if (!string.IsNullOrEmpty(model.DeliveryStatus) && !validDeliveryStatuses.Contains(model.DeliveryStatus))
            {
                _logger.LogWarning("Invalid delivery status: {DeliveryStatus}", model.DeliveryStatus);
                return BadRequest(new { message = "Trạng thái giao hàng không hợp lệ." });
            }

            if (!string.IsNullOrEmpty(model.FullName)) order.FullName = model.FullName;
            if (!string.IsNullOrEmpty(model.Phone)) order.Phone = model.Phone;
            if (!string.IsNullOrEmpty(model.Address)) order.Address = model.Address;

            if (!string.IsNullOrEmpty(model.PaymentStatus))
            {
                order.PaymentStatus = model.PaymentStatus;
                if (order.Payment != null)
                {
                    order.Payment.Status = model.PaymentStatus;
                }
            }

            if (!string.IsNullOrEmpty(model.DeliveryStatus))
            {
                order.DeliveryStatus = model.DeliveryStatus;
            }

            order.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            var orderDto = new OrderDTO
            {
                OrderId = order.OrderId,
                BrandId = order.BrandId,
                CustomerId = order.CustomerId,
                FullName = order.FullName,
                Phone = order.Phone,
                Address = order.Address,
                PaymentStatus = order.PaymentStatus,
                DeliveryStatus = order.DeliveryStatus,
                TotalAmount = order.TotalAmount,
                CreatedAt = order.CreatedAt,
                EstimatedDeliveryDate = order.EstimatedDeliveryDate,
                OrderItems = order.OrderItems.Select(i => new OrderItemDTO
                {
                    ItemId = i.ItemId,
                    OrderId = i.OrderId,
                    ProductId = i.ProductId,
                    ProductName = i.ProductName,
                    Quantity = i.Quantity,
                    Price = i.Price
                }).ToList(),
                Payment = order.Payment != null ? new PaymentDTO
                {
                    PaymentId = order.Payment.PaymentId,
                    Amount = order.Payment.Amount,
                    Method = order.Payment.Method,
                    Status = order.Payment.Status
                } : null
            };

            _logger.LogInformation("Updated order {OrderId} by user {UserId}", id, userId);
            return Ok(orderDto);
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteOrder(int id)
        {
            _logger.LogInformation("DeleteOrder called for order {OrderId}", id);

            var userId = GetUserId();
            if (userId == null)
            {
                _logger.LogWarning("Invalid userId from token");
                return Unauthorized(new { message = "Token không hợp lệ." });
            }

            var order = await _context.Orders
                .Include(o => o.OrderItems)
                .Include(o => o.Payment)
                .FirstOrDefaultAsync(o => o.OrderId == id);

            if (order == null)
            {
                _logger.LogWarning("Order not found: {OrderId}", id);
                return NotFound(new { message = "Đơn hàng không tồn tại." });
            }

            var brand = await _context.Brands.FindAsync(order.BrandId);
            if (brand == null || (User.IsInRole("Staff") && brand.OwnerId != userId))
            {
                _logger.LogWarning("Staff user {UserId} does not have access to brand {BrandId}", userId, order.BrandId);
                return Forbid();
            }

            if (order.DeliveryStatus != "Pending")
            {
                _logger.LogWarning("Order {OrderId} cannot be deleted, deliveryStatus: {DeliveryStatus}", id, order.DeliveryStatus);
                return BadRequest(new { message = "Chỉ có thể xóa đơn hàng ở trạng thái Pending." });
            }

            foreach (var item in order.OrderItems)
            {
                if (item.ProductId.HasValue)
                {
                    var inventory = await _context.Inventories.FirstOrDefaultAsync(i => i.ProductId == item.ProductId.Value);
                    if (inventory != null)
                    {
                        inventory.StockQuantity += item.Quantity;
                        inventory.LastUpdated = DateTime.UtcNow;
                        _context.Inventories.Update(inventory);
                    }
                }
            }

            if (order.Payment != null)
            {
                _context.Payments.Remove(order.Payment);
            }
            _context.Orders.Remove(order);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Deleted order {OrderId} by user {UserId}", id, userId);
            return Ok(new { message = "Xóa đơn hàng thành công." });
        }

        private int? GetUserId()
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            return int.TryParse(userIdClaim, out int userId) ? userId : null;
        }
    }
}