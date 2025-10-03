using PiranaSecuritySystem.Models;
using System;
using System.Collections.Generic;
using System.Linq;

namespace PiranaSecuritySystem.ViewModels
{
    public class GuardCalendarViewModel
    {
        public int GuardId { get; set; }
        public string GuardName { get; set; }
        public List<CalendarShift> Shifts { get; set; }
        public List<GuardCheckIn> CheckIns { get; set; }

        // Add this method to handle the shift hours calculation
        public double GetHoursWorkedForShift(int rosterId)
        {
            var shiftCheckIns = CheckIns
                .Where(c => c.RosterId.HasValue && c.RosterId.Value == rosterId)
                .ToList();

            var checkIn = shiftCheckIns.FirstOrDefault(c => c.Status == "Present" || c.Status == "Late Arrival");
            var checkOut = shiftCheckIns.FirstOrDefault(c => c.Status == "Checked Out" || c.Status == "Late Departure");

            if (checkIn != null && checkOut != null)
            {
                return Math.Round((checkOut.CheckInTime - checkIn.CheckInTime).TotalHours, 2);
            }

            return 0;
        }

        // Add this method to get check-in status for a shift
        public (GuardCheckIn CheckIn, GuardCheckIn CheckOut) GetShiftCheckIns(int rosterId)
        {
            var shiftCheckIns = CheckIns
                .Where(c => c.RosterId.HasValue && c.RosterId.Value == rosterId)
                .ToList();

            var checkIn = shiftCheckIns.FirstOrDefault(c => c.Status == "Present" || c.Status == "Late Arrival");
            var checkOut = shiftCheckIns.FirstOrDefault(c => c.Status == "Checked Out" || c.Status == "Late Departure");

            return (checkIn, checkOut);
        }
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
        public int RosterId { get; internal set; }
    }
}