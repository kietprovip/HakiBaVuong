using HakiBaVuong.Data;
using HakiBaVuong.DTOs;
using HakiBaVuong.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using System.Threading.Tasks;

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
            _logger.LogInformation("CreateOrder called with paymentMethod {PaymentMethod}, fullName {FullName}, phone {Phone}, address {Address}, brandId {BrandId}",
                model.PaymentMethod, model.FullName, model.Phone, model.Address, model.BrandId);

            var customerId = GetCustomerId();
            if (customerId == null)
            {
                _logger.LogWarning("Invalid customerId from token");
                return Unauthorized(new { message = "Token không hợp lệ." });
            }

            if (string.IsNullOrWhiteSpace(model.FullName) || string.IsNullOrWhiteSpace(model.Phone) || string.IsNullOrWhiteSpace(model.Address))
            {
                _logger.LogWarning("Invalid input data: FullName, Phone, or Address is empty");
                return BadRequest(new { message = "Vui lòng điền đầy đủ thông tin thanh toán." });
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

            // Lấy các CartItems thuộc BrandId được gửi từ frontend
            var cartItemsForBrand = cart.Items.Where(i => i.Product.BrandId == model.BrandId).ToList();
            if (!cartItemsForBrand.Any())
            {
                _logger.LogWarning("No items found for brand {BrandId} in cart for customer {CustomerId}", model.BrandId, customerId);
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
                // Bước 1: Tạo và lưu Order trước, không gán OrderItems ngay
                var order = new Order
                {
                    BrandId = model.BrandId,
                    CustomerId = customerId,
                    FullName = model.FullName,
                    Phone = model.Phone,
                    Address = model.Address,
                    Status = "Pending",
                    TotalAmount = cartItemsForBrand.Sum(i => i.Quantity * i.Product.PriceSell),
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };

                _context.Orders.Add(order);
                await _context.SaveChangesAsync();

                // Bước 2: Tạo OrderItems và gán OrderId sau khi Order đã được lưu
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
                    Status = "Pending",
                    CreatedAt = DateTime.UtcNow
                };

                _context.Payments.Add(payment);
                await _context.SaveChangesAsync();

                order.PaymentId = payment.PaymentId;
                payment.Status = "Completed";
                order.Status = "Completed";
                order.UpdatedAt = DateTime.UtcNow;

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

                // Xóa các CartItems đã được thanh toán khỏi giỏ hàng
                _context.CartItems.RemoveRange(cartItemsForBrand);
                await _context.SaveChangesAsync();

                // Nếu giỏ hàng không còn mục nào, xóa giỏ hàng
                if (!cart.Items.Any())
                {
                    _context.Carts.Remove(cart);
                    await _context.SaveChangesAsync();
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
                    Status = order.Status,
                    TotalAmount = order.TotalAmount,
                    CreatedAt = order.CreatedAt,
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

                _logger.LogInformation("Created order {OrderId} for customer {CustomerId}", order.OrderId, customerId);
                return CreatedAtAction(nameof(GetOrder), new { id = order.OrderId }, orderDto);
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, "Failed to create order for customer {CustomerId}. Exception: {Exception}. InnerException: {InnerException}",
                    customerId, ex.Message, ex.InnerException?.Message);
                return StatusCode(500, new { message = "Lỗi khi tạo đơn hàng. Vui lòng thử lại. Chi tiết: " + ex.Message + (ex.InnerException != null ? " Nội dung chi tiết: " + ex.InnerException.Message : "") });
            }
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

        [HttpGet("brand/{brandId}")]
        [Authorize(Roles = "Admin,Staff")]
        public async Task<ActionResult<IEnumerable<OrderDTO>>> GetOrdersByBrand(int brandId)
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

            var orders = await _context.Orders
                .Where(o => o.BrandId == brandId)
                .Include(o => o.OrderItems)
                .ThenInclude(i => i.Product)
                .Include(o => o.Payment)
                .ToListAsync();

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

            _logger.LogInformation("Retrieved {Count} orders for brand {BrandId}", orders.Count, brandId);
            return Ok(orderDtos);
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