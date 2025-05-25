using HakiBaVuong.DTOs;
using HakiBaVuong.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using HakiBaVuong.Data;
using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

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
        public async Task<ActionResult<IEnumerable<ProductDTO>>> GetAll([FromQuery] int? brandId, [FromQuery] List<int> categoryIds)
        {
            _logger.LogInformation("GetAll products called with brandId: {BrandId}, categoryIds: {CategoryIds}", brandId, categoryIds);

            var query = _context.Products
                .Include(p => p.Brand)
                .Include(p => p.ProductImages)
                .Include(p => p.ProductCategories)
                .ThenInclude(pc => pc.Category)
                .AsQueryable();

            if (brandId.HasValue)
            {
                query = query.Where(p => p.BrandId == brandId.Value);
            }

            if (categoryIds?.Any() == true)
            {
                query = query.Where(p => p.ProductCategories.Any(pc => categoryIds.Contains(pc.CategoryId)));
            }

            var products = await query
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
                    ProductImages = p.ProductImages.Select(pi => pi.ImageUrl).ToList(),
                    CategoryIds = p.ProductCategories.Select(pc => pc.CategoryId).ToList()
                })
                .ToListAsync();

            _logger.LogInformation("Retrieved {Count} products", products.Count);
            return Ok(products);
        }

        [HttpGet("staff")]
        [Authorize(Roles = "Admin,Staff,InventoryManager")]
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
                        return StatusCode(500, new { message = "Lỗi khi lấy danh sách sản phẩm cho Admin." });
                    }
                }

                var user = await _context.Users.FindAsync(userId);
                if (user == null)
                {
                    _logger.LogWarning("User not found: {UserId}", userId);
                    return NotFound(new { message = "Người dùng không tồn tại." });
                }

                List<int> brandIds;
                if (user.BrandId.HasValue)
                {
                    var brand = await _context.Brands.FindAsync(user.BrandId.Value);
                    if (brand == null)
                    {
                        _logger.LogWarning("Brand not found for BrandId: {BrandId}", user.BrandId);
                        return NotFound(new { message = "Thương hiệu không tồn tại." });
                    }
                    brandIds = await _context.Brands
                        .Where(b => b.OwnerId == brand.OwnerId)
                        .Select(b => b.BrandId)
                        .ToListAsync();
                }
                else
                {
                    brandIds = await _context.Brands
                        .Where(b => b.OwnerId == userId)
                        .Select(b => b.BrandId)
                        .ToListAsync();
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
                    return StatusCode(500, new { message = "Lỗi khi lấy danh sách sản phẩm cho Staff." });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error in GetAllForStaff.");
                return StatusCode(500, new { message = "Đã xảy ra lỗi không xác định." });
            }
        }

        [HttpGet("customer")]
        [Authorize(Roles = "Customer")]
        public async Task<ActionResult<IEnumerable<Product>>> GetAllForCustomer([FromQuery] int? brandId)
        {
            _logger.LogInformation("GetAllForCustomer products called with brandId: {BrandId}", brandId);

            var query = _context.Products.AsQueryable();
            if (brandId.HasValue)
            {
                query = query.Where(p => p.BrandId == brandId.Value);
            }

            var products = await query
                .Include(p => p.Brand)
                .ToListAsync();
            _logger.LogInformation("Retrieved {Count} products for customer", products.Count);
            return Ok(products);
        }

        [HttpGet("{id}")]
        [Authorize]
        public async Task<ActionResult<Product>> GetById(int id)
        {
            _logger.LogInformation("GetById called for product ID: {Id}", id);

            var product = await _context.Products
                .Include(p => p.Brand)
                .Include(p => p.ProductImages)
                .FirstOrDefaultAsync(p => p.ProductId == id);
            if (product == null)
            {
                _logger.LogWarning("Product not found: {Id}", id);
                return NotFound(new { message = "Sản phẩm không tồn tại." });
            }

            return Ok(product);
        }

        [HttpGet("brand/{brandId}")]
        [Authorize(Roles = "Admin,Staff,InventoryManager")]
        public async Task<ActionResult<IEnumerable<Product>>> GetByBrandId(int brandId)
        {
            _logger.LogInformation("GetByBrandId called for brand ID: {BrandId}", brandId);

            var brand = await _context.Brands.FindAsync(brandId);
            if (brand == null)
            {
                _logger.LogWarning("Brand not found: {BrandId}", brandId);
                return BadRequest(new { message = "Brand không tồn tại." });
            }

            if (User.IsInRole("Staff") || User.IsInRole("InventoryManager"))
            {
                var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userIdClaim) || !int.TryParse(userIdClaim, out int userId))
                {
                    _logger.LogWarning("Cannot determine userId from token.");
                    return BadRequest(new { message = "Không thể xác định userId từ token." });
                }

                var user = await _context.Users.FindAsync(userId);
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
                    effectiveOwnerId = userId;
                }

                if (brand.OwnerId != effectiveOwnerId)
                {
                    _logger.LogWarning("Staff user {UserId} does not have access to brand {BrandId}", userId, brandId);
                    return Forbid();
                }
            }

            var products = await _context.Products
                .Where(p => p.BrandId == brandId)
                .Include(p => p.Brand)
                .Include(p => p.ProductImages)
                .ToListAsync();
            _logger.LogInformation("Retrieved {Count} products for brand ID: {BrandId}", products.Count, brandId);
            return Ok(products);
        }

        [HttpPost]
        [Authorize(Roles = "Admin,Staff,InventoryManager")]
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

            if (User.IsInRole("Staff") || User.IsInRole("InventoryManager"))
            {
                var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userIdClaim) || !int.TryParse(userIdClaim, out int userId))
                {
                    _logger.LogWarning("Cannot determine userId from token.");
                    return BadRequest(new { message = "Không thể xác định userId từ token." });
                }

                var user = await _context.Users.FindAsync(userId);
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
                    effectiveOwnerId = userId;
                }

                if (brand.OwnerId != effectiveOwnerId)
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
                Rating = 0,
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
        [Authorize(Roles = "Admin,Staff,InventoryManager")]
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

            if (User.IsInRole("Staff") || User.IsInRole("InventoryManager"))
            {
                var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userIdClaim) || !int.TryParse(userIdClaim, out int userId))
                {
                    _logger.LogWarning("Cannot determine userId from token.");
                    return BadRequest(new { message = "Không thể xác định userId từ token." });
                }

                var user = await _context.Users.FindAsync(userId);
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
                    effectiveOwnerId = userId;
                }

                if (brand.OwnerId != effectiveOwnerId)
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
            product.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();
            _logger.LogInformation("Updated product ID: {Id}", id);
            return NoContent();
        }

        [HttpDelete("{id}")]
        [Authorize(Roles = "Admin,Staff,InventoryManager")]
        public async Task<IActionResult> Delete(int id)
        {
            _logger.LogInformation("Delete product called for ID: {Id}", id);

            var product = await _context.Products
                .Include(p => p.ProductImages)
                .FirstOrDefaultAsync(p => p.ProductId == id);
            if (product == null)
            {
                _logger.LogWarning("Product not found: {Id}", id);
                return NotFound(new { message = "Sản phẩm không tồn tại." });
            }

            if (User.IsInRole("Staff") || User.IsInRole("InventoryManager"))
            {
                var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userIdClaim) || !int.TryParse(userIdClaim, out int userId))
                {
                    _logger.LogWarning("Cannot determine userId from token.");
                    return BadRequest(new { message = "Không thể xác định userId từ token." });
                }

                var user = await _context.Users.FindAsync(userId);
                if (user == null)
                {
                    _logger.LogWarning("User not found: {UserId}", userId);
                    return NotFound(new { message = "Người dùng không tồn tại." });
                }

                var brand = await _context.Brands.FindAsync(product.BrandId);
                if (brand == null)
                {
                    _logger.LogWarning("Brand not found for product ID: {ProductId}", id);
                    return NotFound(new { message = "Thương hiệu không tồn tại." });
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
                    effectiveOwnerId = userId;
                }

                if (brand.OwnerId != effectiveOwnerId)
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

            if (product.ProductImages != null)
            {
                foreach (var image in product.ProductImages)
                {
                    var imagePath = Path.Combine(_environment.ContentRootPath, image.ImageUrl.TrimStart('/'));
                    if (System.IO.File.Exists(imagePath))
                    {
                        try
                        {
                            System.IO.File.Delete(imagePath);
                            _logger.LogInformation("Deleted image {ImageUrl} for product ID: {Id}", image.ImageUrl, id);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "Failed to delete image {ImageUrl} for product ID: {Id}", image.ImageUrl, id);
                        }
                    }
                }
                _context.ProductImages.RemoveRange(product.ProductImages);
            }

            _context.Products.Remove(product);
            await _context.SaveChangesAsync();
            _logger.LogInformation("Deleted product ID: {Id}", id);
            return NoContent();
        }

        [HttpPost("{id}/images")]
        [Authorize(Roles = "Admin,Staff,InventoryManager")]
        public async Task<IActionResult> UploadImage(int id, IFormFile file)
        {
            _logger.LogInformation("UploadImage called for product ID: {Id}", id);

            var product = await _context.Products.FindAsync(id);
            if (product == null)
            {
                _logger.LogWarning("Product not found: {Id}", id);
                return NotFound(new { message = "Sản phẩm không tồn tại." });
            }

            if (User.IsInRole("Staff") || User.IsInRole("InventoryManager"))
            {
                var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userIdClaim) || !int.TryParse(userIdClaim, out int userId))
                {
                    _logger.LogWarning("Cannot determine userId from token.");
                    return BadRequest(new { message = "Không thể xác định userId từ token." });
                }

                var user = await _context.Users.FindAsync(userId);
                if (user == null)
                {
                    _logger.LogWarning("User not found: {UserId}", userId);
                    return NotFound(new { message = "Người dùng không tồn tại." });
                }

                var brand = await _context.Brands.FindAsync(product.BrandId);
                if (brand == null)
                {
                    _logger.LogWarning("Brand not found for product ID: {ProductId}", id);
                    return NotFound(new { message = "Thương hiệu không tồn tại." });
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
                    effectiveOwnerId = userId;
                }

                if (brand.OwnerId != effectiveOwnerId)
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

            var imageCount = await _context.ProductImages.CountAsync(pi => pi.ProductId == id);
            var fileName = $"product_{id}_{imageCount + 1}{extension}";
            var filePath = Path.Combine(uploadsFolder, fileName);

            using (var stream = new FileStream(filePath, FileMode.Create))
            {
                await file.CopyToAsync(stream);
            }

            var productImage = new ProductImage
            {
                ProductId = id,
                ImageUrl = $"/Images/{fileName}",
                CreatedAt = DateTime.UtcNow
            };

            _context.ProductImages.Add(productImage);
            product.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            _logger.LogInformation("Uploaded image for product ID: {Id}, path: {ImageUrl}", id, productImage.ImageUrl);
            return Ok(new { message = "Tải ảnh lên thành công.", imageUrl = productImage.ImageUrl });
        }

        [HttpDelete("images/{imageId}")]
        [Authorize(Roles = "Admin,Staff,InventoryManager")]
        public async Task<IActionResult> DeleteImage(int imageId)
        {
            _logger.LogInformation("DeleteImage called for image ID: {ImageId}", imageId);

            var image = await _context.ProductImages
                .Include(pi => pi.Product)
                .FirstOrDefaultAsync(pi => pi.ImageId == imageId);
            if (image == null)
            {
                _logger.LogWarning("Image not found: {ImageId}", imageId);
                return NotFound(new { message = "Ảnh không tồn tại." });
            }

            if (User.IsInRole("Staff") || User.IsInRole("InventoryManager"))
            {
                var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userIdClaim) || !int.TryParse(userIdClaim, out int userId))
                {
                    _logger.LogWarning("Cannot determine userId from token.");
                    return BadRequest(new { message = "Không thể xác định userId từ token." });
                }

                var user = await _context.Users.FindAsync(userId);
                if (user == null)
                {
                    _logger.LogWarning("User not found: {UserId}", userId);
                    return NotFound(new { message = "Người dùng không tồn tại." });
                }

                var brand = await _context.Brands.FindAsync(image.Product.BrandId);
                if (brand == null)
                {
                    _logger.LogWarning("Brand not found for product ID: {ProductId}", image.ProductId);
                    return NotFound(new { message = "Thương hiệu không tồn tại." });
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
                    effectiveOwnerId = userId;
                }

                if (brand.OwnerId != effectiveOwnerId)
                {
                    _logger.LogWarning("Staff user {UserId} does not have access to product {ProductId}", userId, image.ProductId);
                    return Forbid();
                }
            }

            var imagePath = Path.Combine(_environment.ContentRootPath, image.ImageUrl.TrimStart('/'));
            if (System.IO.File.Exists(imagePath))
            {
                System.IO.File.Delete(imagePath);
                _logger.LogInformation("Deleted image {ImageUrl} for product ID: {ProductId}", image.ImageUrl, image.ProductId);
            }

            _context.ProductImages.Remove(image);
            image.Product.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            _logger.LogInformation("Deleted image ID: {ImageId}", imageId);
            return NoContent();
        }

        [HttpPost("{id}/comments")]
        [Authorize(Roles = "Customer")]
        public async Task<IActionResult> CreateComment(int id, [FromBody] CommentDTO commentDto)
        {
            _logger.LogInformation("CreateComment called for product ID: {Id}", id);

            var product = await _context.Products.FindAsync(id);
            if (product == null)
            {
                _logger.LogWarning("Product not found: {Id}", id);
                return NotFound(new { message = "Sản phẩm không tồn tại." });
            }

            var customerIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(customerIdClaim) || !int.TryParse(customerIdClaim, out int customerId))
            {
                _logger.LogWarning("Cannot determine customerId from token.");
                return BadRequest(new { message = "Không thể xác định customerId từ token." });
            }

            var customer = await _context.Customers.FindAsync(customerId);
            if (customer == null)
            {
                _logger.LogWarning("Customer not found: {CustomerId}", customerId);
                return NotFound(new { message = "Khách hàng không tồn tại." });
            }

            if (string.IsNullOrWhiteSpace(commentDto.Content))
            {
                _logger.LogWarning("Comment content is empty for product ID: {Id}", id);
                return BadRequest(new { message = "Nội dung bình luận không được để trống." });
            }

            if (commentDto.Rating < 1 || commentDto.Rating > 5)
            {
                _logger.LogWarning("Invalid rating {Rating} for product ID: {Id}", commentDto.Rating, id);
                return BadRequest(new { message = "Đánh giá phải từ 1 đến 5 sao." });
            }

            var comment = new Comment
            {
                ProductId = id,
                CustomerId = customerId,
                Content = commentDto.Content,
                Rating = commentDto.Rating,
                CreatedAt = DateTime.UtcNow
            };

            _context.Comments.Add(comment);
            await _context.SaveChangesAsync();

            var avgRating = await _context.Comments
                .Where(c => c.ProductId == id)
                .AverageAsync(c => (float?)c.Rating) ?? 0;
            product.Rating = (float)Math.Round(avgRating, 1);
            product.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            _logger.LogInformation("Created comment ID: {CommentId} for product ID: {Id}, new rating: {Rating}", comment.CommentId, id, product.Rating);
            return CreatedAtAction(nameof(GetComments), new { id }, comment);
        }

        [HttpGet("{id}/comments")]
        public async Task<ActionResult<IEnumerable<Comment>>> GetComments(int id)
        {
            _logger.LogInformation("GetComments called for product ID: {Id}", id);

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
                .ToListAsync();

            _logger.LogInformation("Retrieved {Count} comments for product ID: {Id}", comments.Count, id);
            return Ok(comments);

        }
        [HttpGet("related/{productId}")]
        public async Task<ActionResult<IEnumerable<ProductDTO>>> GetRelatedProducts(int productId)
        {
            _logger.LogInformation("GetRelatedProducts called for product ID: {ProductId}", productId);

            var product = await _context.Products
                .Include(p => p.ProductCategories)
                .ThenInclude(pc => pc.Category)
                .FirstOrDefaultAsync(p => p.ProductId == productId);

            if (product == null)
            {
                _logger.LogWarning("Product not found: {ProductId}", productId);
                return NotFound(new { message = "Sản phẩm không tồn tại." });
            }

            var categoryIds = product.ProductCategories.Select(pc => pc.CategoryId).ToList();
            var relatedProducts = await _context.Products
                .Include(p => p.Brand)
                .Include(p => p.ProductImages)
                .Include(p => p.ProductCategories)
                .Where(p => p.ProductId != productId && p.ProductCategories.Any(pc => categoryIds.Contains(pc.CategoryId)))
                .Take(5) // Lấy tối đa 5 sản phẩm liên quan
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
                    ProductImages = p.ProductImages.Select(pi => pi.ImageUrl).ToList(),
                    CategoryIds = p.ProductCategories.Select(pc => pc.CategoryId).ToList()
                })
                .ToListAsync();

            _logger.LogInformation("Retrieved {Count} related products for product ID: {ProductId}", relatedProducts.Count, productId);
            return Ok(relatedProducts);
        }
    }
}