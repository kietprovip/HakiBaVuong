namespace HakiBaVuong.DTOs
{
    public class CommentDTO
    {
        public int CommentId { get; set; }
        public int ProductId { get; set; }
        public int CustomerId { get; set; }
        public string Content { get; set; }
        public int Rating { get; set; }
        public DateTime CreatedAt { get; set; }
        public string ProductName { get; set; }
        public string CustomerName { get; set; }
    }
}