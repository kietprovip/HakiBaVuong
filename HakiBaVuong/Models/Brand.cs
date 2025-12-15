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

        // NOTE: "OwnerId" in Brand is the canonical way to determine which User
        // owns (runs) this brand account. Do NOT confuse with User.BrandId which
        // is only used to mark a User as an employee of a brand.
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
