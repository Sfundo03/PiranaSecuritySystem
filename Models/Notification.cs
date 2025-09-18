using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Web;

namespace PiranaSecuritySystem.Models
{
    public class Notification
    {
        [Key]
        public int NotificationId { get; set; }

        public int? ResidentId { get; set; } // Nullable, if notification is for a resident
        public int? DirectorId { get; set; } // Nullable, if notification is for a director
        public int? AdminId { get; set; } // Nullable, if notification is for an admin
        public int? GuardId { get; set; } // Nullable, if notification is for a guard
        public int? InstructorId { get; set; } // Nullable, if notification is for an instructor

        [Required]
        public string UserId { get; set; } // The actual recipient's user ID

        [Required]
        [StringLength(20)]
        public string UserType { get; set; } // "Resident", "Director", "Admin", "Guard", "Instructor"

        [Required]
        [StringLength(200)]
        public string Title { get; set; }

        [Required]
        [StringLength(500)]
        public string Message { get; set; }

        public bool IsRead { get; set; } = false;

        public DateTime CreatedAt { get; set; } = DateTime.Now;

        public DateTime? DateRead { get; set; }

        [StringLength(200)]
        public string RelatedUrl { get; set; } // URL to relevant page

        [StringLength(50)]
        public string NotificationType { get; set; } // "Login", "Incident", "System", "Security", "Guard", "Instructor", "Checkin", "Report"

        public bool IsImportant { get; set; } = false;

        // Additional properties for better notification management
        public string Source { get; set; } // Which system/module generated the notification

        [StringLength(100)]
        public string ActionRequired { get; set; } // Optional: what action is needed

        public DateTime? ExpiryDate { get; set; } // Optional: when notification becomes irrelevant

        public int PriorityLevel { get; set; } = 1; // 1=Low, 2=Medium, 3=High, 4=Critical

        // Navigation properties (if needed)
        public virtual ApplicationUser User { get; set; }
    }

    

    // Optional: Enum for notification types
    public enum NotificationType
    {
        System,
        Security,
        Incident,
        Guard,
        Instructor,
        Checkin,
        Report,
        Login,
        Warning,
        Emergency
    }

    // Optional: Enum for user types
    public enum UserType
    {
        Director,
        Admin,
        Guard,
        Instructor,
        Resident,
        System
    }

    // Optional: Enum for priority levels
    public enum PriorityLevel
    {
        Low = 1,
        Medium = 2,
        High = 3,
        Critical = 4
    }
}