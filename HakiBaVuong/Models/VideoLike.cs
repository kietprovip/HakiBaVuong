using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;

namespace HakiBaVuong.Models
{
    public class VideoLike
    {
        [Key]
        [Column(Order = 0)]
        public int VideoId { get; set; }

        [Key]
        [Column(Order = 1)]
        public int CustomerId { get; set; }

        public DateTime LikedAt { get; set; } = DateTime.UtcNow;

        [ForeignKey("CustomerId")]
        public virtual Customer Customer { get; set; } = null!;

        [ForeignKey("VideoId")]
        public virtual ShortVideo Video { get; set; } = null!;
    }
}
