namespace HakiBaVuong.DTOs
{
    public class OrderDTO
    {
        public int BrandId { get; set; }
        public int? CustomerId { get; set; }
        public string FullName { get; set; }
        public string Phone { get; set; }
        public string Address { get; set; }
        public string Status { get; set; }
        public decimal TotalAmount { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}
