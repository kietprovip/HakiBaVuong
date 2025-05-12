namespace HakiBaVuong.DTOs
{
    public class FilterOrdersDTO
    {
        public string? PaymentStatus { get; set; }
        public string? DeliveryStatus { get; set; }
        public DateTime? StartDate { get; set; }
        public DateTime? EndDate { get; set; }
    }
}