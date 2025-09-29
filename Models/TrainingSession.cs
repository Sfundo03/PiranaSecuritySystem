// Models/TrainingSession.cs
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PiranaSecuritySystem.Models
{
    public class TrainingSession
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [StringLength(100)]
        public string Title { get; set; }

        [Required]
        [Display(Name = "Start Date")]
        public DateTime StartDate { get; set; }

        [Required]
        [Display(Name = "End Date")]
        public DateTime EndDate { get; set; }

        [Required]
        public int Capacity { get; set; }

        [Required]
        [StringLength(50)]
        public string Site { get; set; }

        // Navigation property for enrolled guards
        public virtual ICollection<TrainingEnrollment> Enrollments { get; set; }

        public TrainingSession()
        {
            Enrollments = new HashSet<TrainingEnrollment>();
        }
    }

    public class TrainingEnrollment
    {
        [Key]
        public int Id { get; set; }

        public int TrainingSessionId { get; set; }
        public int GuardId { get; set; }
        public DateTime EnrollmentDate { get; set; }

        [ForeignKey("TrainingSessionId")]
        public virtual TrainingSession TrainingSession { get; set; }

        [ForeignKey("GuardId")]
        public virtual Guard Guard { get; set; }
    }
}