using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;

namespace HakiBaVuong.Models
{
    public class StaffPermission
    {
        [Key, Column(Order = 1)]
        public int StaffId { get; set; }

        [Key, Column(Order = 2)]
        public int PermissionId { get; set; }

        [ForeignKey("StaffId")]
        public virtual User Staff { get; set; }

        [ForeignKey("PermissionId")]
        public virtual Permission Permission { get; set; }
    }
}
