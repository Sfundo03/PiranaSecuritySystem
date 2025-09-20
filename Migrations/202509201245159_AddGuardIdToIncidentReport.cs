namespace PiranaSecuritySystem.Migrations
{
    using System;
    using System.Data.Entity.Migrations;
    
    public partial class AddGuardIdToIncidentReport : DbMigration
    {
        public override void Up()
        {
            DropForeignKey("dbo.IncidentReports", "ResidentId", "dbo.Residents");
            DropIndex("dbo.IncidentReports", new[] { "ResidentId" });
            AddColumn("dbo.IncidentReports", "GuardId", c => c.Int());
            AlterColumn("dbo.IncidentReports", "ResidentId", c => c.Int());
            CreateIndex("dbo.IncidentReports", "ResidentId");
            CreateIndex("dbo.IncidentReports", "GuardId");
            AddForeignKey("dbo.IncidentReports", "GuardId", "dbo.Guards", "GuardId");
            AddForeignKey("dbo.IncidentReports", "ResidentId", "dbo.Residents", "ResidentId");
        }
        
        public override void Down()
        {
            DropForeignKey("dbo.IncidentReports", "ResidentId", "dbo.Residents");
            DropForeignKey("dbo.IncidentReports", "GuardId", "dbo.Guards");
            DropIndex("dbo.IncidentReports", new[] { "GuardId" });
            DropIndex("dbo.IncidentReports", new[] { "ResidentId" });
            AlterColumn("dbo.IncidentReports", "ResidentId", c => c.Int(nullable: false));
            DropColumn("dbo.IncidentReports", "GuardId");
            CreateIndex("dbo.IncidentReports", "ResidentId");
            AddForeignKey("dbo.IncidentReports", "ResidentId", "dbo.Residents", "ResidentId", cascadeDelete: true);
        }
    }
}
