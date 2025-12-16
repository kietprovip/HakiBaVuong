using AutoMapper;
using HakiBaVuong.Data;
using HakiBaVuong.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using System.Security.Claims;

namespace HakiBaVuong.Controllers
{
    [ApiController]
    [Route("api/reels")]
    public class ShortVideoController : ControllerBase
    {
        private readonly DataContext _db;
        private readonly IMapper _mapper;
        private readonly IWebHostEnvironment _environment;
        private readonly ILogger<ShortVideoController> _logger;

        public ShortVideoController(
            DataContext db,
            IMapper mapper,
            IWebHostEnvironment environment,
            ILogger<ShortVideoController> logger)
        {
            _db = db;
            _mapper = mapper;
            _environment = environment;
            _logger = logger;
        }

        // Helper: determine which BrandIds the current authenticated user (owner/staff) can access.
        // Returns null = no restriction (e.g. unauthenticated or Admin)
        private async Task<List<int>?> GetAccessibleBrandIdsAsync()
        {
            if (!User.Identity?.IsAuthenticated ?? true)
                return null;

            if (User.IsInRole("Admin"))
                return null;

            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (!int.TryParse(userIdClaim, out int userId))
                return null;

            var user = await _db.Users.FindAsync(userId);
            if (user == null)
                return null;

            int ownerId;
            if (user.BrandId.HasValue)
            {
                var userBrand = await _db.Brands.FindAsync(user.BrandId.Value);
                if (userBrand == null)
                    return null;
                ownerId = userBrand.OwnerId;
            }
            else
            {
                ownerId = userId;
            }

            var brandIds = await _db.Brands.Where(b => b.OwnerId == ownerId).Select(b => b.BrandId).ToListAsync();
            return brandIds;
        }

        // GET: api/reels/feed?page=1&customerId=123
        [HttpGet("feed")]
        public async Task<IActionResult> GetFeed([FromQuery] int page = 1, [FromQuery] int? customerId = null)
        {
            const int pageSize = 10;

            var query = _db.ShortVideos
                .Include(v => v.Brand)
                .Include(v => v.VideoProducts).ThenInclude(vp => vp.Product)
                .OrderByDescending(v => v.CreatedAt)
                .AsQueryable();

            var accessibleBrandIds = await GetAccessibleBrandIdsAsync();
            if (accessibleBrandIds != null)
            {
                query = query.Where(v => accessibleBrandIds.Contains(v.BrandId));
            }

            var videos = await query
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(v => new
                {
                    v.VideoId,
                    v.VideoUrl,
                    v.ThumbnailUrl,
                    v.Caption,
                    v.Views,
                    v.Likes,
                    v.CommentsCount,
                    v.Shares,
                    v.CreatedAt,
                    Brand = new { v.Brand.BrandId, v.Brand.Name },
                    Products = v.VideoProducts.Select(p => new
                    {
                        p.Product.ProductId,
                        p.Product.Name,
                        p.Product.PriceSell,
                        p.Product.Image,
                        p.X,
                        p.Y
                    }).ToList(),
                    IsLiked = customerId.HasValue && _db.VideoLikes.Any(l => l.VideoId == v.VideoId && l.CustomerId == customerId.Value)
                })
                .ToListAsync();

            return Ok(videos);
        }

        // POST: api/reels/upload (Main endpoint - ANY BRAND, NO AUTH REQUIRED)
        [HttpPost("upload")]
        [Consumes("multipart/form-data")]
        public async Task<IActionResult> Upload([FromForm] UploadVideoDTO dto)
        {
            try
            {
                _logger.LogInformation("Upload request for BrandId={BrandId}", dto.BrandId);

                // ===== VALIDATE BRAND EXISTS =====
                var brand = await _db.Brands.FirstOrDefaultAsync(b => b.BrandId == dto.BrandId);
                if (brand == null)
                {
                    _logger.LogWarning("Brand ID {BrandId} does not exist", dto.BrandId);
                    return BadRequest(new { message = $"Brand ID {dto.BrandId} không tồn tại." });
                }

                // ===== VALIDATE FILES =====
                if (dto.VideoFile == null || dto.VideoFile.Length == 0)
                {
                    _logger.LogWarning("VideoFile missing or empty");
                    return BadRequest(new { message = "Video file không được để trống." });
                }

                var allowedVideoExtensions = new[] { ".mp4", ".webm", ".mov", ".avi" };
                var videoExtension = Path.GetExtension(dto.VideoFile.FileName).ToLowerInvariant();
                if (!allowedVideoExtensions.Contains(videoExtension))
                {
                    _logger.LogWarning("Invalid video extension: {Extension}", videoExtension);
                    return BadRequest(new { message = "Định dạng video không hỗ trợ. Chỉ hỗ trợ: .mp4, .webm, .mov, .avi" });
                }

                var maxVideoSize = 500 * 1024 * 1024;
                if (dto.VideoFile.Length > maxVideoSize)
                {
                    _logger.LogWarning("Video size {Size} exceeds limit", dto.VideoFile.Length);
                    return BadRequest(new { message = "Kích thước video vượt quá 500MB." });
                }

                if (dto.ThumbnailFile == null || dto.ThumbnailFile.Length == 0)
                {
                    _logger.LogWarning("ThumbnailFile missing or empty");
                    return BadRequest(new { message = "Thumbnail file không được để trống." });
                }

                var allowedImageExtensions = new[] { ".jpg", ".jpeg", ".png" };
                var thumbnailExtension = Path.GetExtension(dto.ThumbnailFile.FileName).ToLowerInvariant();
                if (!allowedImageExtensions.Contains(thumbnailExtension))
                {
                    _logger.LogWarning("Invalid thumbnail extension: {Extension}", thumbnailExtension);
                    return BadRequest(new { message = "Định dạng thumbnail không hỗ trợ. Chỉ hỗ trợ: .jpg, .jpeg, .png" });
                }

                var maxThumbnailSize = 10 * 1024 * 1024;
                if (dto.ThumbnailFile.Length > maxThumbnailSize)
                {
                    _logger.LogWarning("Thumbnail size {Size} exceeds limit", dto.ThumbnailFile.Length);
                    return BadRequest(new { message = "Kích thước thumbnail vượt quá 10MB." });
                }

                // ===== CREATE DIRECTORIES =====
                var videosFolder = Path.Combine(_environment.ContentRootPath, "Uploads", "videos");
                var thumbnailsFolder = Path.Combine(_environment.ContentRootPath, "Uploads", "thumbnails");

                if (!Directory.Exists(videosFolder))
                    Directory.CreateDirectory(videosFolder);

                if (!Directory.Exists(thumbnailsFolder))
                    Directory.CreateDirectory(thumbnailsFolder);

                // ===== SAVE FILES =====
                var videoFileName = $"video_{DateTime.UtcNow.Ticks}{videoExtension}";
                var videoPath = Path.Combine(videosFolder, videoFileName);

                using (var stream = new FileStream(videoPath, FileMode.Create))
                {
                    await dto.VideoFile.CopyToAsync(stream);
                }

                var thumbnailFileName = $"thumb_{DateTime.UtcNow.Ticks}{thumbnailExtension}";
                var thumbnailPath = Path.Combine(thumbnailsFolder, thumbnailFileName);

                using (var stream = new FileStream(thumbnailPath, FileMode.Create))
                {
                    await dto.ThumbnailFile.CopyToAsync(stream);
                }

                _logger.LogInformation("Files saved - Video: {VideoFileName}, Thumbnail: {ThumbFileName}", videoFileName, thumbnailFileName);

                // ===== CREATE VIDEO RECORD =====
                var video = new ShortVideo
                {
                    BrandId = dto.BrandId,
                    VideoUrl = $"/Uploads/videos/{videoFileName}",
                    ThumbnailUrl = $"/Uploads/thumbnails/{thumbnailFileName}",
                    Caption = dto.Caption ?? "",
                    CreatedAt = DateTime.UtcNow
                };

                _db.ShortVideos.Add(video);
                await _db.SaveChangesAsync();

                _logger.LogInformation("Video created successfully: VideoId={VideoId}, BrandId={BrandId}", video.VideoId, dto.BrandId);

                return Ok(new
                {
                    videoId = video.VideoId,
                    brandId = dto.BrandId,
                    message = "Đăng video thành công!"
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error uploading video");
                return StatusCode(500, new { message = "Lỗi khi đăng video.", error = ex.Message });
            }
        }

        // POST: api/reels/{videoId}/like
        [HttpPost("{videoId}/like")]
        [Authorize(Roles = "Customer")]
        public async Task<IActionResult> ToggleLike(int videoId)
        {
            try
            {
                var customerIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (!int.TryParse(customerIdClaim, out int customerId))
                {
                    return Unauthorized(new { message = "Token không hợp lệ." });
                }

                var video = await _db.ShortVideos.FindAsync(videoId);
                if (video == null)
                {
                    return NotFound(new { message = "Video không tồn tại." });
                }

                var customer = await _db.Customers.FindAsync(customerId);
                if (customer == null)
                {
                    return NotFound(new { message = "Khách hàng không tồn tại." });
                }

                var existing = await _db.VideoLikes
                    .FirstOrDefaultAsync(l => l.VideoId == videoId && l.CustomerId == customerId);

                if (existing == null)
                {
                    _db.VideoLikes.Add(new VideoLike { VideoId = videoId, CustomerId = customerId });
                    await _db.Database.ExecuteSqlRawAsync("UPDATE ShortVideos SET Likes = Likes + 1 WHERE VideoId = {0}", videoId);
                    await _db.SaveChangesAsync();
                    return Ok(new { message = "Liked!", isLiked = true });
                }
                else
                {
                    _db.VideoLikes.Remove(existing);
                    await _db.Database.ExecuteSqlRawAsync("UPDATE ShortVideos SET Likes = Likes - 1 WHERE VideoId = {0}", videoId);
                    await _db.SaveChangesAsync();
                    return Ok(new { message = "Unliked!", isLiked = false });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in ToggleLike for videoId: {VideoId}", videoId);
                return StatusCode(500, new { message = "Lỗi khi like video.", error = ex.Message });
            }
        }

        // DELETE: api/reels/{videoId}/like
        [HttpDelete("{videoId}/like")]
        [Authorize(Roles = "Customer")]
        public async Task<IActionResult> UnlikeDirect(int videoId)
        {
            try
            {
                var customerIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (!int.TryParse(customerIdClaim, out int customerId))
                {
                    return Unauthorized(new { message = "Token không hợp lệ." });
                }

                var video = await _db.ShortVideos.FindAsync(videoId);
                if (video == null)
                {
                    return NotFound(new { message = "Video không tồn tại." });
                }

                var existing = await _db.VideoLikes
                    .FirstOrDefaultAsync(l => l.VideoId == videoId && l.CustomerId == customerId);

                if (existing == null)
                {
                    return NotFound(new { message = "Bạn chưa like video này." });
                }

                _db.VideoLikes.Remove(existing);
                await _db.Database.ExecuteSqlRawAsync("UPDATE ShortVideos SET Likes = Likes - 1 WHERE VideoId = {0}", videoId);
                await _db.SaveChangesAsync();

                return Ok(new { message = "Unliked!", isLiked = false });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in UnlikeDirect for videoId: {VideoId}", videoId);
                return StatusCode(500, new { message = "Lỗi khi bỏ like video.", error = ex.Message });
            }
        }

        // POST: api/reels/{videoId}/comment
        [HttpPost("{videoId}/comment")]
        [Authorize(Roles = "Customer")]
        public async Task<IActionResult> Comment(int videoId, [FromBody] VideoCommentDTO dto)
        {
            try
            {
                var customerIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (!int.TryParse(customerIdClaim, out int customerId))
                {
                    return Unauthorized(new { message = "Token không hợp lệ." });
                }

                var video = await _db.ShortVideos.FindAsync(videoId);
                if (video == null)
                {
                    return NotFound(new { message = "Video không tồn tại." });
                }

                var customer = await _db.Customers.FindAsync(customerId);
                if (customer == null)
                {
                    return NotFound(new { message = "Khách hàng không tồn tại." });
                }

                var comment = new VideoComment
                {
                    VideoId = videoId,
                    CustomerId = customerId,
                    CustomerName = customer.Name ?? "Khách",
                    Content = dto.Content,
                    CreatedAt = DateTime.UtcNow
                };

                _db.VideoComments.Add(comment);
                await _db.Database.ExecuteSqlRawAsync("UPDATE ShortVideos SET CommentsCount = CommentsCount + 1 WHERE VideoId = {0}", videoId);
                await _db.SaveChangesAsync();

                return Ok(new
                {
                    commentId = comment.CommentId,
                    videoId = videoId,
                    customerId = customerId,
                    customerName = comment.CustomerName,
                    content = comment.Content,
                    createdAt = comment.CreatedAt,
                    message = "Bình luận thành công!"
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in Comment for videoId: {VideoId}", videoId);
                return StatusCode(500, new { message = "Lỗi khi bình luận.", error = ex.Message });
            }
        }

        // GET: api/reels/{videoId}/comments?page=1
        [HttpGet("{videoId}/comments")]
        public async Task<IActionResult> GetComments(int videoId, [FromQuery] int page = 1)
        {
            const int pageSize = 10;

            var comments = await _db.VideoComments
                .Where(c => c.VideoId == videoId)
                .OrderByDescending(c => c.CreatedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(c => new
                {
                    c.CommentId,
                    c.VideoId,
                    c.CustomerId,
                    c.CustomerName,
                    c.Content,
                    c.CreatedAt
                })
                .ToListAsync();

            return Ok(comments);
        }

        // DELETE: api/reels/{videoId}/comment/{commentId}
        [HttpDelete("{videoId}/comment/{commentId}")]
        [Authorize(Roles = "Customer")]
        public async Task<IActionResult> DeleteComment(int videoId, int commentId)
        {
            var authenticatedUserId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (!int.TryParse(authenticatedUserId, out int userId))
            {
                return Unauthorized(new { message = "Token không hợp lệ." });
            }

            var comment = await _db.VideoComments
                .FirstOrDefaultAsync(c => c.CommentId == commentId && c.VideoId == videoId);

            if (comment == null)
            {
                return NotFound(new { message = "Bình luận không tồn tại." });
            }

            // Chỉ cho phép xóa comment của chính mình
            if (comment.CustomerId != userId)
            {
                return Unauthorized(new { message = "Bạn chỉ có thể xóa bình luận của chính mình." });
            }

            _db.VideoComments.Remove(comment);
            await _db.Database.ExecuteSqlRawAsync("UPDATE ShortVideos SET CommentsCount = CommentsCount - 1 WHERE VideoId = {0}", videoId);
            await _db.SaveChangesAsync();

            return Ok(new { message = "Xóa bình luận thành công!" });
        }

        // POST: api/reels/{videoId}/view
        [HttpPost("{videoId}/view")]
        public async Task<IActionResult> ViewVideo(int videoId)
        {
            try
            {
                var video = await _db.ShortVideos.FindAsync(videoId);
                if (video == null)
                {
                    return NotFound(new { message = "Video không tồn tại." });
                }

                // Lấy customerId từ token nếu user đã login
                var customerIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                int? customerId = null;
                if (int.TryParse(customerIdClaim, out int parsed))
                {
                    // Verify customer exists
                    var customer = await _db.Customers.FindAsync(parsed);
                    if (customer != null)
                        customerId = parsed;
                }

                bool isNewView = false;

                // Kiểm tra view đã tồn tại chưa
                if (customerId.HasValue)
                {
                    var existingView = await _db.VideoViews
                        .FirstOrDefaultAsync(vv => vv.VideoId == videoId && vv.CustomerId == customerId.Value);

                    if (existingView == null)
                    {
                        // View lần đầu
                        isNewView = true;
                        _db.VideoViews.Add(new VideoView
                        {
                            VideoId = videoId,
                            CustomerId = customerId.Value,
                            ViewedAt = DateTime.UtcNow
                        });
                    }
                    else
                    {
                        // Đã xem rồi, chỉ update lần xem gần nhất
                        existingView.ViewedAt = DateTime.UtcNow;
                        _db.VideoViews.Update(existingView);
                    }
                }
                else
                {
                    // Nếu chưa login, luôn tính là view mới
                    isNewView = true;
                }

                // Tăng view count chỉ nếu là lần đầu xem
                if (isNewView)
                {
                    await _db.Database.ExecuteSqlRawAsync("UPDATE ShortVideos SET Views = Views + 1 WHERE VideoId = {0}", videoId);
                }

                await _db.SaveChangesAsync();
                return Ok(new { message = "Đã ghi nhận lượt xem.", isNewView });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in ViewVideo for videoId: {VideoId}", videoId);
                return StatusCode(500, new { message = "Lỗi khi ghi nhận lượt xem.", error = ex.Message });
            }
        }

        // POST: api/reels/{videoId}/share
        [HttpPost("{videoId}/share")]
        public async Task<IActionResult> ShareVideo(int videoId)
        {
            try
            {
                var video = await _db.ShortVideos.FindAsync(videoId);
                if (video == null)
                {
                    return NotFound(new { message = "Video không tồn tại." });
                }

                // Lấy customerId từ token nếu user đã login
                var customerIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                int? customerId = null;
                if (int.TryParse(customerIdClaim, out int parsed))
                {
                    // Verify customer exists
                    var customer = await _db.Customers.FindAsync(parsed);
                    if (customer != null)
                        customerId = parsed;
                }

                // Nếu user đã login, lưu vào bảng VideoShare
                if (customerId.HasValue)
                {
                    var existingShare = await _db.VideoShares
                        .FirstOrDefaultAsync(vs => vs.VideoId == videoId && vs.CustomerId == customerId.Value);

                    if (existingShare == null)
                    {
                        _db.VideoShares.Add(new VideoShare
                        {
                            VideoId = videoId,
                            CustomerId = customerId.Value,
                            SharedAt = DateTime.UtcNow
                        });
                    }
                    else
                    {
                        existingShare.SharedAt = DateTime.UtcNow;
                        _db.VideoShares.Update(existingShare);
                    }
                }

                // Tăng share count
                await _db.Database.ExecuteSqlRawAsync("UPDATE ShortVideos SET Shares = Shares + 1 WHERE VideoId = {0}", videoId);
                await _db.SaveChangesAsync();

                return Ok(new { message = "Chia sẻ video thành công!" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in ShareVideo for videoId: {VideoId}", videoId);
                return StatusCode(500, new { message = "Lỗi khi chia sẻ video.", error = ex.Message });
            }
        }

        // POST: api/reels/brand/{brandId}/follow
        [HttpPost("brand/{brandId}/follow")]
        [Authorize(Roles = "Customer")]
        public async Task<IActionResult> FollowBrand(int brandId)
        {
            var customerIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (!int.TryParse(customerIdClaim, out int customerId))
            {
                return Unauthorized(new { message = "Token không hợp lệ." });
            }

            var existing = await _db.BrandFollows
                .FirstOrDefaultAsync(f => f.BrandId == brandId && f.CustomerId == customerId);

            if (existing != null)
            {
                return BadRequest(new { message = "Bạn đã follow brand này rồi." });
            }

            _db.BrandFollows.Add(new BrandFollow { BrandId = brandId, CustomerId = customerId });
            await _db.SaveChangesAsync();

            return Ok(new { message = "Theo dõi brand thành công!" });
        }

        // DELETE: api/reels/brand/{brandId}/unfollow
        [HttpDelete("brand/{brandId}/unfollow")]
        [Authorize(Roles = "Customer")]
        public async Task<IActionResult> UnfollowBrand(int brandId)
        {
            var customerIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (!int.TryParse(customerIdClaim, out int customerId))
            {
                return Unauthorized(new { message = "Token không hợp lệ." });
            }

            var existing = await _db.BrandFollows
                .FirstOrDefaultAsync(f => f.BrandId == brandId && f.CustomerId == customerId);

            if (existing == null)
            {
                return NotFound(new { message = "Bạn chưa follow brand này." });
            }

            _db.BrandFollows.Remove(existing);
            await _db.SaveChangesAsync();

            return Ok(new { message = "Hủy theo dõi thành công!" });
        }

        // GET: api/reels/brand/{brandId}/products?page=1
        [HttpGet("brand/{brandId}/products")]
        public async Task<IActionResult> GetBrandProducts(int brandId, [FromQuery] int page = 1)
        {
            const int pageSize = 20;

            var brand = await _db.Brands.FindAsync(brandId);
            if (brand == null)
            {
                return NotFound(new { message = "Brand không tồn tại." });
            }

            var products = await _db.Products
                .Where(p => p.BrandId == brandId)
                .OrderByDescending(p => p.CreatedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(p => new
                {
                    p.ProductId,
                    p.Name,
                    p.PriceSell,
                    p.Image
                })
                .ToListAsync();

            return Ok(products);
        }

        // GET: api/reels/brand/my-videos?page=1
        [HttpGet("brand/my-videos")]
        [Authorize(Roles = "Brand")]
        public async Task<IActionResult> GetMyVideos([FromQuery] int page = 1)
        {
            const int pageSize = 10;
            var brandId = int.Parse(User.FindFirst("BrandId")!.Value);

            var videos = await _db.ShortVideos
                .Where(v => v.BrandId == brandId)
                .Include(v => v.VideoProducts).ThenInclude(vp => vp.Product)
                .OrderByDescending(v => v.CreatedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(v => new
                {
                    v.VideoId,
                    v.VideoUrl,
                    v.ThumbnailUrl,
                    v.Caption,
                    v.Views,
                    v.Likes,
                    v.CommentsCount,
                    v.Shares,
                    v.CreatedAt,
                    Products = v.VideoProducts.Select(p => new
                    {
                        p.Product.ProductId,
                        p.Product.Name,
                        p.Product.PriceSell,
                        p.Product.Image,
                        p.X,
                        p.Y
                    }).ToList()
                })
                .ToListAsync();

            return Ok(videos);
        }

        // PUT: api/reels/{videoId}
        [HttpPut("{videoId}")]
        [Authorize(Roles = "Brand")]
        public async Task<IActionResult> UpdateVideo(int videoId, [FromBody] UpdateVideoDTO dto)
        {
            var brandId = int.Parse(User.FindFirst("BrandId")!.Value);
            var video = await _db.ShortVideos.FindAsync(videoId);

            if (video == null)
            {
                return NotFound(new { message = "Video không tồn tại." });
            }

            if (video.BrandId != brandId)
            {
                return Forbid();
            }

            video.Caption = dto.Caption ?? video.Caption;
            _db.ShortVideos.Update(video);
            await _db.SaveChangesAsync();

            return Ok(new { message = "Cập nhật video thành công!" });
        }

        // DELETE: api/reels/{videoId}
        [HttpDelete("{videoId}")]
        [Authorize(Roles = "Brand")]
        public async Task<IActionResult> DeleteVideo(int videoId)
        {
            var brandId = int.Parse(User.FindFirst("BrandId")!.Value);
            var video = await _db.ShortVideos
                .Include(v => v.VideoProducts)
                .Include(v => v.VideoLikes)
                .Include(v => v.VideoComments)
                .FirstOrDefaultAsync(v => v.VideoId == videoId);

            if (video == null)
            {
                return NotFound(new { message = "Video không tồn tại." });
            }

            if (video.BrandId != brandId)
            {
                return Forbid();
            }

            try
            {
                if (!string.IsNullOrEmpty(video.VideoUrl))
                {
                    var videoPath = Path.Combine(_environment.ContentRootPath, video.VideoUrl.TrimStart('/'));
                    if (System.IO.File.Exists(videoPath))
                        System.IO.File.Delete(videoPath);
                }

                if (!string.IsNullOrEmpty(video.ThumbnailUrl))
                {
                    var thumbnailPath = Path.Combine(_environment.ContentRootPath, video.ThumbnailUrl.TrimStart('/'));
                    if (System.IO.File.Exists(thumbnailPath))
                        System.IO.File.Delete(thumbnailPath);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting video files for VideoId={VideoId}", videoId);
            }

            _db.VideoProducts.RemoveRange(video.VideoProducts);
            _db.VideoLikes.RemoveRange(video.VideoLikes);
            _db.VideoComments.RemoveRange(video.VideoComments);
            _db.ShortVideos.Remove(video);
            await _db.SaveChangesAsync();

            _logger.LogInformation("Video deleted: VideoId={VideoId}, BrandId={BrandId}", videoId, brandId);
            return Ok(new { message = "Xóa video thành công!" });
        }

        // GET: api/reels/brands (Get all brands for dropdown)
        [HttpGet("brands")]
        public async Task<IActionResult> GetAllBrands()
        {
            try
            {
                var brands = await _db.Brands
                    .Select(b => new
                    {
                        b.BrandId,
                        b.Name
                    })
                    .OrderBy(b => b.Name)
                    .ToListAsync();

                return Ok(brands);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching brands");
                return StatusCode(500, new { message = "Lỗi khi lấy danh sách brands.", error = ex.Message });
            }
        }

        // GET: api/reels/videos (Get all videos)
        [HttpGet("videos")]
        [Produces("application/json")]
        public async Task<IActionResult> GetAllVideos([FromQuery] int page = 1, [FromQuery] int pageSize = 10)
        {
            try
            {
                var accessibleBrandIds = await GetAccessibleBrandIdsAsync();

                var totalVideosQuery = _db.ShortVideos.AsQueryable();
                if (accessibleBrandIds != null)
                    totalVideosQuery = totalVideosQuery.Where(v => accessibleBrandIds.Contains(v.BrandId));

                var totalVideos = await totalVideosQuery.CountAsync();
                var totalPages = (totalVideos + pageSize - 1) / pageSize;

                var videosQuery = _db.ShortVideos
                    .Include(v => v.Brand)
                    .Include(v => v.VideoProducts).ThenInclude(vp => vp.Product)
                    .OrderByDescending(v => v.CreatedAt)
                    .AsQueryable();

                if (accessibleBrandIds != null)
                    videosQuery = videosQuery.Where(v => accessibleBrandIds.Contains(v.BrandId));

                var videos = await videosQuery
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .Select(v => new
                    {
                        v.VideoId,
                        v.VideoUrl,
                        v.ThumbnailUrl,
                        v.Caption,
                        v.Views,
                        v.Likes,
                        v.CommentsCount,
                        v.Shares,
                        v.CreatedAt,
                        Brand = new
                        {
                            v.Brand.BrandId,
                            v.Brand.Name
                        },
                        Products = v.VideoProducts.Select(p => new
                        {
                            p.Product.ProductId,
                            p.Product.Name,
                            p.Product.PriceSell,
                            p.Product.Image,
                            p.X,
                            p.Y
                        }).ToList()
                    })
                    .ToListAsync();

                return Ok(new
                {
                    totalVideos,
                    totalPages,
                    currentPage = page,
                    pageSize,
                    videos
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching all videos");
                return StatusCode(500, new { message = "Lỗi khi lấy danh sách videos.", error = ex.Message });
            }
        }

        // GET: api/reels/videos/{videoId} (Get single video by ID)
        [HttpGet("videos/{videoId}")]
        [Produces("application/json")]
        public async Task<IActionResult> GetVideoById(int videoId)
        {
            try
            {
                var video = await _db.ShortVideos
                    .Include(v => v.Brand)
                    .Include(v => v.VideoProducts).ThenInclude(vp => vp.Product)
                    .FirstOrDefaultAsync(v => v.VideoId == videoId);

                if (video == null)
                {
                    return NotFound(new { message = "Video không tồn tại." });
                }

                var accessibleBrandIds = await GetAccessibleBrandIdsAsync();
                if (accessibleBrandIds != null && !accessibleBrandIds.Contains(video.BrandId))
                    return Forbid();

                var result = new
                {
                    video.VideoId,
                    video.VideoUrl,
                    video.ThumbnailUrl,
                    video.Caption,
                    video.Views,
                    video.Likes,
                    video.CommentsCount,
                    video.Shares,
                    video.CreatedAt,
                    Brand = new
                    {
                        video.Brand.BrandId,
                        video.Brand.Name
                    },
                    Products = video.VideoProducts.Select(p => new
                    {
                        p.Product.ProductId,
                        p.Product.Name,
                        p.Product.PriceSell,
                        p.Product.Image,
                        p.X,
                        p.Y
                    }).ToList()
                };

                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching video by id");
                return StatusCode(500, new { message = "Lỗi khi lấy video.", error = ex.Message });
            }
        }

        // GET: api/reels/brand/{brandId}/videos (Get videos by brand)
        [HttpGet("brand/{brandId}/videos")]
        public async Task<IActionResult> GetVideosByBrand(int brandId, [FromQuery] int page = 1, [FromQuery] int pageSize = 10)
        {
            try
            {
                var brand = await _db.Brands.FindAsync(brandId);
                if (brand == null)
                {
                    return NotFound(new { message = "Brand không tồn tại." });
                }

                var accessibleBrandIds = await GetAccessibleBrandIdsAsync();
                if (accessibleBrandIds != null && !accessibleBrandIds.Contains(brandId))
                    return Forbid();

                var totalVideos = await _db.ShortVideos
                    .Where(v => v.BrandId == brandId)
                    .CountAsync();
                var totalPages = (totalVideos + pageSize - 1) / pageSize;

                var videos = await _db.ShortVideos
                    .Where(v => v.BrandId == brandId)
                    .Include(v => v.VideoProducts).ThenInclude(vp => vp.Product)
                    .OrderByDescending(v => v.CreatedAt)
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .Select(v => new
                    {
                        v.VideoId,
                        v.VideoUrl,
                        v.ThumbnailUrl,
                        v.Caption,
                        v.Views,
                        v.Likes,
                        v.CommentsCount,
                        v.Shares,
                        v.CreatedAt,
                        Products = v.VideoProducts.Select(p => new
                        {
                            p.Product.ProductId,
                            p.Product.Name,
                            p.Product.PriceSell,
                            p.Product.Image,
                            p.X,
                            p.Y
                        }).ToList()
                    })
                    .ToListAsync();

                return Ok(new
                {
                    brandId,
                    brandName = brand.Name,
                    totalVideos,
                    totalPages,
                    currentPage = page,
                    pageSize,
                    videos
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching videos by brand");
                return StatusCode(500, new { message = "Lỗi khi lấy danh sách videos.", error = ex.Message });
            }
        }

        // GET: api/reels/brand/{brandId}/stats (Get brand video stats)
        [HttpGet("brand/{brandId}/stats")]
        public async Task<IActionResult> GetBrandStats(int brandId)
        {
            try
            {
                var brand = await _db.Brands.FindAsync(brandId);
                if (brand == null)
                {
                    return NotFound(new { message = "Brand không tồn tại." });
                }

                var accessibleBrandIds = await GetAccessibleBrandIdsAsync();
                if (accessibleBrandIds != null && !accessibleBrandIds.Contains(brandId))
                    return Forbid();

                var videos = await _db.ShortVideos
                    .Where(v => v.BrandId == brandId)
                    .ToListAsync();

                var stats = new
                {
                    brandId,
                    brandName = brand.Name,
                    totalVideos = videos.Count,
                    totalLikes = videos.Sum(v => v.Likes),
                    totalComments = videos.Sum(v => v.CommentsCount),
                    totalShares = videos.Sum(v => v.Shares),
                    totalViews = videos.Sum(v => v.Views)
                };

                return Ok(stats);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching brand stats");
                return StatusCode(500, new { message = "Lỗi khi lấy thống kê brand.", error = ex.Message });
            }
        }

        // GET: api/reels/brand/{brandId}/profile (Get detailed brand profile with video stats)
        [HttpGet("brand/{brandId}/profile")]
        public async Task<IActionResult> GetBrandProfile(int brandId)
        {
            try
            {
                var brand = await _db.Brands
                    .Include(b => b.Owner)
                    .FirstOrDefaultAsync(b => b.BrandId == brandId);

                if (brand == null)
                {
                    return NotFound(new { message = "Brand không tồn tại." });
                }

                var accessibleBrandIds = await GetAccessibleBrandIdsAsync();
                if (accessibleBrandIds != null && !accessibleBrandIds.Contains(brandId))
                    return Forbid();

                var videos = await _db.ShortVideos
                    .Where(v => v.BrandId == brandId)
                    .ToListAsync();

                var followers = await _db.BrandFollows
                    .Where(bf => bf.BrandId == brandId)
                    .CountAsync();

                var profile = new
                {
                    brandId = brand.BrandId,
                    brandName = brand.Name,
                    ownerName = brand.Owner?.Name ?? "Không rõ",
                    followers,
                    statistics = new
                    {
                        totalVideos = videos.Count,
                        totalViews = videos.Sum(v => v.Views),
                        totalLikes = videos.Sum(v => v.Likes),
                        totalComments = videos.Sum(v => v.CommentsCount),
                        totalShares = videos.Sum(v => v.Shares),
                        averageViews = videos.Count > 0 ? videos.Average(v => v.Views) : 0,
                        averageLikes = videos.Count > 0 ? videos.Average(v => v.Likes) : 0
                    },
                    recentVideos = await _db.ShortVideos
                        .Where(v => v.BrandId == brandId)
                        .OrderByDescending(v => v.CreatedAt)
                        .Take(5)
                        .Select(v => new
                        {
                            v.VideoId,
                            v.Caption,
                            v.Views,
                            v.Likes,
                            v.CommentsCount,
                            v.Shares,
                            v.CreatedAt,
                            v.ThumbnailUrl
                        })
                        .ToListAsync()
                };

                return Ok(profile);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching brand profile");
                return StatusCode(500, new { message = "Lỗi khi lấy profile brand.", error = ex.Message });
            }
        }

    } // end ShortVideoController

    // DTOs
    public class UploadVideoDTO
    {
        public int BrandId { get; set; }
        public IFormFile VideoFile { get; set; } = null!;
        public IFormFile ThumbnailFile { get; set; } = null!;
        public string? Caption { get; set; }
    }

    public class UpdateVideoDTO
    {
        public string? Caption { get; set; }
    }

    public class ProductTagDTO
    {
        public int ProductId { get; set; }
        public double X { get; set; }
        public double Y { get; set; }
    }

    public class VideoCommentDTO
    {
        public string Content { get; set; } = string.Empty;
    }
}
