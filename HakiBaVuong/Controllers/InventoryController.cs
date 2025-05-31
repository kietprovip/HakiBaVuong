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
    [Authorize(Roles = "Admin,Staff,InventoryManager")] 
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

            // Kiểm tra quyền truy cập
            var userId = GetUserId();
            if (!userId.HasValue)
            {
                _logger.LogWarning("Invalid userId from token");
                return Unauthorized(new { message = "Token không hợp lệ." });
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
                _logger.LogWarning("Brand not found for product ID: {ProductId}", productId);
                return NotFound(new { message = "Thương hiệu không tồn tại." });
            }

            if (User.IsInRole("Staff") || User.IsInRole("InventoryManager"))
            {
                var user = await _context.Users.FindAsync(userId.Value);
                if (user == null)
                {
                    _logger.LogWarning("User not found: {UserId}", userId);
                    return NotFound(new { message = "Người dùng không tồn tại." });
                }

                int effectiveOwnerId;
                if (user.BrandId.HasValue)
                {
                    var userBrand = await _context.Brands.FindAsync(user.BrandId.Value);
                    if (userBrand == null)
                    {
                        _logger.LogWarning("Brand not found for BrandId: {BrandId}", user.BrandId);
                        return NotFound(new { message = "Thương hiệu không tồn tại." });
                    }
                    effectiveOwnerId = userBrand.OwnerId;
                }
                else
                {
                    effectiveOwnerId = userId.Value;
                }

                if (brand.OwnerId != effectiveOwnerId)
                {
                    _logger.LogWarning("User {UserId} does not have access to brand {BrandId}", userId, brand.BrandId);
                    return Forbid();
                }
            }

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

            // Kiểm tra quyền truy cập
            var userId = GetUserId();
            if (!userId.HasValue)
            {
                _logger.LogWarning("Invalid userId from token");
                return Unauthorized(new { message = "Token không hợp lệ." });
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
                _logger.LogWarning("Brand not found for product ID: {ProductId}", productId);
                return NotFound(new { message = "Thương hiệu không tồn tại." });
            }

            if (User.IsInRole("Staff") || User.IsInRole("InventoryManager"))
            {
                var user = await _context.Users.FindAsync(userId.Value);
                if (user == null)
                {
                    _logger.LogWarning("User not found: {UserId}", userId);
                    return NotFound(new { message = "Người dùng không tồn tại." });
                }

                int effectiveOwnerId;
                if (user.BrandId.HasValue)
                {
                    var userBrand = await _context.Brands.FindAsync(user.BrandId.Value);
                    if (userBrand == null)
                    {
                        _logger.LogWarning("Brand not found for BrandId: {BrandId}", user.BrandId);
                        return NotFound(new { message = "Thương hiệu không tồn tại." });
                    }
                    effectiveOwnerId = userBrand.OwnerId;
                }
                else
                {
                    effectiveOwnerId = userId.Value;
                }

                if (brand.OwnerId != effectiveOwnerId)
                {
                    _logger.LogWarning("User {UserId} does not have access to brand {BrandId}", userId, brand.BrandId);
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

        private int? GetUserId()
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            return int.TryParse(userIdClaim, out int userId) ? userId : null;
        }
    }
}