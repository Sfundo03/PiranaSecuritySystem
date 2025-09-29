using Microsoft.AspNet.Identity;
using PiranaSecuritySystem.Models;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Data.Entity;
using System.Linq;
using System.Web.Mvc;

namespace PiranaSecuritySystem.Controllers
{
    [Authorize(Roles = "Instructor")]
    public class TrainingSessionsController : Controller
    {
        private ApplicationDbContext db = new ApplicationDbContext();

        // GET: TrainingSessions
        public ActionResult Index()
        {
            try
            {
                var sessions = db.TrainingSessions
                    .Include(t => t.Enrollments)
                    .OrderByDescending(t => t.StartDate)
                    .ToList();

                return View(sessions);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error in Index: {ex.Message}");
                TempData["ErrorMessage"] = "An error occurred while loading training sessions.";
                return View(new List<TrainingSession>());
            }
        }

        // GET: TrainingSessions/Details/5
        // In your TrainingSessionsController, update the Details method:

        // GET: TrainingSessions/Details/5
        public ActionResult Details(int? id)  // Changed to nullable int
        {
            try
            {
                if (!id.HasValue)
                {
                    TempData["ErrorMessage"] = "Training session ID is required.";
                    return RedirectToAction("Index");
                }

                var session = db.TrainingSessions
                    .Include(t => t.Enrollments.Select(e => e.Guard))
                    .FirstOrDefault(t => t.Id == id.Value);

                if (session == null)
                {
                    TempData["ErrorMessage"] = "Training session not found.";
                    return RedirectToAction("Index");
                }

                return View(session);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error in Details: {ex.Message}");
                TempData["ErrorMessage"] = "An error occurred while loading the training session details.";
                return RedirectToAction("Index");
            }
        }

        // GET: TrainingSessions/Create
        public ActionResult Create()
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

                // Get available sites from existing guards
                var sites = db.Guards
                    .Where(g => g.Site != null && g.IsActive)
                    .Select(g => g.Site)
                    .Distinct()
                    .ToList();

                if (!sites.Any())
                {
                    sites = new List<string> { "Site A", "Site B", "Site C" };
                }

                ViewBag.Sites = new SelectList(sites);

                var model = new TrainingSessionViewModel
                {
                    StartDate = DateTime.Now.AddHours(1),
                    EndDate = DateTime.Now.AddHours(2)
                };

                return View(model);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error in Create (GET): {ex.Message}");
                TempData["ErrorMessage"] = "An error occurred while loading the create page.";
                return RedirectToAction("Index");
            }
        }

        // AJAX method to get guards by site - FIXED VERSION
        [HttpPost]
        public JsonResult GetGuardsBySite(string site)
        {
            try
            {
                System.Diagnostics.Debug.WriteLine($"GetGuardsBySite called with site: {site}");

                if (string.IsNullOrEmpty(site))
                {
                    return Json(new { success = false, message = "Site is required" });
                }

                var guards = db.Guards
                    .Where(g => g.Site == site && g.IsActive)
                    .Select(g => new {
                        id = g.GuardId, // Changed from g.id to g.GuardId
                        name = g.Guard_FName + " " + g.Guard_LName,
                        badge = g.PSIRAnumber ?? g.GuardId.ToString()
                    })
                    .Take(50) // Increased limit
                    .ToList();

                System.Diagnostics.Debug.WriteLine($"Found {guards.Count} guards for site: {site}");

                return Json(new
                {
                    success = true,
                    guards = guards,
                    count = guards.Count,
                    message = $"Loaded {guards.Count} guards"
                });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error in GetGuardsBySite: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"Stack Trace: {ex.StackTrace}");
                return Json(new
                {
                    success = false,
                    message = "Error loading guards from database. Please check the server logs."
                });
            }
        }

        // POST: TrainingSessions/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Create(TrainingSessionViewModel model)
        {
            try
            {
                // Get available sites for dropdown
                var sites = db.Guards
                    .Where(g => g.Site != null && g.IsActive)
                    .Select(g => g.Site)
                    .Distinct()
                    .ToList();

                if (!sites.Any())
                {
                    sites = new List<string> { "Site A", "Site B", "Site C" };
                }

                ViewBag.Sites = new SelectList(sites);

                if (!ModelState.IsValid)
                {
                    return View(model);
                }

                // Validate selected guards
                if (model.SelectedGuardIds == null || model.SelectedGuardIds.Count == 0)
                {
                    ModelState.AddModelError("", "Please select at least one guard.");
                    return View(model);
                }

                // Validate dates
                if (model.StartDate >= model.EndDate)
                {
                    ModelState.AddModelError("EndDate", "End date must be after start date.");
                    return View(model);
                }

                // Create training session
                var trainingSession = new TrainingSession
                {
                    Title = model.Title,
                    StartDate = model.StartDate,
                    EndDate = model.EndDate,
                    Capacity = model.SelectedGuardIds.Count,
                    Site = model.Site
                };

                db.TrainingSessions.Add(trainingSession);
                db.SaveChanges();

                // Create enrollments
                foreach (var guardId in model.SelectedGuardIds)
                {
                    var enrollment = new TrainingEnrollment
                    {
                        TrainingSessionId = trainingSession.Id,
                        GuardId = guardId,
                        EnrollmentDate = DateTime.Now
                    };
                    db.TrainingEnrollments.Add(enrollment);
                }

                db.SaveChanges();

                TempData["SuccessMessage"] = $"Training session '{model.Title}' created successfully with {model.SelectedGuardIds.Count} guards!";
                return RedirectToAction("Index");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error in Create (POST): {ex.Message}");
                TempData["ErrorMessage"] = "Error creating training session: " + ex.Message;

                // Repopulate sites on error
                var sites = db.Guards
                    .Where(g => g.Site != null && g.IsActive)
                    .Select(g => g.Site)
                    .Distinct()
                    .ToList();

                if (!sites.Any())
                {
                    sites = new List<string> { "Site A", "Site B", "Site C" };
                }

                ViewBag.Sites = new SelectList(sites);
                return View(model);
            }
        }

        // GET: TrainingSessions/Edit/5
        public ActionResult Edit(int id)
        {
            try
            {
                var session = db.TrainingSessions.Find(id);
                if (session == null)
                {
                    TempData["ErrorMessage"] = "Training session not found.";
                    return RedirectToAction("Index");
                }

                var sites = db.Guards
                    .Where(g => g.Site != null && g.IsActive)
                    .Select(g => g.Site)
                    .Distinct()
                    .ToList();

                if (!sites.Any())
                {
                    sites = new List<string> { "Site A", "Site B", "Site C" };
                }

                ViewBag.Sites = new SelectList(sites, session.Site);

                var model = new TrainingSessionViewModel
                {
                    Id = session.Id,
                    Title = session.Title,
                    StartDate = session.StartDate,
                    EndDate = session.EndDate,
                    Capacity = session.Capacity,
                    Site = session.Site
                };

                return View(model);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error in Edit (GET): {ex.Message}");
                TempData["ErrorMessage"] = "An error occurred while loading the edit page.";
                return RedirectToAction("Index");
            }
        }

        // POST: TrainingSessions/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Edit(TrainingSessionViewModel model)
        {
            try
            {
                var sites = db.Guards
                    .Where(g => g.Site != null && g.IsActive)
                    .Select(g => g.Site)
                    .Distinct()
                    .ToList();

                if (!sites.Any())
                {
                    sites = new List<string> { "Site A", "Site B", "Site C" };
                }

                ViewBag.Sites = new SelectList(sites);

                if (!ModelState.IsValid)
                {
                    return View(model);
                }

                var session = db.TrainingSessions.Find(model.Id);
                if (session == null)
                {
                    TempData["ErrorMessage"] = "Training session not found.";
                    return RedirectToAction("Index");
                }

                session.Title = model.Title;
                session.StartDate = model.StartDate;
                session.EndDate = model.EndDate;
                session.Site = model.Site;

                db.SaveChanges();

                TempData["SuccessMessage"] = "Training session updated successfully!";
                return RedirectToAction("Index");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error in Edit (POST): {ex.Message}");
                TempData["ErrorMessage"] = "Error updating training session: " + ex.Message;
                return View(model);
            }
        }

        // GET: TrainingSessions/Delete/5
        public ActionResult Delete(int id)
        {
            try
            {
                var session = db.TrainingSessions
                    .Include(t => t.Enrollments)
                    .FirstOrDefault(t => t.Id == id);

                if (session == null)
                {
                    TempData["ErrorMessage"] = "Training session not found.";
                    return RedirectToAction("Index");
                }

                return View(session);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error in Delete (GET): {ex.Message}");
                TempData["ErrorMessage"] = "An error occurred while loading the delete page.";
                return RedirectToAction("Index");
            }
        }

        // POST: TrainingSessions/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public ActionResult DeleteConfirmed(int id)
        {
            try
            {
                var session = db.TrainingSessions.Find(id);
                if (session == null)
                {
                    TempData["ErrorMessage"] = "Training session not found.";
                    return RedirectToAction("Index");
                }

                // Get enrollments for this session
                var enrollments = db.TrainingEnrollments.Where(e => e.TrainingSessionId == id).ToList();

                // Remove all enrollments
                foreach (var enrollment in enrollments)
                {
                    db.TrainingEnrollments.Remove(enrollment);
                }

                // Remove the session
                db.TrainingSessions.Remove(session);
                db.SaveChanges();

                TempData["SuccessMessage"] = "Training session deleted successfully!";
                return RedirectToAction("Index");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error in DeleteConfirmed: {ex.Message}");
                TempData["ErrorMessage"] = "Error deleting training session: " + ex.Message;
                return RedirectToAction("Delete", new { id = id });
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

    // TrainingSessionViewModel class
    public class TrainingSessionViewModel
    {
        public int Id { get; set; }

        [Required]
        [StringLength(100)]
        public string Title { get; set; }

        [Required]
        [Display(Name = "Start Date")]
        public DateTime StartDate { get; set; }

        [Required]
        [Display(Name = "End Date")]
        public DateTime EndDate { get; set; }

        [Required]
        [Display(Name = "Site")]
        public string Site { get; set; }

        [Display(Name = "Capacity")]
        public int Capacity { get; set; }

        [Display(Name = "Select Guards")]
        public List<int> SelectedGuardIds { get; set; }

        public TrainingSessionViewModel()
        {
            SelectedGuardIds = new List<int>();
        }
    }
}