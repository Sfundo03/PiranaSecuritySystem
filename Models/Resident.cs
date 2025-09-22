using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PiranaSecuritySystem.Models
{
    public class Resident : ApplicationUser
    {
        
        [Required(ErrorMessage = "Address is required")]
        [StringLength(200, ErrorMessage = "Address cannot exceed 200 characters")]
        public string Address { get; set; }

        [Required(ErrorMessage = "Unit Number is required")]
        [Display(Name = "Unit Number")]
        [StringLength(20, ErrorMessage = "Unit Number cannot exceed 20 characters")]
        public string UnitNumber { get; set; }

        [Display(Name = "Date Registered")]
        public DateTime DateRegistered { get; set; }

        [Display(Name = "Emergency Contact")]
        [StringLength(100, ErrorMessage = "Emergency Contact cannot exceed 100 characters")]
        public string EmergencyContact { get; set; }

        // Navigation property for incidents
        public virtual ICollection<IncidentReport> IncidentReports { get; set; }
    }
}