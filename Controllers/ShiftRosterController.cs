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

            var groupedRosters = rosters.GroupBy(r => r.RosterDate)
                .Select(g => new RosterDisplayViewModel
                {
                    RosterDate = g.Key,
                    DayShiftGuards = g.Where(x => x.ShiftType == "Day").Select(x => x.Guard).ToList(),
                    NightShiftGuards = g.Where(x => x.ShiftType == "Night").Select(x => x.Guard).ToList(),
                    OffDutyGuards = g.Where(x => x.ShiftType == "Off").Select(x => x.Guard).ToList()
                }).ToList();

            return View(groupedRosters);
        }

        // GET: ShiftRoster/Create
        public ActionResult Create()
        {
            var activeGuards = db.Guards.Where(g => g.IsActive).ToList();

            var viewModel = new RosterViewModel
            {
                RosterDate = DateTime.Today
            };

            ViewBag.Guards = new MultiSelectList(activeGuards, "GuardId", "FullName");
            return View(viewModel);
        }

        // POST: ShiftRoster/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Create(RosterViewModel viewModel)
        {
            if (ModelState.IsValid)
            {
                // Validate exactly 12 guards selected
                if (viewModel.SelectedGuardIds.Count != 12)
                {
                    ModelState.AddModelError("SelectedGuardIds", "Please select exactly 12 guards.");
                    ViewBag.Guards = new MultiSelectList(db.Guards.Where(g => g.IsActive), "GuardId", "FullName");
                    return View(viewModel);
                }

                // Check if roster already exists for this date
                var existingRoster = db.ShiftRosters.Any(r => r.RosterDate == viewModel.RosterDate);
                if (existingRoster)
                {
                    ModelState.AddModelError("", "A roster already exists for this date.");
                    ViewBag.Guards = new MultiSelectList(db.Guards.Where(g => g.IsActive), "GuardId", "FullName");
                    return View(viewModel);
                }

                // Get selected guards
                var selectedGuards = db.Guards
                    .Where(g => viewModel.SelectedGuardIds.Contains(g.GuardId))
                    .ToList();

                // Auto-generate shift assignments (simple rotation algorithm)
                AutoGenerateShifts(selectedGuards, viewModel);

                // Save to database
                SaveRosterToDatabase(viewModel);

                // Show preview
                return View("RosterPreview", viewModel);
            }

            ViewBag.Guards = new MultiSelectList(db.Guards.Where(g => g.IsActive), "GuardId", "FullName");
            return View(viewModel);
        }

        // POST: ShiftRoster/ConfirmCreate
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult ConfirmCreate(RosterViewModel viewModel)
        {
            // Get selected guards again
            var selectedGuards = db.Guards
                .Where(g => viewModel.SelectedGuardIds.Contains(g.GuardId))
                .ToList();

            // Regenerate shifts (in case we need to)
            AutoGenerateShifts(selectedGuards, viewModel);

            // Save to database
            SaveRosterToDatabase(viewModel);

            return RedirectToAction("Index");
        }

        // Auto-generate shift assignments
        private void AutoGenerateShifts(List<Guard> selectedGuards, RosterViewModel viewModel)
        {
            // Simple rotation algorithm - you can customize this logic
            // For example: rotate based on previous assignments, seniority, etc.

            // Shuffle the list for random assignment (you can replace with your own logic)
            var random = new Random();
            var shuffledGuards = selectedGuards.OrderBy(x => random.Next()).ToList();

            // Assign first 4 to Day Shift
            viewModel.DayShiftGuards = shuffledGuards.Take(4).ToList();

            // Next 4 to Night Shift
            viewModel.NightShiftGuards = shuffledGuards.Skip(4).Take(4).ToList();

            // Last 4 to Off Duty
            viewModel.OffDutyGuards = shuffledGuards.Skip(8).Take(4).ToList();
        }

        // Save roster to database
        private void SaveRosterToDatabase(RosterViewModel viewModel)
        {
            // Save day shift guards
            foreach (var guard in viewModel.DayShiftGuards)
            {
                var roster = new ShiftRoster
                {
                    RosterDate = viewModel.RosterDate,
                    ShiftType = "Day",
                    GuardId = guard.GuardId,
                    CreatedDate = DateTime.Now
                };
                db.ShiftRosters.Add(roster);
            }

            // Save night shift guards
            foreach (var guard in viewModel.NightShiftGuards)
            {
                var roster = new ShiftRoster
                {
                    RosterDate = viewModel.RosterDate,
                    ShiftType = "Night",
                    GuardId = guard.GuardId,
                    CreatedDate = DateTime.Now
                };
                db.ShiftRosters.Add(roster);
            }

            // Save off duty guards
            foreach (var guard in viewModel.OffDutyGuards)
            {
                var roster = new ShiftRoster
                {
                    RosterDate = viewModel.RosterDate,
                    ShiftType = "Off",
                    GuardId = guard.GuardId,
                    CreatedDate = DateTime.Now
                };
                db.ShiftRosters.Add(roster);
            }

            db.SaveChanges();
        }

        // GET: ShiftRoster/Edit/5
        public ActionResult Edit(DateTime date)
        {
            var rosterItems = db.ShiftRosters
                .Include(r => r.Guard)
                .Where(r => r.RosterDate == date)
                .ToList();

            if (!rosterItems.Any())
            {
                return HttpNotFound();
            }

            var viewModel = new RosterViewModel
            {
                RosterDate = date,
                SelectedGuardIds = rosterItems.Select(r => r.GuardId).ToList(),
                DayShiftGuards = rosterItems.Where(r => r.ShiftType == "Day").Select(r => r.Guard).ToList(),
                NightShiftGuards = rosterItems.Where(r => r.ShiftType == "Night").Select(r => r.Guard).ToList(),
                OffDutyGuards = rosterItems.Where(r => r.ShiftType == "Off").Select(r => r.Guard).ToList()
            };

            ViewBag.Guards = new MultiSelectList(db.Guards.Where(g => g.IsActive), "GuardId", "FullName");
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
                if (viewModel.SelectedGuardIds.Count != 12)
                {
                    ModelState.AddModelError("SelectedGuardIds", "Please select exactly 12 guards.");
                    ViewBag.Guards = new MultiSelectList(db.Guards.Where(g => g.IsActive), "GuardId", "FullName");
                    return View(viewModel);
                }

                // Remove existing roster items for this date
                var existingRosters = db.ShiftRosters.Where(r => r.RosterDate == viewModel.RosterDate);
                db.ShiftRosters.RemoveRange(existingRosters);

                // Get selected guards
                var selectedGuards = db.Guards
                    .Where(g => viewModel.SelectedGuardIds.Contains(g.GuardId))
                    .ToList();

                // Auto-generate new shift assignments
                AutoGenerateShifts(selectedGuards, viewModel);

                // Save new roster items
                foreach (var guard in viewModel.DayShiftGuards)
                {
                    var roster = new ShiftRoster
                    {
                        RosterDate = viewModel.RosterDate,
                        ShiftType = "Day",
                        GuardId = guard.GuardId,
                        CreatedDate = DateTime.Now,
                        ModifiedDate = DateTime.Now
                    };
                    db.ShiftRosters.Add(roster);
                }

                foreach (var guard in viewModel.NightShiftGuards)
                {
                    var roster = new ShiftRoster
                    {
                        RosterDate = viewModel.RosterDate,
                        ShiftType = "Night",
                        GuardId = guard.GuardId,
                        CreatedDate = DateTime.Now,
                        ModifiedDate = DateTime.Now
                    };
                    db.ShiftRosters.Add(roster);
                }

                foreach (var guard in viewModel.OffDutyGuards)
                {
                    var roster = new ShiftRoster
                    {
                        RosterDate = viewModel.RosterDate,
                        ShiftType = "Off",
                        GuardId = guard.GuardId,
                        CreatedDate = DateTime.Now,
                        ModifiedDate = DateTime.Now
                    };
                    db.ShiftRosters.Add(roster);
                }

                db.SaveChanges();
                return RedirectToAction("Index");
            }

            ViewBag.Guards = new MultiSelectList(db.Guards.Where(g => g.IsActive), "GuardId", "FullName");
            return View(viewModel);
        }

        // Other methods (Delete, etc.) remain the same...
    }
}