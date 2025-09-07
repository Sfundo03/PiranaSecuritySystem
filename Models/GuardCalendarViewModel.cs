using PiranaSecuritySystem.Models;
using System;
using System.Collections.Generic;

namespace PiranaSecuritySystem.ViewModels
{
    public class GuardCalendarViewModel
    {
        public int GuardId { get; set; }
        public string GuardName { get; set; }
        public List<CalendarShift> Shifts { get; set; }
        public List<GuardCheckIn> CheckIns { get; set; }
    }

    public class CalendarShift
    {
        public int ShiftId { get; set; }
        public DateTime ShiftDate { get; set; }
        public string ShiftType { get; set; } // "Night", "Day", "Off"
        public string StartTime { get; set; }
        public string EndTime { get; set; }
        public string Location { get; set; }
        public string Status { get; set; } // "Scheduled", "Completed", "Missed"
        public bool HasCheckIn { get; set; }
        public bool HasCheckOut { get; set; }
        public DateTime? CheckInTime { get; set; }
        public DateTime? CheckOutTime { get; set; }
        public bool IsToday { get; set; }
    }
}