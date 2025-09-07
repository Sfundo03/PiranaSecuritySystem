// Models/Instructor.cs
using PiranaSecuritySystem.Models;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PiranaSecuritySystem.Models
{
    public class Instructor
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        [Required(ErrorMessage = "Employee ID is required")]
        [Display(Name = "Employee ID")]
        [StringLength(50, ErrorMessage = "Employee ID cannot exceed 50 characters")]
        public string EmployeeId { get; set; }

        [Required(ErrorMessage = "Full Name is required")]
        [Display(Name = "Full Name")]
        [StringLength(100, ErrorMessage = "Full Name cannot exceed 100 characters")]
        public string FullName { get; set; }

        [Required(ErrorMessage = "Email Address is required")]
        [EmailAddress(ErrorMessage = "Invalid Email Address format")]
        [Display(Name = "Email Address")]
        [StringLength(100, ErrorMessage = "Email cannot exceed 100 characters")]
        public string Email { get; set; }

        [Required(ErrorMessage = "Phone Number is required")]
        [Phone(ErrorMessage = "Invalid Phone Number format")]
        [Display(Name = "Phone Number")]
        [StringLength(20, ErrorMessage = "Phone Number cannot exceed 20 characters")]
        public string PhoneNumber { get; set; }

        [Display(Name = "Specialization")]
        [StringLength(200, ErrorMessage = "Specialization cannot exceed 200 characters")]
        public string Specialization { get; set; }

        [Display(Name = "Date Registered")]
        [DataType(DataType.DateTime)]
        public DateTime DateRegistered { get; set; } = DateTime.Now;

        [Display(Name = "Is Active")]
        public bool IsActive { get; set; } = true;

        // Foreign key to AspNetUsers table
        [Required(ErrorMessage = "User ID is required")]
        [StringLength(450, ErrorMessage = "User ID cannot exceed 450 characters")]
        public string UserId { get; set; }

        [ForeignKey("UserId")]
        public virtual ApplicationUser User { get; set; }

        // Navigation property for Duty Rosters
        public virtual ICollection<Shift> Shifts { get; set; }
    }
}