using System;
using System.ComponentModel.DataAnnotations;

namespace PiranaSecuritySystem.Models
{
    public class Resident
    {
        [Key]
        public int ResidentId { get; set; }

        [Required]
        [Display(Name = "Full Name")]
        public string FullName { get; set; }

        [Required]
        [EmailAddress]
        public string Email { get; set; }

        [Required]
        [Display(Name = "Phone Number")]
        public string PhoneNumber { get; set; }

        [Required]
        public string Address { get; set; }

        [Required]
        [Display(Name = "Unit Number")]
        public string UnitNumber { get; set; }

        [Required]
        [DataType(DataType.Password)]
        [MinLength(6, ErrorMessage = "Password must be at least 6 characters long")]
        public string Password { get; set; }

        [Display(Name = "Date Registered")]
        public DateTime DateRegistered { get; set; }

        public bool IsActive { get; set; }

        [Display(Name = "Emergency Contact")]
        public string EmergencyContact { get; set; }

        public Resident()
        {
            DateRegistered = DateTime.Now;
            IsActive = true;
        }
    }
}