using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace PiranaSecuritySystem.Models
{
    public class DashboardStats
    {
        public int TotalGuards { get; set; }
        public int TotalInstructors { get; set; }
        public int ActiveGuards { get; set; }
        public int ActiveInstructors { get; set; }
        public int TotalShifts { get; set; }
        public int PendingIncidents { get; set; }
        public int TodayAttendance { get; set; }
        public int UpcomingTrainings { get; set; }

        // Notification properties
        public int UnreadNotificationCount { get; set; }
        public List<Notification> Notifications { get; set; }

        

        // Additional statistics for better dashboard insights
        public int InactiveGuards
        {
            get { return TotalGuards - ActiveGuards; }
        }

        public int InactiveInstructors
        {
            get { return TotalInstructors - ActiveInstructors; }
        }

        public double GuardActivationRate
        {
            get
            {
                return TotalGuards > 0 ? Math.Round((double)ActiveGuards / TotalGuards * 100, 2) : 0;
            }
        }

        public double InstructorActivationRate
        {
            get
            {
                return TotalInstructors > 0 ? Math.Round((double)ActiveInstructors / TotalInstructors * 100, 2) : 0;
            }
        }

        // Recent activity statistics
        public int GuardsRegisteredToday { get; set; }
        public int InstructorsRegisteredToday { get; set; }
        public int TotalNotifications { get; set; }

        // Site-wise statistics
        public Dictionary<string, int> GuardsBySite { get; set; }
        public Dictionary<string, int> InstructorsBySite { get; set; }

        public DashboardStats()
        {
            Notifications = new List<Notification>();
            GuardsBySite = new Dictionary<string, int>();
            InstructorsBySite = new Dictionary<string, int>();
            
        }
    }

    // Remove the nested Notification class since we already have a proper Notification model
    // The Notification class below is commented out because it should be in a separate file
    /*
    public class Notification
    {
        public int Id { get; set; }
        public string Title { get; set; }
        public string Message { get; set; }
        public DateTime CreatedAt { get; set; }
        public bool IsRead { get; set; }
        public string NotificationType { get; set; } // e.g., "Info", "Warning", "Alert"

        public string TimeAgo
        {
            get
            {
                var timeSpan = DateTime.Now - CreatedAt;
                if (timeSpan <= TimeSpan.FromSeconds(60))
                    return "just now";
                if (timeSpan <= TimeSpan.FromMinutes(60))
                    return $"{(int)timeSpan.TotalMinutes} min ago";
                if (timeSpan <= TimeSpan.FromHours(24))
                    return $"{(int)timeSpan.TotalHours} hours ago";
                return $"{(int)timeSpan.TotalDays} days ago";
            }
        }
    }
    */
}