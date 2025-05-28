using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace HakiBaVuong.Models
{
    public class RevenueHistory
    {
        [Key]
        public int RevenueHistoryId { get; set; }

        public int BrandId { get; set; }

        public decimal DailyRevenue { get; set; }
        public decimal MonthlyRevenue { get; set; }
        public decimal YearlyRevenue { get; set; }

        public DateTime Date { get; set; }
        public string PeriodType { get; set; } // "Day", "Month", "Year"

        [ForeignKey("BrandId")]
        public virtual Brand Brand { get; set; }
    }
}