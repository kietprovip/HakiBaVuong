namespace HakiBaVuong.DTOs
{
    public class ResetPasswordProfileDTO
    {
        public string Otp { get; set; }
        public string NewPassword { get; set; }
        public string ConfirmPassword { get; set; }
    }
}
