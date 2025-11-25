using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using System.Text;
using System.Text.Json;
using HakiBaVuong.Data;
using HakiBaVuong.Models;

namespace HakiBaVuong.Controllers
{
    [ApiController]
    [Route("api/chatbot")]
    public class ChatbotController : ControllerBase
    {
        private readonly DataContext _db;
        private readonly IMemoryCache _cache;
        private readonly HttpClient _httpClient;
        private readonly string _geminiUrl;

        public ChatbotController(DataContext db, IMemoryCache cache, IConfiguration config)
        {
            _db = db;
            _cache = cache;

            var gemini = config.GetSection("Gemini");
            var apiKey = gemini["ApiKey"] ?? throw new InvalidOperationException("Thiếu Gemini:ApiKey trong appsettings.json");
            var model = gemini["Model"] ?? "gemini-2.0-flash-exp";
            var baseUrl = gemini["BaseUrl"] ?? "https://generativelanguage.googleapis.com/v1/models/";
            var endpoint = gemini["Endpoint"] ?? ":generateContent";

            _geminiUrl = $"{baseUrl}{model}{endpoint}?key={apiKey}";
            _httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
        }

        [HttpPost("ask")]
        public async Task<IActionResult> Ask([FromBody] ChatRequest request)
        {
            if (string.IsNullOrWhiteSpace(request?.Question))
                return BadRequest("Hỏi gì đi nè bạn ơi!");

            var question = request.Question.Trim();
            var userKey = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "anonymous";

  
            var customerIdClaim = User.FindFirst("CustomerId")?.Value;
            int? customerId = int.TryParse(customerIdClaim, out var cid) ? cid : null;

            var cacheKey = $"ans_{userKey}_{question.GetHashCode()}";
            if (_cache.TryGetValue(cacheKey, out string cached))
                return Ok(new { answer = cached + " (lấy nhanh từ bộ nhớ)", fromCache = true });

            var history = GetHistory(userKey);
            var context = await BuildContext(question, customerId);
            var answer = await CallGemini(question, history, context);

            history.Add(new ChatMessage { Question = question, Answer = answer });
            if (history.Count > 15) history.RemoveAt(0);

            _cache.Set(userKey, history, TimeSpan.FromHours(2));
            _cache.Set(cacheKey, answer, TimeSpan.FromMinutes(10));

            return Ok(new { answer, fromCache = false });
        }

        private async Task<string> CallGemini(string question, List<ChatMessage> history, string dbContext)
        {
            var historyText = history.Any()
                ? "Lịch sử chat gần đây:\n" + string.Join("\n", history.TakeLast(8).Select(h => $"Khách: {h.Question}\nBot: {h.Answer}"))
                : "Chưa có lịch sử chat.";

            var prompt = $$"""
                Bạn là trợ lý bán hàng siêu dễ thương, nhiệt tình của shop HakiBaVuong
                Trả lời bằng tiếng Việt, giọng trẻ trung, gần gũi, hay dùng emoji
                {{historyText}}

                Dữ liệu cửa hàng hiện tại (dùng để gợi ý chính xác):
                {{dbContext}}

                Câu hỏi mới nhất của khách: {{question}}

                Hãy trả lời tự nhiên, hấp dẫn, ngắn gọn, không icon, không emoji. 
                Nếu khách hỏi sản phẩm thì gợi ý cụ thể tên + giá + lý do.
                Nếu đã nói rồi thì thêm "Như mình có nói lúc nãy..." cho thân thiện.
                Nếu không có thông tin thì nói thật "Mình chưa có thông tin này ạ" chứ không bịa đặt!
                """;

            var requestBody = new
            {
                contents = new[] { new { role = "user", parts = new[] { new { text = prompt } } } },
                generationConfig = new
                {
                    temperature = 0.9,
                    topK = 40,
                    topP = 0.95,
                    maxOutputTokens = 1024
                }
            };

            try
            {
                var json = JsonSerializer.Serialize(requestBody, new JsonSerializerOptions { DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull });
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                var response = await _httpClient.PostAsync(_geminiUrl, content);
                var responseText = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                    return $"Lỗi Gemini: {response.StatusCode}";

                using var doc = JsonDocument.Parse(responseText);
                var text = doc.RootElement
                    .GetProperty("candidates")[0]
                    .GetProperty("content")
                    .GetProperty("parts")[0]
                    .GetProperty("text")
                    .GetString();

                return string.IsNullOrWhiteSpace(text)
                    ? "Mình đang hơi lag, bạn hỏi lại giúp mình nha!"
                    : text.Trim();
            }
            catch
            {
                return "Oops! Bot đang lỗi nhẹ, bạn thử lại sau 5 giây nha!";
            }
        }

        private async Task<string> BuildContext(string question, int? customerId = null)
        {
            var sb = new StringBuilder();
            var lower = question.ToLowerInvariant();

            try
            {

                var brands = await _db.Brands.Select(b => b.Name).ToListAsync();
                if (brands.Any())
                    sb.AppendLine("THƯƠNG HIỆU: " + string.Join(", ", brands));


                var topSelling = await _db.OrderItems
                    .Where(oi => oi.ProductId.HasValue)
                    .GroupBy(oi => new { oi.ProductId, oi.ProductName })
                    .Select(g => new
                    {
                        Name = g.Key.ProductName,
                        TotalSold = g.Sum(x => x.Quantity)
                    })
                    .OrderByDescending(x => x.TotalSold)
                    .Take(10)
                    .ToListAsync();

                if (topSelling.Any())
                {
                    sb.AppendLine("\nTOP SẢN PHẨM BÁN CHẠY NHẤT:");
                    foreach (var p in topSelling)
                        sb.AppendLine($"• {p.Name} (đã bán {p.TotalSold} cái)");
                }


                if (customerId.HasValue)
                {
                    var customerBrands = await _db.Orders
                        .Where(o => o.CustomerId == customerId.Value)
                        .SelectMany(o => o.OrderItems)
                        .Where(oi => oi.Product != null && oi.Product.Brand != null)
                        .GroupBy(oi => oi.Product!.Brand!.Name)
                        .Select(g => new { Brand = g.Key, Count = g.Count() })
                        .OrderByDescending(x => x.Count)
                        .Take(3)
                        .ToListAsync();

                    if (customerBrands.Any())
                    {
                        sb.AppendLine("\nKHÁCH NÀY HAY MUA:");
                        foreach (var b in customerBrands)
                            sb.AppendLine($"• Thương hiệu {b.Brand} ({b.Count} lần)");
                    }
                }

 
                var products = await _db.Products
                    .Include(p => p.Brand)
                    .Include(p => p.ProductCategories).ThenInclude(pc => pc.Category)
                    .Select(p => new
                    {
                        p.Name,
                        p.PriceSell,
                        p.Rating,
                        Brand = p.Brand.Name ?? "Không rõ",
                        Cats = p.ProductCategories.Select(pc => pc.Category.Name ?? "").ToList()
                    })
                    .ToListAsync();

                var relevant = products.AsQueryable();

                if (lower.Contains("apple") || lower.Contains("iphone") || lower.Contains("mac") || lower.Contains("airpods"))
                    relevant = relevant.Where(p => p.Brand.Contains("Apple", StringComparison.OrdinalIgnoreCase) || p.Name.Contains("iPhone", StringComparison.OrdinalIgnoreCase));
                else if (lower.Contains("nike") || lower.Contains("giày") || lower.Contains("sneaker"))
                    relevant = relevant.Where(p => p.Brand.Contains("Nike", StringComparison.OrdinalIgnoreCase) || p.Cats.Any(c => c.Contains("Giày", StringComparison.OrdinalIgnoreCase)));
                else if (lower.Contains("samsung") || lower.Contains("điện thoại") || lower.Contains("galaxy"))
                    relevant = relevant.Where(p => p.Brand.Contains("Samsung", StringComparison.OrdinalIgnoreCase) || p.Cats.Any(c => c.Contains("Điện thoại", StringComparison.OrdinalIgnoreCase)));
                else if (lower.Contains("áo") || lower.Contains("quần") || lower.Contains("thời trang"))
                    relevant = relevant.Where(p => p.Cats.Any(c => c.Contains("Thời trang", StringComparison.OrdinalIgnoreCase) || c.Contains("Quần áo", StringComparison.OrdinalIgnoreCase)));

                var list = relevant.Any() ? relevant.Take(20) : products.OrderByDescending(p => p.Rating).Take(20);

                sb.AppendLine("\nSẢN PHẨM ĐANG BÁN:");
                foreach (var p in list)
                {
                    var cat = p.Cats.Any() ? string.Join(", ", p.Cats.Take(2)) : "Chưa phân loại";
                    sb.AppendLine($"• {p.Name} | {p.PriceSell:N0}đ | {p.Rating} sao | {p.Brand} | {cat}");
                }
            }
            catch
            {
                sb.AppendLine("Dữ liệu đang được cập nhật, bạn hỏi mình thêm nha!");
            }

            return sb.ToString();
        }

        private List<ChatMessage> GetHistory(string key)
            => _cache.TryGetValue(key, out List<ChatMessage> h) ? h : new List<ChatMessage>();
    }

    public class ChatRequest
    {
        public string Question { get; set; } = "";
    }

    public class ChatMessage
    {
        public string Question { get; set; } = "";
        public string Answer { get; set; } = "";
    }
}