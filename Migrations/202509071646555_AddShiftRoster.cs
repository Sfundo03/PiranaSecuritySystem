namespace PiranaSecuritySystem.Migrations
{
    using System;
    using System.Data.Entity.Migrations;
    
    public partial class AddShiftRoster : DbMigration
    {
        public override void Up()
        {
            DropForeignKey("dbo.Shifts", "GuardId", "dbo.Guards");
            DropForeignKey("dbo.Shifts", "Instructor_Id", "dbo.Instructors");
            DropIndex("dbo.Shifts", new[] { "GuardId" });
            DropIndex("dbo.Shifts", new[] { "Instructor_Id" });
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
            
            DropTable("dbo.Shifts");
        }
        
        public override void Down()
        {
            CreateTable(
                "dbo.Shifts",
                c => new
                    {
                        ShiftId = c.Int(nullable: false, identity: true),
                        GuardId = c.Int(nullable: false),
                        ShiftDate = c.DateTime(nullable: false, precision: 7, storeType: "datetime2"),
                        ShiftType = c.String(),
                        StartTime = c.String(),
                        EndTime = c.String(),
                        Location = c.String(),
                        Status = c.String(),
                        InstructorName = c.String(),
                        GeneratedDate = c.DateTime(nullable: false),
                        Specialization = c.String(),
                        StartDate = c.DateTime(nullable: false),
                        RosterData = c.String(),
                        EndDate = c.DateTime(nullable: false),
                        TrainingType = c.String(),
                        Instructor_Id = c.Int(),
                    })
                .PrimaryKey(t => t.ShiftId);
            
            DropForeignKey("dbo.ShiftRosters", "Instructor_Id", "dbo.Instructors");
            DropForeignKey("dbo.ShiftRosters", "GuardId", "dbo.Guards");
            DropIndex("dbo.ShiftRosters", new[] { "Instructor_Id" });
            DropIndex("dbo.ShiftRosters", new[] { "GuardId" });
            DropTable("dbo.ShiftRosters");
            CreateIndex("dbo.Shifts", "Instructor_Id");
            CreateIndex("dbo.Shifts", "GuardId");
            AddForeignKey("dbo.Shifts", "Instructor_Id", "dbo.Instructors", "Id");
            AddForeignKey("dbo.Shifts", "GuardId", "dbo.Guards", "GuardId", cascadeDelete: true);
        }
    }
}
