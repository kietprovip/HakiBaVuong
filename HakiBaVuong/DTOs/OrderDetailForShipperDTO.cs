namespace HakiBaVuong.DTOs
{
    public class OrderDetailForShipperDTO
    {
        public int OrderId { get; set; }
        public string CustomerName { get; set; } = string.Empty;
        public string CustomerPhone { get; set; } = string.Empty;
        public string CustomerAddress { get; set; } = string.Empty;
        public decimal TotalAmount { get; set; }
        public string DeliveryStatus { get; set; } = "Đang giao";
        public DateTime? EstimatedDeliveryDate { get; set; }
        public DateTime CreatedAt { get; set; }

        public List<OrderItemForShipperDTO> Items { get; set; } = new();
    }
}
