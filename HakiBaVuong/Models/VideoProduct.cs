using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace HakiBaVuong.Models
{

    public class VideoProduct
    {
        [Key]
        public int Id { get; set; }

        public int VideoId { get; set; }

        [ForeignKey("VideoId")]
        public virtual ShortVideo Video { get; set; } = null!;

        public int ProductId { get; set; }

        [ForeignKey("ProductId")]
        public virtual Product Product { get; set; } = null!;

        public double X { get; set; }
        public double Y { get; set; }
    }
}
