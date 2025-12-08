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
        private readonly IWebHostEnvironment _env;
        private readonly ILogger<RecommendationController> _logger;

        private dynamic? _model;
        private int[]? _allProductIds;
        private Dictionary<int, int>? _customerMap;
        private Dictionary<int, int>? _productMap;

        public RecommendationController(DataContext db, IWebHostEnvironment env, ILogger<RecommendationController> logger)
        {
            _db = db;
            _env = env;
            _logger = logger;
            Runtime.PythonDLL = @"C:\Users\GIGABYTE\AppData\Local\Programs\Python\Python311\python311.dll";
            PythonEngine.Initialize();  
            LoadModelFromPkl();
        }

        private void LoadModelFromPkl()
        {
            try
            {
                var modelPath = Path.Combine(Directory.GetCurrentDirectory(), "trainmodel", "lightfm_model.pkl");

                if (!System.IO.File.Exists(modelPath))
                    return;

                var bytes = System.IO.File.ReadAllBytes(modelPath);

                using (Py.GIL())
                {
                    dynamic pickle = Py.Import("pickle");
                    dynamic loaded = pickle.loads(bytes);

                    _model = loaded["model"];
                    dynamic dataset = loaded["dataset"];

                    var mapping = dataset.mapping();
                    _customerMap = mapping[0].As<Dictionary<int, int>>();
                    _productMap = mapping[2].As<Dictionary<int, int>>();
                    _allProductIds = _productMap.Keys.OrderBy(x => x).ToArray();
                }
            }
            catch { }
        }

        [HttpGet("ai-recommend")]
        [Authorize]
        public async Task<IActionResult> GetAiRecommend(int topK = 12)
        {
            var customerIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (!int.TryParse(customerIdClaim, out int customerId))
                return Unauthorized();

            int? userInternalId = null;
            if (_model != null && _customerMap != null && _allProductIds != null)
            {
                _customerMap.TryGetValue(customerId, out var tempId);
                userInternalId = tempId;
            }

            if (userInternalId == null)
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
                    })
                    .ToListAsync();
                return Ok(popular);
            }

            using (Py.GIL())
            {
                var scores = _model.predict(userInternalId.Value, _allProductIds);

                var result = new List<(float score, int productId)>();
                for (int i = 0; i < scores.shape[0]; i++)
                {
                    result.Add((scores[i].As<float>(), _allProductIds[i]));
                }

                var topIds = result
                    .OrderByDescending(x => x.score)
                    .Take(topK)
                    .Select(x => x.productId)
                    .ToList();

                var products = await _db.Products
                    .Where(p => topIds.Contains(p.ProductId))
                    .Select(p => new ProductDTO
                    {
                        ProductId = p.ProductId,
                        Name = p.Name,
                        PriceSell = p.PriceSell,
                        Image = p.Image,
                        Rating = p.Rating
                    })
                    .ToListAsync();

                return Ok(products);
            }
        }

        [HttpPost("reload-model")]
        [Authorize(Roles = "Admin")]
        public IActionResult ReloadModel()
        {
            LoadModelFromPkl();
            return Ok();
        }
    }
}
