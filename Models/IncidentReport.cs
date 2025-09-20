using System;
using System.ComponentModel.DataAnnotations;

namespace PiranaSecuritySystem.Models
{
    public class IncidentReport
    {
        [Key]
        public int IncidentReportId { get; set; }

        // For resident-reported incidents
        public int? ResidentId { get; set; }
        public virtual Resident Resident { get; set; }

        // For guard-reported incidents - ADD THESE
        public int? GuardId { get; set; }
        public virtual Guard Guard { get; set; }

        [Required]
        public string IncidentType { get; set; }

        public string Location { get; set; }

        public string EmergencyContact { get; set; }

        public string Feedback { get; set; }
        public string FeedbackAttachment { get; set; }
        public DateTime? FeedbackDate { get; set; }

        public DateTime ReportDate { get; set; }

        public string Status { get; set; } // Pending, In Progress, Resolved

        [Required]
        public string Description { get; set; }

        public string Priority { get; set; } // Low, Medium, High, Critical

        public string ReportedBy { get; set; }

        public string CreatedBy { get; set; }
        public DateTime CreatedDate { get; set; }

    }
}