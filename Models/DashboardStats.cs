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

            public int UnreadNotificationCount { get; set; }
            public List<Notification> Notifications { get; set; }
        

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

        // You can add more statistics as needed
        public int TodayAttendance { get; set; }
        public int UpcomingTrainings { get; set; }
    }
}