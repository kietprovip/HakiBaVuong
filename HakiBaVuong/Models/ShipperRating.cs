using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace HakiBaVuong.Models
{
    public class ShipperRating
    {
        [Key]
        public int RatingId { get; set; }

        public int ShipperId { get; set; }

        public int CustomerId { get; set; }

        public int OrderId { get; set; }

        [Range(1, 5)]
        public int Score { get; set; }  // 1-5 sao

        public string Comment { get; set; }

        public DateTime RatedAt { get; set; } = DateTime.UtcNow;

        [ForeignKey("ShipperId")]
        public virtual Shipper Shipper { get; set; }

        [ForeignKey("CustomerId")]
        public virtual Customer Customer { get; set; }

        [ForeignKey("OrderId")]
        public virtual Order Order { get; set; }
    }
}