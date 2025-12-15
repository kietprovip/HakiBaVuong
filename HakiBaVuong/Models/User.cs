using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;

namespace HakiBaVuong.Models
{
    public class User
    {
        [Key]
        public int UserId { get; set; }
        [Required]
        public string Name { get; set; }

        [Required, EmailAddress]
        public string Email { get; set; }

        [Required]
        public string Password { get; set; }

        [Required]
        public string Role { get; set; }

        // NOTE: "BrandId" in the User table is NOT the primary identifier for a Brand entity.
        // It is used only to mark that this User is an employee/staff of a specific brand account.
        // To find the owner (the brand account holder) you should look up the Brand record
        // by this BrandId and then read Brand.OwnerId to find the owning User.
        // Do NOT use User.BrandId as the canonical "this user IS a brand owner" flag.
        public int? BrandId { get; set; }
        public string? ApprovalStatus { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.Now;
        public DateTime UpdatedAt { get; set; } = DateTime.Now;

        [ForeignKey("BrandId")]
        public virtual Brand Brand { get; set; }
        public bool IsEmailVerified { get; set; } = false;
    }
}