using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using System.Web.Mvc;
using PiranaSecuritySystem.Models;

namespace PiranaSecuritySystem.Controllers
{
    public class ShiftRosterController : Controller
    {
        private ApplicationDbContext db = new ApplicationDbContext();

        // GET: ShiftRoster
        public ActionResult Index()
        {
            var rosters = db.ShiftRosters
                .Include(r => r.Guard)
                .OrderByDescending(r => r.RosterDate)
                .ThenBy(r => r.ShiftType)
                .ToList();

            var groupedRosters = rosters.GroupBy(r => new { r.RosterDate, r.Site })
                .Select(g => new RosterDisplayViewModel
                {
                    RosterDate = g.Key.RosterDate,
                    Site = g.Key.Site,
                    DayShiftGuards = g.Where(x => x.ShiftType == "Day").Select(x => x.Guard).ToList(),
                    NightShiftGuards = g.Where(x => x.ShiftType == "Night").Select(x => x.Guard).ToList(),
                    OffDutyGuards = g.Where(x => x.ShiftType == "Off").Select(x => x.Guard).ToList()
                }).ToList();

            return View(groupedRosters);
        }

        // GET: ShiftRoster/Create
        public ActionResult Create()
        {
            var viewModel = new RosterViewModel
            {
                RosterDate = DateTime.Today
            };

            // Get distinct sites from guards
            var sites = db.Guards
                .Where(g => g.IsActive)
                .Select(g => g.Site)
                .Distinct()
                .ToList();

            ViewBag.Sites = new SelectList(sites);
            return View(viewModel);
        }

        // AJAX method to get guards by site
        [HttpPost]
        public JsonResult GetGuardsBySite(string site)
        {
            try
            {
                var guards = db.Guards
                    .Where(g => g.IsActive && g.Site == site)
                    .Select(g => new
                    {
                        id = g.GuardId,
                        name = g.Guard_FName + " " + g.Guard_LName,
                        badge = g.PSIRAnumber
                    })
                    .ToList();

                return Json(new { success = true, guards = guards });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Error loading guards: " + ex.Message });
            }
        }

        // POST: ShiftRoster/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Create(RosterViewModel viewModel)
        {
            if (ModelState.IsValid)
            {
                // Use GuardIdList instead of SelectedGuardIds
                if (viewModel.GuardIdList == null || viewModel.GuardIdList.Count != 12)
                {
                    ModelState.AddModelError("", "Please select exactly 12 guards.");
                    PopulateSiteDropdown();
                    return View(viewModel);
                }

                // Check if roster already exists for this date and site
                var existingRoster = db.ShiftRosters.Any(r => r.RosterDate == viewModel.RosterDate && r.Site == viewModel.Site);
                if (existingRoster)
                {
                    ModelState.AddModelError("", $"A roster already exists for {viewModel.RosterDate:yyyy-MM-dd} at {viewModel.Site}.");
                    PopulateSiteDropdown();
                    return View(viewModel);
                }

                // Get selected guards using GuardIdList
                var selectedGuards = db.Guards
                    .Where(g => viewModel.GuardIdList.Contains(g.GuardId))
                    .ToList();

                // Validate all guards belong to the selected site
                var invalidGuards = selectedGuards.Where(g => g.Site != viewModel.Site).ToList();
                if (invalidGuards.Any())
                {
                    ModelState.AddModelError("", $"Some selected guards don't belong to site {viewModel.Site}.");
                    PopulateSiteDropdown();
                    return View(viewModel);
                }

                // Auto-generate shift assignments
                AutoGenerateShifts(selectedGuards, viewModel);

                // Ensure SelectedGuardIds is properly set for the preview
                viewModel.SelectedGuardIds = string.Join(",", viewModel.GuardIdList);

                // Show preview
                return View("RosterPreview", viewModel);
            }

            PopulateSiteDropdown();
            return View(viewModel);
        }

        // POST: ShiftRoster/ConfirmCreate
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult ConfirmCreate(RosterViewModel viewModel)
        {
            try
            {
                // Parse the guard IDs from the hidden field
                var guardIds = new List<int>();
                if (!string.IsNullOrEmpty(viewModel.SelectedGuardIds))
                {
                    guardIds = viewModel.SelectedGuardIds.Split(',')
                        .Where(id => !string.IsNullOrEmpty(id))
                        .Select(int.Parse)
                        .ToList();
                }

                if (guardIds.Count != 12)
                {
                    TempData["ErrorMessage"] = "Invalid guard selection. Please select exactly 12 guards.";
                    return RedirectToAction("Create");
                }

                // Get selected guards
                var selectedGuards = db.Guards
                    .Where(g => guardIds.Contains(g.GuardId))
                    .ToList();

                // Regenerate shifts
                AutoGenerateShifts(selectedGuards, viewModel);

                // Save to database
                SaveRosterToDatabase(viewModel);

                TempData["SuccessMessage"] = $"Roster for {viewModel.RosterDate:yyyy-MM-dd} at {viewModel.Site} created successfully!";
                return RedirectToAction("Index");
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = "Error creating roster: " + ex.Message;
                return RedirectToAction("Create");
            }
        }

        // Auto-generate shift assignments
        private void AutoGenerateShifts(List<Guard> selectedGuards, RosterViewModel viewModel)
        {
            // Get previous roster data for fair rotation
            var previousRosters = db.ShiftRosters
                .Where(r => r.RosterDate < viewModel.RosterDate && selectedGuards.Select(g => g.GuardId).Contains(r.GuardId))
                .OrderByDescending(r => r.RosterDate)
                .ToList();

            // Create guard assignments with shift history
            var guardAssignments = selectedGuards.Select(guard => new
            {
                Guard = guard,
                LastNightShift = previousRosters
                    .Where(r => r.GuardId == guard.GuardId && r.ShiftType == "Night")
                    .OrderByDescending(r => r.RosterDate)
                    .FirstOrDefault()?.RosterDate ?? DateTime.MinValue,
                LastDayShift = previousRosters
                    .Where(r => r.GuardId == guard.GuardId && r.ShiftType == "Day")
                    .OrderByDescending(r => r.RosterDate)
                    .FirstOrDefault()?.RosterDate ?? DateTime.MinValue,
                NightShiftCount = previousRosters.Count(r => r.GuardId == guard.GuardId && r.ShiftType == "Night"),
                DayShiftCount = previousRosters.Count(r => r.GuardId == guard.GuardId && r.ShiftType == "Day")
            }).ToList();

            // Sort by least recent night shift, then by night shift count
            var nightShiftCandidates = guardAssignments
                .OrderBy(g => g.LastNightShift)
                .ThenBy(g => g.NightShiftCount)
                .Take(4)
                .ToList();

            // Remove night shift candidates and get day shift candidates
            var remainingForDay = guardAssignments.Except(nightShiftCandidates)
                .OrderBy(g => g.LastDayShift)
                .ThenBy(g => g.DayShiftCount)
                .Take(4)
                .ToList();

            // Remaining are off duty
            var offDuty = guardAssignments.Except(nightShiftCandidates).Except(remainingForDay).ToList();

            // Assign to viewModel
            viewModel.DayShiftGuards = remainingForDay.Select(g => g.Guard).ToList();
            viewModel.NightShiftGuards = nightShiftCandidates.Select(g => g.Guard).ToList();
            viewModel.OffDutyGuards = offDuty.Select(g => g.Guard).ToList();
        }

        // Save roster to database
        private void SaveRosterToDatabase(RosterViewModel viewModel)
        {
            var rosterEntries = new List<ShiftRoster>();

            // Save day shift guards
            foreach (var guard in viewModel.DayShiftGuards)
            {
                rosterEntries.Add(new ShiftRoster
                {
                    RosterDate = viewModel.RosterDate,
                    ShiftType = "Day",
                    GuardId = guard.GuardId,
                    Site = viewModel.Site,
                    CreatedDate = DateTime.Now
                });
            }

            // Save night shift guards
            foreach (var guard in viewModel.NightShiftGuards)
            {
                rosterEntries.Add(new ShiftRoster
                {
                    RosterDate = viewModel.RosterDate,
                    ShiftType = "Night",
                    GuardId = guard.GuardId,
                    Site = viewModel.Site,
                    CreatedDate = DateTime.Now
                });
            }

            // Save off duty guards
            foreach (var guard in viewModel.OffDutyGuards)
            {
                rosterEntries.Add(new ShiftRoster
                {
                    RosterDate = viewModel.RosterDate,
                    ShiftType = "Off",
                    GuardId = guard.GuardId,
                    Site = viewModel.Site,
                    CreatedDate = DateTime.Now
                });
            }

            db.ShiftRosters.AddRange(rosterEntries);
            db.SaveChanges();
        }

        // GET: ShiftRoster/Edit/5
        public ActionResult Edit(DateTime date, string site)
        {
            var rosterItems = db.ShiftRosters
                .Include(r => r.Guard)
                .Where(r => r.RosterDate == date && r.Site == site)
                .ToList();

            if (!rosterItems.Any())
            {
                return HttpNotFound();
            }

            var viewModel = new RosterViewModel
            {
                RosterDate = date,
                Site = site,
                GuardIdList = rosterItems.Select(r => r.GuardId).ToList()
            };

            // Set the SelectedGuardIds from the GuardIdList
            viewModel.SelectedGuardIds = string.Join(",", viewModel.GuardIdList);

            viewModel.DayShiftGuards = rosterItems.Where(r => r.ShiftType == "Day").Select(r => r.Guard).ToList();
            viewModel.NightShiftGuards = rosterItems.Where(r => r.ShiftType == "Night").Select(r => r.Guard).ToList();
            viewModel.OffDutyGuards = rosterItems.Where(r => r.ShiftType == "Off").Select(r => r.Guard).ToList();

            PopulateSiteDropdown();
            return View(viewModel);
        }

        // POST: ShiftRoster/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Edit(RosterViewModel viewModel)
        {
            if (ModelState.IsValid)
            {
                // Validate exactly 12 guards selected
                if (viewModel.GuardIdList == null || viewModel.GuardIdList.Count != 12)
                {
                    ModelState.AddModelError("", "Please select exactly 12 guards.");
                    PopulateSiteDropdown();
                    return View(viewModel);
                }

                // Remove existing roster items for this date and site
                var existingRosters = db.ShiftRosters.Where(r => r.RosterDate == viewModel.RosterDate && r.Site == viewModel.Site);
                db.ShiftRosters.RemoveRange(existingRosters);

                // Get selected guards
                var selectedGuards = db.Guards
                    .Where(g => viewModel.GuardIdList.Contains(g.GuardId))
                    .ToList();

                // Auto-generate new shift assignments
                AutoGenerateShifts(selectedGuards, viewModel);

                // Save new roster items
                SaveRosterToDatabase(viewModel);

                TempData["SuccessMessage"] = $"Roster for {viewModel.RosterDate:yyyy-MM-dd} at {viewModel.Site} updated successfully!";
                return RedirectToAction("Index");
            }

            PopulateSiteDropdown();
            return View(viewModel);
        }

        // GET: ShiftRoster/Delete/5
        public ActionResult Delete(DateTime date, string site)
        {
            var rosterItems = db.ShiftRosters
                .Include(r => r.Guard)
                .Where(r => r.RosterDate == date && r.Site == site)
                .ToList();

            if (!rosterItems.Any())
            {
                return HttpNotFound();
            }

            var viewModel = new RosterDisplayViewModel
            {
                RosterDate = date,
                Site = site,
                DayShiftGuards = rosterItems.Where(r => r.ShiftType == "Day").Select(r => r.Guard).ToList(),
                NightShiftGuards = rosterItems.Where(r => r.ShiftType == "Night").Select(r => r.Guard).ToList(),
                OffDutyGuards = rosterItems.Where(r => r.ShiftType == "Off").Select(r => r.Guard).ToList()
            };

            return View(viewModel);
        }

        // POST: ShiftRoster/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public ActionResult DeleteConfirmed(DateTime date, string site)
        {
            var rosterItems = db.ShiftRosters.Where(r => r.RosterDate == date && r.Site == site);
            db.ShiftRosters.RemoveRange(rosterItems);
            db.SaveChanges();

            TempData["SuccessMessage"] = $"Roster for {date:yyyy-MM-dd} at {site} deleted successfully!";
            return RedirectToAction("Index");
        }

        // Helper method to populate site dropdown
        private void PopulateSiteDropdown()
        {
            var sites = db.Guards
                .Where(g => g.IsActive)
                .Select(g => g.Site)
                .Distinct()
                .ToList();

            ViewBag.Sites = new SelectList(sites);
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