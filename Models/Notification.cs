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

        public int? ResidentId { get; set; }
        public int? DirectorId { get; set; }
        public int? AdminId { get; set; }
        public int? GuardId { get; set; }
        public int? InstructorId { get; set; }

        [Required]
        public string UserId { get; set; }

        [Required]
        [StringLength(20)]
        public string UserType { get; set; }

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
        public string RelatedUrl { get; set; }

        [StringLength(50)]
        public string NotificationType { get; set; }

        public bool IsImportant { get; set; } = false;

        public string Source { get; set; }

        [StringLength(100)]
        public string ActionRequired { get; set; }

        public DateTime? ExpiryDate { get; set; }

        public int PriorityLevel { get; set; } = 1;

        // New property to track incident status changes
        public int? RelatedIncidentId { get; set; }

        [StringLength(50)]
        public string OldStatus { get; set; }

        [StringLength(50)]
        public string NewStatus { get; set; }

        public virtual ApplicationUser User { get; set; }

        // Helper method to get formatted time ago
        public string GetTimeAgo()
        {
            try
            {
                if (CreatedAt == DateTime.MinValue || CreatedAt.Year <= 2000)
                    return "Just now";

                TimeSpan timeSince = DateTime.Now - CreatedAt;

                if (timeSince.TotalSeconds < 60)
                    return "Just now";
                else if (timeSince.TotalMinutes < 60)
                    return $"{(int)timeSince.TotalMinutes} minute{(timeSince.TotalMinutes >= 2 ? "s" : "")} ago";
                else if (timeSince.TotalHours < 24)
                    return $"{(int)timeSince.TotalHours} hour{(timeSince.TotalHours >= 2 ? "s" : "")} ago";
                else if (timeSince.TotalDays < 7)
                    return $"{(int)timeSince.TotalDays} day{(timeSince.TotalDays >= 2 ? "s" : "")} ago";
                else if (timeSince.TotalDays < 30)
                    return $"{(int)(timeSince.TotalDays / 7)} week{((int)(timeSince.TotalDays / 7) >= 2 ? "s" : "")} ago";
                else if (timeSince.TotalDays < 365)
                    return $"{(int)(timeSince.TotalDays / 30)} month{((int)(timeSince.TotalDays / 30) >= 2 ? "s" : "")} ago";
                else
                    return CreatedAt.ToString("MMM dd, yyyy");
            }
            catch
            {
                return "Just now";
            }
        }
    }

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

    public enum UserType
    {
        Director,
        Admin,
        Guard,
        Instructor,
        Resident,
        System
    }

    public enum PriorityLevel
    {
        Low = 1,
        Medium = 2,
        High = 3,
        Critical = 4
    }
}