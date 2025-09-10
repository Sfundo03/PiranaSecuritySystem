namespace PiranaSecuritySystem.Migrations
{
    using System;
    using System.Data.Entity.Migrations;
    
    public partial class AddIsActiveToApplicationUser : DbMigration
    {
        public override void Up()
        {
            AddColumn("dbo.AspNetUsers", "FullName", c => c.String());
            AddColumn("dbo.AspNetUsers", "IsActive", c => c.Boolean(nullable: false));
            AddColumn("dbo.AspNetUsers", "LastLoginDate", c => c.DateTime());
            AddColumn("dbo.AspNetUsers", "DateCreated", c => c.DateTime(nullable: false));
        }
        
        public override void Down()
        {
            DropColumn("dbo.AspNetUsers", "DateCreated");
            DropColumn("dbo.AspNetUsers", "LastLoginDate");
            DropColumn("dbo.AspNetUsers", "IsActive");
            DropColumn("dbo.AspNetUsers", "FullName");
        }
    }
}
