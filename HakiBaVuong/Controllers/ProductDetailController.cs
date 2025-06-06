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

        [HttpPost("{id}/comments")]
        [Authorize]
        public async Task<ActionResult<CommentDTO>> PostComment(int id, [FromBody] CreateCommentDTO model)
        {
            _logger.LogInformation("PostComment called for product ID: {Id}", id);

            var customerIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (!int.TryParse(customerIdClaim, out int customerId))
            {
                _logger.LogWarning("Invalid customerId from token");
                return Unauthorized(new { message = "Token không hợp lệ." });
            }

            var product = await _context.Products.FindAsync(id);
            if (product == null)
            {
                _logger.LogWarning("Product not found: {Id}", id);
                return NotFound(new { message = "Sản phẩm không tồn tại." });
            }

            if (string.IsNullOrWhiteSpace(model.Content) || model.Rating < 1 || model.Rating > 5)
            {
                _logger.LogWarning("Invalid comment data: Content or Rating is invalid");
                return BadRequest(new { message = "Nội dung bình luận không được để trống và đánh giá phải từ 1 đến 5." });
            }

            var comment = new Comment
            {
                ProductId = id,
                CustomerId = customerId,
                Content = model.Content,
                Rating = model.Rating,
                CreatedAt = DateTime.UtcNow
            };

            _context.Comments.Add(comment);
            await _context.SaveChangesAsync();

            var averageRating = await _context.Comments
                .Where(c => c.ProductId == id)
                .AverageAsync(c => (double?)c.Rating);
            product.Rating = averageRating.HasValue ? (float)Math.Round(averageRating.Value, 1) : 5.0f;
            _context.Products.Update(product);
            await _context.SaveChangesAsync();

            var commentDto = new CommentDTO
            {
                CommentId = comment.CommentId,
                ProductId = comment.ProductId,
                CustomerId = comment.CustomerId,
                Content = comment.Content,
                Rating = comment.Rating,
                CreatedAt = comment.CreatedAt,
                CustomerName = (await _context.Customers.FindAsync(customerId)).Name
            };

            _logger.LogInformation("Comment created for product {ProductId} by customer {CustomerId}", id, customerId);
            return CreatedAtAction(nameof(GetProductComments), new { id = comment.ProductId }, commentDto);
        }

        [HttpGet("brand-related/{brandId}")]
        public async Task<ActionResult<IEnumerable<ProductDTO>>> GetBrandRelatedProducts(int brandId)
        {
            _logger.LogInformation("GetBrandRelatedProducts called for brand ID: {BrandId}", brandId);

            var brand = await _context.Brands.FindAsync(brandId);
            if (brand == null)
            {
                _logger.LogWarning("Brand not found: {BrandId}", brandId);
                return NotFound(new { message = "Thương hiệu không tồn tại." });
            }

            var relatedProducts = await _context.Products
                .Include(p => p.Brand)
                .Include(p => p.ProductCategories)
                .Where(p => p.BrandId == brandId)
                .OrderByDescending(p => p.Rating)
                .Take(4)
                .Select(p => new ProductDTO
                {
                    ProductId = p.ProductId,
                    BrandId = p.BrandId,
                    Name = p.Name,
                    Description = p.Description,
                    PriceSell = p.PriceSell,
                    PriceCost = p.PriceCost,
                    Rating = p.Rating,
                    CreatedAt = p.CreatedAt,
                    UpdatedAt = p.UpdatedAt,
                    Image = p.Image,
                    CategoryIds = p.ProductCategories.Select(pc => pc.CategoryId).ToList()
                })
                .ToListAsync();

            _logger.LogInformation("Retrieved {Count} related products for brand ID: {BrandId}", relatedProducts.Count, brandId);
            return Ok(relatedProducts);
        }
    }
}