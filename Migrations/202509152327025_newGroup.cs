namespace PiranaSecuritySystem.Migrations
{
    using System;
    using System.Data.Entity.Migrations;
    
    public partial class newGroup : DbMigration
    {
        public override void Up()
        {
            AddColumn("dbo.Guards", "Group", c => c.String());
        }
        
        public override void Down()
        {
            DropColumn("dbo.Guards", "Group");
        }
    }
}
