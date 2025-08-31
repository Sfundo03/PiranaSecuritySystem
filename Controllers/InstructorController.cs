using Microsoft.AspNet.Identity;
using PiranaSecuritSystem.Models;
using PiranaSecuritySystem.Models;
using System;
using System.Linq;
using System.Web.Mvc;

namespace PiranaSecuritySystem.Controllers
{
    [Authorize(Roles = "Instructor")]
    public class InstructorController : Controller
    {
        private ApplicationDbContext db = new ApplicationDbContext();

        // GET: Instructor/Dashboard
        public ActionResult Dashboard()
        {
            try
            {
                var currentUserId = User.Identity.GetUserId();

                if (string.IsNullOrEmpty(currentUserId))
                {
                    TempData["ErrorMessage"] = "User not authenticated. Please log in again.";
                    return RedirectToAction("Login", "Account");
                }

                var instructor = db.Instructors.FirstOrDefault(i => i.UserId == currentUserId);

                if (instructor == null)
                {
                    TempData["ErrorMessage"] = "Instructor profile not found. Please contact administrator.";
                    return RedirectToAction("ProfileNotFound", "Error");
                }

                return View(instructor);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error in Dashboard: {ex.Message}");
                TempData["ErrorMessage"] = "An error occurred while loading the dashboard.";
                return RedirectToAction("Error", "Home");
            }
        }

        // GET: Instructor/CreateRoster
        public ActionResult CreateRoster()
        {
            try
            {
                var currentUserId = User.Identity.GetUserId();
                var instructor = db.Instructors.FirstOrDefault(i => i.UserId == currentUserId);

                if (instructor == null)
                {
                    TempData["ErrorMessage"] = "Instructor profile not found.";
                    return RedirectToAction("Dashboard");
                }

                ViewBag.InstructorName = instructor.FullName;
                ViewBag.Specialization = instructor.Specialization;

                return View();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error in CreateRoster: {ex.Message}");
                TempData["ErrorMessage"] = "An error occurred while loading the roster creation page.";
                return RedirectToAction("Dashboard");
            }
        }

        // POST: Instructor/GenerateRoster
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult GenerateRoster(DateTime? startDate, DateTime? endDate, string trainingType)
        {
            try
            {
                var currentUserId = User.Identity.GetUserId();
                var instructor = db.Instructors.FirstOrDefault(i => i.UserId == currentUserId);

                if (instructor != null)
                {
                    var start = startDate ?? DateTime.Today;
                    var end = endDate ?? DateTime.Today.AddDays(7);

                    var roster = new Shift
                    {
                        InstructorName = instructor.FullName,
                        GeneratedDate = DateTime.Now,
                        Specialization = instructor.Specialization,
                        RosterData = GenerateRosterData(start, end, trainingType, instructor.FullName, instructor.Specialization),
                        StartDate = start,
                        EndDate = end,
                        TrainingType = trainingType
                    };

                    db.Shifts.Add(roster);
                    db.SaveChanges();

                    TempData["SuccessMessage"] = "Training roster generated successfully!";
                    return RedirectToAction("ViewRoster", new { id = roster.ShiftID });
                }
                else
                {
                    TempData["ErrorMessage"] = "Failed to generate roster. Instructor profile not found.";
                    return RedirectToAction("CreateRoster");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error in GenerateRoster: {ex.Message}");
                TempData["ErrorMessage"] = "An error occurred while generating the roster.";
                return RedirectToAction("CreateRoster");
            }
        }

        // GET: Instructor/ViewRoster
        public ActionResult ViewRoster(int id)
        {
            var roster = db.Shifts.Find(id);
            if (roster == null)
            {
                TempData["ErrorMessage"] = "Roster not found.";
                return RedirectToAction("Dashboard");
            }

            return View(roster);
        }

        private string GenerateRosterData(DateTime startDate, DateTime endDate, string trainingType, string instructorName, string specialization)
        {
            return $@"Training Roster - {startDate:MMMM dd, yyyy} to {endDate:MMMM dd, yyyy}
====================================================
Instructor: {instructorName}
Specialization: {specialization}
Training Type: {trainingType ?? "General Security Training"}
Generated: {DateTime.Now:yyyy-MM-dd HH:mm}

Schedule:
{GenerateWeeklySchedule(startDate, endDate)}

Notes:
- All participants must bring required equipment
- Practical sessions require appropriate attire
- Assessments will be conducted throughout the period";
        }

        private string GenerateWeeklySchedule(DateTime start, DateTime end)
        {
            var schedule = "";
            var current = start;

            while (current <= end)
            {
                schedule += $"{current:dddd, MMMM dd}:\n";

                switch (current.DayOfWeek)
                {
                    case DayOfWeek.Monday:
                        schedule += "• 09:00 - 12:00: Firearm Safety - Training Room A\n";
                        schedule += "• 14:00 - 17:00: First Aid Refresher - Medical Lab\n";
                        break;
                    case DayOfWeek.Tuesday:
                        schedule += "• 08:00 - 12:00: Defensive Tactics - Training Ground\n";
                        schedule += "• 13:00 - 16:00: Surveillance Techniques - Observation Room\n";
                        break;
                    case DayOfWeek.Wednesday:
                        schedule += "• 10:00 - 15:00: Field Training Exercise - Outdoor Range\n";
                        schedule += "• 16:00 - 17:00: Debriefing - Conference Room\n";
                        break;
                    case DayOfWeek.Thursday:
                        schedule += "• 09:00 - 12:00: Legal Aspects - Classroom B\n";
                        schedule += "• 14:00 - 17:00: Practical Assessment - Training Ground\n";
                        break;
                    case DayOfWeek.Friday:
                        schedule += "• 08:00 - 12:00: Advanced Tactics - Simulation Room\n";
                        schedule += "• 13:00 - 16:00: Final Evaluation - All Locations\n";
                        break;
                    case DayOfWeek.Saturday:
                        schedule += "• 10:00 - 14:00: Optional Practice Session\n";
                        break;
                    case DayOfWeek.Sunday:
                        schedule += "• No scheduled activities - Rest Day\n";
                        break;
                }

                schedule += "\n";
                current = current.AddDays(1);
            }

            return schedule;
        }

        public ActionResult MyTrainings()
        {
            try
            {
                var currentUserId = User.Identity.GetUserId();
                var instructor = db.Instructors.FirstOrDefault(i => i.UserId == currentUserId);

                if (instructor == null)
                {
                    TempData["ErrorMessage"] = "Instructor profile not found.";
                    return RedirectToAction("Dashboard");
                }

                var trainings = db.Shifts
                    .Where(s => s.InstructorName == instructor.FullName)
                    .OrderByDescending(s => s.GeneratedDate)
                    .ToList();

                return View(trainings);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error in MyTrainings: {ex.Message}");
                TempData["ErrorMessage"] = "An error occurred while loading your trainings.";
                return RedirectToAction("Dashboard");
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                db.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}