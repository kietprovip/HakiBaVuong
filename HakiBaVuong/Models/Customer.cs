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

        public virtual ICollection<CustomerAddress> Addresses { get; set; }
        public virtual ICollection<Order> Orders { get; set; }
        public bool IsEmailVerified { get; set; } = false;
    }
}
