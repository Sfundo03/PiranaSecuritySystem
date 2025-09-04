// Create a new file: GuardCalendarViewModel.cs
using System;
using System.Collections.Generic;

namespace PiranaSecuritySystem.ViewModels
{
    public class GuardCalendarViewModel
    {
        public int GuardId { get; set; }
        public string GuardName { get; set; }
        public int Year { get; set; }
        public int Month { get; set; }
        public List<CalendarDay> Days { get; set; }
    }

    public class CalendarDay
    {
        public DateTime Date { get; set; }
        public string DayOfWeek { get; set; }
        public string ShiftType { get; set; }
        public string Status { get; set; } // "CheckedIn", "NotCheckedIn", "OffDuty", "Normal"
        public bool HasCheckIn { get; set; }
        public bool IsToday { get; set; }
    }
}