namespace PiranaSecuritySystem.Migrations
{
    using System;
    using System.Data.Entity.Migrations;
    
    public partial class InitialCreate : DbMigration
    {
        public override void Up()
        {
            CreateTable(
                "dbo.Attendances",
                c => new
                    {
                        AttendanceId = c.Int(nullable: false, identity: true),
                        GuardId = c.Int(nullable: false),
                        CheckInTime = c.DateTime(nullable: false),
                        CheckOutTime = c.DateTime(),
                        HoursWorked = c.Double(nullable: false),
                        AttendanceDate = c.DateTime(nullable: false),
                    })
                .PrimaryKey(t => t.AttendanceId)
                .ForeignKey("dbo.Guards", t => t.GuardId, cascadeDelete: true)
                .Index(t => t.GuardId);
            
            CreateTable(
                "dbo.Guards",
                c => new
                    {
                        GuardId = c.Int(nullable: false, identity: true),
                        Guard_FName = c.String(nullable: false),
                        Guard_LName = c.String(nullable: false),
                        IdentityNumber = c.String(nullable: false, maxLength: 13),
                        Gender = c.String(nullable: false, maxLength: 1),
                        PSIRAnumber = c.String(nullable: false),
                        PhoneNumber = c.String(nullable: false, maxLength: 10),
                        Emergency_CellNo = c.String(nullable: false, maxLength: 10),
                        Email = c.String(nullable: false),
                        Address = c.String(nullable: false),
                        Street = c.String(),
                        City = c.String(),
                        PostalCode = c.String(),
                        DateRegistered = c.DateTime(nullable: false),
                        Site = c.String(),
                        SiteUsername = c.String(),
                        UserId = c.String(),
                        IsActive = c.Boolean(nullable: false),
                    })
                .PrimaryKey(t => t.GuardId);
            
            CreateTable(
                "dbo.GuardCheckIns",
                c => new
                    {
                        CheckInId = c.Int(nullable: false, identity: true),
                        GuardId = c.Int(nullable: false),
                        CheckInTime = c.DateTime(nullable: false),
                        Status = c.String(nullable: false, maxLength: 20),
                        CreatedDate = c.DateTime(nullable: false),
                    })
                .PrimaryKey(t => t.CheckInId)
                .ForeignKey("dbo.Guards", t => t.GuardId, cascadeDelete: true)
                .Index(t => t.GuardId);
            
            CreateTable(
                "dbo.Directors",
                c => new
                    {
                        DirectorId = c.Int(nullable: false, identity: true),
                        FullName = c.String(nullable: false, maxLength: 100),
                        Email = c.String(nullable: false),
                        Password = c.String(nullable: false),
                        PhoneNumber = c.String(),
                        IsActive = c.Boolean(nullable: false),
                        DateRegistered = c.DateTime(nullable: false),
                    })
                .PrimaryKey(t => t.DirectorId);
            
            CreateTable(
                "dbo.GuardRates",
                c => new
                    {
                        GuardRateId = c.Int(nullable: false, identity: true),
                        GuardId = c.Int(nullable: false),
                        Rate = c.Decimal(nullable: false, precision: 18, scale: 2),
                        EffectiveDate = c.DateTime(nullable: false),
                        IsActive = c.Boolean(nullable: false),
                    })
                .PrimaryKey(t => t.GuardRateId)
                .ForeignKey("dbo.Guards", t => t.GuardId, cascadeDelete: true)
                .Index(t => t.GuardId);
            
            CreateTable(
                "dbo.IncidentReports",
                c => new
                    {
                        IncidentReportId = c.Int(nullable: false, identity: true),
                        ResidentId = c.Int(nullable: false),
                        IncidentType = c.String(nullable: false),
                        Location = c.String(),
                        EmergencyContact = c.String(),
                        Feedback = c.String(),
                        FeedbackAttachment = c.String(),
                        FeedbackDate = c.DateTime(),
                        ReportDate = c.DateTime(nullable: false),
                        Status = c.String(),
                        Description = c.String(nullable: false),
                        Priority = c.String(),
                        ReportedBy = c.String(),
                        CreatedBy = c.String(),
                        CreatedDate = c.DateTime(nullable: false),
                    })
                .PrimaryKey(t => t.IncidentReportId)
                .ForeignKey("dbo.Residents", t => t.ResidentId, cascadeDelete: true)
                .Index(t => t.ResidentId);
            
            CreateTable(
                "dbo.Residents",
                c => new
                    {
                        ResidentId = c.Int(nullable: false, identity: true),
                        FullName = c.String(nullable: false),
                        Email = c.String(nullable: false),
                        PhoneNumber = c.String(nullable: false),
                        Address = c.String(nullable: false),
                        UnitNumber = c.String(nullable: false),
                        Password = c.String(nullable: false),
                        DateRegistered = c.DateTime(nullable: false),
                        IsActive = c.Boolean(nullable: false),
                        EmergencyContact = c.String(),
                        CreatedAt = c.DateTime(nullable: false),
                    })
                .PrimaryKey(t => t.ResidentId);
            
            CreateTable(
                "dbo.Instructors",
                c => new
                    {
                        Id = c.Int(nullable: false, identity: true),
                        EmployeeId = c.String(nullable: false, maxLength: 50),
                        FullName = c.String(nullable: false, maxLength: 100),
                        Email = c.String(nullable: false, maxLength: 100),
                        PhoneNumber = c.String(nullable: false, maxLength: 20),
                        Specialization = c.String(maxLength: 200),
                        DateRegistered = c.DateTime(nullable: false),
                        Site = c.String(),
                        SiteUsername = c.String(),
                        IsActive = c.Boolean(nullable: false),
                        UserId = c.String(nullable: false, maxLength: 128),
                    })
                .PrimaryKey(t => t.Id)
                .ForeignKey("dbo.AspNetUsers", t => t.UserId, cascadeDelete: true)
                .Index(t => t.UserId);
            
            CreateTable(
                "dbo.ShiftRosters",
                c => new
                    {
                        RosterId = c.Int(nullable: false, identity: true),
                        RosterDate = c.DateTime(nullable: false),
                        ShiftType = c.String(nullable: false),
                        GuardId = c.Int(nullable: false),
                        CreatedDate = c.DateTime(nullable: false),
                        ModifiedDate = c.DateTime(),
                        Location = c.String(),
                        Status = c.String(),
                        InstructorName = c.String(),
                        Specialization = c.String(),
                        RosterData = c.String(),
                        StartDate = c.DateTime(nullable: false),
                        TrainingType = c.String(),
                        EndDate = c.DateTime(nullable: false),
                        Instructor_Id = c.Int(),
                    })
                .PrimaryKey(t => t.RosterId)
                .ForeignKey("dbo.Guards", t => t.GuardId, cascadeDelete: true)
                .ForeignKey("dbo.Instructors", t => t.Instructor_Id)
                .Index(t => t.GuardId)
                .Index(t => t.Instructor_Id);
            
            CreateTable(
                "dbo.AspNetUsers",
                c => new
                    {
                        Id = c.String(nullable: false, maxLength: 128),
                        FullName = c.String(),
                        IsActive = c.Boolean(nullable: false),
                        LastLoginDate = c.DateTime(),
                        DateCreated = c.DateTime(nullable: false),
                        Email = c.String(maxLength: 256),
                        EmailConfirmed = c.Boolean(nullable: false),
                        PasswordHash = c.String(),
                        SecurityStamp = c.String(),
                        PhoneNumber = c.String(),
                        PhoneNumberConfirmed = c.Boolean(nullable: false),
                        TwoFactorEnabled = c.Boolean(nullable: false),
                        LockoutEndDateUtc = c.DateTime(),
                        LockoutEnabled = c.Boolean(nullable: false),
                        AccessFailedCount = c.Int(nullable: false),
                        UserName = c.String(nullable: false, maxLength: 256),
                    })
                .PrimaryKey(t => t.Id)
                .Index(t => t.UserName, unique: true, name: "UserNameIndex");
            
            CreateTable(
                "dbo.AspNetUserClaims",
                c => new
                    {
                        Id = c.Int(nullable: false, identity: true),
                        UserId = c.String(nullable: false, maxLength: 128),
                        ClaimType = c.String(),
                        ClaimValue = c.String(),
                    })
                .PrimaryKey(t => t.Id)
                .ForeignKey("dbo.AspNetUsers", t => t.UserId, cascadeDelete: true)
                .Index(t => t.UserId);
            
            CreateTable(
                "dbo.AspNetUserLogins",
                c => new
                    {
                        LoginProvider = c.String(nullable: false, maxLength: 128),
                        ProviderKey = c.String(nullable: false, maxLength: 128),
                        UserId = c.String(nullable: false, maxLength: 128),
                    })
                .PrimaryKey(t => new { t.LoginProvider, t.ProviderKey, t.UserId })
                .ForeignKey("dbo.AspNetUsers", t => t.UserId, cascadeDelete: true)
                .Index(t => t.UserId);
            
            CreateTable(
                "dbo.AspNetUserRoles",
                c => new
                    {
                        UserId = c.String(nullable: false, maxLength: 128),
                        RoleId = c.String(nullable: false, maxLength: 128),
                    })
                .PrimaryKey(t => new { t.UserId, t.RoleId })
                .ForeignKey("dbo.AspNetUsers", t => t.UserId, cascadeDelete: true)
                .ForeignKey("dbo.AspNetRoles", t => t.RoleId, cascadeDelete: true)
                .Index(t => t.UserId)
                .Index(t => t.RoleId);
            
            CreateTable(
                "dbo.Notifications",
                c => new
                    {
                        NotificationId = c.Int(nullable: false, identity: true),
                        ResidentId = c.Int(),
                        DirectorId = c.Int(),
                        AdminId = c.Int(),
                        GuardId = c.Int(),
                        InstructorId = c.Int(),
                        UserId = c.String(nullable: false, maxLength: 128),
                        UserType = c.String(nullable: false, maxLength: 20),
                        Title = c.String(nullable: false, maxLength: 200),
                        Message = c.String(nullable: false, maxLength: 500),
                        IsRead = c.Boolean(nullable: false),
                        CreatedAt = c.DateTime(nullable: false),
                        DateRead = c.DateTime(),
                        RelatedUrl = c.String(maxLength: 200),
                        NotificationType = c.String(maxLength: 50),
                        IsImportant = c.Boolean(nullable: false),
                        Source = c.String(),
                        ActionRequired = c.String(maxLength: 100),
                        ExpiryDate = c.DateTime(),
                        PriorityLevel = c.Int(nullable: false),
                    })
                .PrimaryKey(t => t.NotificationId)
                .ForeignKey("dbo.AspNetUsers", t => t.UserId, cascadeDelete: true)
                .Index(t => t.UserId);
            
            CreateTable(
                "dbo.Payrolls",
                c => new
                    {
                        PayrollId = c.Int(nullable: false, identity: true),
                        GuardId = c.Int(nullable: false),
                        PayPeriodStart = c.DateTime(nullable: false),
                        PayPeriodEnd = c.DateTime(nullable: false),
                        TotalHours = c.Double(nullable: false),
                        HourlyRate = c.Decimal(nullable: false, precision: 18, scale: 2),
                        GrossPay = c.Decimal(nullable: false, precision: 18, scale: 2),
                        TaxAmount = c.Decimal(nullable: false, precision: 18, scale: 2),
                        NetPay = c.Decimal(nullable: false, precision: 18, scale: 2),
                        PayDate = c.DateTime(nullable: false),
                        Status = c.String(),
                        PaymentMethod = c.String(),
                    })
                .PrimaryKey(t => t.PayrollId)
                .ForeignKey("dbo.Guards", t => t.GuardId, cascadeDelete: true)
                .Index(t => t.GuardId);
            
            CreateTable(
                "dbo.AspNetRoles",
                c => new
                    {
                        Id = c.String(nullable: false, maxLength: 128),
                        Name = c.String(nullable: false, maxLength: 256),
                    })
                .PrimaryKey(t => t.Id)
                .Index(t => t.Name, unique: true, name: "RoleNameIndex");
            
            CreateTable(
                "dbo.TaxConfigurations",
                c => new
                    {
                        TaxConfigId = c.Int(nullable: false, identity: true),
                        TaxYear = c.Int(nullable: false),
                        TaxPercentage = c.Decimal(nullable: false, precision: 18, scale: 2),
                        TaxThreshold = c.Decimal(nullable: false, precision: 18, scale: 2),
                        IsActive = c.Boolean(nullable: false),
                    })
                .PrimaryKey(t => t.TaxConfigId);
            
            CreateTable(
                "dbo.TrainingSessions",
                c => new
                    {
                        Id = c.Int(nullable: false, identity: true),
                        Title = c.String(nullable: false, maxLength: 100),
                        StartDate = c.DateTime(nullable: false),
                        EndDate = c.DateTime(nullable: false),
                        Capacity = c.Int(nullable: false),
                        Location = c.String(nullable: false, maxLength: 100),
                    })
                .PrimaryKey(t => t.Id);
            
        }
        
        public override void Down()
        {
            DropForeignKey("dbo.AspNetUserRoles", "RoleId", "dbo.AspNetRoles");
            DropForeignKey("dbo.Payrolls", "GuardId", "dbo.Guards");
            DropForeignKey("dbo.Notifications", "UserId", "dbo.AspNetUsers");
            DropForeignKey("dbo.Instructors", "UserId", "dbo.AspNetUsers");
            DropForeignKey("dbo.AspNetUserRoles", "UserId", "dbo.AspNetUsers");
            DropForeignKey("dbo.AspNetUserLogins", "UserId", "dbo.AspNetUsers");
            DropForeignKey("dbo.AspNetUserClaims", "UserId", "dbo.AspNetUsers");
            DropForeignKey("dbo.ShiftRosters", "Instructor_Id", "dbo.Instructors");
            DropForeignKey("dbo.ShiftRosters", "GuardId", "dbo.Guards");
            DropForeignKey("dbo.IncidentReports", "ResidentId", "dbo.Residents");
            DropForeignKey("dbo.GuardRates", "GuardId", "dbo.Guards");
            DropForeignKey("dbo.Attendances", "GuardId", "dbo.Guards");
            DropForeignKey("dbo.GuardCheckIns", "GuardId", "dbo.Guards");
            DropIndex("dbo.AspNetRoles", "RoleNameIndex");
            DropIndex("dbo.Payrolls", new[] { "GuardId" });
            DropIndex("dbo.Notifications", new[] { "UserId" });
            DropIndex("dbo.AspNetUserRoles", new[] { "RoleId" });
            DropIndex("dbo.AspNetUserRoles", new[] { "UserId" });
            DropIndex("dbo.AspNetUserLogins", new[] { "UserId" });
            DropIndex("dbo.AspNetUserClaims", new[] { "UserId" });
            DropIndex("dbo.AspNetUsers", "UserNameIndex");
            DropIndex("dbo.ShiftRosters", new[] { "Instructor_Id" });
            DropIndex("dbo.ShiftRosters", new[] { "GuardId" });
            DropIndex("dbo.Instructors", new[] { "UserId" });
            DropIndex("dbo.IncidentReports", new[] { "ResidentId" });
            DropIndex("dbo.GuardRates", new[] { "GuardId" });
            DropIndex("dbo.GuardCheckIns", new[] { "GuardId" });
            DropIndex("dbo.Attendances", new[] { "GuardId" });
            DropTable("dbo.TrainingSessions");
            DropTable("dbo.TaxConfigurations");
            DropTable("dbo.AspNetRoles");
            DropTable("dbo.Payrolls");
            DropTable("dbo.Notifications");
            DropTable("dbo.AspNetUserRoles");
            DropTable("dbo.AspNetUserLogins");
            DropTable("dbo.AspNetUserClaims");
            DropTable("dbo.AspNetUsers");
            DropTable("dbo.ShiftRosters");
            DropTable("dbo.Instructors");
            DropTable("dbo.Residents");
            DropTable("dbo.IncidentReports");
            DropTable("dbo.GuardRates");
            DropTable("dbo.Directors");
            DropTable("dbo.GuardCheckIns");
            DropTable("dbo.Guards");
            DropTable("dbo.Attendances");
        }
    }
}
