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
                // Get current user's ID from Identity
                var currentUserId = User.Identity.Name;

                // Find director by email
                var director = db.Directors.FirstOrDefault(d => d.Email == currentUserId);
                if (director == null)
                {
                    // Still return view but with empty data
                    ViewBag.TotalIncidents = 0;
                    ViewBag.ResolvedIncidents = 0;
                    ViewBag.PendingIncidents = 0;
                    ViewBag.InProgressIncidents = 0;
                    ViewBag.HighPriorityIncidents = 0;
                    ViewBag.CriticalPriorityIncidents = 0;
                    ViewBag.ThisMonthIncidents = 0;
                    ViewBag.TotalGuardCheckIns = 0;
                    ViewBag.TodayCheckIns = 0;
                    ViewBag.CurrentOnDuty = 0;
                    ViewBag.RecentIncidents = new List<IncidentReport>();
                    return View();
                }

                // Set session for future use
                Session["DirectorId"] = director.DirectorId.ToString();


                // Get notifications
                var notifications = db.Notifications
                    .Where(n => n.UserId == director.DirectorId.ToString() && n.UserType == "Director")
                    .OrderByDescending(n => n.CreatedAt)
                    .ToList();

                ViewBag.Notifications = notifications;
                ViewBag.UnreadNotificationCount = notifications.Count(n => !n.IsRead);

                // Create login notification for Director (not Admin)
                var existingLoginNotification = notifications.FirstOrDefault(n =>
                    n.Message.Contains("logged in successfully") &&
                    n.UserId == director.DirectorId.ToString());

                if (existingLoginNotification == null)
                {
                    // Create login notification for Director
                    var loginNotification = new Notification
                    {
                        UserId = director.DirectorId.ToString(),
                        UserType = "Director",
                        Title = "Login Successful",
                        Message = $"You have logged in successfully at {DateTime.Now.ToString("hh:mm tt")} on {DateTime.Now.ToString("MMM dd, yyyy")}",
                        IsRead = false,
                        CreatedAt = DateTime.Now,
                        RelatedUrl = Url.Action("Dashboard", "Director"),
                        NotificationType = "Login",
                        PriorityLevel = 1
                    };
                    db.Notifications.Add(loginNotification);
                    db.SaveChanges();

                    TempData["LoginSuccess"] = "You have successfully logged in!";
                }
                else if (!existingLoginNotification.IsRead)
                {
                    TempData["LoginSuccess"] = "You have successfully logged in!";
                    existingLoginNotification.IsRead = true;
                    db.SaveChanges();
                }

                // Calculate statistics with proper database queries
                ViewBag.TotalIncidents = db.IncidentReports.Count();
                ViewBag.ResolvedIncidents = db.IncidentReports.Count(i => i.Status == "Resolved");
                ViewBag.PendingIncidents = db.IncidentReports.Count(i => i.Status == "Pending");
                ViewBag.InProgressIncidents = db.IncidentReports.Count(i => i.Status == "In Progress");
                ViewBag.HighPriorityIncidents = db.IncidentReports.Count(i => i.Priority == "High");
                ViewBag.CriticalPriorityIncidents = db.IncidentReports.Count(i => i.Priority == "Critical");

                // Fix for month comparison - current month incidents
                var now = DateTime.Now;
                ViewBag.ThisMonthIncidents = db.IncidentReports
                    .Count(i => i.ReportDate.Month == now.Month && i.ReportDate.Year == now.Year);

                // Guard statistics
                ViewBag.TotalGuardCheckIns = db.GuardCheckIns.Count();

                // Today's check-ins (current date only)
                var today = DateTime.Today;
                ViewBag.TodayCheckIns = db.GuardCheckIns
                    .Count(g => DbFunctions.TruncateTime(g.CheckInTime) == today);

                // Current on duty (today and status present)
                ViewBag.CurrentOnDuty = db.GuardCheckIns
                    .Where(g => DbFunctions.TruncateTime(g.CheckInTime) == today && g.Status == "Present")
                    .Select(g => g.GuardId)
                    .Distinct()
                    .Count();

                // Recent incidents - ensure we're getting data
                ViewBag.RecentIncidents = db.IncidentReports
                    .OrderByDescending(i => i.ReportDate)
                    .Take(10)
                    .ToList();

                // Debug information
                System.Diagnostics.Debug.WriteLine($"Total incidents: {ViewBag.TotalIncidents}");
                System.Diagnostics.Debug.WriteLine($"Recent incidents count: {((List<IncidentReport>)ViewBag.RecentIncidents).Count}");

                return View();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("Dashboard error: " + ex.Message);
                System.Diagnostics.Debug.WriteLine("Stack trace: " + ex.StackTrace);
                TempData["ErrorMessage"] = "Error loading dashboard statistics: " + ex.Message;

                // Set default values on error
                ViewBag.TotalIncidents = 0;
                ViewBag.RecentIncidents = new List<IncidentReport>();
                return View();
            }
        }

        // GET: Director/Statistics
        public ActionResult Statistics()
        {
            try
            {
                var statistics = new StatisticsViewModel
                {
                    // Basic incident counts
                    TotalIncidents = db.IncidentReports.Count(),
                    ResolvedIncidents = db.IncidentReports.Count(i => i.Status == "Resolved"),
                    PendingIncidents = db.IncidentReports.Count(i => i.Status == "Pending"),
                    InProgressIncidents = db.IncidentReports.Count(i => i.Status == "In Progress"),
                    HighPriorityIncidents = db.IncidentReports.Count(i => i.Priority == "High"),
                    CriticalPriorityIncidents = db.IncidentReports.Count(i => i.Priority == "Critical"),

                    // Monthly incidents with proper grouping
                    MonthlyIncidents = db.IncidentReports
                        .Where(i => i.ReportDate.Year == DateTime.Now.Year)
                        .AsEnumerable()
                        .GroupBy(i => i.ReportDate.Month)
                        .Select(g => new MonthlyIncidentData
                        {
                            Month = g.Key,
                            Count = g.Count()
                        })
                        .OrderBy(x => x.Month)
                        .ToList(),

                    // Incidents by type with null check
                    IncidentByType = db.IncidentReports
                        .Where(i => i.IncidentType != null)
                        .GroupBy(i => i.IncidentType)
                        .Select(g => new IncidentTypeData
                        {
                            Type = g.Key ?? "Unknown",
                            Count = g.Count()
                        })
                        .OrderByDescending(x => x.Count)
                        .ToList(),

                    // Incidents by status with null check
                    IncidentByStatus = db.IncidentReports
                        .Where(i => i.Status != null)
                        .GroupBy(i => i.Status)
                        .Select(g => new IncidentStatusData
                        {
                            Status = g.Key ?? "Unknown",
                            Count = g.Count()
                        })
                        .ToList(),

                    // Guard statistics
                    TotalGuards = db.Guards.Count(g => g.IsActive),
                    TotalCheckIns = db.GuardCheckIns.Count(),

                    // Today's check-ins with proper date comparison
                    TodayCheckIns = db.GuardCheckIns.Count(g =>
                        DbFunctions.TruncateTime(g.CheckInTime) == DbFunctions.TruncateTime(DateTime.Now)),

                    CurrentOnDuty = db.GuardCheckIns
                        .Where(g => DbFunctions.TruncateTime(g.CheckInTime) == DbFunctions.TruncateTime(DateTime.Now) &&
                                   g.Status == "Present")
                        .Select(g => g.GuardId)
                        .Distinct()
                        .Count(),

                    // Average resolution time with proper calculation
                    AverageResolutionTime = db.IncidentReports
                        .Where(i => i.Status == "Resolved" && i.FeedbackDate != null && i.ReportDate != null)
                        .AsEnumerable()
                        .Select(i => (i.FeedbackDate.Value - i.ReportDate).TotalDays)
                        .DefaultIfEmpty(0)
                        .Average()
                };

                return View(statistics);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("Statistics error: " + ex.Message);
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
                    .Include("Guard")
                    .FirstOrDefault(i => i.IncidentReportId == id.Value);

                if (incident == null)
                {
                    TempData["ErrorMessage"] = "Incident not found.";
                    return RedirectToAction("Incidents");
                }

                // Create notification for Director about viewing incident
                CreateDirectorNotification(
                    "Incident Viewed",
                    $"You viewed incident #{id.Value} - {incident.IncidentType}",
                    Url.Action("IncidentDetails", "Director", new { id = id.Value }),
                    "Incident",
                    2
                );

                // Load resident user information if ResidentId exists
                if (!string.IsNullOrEmpty(incident.ResidentId))
                {
                    var residentUser = db.Users.FirstOrDefault(u => u.Id == incident.ResidentId);
                    ViewBag.ResidentUser = residentUser;
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
                        IncidentReportId = i.IncidentReportId,
                        IncidentType = i.IncidentType ?? "Unknown",
                        Location = i.Location ?? "Unknown",
                        Status = i.Status ?? "Unknown",
                        Priority = i.Priority ?? "Unknown",
                        ReportDate = i.ReportDate
                    })
                    .ToList()
                    .Select(i => new
                    {
                        i.IncidentReportId,
                        i.IncidentType,
                        i.Location,
                        i.Status,
                        i.Priority,
                        ReportDate = i.ReportDate.ToString("yyyy-MM-ddTHH:mm:ss.fffZ")
                    })
                    .ToList();

                return Json(new { Success = true, Incidents = recentIncidents }, JsonRequestBehavior.AllowGet);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("GetRecentIncidents error: " + ex.Message);
                return Json(new
                {
                    Success = false,
                    Error = ex.Message,
                    Incidents = new List<object>()
                }, JsonRequestBehavior.AllowGet);
            }
        }

        // GET: Director/Notifications
        public ActionResult Notifications(string typeFilter = "", string statusFilter = "", int page = 1, int pageSize = 20)
        {
            try
            {
                string directorId = GetCurrentDirectorId();
                if (string.IsNullOrEmpty(directorId))
                {
                    TempData["ErrorMessage"] = "Please log in to view notifications.";
                    return RedirectToAction("Login", "Account");
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

        // GET: Director/GetNotifications - UPDATED VERSION
        [HttpGet]
        public JsonResult GetNotifications()
        {
            try
            {
                string directorId = GetCurrentDirectorId();
                if (string.IsNullOrEmpty(directorId))
                {
                    return Json(new { error = "Not authenticated" }, JsonRequestBehavior.AllowGet);
                }

                var notifications = db.Notifications
                    .Where(n => n.UserId == directorId && n.UserType == "Director")
                    .OrderByDescending(n => n.CreatedAt)
                    .Take(20)
                    .Select(n => new
                    {
                        NotificationId = n.NotificationId,
                        Title = n.Title,
                        Message = n.Message,
                        IsRead = n.IsRead,
                        CreatedAt = n.CreatedAt,
                        RelatedUrl = n.RelatedUrl,
                        NotificationType = n.NotificationType,
                        IsImportant = n.IsImportant,
                        PriorityLevel = n.PriorityLevel
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
        [ValidateAntiForgeryToken]
        public JsonResult MarkNotificationAsRead(int id)
        {
            try
            {
                string directorId = GetCurrentDirectorId();
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
        [ValidateAntiForgeryToken]
        public JsonResult MarkAllNotificationsAsRead()
        {
            try
            {
                string directorId = GetCurrentDirectorId();
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

                // Create notification for Director about viewing incidents
                CreateDirectorNotification(
                    "Incidents Viewed",
                    "You accessed the incidents management page",
                    Url.Action("Incidents", "Director"),
                    "System",
                    1
                );

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
        public JsonResult UpdateIncidentStatus(int incidentId, string status, string feedback = "", HttpPostedFileBase pdfFile = null, bool removeFile = false)
        {
            try
            {
                var incident = db.IncidentReports.Find(incidentId);
                if (incident == null)
                {
                    return Json(new { success = false, message = "Incident not found." });
                }

                string oldStatus = incident.Status;
                incident.Status = status;
                incident.FeedbackDate = DateTime.Now;

                // Handle feedback text
                if (!string.IsNullOrEmpty(feedback))
                {
                    incident.Feedback = feedback;
                }

                // Handle PDF file upload
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

                    using (var memoryStream = new MemoryStream())
                    {
                        pdfFile.InputStream.CopyTo(memoryStream);
                        byte[] fileBytes = memoryStream.ToArray();
                        string base64FileData = Convert.ToBase64String(fileBytes);

                        incident.FeedbackFileData = base64FileData;
                        incident.FeedbackFileName = Path.GetFileName(pdfFile.FileName);
                        incident.FeedbackFileType = pdfFile.ContentType;
                        incident.FeedbackFileSize = pdfFile.ContentLength;

                        var fileName = $"feedback_{incidentId}_{DateTime.Now:yyyyMMddHHmmss}.pdf";
                        incident.FeedbackAttachment = fileName;
                    }
                }
                else if (removeFile)
                {
                    incident.FeedbackFileData = null;
                    incident.FeedbackFileName = null;
                    incident.FeedbackFileType = null;
                    incident.FeedbackFileSize = null;
                    incident.FeedbackAttachment = null;
                }

                db.Entry(incident).State = EntityState.Modified;
                db.SaveChanges();

                // Create notification for Director about status update
                CreateDirectorNotification(
                    "Incident Status Updated",
                    $"You updated incident #{incidentId} from {oldStatus} to {status}",
                    Url.Action("IncidentDetails", "Director", new { id = incidentId }),
                    "Incident",
                    3
                );

                // Create notification for resident about status update
                if (!string.IsNullOrEmpty(incident.ResidentId))
                {
                    var notification = new Notification
                    {
                        UserId = incident.ResidentId,
                        UserType = "Resident",
                        Title = "Incident Status Updated",
                        Message = $"Your incident report #{incidentId} status has been updated to '{status}'.",
                        IsRead = false,
                        CreatedAt = DateTime.Now,
                        RelatedUrl = Url.Action("MyIncidents", "Resident"),
                        NotificationType = "Incident",
                        PriorityLevel = 3
                    };
                    db.Notifications.Add(notification);
                    db.SaveChanges();
                }

                string statusBadge = GetStatusBadge(status).ToString();

                return Json(new
                {
                    success = true,
                    message = $"Incident status updated to {status} successfully.",
                    statusBadge = statusBadge,
                    hasFeedback = !string.IsNullOrEmpty(incident.Feedback) || !string.IsNullOrEmpty(incident.FeedbackFileData),
                    feedback = incident.Feedback,
                    hasAttachment = !string.IsNullOrEmpty(incident.FeedbackFileData),
                    attachmentName = incident.FeedbackFileName
                });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("UpdateIncidentStatus error: " + ex.Message);
                return Json(new { success = false, message = "Error updating incident status: " + ex.Message });
            }
        }

        // GET: Director/DownloadFeedbackAttachment
        [Authorize(Roles = "Director")]
        public ActionResult DownloadFeedbackAttachment(int id)
        {
            try
            {
                var incident = db.IncidentReports.Find(id);
                if (incident == null)
                {
                    TempData["ErrorMessage"] = "Incident report not found.";
                    return RedirectToAction("Incidents");
                }

                // Check if we have file data stored in database
                if (!string.IsNullOrEmpty(incident.FeedbackFileData))
                {
                    return DownloadFileFromDatabase(incident);
                }

                // Fallback to file path method
                if (string.IsNullOrEmpty(incident.FeedbackAttachment))
                {
                    TempData["ErrorMessage"] = "No attachment available for this incident.";
                    return RedirectToAction("Incidents");
                }

                string storedFilePath = incident.FeedbackAttachment;
                string actualFilePath = FindActualFilePath(storedFilePath);

                if (string.IsNullOrEmpty(actualFilePath) || !System.IO.File.Exists(actualFilePath))
                {
                    TempData["ErrorMessage"] = "The requested file is not available.";
                    return RedirectToAction("Incidents");
                }

                FileInfo fileInfo = new FileInfo(actualFilePath);
                string fileNameForDownload = GetDownloadFileName(incident, fileInfo.Name);

                byte[] fileBytes = System.IO.File.ReadAllBytes(actualFilePath);
                return File(fileBytes, "application/pdf", fileNameForDownload);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("DownloadFeedbackAttachment error: " + ex.Message);
                TempData["ErrorMessage"] = "An error occurred while downloading the file.";
                return RedirectToAction("Incidents");
            }
        }

        // Helper method to download file from database storage
        private ActionResult DownloadFileFromDatabase(IncidentReport incident)
        {
            try
            {
                if (string.IsNullOrEmpty(incident.FeedbackFileData))
                {
                    TempData["ErrorMessage"] = "File data not available.";
                    return RedirectToAction("Incidents");
                }

                byte[] fileBytes = Convert.FromBase64String(incident.FeedbackFileData);
                string fileName = incident.FeedbackFileName ?? $"Incident_Feedback_{incident.IncidentReportId}.pdf";
                string contentType = incident.FeedbackFileType ?? "application/pdf";

                return File(fileBytes, contentType, fileName);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("Download from database error: " + ex.Message);
                TempData["ErrorMessage"] = "Error processing file data.";
                return RedirectToAction("Incidents");
            }
        }

        // Helper method to find the actual file path
        private string FindActualFilePath(string storedFilePath)
        {
            if (string.IsNullOrEmpty(storedFilePath))
                return null;

            if (System.IO.File.Exists(storedFilePath))
                return storedFilePath;

            string fileName = Path.GetFileName(storedFilePath);

            var possibleLocations = new[]
            {
                Server.MapPath("~/App_Data/Attachments/"),
                Server.MapPath("~/Uploads/"),
                Server.MapPath("~/Files/"),
                Server.MapPath("~/Content/Attachments/"),
                Server.MapPath("~/Attachments/"),
                Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Attachments"),
                Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "App_Data", "Attachments")
            };

            foreach (var location in possibleLocations)
            {
                try
                {
                    if (Directory.Exists(location))
                    {
                        string possiblePath = Path.Combine(location, fileName);
                        if (System.IO.File.Exists(possiblePath))
                            return possiblePath;
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Error checking location {location}: {ex.Message}");
                }
            }

            return null;
        }

        // Helper method to generate download file name
        private string GetDownloadFileName(IncidentReport incident, string originalFileName)
        {
            string fileExtension = Path.GetExtension(originalFileName);
            if (string.IsNullOrEmpty(fileExtension))
                fileExtension = ".pdf";

            return $"Incident_Feedback_{incident.IncidentReportId}_{DateTime.Now:yyyyMMdd}{fileExtension}";
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

                // Create notification for Director about viewing guard logs
                CreateDirectorNotification(
                    "Guard Logs Viewed",
                    "You accessed the guard logs management page",
                    Url.Action("GuardLogs", "Director"),
                    "Guard",
                    1
                );

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

        // Helper method to get current director ID
        private string GetCurrentDirectorId()
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
            }
            return directorId;
        }

        // Helper method to create director notifications
        private void CreateDirectorNotification(string title, string message, string relatedUrl, string notificationType, int priorityLevel)
        {
            try
            {
                string directorId = GetCurrentDirectorId();
                if (!string.IsNullOrEmpty(directorId))
                {
                    var notification = new Notification
                    {
                        UserId = directorId,
                        UserType = "Director",
                        Title = title,
                        Message = message,
                        IsRead = false,
                        CreatedAt = DateTime.Now,
                        RelatedUrl = relatedUrl,
                        NotificationType = notificationType,
                        PriorityLevel = priorityLevel
                    };
                    db.Notifications.Add(notification);
                    db.SaveChanges();
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error creating director notification: {ex.Message}");
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