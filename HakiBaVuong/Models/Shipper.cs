using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace HakiBaVuong.Models
{
    public class Shipper
    {
        [Key]
        public int ShipperId { get; set; }

        [Required]
        public string Name { get; set; }

        [Required]
        [EmailAddress]
        public string Email { get; set; }

        [Required]
        public string Password { get; set; }

        [Required]
        public string Phone { get; set; }

        public string VehicleType { get; set; }  // e.g., "Xe máy", "Ô tô"

        public string LicensePlate { get; set; }  // Biển số xe

        public double? Rating { get; set; }  // Average rating (1-5)

        public bool IsEmailVerified { get; set; } = false;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        public virtual ICollection<ShipperRating> Ratings { get; set; } = new List<ShipperRating>();

        public virtual ICollection<Order> AssignedOrders { get; set; } = new List<Order>();
    }
}