using System.ComponentModel.DataAnnotations;

namespace HakiBaVuong.Models
{
    public class Customer
    {
        [Key]
        public int CustomerId { get; set; }
        [Required]
        public string Name { get; set; }
        [Required, EmailAddress]
        public string Email { get; set; }
        [Required]
        public string Password { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.Now;
        public int LoyaltyPoints { get; set; } = 0;
        public virtual ICollection<CustomerAddress> Addresses { get; set; }
        public virtual ICollection<Order> Orders { get; set; }
        public bool IsEmailVerified { get; set; } = false;
        public virtual ICollection<Comment> Comments { get; set; } = new List<Comment>();
        public virtual ICollection<ViewedProduct> ViewedProducts { get; set; } = new List<ViewedProduct>();
        public virtual ICollection<Wishlist> Wishlist { get; set; } = new List<Wishlist>();
    }
}