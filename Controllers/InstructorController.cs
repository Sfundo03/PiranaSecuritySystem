using Microsoft.AspNet.Identity;
using PiranaSecuritySystem.Models;
using System;
using System.Collections.Generic;
using System.Data.Entity;
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
                var instructor = db.Instructors.FirstOrDefault(i => i.UserId == currentUserId);

                if (instructor == null)
                {
                    TempData["ErrorMessage"] = "Instructor profile not found.";
                    return RedirectToAction("Login", "Account");
                }

                return View(instructor);
            }
            catch (Exception ex)
            {
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

                // Get unique sites from Guards table
                var sites = db.Guards
                    .Where(g => g.Site != null && g.Site != "")
                    .Select(g => g.Site)
                    .Distinct()
                    .OrderBy(s => s)
                    .ToList();

                // If no sites found in database, use default sites
                if (!sites.Any())
                {
                    sites = new List<string> { "Site A", "Site B", "Site C" };
                }

                ViewBag.Sites = new SelectList(sites);
                ViewBag.InstructorName = instructor.FullName;

                var model = new RosterViewModel
                {
                    RosterDate = DateTime.Today
                };

                return View(model);
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = "An error occurred while loading the roster creation page.";
                return RedirectToAction("Dashboard");
            }
        }

        // AJAX method to get guards by site
        [HttpPost]
        public JsonResult GetGuardsBySite(string site)
        {
            try
            {
                if (string.IsNullOrEmpty(site))
                {
                    return Json(new { success = false, message = "Site is required" }, JsonRequestBehavior.AllowGet);
                }

                var guards = db.Guards
                    .Where(g => g.Site == site && g.IsActive)
                    .AsEnumerable() // Switch to client-side evaluation
                    .Select(g => new {
                        id = g.GuardId,
                        name = g.Guard_FName + " " + g.Guard_LName,
                        badge = g.GuardId.ToString()
                    })
                    .ToList();

                return Json(new { success = true, guards = guards }, JsonRequestBehavior.AllowGet);
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Error loading guards: " + ex.Message }, JsonRequestBehavior.AllowGet);
            }
        }

        // POST: Instructor/GenerateRoster
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult GenerateRoster(RosterViewModel model)
        {
            try
            {
                // Repopulate sites dropdown
                var sites = db.Guards
                    .Where(g => g.Site != null && g.Site != "")
                    .Select(g => g.Site)
                    .Distinct()
                    .OrderBy(s => s)
                    .ToList();

                if (!sites.Any())
                {
                    sites = new List<string> { "Site A", "Site B", "Site C" };
                }
                ViewBag.Sites = new SelectList(sites);

                if (!ModelState.IsValid)
                {
                    return View("CreateRoster", model);
                }

                var currentUserId = User.Identity.GetUserId();
                var instructor = db.Instructors.FirstOrDefault(i => i.UserId == currentUserId);

                if (instructor == null)
                {
                    TempData["ErrorMessage"] = "Instructor profile not found.";
                    return View("CreateRoster", model);
                }

                // Validate guards selection
                if (model.SelectedGuardIds == null || model.SelectedGuardIds.Count != 12)
                {
                    ModelState.AddModelError("SelectedGuardIds", "Please select exactly 12 guards.");
                    return View("CreateRoster", model);
                }

                // Get selected guards
                var selectedGuards = db.Guards
                    .Where(g => model.SelectedGuardIds.Contains(g.GuardId))
                    .ToList()
                    .Select(g => new Guard
                    {
                        GuardId = g.GuardId,
                        Guard_FName = g.Guard_FName,
                        Guard_LName = g.Guard_LName,
                        Site = g.Site

                    })
                    .ToList();

                // Create main roster
                var roster = new ShiftRoster
                {
                    RosterDate = model.RosterDate,
                    Site = model.Site,
                    InstructorName = instructor.FullName,
                    GeneratedDate = DateTime.Now,
                    RosterData = GenerateRosterData(model.RosterDate, model.Site, selectedGuards, instructor.FullName),
                    CreatedDate = DateTime.Now,
                    Status = "Active"
                };

                db.ShiftRosters.Add(roster);
                db.SaveChanges();

                // Create shift assignments
                CreateShiftAssignments(selectedGuards, model.Site, model.RosterDate);

                TempData["SuccessMessage"] = $"Roster for {model.Site} created successfully!";
                return RedirectToAction("ViewRoster", new { id = roster.RosterId });
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = "Error creating roster: " + ex.Message;

                // Repopulate sites on error
                var sites = db.Guards
                    .Where(g => g.Site != null && g.Site != "")
                    .Select(g => g.Site)
                    .Distinct()
                    .OrderBy(s => s)
                    .ToList();

                if (!sites.Any())
                {
                    sites = new List<string> { "Site A", "Site B", "Site C" };
                }
                ViewBag.Sites = new SelectList(sites);

                return View("CreateRoster", model);
            }
        }

        private void CreateShiftAssignments(List<Guard> guards, string site, DateTime rosterDate)
        {
            for (int i = 0; i < guards.Count; i++)
            {
                string shiftType = i < 4 ? "Day" : (i < 8 ? "Night" : "Off");

                var shift = new ShiftRoster
                {
                    RosterDate = rosterDate,
                    Site = site,
                    GuardId = guards[i].GuardId,
                    ShiftType = shiftType,
                    CreatedDate = DateTime.Now,
                    Status = "Active"
                };

                db.ShiftRosters.Add(shift);
            }
            db.SaveChanges();
        }

        private string GenerateRosterData(DateTime rosterDate, string site, List<Guard> guards, string instructorName)
        {
            var dayShift = guards.Take(4).ToList();
            var nightShift = guards.Skip(4).Take(4).ToList();
            var offDuty = guards.Skip(8).Take(4).ToList();

            return $@"Shift Roster - {rosterDate:MMMM dd, yyyy}
Site: {site}
Instructor: {instructorName}

Day Shift (06:00-18:00):
{string.Join("\n", dayShift.Select(g => $"- {g.Guard_FName} {g.Guard_LName}"))}

Night Shift (18:00-06:00):
{string.Join("\n", nightShift.Select(g => $"- {g.Guard_FName} {g.Guard_LName}"))}

Off Duty:
{string.Join("\n", offDuty.Select(g => $"- {g.Guard_FName} {g.Guard_LName}"))}";
        }

        // GET: Instructor/ViewRoster
        public ActionResult ViewRoster(int id)
        {
            var roster = db.ShiftRosters.Find(id);
            if (roster == null)
            {
                TempData["ErrorMessage"] = "Roster not found.";
                return RedirectToAction("Dashboard");
            }

            var shifts = db.ShiftRosters
                .Where(s => s.RosterDate == roster.RosterDate && s.Site == roster.Site)
                .Include("Guard")
                .ToList();

            var viewModel = new RosterDisplayViewModel
            {
                RosterId = id,
                RosterDate = roster.RosterDate,
                Site = roster.Site,
                DayShiftGuards = shifts.Where(s => s.ShiftType == "Day").Select(s => s.Guard).ToList(),
                NightShiftGuards = shifts.Where(s => s.ShiftType == "Night").Select(s => s.Guard).ToList(),
                OffDutyGuards = shifts.Where(s => s.ShiftType == "Off").Select(s => s.Guard).ToList()
            };

            return View(viewModel);
        }

        public ActionResult MyTrainings()
        {
            var currentUserId = User.Identity.GetUserId();
            var instructor = db.Instructors.FirstOrDefault(i => i.UserId == currentUserId);

            if (instructor == null)
            {
                TempData["ErrorMessage"] = "Instructor profile not found.";
                return RedirectToAction("Dashboard");
            }

            var trainings = db.ShiftRosters
                .Where(s => s.InstructorName == instructor.FullName)
                .OrderByDescending(s => s.GeneratedDate)
                .ToList();

            return View(trainings);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) db.Dispose();
            base.Dispose(disposing);
        }
    }
}