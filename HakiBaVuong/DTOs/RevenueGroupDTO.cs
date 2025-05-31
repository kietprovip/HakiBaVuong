namespace HakiBaVuong.DTOs
{
    public class RevenueGroupDTO
    {
        public string GroupKey { get; set; }
        public decimal TotalRevenue { get; set; }
        public decimal TotalCost { get; set; }
        public decimal TotalProfit { get; set; }
        public int OrderCount { get; set; }
    }
}