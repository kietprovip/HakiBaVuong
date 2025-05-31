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
    public class ViewedProductController : ControllerBase
    {
        private readonly DataContext _context;

        public ViewedProductController(DataContext context)
        {
            _context = context;
        }

        [HttpPost("view/{productId}")]
        [Authorize]
        public async Task<IActionResult> MarkProductAsViewed(int productId)
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

            var existingView = await _context.ViewedProducts
                .FirstOrDefaultAsync(vp => vp.CustomerId == customerId && vp.ProductId == productId);

            if (existingView != null)
            {
                existingView.ViewedAt = DateTime.UtcNow;
                _context.ViewedProducts.Update(existingView);
            }
            else
            {
                var viewedProduct = new ViewedProduct
                {
                    CustomerId = customerId,
                    ProductId = productId,
                    ViewedAt = DateTime.UtcNow
                };
                _context.ViewedProducts.Add(viewedProduct);
            }

            await _context.SaveChangesAsync();
            return Ok(new { message = "Đã cập nhật trạng thái xem sản phẩm." });
        }

        [HttpGet("has-viewed/{productId}")]
        [Authorize]
        public async Task<ActionResult<bool>> HasViewedProduct(int productId)
        {
            var customerIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (!int.TryParse(customerIdClaim, out int customerId))
            {
                return Unauthorized(new { message = "Token không hợp lệ." });
            }

            var hasViewed = await _context.ViewedProducts
                .AnyAsync(vp => vp.CustomerId == customerId && vp.ProductId == productId);

            return Ok(hasViewed);
        }
    }
}