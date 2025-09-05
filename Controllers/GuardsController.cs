using Microsoft.AspNet.Identity;
using PiranaSecuritySystem.Models;
using PiranaSecuritySystem.ViewModels;
using System;
using System.Linq;
using System.Web.Mvc;
using System.Collections.Generic;

namespace PiranaSecuritySystem.Controllers
{
    [Authorize(Roles = "Admin")] // Changed to Admin for guard management
    public class GuardController : Controller
    {
        private ApplicationDbContext db = new ApplicationDbContext();

        // GET: Guard/ManageGuards
        public ActionResult ManageGuards()
        {
            try
            {
                var guards = db.Guards.ToList();
                return View(guards);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error in ManageGuards: {ex.Message}");
                ViewBag.ErrorMessage = "An error occurred while loading guards.";
                return View("Error");
            }
        }

        // GET: Guard/EditGuard/5
        public ActionResult EditGuard(int id)
        {
            try
            {
                var guard = db.Guards.Find(id);
                if (guard == null)
                {
                    ViewBag.ErrorMessage = "Guard not found.";
                    return View("Error");
                }

                return View(guard);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error in EditGuard: {ex.Message}");
                ViewBag.ErrorMessage = "An error occurred while loading guard details.";
                return View("Error");
            }
        }

        // POST: Guard/EditGuard/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult EditGuard(Guard model)
        {
            try
            {
                if (ModelState.IsValid)
                {
                    var guard = db.Guards.Find(model.GuardId);
                    if (guard == null)
                    {
                        ViewBag.ErrorMessage = "Guard not found.";
                        return View("Error");
                    }

                    // Update guard properties
                    guard.Guard_FName = model.Guard_FName;
                    guard.Guard_LName = model.Guard_LName;
                    guard.Email = model.Email;
                    guard.PhoneNumber = model.PhoneNumber;
                    guard.IsActive = model.IsActive;

                    db.SaveChanges();
                    return RedirectToAction("ManageGuards");
                }

                return View(model);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error in EditGuard POST: {ex.Message}");
                ViewBag.ErrorMessage = "An error occurred while updating guard.";
                return View("Error");
            }
        }

        // GET: Guard/DeleteGuard/5
        public ActionResult DeleteGuard(int id)
        {
            try
            {
                var guard = db.Guards.Find(id);
                if (guard == null)
                {
                    ViewBag.ErrorMessage = "Guard not found.";
                    return View("Error");
                }

                return View(guard);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error in DeleteGuard: {ex.Message}");
                ViewBag.ErrorMessage = "An error occurred while loading guard details.";
                return View("Error");
            }
        }

        // POST: Guard/DeleteGuard/5
        [HttpPost, ActionName("DeleteGuard")]
        [ValidateAntiForgeryToken]
        public ActionResult DeleteGuardConfirmed(int id)
        {
            try
            {
                var guard = db.Guards.Find(id);
                if (guard == null)
                {
                    ViewBag.ErrorMessage = "Guard not found.";
                    return View("Error");
                }

                db.Guards.Remove(guard);
                db.SaveChanges();
                return RedirectToAction("ManageGuards");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error in DeleteGuardConfirmed: {ex.Message}");
                ViewBag.ErrorMessage = "An error occurred while deleting guard.";
                return View("Error");
            }
        }

        // Other existing methods remain the same...
        // GET: Guard/Dashboard
        public ActionResult Dashboard()
        {
            try
            {
                var currentUserId = User.Identity.GetUserId();

                if (string.IsNullOrEmpty(currentUserId))
                {
                    ViewBag.ErrorMessage = "User not authenticated. Please log in again.";
                    return View("Error");
                }

                var guard = db.Guards.FirstOrDefault(g => g.UserId == currentUserId);

                if (guard == null)
                {
                    ViewBag.ErrorMessage = "Guard profile not found. Please contact administrator.";
                    return View("ProfileNotFound");
                }

                // Add dashboard statistics
                ViewBag.TotalIncidents = db.IncidentReports.Count(r => r.ResidentId == guard.GuardId);
                ViewBag.PendingIncidents = db.IncidentReports.Count(r => r.ResidentId == guard.GuardId && r.Status == "Pending Review");
                ViewBag.ResolvedIncidents = db.IncidentReports.Count(r => r.ResidentId == guard.GuardId && r.Status == "Resolved");
                ViewBag.UpcomingShifts = db.Shifts.Count(s => s.GuardId == guard.GuardId && s.StartDate >= DateTime.Today);

                // Get recent incidents for dashboard
                var recentIncidents = db.IncidentReports
                    .Where(r => r.ResidentId == guard.GuardId)
                    .OrderByDescending(r => r.ReportDate)
                    .Take(5)
                    .ToList();

                ViewBag.RecentIncidents = recentIncidents;

                return View(guard);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error in Dashboard: {ex.Message}");
                ViewBag.ErrorMessage = "An error occurred while loading the dashboard.";
                return View("Error");
            }
        }

        // Other existing methods...

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