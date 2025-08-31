// Controllers/IncidentReportsController.cs
using PiranaSecuritSystem.Models;
using PiranaSecuritySystem.Models;
using System.Data.Entity;
using System.Linq;
using System.Web.Mvc;

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