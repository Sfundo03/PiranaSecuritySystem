using Microsoft.AspNet.Identity;
using Microsoft.AspNet.Identity.EntityFramework;
using PiranaSecuritSystem.Models;
using PiranaSecuritySystem.ViewModels;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Data.Entity;
using System.Security.Claims;
using System.Threading.Tasks;

namespace PiranaSecuritySystem.Models
{
    // You can add profile data for the user by adding more properties to your ApplicationUser class, please visit https://go.microsoft.com/fwlink/?LinkID=317594 to learn more.
    public class ApplicationUser : IdentityUser
    {
        [Display(Name = "Full Name")]
        public string FullName { get; set; }

        [Display(Name = "Last Login")]
        public DateTime? LastLoginDate { get; set; }

        public virtual ICollection<Notification> Notifications { get; set; }


        public async Task<ClaimsIdentity> GenerateUserIdentityAsync(UserManager<ApplicationUser> manager)
        {
            // Note the authenticationType must match the one defined in CookieAuthenticationOptions.AuthenticationType
            var userIdentity = await manager.CreateIdentityAsync(this, DefaultAuthenticationTypes.ApplicationCookie);
            // Add custom user claims here
            return userIdentity;
        }
    }

    public class ApplicationDbContext : IdentityDbContext<ApplicationUser>
    {
        public ApplicationDbContext()
            : base("DefaultConnection", throwIfV1Schema: false)
        {
        }

        // Add your DbSets here
        public DbSet<IncidentReport> IncidentReports { get; set; }

        public DbSet<Instructor> Instructors { get; set; }
        
        public DbSet<Resident> Residents { get; set; }
        public DbSet<Guard> Guards { get; set; }
        public DbSet<Shift> Shifts { get; set; }
        public List<int> SelectedGuardIDs { get; set; }
        public List<string> SelectedSpecializations { get; set; }
        public DbSet<GuardCheckIn> GuardCheckIns { get; set; }
        public DbSet<Notification> Notifications { get; set; }
        public DbSet<Director> Directors { get; set; }


        public static ApplicationDbContext Create()
        {
            return new ApplicationDbContext();
        }
    }
}