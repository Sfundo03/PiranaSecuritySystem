using Microsoft.AspNet.Identity;
using Microsoft.AspNet.Identity.EntityFramework;

using PiranaSecuritySystem.ViewModels;
using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Security.Claims;
using System.Threading.Tasks;

namespace PiranaSecuritySystem.Models
{
    // You can add profile data for the user by adding more properties to your ApplicationUser class, please visit https://go.microsoft.com/fwlink/?LinkID=317594 to learn more.
    public class ApplicationUser : IdentityUser
    {
        // Add these properties to your existing ApplicationUser class
        public string FullName { get; set; }
        public bool IsActive { get; set; } = true; // Add this line
        public DateTime? LastLoginDate { get; set; }
        public DateTime DateCreated { get; set; } = DateTime.Now;
        public DateTime CreatedAt { get; internal set; }

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

        public DbSet<ResidentInfo> ResidentInfos { get; set; }
        public DbSet<TrainingSession> TrainingSessions { get; set; }
        public DbSet<ShiftRoster> ShiftRosters { get; set; }

        public DbSet<Instructor> Instructors { get; set; }
        public DbSet<Payroll> Payrolls { get; set; }
        public DbSet<GuardRate> GuardRates { get; set; }
        public DbSet<Attendance> Attendances { get; set; }
        public DbSet<TaxConfiguration> TaxConfigurations { get; set; }

        public DbSet<Resident> Residents { get; set; }
        public DbSet<Guard> Guards { get; set; }

        public List<int> SelectedGuardIDs { get; set; }
        public List<string> SelectedSpecializations { get; set; }
        public DbSet<GuardCheckIn> GuardCheckIns { get; set; }
        public DbSet<Notification> Notifications { get; set; }
        public DbSet<Director> Directors { get; set; }
        public object TrainingEnrollments { get; internal set; }

        public static ApplicationDbContext Create()
        {
            return new ApplicationDbContext();
        }
    }
}