using HakiBaVuong.DTOs;
using HakiBaVuong.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using HakiBaVuong.Data;
using System.Security.Claims;
using Microsoft.Extensions.Logging;

namespace HakiBaVuong.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class ProductDetailController : ControllerBase
    {
        private readonly DataContext _context;
        private readonly ILogger<ProductDetailController> _logger;

        public ProductDetailController(DataContext context, ILogger<ProductDetailController> logger)
        {
            _context = context;
            _logger = logger;
        }

        [HttpGet("{id}")]
        [Authorize]
        public async Task<ActionResult<ProductDTO>> GetProductDetail(int id)
        {
            _logger.LogInformation("GetProductDetail called for product ID: {Id}", id);

            var product = await _context.Products
                .Include(p => p.Brand)
                .FirstOrDefaultAsync(p => p.ProductId == id);
            if (product == null)
            {
                _logger.LogWarning("Product not found: {Id}", id);
                return NotFound(new { message = "Sản phẩm không tồn tại." });
            }

            return Ok(product);
        }

        [HttpGet("{id}/rating")]
        [Authorize]
        public async Task<ActionResult<float>> GetProductRating(int id)
        {
            _logger.LogInformation("GetProductRating called for product ID: {Id}", id);

            var product = await _context.Products
                .FirstOrDefaultAsync(p => p.ProductId == id);

            if (product == null)
            {
                _logger.LogWarning("Product not found: {Id}", id);
                return NotFound(new { message = "Sản phẩm không tồn tại." });
            }

            _logger.LogInformation("Retrieved rating {Rating} for product ID: {Id}", product.Rating, id);
            return Ok(product.Rating);
        }

        [HttpGet("{id}/comments")]
        [Authorize]
        public async Task<ActionResult<IEnumerable<CommentDTO>>> GetProductComments(int id)
        {
            _logger.LogInformation("GetProductComments called for product ID: {Id}", id);

            var product = await _context.Products.FindAsync(id);
            if (product == null)
            {
                _logger.LogWarning("Product not found: {Id}", id);
                return NotFound(new { message = "Sản phẩm không tồn tại." });
            }

            var comments = await _context.Comments
                .Where(c => c.ProductId == id)
                .Include(c => c.Customer)
                .OrderByDescending(c => c.CreatedAt)
                .Select(c => new CommentDTO
                {
                    CommentId = c.CommentId,
                    ProductId = c.ProductId,
                    CustomerId = c.CustomerId,
                    Content = c.Content,
                    Rating = c.Rating,
                    CreatedAt = c.CreatedAt,
                    CustomerName = c.Customer.Name
                })
                .ToListAsync();

            _logger.LogInformation("Retrieved {Count} comments for product ID: {Id}", comments.Count, id);
            return Ok(comments);
        }
    }
}