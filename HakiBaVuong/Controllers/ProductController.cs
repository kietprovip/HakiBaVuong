using HakiBaVuong.DTOs;
using HakiBaVuong.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using HakiBaVuong.Data;
using System.Security.Claims;
using Microsoft.AspNetCore.Http;

namespace HakiBaVuong.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class ProductController : ControllerBase
    {
        private readonly DataContext _context;
        private readonly ILogger<ProductController> _logger;
        private readonly IWebHostEnvironment _environment;

        public ProductController(DataContext context, ILogger<ProductController> logger, IWebHostEnvironment environment)
        {
            _context = context;
            _logger = logger;
            _environment = environment;
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

        [HttpGet("staff")]
        [Authorize(Roles = "Admin,Staff")]
        public async Task<ActionResult<IEnumerable<Product>>> GetAllForStaff()
        {
            try
            {
                _logger.LogInformation("GetAllForStaff products called");

                var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userIdClaim))
                {
                    _logger.LogWarning("UserId claim is missing in token.");
                    return Unauthorized(new { message = "Token không hợp lệ: Thiếu userId." });
                }

                if (!int.TryParse(userIdClaim, out int userId))
                {
                    _logger.LogWarning("Invalid userId format in token: {UserIdClaim}", userIdClaim);
                    return BadRequest(new { message = "UserId trong token không hợp lệ." });
                }

                if (User.IsInRole("Admin"))
                {
                    try
                    {
                        var allProducts = await _context.Products
                            .Include(p => p.Brand)
                            .ToListAsync();
                        _logger.LogInformation("Admin retrieved {Count} products", allProducts.Count);
                        return Ok(allProducts);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error occurred while fetching all products for Admin.");
                        return StatusCode(500, new { message = "Lỗi khi lấy danh sách sản phẩm cho Admin. Vui lòng thử lại sau." });
                    }
                }

                List<int> brandIds;
                try
                {
                    brandIds = await _context.Brands
                        .Where(b => b.OwnerId == userId)
                        .Select(b => b.BrandId)
                        .ToListAsync();
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error occurred while fetching brands for staff user {UserId}", userId);
                    return StatusCode(500, new { message = "Lỗi khi lấy danh sách thương hiệu. Vui lòng thử lại sau." });
                }

                if (!brandIds.Any())
                {
                    _logger.LogInformation("No brands found for staff user {UserId}", userId);
                    return Ok(new List<Product>());
                }

                try
                {
                    var products = await _context.Products
                        .Where(p => brandIds.Contains(p.BrandId))
                        .Include(p => p.Brand)
                        .ToListAsync();

                    _logger.LogInformation("Retrieved {Count} products for staff user {UserId}", products.Count, userId);
                    return Ok(products);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error occurred while fetching products for staff user {UserId}", userId);
                    return StatusCode(500, new { message = "Lỗi khi lấy danh sách sản phẩm cho Staff. Vui lòng thử lại sau." });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error in GetAllForStaff.");
                return StatusCode(500, new { message = "Đã xảy ra lỗi không xác định. Vui lòng thử lại sau." });
            }
        }

        [HttpGet("{id}")]
        [Authorize(Roles = "Admin,Staff")]
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

            if (User.IsInRole("Staff"))
            {
                var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userIdClaim) || !int.TryParse(userIdClaim, out int userId))
                {
                    _logger.LogWarning("Cannot determine userId from token.");
                    return BadRequest(new { message = "Không thể xác định userId từ token." });
                }

                var brand = await _context.Brands.FindAsync(product.BrandId);
                if (brand == null || brand.OwnerId != userId)
                {
                    _logger.LogWarning("Staff user {UserId} does not have access to product {ProductId}", userId, id);
                    return Forbid();
                }
            }

            return Ok(product);
        }

        [HttpGet("brand/{brandId}")]
        [Authorize(Roles = "Admin,Staff")]
        public async Task<ActionResult<IEnumerable<Product>>> GetByBrandId(int brandId)
        {
            _logger.LogInformation("GetByBrandId called for brand ID: {BrandId}", brandId);

            var brand = await _context.Brands.FindAsync(brandId);
            if (brand == null)
            {
                _logger.LogWarning("Brand not found: {BrandId}", brandId);
                return BadRequest(new { message = "Brand không tồn tại." });
            }

            if (User.IsInRole("Staff"))
            {
                var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userIdClaim) || !int.TryParse(userIdClaim, out int userId))
                {
                    _logger.LogWarning("Cannot determine userId from token.");
                    return BadRequest(new { message = "Không thể xác định userId từ token." });
                }

                if (brand.OwnerId != userId)
                {
                    _logger.LogWarning("Staff user {UserId} does not have access to brand {BrandId}", userId, brandId);
                    return Forbid();
                }
            }

            var products = await _context.Products
                .Where(p => p.BrandId == brandId)
                .Include(p => p.Brand)
                .ToListAsync();
            _logger.LogInformation("Retrieved {Count} products for brand ID: {BrandId}", products.Count, brandId);
            return Ok(products);
        }

        [HttpPost]
        [Authorize(Roles = "Admin,Staff")]
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

            if (User.IsInRole("Staff"))
            {
                var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userIdClaim) || !int.TryParse(userIdClaim, out int userId))
                {
                    _logger.LogWarning("Cannot determine userId from token.");
                    return BadRequest(new { message = "Không thể xác định userId từ token." });
                }

                if (brand.OwnerId != userId)
                {
                    _logger.LogWarning("Staff user {UserId} does not have access to brand {BrandId}", userId, productDto.BrandId);
                    return Forbid();
                }
            }

            var product = new Product
            {
                BrandId = productDto.BrandId,
                Name = productDto.Name,
                Description = productDto.Description,
                PriceSell = productDto.PriceSell,
                PriceCost = productDto.PriceCost,
                Image = productDto.Image,
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
        [Authorize(Roles = "Admin,Staff")]
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

            if (User.IsInRole("Staff"))
            {
                var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userIdClaim) || !int.TryParse(userIdClaim, out int userId))
                {
                    _logger.LogWarning("Cannot determine userId from token.");
                    return BadRequest(new { message = "Không thể xác định userId từ token." });
                }

                if (brand.OwnerId != userId)
                {
                    _logger.LogWarning("Staff user {UserId} does not have access to brand {BrandId}", userId, productDto.BrandId);
                    return Forbid();
                }
            }

            product.Name = productDto.Name;
            product.Description = productDto.Description;
            product.PriceSell = productDto.PriceSell;
            product.PriceCost = productDto.PriceCost;
            product.BrandId = productDto.BrandId;
            product.Image = productDto.Image;
            product.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();
            _logger.LogInformation("Updated product ID: {Id}", id);
            return NoContent();
        }

        [HttpDelete("{id}")]
        [Authorize(Roles = "Admin,Staff")]
        public async Task<IActionResult> Delete(int id)
        {
            _logger.LogInformation("Delete product called for ID: {Id}", id);

            var product = await _context.Products.FindAsync(id);
            if (product == null)
            {
                _logger.LogWarning("Product not found: {Id}", id);
                return NotFound(new { message = "Sản phẩm không tồn tại." });
            }

            if (User.IsInRole("Staff"))
            {
                var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userIdClaim) || !int.TryParse(userIdClaim, out int userId))
                {
                    _logger.LogWarning("Cannot determine userId from token.");
                    return BadRequest(new { message = "Không thể xác định userId từ token." });
                }

                var brand = await _context.Brands.FindAsync(product.BrandId);
                if (brand == null || brand.OwnerId != userId)
                {
                    _logger.LogWarning("Staff user {UserId} does not have access to product {ProductId}", userId, id);
                    return Forbid();
                }
            }

            var inventory = await _context.Inventories.FirstOrDefaultAsync(i => i.ProductId == id);
            if (inventory != null)
            {
                _context.Inventories.Remove(inventory);
            }


            if (!string.IsNullOrEmpty(product.Image))
            {
                var imagePath = Path.Combine(_environment.ContentRootPath, product.Image.TrimStart('/'));
                if (System.IO.File.Exists(imagePath))
                {
                    System.IO.File.Delete(imagePath);
                    _logger.LogInformation("Deleted image for product ID: {Id}", id);
                }
            }

            _context.Products.Remove(product);
            await _context.SaveChangesAsync();
            _logger.LogInformation("Deleted product ID: {Id}", id);
            return NoContent();
        }

        [HttpPost("{id}/upload-image")]
        [Authorize(Roles = "Admin,Staff")]
        public async Task<IActionResult> UploadImage(int id, IFormFile file)
        {
            _logger.LogInformation("UploadImage called for product ID: {Id}", id);

            var product = await _context.Products.FindAsync(id);
            if (product == null)
            {
                _logger.LogWarning("Product not found: {Id}", id);
                return NotFound(new { message = "Sản phẩm không tồn tại." });
            }

            if (User.IsInRole("Staff"))
            {
                var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userIdClaim) || !int.TryParse(userIdClaim, out int userId))
                {
                    _logger.LogWarning("Cannot determine userId from token.");
                    return BadRequest(new { message = "Không thể xác định userId từ token." });
                }

                var brand = await _context.Brands.FindAsync(product.BrandId);
                if (brand == null || brand.OwnerId != userId)
                {
                    _logger.LogWarning("Staff user {UserId} does not have access to product {ProductId}", userId, id);
                    return Forbid();
                }
            }

            if (file == null || file.Length == 0)
            {
                _logger.LogWarning("No file uploaded for product ID: {Id}", id);
                return BadRequest(new { message = "Vui lòng cung cấp file ảnh." });
            }

            var allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".gif" };
            var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
            if (!allowedExtensions.Contains(extension))
            {
                _logger.LogWarning("Invalid file extension {Extension} for product ID: {Id}", extension, id);
                return BadRequest(new { message = "Định dạng file không hợp lệ. Chỉ hỗ trợ .jpg, .jpeg, .png, .gif." });
            }

            var maxFileSize = 5 * 1024 * 1024;
            if (file.Length > maxFileSize)
            {
                _logger.LogWarning("File size {Size} exceeds limit for product ID: {Id}", file.Length, id);
                return BadRequest(new { message = "Kích thước file vượt quá 5MB." });
            }

            var uploadsFolder = Path.Combine(_environment.ContentRootPath, "Images");
            if (!Directory.Exists(uploadsFolder))
            {
                Directory.CreateDirectory(uploadsFolder);
            }

            var fileName = $"product_{id}{extension}";
            var filePath = Path.Combine(uploadsFolder, fileName);


            if (!string.IsNullOrEmpty(product.Image))
            {
                var oldImagePath = Path.Combine(_environment.ContentRootPath, product.Image.TrimStart('/'));
                if (System.IO.File.Exists(oldImagePath))
                {
                    System.IO.File.Delete(oldImagePath);
                    _logger.LogInformation("Deleted old image for product ID: {Id}", id);
                }
            }

            using (var stream = new FileStream(filePath, FileMode.Create))
            {
                await file.CopyToAsync(stream);
            }

            product.Image = $"Images/{fileName}";
            product.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            _logger.LogInformation("Uploaded image for product ID: {Id}, path: {ImagePath}", id, product.Image);
            return Ok(new { message = "Tải ảnh lên thành công.", imageUrl = $"/images/{fileName}" });
        }
    }
}