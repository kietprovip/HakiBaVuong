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
    public class CartController : ControllerBase
    {
        private readonly DataContext _context;
        private readonly ILogger<CartController> _logger;

        public CartController(DataContext context, ILogger<CartController> logger)
        {
            _context = context;
            _logger = logger;
        }

        [HttpGet]
        public async Task<ActionResult<CartDTO>> GetCart()
        {
            _logger.LogInformation("GetCart called");

            var customerId = GetCustomerId();
            if (customerId == null)
            {
                _logger.LogWarning("Invalid customerId from token");
                return Unauthorized(new { message = "Token không hợp lệ." });
            }

            var cart = await _context.Carts
                .Include(c => c.Items)
                .ThenInclude(i => i.Product)
                .ThenInclude(p => p.Brand) // Include Brand để lấy BrandName
                .FirstOrDefaultAsync(c => c.CustomerId == customerId);

            if (cart == null)
            {
                cart = new Cart { CustomerId = customerId.Value, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow };
                _context.Carts.Add(cart);
                await _context.SaveChangesAsync();
                _logger.LogInformation("Created new cart for customer {CustomerId}", customerId);
            }

            var cartDto = new CartDTO
            {
                CartId = cart.CartId,
                CustomerId = cart.CustomerId,
                Items = cart.Items.Select(i => new CartItemDTO
                {
                    CartItemId = i.CartItemId,
                    ProductId = i.ProductId,
                    ProductName = i.Product.Name,
                    PriceSell = i.Product.PriceSell,
                    Image = i.Product.Image,
                    Quantity = i.Quantity,
                    BrandId = i.Product.BrandId,
                    BrandName = i.Product.Brand?.Name ?? "Thương hiệu không xác định" // Thêm BrandName
                }).ToList()
            };

            _logger.LogInformation("Retrieved cart {CartId} with {ItemCount} items", cart.CartId, cartDto.Items.Count);
            return Ok(cartDto);
        }

        [HttpPost("add")]
        public async Task<IActionResult> AddToCart([FromBody] AddToCartDTO model)
        {
            _logger.LogInformation("AddToCart called for product {ProductId}, quantity {Quantity}", model.ProductId, model.Quantity);

            var customerId = GetCustomerId();
            if (customerId == null)
            {
                _logger.LogWarning("Invalid customerId from token");
                return Unauthorized(new { message = "Token không hợp lệ." });
            }

            if (model.Quantity <= 0)
            {
                _logger.LogWarning("Invalid quantity: {Quantity}", model.Quantity);
                return BadRequest(new { message = "Số lượng phải lớn hơn 0." });
            }

            var product = await _context.Products.FindAsync(model.ProductId);
            if (product == null)
            {
                _logger.LogWarning("Product not found: {ProductId}", model.ProductId);
                return NotFound(new { message = "Sản phẩm không tồn tại." });
            }

            var inventory = await _context.Inventories.FirstOrDefaultAsync(i => i.ProductId == model.ProductId);
            if (inventory == null || inventory.StockQuantity < model.Quantity)
            {
                _logger.LogWarning("Insufficient stock for product {ProductId}", model.ProductId);
                return BadRequest(new { message = "Số lượng tồn kho không đủ." });
            }

            var cart = await _context.Carts
                .Include(c => c.Items)
                .FirstOrDefaultAsync(c => c.CustomerId == customerId);

            if (cart == null)
            {
                cart = new Cart { CustomerId = customerId.Value, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow };
                _context.Carts.Add(cart);
                await _context.SaveChangesAsync();
            }

            var cartItem = cart.Items.FirstOrDefault(i => i.ProductId == model.ProductId);
            if (cartItem == null)
            {
                cartItem = new CartItem
                {
                    CartId = cart.CartId,
                    ProductId = model.ProductId,
                    Quantity = model.Quantity
                };
                cart.Items.Add(cartItem);
            }
            else
            {
                cartItem.Quantity += model.Quantity;
                if (cartItem.Quantity > inventory.StockQuantity)
                {
                    _logger.LogWarning("Total quantity exceeds stock for product {ProductId}", model.ProductId);
                    return BadRequest(new { message = "Tổng số lượng vượt quá tồn kho." });
                }
            }

            cart.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            _logger.LogInformation("Added product {ProductId} to cart {CartId}", model.ProductId, cart.CartId);
            return Ok(new { message = "Thêm sản phẩm vào giỏ hàng thành công." });
        }

        [HttpPut("update/{cartItemId}")]
        public async Task<IActionResult> UpdateCartItem(int cartItemId, [FromBody] int quantity)
        {
            _logger.LogInformation("UpdateCartItem called for cartItem {CartItemId}, quantity {Quantity}", cartItemId, quantity);

            var customerId = GetCustomerId();
            if (customerId == null)
            {
                _logger.LogWarning("Invalid customerId from token");
                return Unauthorized(new { message = "Token không hợp lệ." });
            }

            if (quantity <= 0)
            {
                _logger.LogWarning("Invalid quantity: {Quantity}", quantity);
                return BadRequest(new { message = "Số lượng phải lớn hơn 0." });
            }

            var cart = await _context.Carts
                .Include(c => c.Items)
                .FirstOrDefaultAsync(c => c.CustomerId == customerId);

            if (cart == null)
            {
                _logger.LogWarning("Cart not found for customer {CustomerId}", customerId);
                return NotFound(new { message = "Giỏ hàng không tồn tại." });
            }

            var cartItem = cart.Items.FirstOrDefault(i => i.CartItemId == cartItemId);
            if (cartItem == null)
            {
                _logger.LogWarning("Cart item not found: {CartItemId}", cartItemId);
                return NotFound(new { message = "Mục giỏ hàng không tồn tại." });
            }

            var inventory = await _context.Inventories.FirstOrDefaultAsync(i => i.ProductId == cartItem.ProductId);
            if (inventory == null || inventory.StockQuantity < quantity)
            {
                _logger.LogWarning("Insufficient stock for product {ProductId}", cartItem.ProductId);
                return BadRequest(new { message = "Số lượng tồn kho không đủ." });
            }

            cartItem.Quantity = quantity;
            cart.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            _logger.LogInformation("Updated cart item {CartItemId} in cart {CartId}", cartItemId, cart.CartId);
            return Ok(new { message = "Cập nhật giỏ hàng thành công." });
        }

        [HttpDelete("remove/{cartItemId}")]
        public async Task<IActionResult> RemoveFromCart(int cartItemId)
        {
            _logger.LogInformation("RemoveFromCart called for cartItem {CartItemId}", cartItemId);

            var customerId = GetCustomerId();
            if (customerId == null)
            {
                _logger.LogWarning("Invalid customerId from token");
                return Unauthorized(new { message = "Token không hợp lệ." });
            }

            var cart = await _context.Carts
                .Include(c => c.Items)
                .FirstOrDefaultAsync(c => c.CustomerId == customerId);

            if (cart == null)
            {
                _logger.LogWarning("Cart not found for customer {CustomerId}", customerId);
                return NotFound(new { message = "Giỏ hàng không tồn tại." });
            }

            var cartItem = cart.Items.FirstOrDefault(i => i.CartItemId == cartItemId);
            if (cartItem == null)
            {
                _logger.LogWarning("Cart item not found: {CartItemId}", cartItemId);
                return NotFound(new { message = "Mục giỏ hàng không tồn tại." });
            }

            _context.CartItems.Remove(cartItem);
            cart.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            _logger.LogInformation("Removed cart item {CartItemId} from cart {CartId}", cartItemId, cart.CartId);
            return Ok(new { message = "Xóa sản phẩm khỏi giỏ hàng thành công." });
        }

        private int? GetCustomerId()
        {
            var customerIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            return int.TryParse(customerIdClaim, out int customerId) ? customerId : null;
        }
    }
}