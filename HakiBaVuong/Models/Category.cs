using System.ComponentModel.DataAnnotations;

namespace HakiBaVuong.Models
{
    public class Category
    {
        [Key]
        public int CategoryId { get; set; }

        [Required]
        public string Name { get; set; }

        public string Description { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;


        public virtual ICollection<ProductCategory> ProductCategories { get; set; } = new List<ProductCategory>();
    }
}