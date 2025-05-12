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
            _logger.LogInformation("CreateOrder called with paymentMethod {PaymentMethod}, addressId {AddressId}, brandId {BrandId}",
                model.PaymentMethod, model.AddressId, model.BrandId);

            var customerId = GetCustomerId();
            if (!customerId.HasValue)
            {
                _logger.LogWarning("Invalid customerId from token");
                return Unauthorized(new { message = "Token không hợp lệ." });
            }

            var address = await _context.CustomerAddresses
                .FirstOrDefaultAsync(a => a.AddressId == model.AddressId && a.CustomerId == customerId.Value);
            if (address == null)
            {
                _logger.LogWarning("Invalid or unauthorized addressId {AddressId} for customer {CustomerId}", model.AddressId, customerId.Value);
                return BadRequest(new { message = "Địa chỉ không hợp lệ hoặc không thuộc về bạn." });
            }

            var cart = await _context.Carts
                .Include(c => c.Items)
                .ThenInclude(i => i.Product)
                .FirstOrDefaultAsync(c => c.CustomerId == customerId.Value);
            if (cart == null || !cart.Items.Any())
            {
                _logger.LogWarning("Cart is empty or not found for customer {CustomerId}", customerId.Value);
                return BadRequest(new { message = "Giỏ hàng trống hoặc không tồn tại." });
            }

            var cartItemsForBrand = cart.Items.Where(i => i.Product.BrandId == model.BrandId).ToList();
            if (!cartItemsForBrand.Any())
            {
                _logger.LogWarning("No items found for brand {BrandId} in cart for customer {CustomerId}", model.BrandId, customerId.Value);
                return BadRequest(new { message = "Không có sản phẩm nào thuộc thương hiệu này trong giỏ hàng." });
            }

            var brand = await _context.Brands.FindAsync(model.BrandId);
            if (brand == null)
            {
                _logger.LogWarning("Brand not found: {BrandId}", model.BrandId);
                return BadRequest(new { message = "Brand không tồn tại." });
            }

            foreach (var item in cartItemsForBrand)
            {
                var product = await _context.Products.FindAsync(item.ProductId);
                if (product == null)
                {
                    _logger.LogWarning("Product not found: {ProductId}", item.ProductId);
                    return BadRequest(new { message = $"Sản phẩm với ID {item.ProductId} không tồn tại." });
                }

                var inventory = await _context.Inventories.FirstOrDefaultAsync(i => i.ProductId == item.ProductId);
                if (inventory == null || inventory.StockQuantity < item.Quantity)
                {
                    _logger.LogWarning("Insufficient stock for product {ProductId}", item.ProductId);
                    return BadRequest(new { message = $"Sản phẩm {item.Product.Name} không đủ tồn kho." });
                }
            }

            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                var order = new Order
                {
                    BrandId = model.BrandId,
                    CustomerId = customerId.Value,
                    FullName = address.FullName,
                    Phone = address.Phone,
                    Address = address.Address,
                    PaymentStatus = model.PaymentMethod == "BankCard" ? "Completed" : "Pending",
                    DeliveryStatus = "Pending",
                    TotalAmount = cartItemsForBrand.Sum(i => i.Quantity * i.Product.PriceSell),
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow,
                    EstimatedDeliveryDate = DateTime.UtcNow.AddDays(5)
                };

                _context.Orders.Add(order);
                await _context.SaveChangesAsync();

                var orderItems = cartItemsForBrand.Select(i => new OrderItem
                {
                    OrderId = order.OrderId,
                    ProductId = i.ProductId,
                    ProductName = i.Product.Name,
                    Quantity = i.Quantity,
                    Price = i.Product.PriceSell
                }).ToList();

                _context.OrderItems.AddRange(orderItems);
                await _context.SaveChangesAsync();

                var payment = new Payment
                {
                    OrderId = order.OrderId,
                    Amount = order.TotalAmount,
                    Method = model.PaymentMethod,
                    Status = model.PaymentMethod == "BankCard" ? "Completed" : "Pending",
                    CreatedAt = DateTime.UtcNow
                };

                _context.Payments.Add(payment);
                await _context.SaveChangesAsync();

                order.PaymentId = payment.PaymentId;

                if (model.PaymentMethod == "BankCard")
                {
                    foreach (var item in cartItemsForBrand)
                    {
                        var inventory = await _context.Inventories.FirstOrDefaultAsync(i => i.ProductId == item.ProductId);
                        if (inventory == null)
                        {
                            _logger.LogWarning("Inventory not found for product {ProductId}", item.ProductId);
                            throw new Exception($"Inventory not found for product {item.ProductId}");
                        }
                        inventory.StockQuantity -= item.Quantity;
                        inventory.LastUpdated = DateTime.UtcNow;
                        _context.Inventories.Update(inventory);
                    }

                    _context.CartItems.RemoveRange(cartItemsForBrand);
                    await _context.SaveChangesAsync();

                    if (!cart.Items.Any())
                    {
                        _context.Carts.Remove(cart);
                        await _context.SaveChangesAsync();
                    }
                }

                await transaction.CommitAsync();

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
                    OrderItems = orderItems.Select(i => new OrderItemDTO
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

                _logger.LogInformation("Created order {OrderId} for customer {CustomerId}", order.OrderId, customerId.Value);
                return CreatedAtAction(nameof(GetOrder), new { id = order.OrderId }, orderDto);
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, "Failed to create order for customer {CustomerId}. Exception: {Exception}. InnerException: {InnerException}",
                    customerId.Value, ex.Message, ex.InnerException?.Message);
                return StatusCode(500, new { message = "Lỗi khi tạo đơn hàng. Vui lòng thử lại. Chi tiết: " + ex.Message + (ex.InnerException != null ? " Nội dung chi tiết: " + ex.InnerException.Message : "") });
            }
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<OrderDTO>> GetOrder(int id)
        {
            _logger.LogInformation("GetOrder called for order {OrderId}", id);

            var customerId = GetCustomerId();
            if (!customerId.HasValue)
            {
                _logger.LogWarning("Invalid customerId from token");
                return Unauthorized(new { message = "Token không hợp lệ." });
            }

            var order = await _context.Orders
                .Include(o => o.OrderItems)
                .ThenInclude(i => i.Product)
                .Include(o => o.Payment)
                .FirstOrDefaultAsync(o => o.OrderId == id && o.CustomerId == customerId.Value);

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

            _logger.LogInformation("Retrieved order {OrderId}", id);
            return Ok(orderDto);
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateOrder(int id, [FromBody] UpdateOrderDTO model)
        {
            _logger.LogInformation("UpdateOrder called for order {OrderId}", id);

            var customerId = GetCustomerId();
            if (!customerId.HasValue)
            {
                _logger.LogWarning("Invalid customerId from token");
                return Unauthorized(new { message = "Token không hợp lệ." });
            }

            var order = await _context.Orders
                .Include(o => o.Payment)
                .Include(o => o.OrderItems)
                .FirstOrDefaultAsync(o => o.OrderId == id && o.CustomerId == customerId.Value);

            if (order == null)
            {
                _logger.LogWarning("Order not found: {OrderId}", id);
                return NotFound(new { message = "Đơn hàng không tồn tại." });
            }

            if (order.DeliveryStatus != "Pending" && order.DeliveryStatus != "Processing")
            {
                _logger.LogWarning("Order {OrderId} cannot be updated by customer, deliveryStatus: {DeliveryStatus}", id, order.DeliveryStatus);
                return BadRequest(new { message = "Chỉ có thể cập nhật đơn hàng ở trạng thái Pending hoặc Processing." });
            }

            if ((!string.IsNullOrEmpty(model.DeliveryStatus) && model.DeliveryStatus != "Cancelled") ||
                (!string.IsNullOrEmpty(model.PaymentStatus) && model.PaymentStatus != "Cancelled"))
            {
                _logger.LogWarning("Customer {CustomerId} can only cancel order {OrderId}", customerId.Value, id);
                return BadRequest(new { message = "Khách hàng chỉ có thể hủy đơn hàng." });
            }

            if (!string.IsNullOrEmpty(model.FullName)) order.FullName = model.FullName;
            if (!string.IsNullOrEmpty(model.Phone)) order.Phone = model.Phone;
            if (!string.IsNullOrEmpty(model.Address)) order.Address = model.Address;

            if (!string.IsNullOrEmpty(model.DeliveryStatus) && !string.IsNullOrEmpty(model.PaymentStatus))
            {
                if (model.DeliveryStatus == "Cancelled" && model.PaymentStatus == "Cancelled")
                {
                    order.DeliveryStatus = "Cancelled";
                    order.PaymentStatus = "Cancelled";
                    if (order.Payment != null)
                    {
                        order.Payment.Status = "Cancelled";
                    }
                }
                else
                {
                    _logger.LogWarning("Invalid status combination for order {OrderId}", id);
                    return BadRequest(new { message = "Cả DeliveryStatus và PaymentStatus phải là Cancelled khi hủy đơn hàng." });
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

            _logger.LogInformation("Updated order {OrderId} by customer {CustomerId}", id, customerId.Value);
            return Ok(orderDto);
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