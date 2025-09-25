using PiranaSecuritySystem.Models;
using PiranaSecuritySystem.ViewModels;
using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.IO;
using System.Linq;
using System.Web;
using System.Web.Mvc;

namespace PiranaSecuritySystem.Controllers
{
    [Authorize(Roles = "Director")]
    public class DirectorController : Controller
    {
        private ApplicationDbContext db = new ApplicationDbContext();

        // GET: Director/Dashboard
        public ActionResult Dashboard()
        {
            try
            {
                // Get current user's ID from Identity instead of session
                var currentUserId = User.Identity.Name;

                // Find director by email
                var director = db.Directors.FirstOrDefault(d => d.Email == currentUserId);
                if (director == null)
                {
                    return View();
                }

                // Set session for future use
                Session["DirectorId"] = director.DirectorId.ToString();

                // Get notifications for this director
                var notifications = db.Notifications
                    .Where(n => n.UserId == director.DirectorId.ToString() && n.UserType == "Director")
                    .OrderByDescending(n => n.CreatedAt)
                    .ToList();

                ViewBag.Notifications = notifications;
                ViewBag.UnreadNotificationCount = notifications.Count(n => !n.IsRead);

                // Check if we should show login success message
                var loginNotification = notifications.FirstOrDefault(n => n.Message.Contains("logged in successfully"));
                if (loginNotification != null && !loginNotification.IsRead)
                {
                    TempData["LoginSuccess"] = "You have successfully logged in!";
                    loginNotification.IsRead = true;
                    db.SaveChanges();
                }

                // Calculate statistics with null checks
                ViewBag.TotalIncidents = db.IncidentReports.Count();
                ViewBag.ResolvedIncidents = db.IncidentReports.Count(i => i.Status == "Resolved");
                ViewBag.PendingIncidents = db.IncidentReports.Count(i => i.Status == "Pending");
                ViewBag.InProgressIncidents = db.IncidentReports.Count(i => i.Status == "In Progress");
                ViewBag.HighPriorityIncidents = db.IncidentReports.Count(i => i.Priority == "High");
                ViewBag.CriticalPriorityIncidents = db.IncidentReports.Count(i => i.Priority == "Critical");

                // Fix for month comparison
                var now = DateTime.Now;
                ViewBag.ThisMonthIncidents = db.IncidentReports
                    .Count(i => i.ReportDate.Month == now.Month && i.ReportDate.Year == now.Year);

                // Guard statistics with null checks
                ViewBag.TotalGuardCheckIns = db.GuardCheckIns.Count();

                ViewBag.TodayCheckIns = db.GuardCheckIns
                    .Count(g => DbFunctions.TruncateTime(g.CheckInTime) == DbFunctions.TruncateTime(DateTime.Now));

                ViewBag.CurrentOnDuty = db.GuardCheckIns
                    .Where(g => DbFunctions.TruncateTime(g.CheckInTime) == DbFunctions.TruncateTime(DateTime.Now) &&
                               g.Status == "Present")
                    .GroupBy(g => g.GuardId)
                    .Count();

                // Recent incidents
                ViewBag.RecentIncidents = db.IncidentReports
                    .OrderByDescending(i => i.ReportDate)
                    .Take(10)
                    .ToList();

                return View();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("Dashboard error: " + ex.Message);
                System.Diagnostics.Debug.WriteLine("Stack trace: " + ex.StackTrace);
                TempData["ErrorMessage"] = "Error loading dashboard statistics: " + ex.Message;
                return View();
            }
        }

        // GET: Director/Statistics - FIXED VERSION
        public ActionResult Statistics()
        {
            try
            {
                // Create StatisticsViewModel with all required data including the missing properties
                var statistics = new StatisticsViewModel
                {
                    TotalIncidents = db.IncidentReports.Count(),
                    ResolvedIncidents = db.IncidentReports.Count(i => i.Status == "Resolved"),
                    PendingIncidents = db.IncidentReports.Count(i => i.Status == "Pending"),
                    InProgressIncidents = db.IncidentReports.Count(i => i.Status == "In Progress"),
                    HighPriorityIncidents = db.IncidentReports.Count(i => i.Priority == "High"),
                    CriticalPriorityIncidents = db.IncidentReports.Count(i => i.Priority == "Critical"),

                    // Monthly incidents for current year
                    MonthlyIncidents = db.IncidentReports
                        .Where(i => i.ReportDate.Year == DateTime.Now.Year)
                        .GroupBy(i => i.ReportDate.Month)
                        .Select(g => new MonthlyIncidentData
                        {
                            Month = g.Key,
                            Count = g.Count()
                        })
                        .OrderBy(x => x.Month)
                        .ToList(),

                    // Incidents by type
                    IncidentByType = db.IncidentReports
                        .GroupBy(i => i.IncidentType)
                        .Select(g => new IncidentTypeData
                        {
                            Type = g.Key,
                            Count = g.Count()
                        })
                        .OrderByDescending(x => x.Count)
                        .ToList(),

                    // Incidents by status
                    IncidentByStatus = db.IncidentReports
                        .GroupBy(i => i.Status)
                        .Select(g => new IncidentStatusData
                        {
                            Status = g.Key,
                            Count = g.Count()
                        })
                        .ToList(),

                    // Guard statistics - INCLUDING THE MISSING PROPERTIES
                    TotalGuards = db.Guards.Count(g => g.IsActive),
                    TotalCheckIns = db.GuardCheckIns.Count(),
                    TodayCheckIns = db.GuardCheckIns.Count(g => DbFunctions.TruncateTime(g.CheckInTime) == DbFunctions.TruncateTime(DateTime.Now)),
                    CurrentOnDuty = db.GuardCheckIns
                        .Where(g => DbFunctions.TruncateTime(g.CheckInTime) == DbFunctions.TruncateTime(DateTime.Now) && g.Status == "Present")
                        .GroupBy(g => g.GuardId)
                        .Count(),

                    // Average resolution time - INCLUDING THE MISSING PROPERTY
                    AverageResolutionTime = db.IncidentReports
                        .Where(i => i.Status == "Resolved" && i.FeedbackDate != null && i.ReportDate != null)
                        .AsEnumerable() // Switch to client-side evaluation for TimeSpan operations
                        .Average(i => (i.FeedbackDate.Value - i.ReportDate).TotalDays)
                };

                return View(statistics);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("Statistics error: " + ex.Message);
                System.Diagnostics.Debug.WriteLine("Stack trace: " + ex.StackTrace);
                TempData["ErrorMessage"] = "Error loading statistics: " + ex.Message;
                return View(new StatisticsViewModel());
            }
        }

        // GET: Director/IncidentDetails
        public ActionResult IncidentDetails(int? id)
        {
            try
            {
                if (!id.HasValue)
                {
                    TempData["ErrorMessage"] = "Incident ID not provided.";
                    return RedirectToAction("Incidents");
                }

                var incident = db.IncidentReports
                    .Include("Resident")
                    .FirstOrDefault(i => i.IncidentReportId == id.Value);

                if (incident == null)
                {
                    TempData["ErrorMessage"] = "Incident not found.";
                    return RedirectToAction("Incidents");
                }

                // Add status options to ViewBag
                ViewBag.StatusOptions = new SelectList(new[] {
                    new { Value = "Pending", Text = "Pending" },
                    new { Value = "In Progress", Text = "In Progress" },
                    new { Value = "Resolved", Text = "Resolved" }
                }, "Value", "Text", incident.Status);

                return View(incident);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("IncidentDetails error: " + ex.Message);
                TempData["ErrorMessage"] = "Error loading incident details.";
                return RedirectToAction("Incidents");
            }
        }

        // GET: Director/AllIncidents
        public ActionResult AllIncidents()
        {
            try
            {
                var incidents = db.IncidentReports
                    .Include("Resident")
                    .OrderByDescending(i => i.ReportDate)
                    .ToList();

                return View(incidents);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("AllIncidents error: " + ex.Message);
                TempData["ErrorMessage"] = "Error loading all incidents.";
                return View(new List<IncidentReport>());
            }
        }

        // GET: Director/GetDashboardStats
        [HttpGet]
        public JsonResult GetDashboardStats()
        {
            try
            {
                var now = DateTime.Now;
                var stats = new
                {
                    TotalIncidents = db.IncidentReports.Count(),
                    ResolvedIncidents = db.IncidentReports.Count(i => i.Status == "Resolved"),
                    ThisMonthIncidents = db.IncidentReports
                        .Count(i => i.ReportDate.Month == now.Month && i.ReportDate.Year == now.Year),
                    HighPriorityIncidents = db.IncidentReports.Count(i => i.Priority == "High"),
                    CriticalPriorityIncidents = db.IncidentReports.Count(i => i.Priority == "Critical"),
                    TotalGuardCheckIns = db.GuardCheckIns.Count(),
                    TodayCheckIns = db.GuardCheckIns
                        .Count(g => DbFunctions.TruncateTime(g.CheckInTime) == DbFunctions.TruncateTime(DateTime.Now)),
                    CurrentOnDuty = db.GuardCheckIns
                        .Where(g => DbFunctions.TruncateTime(g.CheckInTime) == DbFunctions.TruncateTime(DateTime.Now) &&
                                   g.Status == "Present")
                        .GroupBy(g => g.GuardId)
                        .Count(),
                    Success = true
                };
                return Json(stats, JsonRequestBehavior.AllowGet);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("GetDashboardStats error: " + ex.Message);
                return Json(new { Success = false, Error = ex.Message }, JsonRequestBehavior.AllowGet);
            }
        }

        // GET: Director/GetRecentIncidents
        [HttpGet]
        public JsonResult GetRecentIncidents()
        {
            try
            {
                var recentIncidents = db.IncidentReports
                    .OrderByDescending(i => i.ReportDate)
                    .Take(10)
                    .Select(i => new
                    {
                        i.IncidentReportId,
                        i.IncidentType,
                        i.Location,
                        i.Status,
                        i.Priority,
                        ReportDate = i.ReportDate
                    })
                    .ToList();

                return Json(new { Success = true, Incidents = recentIncidents }, JsonRequestBehavior.AllowGet);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("GetRecentIncidents error: " + ex.Message);
                return Json(new { Success = false, Error = ex.Message }, JsonRequestBehavior.AllowGet);
            }
        }

        // GET: Director/Notifications
        public ActionResult Notifications(string typeFilter = "", string statusFilter = "", int page = 1, int pageSize = 20)
        {
            try
            {
                string directorId = Session["DirectorId"] as string;
                if (string.IsNullOrEmpty(directorId))
                {
                    var currentUser = User.Identity.Name;
                    var director = db.Directors.FirstOrDefault(d => d.Email == currentUser);
                    if (director != null)
                    {
                        directorId = director.DirectorId.ToString();
                        Session["DirectorId"] = directorId;
                    }
                    else
                    {
                        TempData["ErrorMessage"] = "Please log in to view notifications.";
                        return RedirectToAction("Login", "Account");
                    }
                }

                var query = db.Notifications
                    .Where(n => n.UserId == directorId && n.UserType == "Director")
                    .AsQueryable();

                if (!string.IsNullOrEmpty(typeFilter))
                {
                    query = query.Where(n => n.NotificationType == typeFilter);
                }

                if (!string.IsNullOrEmpty(statusFilter))
                {
                    if (statusFilter == "unread")
                    {
                        query = query.Where(n => !n.IsRead);
                    }
                    else if (statusFilter == "read")
                    {
                        query = query.Where(n => n.IsRead);
                    }
                }

                query = query.OrderByDescending(n => n.CreatedAt);

                var totalCount = query.Count();
                var notifications = query
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .ToList();

                ViewBag.TypeFilter = typeFilter;
                ViewBag.StatusFilter = statusFilter;
                ViewBag.CurrentPage = page;
                ViewBag.PageSize = pageSize;
                ViewBag.TotalCount = totalCount;
                ViewBag.TotalPages = (int)Math.Ceiling((double)totalCount / pageSize);

                return View(notifications);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error loading notifications: {ex.Message}");
                TempData["ErrorMessage"] = "Error loading notifications.";
                return View(new List<Notification>());
            }
        }

        // GET: Director/GetNotifications
        [HttpGet]
        public JsonResult GetNotifications()
        {
            try
            {
                string directorId = Session["DirectorId"] as string;
                if (string.IsNullOrEmpty(directorId))
                {
                    var currentUser = User.Identity.Name;
                    var director = db.Directors.FirstOrDefault(d => d.Email == currentUser);
                    if (director != null)
                    {
                        directorId = director.DirectorId.ToString();
                        Session["DirectorId"] = directorId;
                    }
                    else
                    {
                        return Json(new { error = "Not authenticated" }, JsonRequestBehavior.AllowGet);
                    }
                }

                var notifications = db.Notifications
                    .Where(n => n.UserId == directorId && n.UserType == "Director")
                    .OrderByDescending(n => n.CreatedAt)
                    .Take(20)
                    .Select(n => new
                    {
                        n.NotificationId,
                        n.Title,
                        n.Message,
                        n.IsRead,
                        n.CreatedAt,
                        n.RelatedUrl,
                        n.NotificationType,
                        n.IsImportant
                    })
                    .ToList();

                var unreadCount = notifications.Count(n => !n.IsRead);

                return Json(new
                {
                    notifications,
                    unreadCount
                }, JsonRequestBehavior.AllowGet);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("GetNotifications error: " + ex.Message);
                return Json(new { error = ex.Message }, JsonRequestBehavior.AllowGet);
            }
        }

        // POST: Director/MarkNotificationAsRead
        [HttpPost]
        public JsonResult MarkNotificationAsRead(int id)
        {
            try
            {
                string directorId = Session["DirectorId"] as string;
                if (string.IsNullOrEmpty(directorId))
                {
                    return Json(new { success = false, error = "Not authenticated" });
                }

                var notification = db.Notifications.Find(id);
                if (notification != null && notification.UserId == directorId && notification.UserType == "Director")
                {
                    notification.IsRead = true;
                    db.SaveChanges();
                    return Json(new { success = true });
                }

                return Json(new { success = false, error = "Notification not found or access denied" });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("MarkNotificationAsRead error: " + ex.Message);
                return Json(new { success = false, error = ex.Message });
            }
        }

        // POST: Director/MarkAllNotificationsAsRead
        [HttpPost]
        public JsonResult MarkAllNotificationsAsRead()
        {
            try
            {
                string directorId = Session["DirectorId"] as string;
                if (string.IsNullOrEmpty(directorId))
                {
                    return Json(new { success = false, error = "Not authenticated" });
                }

                var notifications = db.Notifications
                    .Where(n => n.UserId == directorId && n.UserType == "Director" && !n.IsRead)
                    .ToList();

                foreach (var notification in notifications)
                {
                    notification.IsRead = true;
                }

                db.SaveChanges();

                return Json(new { success = true, markedCount = notifications.Count });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("MarkAllNotificationsAsRead error: " + ex.Message);
                return Json(new { success = false, error = ex.Message });
            }
        }

        // GET: Director/Incidents
        public ActionResult Incidents(string status = "", string priority = "", string type = "")
        {
            try
            {
                var incidents = db.IncidentReports
                    .Include("Resident")
                    .AsQueryable();

                if (!string.IsNullOrEmpty(status) && status != "All")
                {
                    incidents = incidents.Where(i => i.Status == status);
                }

                if (!string.IsNullOrEmpty(priority) && priority != "All")
                {
                    incidents = incidents.Where(i => i.Priority == priority);
                }

                if (!string.IsNullOrEmpty(type) && type != "All")
                {
                    incidents = incidents.Where(i => i.IncidentType == type);
                }

                var incidentList = incidents
                    .OrderByDescending(i => i.ReportDate)
                    .ToList();

                ViewBag.StatusFilter = status;
                ViewBag.PriorityFilter = priority;
                ViewBag.TypeFilter = type;
                ViewBag.StatusList = new SelectList(new[] { "All", "Pending", "In Progress", "Resolved" });
                ViewBag.PriorityList = new SelectList(new[] { "All", "Low", "Medium", "High", "Critical" });
                ViewBag.TypeList = new SelectList(db.IncidentReports.Select(i => i.IncidentType).Distinct().ToList());

                return View(incidentList);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("Incidents error: " + ex.Message);
                TempData["ErrorMessage"] = "Error loading incidents.";
                return View(new List<IncidentReport>());
            }
        }
        
        // POST: Director/UpdateIncidentStatus
        [HttpPost]
        [ValidateAntiForgeryToken]
        public JsonResult UpdateIncidentStatus(int incidentId, string status, HttpPostedFileBase pdfFile = null, bool removeFile = false)
        {
            try
            {
                var incident = db.IncidentReports.Find(incidentId);
                if (incident == null)
                {
                    return Json(new { success = false, message = "Incident not found." });
                }

                incident.Status = status;

                if (pdfFile != null && pdfFile.ContentLength > 0)
                {
                    if (pdfFile.ContentType != "application/pdf")
                    {
                        return Json(new { success = false, message = "Only PDF files are allowed." });
                    }

                    if (pdfFile.ContentLength > 5 * 1024 * 1024)
                    {
                        return Json(new { success = false, message = "File size must be less than 5MB." });
                    }

                    var fileName = $"feedback_{incidentId}_{DateTime.Now:yyyyMMddHHmmss}.pdf";
                    var path = Path.Combine(Server.MapPath("~/App_Data/FeedbackFiles"), fileName);

                    Directory.CreateDirectory(Path.GetDirectoryName(path));
                    pdfFile.SaveAs(path);

                    incident.FeedbackAttachment = fileName;
                    incident.Feedback = fileName;
                }
                else if (removeFile && !string.IsNullOrEmpty(incident.FeedbackAttachment))
                {
                    var oldFilePath = Path.Combine(Server.MapPath("~/App_Data/FeedbackFiles"), incident.FeedbackAttachment);
                    if (System.IO.File.Exists(oldFilePath))
                    {
                        System.IO.File.Delete(oldFilePath);
                    }
                    incident.FeedbackAttachment = null;
                    incident.Feedback = null;
                }

                incident.FeedbackDate = DateTime.Now;

                db.Entry(incident).State = EntityState.Modified;
                db.SaveChanges();

                string statusBadge = GetStatusBadge(status).ToString();

                return Json(new
                {
                    success = true,
                    message = $"Incident status updated to {status} successfully.",
                    statusBadge = statusBadge,
                    hasFeedback = !string.IsNullOrEmpty(incident.Feedback),
                    feedback = incident.Feedback,
                    isPdf = !string.IsNullOrEmpty(incident.Feedback) && incident.Feedback.EndsWith(".pdf")
                });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("UpdateIncidentStatus error: " + ex.Message);
                return Json(new { success = false, message = "Error updating incident status." });
            }
        }

        // GET: Director/GuardLogs
        public ActionResult GuardLogs(DateTime? startDate = null, DateTime? endDate = null, int? guardId = null)
        {
            try
            {
                if (!startDate.HasValue)
                    startDate = new DateTime(DateTime.Now.Year, DateTime.Now.Month, 1);
                if (!endDate.HasValue)
                    endDate = DateTime.Now.Date.AddDays(1).AddSeconds(-1);

                var logsQuery = db.GuardCheckIns
                    .Include(g => g.Guard)
                    .AsQueryable();

                logsQuery = logsQuery.Where(g => g.CheckInTime >= startDate.Value && g.CheckInTime <= endDate.Value);

                if (guardId.HasValue && guardId > 0)
                {
                    logsQuery = logsQuery.Where(g => g.GuardId == guardId.Value);
                }

                var logs = logsQuery
                    .OrderByDescending(g => g.CheckInTime)
                    .ToList();

                ViewBag.StartDate = startDate.Value.ToString("yyyy-MM-dd");
                ViewBag.EndDate = endDate.Value.ToString("yyyy-MM-dd");
                ViewBag.GuardId = guardId;
                ViewBag.GuardsList = new SelectList(db.Guards.Where(g => g.IsActive).OrderBy(g => g.Guard_FName).ToList(), "GuardId", "FullName", guardId);

                return View(logs);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("GuardLogs error: " + ex.Message);
                TempData["ErrorMessage"] = "Error loading guard logs.";
                return View(new List<GuardCheckIn>());
            }
        }

        // Helper method to generate status badge HTML
        private IHtmlString GetStatusBadge(string status)
        {
            string badgeClass;
            switch (status)
            {
                case "Resolved":
                    badgeClass = "badge badge-success";
                    break;
                case "In Progress":
                    badgeClass = "badge badge-primary";
                    break;
                case "Pending":
                    badgeClass = "badge badge-warning";
                    break;
                default:
                    badgeClass = "badge badge-secondary";
                    break;
            }

            return new HtmlString($"<span class='{badgeClass}'>{status}</span>");
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