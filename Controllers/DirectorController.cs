using PiranaSecuritySystem.Models;
using PiranaSecuritySystem.ViewModels;
using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Data.Entity.Core.Objects;
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
                var currentUserId = User.Identity.Name; // This gets the username/email

                // Find director by email
                var director = db.Directors.FirstOrDefault(d => d.Email == currentUserId);
                if (director == null)
                {
                    // Removed the error message about director profile not found
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

                // Fix for month comparison - use DbFunctions for database compatibility
                var now = DateTime.Now;
                ViewBag.ThisMonthIncidents = db.IncidentReports
                    .Count(i => i.ReportDate.Month == now.Month && i.ReportDate.Year == now.Year);

                // Guard statistics with null checks
                ViewBag.TotalGuardCheckIns = db.GuardCheckIns.Count();

                // Updated: Replaced EntityFunctions with DbFunctions
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

        // Add this method to create notifications for the director
        private void CreateNotificationForDirector(string title, string message, string notificationType = "System", string relatedUrl = "")
        {
            try
            {
                string directorId = Session["DirectorId"] as string;

                if (string.IsNullOrEmpty(directorId))
                {
                    // Try to get director ID from database if not in session
                    var currentUser = User.Identity.Name;
                    var director = db.Directors.FirstOrDefault(d => d.Email == currentUser);
                    if (director != null)
                    {
                        directorId = director.DirectorId.ToString();
                        Session["DirectorId"] = directorId;
                    }
                    else
                    {
                        // If we can't find the director, log the error but don't throw
                        System.Diagnostics.Debug.WriteLine("Cannot create notification: Director not found");
                        return;
                    }
                }

                var notification = new Notification
                {
                    UserId = directorId,
                    UserType = "Director",
                    Title = title,
                    Message = message,
                    NotificationType = notificationType,
                    CreatedAt = DateTime.Now,
                    RelatedUrl = relatedUrl,
                    IsImportant = notificationType == "Incident" || notificationType == "Emergency"
                };

                db.Notifications.Add(notification);
                db.SaveChanges();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("Error creating notification: " + ex.Message);
            }
        }

        // Add this method to be called from other controllers when incidents are reported
        [AllowAnonymous] // Allow other controllers to call this
        public JsonResult NotifyIncidentReported(int incidentId, string reportedBy)
        {
            try
            {
                var incident = db.IncidentReports.Find(incidentId);
                if (incident != null)
                {
                    string title = "New Incident Reported";
                    string message = $"A new {incident.IncidentType} incident has been reported by {reportedBy} at {incident.Location}";
                    string relatedUrl = Url.Action("IncidentDetails", "Director", new { id = incidentId });

                    CreateNotificationForDirector(title, message, "Incident", relatedUrl);

                    return Json(new { success = true }, JsonRequestBehavior.AllowGet);
                }
                return Json(new { success = false, error = "Incident not found" }, JsonRequestBehavior.AllowGet);
            }
            catch (Exception ex)
            {
                return Json(new { success = false, error = ex.Message }, JsonRequestBehavior.AllowGet);
            }
        }

        // Add this method to be called when guards/instructors are added
        [AllowAnonymous] // Allow other controllers to call this
        public JsonResult NotifyStaffAdded(string staffType, string staffName, int staffId)
        {
            try
            {
                string title = $"New {staffType} Added";
                string message = $"{staffType} {staffName} has been added to the system";
                string relatedUrl = staffType == "Guard"
                    ? Url.Action("Index", "Guards")
                    : Url.Action("Index", "Instructors");

                CreateNotificationForDirector(title, message, staffType, relatedUrl);

                return Json(new { success = true }, JsonRequestBehavior.AllowGet);
            }
            catch (Exception ex)
            {
                return Json(new { success = false, error = ex.Message }, JsonRequestBehavior.AllowGet);
            }
        }

        // Add this method to be called when payroll is created
        [AllowAnonymous] // Allow other controllers to call this
        public JsonResult NotifyPayrollCreated(int payrollId, string guardName)
        {
            try
            {
                string title = "Payroll Created";
                string message = $"Payroll has been created for guard {guardName}";
                string relatedUrl = Url.Action("PayrollDetails", "Director", new { id = payrollId });

                CreateNotificationForDirector(title, message, "Report", relatedUrl);

                return Json(new { success = true }, JsonRequestBehavior.AllowGet);
            }
            catch (Exception ex)
            {
                return Json(new { success = false, error = ex.Message }, JsonRequestBehavior.AllowGet);
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
                    // Updated: Replaced EntityFunctions with DbFunctions
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
                // Get director ID from session or identity
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

                // Apply filters
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

                // Order by creation date (newest first)
                query = query.OrderByDescending(n => n.CreatedAt);

                // Implement pagination
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
                // Get director ID from session or identity
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

        // POST: Director/DeleteNotification
        [HttpPost]
        public JsonResult DeleteNotification(int id)
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
                    db.Notifications.Remove(notification);
                    db.SaveChanges();
                    return Json(new { success = true });
                }

                return Json(new { success = false, error = "Notification not found or access denied" });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("DeleteNotification error: " + ex.Message);
                return Json(new { success = false, error = ex.Message });
            }
        }

        // POST: Director/DeleteMultipleNotifications
        [HttpPost]
        public JsonResult DeleteMultipleNotifications(List<int> ids)
        {
            try
            {
                string directorId = Session["DirectorId"] as string;
                if (string.IsNullOrEmpty(directorId))
                {
                    return Json(new { success = false, error = "Not authenticated" });
                }

                var notifications = db.Notifications
                    .Where(n => n.UserId == directorId && n.UserType == "Director" && ids.Contains(n.NotificationId))
                    .ToList();

                db.Notifications.RemoveRange(notifications);
                db.SaveChanges();

                return Json(new { success = true, deletedCount = notifications.Count });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("DeleteMultipleNotifications error: " + ex.Message);
                return Json(new { success = false, error = ex.Message });
            }
        }

        // GET: Director/NotificationBellPartial
        [ChildActionOnly]
        public ActionResult NotificationBellPartial()
        {
            string directorId = Session["DirectorId"] as string;
            if (string.IsNullOrEmpty(directorId))
            {
                return PartialView("_NotificationBell", new List<Notification>());
            }

            var notifications = db.Notifications
                .Where(n => n.UserId == directorId && n.UserType == "Director")
                .OrderByDescending(n => n.CreatedAt)
                .Take(10)
                .ToList();

            return PartialView("_NotificationBell", notifications);
        }

        // GET: Director/GuardLogs
        public ActionResult GuardLogs(DateTime? startDate = null, DateTime? endDate = null, int? guardId = null)
        {
            try
            {
                // Set default date range to current month if not specified
                if (!startDate.HasValue)
                    startDate = new DateTime(DateTime.Now.Year, DateTime.Now.Month, 1);
                if (!endDate.HasValue)
                    endDate = DateTime.Now.Date.AddDays(1).AddSeconds(-1); // End of today

                var logsQuery = db.GuardCheckIns
                    .Include(g => g.Guard)
                    .AsQueryable();

                // Apply date filters
                logsQuery = logsQuery.Where(g => g.CheckInTime >= startDate.Value && g.CheckInTime <= endDate.Value);

                // Apply guard filter if specified
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

        // GET: Director/GuardLogsExport
        public ActionResult GuardLogsExport(DateTime? startDate = null, DateTime? endDate = null, int? guardId = null)
        {
            try
            {
                // Set default date range to current month if not specified
                if (!startDate.HasValue)
                    startDate = new DateTime(DateTime.Now.Year, DateTime.Now.Month, 1);
                if (!endDate.HasValue)
                    endDate = DateTime.Now.Date.AddDays(1).AddSeconds(-1); // End of today

                var logsQuery = db.GuardCheckIns
                    .Include(g => g.Guard)
                    .AsQueryable();

                // Apply date filters
                logsQuery = logsQuery.Where(g => g.CheckInTime >= startDate.Value && g.CheckInTime <= endDate.Value);

                // Apply guard filter if specified
                if (guardId.HasValue && guardId > 0)
                {
                    logsQuery = logsQuery.Where(g => g.GuardId == guardId.Value);
                }

                var logs = logsQuery
                    .OrderByDescending(g => g.CheckInTime)
                    .ToList();

                // Create CSV content
                var csv = "Date,Time,Guard Name,Status\n";
                foreach (var log in logs)
                {
                    csv += $"\"{log.CheckInTime:yyyy-MM-dd}\",\"{log.CheckInTime:HH:mm:ss}\",\"{log.Guard?.FullName}\",\"{log.Status}\"\n";
                }

                byte[] buffer = System.Text.Encoding.UTF8.GetBytes(csv);
                return File(buffer, "text/csv", $"guard-logs-{DateTime.Now:yyyyMMddHHmmss}.csv");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("GuardLogsExport error: " + ex.Message);
                TempData["ErrorMessage"] = "Error exporting guard logs.";
                return RedirectToAction("GuardLogs");
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

                // Apply filters
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

        // GET: Director/IncidentDetails/5
        public ActionResult IncidentDetails(int id)
        {
            try
            {
                var incident = db.IncidentReports
                    .Include("Resident")
                    .FirstOrDefault(i => i.IncidentReportId == id);

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

                // Update the status
                incident.Status = status;

                // Handle PDF file upload
                if (pdfFile != null && pdfFile.ContentLength > 0)
                {
                    // Validate file type
                    if (pdfFile.ContentType != "application/pdf")
                    {
                        return Json(new { success = false, message = "Only PDF files are allowed." });
                    }

                    // Validate file size (5MB max)
                    if (pdfFile.ContentLength > 5 * 1024 * 1024)
                    {
                        return Json(new { success = false, message = "File size must be less than 5MB." });
                    }

                    // Generate unique file name
                    var fileName = $"feedback_{incidentId}_{DateTime.Now:yyyyMMddHHmmss}.pdf";
                    var path = Path.Combine(Server.MapPath("~/App_Data/FeedbackFiles"), fileName);

                    // Ensure directory exists
                    Directory.CreateDirectory(Path.GetDirectoryName(path));

                    // Save the file
                    pdfFile.SaveAs(path);

                    // Store file name in database
                    incident.FeedbackAttachment = fileName;
                    incident.Feedback = fileName; // Store filename as feedback for display
                }
                else if (removeFile && !string.IsNullOrEmpty(incident.FeedbackAttachment))
                {
                    // Remove existing file
                    var oldFilePath = Path.Combine(Server.MapPath("~/App_Data/FeedbackFiles"), incident.FeedbackAttachment);
                    if (System.IO.File.Exists(oldFilePath))
                    {
                        System.IO.File.Delete(oldFilePath);
                    }
                    incident.FeedbackAttachment = null;
                    incident.Feedback = null;
                }

                // Update the feedback date
                incident.FeedbackDate = DateTime.Now;

                db.Entry(incident).State = EntityState.Modified;
                db.SaveChanges();

                // Get the status badge HTML
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

        // GET: Director/DownloadFeedback/5
        public ActionResult DownloadFeedback(int id)
        {
            try
            {
                var incident = db.IncidentReports.Find(id);
                if (incident == null || string.IsNullOrEmpty(incident.FeedbackAttachment) || !incident.FeedbackAttachment.EndsWith(".pdf"))
                {
                    TempData["ErrorMessage"] = "Feedback file not found.";
                    return RedirectToAction("IncidentDetails", new { id });
                }

                var path = Path.Combine(Server.MapPath("~/App_Data/FeedbackFiles"), incident.FeedbackAttachment);
                if (!System.IO.File.Exists(path))
                {
                    TempData["ErrorMessage"] = "Feedback file not found on server.";
                    return RedirectToAction("IncidentDetails", new { id });
                }

                return File(path, "application/pdf", incident.FeedbackAttachment);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("DownloadFeedback error: " + ex.Message);
                TempData["ErrorMessage"] = "Error downloading feedback file.";
                return RedirectToAction("IncidentDetails", new { id });
            }
        }

        // GET: Director/Reports
        public ActionResult Reports()
        {
            try
            {
                var reportData = new
                {
                    MonthlyIncidents = db.IncidentReports
                        .Where(i => i.ReportDate.Year == DateTime.Now.Year)
                        .GroupBy(i => i.ReportDate.Month)
                        .Select(g => new { Month = g.Key, Count = g.Count() })
                        .OrderBy(x => x.Month)
                        .ToList(),
                    IncidentByType = db.IncidentReports
                        .GroupBy(i => i.IncidentType)
                        .Select(g => new { Type = g.Key, Count = g.Count() })
                        .OrderByDescending(x => x.Count)
                        .ToList(),
                    IncidentByStatus = db.IncidentReports
                        .GroupBy(i => i.Status)
                        .Select(g => new { Status = g.Key, Count = g.Count() })
                        .ToList(),
                    AverageResolutionTime = db.IncidentReports
                        .Where(i => i.Status == "Resolved" && i.FeedbackDate != null)
                        .Average(i => (i.FeedbackDate.Value - i.ReportDate).TotalDays)
                };

                return View(reportData);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("Reports error: " + ex.Message);
                TempData["ErrorMessage"] = "Error generating reports.";
                return View();
            }
        }

        // GET: Director/Statistics
        public ActionResult Statistics()
        {
            try
            {
                // Get incident statistics
                var incidentStats = new
                {
                    TotalIncidents = db.IncidentReports.Count(),
                    ResolvedIncidents = db.IncidentReports.Count(i => i.Status == "Resolved"),
                    PendingIncidents = db.IncidentReports.Count(i => i.Status == "Pending"),
                    InProgressIncidents = db.IncidentReports.Count(i => i.Status == "In Progress"),
                    HighPriorityIncidents = db.IncidentReports.Count(i => i.Priority == "High"),
                    CriticalPriorityIncidents = db.IncidentReports.Count(i => i.Priority == "Critical"),
                    ThisMonthIncidents = db.IncidentReports.Count(i => i.ReportDate.Month == DateTime.Now.Month && i.ReportDate.Year == DateTime.Now.Year)
                };

                // Get guard statistics
                var guardStats = new
                {
                    TotalGuards = db.Guards.Count(g => g.IsActive),
                    TotalCheckIns = db.GuardCheckIns.Count(),
                    TodayCheckIns = db.GuardCheckIns.Count(g => DbFunctions.TruncateTime(g.CheckInTime) == DbFunctions.TruncateTime(DateTime.Now)),
                    CurrentOnDuty = db.GuardCheckIns
                        .Where(g => DbFunctions.TruncateTime(g.CheckInTime) == DbFunctions.TruncateTime(DateTime.Now) && g.Status == "Present")
                        .GroupBy(g => g.GuardId)
                        .Count()
                };

                ViewBag.IncidentStats = incidentStats;
                ViewBag.GuardStats = guardStats;

                return View();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("Statistics error: " + ex.Message);
                TempData["ErrorMessage"] = "Error loading statistics.";
                return View();
            }
        }

        // GET: Director/Residents
        public ActionResult Residents()
        {
            try
            {
                var residents = db.Residents
                    .OrderBy(r => r.FullName)
                    .ToList();

                return View(residents);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("Residents error: " + ex.Message);
                TempData["ErrorMessage"] = "Error loading residents list.";
                return View(new List<Resident>());
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