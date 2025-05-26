using System;
using System.ComponentModel.DataAnnotations;

namespace HakiBaVuong.Models
{
    public class ViewedProduct
    {
        [Key]
        public int ViewedProductId { get; set; }

        public int CustomerId { get; set; }
        public virtual Customer Customer { get; set; }

        public int ProductId { get; set; }
        public virtual Product Product { get; set; }

        public DateTime ViewedAt { get; set; } = DateTime.UtcNow;
    }
}