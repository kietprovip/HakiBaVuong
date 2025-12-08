using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;

namespace HakiBaVuong.Models
{
    public class ShortVideo
    {
        [Key]
        public int VideoId { get; set; }

        public int BrandId { get; set; }
        [ForeignKey("BrandId")]
        public virtual Brand Brand { get; set; } = null!;

        public string VideoUrl { get; set; } = string.Empty;
        public string ThumbnailUrl { get; set; } = string.Empty;
        public string Caption { get; set; } = string.Empty;

        public int Views { get; set; } = 0;
        public int Likes { get; set; } = 0;
        public int CommentsCount { get; set; } = 0;
        public int Shares { get; set; } = 0;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public virtual ICollection<VideoProduct> VideoProducts { get; set; } = new List<VideoProduct>();
        public virtual ICollection<VideoLike> VideoLikes { get; set; } = new List<VideoLike>();
        public virtual ICollection<VideoComment> VideoComments { get; set; } = new List<VideoComment>();
        public virtual ICollection<VideoView> VideoViews { get; set; } = new List<VideoView>();
        public virtual ICollection<VideoShare> VideoShares { get; set; } = new List<VideoShare>();
    }
}
