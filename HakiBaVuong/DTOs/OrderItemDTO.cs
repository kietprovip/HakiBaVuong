﻿namespace HakiBaVuong.DTOs
{
    public class OrderItemDTO
    {
        public int OrderId { get; set; }
        public int ProductId { get; set; }
        public string ProductName { get; set; }
        public int Quantity { get; set; }
        public decimal Price { get; set; } // Giá bán tại thời điểm đặt hàng
    }
}
