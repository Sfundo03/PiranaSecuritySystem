using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;

namespace PiranaSecuritySystem.Models
{

    public class ShiftRoster
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int RosterId { get; set; }

        [Required]
        [Display(Name = "datetime2")]
        [DataType(DataType.Date)]
        public DateTime RosterDate { get; set; }

        [Required]
        [Display(Name = "Shift Type")]
        [StringLength(10)]
        public string ShiftType { get; set; } // "Day", "Night", "Off"

        [Required]
        [Display(Name = "Site")]
        [StringLength(100)]
        public string Site { get; set; }

        [Required]
        [Display(Name = "Guard")]
        public int GuardId { get; set; }

        [ForeignKey("GuardId")]
        public virtual Guard Guard { get; set; }

        [Column(TypeName = "datetime2")]
        public DateTime CreatedDate { get; set; } = DateTime.Now;

        
        public string InstructorName { get; internal set; }

        [Column(TypeName = "datetime2")]
        public DateTime GeneratedDate { get; internal set; }
        public string RosterData { get; internal set; }
        public string Status { get; internal set; }
        public object Location { get; internal set; }
    }

    public class RosterViewModel
    {
        [Required]
        [Display(Name = "Roster Date")]
        [DataType(DataType.Date)]
        public DateTime RosterDate { get; set; }

        public bool GenerateFor30Days { get; set; }

        [Required]
        [Display(Name = "Site")]
        public string Site { get; set; }

        // This will store the comma-separated string from the form
        public string SelectedGuardIds { get; set; }

        // Helper property to get/set as List<int>
        [Required(ErrorMessage = "Please select exactly 12 guards")]
        public List<int> GuardIdList
        {
            get
            {
                if (string.IsNullOrEmpty(SelectedGuardIds))
                    return new List<int>();

                try
                {
                    return SelectedGuardIds.Split(',')
                        .Where(id => !string.IsNullOrEmpty(id) && int.TryParse(id, out _))
                        .Select(int.Parse)
                        .ToList();
                }
                catch
                {
                    return new List<int>();
                }
            }
            set
            {
                SelectedGuardIds = value != null ? string.Join(",", value) : "";
            }
        }

        public List<Guard> DayShiftGuards { get; set; }
        public List<Guard> NightShiftGuards { get; set; }
        public List<Guard> OffDutyGuards { get; set; }

        public RosterViewModel()
        {
            DayShiftGuards = new List<Guard>();
            NightShiftGuards = new List<Guard>();
            OffDutyGuards = new List<Guard>();
            SelectedGuardIds = "";
        }
    }

    public class RosterDisplayViewModel
    {
        public DateTime RosterDate { get; set; }
        public string Site { get; set; }
        public List<Guard> DayShiftGuards { get; set; }
        public List<Guard> NightShiftGuards { get; set; }
        public List<Guard> OffDutyGuards { get; set; }

        public RosterDisplayViewModel()
        {
            DayShiftGuards = new List<Guard>();
            NightShiftGuards = new List<Guard>();
            OffDutyGuards = new List<Guard>();
        }
    }
}