namespace HakiBaVuong.DTOs
{
    public class UpdateOrderDTO
    {
        public string? FullName { get; set; }
        public string? Phone { get; set; }
        public string? Address { get; set; }
        public string? Status { get; set; }
        public string? DeliveryStatus { get; set; }
        public DateTime? EstimatedDeliveryDate { get; set; }
    }
}