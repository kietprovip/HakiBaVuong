using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
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
            var model = gemini["Model"] ?? "gemini-2.0-flash-exp"; // Model mới nhất, hỗ trợ ảnh
            var baseUrl = gemini["BaseUrl"] ?? "https://generativelanguage.googleapis.com/v1/models/";
            var endpoint = gemini["Endpoint"] ?? ":generateContent";

            _geminiUrl = $"{baseUrl}{model}{endpoint}?key={apiKey}";
            _httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(40) };
        }

        [HttpPost("ask")]
        public async Task<IActionResult> Ask([FromBody] ChatRequest request)
        {
            if (string.IsNullOrWhiteSpace(request?.Question) && 
                (request?.ImageUrls == null || !request.ImageUrls.Any()) &&
                (request?.Base64Images == null || !request.Base64Images.Any()))
                return BadRequest("Hỏi gì đi nè bạn ơi!");

            var question = request.Question?.Trim() ?? "Mô tả sản phẩm trong ảnh giúp mình nhé";
            var userKey = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "anonymous";

            var customerIdClaim = User.FindFirst("CustomerId")?.Value;
            int? customerId = int.TryParse(customerIdClaim, out var cid) ? cid : null;

            var cacheKey = $"ans_{userKey}_{question.GetHashCode()}";
            if (_cache.TryGetValue(cacheKey, out string cached))
                return Ok(new { answer = cached + " (lấy nhanh từ bộ nhớ)", fromCache = true });

            var history = GetHistory(userKey);
            var context = await BuildContext(question, customerId);
            var answer = await CallGemini(question, history, context, request.ImageUrls, request.Base64Images);

            history.Add(new ChatMessage { Question = question, Answer = answer });
            if (history.Count > 15) history.RemoveAt(0);

            _cache.Set(userKey, history, TimeSpan.FromHours(2));
            _cache.Set(cacheKey, answer, TimeSpan.FromMinutes(10));

            return Ok(new { answer, fromCache = false });
        }

        private async Task<string> CallGemini(string question, List<ChatMessage> history, string dbContext, List<string>? imageUrls = null, List<string>? base64Images = null)
        {
            var historyText = history.Any()
                ? "Lịch sử chat gần đây:\n" + string.Join("\n", history.TakeLast(8).Select(h => $"Khách: {h.Question}\nBot: {h.Answer}"))
                : "Chưa có lịch sử chat.";

            var prompt = $$"""
                Bạn là trợ lý bán hàng siêu dễ thương, nhiệt tình của shop HakiBaVuong
                Trả lời bằng tiếng Việt, giọng trẻ trung, gần gũi, KHÔNG dùng emoji, không dùng icon
                {{historyText}}

                Dữ liệu cửa hàng hiện tại (dùng để gợi ý chính xác):
                {{dbContext}}

                Câu hỏi mới nhất của khách: {{question}}

                Hãy trả lời tự nhiên, ngắn gọn, hấp dẫn,  KHÔNG dùng emoji, không dùng icon.
                Nếu khách gửi ảnh sản phẩm thì mô tả chính xác và gợi ý hàng đang bán.
                Nếu đã nói rồi thì thêm "Như mình có nói lúc nãy..." cho thân thiện.
                Nếu không biết thì nói thật "Mình chưa có thông tin này ạ" chứ không bịa đặt!
                """;

            var parts = new List<object> { new { text = prompt } };

            // Xử lý URL images (tải từ internet)
            if (imageUrls != null && imageUrls.Any())
            {
                foreach (var url in imageUrls.Take(3)) 
                {
                    // Kiểm tra nếu là Data URI (base64 dạng URL)
                    if (url.StartsWith("data:"))
                    {
                        var (mimeType, base64Data) = ExtractBase64FromDataUri(url);
                        if (!string.IsNullOrEmpty(base64Data))
                        {
                            parts.Add(new
                            {
                                inline_data = new
                                {
                                    mime_type = mimeType,
                                    data = base64Data
                                }
                            });
                        }
                    }
                    else
                    {
                        // URL thông thường - tải từ internet
                        var imageBytes = await DownloadImageAsync(url);
                        if (imageBytes != null)
                        {
                            parts.Add(new
                            {
                                inline_data = new
                                {
                                    mime_type = GetMimeType(url),
                                    data = Convert.ToBase64String(imageBytes)
                                }
                            });
                        }
                    }
                }
            }

            // Xử lý Base64 images (gửi trực tiếp từ FE)
            if (base64Images != null && base64Images.Any())
            {
                foreach (var base64String in base64Images.Take(3))
                {
                    // Base64 string có thể ở 2 dạng:
                    // 1. "data:image/jpeg;base64,/9j/4AAQSkZJ..." (Data URI)
                    // 2. "/9j/4AAQSkZJ..." (pure base64)

                    if (base64String.StartsWith("data:"))
                    {
                        var (mimeType, base64Data) = ExtractBase64FromDataUri(base64String);
                        if (!string.IsNullOrEmpty(base64Data))
                        {
                            parts.Add(new
                            {
                                inline_data = new
                                {
                                    mime_type = mimeType,
                                    data = base64Data
                                }
                            });
                        }
                    }
                    else
                    {
                        // Pure base64 - giả định là JPEG nếu không có MIME type
                        parts.Add(new
                        {
                            inline_data = new
                            {
                                mime_type = "image/jpeg",
                                data = base64String
                            }
                        });
                    }
                }
            }

            var requestBody = new
            {
                contents = new[] { new { role = "user", parts } },
                generationConfig = new
                {
                    temperature = 0.85,
                    topK = 40,
                    topP = 0.95,
                    maxOutputTokens = 1024
                }
            };

            try
            {
                var json = JsonSerializer.Serialize(requestBody, new JsonSerializerOptions
                {
                    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
                });

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
                    ? "Mình đang xử lý ảnh một chút, bạn đợi mình xíu nha!"
                    : text.Trim();
            }
            catch (Exception ex)
            {

                return "Oops! Bot đang lỗi nhẹ, bạn thử lại sau 5 giây nha!";
            }
        }

        private (string mimeType, string base64Data) ExtractBase64FromDataUri(string dataUri)
        {
            try
            {
                // Format: "data:image/jpeg;base64,/9j/4AAQSkZJ..."
                if (!dataUri.StartsWith("data:"))
                    return ("image/jpeg", dataUri);

                var parts = dataUri.Split(',');
                if (parts.Length != 2)
                    return ("image/jpeg", "");

                var mimeTypePart = parts[0]; // "data:image/jpeg;base64"
                var base64Data = parts[1];   // "/9j/4AAQSkZJ..."

                var mimeType = "image/jpeg"; // default
                if (mimeTypePart.Contains("image/png"))
                    mimeType = "image/png";
                else if (mimeTypePart.Contains("image/webp"))
                    mimeType = "image/webp";
                else if (mimeTypePart.Contains("image/gif"))
                    mimeType = "image/gif";

                return (mimeType, base64Data);
            }
            catch
            {
                return ("image/jpeg", "");
            }
        }

        private async Task<byte[]?> DownloadImageAsync(string url)
        {
            try
            {
                return await _httpClient.GetByteArrayAsync(url);
            }
            catch
            {
                return null;
            }
        }

        private string GetMimeType(string url)
        {
            return url.Contains(".png", StringComparison.OrdinalIgnoreCase) ? "image/png" :
                   url.Contains(".webp", StringComparison.OrdinalIgnoreCase) ? "image/webp" :
                   "image/jpeg";
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
                    .Where(oi => oi.ProductId.HasValue && oi.ProductName != null)
                    .GroupBy(oi => oi.ProductName!)
                    .Select(g => new { Name = g.Key, TotalSold = g.Sum(x => x.Quantity) })
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
                        .Where(oi => oi.Product.Brand.Name != null)
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
                sb.AppendLine("Dữ liệu đang được cập nhật...");
            }

            return sb.ToString();
        }

        private List<ChatMessage> GetHistory(string key)
            => _cache.TryGetValue(key, out List<ChatMessage> h) ? h : new List<ChatMessage>();
    }

    public class ChatRequest
    {
        public string Question { get; set; } = "";
        public List<string>? ImageUrls { get; set; }
        public List<string>? Base64Images { get; set; }
    }

    public class ChatMessage
    {
        public string Question { get; set; } = "";
        public string Answer { get; set; } = "";
    }
}