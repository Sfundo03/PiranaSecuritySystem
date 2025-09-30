// PasswordResetOTP.cs - Add to Models folder
using System;
using System.ComponentModel.DataAnnotations;

namespace PiranaSecuritySystem.Models
{
    public class PasswordResetOTP
    {
        public int Id { get; set; }

        [Required]
        [EmailAddress]
        public string Email { get; set; }

        [Required]
        [StringLength(6)]
        public string OTP { get; set; }

        public DateTime ExpiryTime { get; set; }

        public bool IsUsed { get; set; }

        public DateTime CreatedAt { get; set; }
    }
}

// VerifyOTPViewModel.cs - Add to Models folder

    public class VerifyOTPViewModel
    {
        [Required]
        [EmailAddress]
        [Display(Name = "Email")]
        public string Email { get; set; }

        [Required]
        [Display(Name = "OTP Code")]
        [StringLength(6, MinimumLength = 6, ErrorMessage = "OTP must be 6 digits")]
        [RegularExpression(@"^\d{6}$", ErrorMessage = "OTP must be 6 digits")]
        public string OTP { get; set; }
    }
