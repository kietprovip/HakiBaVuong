﻿namespace HakiBaVuong.DTOs
{
    public class ResetPasswordDTO
    {
        public string Email { get; set; }
        public string Otp { get; set; }
        public string NewPassword { get; set; }
        public string ConfirmPassword { get; set; }
        public string CaptchaResponse { get; set; }
    }
}
