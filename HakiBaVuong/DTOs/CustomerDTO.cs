namespace HakiBaVuong.DTOs
{
    public class CustomerDTO
    {
        public int CustomerId { get; set; }
        public string Name { get; set; }
        public string Email { get; set; }
        public DateTime CreatedAt { get; set; }
        public bool IsEmailVerified { get; set; }
        public int LoyaltyPoints { get; set; }
        public List<CustomerAddressDTO> Addresses { get; set; }
    }
}