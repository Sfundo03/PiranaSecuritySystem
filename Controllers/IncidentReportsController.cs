using PiranaSecuritySystem.Models;
using System.Data.Entity;
using System.Linq;
using System.Web.Mvc;
using System;

namespace PiranaSecuritySystem.Controllers
{
    [Authorize(Roles = "Director")]
    public class IncidentReportsController : Controller
    {
        private ApplicationDbContext db = new ApplicationDbContext();

        // GET: IncidentReports
        public ActionResult Index(string searchString, string statusFilter, string priorityFilter)
        {
            var incidentReports = db.IncidentReports.AsQueryable();

            // Search filter
            if (!string.IsNullOrEmpty(searchString))
            {
                incidentReports = incidentReports.Where(i =>
                    i.IncidentType.Contains(searchString) ||
                    i.Description.Contains(searchString) ||
                    i.Location.Contains(searchString) ||
                    i.ReportedBy.Contains(searchString));
            }

            // Status filter
            if (!string.IsNullOrEmpty(statusFilter))
            {
                incidentReports = incidentReports.Where(i => i.Status == statusFilter);
            }

            // Priority filter
            if (!string.IsNullOrEmpty(priorityFilter))
            {
                incidentReports = incidentReports.Where(i => i.Priority == priorityFilter);
            }

            // Add filter options to ViewBag for dropdowns
            ViewBag.StatusFilter = new SelectList(new[] { "All", "Pending", "In Progress", "Resolved" });
            ViewBag.PriorityFilter = new SelectList(new[] { "All", "Low", "Medium", "High", "Critical" });

            return View(incidentReports.OrderByDescending(i => i.ReportDate).ToList());
        }

        // GET: IncidentReports/Details/5
        public ActionResult Details(int? id)
        {
            if (id == null)
            {
                return new HttpStatusCodeResult(System.Net.HttpStatusCode.BadRequest);
            }

            IncidentReport incidentReport = db.IncidentReports.Find(id);
            if (incidentReport == null)
            {
                return HttpNotFound();
            }

            return View(incidentReport);
        }

        // GET: IncidentReports/Create
        public ActionResult Create()
        {
            return View();
        }

        // POST: IncidentReports/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Create(IncidentReport incidentReport)
        {
            if (ModelState.IsValid)
            {
                incidentReport.ReportDate = DateTime.Now;
                incidentReport.Status = "Pending"; // Default status

                db.IncidentReports.Add(incidentReport);
                db.SaveChanges();

                // Create notification for director
                CreateIncidentNotification(incidentReport);

                TempData["SuccessMessage"] = "Incident report created successfully!";
                return RedirectToAction("Index");
            }

            return View(incidentReport);
        }

        // GET: IncidentReports/Edit/5
        public ActionResult Edit(int? id)
        {
            if (id == null)
            {
                return new HttpStatusCodeResult(System.Net.HttpStatusCode.BadRequest);
            }

            IncidentReport incidentReport = db.IncidentReports.Find(id);
            if (incidentReport == null)
            {
                return HttpNotFound();
            }

            return View(incidentReport);
        }

        // POST: IncidentReports/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Edit(IncidentReport incidentReport)
        {
            if (ModelState.IsValid)
            {
                // Get the original incident to check if status changed
                var originalIncident = db.IncidentReports.AsNoTracking().FirstOrDefault(i => i.IncidentReportId == incidentReport.IncidentReportId);

                if (originalIncident != null && originalIncident.Status != incidentReport.Status)
                {
                    // Status has changed - notify the resident
                    CreateStatusChangeNotification(incidentReport, originalIncident.Status);
                }

                db.Entry(incidentReport).State = EntityState.Modified;
                db.SaveChanges();

                TempData["SuccessMessage"] = "Incident report updated successfully!";
                return RedirectToAction("Index");
            }

            return View(incidentReport);
        }

        // GET: IncidentReports/Delete/5
        public ActionResult Delete(int? id)
        {
            if (id == null)
            {
                return new HttpStatusCodeResult(System.Net.HttpStatusCode.BadRequest);
            }

            IncidentReport incidentReport = db.IncidentReports.Find(id);
            if (incidentReport == null)
            {
                return HttpNotFound();
            }

            return View(incidentReport);
        }

        // POST: IncidentReports/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public ActionResult DeleteConfirmed(int id)
        {
            IncidentReport incidentReport = db.IncidentReports.Find(id);
            db.IncidentReports.Remove(incidentReport);
            db.SaveChanges();

            TempData["SuccessMessage"] = "Incident report deleted successfully!";
            return RedirectToAction("Index");
        }

        // GET: IncidentReports/Statistics
        public ActionResult Statistics()
        {
            var stats = new
            {
                TotalIncidents = db.IncidentReports.Count(),
                ResolvedIncidents = db.IncidentReports.Count(i => i.Status == "Resolved"),
                PendingIncidents = db.IncidentReports.Count(i => i.Status == "Pending"),
                InProgressIncidents = db.IncidentReports.Count(i => i.Status == "In Progress"),
                HighPriority = db.IncidentReports.Count(i => i.Priority == "High" || i.Priority == "Critical")
            };

            return View(stats);
        }

        private void CreateIncidentNotification(IncidentReport incident)
        {
            try
            {
                var notification = new Notification
                {
                    Title = "New Incident Reported",
                    Message = $"New {incident.IncidentType} incident reported at {incident.Location}",
                    NotificationType = "Incident",
                    RelatedUrl = Url.Action("Details", "IncidentReports", new { id = incident.IncidentReportId }),
                    UserId = "Director", // Send to all directors
                    UserType = "Director",
                    CreatedAt = DateTime.Now,
                    IsRead = false
                };

                db.Notifications.Add(notification);
                db.SaveChanges();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error creating incident notification: {ex.Message}");
                // Don't throw, just log the error
            }
        }

        private void CreateStatusChangeNotification(IncidentReport incident, string oldStatus)
        {
            try
            {
                var notification = new Notification
                {
                    UserId = incident.ResidentId,
                    UserType = "Resident",
                    Title = "Incident Status Updated",
                    Message = $"There has been a status change in your incident report #{incident.IncidentReportId}. Status changed from {oldStatus} to {incident.Status}.",
                    IsRead = false,
                    CreatedAt = DateTime.Now,
                    RelatedUrl = Url.Action("IncidentDetails", "Resident", new { id = incident.IncidentReportId }),
                    NotificationType = "Incident"
                };

                db.Notifications.Add(notification);
                db.SaveChanges();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error creating status change notification: {ex.Message}");
                // Don't throw, just log the error
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