namespace HakiBaVuong.DTOs
{
    public class CreateOrderDTO
    {
        public int BrandId { get; set; }
        public string FullName { get; set; }
        public string Phone { get; set; }
        public string Address { get; set; }
        public string PaymentMethod { get; set; }
        public int? AddressId { get; set; }
    }
}