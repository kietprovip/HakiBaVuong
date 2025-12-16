namespace HakiBaVuong.DTOs
{
    public class ShipperDTO
    {
        public int ShipperId { get; set; }
        public string Name { get; set; }
        public string Email { get; set; }
        public string Phone { get; set; }
        public string VehicleType { get; set; }
        public string LicensePlate { get; set; }
        public double? Rating { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}