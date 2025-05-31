using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace HakiBaVuong.Models
{
    public class Wishlist
    {
        [Key]
        [Column(Order = 0)]
        public int CustomerId { get; set; }

        [Key]
        [Column(Order = 1)]
        public int ProductId { get; set; }

        public DateTime AddedAt { get; set; } = DateTime.UtcNow;

        [ForeignKey("CustomerId")]
        public virtual Customer Customer { get; set; }

        [ForeignKey("ProductId")]
        public virtual Product Product { get; set; }
    }
}