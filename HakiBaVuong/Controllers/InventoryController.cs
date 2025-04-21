using HakiBaVuong.DTOs;
using HakiBaVuong.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using HakiBaVuong.Data;

namespace HakiBaVuong.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class InventoryController : ControllerBase
    {
        private readonly DataContext _context;
        private readonly ILogger<InventoryController> _logger;

        public InventoryController(DataContext context, ILogger<InventoryController> logger)
        {
            _context = context;
            _logger = logger;
        }

        [HttpGet("{productId}")]
        public async Task<ActionResult<Inventory>> GetByProductId(int productId)
        {
            _logger.LogInformation("GetByProductId called for product ID: {ProductId}", productId);

            var inventory = await _context.Inventories
                .Include(i => i.Product)
                .FirstOrDefaultAsync(i => i.ProductId == productId);
            if (inventory == null)
            {
                _logger.LogWarning("Inventory not found for product ID: {ProductId}", productId);
                return NotFound(new { message = "Không tìm thấy thông tin tồn kho." });
            }

            return Ok(inventory);
        }

        [HttpPut("{productId}")]
        public async Task<IActionResult> Update(int productId, InventoryDTO inventoryDto)
        {
            _logger.LogInformation("Update inventory called for product ID: {ProductId}", productId);

            if (productId != inventoryDto.ProductId)
            {
                _logger.LogWarning("Product ID mismatch: {ProductId} vs {DtoProductId}", productId, inventoryDto.ProductId);
                return BadRequest(new { message = "ProductId không khớp." });
            }

            var inventory = await _context.Inventories.FirstOrDefaultAsync(i => i.ProductId == productId);
            if (inventory == null)
            {
                _logger.LogWarning("Inventory not found for product ID: {ProductId}", productId);
                return NotFound(new { message = "Không tìm thấy thông tin tồn kho." });
            }

            if (inventoryDto.StockQuantity < 0)
            {
                _logger.LogWarning("Invalid StockQuantity: {StockQuantity}", inventoryDto.StockQuantity);
                return BadRequest(new { message = "Số lượng tồn kho không thể nhỏ hơn 0." });
            }

            inventory.StockQuantity = inventoryDto.StockQuantity;
            inventory.LastUpdated = DateTime.UtcNow;

            await _context.SaveChangesAsync();
            _logger.LogInformation("Updated inventory for product ID: {ProductId}, new quantity: {StockQuantity}", productId, inventory.StockQuantity);
            return NoContent();
        }
    }
}