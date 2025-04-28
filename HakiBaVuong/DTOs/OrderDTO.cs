namespace HakiBaVuong.DTOs
{
    public class OrderDTO
    {
        public int OrderId { get; set; }
        public int BrandId { get; set; }
        public int? CustomerId { get; set; }
        public string FullName { get; set; }
        public string Phone { get; set; }
        public string Address { get; set; }
        public string Status { get; set; }
        public decimal TotalAmount { get; set; }
        public DateTime CreatedAt { get; set; }
        public List<OrderItemDTO> OrderItems { get; set; }
        public PaymentDTO Payment { get; set; }
    }

    public class PaymentDTO
    {
        public int PaymentId { get; set; }
        public decimal Amount { get; set; }
        public string Method { get; set; }
        public string Status { get; set; }
    }
}