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
                    .GroupBy(s => s.GuardId)
                    .Select(g => new
                    {
                        GuardID = g.Key,
                        GuardName = g.FirstOrDefault().Guard.Guard_FName + " " + g.FirstOrDefault().Guard.Guard_LName,
                        Shifts = g.OrderBy(s => s.ShiftDate).ToList()
                    }).ToList();

                ViewBag.GroupedShifts = groupedShifts;

                return View(db.Shifts.ToList());
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
                var guards = db.Guards.Select(g => new {
                    g.GuardId,
                    FullName = g.Guard_FName + " " + g.Guard_LName
                }).ToList();

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
        public ActionResult Create(List<int> SelectedGuardIDs, DateTime startDate, DateTime endDate)
        {
            try
            {
                // Validate input
                if (SelectedGuardIDs == null || !SelectedGuardIDs.Any())
                {
                    ModelState.AddModelError("SelectedGuardIDs", "Please select at least one guard.");
                    RepopulateViewBag();
                    return View();
                }

                if (startDate < DateTime.Today)
                {
                    ModelState.AddModelError("startDate", "Cannot create shifts for past dates.");
                    RepopulateViewBag();
                    return View();
                }

                if (endDate < startDate)
                {
                    ModelState.AddModelError("endDate", "End date must be after start date.");
                    RepopulateViewBag();
                    return View();
                }

                // Check for existing shifts
                if (HasExistingShifts(SelectedGuardIDs, startDate, endDate))
                {
                    ModelState.AddModelError("", "Shifts already exist for the selected guards in this date range. Please delete existing shifts first.");
                    RepopulateViewBag();
                    return View();
                }

                // Generate shifts
                GenerateShifts(SelectedGuardIDs, startDate, endDate);

                db.SaveChanges();
                TempData["SuccessMessage"] = $"Shifts from {startDate.ToShortDateString()} to {endDate.ToShortDateString()} created successfully!";
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
                if (innerEx.Message.Contains("datetime2") && innerEx.Message.Contains("datetime"))
                {
                    ModelState.AddModelError("", "Database schema issue detected. Please run the database update by clicking the button below.");
                    ViewBag.NeedsDatabaseUpdate = true;
                }
                else
                {
                    ModelState.AddModelError("", "Database error: " + innerEx.Message);
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
            return db.Shifts.Any(s => guardIds.Contains(s.GuardId) &&
                                     s.ShiftDate >= startDate &&
                                     s.ShiftDate <= endDate);
        }

        private void GenerateShifts(List<int> guardIds, DateTime startDate, DateTime endDate)
        {
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
                var guards = db.Guards.Select(g => new {
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
                db.Dispose();
            }
            base.Dispose(disposing);
        }

        public ActionResult DeleteAllShifts()
        {
            try
            {
                var allShifts = db.Shifts.ToList();
                db.Shifts.RemoveRange(allShifts);
                db.SaveChanges();
                TempData["Message"] = "All shifts have been deleted.";
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = "Error deleting shifts: " + GetExceptionMessage(ex);
            }
            return RedirectToAction("Index");
        }
    }
}