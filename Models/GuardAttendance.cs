using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PiranaSecuritySystem.Models
{
    public class GuardAttendance
    {
        public DateTime Date { get; set; }
        public string Status { get; set; } // "Present", "Absent", "Leave", "Scheduled", "Future"
        public string ShiftType { get; set; }
        public TimeSpan? CheckInTime { get; set; }
        public DateTime? CheckOutTime { get; private set; }
    }


    public class Attendance
    {
        [Key]
        public int AttendanceId { get; set; }

        [Required]
        public int GuardId { get; set; }

        [ForeignKey("GuardId")]
        public virtual Guard Guard { get; set; }

        [Required]
        [Display(Name = "Check-In Time")]
        public DateTime CheckInTime { get; set; }

        [Display(Name = "Check-Out Time")]
        public DateTime? CheckOutTime { get; set; }

        [Display(Name = "Hours Worked")]
        public double HoursWorked { get; set; }

        [Display(Name = "Date")]
        [DataType(DataType.Date)]
        public DateTime AttendanceDate { get; set; }

        // Link to GuardCheckIn for better tracking
        [ForeignKey("GuardCheckIn")]
        public int? CheckInId { get; set; }

        public virtual GuardCheckIn GuardCheckIn { get; set; }
    }
}