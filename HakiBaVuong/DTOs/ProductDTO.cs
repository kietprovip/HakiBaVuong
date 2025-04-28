namespace HakiBaVuong.DTOs
{
    public class ProductDTO
    {
        public int BrandId { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public decimal PriceSell { get; set; }
        public decimal? PriceCost { get; set; }
        public string Image { get; set; }
    }
}
