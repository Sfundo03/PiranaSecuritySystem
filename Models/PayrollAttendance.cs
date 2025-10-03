using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace PiranaSecuritySystem.Models
{
    public class PayrollAttendance
    {
        public DateTime Date { get; set; }
        public DateTime CheckInTime { get; set; }
        public DateTime CheckOutTime { get; set; }
        public double HoursWorked { get; set; }
        public string ShiftType { get; set; }
        public int? RosterId { get; set; }
    }
}