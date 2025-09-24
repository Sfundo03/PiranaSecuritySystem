using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PiranaSecuritySystem.Models
{
    public class ShiftRoster
    {
        [Key]
        [Display(Name = "Roster ID")]
        public int RosterId { get; set; }

        [Required]
        [Display(Name = "Roster Date")]
        [DataType(DataType.Date)]
        public DateTime RosterDate { get; set; }

        [Required]
        [Display(Name = "Shift Type")]
        public string ShiftType { get; set; } // "Day", "Night", "Off"

        [Required]
        [Display(Name = "Guard")]
        public int GuardId { get; set; }

        [ForeignKey("GuardId")]
        public virtual Guard Guard { get; set; }

        [Display(Name = "Created Date")]
        public DateTime CreatedDate { get; set; }

        [Display(Name = "Modified Date")]
        public DateTime? ModifiedDate { get; set; }

        [Required]
        [Display(Name = "Site")]
        public string Site { get; set; } // "Site A", "Site B", "Site C"

        public string Location { get; set; }
        public string Status { get; set; }
        public string InstructorName { get; set; }
        public DateTime GeneratedDate { get; set; }
        public string Specialization { get; set; }
        public string RosterData { get; set; }
        public DateTime StartDate { get; set; }
        public string TrainingType { get; set; }
        public DateTime EndDate { get; set; }
    }

    public class RosterViewModel
    {
        [Required]
        [Display(Name = "Roster Date")]
        [DataType(DataType.Date)]
        public DateTime RosterDate { get; set; }

        [Required(ErrorMessage = "Please select a site")]
        [Display(Name = "Site")]
        public string Site { get; set; } // "Site A", "Site B", "Site C"

        [Required(ErrorMessage = "Please select exactly 12 guards")]
        [Display(Name = "Selected Guards")]
        public List<int> SelectedGuardIds { get; set; } = new List<int>();

        // These will be auto-generated
        public List<Guard> DayShiftGuards { get; set; } = new List<Guard>();
        public List<Guard> NightShiftGuards { get; set; } = new List<Guard>();
        public List<Guard> OffDutyGuards { get; set; } = new List<Guard>();
    }

    public class RosterDisplayViewModel
    {
        public DateTime RosterDate { get; set; }
        public List<Guard> DayShiftGuards { get; set; }
        public List<Guard> NightShiftGuards { get; set; }
        public List<Guard> OffDutyGuards { get; set; }
        public int RosterId { get; set; }
        public string Site { get; set; }
    }
}