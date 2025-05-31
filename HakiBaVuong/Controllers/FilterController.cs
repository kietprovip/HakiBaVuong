using HakiBaVuong.Data;
using HakiBaVuong.DTOs;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace HakiBaVuong.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class FilterController : ControllerBase
    {
        private readonly DataContext _context;
        private readonly ILogger<FilterController> _logger;

        public FilterController(DataContext context, ILogger<FilterController> logger)
        {
            _context = context;
            _logger = logger;
        }

        [HttpGet("categories")]
        public async Task<ActionResult<IEnumerable<CategoryDTO>>> GetCategories()
        {
            _logger.LogInformation("GetCategories for filter called");

            var categories = await _context.Categories
                .Select(c => new CategoryDTO
                {
                    CategoryId = c.CategoryId,
                    Name = c.Name,
                    Description = c.Description
                })
                .ToListAsync();

            _logger.LogInformation("Retrieved {Count} categories for filter", categories.Count);
            return Ok(categories);
        }

        [HttpGet("brands")]
        public async Task<ActionResult<IEnumerable<BrandDTO>>> GetBrands()
        {
            _logger.LogInformation("GetBrands for filter called");

            var brands = await _context.Brands
                .Select(b => new BrandDTO
                {
                    Name = b.Name,
                    OwnerId = b.OwnerId,
                    BackgroundColor = b.BackgroundColor,
                    BackgroundImageUrl = b.BackgroundImageUrl
                })
                .ToListAsync();

            _logger.LogInformation("Retrieved {Count} brands for filter", brands.Count);
            return Ok(brands);
        }

        [HttpGet("price-range")]
        public async Task<ActionResult<object>> GetPriceRange()
        {
            _logger.LogInformation("GetPriceRange for filter called");

            var minPrice = await _context.Products
                .MinAsync(p => (decimal?)p.PriceSell) ?? 0;
            var maxPrice = await _context.Products
                .MaxAsync(p => (decimal?)p.PriceSell) ?? 0;

            _logger.LogInformation("Price range retrieved: Min {MinPrice}, Max {MaxPrice}", minPrice, maxPrice);
            return Ok(new { minPrice, maxPrice });
        }
    }
}