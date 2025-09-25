namespace PiranaSecuritySystem.Migrations
{
    using System;
    using System.Data.Entity.Migrations;

    public partial class UpdateDateTimeColumns : DbMigration
    {
        public override void Up()
        {
            // Update both RosterDate and CreatedDate columns
            AlterColumn("dbo.ShiftRosters", "RosterDate", c => c.DateTime(nullable: false, precision: 7, storeType: "datetime2"));
            AlterColumn("dbo.ShiftRosters", "CreatedDate", c => c.DateTime(nullable: false, precision: 7, storeType: "datetime2"));
        }

        public override void Down()
        {
            // Revert both columns back to datetime
            AlterColumn("dbo.ShiftRosters", "CreatedDate", c => c.DateTime(nullable: false));
            AlterColumn("dbo.ShiftRosters", "RosterDate", c => c.DateTime(nullable: false));
        }
    }
}