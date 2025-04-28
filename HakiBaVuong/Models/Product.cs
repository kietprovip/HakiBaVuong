using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;

namespace HakiBaVuong.Models
{
    public class Product
    {
        [Key]
        public int ProductId { get; set; }

        public int BrandId { get; set; }

        [Required]
        public string Name { get; set; }

        public string Description { get; set; }

        [Required]
        public decimal PriceSell { get; set; }

        public decimal? PriceCost { get; set; }
        public string Image { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.Now;
        public DateTime UpdatedAt { get; set; } = DateTime.Now;

        [ForeignKey("BrandId")]
        public virtual Brand Brand { get; set; }
    }
}
