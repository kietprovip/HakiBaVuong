using HakiBaVuong.DTOs;
using HakiBaVuong.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using HakiBaVuong.Data;
using System.Security.Claims;

namespace HakiBaVuong.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize(Roles = "Admin,Staff")]
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

            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userIdClaim) || !int.TryParse(userIdClaim, out int userId))
            {
                _logger.LogWarning("Cannot determine userId from token.");
                return BadRequest(new { message = "Không thể xác định userId từ token." });
            }

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

            var product = await _context.Products.FindAsync(productId);
            if (product == null)
            {
                _logger.LogWarning("Product not found: {ProductId}", productId);
                return NotFound(new { message = "Sản phẩm không tồn tại." });
            }

            var brand = await _context.Brands.FindAsync(product.BrandId);
            if (brand == null)
            {
                _logger.LogWarning("Brand not found for product {ProductId}", productId);
                return BadRequest(new { message = "Brand không tồn tại." });
            }

            if (User.IsInRole("Staff") && brand.OwnerId != userId)
            {
                var hasPermission = await _context.StaffPermissions
                    .AnyAsync(sp => sp.StaffId == userId && sp.Permission.Name == "UpdateInventory");
                if (!hasPermission)
                {
                    _logger.LogWarning("Staff user {UserId} does not have UpdateInventory permission for brand {BrandId}", userId, product.BrandId);
                    return Forbid();
                }
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