using HakiBaVuong.Data;
using HakiBaVuong.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace HakiBaVuong.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class WishlistController : ControllerBase
    {
        private readonly DataContext _context;

        public WishlistController(DataContext context)
        {
            _context = context;
        }

        [HttpPost("toggle/{productId}")]
        [Authorize]
        public async Task<IActionResult> ToggleWishlist(int productId)
        {
            var customerIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (!int.TryParse(customerIdClaim, out int customerId))
            {
                return Unauthorized(new { message = "Token không hợp lệ." });
            }

            var product = await _context.Products.FindAsync(productId);
            if (product == null)
            {
                return NotFound(new { message = "Sản phẩm không tồn tại." });
            }

            var existingWishlist = await _context.Wishlists
                .FirstOrDefaultAsync(w => w.CustomerId == customerId && w.ProductId == productId);

            if (existingWishlist != null)
            {
                _context.Wishlists.Remove(existingWishlist);
                await _context.SaveChangesAsync();
                return Ok(new { isLiked = false });
            }
            else
            {
                var wishlist = new Wishlist
                {
                    CustomerId = customerId,
                    ProductId = productId,
                    AddedAt = DateTime.UtcNow
                };
                _context.Wishlists.Add(wishlist);
                await _context.SaveChangesAsync();
                return Ok(new { isLiked = true });
            }
        }

        [HttpGet("has-liked/{productId}")]
        [Authorize]
        public async Task<ActionResult<bool>> HasLikedProduct(int productId)
        {
            var customerIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (!int.TryParse(customerIdClaim, out int customerId))
            {
                return Unauthorized(new { message = "Token không hợp lệ." });
            }

            var hasLiked = await _context.Wishlists
                .AnyAsync(w => w.CustomerId == customerId && w.ProductId == productId);

            return Ok(hasLiked);
        }

        [HttpGet("list")]
        [Authorize]
        public async Task<IActionResult> GetWishlist()
        {
            var customerIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (!int.TryParse(customerIdClaim, out int customerId))
            {
                return Unauthorized(new { message = "Token không hợp lệ." });
            }

            var wishlist = await _context.Wishlists
                .Where(w => w.CustomerId == customerId)
                .Include(w => w.Product)
                .ThenInclude(p => p.Brand)
                .Select(w => new
                {
                    w.Product.ProductId,
                    w.Product.Name,
                    w.Product.Description,
                    w.Product.PriceSell,
                    w.Product.Rating,
                    w.Product.Image,
                    Brand = new
                    {
                        w.Product.Brand.BrandId,
                        w.Product.Brand.Name
                    }
                })
                .ToListAsync();

            return Ok(wishlist);
        }
    }
}