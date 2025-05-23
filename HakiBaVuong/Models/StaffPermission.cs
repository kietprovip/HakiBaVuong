using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace HakiBaVuong.Models
{
    public class StaffPermission
    {
        [Key, Column(Order = 1)]
        public int StaffId { get; set; }

        [Key, Column(Order = 2)]
        [Required]
        public string Role { get; set; }

        [ForeignKey("StaffId")]
        public virtual User Staff { get; set; }
    }
}