namespace HakiBaVuong.DTOs
{
    public class CartDTO
    {
        public int CartId { get; set; }
        public int CustomerId { get; set; }
        public List<CartItemDTO> Items { get; set; }
    }

    public class CartItemDTO
    {
        public int CartItemId { get; set; }
        public int ProductId { get; set; }
        public string ProductName { get; set; }
        public decimal PriceSell { get; set; }
        public string Image { get; set; }
        public int Quantity { get; set; }
        public int BrandId { get; set; }
        public string BrandName { get; set; }
    }
}