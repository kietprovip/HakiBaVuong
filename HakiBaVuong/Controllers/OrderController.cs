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
    [Authorize]
    public class OrderController : ControllerBase
    {
        private readonly DataContext _context;
        private readonly ILogger<OrderController> _logger;

        public OrderController(DataContext context, ILogger<OrderController> logger)
        {
            _context = context;
            _logger = logger;
        }

        [HttpPost("create")]
        public async Task<ActionResult<OrderDTO>> CreateOrder([FromBody] CreateOrderDTO model)
        {
            _logger.LogInformation("CreateOrder called with brandId {BrandId}, paymentMethod {PaymentMethod}", model.BrandId, model.PaymentMethod);

            var customerId = GetCustomerId();
            if (customerId == null)
            {
                _logger.LogWarning("Invalid customerId from token");
                return Unauthorized(new { message = "Token không hợp lệ." });
            }

            var brand = await _context.Brands.FindAsync(model.BrandId);
            if (brand == null)
            {
                _logger.LogWarning("Brand not found: {BrandId}", model.BrandId);
                return BadRequest(new { message = "Brand không tồn tại." });
            }

            var cart = await _context.Carts
                .Include(c => c.Items)
                .ThenInclude(i => i.Product)
                .FirstOrDefaultAsync(c => c.CustomerId == customerId);

            if (cart == null || !cart.Items.Any())
            {
                _logger.LogWarning("Cart is empty or not found for customer {CustomerId}", customerId);
                return BadRequest(new { message = "Giỏ hàng trống hoặc không tồn tại." });
            }

            if (cart.Items.Any(i => i.Product.BrandId != model.BrandId))
            {
                _logger.LogWarning("Cart contains products from different brand for customer {CustomerId}", customerId);
                return BadRequest(new { message = "Giỏ hàng chứa sản phẩm từ brand khác." });
            }

            foreach (var item in cart.Items)
            {
                var inventory = await _context.Inventories.FirstOrDefaultAsync(i => i.ProductId == item.ProductId);
                if (inventory == null || inventory.StockQuantity < item.Quantity)
                {
                    _logger.LogWarning("Insufficient stock for product {ProductId}", item.ProductId);
                    return BadRequest(new { message = $"Sản phẩm {item.Product.Name} không đủ tồn kho." });
                }
            }

            var order = new Order
            {
                BrandId = model.BrandId,
                CustomerId = customerId,
                FullName = model.FullName,
                Phone = model.Phone,
                Address = model.Address,
                Status = "Pending",
                TotalAmount = cart.Items.Sum(i => i.Quantity * i.Product.PriceSell),
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
                OrderItems = cart.Items.Select(i => new OrderItem
                {
                    ProductId = i.ProductId,
                    ProductName = i.Product.Name,
                    Quantity = i.Quantity,
                    Price = i.Product.PriceSell
                }).ToList()
            };

            _context.Orders.Add(order);
            await _context.SaveChangesAsync();

            var payment = new Payment
            {
                OrderId = order.OrderId,
                Amount = order.TotalAmount,
                Method = model.PaymentMethod,
                Status = "Pending",
                CreatedAt = DateTime.UtcNow
            };

            _context.Payments.Add(payment);
            await _context.SaveChangesAsync();

            order.PaymentId = payment.PaymentId;
            await _context.SaveChangesAsync();

            var orderDto = new OrderDTO
            {
                OrderId = order.OrderId,
                BrandId = order.BrandId,
                CustomerId = order.CustomerId,
                FullName = order.FullName,
                Phone = order.Phone,
                Address = order.Address,
                Status = order.Status,
                TotalAmount = order.TotalAmount,
                CreatedAt = order.CreatedAt,
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
                    PaymentId = payment.PaymentId,
                    Amount = payment.Amount,
                    Method = payment.Method,
                    Status = payment.Status
                }
            };

            _logger.LogInformation("Created order {OrderId} for customer {CustomerId}", order.OrderId, customerId);
            return CreatedAtAction(nameof(GetOrder), new { id = order.OrderId }, orderDto);
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<OrderDTO>> GetOrder(int id)
        {
            _logger.LogInformation("GetOrder called for order {OrderId}", id);

            var customerId = GetCustomerId();
            if (customerId == null)
            {
                _logger.LogWarning("Invalid customerId from token");
                return Unauthorized(new { message = "Token không hợp lệ." });
            }

            var order = await _context.Orders
                .Include(o => o.OrderItems)
                .ThenInclude(i => i.Product)
                .Include(o => o.Payment)
                .FirstOrDefaultAsync(o => o.OrderId == id && o.CustomerId == customerId);

            if (order == null)
            {
                _logger.LogWarning("Order not found: {OrderId}", id);
                return NotFound(new { message = "Đơn hàng không tồn tại." });
            }

            var orderDto = new OrderDTO
            {
                OrderId = order.OrderId,
                BrandId = order.BrandId,
                CustomerId = order.CustomerId,
                FullName = order.FullName,
                Phone = order.Phone,
                Address = order.Address,
                Status = order.Status,
                TotalAmount = order.TotalAmount,
                CreatedAt = order.CreatedAt,
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

            _logger.LogInformation("Retrieved order {OrderId}", id);
            return Ok(orderDto);
        }

        [HttpGet("customer")]
        public async Task<ActionResult<IEnumerable<OrderDTO>>> GetCustomerOrders([FromQuery] FilterOrdersDTO filter)
        {
            _logger.LogInformation("GetCustomerOrders called");

            var customerId = GetCustomerId();
            if (customerId == null)
            {
                _logger.LogWarning("Invalid customerId from token");
                return Unauthorized(new { message = "Token không hợp lệ." });
            }

            var query = _context.Orders
                .Where(o => o.CustomerId == customerId)
                .Include(o => o.OrderItems)
                .ThenInclude(i => i.Product)
                .Include(o => o.Payment)
                .AsQueryable();

            if (!string.IsNullOrEmpty(filter.Status))
            {
                query = query.Where(o => o.Status == filter.Status);
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
                Status = o.Status,
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

            _logger.LogInformation("Retrieved {Count} orders for customer {CustomerId}", orders.Count, customerId);
            return Ok(orderDtos);
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateOrder(int id, [FromBody] UpdateOrderDTO model)
        {
            _logger.LogInformation("UpdateOrder called for order {OrderId}", id);

            var customerId = GetCustomerId();
            if (customerId == null)
            {
                _logger.LogWarning("Invalid customerId from token");
                return Unauthorized(new { message = "Token không hợp lệ." });
            }

            var order = await _context.Orders
                .Include(o => o.Payment)
                .Include(o => o.OrderItems)
                .FirstOrDefaultAsync(o => o.OrderId == id && o.CustomerId == customerId);

            if (order == null)
            {
                _logger.LogWarning("Order not found: {OrderId}", id);
                return NotFound(new { message = "Đơn hàng không tồn tại." });
            }

            if (order.Status != "Pending")
            {
                _logger.LogWarning("Order {OrderId} cannot be updated by customer, status: {Status}", id, order.Status);
                return BadRequest(new { message = "Chỉ có thể cập nhật đơn hàng ở trạng thái Pending." });
            }

            if (!string.IsNullOrEmpty(model.Status) && model.Status != "Cancelled")
            {
                _logger.LogWarning("Customer {CustomerId} can only cancel order {OrderId}", customerId, id);
                return BadRequest(new { message = "Khách hàng chỉ có thể hủy đơn hàng." });
            }

            if (!string.IsNullOrEmpty(model.FullName)) order.FullName = model.FullName;
            if (!string.IsNullOrEmpty(model.Phone)) order.Phone = model.Phone;
            if (!string.IsNullOrEmpty(model.Address)) order.Address = model.Address;
            if (!string.IsNullOrEmpty(model.Status))
            {
                order.Status = model.Status;
                if (order.Payment != null && model.Status == "Cancelled")
                {
                    order.Payment.Status = "Cancelled";
                }
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
                Status = order.Status,
                TotalAmount = order.TotalAmount,
                CreatedAt = order.CreatedAt,
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

            _logger.LogInformation("Updated order {OrderId} by customer {CustomerId}", id, customerId);
            return Ok(orderDto);
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteOrder(int id)
        {
            _logger.LogInformation("DeleteOrder called for order {OrderId}", id);

            var customerId = GetCustomerId();
            if (customerId == null)
            {
                _logger.LogWarning("Invalid customerId from token");
                return Unauthorized(new { message = "Token không hợp lệ." });
            }

            var order = await _context.Orders
                .Include(o => o.OrderItems)
                .Include(o => o.Payment)
                .FirstOrDefaultAsync(o => o.OrderId == id && o.CustomerId == customerId);

            if (order == null)
            {
                _logger.LogWarning("Order not found: {OrderId}", id);
                return NotFound(new { message = "Đơn hàng không tồn tại." });
            }

            if (order.Status != "Pending")
            {
                _logger.LogWarning("Order {OrderId} cannot be deleted, status: {Status}", id, order.Status);
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

            _logger.LogInformation("Deleted order {OrderId} by customer {CustomerId}", id, customerId);
            return Ok(new { message = "Xóa đơn hàng thành công." });
        }

        private int? GetCustomerId()
        {
            var customerIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            return int.TryParse(customerIdClaim, out int customerId) ? customerId : null;
        }

        private int? GetUserId()
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            return int.TryParse(userIdClaim, out int userId) ? userId : null;
        }
    }
}