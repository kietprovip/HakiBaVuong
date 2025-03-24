namespace HakiBaVuong.DTOs
{
    public class UserDTO
    {
        public string Email { get; set; }
        public string Role { get; set; } // 'admin', 'brand_owner', 'staff'
        public int? BrandId { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}
