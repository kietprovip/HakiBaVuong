using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;

namespace HakiBaVuong.Models
{
    public class VideoLike
    {
        [Key]
        public int Id { get; set; }

        public int VideoId { get; set; }
        public int CustomerId { get; set; }

        public DateTime LikedAt { get; set; } = DateTime.UtcNow;

        [ForeignKey("CustomerId")]
        public virtual Customer Customer { get; set; } = null!;
    }
}
