using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using GenerativeAI; 
using GenerativeAI.Types; 
using HakiBaVuong.Models;
using System.Text;
using GenerativeAI;
using System;
using HakiBaVuong.Data;
using System.Text.Json;

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
            var apiKey = gemini["ApiKey"] ?? throw new InvalidOperationException("Thiếu Gemini:ApiKey");
            var model = gemini["Model"] ?? "gemini-2.0-flash-exp"; // Mặc định model mới nhất
            var baseUrl = gemini["BaseUrl"] ?? "https://generativelanguage.googleapis.com/v1/models/";
            var endpoint = gemini["Endpoint"] ?? ":generateContent";

            _geminiUrl = $"{baseUrl}{model}{endpoint}?key={apiKey}";
            _httpClient = new HttpClient();
        }

        [HttpPost("ask")]
        public async Task<IActionResult> Ask([FromBody] ChatRequest request)
        {
            if (string.IsNullOrWhiteSpace(request?.Question))
                return BadRequest("Hỏi gì đi nè bạn ơi");

            var question = request.Question.Trim();
            var userKey = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "anonymous";

            var cacheKey = $"ans_{userKey}_{question.GetHashCode()}";
            if (_cache.TryGetValue(cacheKey, out string cached))
                return Ok(new { answer = cached + " (lấy nhanh từ bộ nhớ)", fromCache = true });

            var history = GetHistory(userKey);
            var context = await BuildContext(question);
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
                : "Chưa có lịch sử.";

            var prompt = $$"""
                Bạn là trợ lý bán hàng siêu dễ thương của shop HakiBaVuong
                Trả lời bằng tiếng Việt, giọng trẻ trung, hay dùng emoji, cực kỳ thân thiện

                {{historyText}}

                Dữ liệu shop hiện có:
                {{dbContext}}

                Câu hỏi mới nhất: {{question}}

                Trả lời tự nhiên, ngắn gọn, hấp dẫn, không dùng icon, emoji. Nếu đã nói rồi thì thêm "Như mình có nói lúc nãy..." cho thân thiện nhé!
                Nếu không có thông tin thì nói thật "Mình chưa có thông tin này ạ" chứ đừng bịa nha!
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
                var json = JsonSerializer.Serialize(requestBody);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                var response = await _httpClient.PostAsync(_geminiUrl, content);
                var responseText = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                    return $"Lỗi Gemini: {response.StatusCode} - {responseText}";

                using var doc = JsonDocument.Parse(responseText);
                var root = doc.RootElement;
                var text = root
                    .GetProperty("candidates")[0]
                    .GetProperty("content")
                    .GetProperty("parts")[0]
                    .GetProperty("text")
                    .GetString();

                return string.IsNullOrWhiteSpace(text)
                    ? "Mình đang hơi lag, bạn hỏi lại giúp mình nha"
                    : text.Trim();
            }
            catch (Exception ex)
            {
                // Log lỗi nếu cần: Console.WriteLine(ex);
                return "Oops! Bot đang lỗi nhẹ, thử lại sau 5s nha";
            }
        }

        private async Task<string> BuildContext(string question)
        {
            var sb = new StringBuilder();
            var lower = question.ToLowerInvariant();

            try
            {
                var brands = await _db.Brands.Select(b => b.Name).ToListAsync();
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

                if (brands.Any())
                    sb.AppendLine("THƯƠNG HIỆU: " + string.Join(", ", brands));

                var relevant = products.AsQueryable();
                if (lower.Contains("apple") || lower.Contains("iphone"))
                    relevant = relevant.Where(p => p.Brand.Contains("Apple", StringComparison.OrdinalIgnoreCase) || p.Name.Contains("iPhone"));
                else if (lower.Contains("nike"))
                    relevant = relevant.Where(p => p.Brand.Contains("Nike", StringComparison.OrdinalIgnoreCase));
                else if (lower.Contains("samsung") || lower.Contains("điện thoại"))
                    relevant = relevant.Where(p => p.Cats.Any(c => c.Contains("Điện thoại", StringComparison.OrdinalIgnoreCase)));

                var list = relevant.Any() ? relevant.Take(30) : products.Take(20);

                sb.AppendLine("\nSẢN PHẨM:");
                foreach (var p in list)
                {
                    var cat = p.Cats.Any() ? string.Join(", ", p.Cats) : "Chưa phân loại";
                    sb.AppendLine($"• {p.Name} | {p.PriceSell:N0}đ | {p.Rating} sao | {p.Brand} | {cat}");
                }
            }
            catch
            {
                sb.AppendLine("Hiện tại chưa có dữ liệu sản phẩm.");
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