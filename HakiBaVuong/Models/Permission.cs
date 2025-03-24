using System.ComponentModel.DataAnnotations;

namespace HakiBaVuong.Models
{
    public class Permission
    {
        [Key]
        public int PermissionId { get; set; }

        [Required]
        public string Name { get; set; }

        public string Description { get; set; }

        public virtual ICollection<StaffPermission> StaffPermissions { get; set; }
    }
}
