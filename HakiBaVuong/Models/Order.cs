using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace HakiBaVuong.Models
{
    public class Order
    {
        [Key]
        public int OrderId { get; set; }

        public int BrandId { get; set; }
        public int? CustomerId { get; set; }

        public string FullName { get; set; }
        public string Phone { get; set; }
        public string Address { get; set; }

        [Required]
        public string Status { get; set; }

        [Required]
        public decimal TotalAmount { get; set; }

        public int? PaymentId { get; set; }

        public string DeliveryStatus { get; set; }
        public DateTime? EstimatedDeliveryDate { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        [ForeignKey("BrandId")]
        public virtual Brand Brand { get; set; }

        [ForeignKey("CustomerId")]
        public virtual Customer Customer { get; set; }

        [ForeignKey("PaymentId")]
        public virtual Payment Payment { get; set; }

        public virtual ICollection<OrderItem> OrderItems { get; set; }
    }
}