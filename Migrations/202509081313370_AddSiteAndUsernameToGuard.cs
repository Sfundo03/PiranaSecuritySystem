namespace PiranaSecuritySystem.Migrations
{
    using System;
    using System.Data.Entity.Migrations;
    
    public partial class AddSiteAndUsernameToGuard : DbMigration
    {
        public override void Up()
        {
            AddColumn("dbo.Guards", "Site", c => c.String());
            AddColumn("dbo.Guards", "SiteUsername", c => c.String());
        }
        
        public override void Down()
        {
            DropColumn("dbo.Guards", "SiteUsername");
            DropColumn("dbo.Guards", "Site");
        }
    }
}
