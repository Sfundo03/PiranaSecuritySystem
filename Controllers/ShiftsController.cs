using PiranaSecuritSystem.Models;
using PiranaSecuritySystem.Models;
using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Data.Entity.Validation;
using System.Linq;
using System.Web;
using System.Web.Mvc;

namespace PiranaSecuritySystem.Controllers
{
    public class ShiftsController : Controller
    {
        private ApplicationDbContext db = new ApplicationDbContext();

        // GET: Shifts
        public ActionResult Index()
        {
            try
            {
                var groupedShifts = db.Shifts
                    .Include(s => s.Guard)
                    .Where(s => s.Guard != null) // Filter out shifts without guards
                    .GroupBy(s => s.GuardId)
                    .Select(g => new
                    {
                        GuardID = g.Key,
                        GuardName = g.FirstOrDefault() != null && g.FirstOrDefault().Guard != null
                            ? g.FirstOrDefault().Guard.Guard_FName + " " + g.FirstOrDefault().Guard.Guard_LName
                            : "Unknown Guard",
                        Shifts = g.OrderBy(s => s.ShiftDate).ToList()
                    }).ToList();

                ViewBag.GroupedShifts = groupedShifts;

                var shifts = db.Shifts.Include(s => s.Guard).ToList();
                return View(shifts);
            }
            catch (Exception ex)
            {
                ViewBag.ErrorMessage = "Error loading shifts: " + GetExceptionMessage(ex);
                return View(new List<Shift>());
            }
        }

        // GET: Shifts/Create
        public ActionResult Create()
        {
            try
            {
                var guards = db.Guards
                    .Where(g => g != null)
                    .Select(g => new {
                        g.GuardId,
                        FullName = g.Guard_FName + " " + g.Guard_LName
                    }).ToList();

                if (guards == null || !guards.Any())
                {
                    TempData["ErrorMessage"] = "No guards found in the system. Please add guards first.";
                    return RedirectToAction("Index");
                }

                ViewBag.Guards = new SelectList(guards, "GuardId", "FullName");
                ViewBag.Today = DateTime.Today.ToString("yyyy-MM-dd");
                ViewBag.MaxDate = new DateTime(9999, 12, 31).ToString("yyyy-MM-dd");
                return View();
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = "Error loading form: " + GetExceptionMessage(ex);
                return RedirectToAction("Index");
            }
        }

        // POST: Shifts/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Create(List<int> SelectedGuardIDs, DateTime startDate)
        {
            try
            {
                // Validate input
                if (SelectedGuardIDs == null || !SelectedGuardIDs.Any())
                {
                    ModelState.AddModelError("", "Please select at least one guard.");
                    RepopulateViewBag();
                    return View();
                }

                if (startDate == DateTime.MinValue || startDate == DateTime.MaxValue)
                {
                    ModelState.AddModelError("", "Please select a valid start date.");
                    RepopulateViewBag();
                    return View();
                }

                var shiftPattern = new List<string> { "Night", "Night", "Day", "Day", "Off", "Off" };

                foreach (var guardId in SelectedGuardIDs)
                {
                    // Verify guard exists
                    var guardExists = db.Guards.Any(g => g.GuardId == guardId);
                    if (!guardExists)
                    {
                        ModelState.AddModelError("", $"Guard with ID {guardId} does not exist.");
                        RepopulateViewBag();
                        return View();
                    }

                    DateTime currentDate = startDate;

                    for (int i = 0; i < 30; i++) // Generate for 30 days
                    {
                        string shiftType = shiftPattern[i % shiftPattern.Count];

                        var shift = new Shift
                        {
                            GuardId = guardId,
                            ShiftDate = currentDate,
                            ShiftType = shiftType
                        };

                        db.Shifts.Add(shift);
                        currentDate = currentDate.AddDays(1);
                    }
                }

                db.SaveChanges();
                TempData["SuccessMessage"] = $"Shifts from {startDate.ToShortDateString()} created successfully!";
                return RedirectToAction("Index");
            }
            catch (DbEntityValidationException ex)
            {
                var errorMessages = ex.EntityValidationErrors
                    .SelectMany(x => x.ValidationErrors)
                    .Select(x => x.ErrorMessage);

                var fullErrorMessage = string.Join("; ", errorMessages);
                ModelState.AddModelError("", "Validation errors: " + fullErrorMessage);
                RepopulateViewBag();
                return View();
            }
            catch (System.Data.Entity.Infrastructure.DbUpdateException ex)
            {
                // Check if this is a datetime conversion error
                var innerEx = GetInnerException(ex);
                if (innerEx != null && innerEx.Message.Contains("datetime2") && innerEx.Message.Contains("datetime"))
                {
                    ModelState.AddModelError("", "Database schema issue detected. Please run the database update by clicking the button below.");
                    ViewBag.NeedsDatabaseUpdate = true;
                }
                else
                {
                    ModelState.AddModelError("", "Database error: " + (innerEx != null ? innerEx.Message : ex.Message));
                }
                RepopulateViewBag();
                return View();
            }
            catch (Exception ex)
            {
                ModelState.AddModelError("", "An error occurred: " + GetExceptionMessage(ex));
                RepopulateViewBag();
                return View();
            }
        }

        // POST: Update database schema
        [HttpPost]
        public ActionResult UpdateDatabase()
        {
            try
            {
                // Execute SQL to alter the column type
                db.Database.ExecuteSqlCommand("ALTER TABLE Shifts ALTER COLUMN ShiftDate datetime2");
                TempData["SuccessMessage"] = "Database schema updated successfully!";
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = "Error updating database: " + GetExceptionMessage(ex);
            }
            return RedirectToAction("Create");
        }

        private bool HasExistingShifts(List<int> guardIds, DateTime startDate, DateTime endDate)
        {
            if (guardIds == null || !guardIds.Any())
                return false;

            return db.Shifts.Any(s => guardIds.Contains(s.GuardId) &&
                                     s.ShiftDate >= startDate &&
                                     s.ShiftDate <= endDate);
        }

        private void GenerateShifts(List<int> guardIds, DateTime startDate, DateTime endDate)
        {
            if (guardIds == null || !guardIds.Any())
                throw new Exception("No guard IDs provided.");

            var shiftPattern = new List<string> { "Night", "Night", "Day", "Day", "Off", "Off" };
            int totalDays = (endDate - startDate).Days + 1;

            foreach (var guardId in guardIds)
            {
                if (!db.Guards.Any(g => g.GuardId == guardId))
                {
                    throw new Exception($"Guard with ID {guardId} does not exist.");
                }

                for (int i = 0; i < totalDays; i++)
                {
                    DateTime currentDate = startDate.AddDays(i);
                    string shiftType = shiftPattern[i % shiftPattern.Count];

                    var shift = new Shift
                    {
                        GuardId = guardId,
                        ShiftDate = currentDate,
                        ShiftType = shiftType
                    };

                    db.Shifts.Add(shift);
                }
            }
        }

        private void RepopulateViewBag()
        {
            try
            {
                var guards = db.Guards
                    .Where(g => g != null)
                    .Select(g => new {
                        g.GuardId,
                        FullName = g.Guard_FName + " " + g.Guard_LName
                    }).ToList();

                ViewBag.Guards = new SelectList(guards, "GuardId", "FullName");
                ViewBag.Today = DateTime.Today.ToString("yyyy-MM-dd");
                ViewBag.MaxDate = new DateTime(9999, 12, 31).ToString("yyyy-MM-dd");
            }
            catch (Exception ex)
            {
                ViewBag.Guards = null;
                ModelState.AddModelError("", "Error loading guards: " + GetExceptionMessage(ex));
            }
        }

        private string GetExceptionMessage(Exception ex)
        {
            if (ex == null) return "Unknown error";

            Exception inner = ex;
            while (inner.InnerException != null)
            {
                inner = inner.InnerException;
            }

            return inner.Message;
        }

        private Exception GetInnerException(Exception ex)
        {
            if (ex == null) return null;

            Exception inner = ex;
            while (inner.InnerException != null)
            {
                inner = inner.InnerException;
            }

            return inner;
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                if (db != null)
                {
                    db.Dispose();
                }
            }
            base.Dispose(disposing);
        }

        public ActionResult DeleteAllShifts()
        {
            try
            {
                var allShifts = db.Shifts.ToList();
                if (allShifts != null && allShifts.Any())
                {
                    db.Shifts.RemoveRange(allShifts);
                    db.SaveChanges();
                    TempData["Message"] = "All shifts have been deleted.";
                }
                else
                {
                    TempData["Message"] = "No shifts found to delete.";
                }
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = "Error deleting shifts: " + GetExceptionMessage(ex);
            }
            return RedirectToAction("Index");
        }
    }
}