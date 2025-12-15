using HakiBaVuong.DTOs;
using HakiBaVuong.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using HakiBaVuong.Data;
using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using System.Net.Http;
using System.Text.Json;
//BE mới
namespace HakiBaVuong.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class ProductController : ControllerBase
    {
        private readonly IConfiguration _config;
        private readonly DataContext _context;
        private readonly ILogger<ProductController> _logger;
        private readonly IWebHostEnvironment _environment;
        private readonly HttpClient _httpClient;

        public ProductController(DataContext context, ILogger<ProductController> logger, IWebHostEnvironment environment, HttpClient httpClient, IConfiguration config)
        {
            _context = context;
            _logger = logger;
            _environment = environment;
            _httpClient = httpClient;
            _config = config;
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<ProductDTO>>> GetAll([FromQuery] int? brandId, [FromQuery] List<int> categoryIds)
        {
            _logger.LogInformation("GetAll products called with brandId: {BrandId}, categoryIds: {CategoryIds}", brandId, categoryIds);

            var query = _context.Products
                .Include(p => p.Brand)
                .Include(p => p.ProductCategories)
                .ThenInclude(pc => pc.Category)
                .AsQueryable();

            if (brandId.HasValue)
            {
                query = query.Where(p => p.BrandId == brandId.Value);
            }

            if (categoryIds != null && categoryIds.Any())
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
                    Image = p.Image,
                    CategoryIds = p.ProductCategories.Select(pc => pc.CategoryId).ToList()
                })
                .ToListAsync();

            _logger.LogInformation("Retrieved {Count} products", products.Count);
            return Ok(products);
        }

        [HttpGet("with-brand")]
        public async Task<ActionResult<IEnumerable<Product>>> GetAllWithBrand([FromQuery] int? brandId, [FromQuery] List<int> categoryIds)
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
                .ToListAsync();
            _logger.LogInformation("Retrieved {Count} products for brand ID: {BrandId}", products.Count, brandId);
            return Ok(products);
        }

        [HttpGet("categories")]
        [Authorize(Roles = "Admin,Staff,InventoryManager")]
        public async Task<ActionResult<IEnumerable<CategoryDTO>>> GetCategories([FromQuery] string search = "")
        {
            _logger.LogInformation("GetCategories called with search: {Search}", search);

            var query = _context.Categories.AsQueryable();

            if (!string.IsNullOrWhiteSpace(search))
            {
                var searchLower = search.ToLower();
                query = query.Where(c => c.Name.ToLower().Contains(searchLower) || (c.Description != null && c.Description.ToLower().Contains(searchLower)));
            }

            var categories = await query
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

        [HttpPost]
        [Authorize(Roles = "Admin,Staff,InventoryManager")]
        public async Task<ActionResult<ProductDTO>> Create(ProductDTO productDto)
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
                Image = productDto.Image,
                Rating = 0,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            _context.Products.Add(product);
            await _context.SaveChangesAsync();

            if (productDto.CategoryIds != null && productDto.CategoryIds.Any())
            {
                var productCategories = productDto.CategoryIds.Select(categoryId => new ProductCategory
                {
                    ProductId = product.ProductId,
                    CategoryId = categoryId,
                    AssignedAt = DateTime.UtcNow
                }).ToList();
                _context.ProductCategories.AddRange(productCategories);
                await _context.SaveChangesAsync();
            }

            var inventory = new Inventory
            {
                ProductId = product.ProductId,
                StockQuantity = 0,
                LastUpdated = DateTime.UtcNow
            };
            _context.Inventories.Add(inventory);
            await _context.SaveChangesAsync();

            var productDtoResponse = new ProductDTO
            {
                ProductId = product.ProductId,
                BrandId = product.BrandId,
                Name = product.Name,
                Description = product.Description,
                PriceSell = product.PriceSell,
                PriceCost = product.PriceCost,
                Rating = product.Rating,
                CreatedAt = product.CreatedAt,
                UpdatedAt = product.UpdatedAt,
                Image = product.Image,
                CategoryIds = productDto.CategoryIds ?? new List<int>()
            };

            _logger.LogInformation("Created product ID: {ProductId} with inventory ID: {InventoryId}", product.ProductId, inventory.InventoryId);
            return CreatedAtAction(nameof(GetById), new { id = product.ProductId }, productDtoResponse);
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
            product.Image = productDto.Image;
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
                    try
                    {
                        System.IO.File.Delete(imagePath);
                        _logger.LogInformation("Deleted image for product ID: {Id}", id);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Failed to delete image for product ID: {Id}", id);
                    }
                }
            }

            _context.Products.Remove(product);
            await _context.SaveChangesAsync();
            _logger.LogInformation("Deleted product ID: {Id}", id);
            return NoContent();
        }

        [HttpPost("{id}/upload-image")]
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

            var fileName = $"product_{id}{extension}";
            var filePath = Path.Combine(uploadsFolder, fileName);

            if (!string.IsNullOrEmpty(product.Image))
            {
                var oldImagePath = Path.Combine(_environment.ContentRootPath, product.Image.TrimStart('/'));
                if (System.IO.File.Exists(oldImagePath))
                {
                    try
                    {
                        System.IO.File.Delete(oldImagePath);
                        _logger.LogInformation("Deleted old image for product ID: {Id}", id);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Failed to delete old image for product ID: {Id}", id);
                    }
                }
            }

            using (var stream = new FileStream(filePath, FileMode.Create))
            {
                await file.CopyToAsync(stream);
            }

            product.Image = $"/Images/{fileName}";
            product.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            _logger.LogInformation("Uploaded image for product ID: {Id}, path: {ImagePath}", id, product.Image);
            return Ok(new { message = "Tải ảnh lên thành công.", imageUrl = product.Image });
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
                .Include(p => p.ProductCategories)
                .Where(p => p.ProductId != productId && p.ProductCategories.Any(pc => categoryIds.Contains(pc.CategoryId)))
                .Take(10)
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

            _logger.LogInformation("Retrieved {Count} related products for product ID: {ProductId}", relatedProducts.Count, productId);
            return Ok(relatedProducts);
        }

        [HttpGet("suggest")]
        [Authorize]
        public async Task<ActionResult<IEnumerable<ProductDTO>>> Suggest([FromQuery] string query, [FromQuery] int? brandId, [FromQuery] List<int> categoryIds)
        {
            _logger.LogInformation("Suggest products called with query: {Query}, brandId: {BrandId}, categoryIds: {CategoryIds}", query, brandId, categoryIds);

            var customerId = GetCustomerId();
            if (customerId == null)
            {
                _logger.LogWarning("Invalid customerId from token");
                return Unauthorized(new { message = "Token không hợp lệ." });
            }

            var queryLower = query?.ToLower() ?? "";
            var queryable = _context.Products
                .Include(p => p.Brand)
                .Include(p => p.ProductCategories)
                .ThenInclude(pc => pc.Category)
                .AsQueryable();

            if (categoryIds != null && categoryIds.Any())
            {
                queryable = queryable.Where(p => p.ProductCategories.Any(pc => categoryIds.Contains(pc.CategoryId)));
            }

            var products = await queryable
                .Where(p => string.IsNullOrEmpty(queryLower) || p.Name.ToLower().Contains(queryLower) || p.Brand.Name.ToLower().Contains(queryLower))
                .Where(p => !brandId.HasValue || p.BrandId == brandId.Value)
                .OrderByDescending(p => p.Rating)
                .Take(5)
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

            _logger.LogInformation("Retrieved {Count} suggested products", products.Count);
            return Ok(products);
        }

        [HttpGet("home")]
        public async Task<ActionResult<IEnumerable<ProductDTO>>> GetHomeRecommendations()
        {
            _logger.LogInformation("GetHomeRecommendations called");

            var customerId = GetCustomerId();
            if (customerId == null)
            {
                _logger.LogInformation("No customerId provided, returning popular products");

                var popularProducts = await _context.Products
                    .Include(p => p.Brand)
                    .Include(p => p.ProductCategories)
                    .ThenInclude(pc => pc.Category)
                    .OrderByDescending(p => p.Rating)
                    .Take(9)
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

                _logger.LogInformation("Retrieved {Count} popular products", popularProducts.Count);
                return Ok(popularProducts);
            }

            var viewedProducts = await _context.ViewedProducts
                .Where(vp => vp.CustomerId == customerId)
                .Include(vp => vp.Product)
                .ThenInclude(p => p.ProductCategories)
                .ThenInclude(pc => pc.Category)
                .ToListAsync();

            if (!viewedProducts.Any())
            {
                _logger.LogInformation("No viewed products found for customer {CustomerId}, returning popular products", customerId);

                var popularProducts = await _context.Products
                    .Include(p => p.Brand)
                    .Include(p => p.ProductCategories)
                    .ThenInclude(pc => pc.Category)
                    .OrderByDescending(p => p.Rating)
                    .Take(9)
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

                _logger.LogInformation("Retrieved {Count} popular products", popularProducts.Count);
                return Ok(popularProducts);
            }

            var randomViewedProduct = viewedProducts.OrderBy(x => Guid.NewGuid()).First();
            var categoryIds = randomViewedProduct.Product.ProductCategories.Select(pc => pc.CategoryId).ToList();

            var recommendedProducts = await _context.Products
                .Include(p => p.Brand)
                .Include(p => p.ProductCategories)
                .ThenInclude(pc => pc.Category)
                .Where(p => p.ProductCategories.Any(pc => categoryIds.Contains(pc.CategoryId)))
                .OrderByDescending(p => p.Rating)
                .Take(9)
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

            _logger.LogInformation("Retrieved {Count} recommended products for customer {CustomerId} based on viewed product categories", recommendedProducts.Count, customerId);
            return Ok(recommendedProducts);
        }

        [HttpPost("{productId}/view")]
        [Authorize]
        public async Task<IActionResult> MarkAsViewed(int productId)
        {
            _logger.LogInformation("MarkAsViewed called for product ID: {ProductId}", productId);

            var customerId = GetCustomerId();
            if (customerId == null)
            {
                _logger.LogWarning("Invalid customerId from token");
                return Unauthorized(new { message = "Token không hợp lệ." });
            }

            var product = await _context.Products.FindAsync(productId);
            if (product == null)
            {
                _logger.LogWarning("Product not found: {ProductId}", productId);
                return NotFound(new { message = "Sản phẩm không tồn tại." });
            }

            var viewed = await _context.ViewedProducts
                .FirstOrDefaultAsync(vp => vp.CustomerId == customerId && vp.ProductId == productId);

            if (viewed == null)
            {
                var viewedProduct = new ViewedProduct
                {
                    CustomerId = customerId.Value,
                    ProductId = productId
                };
                _context.ViewedProducts.Add(viewedProduct);
                await _context.SaveChangesAsync();
                _logger.LogInformation("Marked product {ProductId} as viewed by customer {CustomerId}", productId, customerId);
            }

            return Ok(new { message = "Đã ghi nhận sản phẩm đã xem." });
        }

        [HttpGet("brand-name/{brandId}")]
        public async Task<ActionResult<string>> GetBrandName(int brandId)
        {
            _logger.LogInformation("GetBrandName called for brand ID: {BrandId}", brandId);

            var brand = await _context.Brands
                .Where(b => b.BrandId == brandId)
                .Select(b => b.Name)
                .FirstOrDefaultAsync();

            if (brand == null)
            {
                _logger.LogWarning("Brand not found: {BrandId}", brandId);
                return NotFound(new { message = "Thương hiệu không tồn tại." });
            }

            return Ok(brand);
        }

        private int? GetCustomerId()
        {
            var customerIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            return int.TryParse(customerIdClaim, out int customerId) ? customerId : null;
        }

        [HttpPost("scan-and-create")]
        // No [Authorize] - open endpoint for testing: call barcode API, create product and save to DB
        public async Task<IActionResult> ScanAndCreate([FromBody] ScanBarcodeDTO model)
        {
            if (string.IsNullOrWhiteSpace(model?.Barcode))
                return BadRequest(new { message = "Vui lòng cung cấp barcode trong body (Barcode property)." });

            _logger.LogInformation("ScanAndCreate called for barcode: {Barcode}, requested BrandId: {BrandId}", model.Barcode, model.BrandId);

            var baseUrl = _config["BarcodeApi:BaseUrl"];
            var apiKey = _config["BarcodeApi:Key"];
            var url = $"{baseUrl}?barcode={System.Net.WebUtility.UrlEncode(model.Barcode)}&formatted=y&key={apiKey}";

            try
            {
                var response = await _httpClient.GetAsync(url);
                if (!response.IsSuccessStatusCode)
                {
                    var err = await response.Content.ReadAsStringAsync();
                    _logger.LogWarning("Barcode API returned non-success: {Status} - {Body}", response.StatusCode, err);
                    return StatusCode((int)response.StatusCode, new { message = "Không tìm thấy thông tin sản phẩm từ API barcode.", details = err });
                }

                var json = await response.Content.ReadAsStringAsync();
                using var data = JsonDocument.Parse(json);
                if (!data.RootElement.TryGetProperty("products", out var products) || products.GetArrayLength() == 0)
                    return NotFound(new { message = "Không tìm thấy thông tin sản phẩm!" });

                var item = products[0];

                var title = item.TryGetProperty("title", out var t) ? t.GetString() : null;
                var brandName = item.TryGetProperty("brand", out var b) ? b.GetString() : null;
                var manufacturer = item.TryGetProperty("manufacturer", out var m) ? m.GetString() : null;
                var category = item.TryGetProperty("category", out var c) ? c.GetString() : null;
                var description = item.TryGetProperty("description", out var d) ? d.GetString() : null;

                var finalDescription = string.Join("\n\n", new[] { category, brandName ?? manufacturer, description }.Where(s => !string.IsNullOrWhiteSpace(s)));

                Brand brand = null;
                if (model.BrandId.HasValue)
                {
                    brand = await _context.Brands.FindAsync(model.BrandId.Value);
                    if (brand == null)
                    {
                        _logger.LogWarning("Provided BrandId {BrandId} not found.", model.BrandId.Value);
                        return BadRequest(new { message = "BrandId được cung cấp không tồn tại." });
                    }
                }
                else
                {
                    // Ensure we have a Brand to assign. Try to find an existing brand; if none, create a simple one referencing an existing or new user.
                    brand = await _context.Brands.FirstOrDefaultAsync();
                    if (brand == null)
                    {
                        var user = await _context.Users.FirstOrDefaultAsync();
                        if (user == null)
                        {
                            user = new User
                            {
                                Name = "ImportedUser",
                                Email = $"importer_{Guid.NewGuid():N}@local",
                                Password = "importer",
                                Role = "User",
                                CreatedAt = DateTime.UtcNow,
                                UpdatedAt = DateTime.UtcNow
                            };
                            _context.Users.Add(user);
                            await _context.SaveChangesAsync();
                            _logger.LogInformation("Created placeholder user for import with id {UserId}", user.UserId);
                        }

                        brand = new Brand
                        {
                            Name = !string.IsNullOrWhiteSpace(brandName) ? brandName : "Imported Brand",
                            OwnerId = user.UserId,
                            CreatedAt = DateTime.UtcNow
                        };
                        _context.Brands.Add(brand);
                        await _context.SaveChangesAsync();
                        _logger.LogInformation("Created placeholder brand for import with id {BrandId}", brand.BrandId);
                    }
                }

                // Download first image if available
                string imageUrl = null;
                if (item.TryGetProperty("images", out var imgs) && imgs.ValueKind == JsonValueKind.Array && imgs.GetArrayLength() > 0)
                {
                    imageUrl = imgs[0].GetString();
                }

                string savedImagePath = null;
                if (!string.IsNullOrWhiteSpace(imageUrl))
                {
                    try
                    {
                        var imgResp = await _httpClient.GetAsync(imageUrl);
                        if (imgResp.IsSuccessStatusCode)
                        {
                            var bytes = await imgResp.Content.ReadAsByteArrayAsync();
                            // Determine extension
                            var ext = Path.GetExtension(new Uri(imageUrl).LocalPath);
                            if (string.IsNullOrEmpty(ext))
                            {
                                var ct = imgResp.Content.Headers.ContentType?.MediaType ?? "";
                                ext = ct switch
                                {
                                    "image/png" => ".png",
                                    "image/jpeg" => ".jpg",
                                    "image/gif" => ".gif",
                                    _ => ".jpg"
                                };
                            }

                            var uploadsFolder = Path.Combine(_environment.ContentRootPath, "Images");
                            if (!Directory.Exists(uploadsFolder)) Directory.CreateDirectory(uploadsFolder);

                            var fileName = $"barcode_{DateTime.UtcNow:yyyyMMddHHmmss}_{Guid.NewGuid()}{ext}";
                            var filePath = Path.Combine(uploadsFolder, fileName);
                            await System.IO.File.WriteAllBytesAsync(filePath, bytes);
                            savedImagePath = $"/Images/{fileName}";
                            _logger.LogInformation("Downloaded and saved image for barcode to {Path}", savedImagePath);
                        }
                        else
                        {
                            _logger.LogWarning("Failed to download image from {Url} - status {Status}", imageUrl, imgResp.StatusCode);
                        }
                    }
                    catch (Exception exImg)
                    {
                        _logger.LogWarning(exImg, "Exception when downloading image {Url}", imageUrl);
                    }
                }

                var product = new Product
                {
                    BrandId = brand.BrandId,
                    Name = (title ?? "Imported product").Trim(),
                    Description = string.IsNullOrWhiteSpace(finalDescription) ? (description ?? "") : finalDescription,
                    PriceSell = 99999999m,
                    PriceCost = 99999999m,
                    Image = savedImagePath ?? string.Empty,
                    Rating = 5.0f,
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

                _logger.LogInformation("Imported product {ProductId} from barcode {Barcode} assigned to BrandId {BrandId}", product.ProductId, model.Barcode, brand.BrandId);

                var productDtoResponse = new ProductDTO
                {
                    ProductId = product.ProductId,
                    BrandId = product.BrandId,
                    Name = product.Name,
                    Description = product.Description,
                    PriceSell = product.PriceSell,
                    PriceCost = product.PriceCost ?? 0,
                    Rating = product.Rating,
                    CreatedAt = product.CreatedAt,
                    UpdatedAt = product.UpdatedAt,
                    Image = product.Image,
                    CategoryIds = new List<int>()
                };

                return CreatedAtAction(nameof(GetById), new { id = product.ProductId }, new { message = "Đã thêm sản phẩm từ barcode.", product = productDtoResponse });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in ScanAndCreate for barcode: {Barcode}", model.Barcode);
                return StatusCode(500, new { message = "Lỗi khi import sản phẩm từ barcode." });
            }
        }
    }
}