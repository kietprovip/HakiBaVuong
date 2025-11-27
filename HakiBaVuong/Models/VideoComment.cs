using System.ComponentModel.DataAnnotations;

namespace HakiBaVuong.Models
{
    public class VideoComment
    {
        [Key]
        public int CommentId { get; set; }

        public int VideoId { get; set; }
        public virtual ShortVideo Video { get; set; } = null!;

        public int? CustomerId { get; set; }
        public string CustomerName { get; set; } = "Khách vãng lai";
        public string Content { get; set; } = string.Empty;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
