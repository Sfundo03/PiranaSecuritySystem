namespace PiranaSecuritySystem.Migrations
{
    using System;
    using System.Data.Entity.Migrations;
    
    public partial class UpdateGuardCheckInSystem : DbMigration
    {
        public override void Up()
        {
            AddColumn("dbo.GuardCheckIns", "IsLate", c => c.Boolean(nullable: false));
            AddColumn("dbo.GuardCheckIns", "ExpectedTime", c => c.String(maxLength: 10));
            AddColumn("dbo.GuardCheckIns", "ActualTime", c => c.String(maxLength: 10));
            AddColumn("dbo.GuardCheckIns", "SiteUsername", c => c.String(maxLength: 50));
        }
        
        public override void Down()
        {
            DropColumn("dbo.GuardCheckIns", "SiteUsername");
            DropColumn("dbo.GuardCheckIns", "ActualTime");
            DropColumn("dbo.GuardCheckIns", "ExpectedTime");
            DropColumn("dbo.GuardCheckIns", "IsLate");
        }
    }
}
