namespace HakiBaVuong.DTOs
{
    public class CustomerAddressDTO
    {
        public int AddressId { get; set; }
        public int CustomerId { get; set; }
        public string FullName { get; set; }
        public string Phone { get; set; }
        public string Address { get; set; }
        public bool IsDefault { get; set; }
    }
}