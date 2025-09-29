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
        private string _fullName;
        private DateTime _dateCreated;
        private DateTime _createdAt;

        public string FullName
        {
            get => _fullName ?? string.Empty;
            set => _fullName = value;
        }

        public bool IsActive { get; set; } = true;
        public DateTime? LastLoginDate { get; set; }

        public DateTime DateCreated
        {
            get => _dateCreated;
            set => _dateCreated = EnsureValidDateTime(value);
        }

        public DateTime CreatedAt
        {
            get => _createdAt;
            internal set => _createdAt = EnsureValidDateTime(value);
        }

        public ApplicationUser()
        {
            FullName = string.Empty;
            _dateCreated = DateTime.Now;
            _createdAt = DateTime.Now;
        }

        // Helper method to ensure valid SQL Server datetime range
        private DateTime EnsureValidDateTime(DateTime dateTime)
        {
            // SQL Server datetime range: January 1, 1753, through December 31, 9999
            if (dateTime < new DateTime(1753, 1, 1))
                return new DateTime(1753, 1, 1);
            if (dateTime > new DateTime(9999, 12, 31))
                return new DateTime(9999, 12, 31);
            return dateTime;
        }

        public async Task<ClaimsIdentity> GenerateUserIdentityAsync(UserManager<ApplicationUser> manager)
        {
            var userIdentity = await manager.CreateIdentityAsync(this, DefaultAuthenticationTypes.ApplicationCookie);
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
        public DbSet<TrainingEnrollment> TrainingEnrollments { get; set; }

        protected override void OnModelCreating(DbModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Configure ApplicationUser properties
            modelBuilder.Entity<ApplicationUser>()
                .Property(u => u.FullName)
                .IsOptional()
                .HasMaxLength(255);

            // Configure datetime properties to use datetime2 for better range
            modelBuilder.Entity<ApplicationUser>()
                .Property(u => u.DateCreated)
                .HasColumnType("datetime2");

            modelBuilder.Entity<ApplicationUser>()
                .Property(u => u.CreatedAt)
                .HasColumnType("datetime2");

            modelBuilder.Entity<ApplicationUser>()
                .Property(u => u.LastLoginDate)
                .HasColumnType("datetime2");
        }


        public static ApplicationDbContext Create()
        {
            return new ApplicationDbContext();
        }
    }
}