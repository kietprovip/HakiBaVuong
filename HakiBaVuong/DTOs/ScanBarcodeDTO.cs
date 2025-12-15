namespace HakiBaVuong.DTOs
{
    public class ScanBarcodeDTO
    {
        public string Barcode { get; set; } = string.Empty;
        // Optional BrandId passed from FE when scanning so backend can assign product to this brand
        public int? BrandId { get; set; }
    }
}
