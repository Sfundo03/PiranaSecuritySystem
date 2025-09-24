using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Web.Mvc;

namespace PiranaSecuritySystem.ViewModels
{
    public class RegisterInstructorViewModel
    {
      

        [Required]
        [Display(Name = "Full Name")]
        public string FullName { get; set; }

        [Required]
        [Display(Name = "Group")]
        public string Group { get; set; }

        public List<SelectListItem> GroupOptions { get; set; }

        [Required]
        [EmailAddress]
        public string Email { get; set; }

        [Required]
        [Display(Name = "Phone Number")]
        public string PhoneNumber { get; set; }

        [Required]
        public string Specialization { get; set; }

        [Required]
        [Display(Name = "Site")]
        public string Site { get; set; }

        public List<SelectListItem> SiteOptions { get; set; }

        [Required]
        [StringLength(100, ErrorMessage = "The {0} must be at least {2} characters long.", MinimumLength = 6)]
        [DataType(DataType.Password)]
        [Display(Name = "Password")]
        public string Password { get; set; }

        [DataType(DataType.Password)]
        [Display(Name = "Confirm password")]
        [System.ComponentModel.DataAnnotations.Compare("Password", ErrorMessage = "The password and confirmation password do not match.")]
        public string ConfirmPassword { get; set; }

        public RegisterInstructorViewModel()
        {
            // Initialize the lists to avoid null reference exceptions
            GroupOptions = new List<SelectListItem>();
            SiteOptions = new List<SelectListItem>();
        }
    }
}