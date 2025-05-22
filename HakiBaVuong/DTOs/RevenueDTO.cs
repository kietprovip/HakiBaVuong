namespace HakiBaVuong.DTOs
{
    public class RevenueDTO
    {
        public int BrandId { get; set; }
        public string BrandName { get; set; }
        public decimal TotalRevenue { get; set; }
        public decimal TotalProfit { get; set; }
        public int TotalItemsSold { get; set; }
    }
}