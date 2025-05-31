namespace HakiBaVuong.DTOs
{
    public class ProductDTO
    {
        public int ProductId { get; set; }
        public int BrandId { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public decimal PriceSell { get; set; }
        public decimal? PriceCost { get; set; }
        public float Rating { get; set; }
        public string Image { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
        public List<int> CategoryIds { get; set; }


    }
}