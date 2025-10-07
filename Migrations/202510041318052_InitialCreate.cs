namespace PiranaSecuritySystem.Migrations
{
    using System;
    using System.Data.Entity.Migrations;
    
    public partial class InitialCreate : DbMigration
    {
        public override void Up()
        {
            CreateTable(
                "dbo.TrainingEnrollments",
                c => new
                    {
                        Id = c.Int(nullable: false, identity: true),
                        TrainingSessionId = c.Int(nullable: false),
                        GuardId = c.Int(nullable: false),
                        EnrollmentDate = c.DateTime(nullable: false),
                    })
                .PrimaryKey(t => t.Id)
                .ForeignKey("dbo.Guards", t => t.GuardId, cascadeDelete: true)
                .ForeignKey("dbo.TrainingSessions", t => t.TrainingSessionId, cascadeDelete: true)
                .Index(t => t.TrainingSessionId)
                .Index(t => t.GuardId);
            
            CreateTable(
                "dbo.PasswordResetOTPs",
                c => new
                    {
                        Id = c.Int(nullable: false, identity: true),
                        Email = c.String(nullable: false),
                        OTP = c.String(nullable: false, maxLength: 6),
                        ExpiryTime = c.DateTime(nullable: false),
                        IsUsed = c.Boolean(nullable: false),
                        CreatedAt = c.DateTime(nullable: false),
                    })
                .PrimaryKey(t => t.Id);
            
            AddColumn("dbo.Attendances", "RosterId", c => c.Int());
            AddColumn("dbo.Attendances", "CheckInId", c => c.Int());
            AddColumn("dbo.GuardCheckIns", "RosterId", c => c.Int());
            AddColumn("dbo.IncidentReports", "FeedbackFileData", c => c.String());
            AddColumn("dbo.IncidentReports", "FeedbackFileName", c => c.String());
            AddColumn("dbo.IncidentReports", "FeedbackFileType", c => c.String());
            AddColumn("dbo.IncidentReports", "FeedbackFileSize", c => c.Long());
            AddColumn("dbo.Notifications", "RelatedIncidentId", c => c.Int());
            AddColumn("dbo.Notifications", "OldStatus", c => c.String(maxLength: 50));
            AddColumn("dbo.Notifications", "NewStatus", c => c.String(maxLength: 50));
            AddColumn("dbo.Notifications", "TrainingSessionId", c => c.Int());
            AddColumn("dbo.Notifications", "RosterId", c => c.Int());
            AddColumn("dbo.TrainingSessions", "Site", c => c.String(nullable: false, maxLength: 50));
            AlterColumn("dbo.ShiftRosters", "GeneratedDate", c => c.DateTime(nullable: false, precision: 7, storeType: "datetime2"));
            CreateIndex("dbo.Attendances", "RosterId");
            CreateIndex("dbo.Attendances", "CheckInId");
            CreateIndex("dbo.GuardCheckIns", "RosterId");
            CreateIndex("dbo.Notifications", "TrainingSessionId");
            CreateIndex("dbo.Notifications", "RosterId");
            AddForeignKey("dbo.GuardCheckIns", "RosterId", "dbo.ShiftRosters", "RosterId");
            AddForeignKey("dbo.Attendances", "CheckInId", "dbo.GuardCheckIns", "CheckInId");
            AddForeignKey("dbo.Attendances", "RosterId", "dbo.ShiftRosters", "RosterId");
            AddForeignKey("dbo.Notifications", "RosterId", "dbo.ShiftRosters", "RosterId");
            AddForeignKey("dbo.Notifications", "TrainingSessionId", "dbo.TrainingSessions", "Id");
            DropColumn("dbo.TrainingSessions", "Location");
        }
        
        public override void Down()
        {
            AddColumn("dbo.TrainingSessions", "Location", c => c.String(nullable: false, maxLength: 100));
            DropForeignKey("dbo.Notifications", "TrainingSessionId", "dbo.TrainingSessions");
            DropForeignKey("dbo.TrainingEnrollments", "TrainingSessionId", "dbo.TrainingSessions");
            DropForeignKey("dbo.TrainingEnrollments", "GuardId", "dbo.Guards");
            DropForeignKey("dbo.Notifications", "RosterId", "dbo.ShiftRosters");
            DropForeignKey("dbo.Attendances", "RosterId", "dbo.ShiftRosters");
            DropForeignKey("dbo.Attendances", "CheckInId", "dbo.GuardCheckIns");
            DropForeignKey("dbo.GuardCheckIns", "RosterId", "dbo.ShiftRosters");
            DropIndex("dbo.TrainingEnrollments", new[] { "GuardId" });
            DropIndex("dbo.TrainingEnrollments", new[] { "TrainingSessionId" });
            DropIndex("dbo.Notifications", new[] { "RosterId" });
            DropIndex("dbo.Notifications", new[] { "TrainingSessionId" });
            DropIndex("dbo.GuardCheckIns", new[] { "RosterId" });
            DropIndex("dbo.Attendances", new[] { "CheckInId" });
            DropIndex("dbo.Attendances", new[] { "RosterId" });
            AlterColumn("dbo.ShiftRosters", "GeneratedDate", c => c.DateTime(nullable: false));
            DropColumn("dbo.TrainingSessions", "Site");
            DropColumn("dbo.Notifications", "RosterId");
            DropColumn("dbo.Notifications", "TrainingSessionId");
            DropColumn("dbo.Notifications", "NewStatus");
            DropColumn("dbo.Notifications", "OldStatus");
            DropColumn("dbo.Notifications", "RelatedIncidentId");
            DropColumn("dbo.IncidentReports", "FeedbackFileSize");
            DropColumn("dbo.IncidentReports", "FeedbackFileType");
            DropColumn("dbo.IncidentReports", "FeedbackFileName");
            DropColumn("dbo.IncidentReports", "FeedbackFileData");
            DropColumn("dbo.GuardCheckIns", "RosterId");
            DropColumn("dbo.Attendances", "CheckInId");
            DropColumn("dbo.Attendances", "RosterId");
            DropTable("dbo.PasswordResetOTPs");
            DropTable("dbo.TrainingEnrollments");
        }
    }
}
