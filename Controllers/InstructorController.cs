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
                    return Json(new { success = false, message = "Site is required" });
                }

                var guards = db.Guards
                    .Where(g => g.Site == site && g.IsActive)
                    .Select(g => new {
                        id = g.GuardId,
                        name = g.Guard_FName + " " + g.Guard_LName,
                        badge = g.PSIRAnumber ?? g.GuardId.ToString()
                    })
                    .ToList();

                return Json(new { success = true, guards = guards });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Error loading guards: " + ex.Message });
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

                // Use GuardIdList property instead of SelectedGuardIds
                if (model.GuardIdList == null || model.GuardIdList.Count != 12)
                {
                    ModelState.AddModelError("", "Please select exactly 12 guards.");
                    return View("CreateRoster", model);
                }

                // Get selected guards
                var selectedGuards = db.Guards
                    .Where(g => model.GuardIdList.Contains(g.GuardId))
                    .ToList();

                // Create shift assignments
                CreateShiftAssignments(selectedGuards, model.Site, model.RosterDate, instructor.FullName);

                TempData["SuccessMessage"] = $"Roster for {model.Site} on {model.RosterDate:yyyy-MM-dd} created successfully!";

                // Redirect to the main ShiftRoster index to view the result
                return RedirectToAction("Index", "ShiftRoster");
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

        private void CreateShiftAssignments(List<Guard> guards, string site, DateTime rosterDate, string instructorName)
        {
            var shiftAssignments = new List<ShiftRoster>();

            // Assign first 4 guards to Day shift
            for (int i = 0; i < 4; i++)
            {
                shiftAssignments.Add(new ShiftRoster
                {
                    RosterDate = rosterDate,
                    Site = site,
                    GuardId = guards[i].GuardId,
                    ShiftType = "Day",
                    InstructorName = instructorName,
                    GeneratedDate = DateTime.Now,
                    CreatedDate = DateTime.Now,
                    Status = "Active"
                });
            }

            // Assign next 4 guards to Night shift
            for (int i = 4; i < 8; i++)
            {
                shiftAssignments.Add(new ShiftRoster
                {
                    RosterDate = rosterDate,
                    Site = site,
                    GuardId = guards[i].GuardId,
                    ShiftType = "Night",
                    InstructorName = instructorName,
                    GeneratedDate = DateTime.Now,
                    CreatedDate = DateTime.Now,
                    Status = "Active"
                });
            }

            // Assign remaining 4 guards to Off duty
            for (int i = 8; i < 12; i++)
            {
                shiftAssignments.Add(new ShiftRoster
                {
                    RosterDate = rosterDate,
                    Site = site,
                    GuardId = guards[i].GuardId,
                    ShiftType = "Off",
                    InstructorName = instructorName,
                    GeneratedDate = DateTime.Now,
                    CreatedDate = DateTime.Now,
                    Status = "Active"
                });
            }

            db.ShiftRosters.AddRange(shiftAssignments);
            db.SaveChanges();
        }

        // GET: Instructor/ViewRoster
        public ActionResult ViewRoster(DateTime date, string site)
        {
            try
            {
                var shifts = db.ShiftRosters
                    .Include(s => s.Guard)
                    .Where(s => s.RosterDate == date && s.Site == site)
                    .ToList();

                if (!shifts.Any())
                {
                    TempData["ErrorMessage"] = "Roster not found.";
                    return RedirectToAction("Dashboard");
                }

                var viewModel = new RosterDisplayViewModel
                {
                    RosterDate = date,
                    Site = site,
                    DayShiftGuards = shifts.Where(s => s.ShiftType == "Day").Select(s => s.Guard).ToList(),
                    NightShiftGuards = shifts.Where(s => s.ShiftType == "Night").Select(s => s.Guard).ToList(),
                    OffDutyGuards = shifts.Where(s => s.ShiftType == "Off").Select(s => s.Guard).ToList()
                };

                return View(viewModel);
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = "Error loading roster: " + ex.Message;
                return RedirectToAction("Dashboard");
            }
        }

        // GET: Instructor/MyRosters
        public ActionResult MyRosters()
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

                // Get unique roster dates and sites created by this instructor
                var rosters = db.ShiftRosters
                    .Where(s => s.InstructorName == instructor.FullName)
                    .GroupBy(s => new { s.RosterDate, s.Site })
                    .Select(g => new RosterDisplayViewModel
                    {
                        RosterDate = g.Key.RosterDate,
                        Site = g.Key.Site,
                        DayShiftGuards = g.Where(x => x.ShiftType == "Day").Select(x => x.Guard).ToList(),
                        NightShiftGuards = g.Where(x => x.ShiftType == "Night").Select(x => x.Guard).ToList(),
                        OffDutyGuards = g.Where(x => x.ShiftType == "Off").Select(x => x.Guard).ToList()
                    })
                    .OrderByDescending(r => r.RosterDate)
                    .ThenBy(r => r.Site)
                    .ToList();

                return View(rosters);
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = "Error loading rosters: " + ex.Message;
                return RedirectToAction("Dashboard");
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) db.Dispose();
            base.Dispose(disposing);
        }
    }
}