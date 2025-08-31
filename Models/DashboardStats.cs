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

        // You can add more statistics as needed
        public int TodayAttendance { get; set; }
        public int UpcomingTrainings { get; set; }
    }
}