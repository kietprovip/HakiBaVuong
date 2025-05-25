namespace HakiBaVuong.DTOs
{
    public class PaymentRequestDTO
    {
        public int OrderId { get; set; }
    }

    public class PaymentResponseDTO
    {
        public int PaymentId { get; set; }
        public int OrderId { get; set; }
        public decimal Amount { get; set; }
        public string Method { get; set; }
        public string Status { get; set; }
        public string QrCodeUrl { get; set; }
        public string PaymentLink { get; set; }
    }

    public class PayOSWebhookDTO
    {
        public string Signature { get; set; }
        public PayOSWebhookData Data { get; set; }
    }

    public class PayOSWebhookData
    {
        public int OrderCode { get; set; }
        public string PaymentLinkId { get; set; }
        public string Status { get; set; }
    }
}