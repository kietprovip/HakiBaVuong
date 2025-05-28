using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace HakiBaVuong.Models
{
    public class Revenue
    {
        [Key]
        public int RevenueId { get; set; }

        public int OrderId { get; set; }

        public decimal DailyRevenue { get; set; }
        public decimal MonthlyRevenue { get; set; }
        public decimal YearlyRevenue { get; set; }

        public DateTime Date { get; set; } = DateTime.UtcNow;

        [ForeignKey("OrderId")]
        public virtual Order Order { get; set; }
    }
}