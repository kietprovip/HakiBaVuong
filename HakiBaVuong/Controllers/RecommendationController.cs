using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using HakiBaVuong.Data;
using HakiBaVuong.DTOs;
using System.Security.Claims;
using Python.Runtime;

namespace HakiBaVuong.Controllers
{
    [ApiController]
    [Route("api/recommendation")]
    public class RecommendationController : ControllerBase
    {
        private readonly DataContext _db;
        private readonly RecommendationService _rcService;

        public RecommendationController(DataContext db, RecommendationService rcService)
        {
            _db = db;
            _rcService = rcService; 
        }

        [HttpGet("ai-recommend")]
        [Authorize]
        public async Task<IActionResult> GetAiRecommend(int topK = 12)
        {
            var customerIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (!int.TryParse(customerIdClaim, out int customerId)) return Unauthorized();

            var topIds = await _rcService.PredictTopKAsync(customerId, topK);

            if (topIds.Count == 0)
            {

                var popular = await _db.Products
                    .OrderByDescending(p => p.Rating)
                    .Take(topK)
                    .Select(p => new ProductDTO
                    {
                        ProductId = p.ProductId,
                        Name = p.Name,
                        PriceSell = p.PriceSell,
                        Image = p.Image,
                        Rating = p.Rating
                    }).ToListAsync();
                return Ok(popular);
            }

            var products = await _db.Products
                .Where(p => topIds.Contains(p.ProductId))
                .Select(p => new ProductDTO
                {
                    ProductId = p.ProductId,
                    Name = p.Name,
                    PriceSell = p.PriceSell,
                    Image = p.Image,
                    Rating = p.Rating
                }).ToListAsync();

            return Ok(products);
        }

        [HttpPost("reload-model")]
        [Authorize(Roles = "Admin")]
        public IActionResult ReloadModel()
        {
            _rcService.ReloadModel();
            return Ok(new { message = "Model reloaded successfully." });
        }
    }

}
