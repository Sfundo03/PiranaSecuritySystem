using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Web;

namespace PiranaSecuritySystem.Models
{
    public class Director
    {
        public int DirectorId { get; set; }

        [Required]
        [StringLength(100)]
        public string FullName { get; set; }

        [Required]
        [EmailAddress]
        public string Email { get; set; }

        [Required]
        public string Password { get; set; }

        public string PhoneNumber { get; set; }
        public bool IsActive { get; set; } = true;
        public DateTime DateRegistered { get; set; } = DateTime.Now;
    }
}