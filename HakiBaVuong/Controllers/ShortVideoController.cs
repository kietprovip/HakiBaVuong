using AutoMapper;
using HakiBaVuong.Data;
using HakiBaVuong.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HakiBaVuong.Controllers
{
    [ApiController]
    [Route("api/reels")]
    public class ShortVideoController : ControllerBase
    {
        private readonly DataContext _db;
        private readonly IMapper _mapper;

        public ShortVideoController(DataContext db, IMapper mapper)
        {
            _db = db;
            _mapper = mapper;
        }

        // GET: api/reels/feed?page=1&customerId=123
        [HttpGet("feed")]
        public async Task<IActionResult> GetFeed([FromQuery] int page = 1, [FromQuery] int? customerId = null)
        {
            const int pageSize = 10;

            var query = _db.ShortVideos
                .Include(v => v.Brand)
                .Include(v => v.VideoProducts).ThenInclude(vp => vp.Product)
                .OrderByDescending(v => v.CreatedAt);

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

        // POST: api/reels/upload (Brand đăng video)
        [HttpPost("upload")]
        [Authorize(Roles = "Brand")]
        public async Task<IActionResult> Upload([FromForm] UploadVideoDTO dto)
        {
            var brandId = int.Parse(User.FindFirst("BrandId")!.Value);

            var video = new ShortVideo
            {
                BrandId = brandId,
                VideoUrl = dto.VideoUrl,           // URL từ Cloudinary/S3
                ThumbnailUrl = dto.ThumbnailUrl,
                Caption = dto.Caption ?? ""
            };

            _db.ShortVideos.Add(video);
            await _db.SaveChangesAsync();

            if (dto.ProductTags != null)
            {
                foreach (var tag in dto.ProductTags)
                {
                    _db.VideoProducts.Add(new VideoProduct
                    {
                        VideoId = video.VideoId,
                        ProductId = tag.ProductId,
                        X = tag.X,
                        Y = tag.Y
                    });
                }
                await _db.SaveChangesAsync();
            }

            return Ok(new { video.VideoId, message = "Đăng video thành công!" });
        }

        // POST: api/reels/{id}/like
        [HttpPost("{id}/like")]
        [Authorize(Roles = "Customer")]
        public async Task<IActionResult> ToggleLike(int id)
        {
            var customerId = int.Parse(User.FindFirst("CustomerId")!.Value);

            var existing = await _db.VideoLikes
                .FirstOrDefaultAsync(l => l.VideoId == id && l.CustomerId == customerId);

            if (existing == null)
            {
                _db.VideoLikes.Add(new VideoLike { VideoId = id, CustomerId = customerId });
                await _db.Database.ExecuteSqlRawAsync("UPDATE ShortVideos SET Likes = Likes + 1 WHERE VideoId = {0}", id);
            }
            else
            {
                _db.VideoLikes.Remove(existing);
                await _db.Database.ExecuteSqlRawAsync("UPDATE ShortVideos SET Likes = Likes - 1 WHERE VideoId = {0}", id);
            }

            await _db.SaveChangesAsync();
            return Ok();
        }

        // POST: api/reels/{id}/comment
        [HttpPost("{id}/comment")]
        [Authorize(Roles = "Customer")]
        public async Task<IActionResult> Comment(int id, [FromBody] VideoCommentDTO dto)
        {
            var customerId = int.Parse(User.FindFirst("CustomerId")!.Value);
            var customer = await _db.Customers.FindAsync(customerId);

            var comment = new VideoComment
            {
                VideoId = id,
                CustomerId = customerId,
                CustomerName = customer?.Name ?? "Khách",
                Content = dto.Content
            };

            _db.VideoComments.Add(comment);
            await _db.Database.ExecuteSqlRawAsync("UPDATE ShortVideos SET CommentsCount = CommentsCount + 1 WHERE VideoId = {0}", id);
            await _db.SaveChangesAsync();

            return Ok(comment);
        }

        // POST: api/reels/brand/{brandId}/follow
        [HttpPost("brand/{brandId}/follow")]
        [Authorize(Roles = "Customer")]
        public async Task<IActionResult> FollowBrand(int brandId)
        {
            var customerId = int.Parse(User.FindFirst("CustomerId")!.Value);

            if (!_db.BrandFollows.Any(f => f.BrandId == brandId && f.CustomerId == customerId))
            {
                _db.BrandFollows.Add(new BrandFollow { BrandId = brandId, CustomerId = customerId });
                await _db.SaveChangesAsync();
            }

            return Ok();
        }
    }

    // DTOs
    public class UploadVideoDTO
    {
        public string VideoUrl { get; set; } = string.Empty;
        public string ThumbnailUrl { get; set; } = string.Empty;
        public string? Caption { get; set; }
        public List<ProductTagDTO>? ProductTags { get; set; }
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
