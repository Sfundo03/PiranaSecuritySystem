using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PiranaSecuritySystem.Models
{
    public class AssessmentResult
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int AssessmentId { get; set; }

        [Required]
        public int TrainingSessionId { get; set; }

        [ForeignKey("TrainingSessionId")]
        public virtual TrainingSession TrainingSession { get; set; }

        [Required]
        public int GuardId { get; set; }

        [ForeignKey("GuardId")]
        public virtual Guard Guard { get; set; }

        [Required]
        [Range(0, 100)]
        [Display(Name = "Score (%)")]
        public decimal Score { get; set; }

        [Required]
        [Display(Name = "Maximum Score")]
        [Range(0, 100)]
        public decimal MaxScore { get; set; } = 100;

        [Display(Name = "Passing Percentage")]
        [Range(0, 100)]
        public decimal PassingPercentage { get; set; } = 70;

        [Display(Name = "Passed")]
        public bool IsPassed { get; set; }

        [StringLength(500)]
        [Display(Name = "Instructor Comments")]
        public string Comments { get; set; }

        [Required]
        [Display(Name = "Assessment Date")]
        public DateTime AssessmentDate { get; set; } = DateTime.Now;

        [Required]
        public string AssessedBy { get; set; }

        // Certificate properties
        public bool CertificateGenerated { get; set; }
        public DateTime? CertificateGeneratedDate { get; set; }
        public string CertificateNumber { get; set; }

        // Navigation properties
        public virtual TrainingEnrollment Enrollment { get; set; }
    }

    public class AssessmentViewModel
    {
        public int AssessmentId { get; set; } // Add this line

        public int TrainingSessionId { get; set; }

        [Required]
        [Display(Name = "Training Session")]
        public string TrainingSessionTitle { get; set; }

        [Required]
        [Display(Name = "Guard")]
        public int GuardId { get; set; }

        [Display(Name = "Guard Name")]
        public string GuardName { get; set; }

        [Required]
        [Range(0, 100, ErrorMessage = "Score must be between 0 and 100")]
        [Display(Name = "Score (%)")]
        public decimal Score { get; set; }

        [Required]
        [Range(0, 100, ErrorMessage = "Maximum score must be between 0 and 100")]
        [Display(Name = "Maximum Score")]
        public decimal MaxScore { get; set; } = 100;

        [Required]
        [Range(0, 100, ErrorMessage = "Passing percentage must be between 0 and 100")]
        [Display(Name = "Passing Percentage")]
        public decimal PassingPercentage { get; set; } = 70;

        [Display(Name = "Instructor Comments")]
        [StringLength(500)]
        public string Comments { get; set; }

        public bool IsPassed { get; set; }
        public string CertificateNumber { get; set; }
    }

    public class AssessmentResultsViewModel
    {
        public List<AssessmentResult> AssessmentResults { get; set; }
        public TrainingSession TrainingSession { get; set; }
        public int TotalAssessments { get; set; }
        public int PassedCount { get; set; }
        public int FailedCount { get; set; }
        public decimal AverageScore { get; set; }
    }
}