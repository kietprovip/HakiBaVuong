using HakiBaVuong.Data;
using HakiBaVuong.DTOs;
using HakiBaVuong.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Threading.Tasks;

namespace HakiBaVuong.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class CategoryController : ControllerBase
    {
        private readonly DataContext _context;
        private readonly ILogger<CategoryController> _logger;

        public CategoryController(DataContext context, ILogger<CategoryController> logger)
        {
            _context = context;
            _logger = logger;
        }

        [HttpGet]
        [Authorize(Roles = "Admin,Staff,InventoryManager")] 
        public async Task<ActionResult<IEnumerable<CategoryDTO>>> GetAll()
        {
            _logger.LogInformation("GetAll categories called");

            var categories = await _context.Categories
                .Select(c => new CategoryDTO
                {
                    CategoryId = c.CategoryId,
                    Name = c.Name,
                    Description = c.Description
                })
                .ToListAsync();

            _logger.LogInformation("Retrieved {Count} categories", categories.Count);
            return Ok(categories);
        }

        [HttpGet("{id}")]
        [Authorize(Roles = "Admin,Staff")] 
        public async Task<ActionResult<CategoryDTO>> GetById(int id)
        {
            _logger.LogInformation("GetById called for category ID: {Id}", id);

            var category = await _context.Categories
                .Select(c => new CategoryDTO
                {
                    CategoryId = c.CategoryId,
                    Name = c.Name,
                    Description = c.Description
                })
                .FirstOrDefaultAsync(c => c.CategoryId == id);

            if (category == null)
            {
                _logger.LogWarning("Category not found: {Id}", id);
                return NotFound(new { message = "Danh mục không tồn tại." });
            }

            return Ok(category);
        }

        [HttpPost]
        [Authorize(Roles = "Admin")] 
        public async Task<ActionResult<CategoryDTO>> Create([FromBody] CategoryDTO categoryDto)
        {
            _logger.LogInformation("Create category called with name: {Name}", categoryDto.Name);

            if (string.IsNullOrWhiteSpace(categoryDto.Name))
            {
                _logger.LogWarning("Invalid category name");
                return BadRequest(new { message = "Tên danh mục không được để trống." });
            }

            var category = new Category
            {
                Name = categoryDto.Name,
                Description = categoryDto.Description
            };

            _context.Categories.Add(category);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Created category ID: {CategoryId}", category.CategoryId);
            return CreatedAtAction(nameof(GetById), new { id = category.CategoryId }, categoryDto);
        }

        [HttpPut("{id}")]
        [Authorize(Roles = "Admin")] 
        public async Task<IActionResult> Update(int id, [FromBody] CategoryDTO categoryDto)
        {
            _logger.LogInformation("Update category called for ID: {Id}", id);

            var category = await _context.Categories.FindAsync(id);
            if (category == null)
            {
                _logger.LogWarning("Category not found: {Id}", id);
                return NotFound(new { message = "Danh mục không tồn tại." });
            }

            if (string.IsNullOrWhiteSpace(categoryDto.Name))
            {
                _logger.LogWarning("Invalid category name");
                return BadRequest(new { message = "Tên danh mục không được để trống." });
            }

            category.Name = categoryDto.Name;
            category.Description = categoryDto.Description;
            category.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            _logger.LogInformation("Updated category ID: {Id}", id);
            return NoContent();
        }

        [HttpDelete("{id}")]
        [Authorize(Roles = "Admin")] 
        public async Task<IActionResult> Delete(int id)
        {
            _logger.LogInformation("Delete category called for ID: {Id}", id);

            var category = await _context.Categories.FindAsync(id);
            if (category == null)
            {
                _logger.LogWarning("Category not found: {Id}", id);
                return NotFound(new { message = "Danh mục không tồn tại." });
            }

            _context.Categories.Remove(category);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Deleted category ID: {Id}", id);
            return NoContent();
        }
    }
}