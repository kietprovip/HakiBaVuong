namespace HakiBaVuong.DTOs
{
    public class RegisterCustomerDTO
    {
        public string Name { get; set; }
        public string Email { get; set; }
        public string Password { get; set; }
        public string ConfirmPassword { get; set; }
        public string CaptchaResponse { get; set; }
    }
}
