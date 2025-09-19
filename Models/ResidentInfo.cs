// ResidentInfo.cs (add to Models folder)
using System;
using System.ComponentModel.DataAnnotations;

namespace PiranaSecuritySystem.Models
{
    public class ResidentInfo
    {
        public int Id { get; set; }

        [Required]
        public string UserId { get; set; }  // Links to ApplicationUser Id

        [StringLength(200, ErrorMessage = "Address cannot exceed 200 characters")]
        public string Address { get; set; }

        [StringLength(20, ErrorMessage = "Unit Number cannot exceed 20 characters")]
        public string UnitNumber { get; set; }

        [StringLength(100, ErrorMessage = "Emergency Contact cannot exceed 100 characters")]
        public string EmergencyContact { get; set; }

        public DateTime DateRegistered { get; set; }

        // Navigation property
        public virtual ApplicationUser User { get; set; }
    }
}