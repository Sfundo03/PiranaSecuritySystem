using System;

namespace PiranaSecuritySystem.Models
{
    public class GuardAttendance
    {
        public DateTime Date { get; set; }
        public string Status { get; set; } // "Present", "Absent", "Leave", "Scheduled", "Future"
        public string ShiftType { get; set; }
    }
}