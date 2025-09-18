namespace PiranaSecuritySystem.Migrations
{
    using System;
    using System.Data.Entity.Migrations;
    
    public partial class newStatus : DbMigration
    {
        public override void Up()
        {
            AddColumn("dbo.Guards", "Status", c => c.String());
        }
        
        public override void Down()
        {
            DropColumn("dbo.Guards", "Status");
        }
    }
}
