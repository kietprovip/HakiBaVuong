using System.ComponentModel.DataAnnotations;

namespace HakiBaVuong.Models
{
    public class ProductCategory
    {
        [Key]
        public int ProductCategoryId { get; set; }

        public int ProductId { get; set; }
        public virtual Product Product { get; set; }

        public int CategoryId { get; set; }
        public virtual Category Category { get; set; }

        public DateTime AssignedAt { get; set; } = DateTime.UtcNow;
    }
}