namespace HakiBaVuong.DTOs
{
    public class RevenueResponseDTO
    {
        public int BrandId { get; set; }
        public decimal TotalRevenue { get; set; }
        public decimal TotalProfit { get; set; }
        public List<OrderDTO> Orders { get; set; }
        public DateTime? StartDate { get; set; }
        public DateTime? EndDate { get; set; }
    }
}