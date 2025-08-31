using PiranaSecuritySystem.Models;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Web;

namespace PiranaSecuritSystem.Models
{
    public class Shift
    {
        [Key]
        public int ShiftID { get; set; }
        [ForeignKey("Guard")]
        public int GuardId { get; set; }

        [Column(TypeName = "datetime2")]
        public DateTime ShiftDate { get; set; }

        public string ShiftType { get; set; } // "Night", "Day", "Off"

        public virtual Guard Guard { get; set; }
        public string InstructorName { get; internal set; }
        public object GeneratedDate { get; internal set; }
        public string Specialization { get; internal set; }
        public string RosterData { get; internal set; }



        public DateTime StartDate { get; internal set; }


        public DateTime EndDate { get; internal set; }
        public string TrainingType { get; internal set; }



    }

}