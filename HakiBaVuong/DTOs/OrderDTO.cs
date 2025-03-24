namespace HakiBaVuong.DTOs
{
    public class OrderDTO
    {
        public int BrandId { get; set; }
        public int? CustomerId { get; set; } // Nếu khách vãng lai, có thể null
        public string FullName { get; set; }
        public string Phone { get; set; }
        public string Address { get; set; }
        public string Status { get; set; } // 'pending', 'processing', 'shipped', 'delivered', 'canceled'
        public decimal TotalAmount { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}
