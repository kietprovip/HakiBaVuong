namespace HakiBaVuong.DTOs
{
    public class RevenueHistoryDTO
    {
        public int RevenueHistoryId { get; set; }
        public int BrandId { get; set; }
        public decimal DailyRevenue { get; set; }
        public decimal MonthlyRevenue { get; set; }
        public decimal YearlyRevenue { get; set; }
        public DateTime Date { get; set; }
        public string PeriodType { get; set; }
    }
}