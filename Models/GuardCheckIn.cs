using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PiranaSecuritySystem.Models
{
    public class GuardCheckIn
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int CheckInId { get; set; }

        [Required]
        [ForeignKey("Guard")]
        public int GuardId { get; set; }

        [Required]
        [Display(Name = "Check-In Time")]
        public DateTime CheckInTime { get; set; }

        [Required]
        [StringLength(20)]
        public string Status { get; set; } // "Present", "Checked Out", "Late Arrival", "Late Departure"

        [Required]
        [Display(Name = "Created Date")]
        public DateTime CreatedDate { get; set; }

        [Display(Name = "Is Late")]
        public bool IsLate { get; set; }

        [StringLength(10)]
        [Display(Name = "Expected Time")]
        public string ExpectedTime { get; set; } // "06:00" or "18:00"

        [StringLength(10)]
        [Display(Name = "Actual Time")]
        public string ActualTime { get; set; } // HH:mm format

        [StringLength(50)]
        [Display(Name = "Site Username")]
        public string SiteUsername { get; set; }

        // Navigation property
        public virtual Guard Guard { get; set; }
    }
}
