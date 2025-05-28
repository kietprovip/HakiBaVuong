namespace HakiBaVuong.DTOs
{
    public class RevenueDTO
    {
        public int RevenueId { get; set; }
        public int OrderId { get; set; }
        public decimal DailyRevenue { get; set; }
        public decimal MonthlyRevenue { get; set; }
        public decimal YearlyRevenue { get; set; }
        public DateTime Date { get; set; }
    }
}