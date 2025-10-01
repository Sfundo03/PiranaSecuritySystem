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

            PopulateSiteDropdown();
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

                // Check if rosters already exist for the date range
                if (viewModel.GenerateFor30Days)
                {
                    var endDate = viewModel.RosterDate.AddDays(29);
                    var existingRosters = db.ShiftRosters
                        .Any(r => r.Site == viewModel.Site &&
                                 r.RosterDate >= viewModel.RosterDate &&
                                 r.RosterDate <= endDate);

                    if (existingRosters)
                    {
                        ModelState.AddModelError("", $"Rosters already exist for some dates between {viewModel.RosterDate:yyyy-MM-dd} and {endDate:yyyy-MM-dd} at {viewModel.Site}.");
                        PopulateSiteDropdown();
                        return View(viewModel);
                    }
                }
                else
                {
                    // Single date check
                    var existingRoster = db.ShiftRosters.Any(r => r.RosterDate == viewModel.RosterDate && r.Site == viewModel.Site);
                    if (existingRoster)
                    {
                        ModelState.AddModelError("", $"A roster already exists for {viewModel.RosterDate:yyyy-MM-dd} at {viewModel.Site}.");
                        PopulateSiteDropdown();
                        return View(viewModel);
                    }
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

                // Auto-generate shift assignments for preview (single day)
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

                if (viewModel.GenerateFor30Days)
                {
                    // Generate roster for 30 days with 2-2-2 fixed rotation
                    Generate30DayRosterWithFixedRotation(selectedGuards, viewModel);
                    TempData["SuccessMessage"] = $"Rosters for 30 days starting from {viewModel.RosterDate:yyyy-MM-dd} at {viewModel.Site} created successfully!";
                }
                else
                {
                    // Single day generation
                    AutoGenerateShifts(selectedGuards, viewModel);
                    SaveRosterToDatabase(viewModel);
                    TempData["SuccessMessage"] = $"Roster for {viewModel.RosterDate:yyyy-MM-dd} at {viewModel.Site} created successfully!";
                }

                return RedirectToAction("Index");
            }
            catch (System.Data.Entity.Validation.DbEntityValidationException ex)
            {
                // Handle validation errors
                var errorMessages = ex.EntityValidationErrors
                    .SelectMany(x => x.ValidationErrors)
                    .Select(x => x.ErrorMessage);

                var fullErrorMessage = string.Join("; ", errorMessages);
                TempData["ErrorMessage"] = "Validation error: " + fullErrorMessage;
                return RedirectToAction("Create");
            }
            catch (System.Data.Entity.Infrastructure.DbUpdateException ex)
            {
                // Handle update errors (constraint violations, etc.)
                var innerException = GetInnerException(ex);
                TempData["ErrorMessage"] = "Database update error: " + innerException.Message;
                return RedirectToAction("Create");
            }
            catch (Exception ex)
            {
                // Handle all other errors
                var innerException = GetInnerException(ex);
                TempData["ErrorMessage"] = "Error creating roster: " + innerException.Message;
                return RedirectToAction("Create");
            }
        }

        // UPDATED: Generate 30-day roster with fixed 2-2-2 rotation
        private void Generate30DayRosterWithFixedRotation(List<Guard> selectedGuards, RosterViewModel viewModel)
        {
            var allRosters = new List<ShiftRoster>();

            // Divide guards into 3 groups of 4 for the rotation
            var group1 = selectedGuards.Take(4).ToList();
            var group2 = selectedGuards.Skip(4).Take(4).ToList();
            var group3 = selectedGuards.Skip(8).Take(4).ToList();

            // Generate roster for each day
            for (int day = 0; day < 30; day++)
            {
                var currentDate = viewModel.RosterDate.AddDays(day);

                // Check if roster already exists for this specific date
                var existingRoster = db.ShiftRosters.Any(r => r.RosterDate == currentDate && r.Site == viewModel.Site);
                if (existingRoster)
                {
                    // Skip this date if roster already exists
                    continue;
                }

                // Calculate rotation position based on days from start date - 6-day cycle (2 Day + 2 Night + 2 Off)
                int cycleDay = day % 6;

                // Determine which group has which shift based on the cycle day
                List<Guard> dayShiftGuards, nightShiftGuards, offDutyGuards;

                if (cycleDay < 2)
                {
                    // Days 0-1: Group1=Day, Group2=Night, Group3=Off
                    dayShiftGuards = group1;
                    nightShiftGuards = group2;
                    offDutyGuards = group3;
                }
                else if (cycleDay < 4)
                {
                    // Days 2-3: Group1=Night, Group2=Off, Group3=Day
                    dayShiftGuards = group3;
                    nightShiftGuards = group1;
                    offDutyGuards = group2;
                }
                else
                {
                    // Days 4-5: Group1=Off, Group2=Day, Group3=Night
                    dayShiftGuards = group2;
                    nightShiftGuards = group3;
                    offDutyGuards = group1;
                }

                // Create roster entries
                allRosters.AddRange(CreateRosterEntries(dayShiftGuards, "Day", currentDate, viewModel.Site));
                allRosters.AddRange(CreateRosterEntries(nightShiftGuards, "Night", currentDate, viewModel.Site));
                allRosters.AddRange(CreateRosterEntries(offDutyGuards, "Off", currentDate, viewModel.Site));
            }

            // Save all rosters to database
            db.ShiftRosters.AddRange(allRosters);
            db.SaveChanges();
        }

        // Create roster entries for guards
        private List<ShiftRoster> CreateRosterEntries(List<Guard> guards, string shiftType, DateTime date, string site)
        {
            return guards.Select(guard => new ShiftRoster
            {
                RosterDate = date,
                ShiftType = shiftType,
                GuardId = guard.GuardId,
                Site = site,
                CreatedDate = DateTime.Now
            }).ToList();
        }

        // Helper method to get the deepest inner exception
        private Exception GetInnerException(Exception ex)
        {
            while (ex.InnerException != null)
            {
                ex = ex.InnerException;
            }
            return ex;
        }

        // UPDATED: Auto-generate shift assignments with 2-2-2 fixed rotation
        private void AutoGenerateShifts(List<Guard> selectedGuards, RosterViewModel viewModel)
        {
            // For single day creation, we need to determine the rotation based on existing history
            // or start a new rotation if no history exists

            // Extract guard IDs for the query
            var guardIds = selectedGuards.Select(g => g.GuardId).ToList();

            // Get the most recent roster for this site to continue the rotation
            var latestRoster = db.ShiftRosters
                .Where(r => r.Site == viewModel.Site && r.RosterDate < viewModel.RosterDate)
                .OrderByDescending(r => r.RosterDate)
                .FirstOrDefault();

            List<Guard> dayShiftGuards, nightShiftGuards, offDutyGuards;

            if (latestRoster != null)
            {
                // Continue rotation from previous day
                var previousDate = latestRoster.RosterDate;

                // Get previous day's assignments
                var previousRosters = db.ShiftRosters
                    .Include(r => r.Guard)
                    .Where(r => r.RosterDate == previousDate && r.Site == viewModel.Site)
                    .ToList();

                var prevDayShift = previousRosters.Where(r => r.ShiftType == "Day").Select(r => r.Guard).ToList();
                var prevNightShift = previousRosters.Where(r => r.ShiftType == "Night").Select(r => r.Guard).ToList();
                var prevOffDuty = previousRosters.Where(r => r.ShiftType == "Off").Select(r => r.Guard).ToList();

                // Calculate days since rotation start - now using 6-day cycle
                var rotationStartDate = GetRotationStartDate(viewModel.Site, previousDate);
                var daysInRotation = (previousDate - rotationStartDate).Days;
                var nextCycleDay = (daysInRotation + 1) % 6;

                // Determine next shift assignment based on 2-2-2 rotation
                if (nextCycleDay < 2)
                {
                    // Continue current pattern for first 2 days
                    dayShiftGuards = prevDayShift;
                    nightShiftGuards = prevNightShift;
                    offDutyGuards = prevOffDuty;
                }
                else if (nextCycleDay == 2)
                {
                    // Rotate: Day->Night, Night->Off, Off->Day
                    dayShiftGuards = prevOffDuty;
                    nightShiftGuards = prevDayShift;
                    offDutyGuards = prevNightShift;
                }
                else if (nextCycleDay < 4)
                {
                    // Continue current pattern for next 2 days
                    dayShiftGuards = prevOffDuty;
                    nightShiftGuards = prevDayShift;
                    offDutyGuards = prevNightShift;
                }
                else if (nextCycleDay == 4)
                {
                    // Rotate again: Day->Off, Night->Day, Off->Night
                    dayShiftGuards = prevNightShift;
                    nightShiftGuards = prevOffDuty;
                    offDutyGuards = prevDayShift;
                }
                else
                {
                    // Continue current pattern for last 2 days
                    dayShiftGuards = prevNightShift;
                    nightShiftGuards = prevOffDuty;
                    offDutyGuards = prevDayShift;
                }
            }
            else
            {
                // No previous roster - start new rotation
                // Divide guards into 3 groups of 4
                var group1 = selectedGuards.Take(4).ToList();
                var group2 = selectedGuards.Skip(4).Take(4).ToList();
                var group3 = selectedGuards.Skip(8).Take(4).ToList();

                // Start with Group1=Day, Group2=Night, Group3=Off
                dayShiftGuards = group1;
                nightShiftGuards = group2;
                offDutyGuards = group3;
            }

            // Assign to viewModel
            viewModel.DayShiftGuards = dayShiftGuards;
            viewModel.NightShiftGuards = nightShiftGuards;
            viewModel.OffDutyGuards = offDutyGuards;
        }

        // Helper method to find rotation start date
        private DateTime GetRotationStartDate(string site, DateTime currentDate)
        {
            // Look back up to 30 days to find when the rotation started
            for (int i = 0; i < 30; i++)
            {
                var checkDate = currentDate.AddDays(-i);
                var roster = db.ShiftRosters
                    .Where(r => r.RosterDate == checkDate && r.Site == site)
                    .ToList();

                if (!roster.Any())
                {
                    // No roster found for this date, rotation likely started the next day
                    return checkDate.AddDays(1);
                }
            }

            // If no break found, assume rotation started 30 days ago
            return currentDate.AddDays(-30);
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

                // For editing, maintain the same groups but recalculate if guards changed
                if (viewModel.DayShiftGuards.Count == 4 && viewModel.NightShiftGuards.Count == 4 && viewModel.OffDutyGuards.Count == 4)
                {
                    // Keep the same shift assignment if valid
                    SaveRosterToDatabase(viewModel);
                }
                else
                {
                    // Auto-generate new shift assignments with 2-2-2 rotation
                    AutoGenerateShifts(selectedGuards, viewModel);
                    SaveRosterToDatabase(viewModel);
                }

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