using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace HakiBaVuong.Models
{
    public class Comment
    {
        [Key]
        public int CommentId { get; set; }

        [Required]
        public int ProductId { get; set; }

        [Required]
        public int CustomerId { get; set; }

        [Required]
        public string Content { get; set; }

        [Range(1, 5)]
        public int Rating { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.Now;

        [ForeignKey("ProductId")]
        public virtual Product Product { get; set; }

        [ForeignKey("CustomerId")]
        public virtual Customer Customer { get; set; }
    }
}