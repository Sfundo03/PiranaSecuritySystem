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
        public string Status { get; set; } // "Present" or "Checked Out"

        [Required]
        [Display(Name = "Created Date")]
        public DateTime CreatedDate { get; set; }

        // Navigation property
        public virtual Guard Guard { get; set; }



    }
}