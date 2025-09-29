using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Web.Mvc;

namespace PiranaSecuritySystem.ViewModels
{
    public class EditInstructorViewModel
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "Full Name is required")]
        [Display(Name = "Full Name")]
        public string FullName { get; set; }

        [Required(ErrorMessage = "Employee ID is required")]
        [Display(Name = "Employee ID")]
        public string EmployeeId { get; set; } // Add this property

        [Required(ErrorMessage = "Instructor Group is required")]
        [Display(Name = "Instructor Group")]
        public string Group { get; set; }

        [Required(ErrorMessage = "Email is required")]
        [EmailAddress(ErrorMessage = "Invalid Email Address")]
        public string Email { get; set; }

        [Required(ErrorMessage = "Phone Number is required")]
        [Display(Name = "Phone Number")]
        public string PhoneNumber { get; set; }

        [Required(ErrorMessage = "Specialization is required")]
        public string Specialization { get; set; }

        [Required(ErrorMessage = "Site is required")]
        [Display(Name = "Site")]
        public string Site { get; set; }

        [Display(Name = "Is Active")]
        public bool IsActive { get; set; }

        public List<SelectListItem> SiteOptions { get; set; }
        public List<SelectListItem> GroupOptions { get; set; }

        public EditInstructorViewModel()
        {
            SiteOptions = new List<SelectListItem>
            {
                new SelectListItem { Value = "Site A", Text = "Site A" },
                new SelectListItem { Value = "Site B", Text = "Site B" },
                new SelectListItem { Value = "Site C", Text = "Site C" }
            };

            GroupOptions = new List<SelectListItem>
            {
                new SelectListItem { Value = "Group A", Text = "Group A" },
                new SelectListItem { Value = "Group B", Text = "Group B" },
                new SelectListItem { Value = "Group C", Text = "Group C" }
            };
        }
    }
}