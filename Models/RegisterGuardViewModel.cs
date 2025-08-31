using System.ComponentModel.DataAnnotations;

namespace PiranaSecuritySystem.ViewModels
{
    public class RegisterGuardViewModel
    {
        [Display(Name = "Guard ID")]
        public int GuardId { get; set; }

        [Required]
        [Display(Name = "First Name")]
        public string Guard_FName { get; set; }

        [Required(ErrorMessage = "Last name is required")]
        [Display(Name = "Last Name")]
        public string Guard_LName { get; set; }

        [Required(ErrorMessage = "ID number has 13 Digits")]
        [StringLength(13, MinimumLength = 13, ErrorMessage = "ID number must be exactly 13 digits")]
        [Display(Name = "ID Number")]
        public string IdentityNumber { get; set; }  // Changed from ID_number

        [Required(ErrorMessage = "PSIRA Number is required")]
        [Display(Name = "PSIRA Number")]
        public string PSIRAnumber { get; set; }     // Changed from PsiraNumber

        [Required]
        [Display(Name = "Emergency Contact")]
        [StringLength(10, MinimumLength = 10, ErrorMessage = "Phone number must be exactly 10 digits.")]
        public string Emergency_CellNo { get; set; }

        [Required(ErrorMessage = "Gender is required")]
        [RegularExpression("^[MF]$", ErrorMessage = "Enter Only F/M")]
        [Display(Name = "Gender")]
        public string Gender { get; set; }

        [Required(ErrorMessage = "Email is required")]
        [EmailAddress(ErrorMessage = "Invalid email address")]
        [Display(Name = "Email")]
        public string Email { get; set; }

        [Required(ErrorMessage = "Phone number is required")]
        [StringLength(10, MinimumLength = 10, ErrorMessage = "Phone number must be exactly 10 digits.")]
        [Display(Name = "Phone Number")]
        public string PhoneNumber { get; set; }

        [Required(ErrorMessage = "Address is required")]
        [Display(Name = "Address")]
        public string Address { get; set; }

        public string Street { get; set; }
        public string HouseNumber { get; set; }
        public string City { get; set; }
        public string PostalCode { get; set; }


        [Required(ErrorMessage = "Password is required")]
        [StringLength(100, ErrorMessage = "The {0} must be at least {2} characters long.", MinimumLength = 6)]
        [DataType(DataType.Password)]
        [Display(Name = "Password")]
        public string Password { get; set; }

        [Required(ErrorMessage = "Please confirm your password")]
        [DataType(DataType.Password)]
        [Display(Name = "Confirm password")]
        [Compare("Password", ErrorMessage = "The password and confirmation password do not match.")]
        public string ConfirmPassword { get; set; }

    }
}