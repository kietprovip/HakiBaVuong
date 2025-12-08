using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace HakiBaVuong.Models
{
    public class VideoComment
    {
        [Key]
        [Column(Order = 0)]
        public int VideoId { get; set; }

        [Key]
        [Column(Order = 1)]
        public int? CustomerId { get; set; }

        [ForeignKey("VideoId")]
        public virtual ShortVideo Video { get; set; } = null!;

        public string CustomerName { get; set; } = "Khách vãng lai";
        public string Content { get; set; } = string.Empty;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
