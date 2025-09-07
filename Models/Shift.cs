using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PiranaSecuritySystem.Models
{
    public class Shift
    {
        [Key]
        public int ShiftId { get; set; }

        [ForeignKey("Guard")]
        public int GuardId { get; set; }

        [Column(TypeName = "datetime2")]
        public DateTime ShiftDate { get; set; }

        public string ShiftType { get; set; } // "Night", "Day", "Off"

        public string StartTime { get; set; }
        public string EndTime { get; set; }
        public string Location { get; set; }
        public string Status { get; set; } // "Scheduled", "Completed", "Missed"

        public virtual Guard Guard { get; set; }
        public string InstructorName { get; internal set; }
        public DateTime GeneratedDate { get; internal set; }
        public string Specialization { get; internal set; }
        public DateTime StartDate { get; internal set; }
        public string RosterData { get; internal set; }
        public DateTime EndDate { get; internal set; }
        public string TrainingType { get; internal set; }
    }
}