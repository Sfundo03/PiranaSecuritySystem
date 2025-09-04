using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Web;

namespace PiranaSecuritySystem.Models
{
    public class Guard
    {
        [Key]
        
        [Display(Name = "Guard ID")]
        public int GuardId { get; set; }

        [Required]
        [Display(Name = "First Name")]
        public string Guard_FName { get; set; }

        [Required]
        [Display(Name = "Last Name")]
        public string Guard_LName { get; set; }

        [Required(ErrorMessage = "ID number has 13 Digits")]
        [StringLength(13, MinimumLength = 13, ErrorMessage = "ID number must be exactly 13 digits")]
        [Display(Name = "ID Number")]
        public string IdentityNumber { get; set; }

        [Required(ErrorMessage = "Enter Only F/M")]
        [StringLength(1)]
        [RegularExpression("^[MF]$", ErrorMessage = "Enter Only F/M")]
        public string Gender { get; set; }

        [Required(ErrorMessage = "Enter PSIRA number")]
        [Display(Name = "PSIRA Number")]
        public string PSIRAnumber { get; set; }

        [Required]
        [StringLength(10, MinimumLength = 10, ErrorMessage = "Phone number must be exactly 10 digits.")]
        [Display(Name = "Phone Number")]
        public string PhoneNumber { get; set; }

        [Required]
        [StringLength(10, MinimumLength = 10, ErrorMessage = "Phone number must be exactly 10 digits.")]
        [Display(Name = "Emergency Contact")]
        public string Emergency_CellNo { get; set; }

        [Required]
        [EmailAddress]
        [Display(Name = "Email")]
        public string Email { get; set; }

        [Required]
        [Display(Name = "Address")]
        public string Address { get; set; }

        public string Street { get; set; }
        public string HouseNumber { get; set; }
        public string City { get; set; }
        public string PostalCode { get; set; }

        [Display(Name = "Date Registered")]
        public DateTime DateRegistered { get; set; }

        [NotMapped]
        public List<GuardAttendance> MonthlyAttendance { get; set; } = new List<GuardAttendance>();
        public string UserId { get; set; }

        [Display(Name = "Active")]
        public bool IsActive { get; set; }
        [NotMapped] // This property is not stored in the database
        public string FullName
        {
            get { return $"{Guard_FName} {Guard_LName}"; }
        }
        // In your Guard model, add this property
        public virtual ICollection<GuardCheckIn> CheckIns { get; set; }

    }
}