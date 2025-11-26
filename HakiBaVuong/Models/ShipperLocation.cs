using System.ComponentModel.DataAnnotations;

namespace HakiBaVuong.Models
{
    public class ShipperLocation
    {
        [Key]
        public int ShipperId { get; set; }
        public decimal Latitude { get; set; }
        public decimal Longitude { get; set; }
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        public virtual Shipper Shipper { get; set; } = null!;
    }
}
