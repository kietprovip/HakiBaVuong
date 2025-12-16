using System.ComponentModel.DataAnnotations;

namespace HakiBaVuong.Models
{
    public class BrandFollow
    {
        [Key]
        public int Id { get; set; }

        public int BrandId { get; set; }
        public int CustomerId { get; set; }

        public DateTime FollowedAt { get; set; } = DateTime.UtcNow;
    }
}
