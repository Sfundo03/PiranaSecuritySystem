using Microsoft.AspNet.Identity;
using PiranaSecuritySystem.Models;
using System;
using System.Linq;
using System.Web.Mvc;
using System.Collections.Generic;

namespace PiranaSecuritySystem.Controllers
{
    [Authorize(Roles = "Guard")]
    public class GuardController : Controller
    {
        private ApplicationDbContext db = new ApplicationDbContext();

        // GET: Guard/Dashboard
        public ActionResult Dashboard()
        {
            try
            {
                var currentUserId = User.Identity.GetUserId();

                if (string.IsNullOrEmpty(currentUserId))
                {
                    TempData["ErrorMessage"] = "User not authenticated. Please log in again.";
                    return RedirectToAction("Login", "Account");
                }

                var guard = db.Guards.FirstOrDefault(g => g.UserId == currentUserId);

                if (guard == null)
                {
                    TempData["ErrorMessage"] = "Guard profile not found. Please contact administrator.";
                    return RedirectToAction("ProfileNotFound", "Error");
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

                // Return the view with guard model
                return View("~/Views/Guard/Dashboard.cshtml", guard);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error in Dashboard: {ex.Message}");
                TempData["ErrorMessage"] = "An error occurred while loading the dashboard.";
                return RedirectToAction("Error", "Home");
            }
        }

        // GET: Guard/Create
        public ActionResult Create()
        {
            try
            {
                var currentUserId = User.Identity.GetUserId();

                if (string.IsNullOrEmpty(currentUserId))
                {
                    TempData["ErrorMessage"] = "User not authenticated. Please log in again.";
                    return RedirectToAction("Login", "Account");
                }

                var guard = db.Guards.FirstOrDefault(g => g.UserId == currentUserId);

                if (guard == null)
                {
                    TempData["ErrorMessage"] = "Guard profile not found. Please contact administrator.";
                    return RedirectToAction("ProfileNotFound", "Error");
                }

                // Create a new incident report with guard info pre-filled
                var incidentReport = new IncidentReport
                {
                    ResidentId = guard.GuardId,
                    ReportDate = DateTime.Now,
                    Status = "Pending Review",
                    CreatedBy = guard.FullName
                };

                // Populate dropdown lists
                ViewBag.IncidentTypes = GetIncidentTypes();
                ViewBag.Locations = GetLocations();
                ViewBag.PriorityLevels = GetPriorityLevels();

                return View("~/Views/Guard/Create.cshtml", incidentReport);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error in Create (GET): {ex.Message}");
                TempData["ErrorMessage"] = "An error occurred while loading the incident report form.";
                return RedirectToAction("Dashboard");
            }
        }

        // POST: Guard/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Create(IncidentReport incidentReport)
        {
            try
            {
                var currentUserId = User.Identity.GetUserId();

                if (string.IsNullOrEmpty(currentUserId))
                {
                    TempData["ErrorMessage"] = "User not authenticated. Please log in again.";
                    return RedirectToAction("Login", "Account");
                }

                var guard = db.Guards.FirstOrDefault(g => g.UserId == currentUserId);

                if (guard == null)
                {
                    TempData["ErrorMessage"] = "Guard profile not found. Please contact administrator.";
                    return RedirectToAction("ProfileNotFound", "Error");
                }

                if (ModelState.IsValid)
                {
                    // Set additional properties
                    incidentReport.ResidentId = guard.GuardId;
                    incidentReport.ReportDate = DateTime.Now;
                    incidentReport.Status = "Pending Review";
                    incidentReport.CreatedBy = guard.FullName;
                    incidentReport.CreatedDate = DateTime.Now;

                    db.IncidentReports.Add(incidentReport);
                    db.SaveChanges();

                    // Create notification for the incident report
                    var notification = new Notification
                    {
                        GuardId = guard.GuardId,
                        UserId = currentUserId,
                        UserType = "Guard",
                        Title = "Incident Reported",
                        Message = $"You have successfully reported a {incidentReport.IncidentType} incident at {incidentReport.Location}",
                        NotificationType = "Incident",
                        IsRead = false,
                        CreatedAt = DateTime.Now,
                        IsImportant = true,
                        PriorityLevel = 3 // High priority
                    };

                    db.Notifications.Add(notification);
                    db.SaveChanges();

                    TempData["SuccessMessage"] = "Incident reported successfully!";
                    return RedirectToAction("Dashboard");
                }

                // If model state is invalid, repopulate dropdowns
                ViewBag.IncidentTypes = GetIncidentTypes();
                ViewBag.Locations = GetLocations();
                ViewBag.PriorityLevels = GetPriorityLevels();

                return View("~/Views/Guard/Create.cshtml", incidentReport);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error in Create (POST): {ex.Message}");
                TempData["ErrorMessage"] = "An error occurred while reporting the incident.";

                // Repopulate dropdowns
                ViewBag.IncidentTypes = GetIncidentTypes();
                ViewBag.Locations = GetLocations();
                ViewBag.PriorityLevels = GetPriorityLevels();

                return View("~/Views/Guard/Create.cshtml", incidentReport);
            }
        }

        // GET: Guard/MyIncidentReports
        public ActionResult MyIncidentReports()
        {
            try
            {
                var currentUserId = User.Identity.GetUserId();

                if (string.IsNullOrEmpty(currentUserId))
                {
                    TempData["ErrorMessage"] = "User not authenticated. Please log in again.";
                    return RedirectToAction("Login", "Account");
                }

                var guard = db.Guards.FirstOrDefault(g => g.UserId == currentUserId);

                if (guard == null)
                {
                    TempData["ErrorMessage"] = "Guard profile not found. Please contact administrator.";
                    return RedirectToAction("ProfileNotFound", "Error");
                }

                var incidents = db.IncidentReports
                    .Where(r => r.ResidentId == guard.GuardId)
                    .OrderByDescending(r => r.ReportDate)
                    .ToList();

                // Check if the view exists, if not return a simple message
                return View("~/Views/Guard/MyIncidentReports.cshtml", incidents);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error in MyIncidentReports: {ex.Message}");
                TempData["ErrorMessage"] = "An error occurred while loading your incident reports.";
                return RedirectToAction("Dashboard");
            }
        }

        // GET: Guard/Attendance
        public ActionResult Attendance()
        {
            // Redirect to the static attendance page
            return Redirect("~/Content/Guard/Index.html");
        }

        // GET: Guard/Calender
        public ActionResult Calender()
        {
            try
            {
                var currentUserId = User.Identity.GetUserId();

                if (string.IsNullOrEmpty(currentUserId))
                {
                    TempData["ErrorMessage"] = "User not authenticated. Please log in again.";
                    return RedirectToAction("Login", "Account");
                }

                var guard = db.Guards.FirstOrDefault(g => g.UserId == currentUserId);

                if (guard == null)
                {
                    TempData["ErrorMessage"] = "Guard profile not found. Please contact administrator.";
                    return RedirectToAction("ProfileNotFound", "Error");
                }

                var shifts = db.Shifts
                    .Where(s => s.GuardId == guard.GuardId && s.StartDate >= DateTime.Today)
                    .OrderBy(s => s.StartDate)
                    .ToList();

                // Check if the view exists, if not return a simple message
                return View("~/Views/Guard/Calender.cshtml", shifts);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error in Calender: {ex.Message}");
                TempData["ErrorMessage"] = "An error occurred while loading the calendar.";
                return RedirectToAction("Dashboard");
            }
        }

        // GET: Guard/Notifications
        public ActionResult Notifications()
        {
            try
            {
                var currentUserId = User.Identity.GetUserId();

                if (string.IsNullOrEmpty(currentUserId))
                {
                    TempData["ErrorMessage"] = "User not authenticated. Please log in again.";
                    return RedirectToAction("Login", "Account");
                }

                var guard = db.Guards.FirstOrDefault(g => g.UserId == currentUserId);

                if (guard == null)
                {
                    TempData["ErrorMessage"] = "Guard profile not found. Please contact administrator.";
                    return RedirectToAction("ProfileNotFound", "Error");
                }

                // Get notifications for the guard
                var notifications = db.Notifications
                    .Where(n => n.GuardId == guard.GuardId)
                    .OrderByDescending(n => n.CreatedAt)
                    .ToList()
                    .Select(n => new NotificationViewModel
                    {
                        NotificationId = n.NotificationId,
                        Title = n.Title,
                        Message = n.Message,
                        NotificationType = n.NotificationType,
                        IsRead = n.IsRead,
                        CreatedAt = n.CreatedAt,
                        TimeAgo = GetTimeAgo(n.CreatedAt),
                        RelatedUrl = n.RelatedUrl,
                        IsImportant = n.IsImportant,
                        PriorityLevel = n.PriorityLevel,
                        PriorityClass = GetPriorityClass(n.PriorityLevel)
                    })
                    .ToList();

                // Check if the view exists, if not return a simple message
                return View("~/Views/Guard/Notifications.cshtml", notifications);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error in Notifications: {ex.Message}");
                TempData["ErrorMessage"] = "An error occurred while loading notifications.";
                return RedirectToAction("Dashboard");
            }
        }

        // API: Get notifications for guard
        [HttpGet]
        public JsonResult GetGuardNotifications()
        {
            try
            {
                var currentUserId = User.Identity.GetUserId();
                var guard = db.Guards.FirstOrDefault(g => g.UserId == currentUserId);

                if (guard == null)
                {
                    return Json(new { success = false, message = "Guard not found" }, JsonRequestBehavior.AllowGet);
                }

                var notifications = db.Notifications
                    .Where(n => n.GuardId == guard.GuardId)
                    .OrderByDescending(n => n.CreatedAt)
                    .Take(10)
                    .ToList()
                    .Select(n => new
                    {
                        notificationId = n.NotificationId,
                        title = n.Title,
                        message = n.Message,
                        notificationType = n.NotificationType,
                        isRead = n.IsRead,
                        createdAt = n.CreatedAt,
                        isImportant = n.IsImportant
                    })
                    .ToList();

                return Json(notifications, JsonRequestBehavior.AllowGet);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error in GetGuardNotifications: {ex.Message}");
                return Json(new { success = false, message = "Error loading notifications" }, JsonRequestBehavior.AllowGet);
            }
        }

        // API: Mark all notifications as read
        [HttpPost]
        public JsonResult MarkAllNotificationsAsRead()
        {
            try
            {
                var currentUserId = User.Identity.GetUserId();
                var guard = db.Guards.FirstOrDefault(g => g.UserId == currentUserId);

                if (guard == null)
                {
                    return Json(new { success = false, message = "Guard not found" });
                }

                var unreadNotifications = db.Notifications
                    .Where(n => n.GuardId == guard.GuardId && !n.IsRead)
                    .ToList();

                foreach (var notification in unreadNotifications)
                {
                    notification.IsRead = true;
                    notification.DateRead = DateTime.Now;
                }

                db.SaveChanges();

                return Json(new { success = true, message = "All notifications marked as read" });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error in MarkAllNotificationsAsRead: {ex.Message}");
                return Json(new { success = false, message = "Error marking notifications as read" });
            }
        }

        // API: Create a new notification
        [HttpPost]
        public JsonResult CreateNotification(string title, string message, string notificationType)
        {
            try
            {
                var currentUserId = User.Identity.GetUserId();
                var guard = db.Guards.FirstOrDefault(g => g.UserId == currentUserId);

                if (guard == null)
                {
                    return Json(new { success = false, message = "Guard not found" });
                }

                var notification = new Notification
                {
                    GuardId = guard.GuardId,
                    UserId = currentUserId,
                    UserType = "Guard",
                    Title = title,
                    Message = message,
                    NotificationType = notificationType,
                    IsRead = false,
                    CreatedAt = DateTime.Now,
                    IsImportant = notificationType == "Incident" || notificationType == "Security"
                };

                db.Notifications.Add(notification);
                db.SaveChanges();

                return Json(new { success = true, message = "Notification created successfully" });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error in CreateNotification: {ex.Message}");
                return Json(new { success = false, message = "Error creating notification" });
            }
        }

        // GET: Guard/CreateDashboardView (Temporary action to help create the view)
        public ActionResult CreateDashboardView()
        {
            // This action helps you understand what the Dashboard view should contain
            var currentUserId = User.Identity.GetUserId();
            var guard = db.Guards.FirstOrDefault(g => g.UserId == currentUserId);

            if (guard == null)
            {
                // Return a sample guard for view creation purposes
                guard = new Guard
                {
                    Guard_FName = "Sample",
                    Guard_LName = "Guard",
                    GuardId = 1
                };
            }

            return View("~/Views/Guard/Dashboard.cshtml", guard);
        }

        // API method to validate guard (MVC style)
        [HttpPost]
        public JsonResult ValidateGuard(string firstName)
        {
            try
            {
                if (string.IsNullOrEmpty(firstName))
                {
                    return Json(new { isValid = false, message = "First name is required" });
                }

                // Check if guard exists with the provided first name
                var guard = db.Guards.FirstOrDefault(g =>
                    g.Guard_FName.Equals(firstName, StringComparison.OrdinalIgnoreCase) &&
                    g.IsActive);

                if (guard != null)
                {
                    return Json(new
                    {
                        isValid = true,
                        guardId = guard.GuardId,
                        message = "Guard validated successfully"
                    });
                }
                else
                {
                    return Json(new
                    {
                        isValid = false,
                        message = "Guard name not recognized"
                    });
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error in ValidateGuard: {ex.Message}");
                return Json(new
                {
                    isValid = false,
                    message = "An error occurred while validating guard"
                });
            }
        }

        // API method to save check-in/check-out (MVC style)
        [HttpPost]
        public JsonResult SaveCheckIn(int guardId, string status)
        {
            try
            {
                if (guardId <= 0)
                {
                    return Json(new { success = false, message = "Invalid guard ID" });
                }

                // Create a new check-in record
                var checkIn = new GuardCheckIn
                {
                    GuardId = guardId,
                    CheckInTime = DateTime.Now,
                    Status = status,
                    CreatedDate = DateTime.Now
                };

                db.GuardCheckIns.Add(checkIn);
                db.SaveChanges();

                // Create a notification for the check-in
                var currentUserId = User.Identity.GetUserId();
                var notification = new Notification
                {
                    GuardId = guardId,
                    UserId = currentUserId,
                    UserType = "Guard",
                    Title = "Check-in Recorded",
                    Message = $"You have successfully {status.ToLower()} at {DateTime.Now.ToShortTimeString()}",
                    NotificationType = "Checkin",
                    IsRead = false,
                    CreatedAt = DateTime.Now
                };

                db.Notifications.Add(notification);
                db.SaveChanges();

                return Json(new { success = true, message = "Check-in recorded successfully" });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error in SaveCheckIn: {ex.Message}");
                return Json(new { success = false, message = "An error occurred while saving check-in" });
            }
        }

        // Helper method to get time ago string
        private string GetTimeAgo(DateTime date)
        {
            var timeSpan = DateTime.Now - date;

            if (timeSpan <= TimeSpan.FromSeconds(60))
                return "Just now";
            else if (timeSpan <= TimeSpan.FromMinutes(60))
                return $"{(int)timeSpan.TotalMinutes} minutes ago";
            else if (timeSpan <= TimeSpan.FromHours(24))
                return $"{(int)timeSpan.TotalHours} hours ago";
            else
                return $"{(int)timeSpan.TotalDays} days ago";
        }

        // Helper method to get priority class
        private string GetPriorityClass(int priorityLevel)
        {
            switch (priorityLevel)
            {
                case 1: return "badge bg-secondary";
                case 2: return "badge bg-info";
                case 3: return "badge bg-warning";
                case 4: return "badge bg-danger";
                default: return "badge bg-secondary";
            }
        }

        // Helper method to get incident types for dropdown
        private List<SelectListItem> GetIncidentTypes()
        {
            return new List<SelectListItem>
            {
                new SelectListItem { Value = "Theft", Text = "Theft" },
                new SelectListItem { Value = "Burglary", Text = "Burglary" },
                new SelectListItem { Value = "Vandalism", Text = "Vandalism" },
                new SelectListItem { Value = "Trespassing", Text = "Trespassing" },
                new SelectListItem { Value = "Suspicious Activity", Text = "Suspicious Activity" },
                new SelectListItem { Value = "Assault", Text = "Assault" },
                new SelectListItem { Value = "Fire", Text = "Fire" },
                new SelectListItem { Value = "Medical Emergency", Text = "Medical Emergency" },
                new SelectListItem { Value = "Other", Text = "Other" }
            };
        }

        // Helper method to get locations for dropdown
        private List<SelectListItem> GetLocations()
        {
            return new List<SelectListItem>
            {
                new SelectListItem { Value = "Main Gate", Text = "Main Gate" },
                new SelectListItem { Value = "North Gate", Text = "North Gate" },
                new SelectListItem { Value = "South Gate", Text = "South Gate" },
                new SelectListItem { Value = "East Gate", Text = "East Gate" },
                new SelectListItem { Value = "West Gate", Text = "West Gate" },
                new SelectListItem { Value = "Building A", Text = "Building A" },
                new SelectListItem { Value = "Building B", Text = "Building B" },
                new SelectListItem { Value = "Building C", Text = "Building C" },
                new SelectListItem { Value = "Parking Lot", Text = "Parking Lot" },
                new SelectListItem { Value = "Perimeter", Text = "Perimeter" },
                new SelectListItem { Value = "Other", Text = "Other" }
            };
        }

        // Helper method to get priority levels for dropdown
        private List<SelectListItem> GetPriorityLevels()
        {
            return new List<SelectListItem>
            {
                new SelectListItem { Value = "Low", Text = "Low" },
                new SelectListItem { Value = "Medium", Text = "Medium" },
                new SelectListItem { Value = "High", Text = "High" },
                new SelectListItem { Value = "Critical", Text = "Critical" }
            };
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