using HakiBaVuong.DTOs;
using HakiBaVuong.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using HakiBaVuong.Data;

namespace HakiBaVuong.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class ProductController : ControllerBase
    {
        private readonly DataContext _context;
        private readonly ILogger<ProductController> _logger;

        public ProductController(DataContext context, ILogger<ProductController> logger)
        {
            _context = context;
            _logger = logger;
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<Product>>> GetAll([FromQuery] int? brandId)
        {
            _logger.LogInformation("GetAll products called with brandId: {BrandId}", brandId);

            var query = _context.Products.AsQueryable();
            if (brandId.HasValue)
            {
                query = query.Where(p => p.BrandId == brandId.Value);
            }

            var products = await query.Include(p => p.Brand).ToListAsync();
            _logger.LogInformation("Retrieved {Count} products", products.Count);
            return Ok(products);
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<Product>> GetById(int id)
        {
            _logger.LogInformation("GetById called for product ID: {Id}", id);

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

        [HttpGet("brand/{brandId}")]
        public async Task<ActionResult<IEnumerable<Product>>> GetByBrandId(int brandId)
        {
            _logger.LogInformation("GetByBrandId called for brand ID: {BrandId}", brandId);

            var brand = await _context.Brands.FindAsync(brandId);
            if (brand == null)
            {
                _logger.LogWarning("Brand not found: {BrandId}", brandId);
                return BadRequest(new { message = "Brand không tồn tại." });
            }

            var products = await _context.Products
                .Where(p => p.BrandId == brandId)
                .Include(p => p.Brand)
                .ToListAsync();
            _logger.LogInformation("Retrieved {Count} products for brand ID: {BrandId}", products.Count, brandId);
            return Ok(products);
        }

        [HttpPost]
        public async Task<ActionResult<Product>> Create(ProductDTO productDto)
        {
            _logger.LogInformation("Create product called with name: {Name}, brandId: {BrandId}", productDto.Name, productDto.BrandId);

            if (productDto.PriceSell <= 0)
            {
                _logger.LogWarning("Invalid PriceSell: {PriceSell}", productDto.PriceSell);
                return BadRequest(new { message = "Giá bán phải lớn hơn 0." });
            }

            var brand = await _context.Brands.FindAsync(productDto.BrandId);
            if (brand == null)
            {
                _logger.LogWarning("Brand not found: {BrandId}", productDto.BrandId);
                return BadRequest(new { message = "Brand không tồn tại." });
            }

            var product = new Product
            {
                BrandId = productDto.BrandId,
                Name = productDto.Name,
                Description = productDto.Description,
                PriceSell = productDto.PriceSell,
                PriceCost = productDto.PriceCost,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            _context.Products.Add(product);
            await _context.SaveChangesAsync();


            var inventory = new Inventory
            {
                ProductId = product.ProductId,
                StockQuantity = 0,
                LastUpdated = DateTime.UtcNow
            };
            _context.Inventories.Add(inventory);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Created product ID: {ProductId} with inventory ID: {InventoryId}", product.ProductId, inventory.InventoryId);
            return CreatedAtAction(nameof(GetById), new { id = product.ProductId }, product);
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> Update(int id, ProductDTO productDto)
        {
            _logger.LogInformation("Update product called for ID: {Id}", id);

            var product = await _context.Products.FindAsync(id);
            if (product == null)
            {
                _logger.LogWarning("Product not found: {Id}", id);
                return NotFound(new { message = "Sản phẩm không tồn tại." });
            }

            if (productDto.PriceSell <= 0)
            {
                _logger.LogWarning("Invalid PriceSell: {PriceSell}", productDto.PriceSell);
                return BadRequest(new { message = "Giá bán phải lớn hơn 0." });
            }

            var brand = await _context.Brands.FindAsync(productDto.BrandId);
            if (brand == null)
            {
                _logger.LogWarning("Brand not found: {BrandId}", productDto.BrandId);
                return BadRequest(new { message = "Brand không tồn tại." });
            }

            product.Name = productDto.Name;
            product.Description = productDto.Description;
            product.PriceSell = productDto.PriceSell;
            product.PriceCost = productDto.PriceCost;
            product.BrandId = productDto.BrandId;
            product.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();
            _logger.LogInformation("Updated product ID: {Id}", id);
            return NoContent();
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(int id)
        {
            _logger.LogInformation("Delete product called for ID: {Id}", id);

            var product = await _context.Products.FindAsync(id);
            if (product == null)
            {
                _logger.LogWarning("Product not found: {Id}", id);
                return NotFound(new { message = "Sản phẩm không tồn tại." });
            }

 
            var inventory = await _context.Inventories.FirstOrDefaultAsync(i => i.ProductId == id);
            if (inventory != null)
            {
                _context.Inventories.Remove(inventory);
            }

            _context.Products.Remove(product);
            await _context.SaveChangesAsync();
            _logger.LogInformation("Deleted product ID: {Id}", id);
            return NoContent();
        }
    }
}