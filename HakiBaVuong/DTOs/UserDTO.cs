namespace HakiBaVuong.DTOs
{
    public class UserDTO
    {
        public int UserId { get; set; }
        public string Name { get; set; }
        public string Email { get; set; }
        public string Role { get; set; }
        public int? BrandId { get; set; }
        public string? ApprovalStatus { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}