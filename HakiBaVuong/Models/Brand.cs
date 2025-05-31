using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;

namespace HakiBaVuong.Models
{
    public class Brand
    {
        [Key]
        public int BrandId { get; set; }

        [Required]
        public string Name { get; set; }

        public int OwnerId { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.Now;
        public string? BackgroundColor { get; set; }
        public string? BackgroundImageUrl { get; set; }

        [ForeignKey("OwnerId")]
        public virtual User Owner { get; set; }

        public virtual ICollection<Product> Products { get; set; }
        public virtual ICollection<Order> Orders { get; set; }
    }
}
