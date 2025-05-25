using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace HakiBaVuong.Models
{
    public class ProductImage
    {
        [Key]
        public int ImageId { get; set; }

        [Required]
        public int ProductId { get; set; }

        [Required]
        public string ImageUrl { get; set; } // e.g., "/images/product_1_1.jpg"

        public DateTime CreatedAt { get; set; } = DateTime.Now;

        [ForeignKey("ProductId")]
        public virtual Product Product { get; set; }
    }
}