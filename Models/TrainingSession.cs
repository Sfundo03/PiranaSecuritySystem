using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Web;

namespace PiranaSecuritySystem.Models
{
    public class TrainingSession
    {
        [Key]
        public int Id { get; set; }

        [Required(ErrorMessage = "Session title is required")]
        [StringLength(100)]
        public string Title { get; set; } = "";

        [Required(ErrorMessage = "Start date is required")]
        [DataType(DataType.DateTime)]
        public DateTime StartDate { get; set; }

        [Required(ErrorMessage = "End date is required")]
        [DataType(DataType.DateTime)]
        public DateTime EndDate { get; set; }

        [Range(1, 100, ErrorMessage = "Capacity must be between 1 and 100")]
        public int Capacity { get; set; }

        [Required]
        [StringLength(100)]
        public string Location { get; set; } = "";



    }
}