using PiranaSecuritySystem.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;

namespace PiranaSecuritySystem.Controllers
{
    public class NotificationController : Controller
    {
        private ApplicationDbContext db = new ApplicationDbContext();

        // GET: Notification/GetAdminNotifications
        public JsonResult GetAdminNotifications()
        {
            try
            {
                var notifications = db.Notifications
                    .Where(n => n.UserType == "Admin" || n.UserType == "Director" || string.IsNullOrEmpty(n.UserType))
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
                        TimeAgo = n.GetTimeAgo()
                    })
                    .ToList();

                var unreadCount = db.Notifications
                    .Count(n => (n.UserType == "Admin" || n.UserType == "Director" || string.IsNullOrEmpty(n.UserType)) && !n.IsRead);

                return Json(new
                {
                    success = true,
                    notifications = notifications,
                    unreadCount = unreadCount
                }, JsonRequestBehavior.AllowGet);
            }
            catch (Exception ex)
            {
                return Json(new { success = false, error = ex.Message }, JsonRequestBehavior.AllowGet);
            }
        }

        // GET: Notification/GetDirectorNotifications
        public JsonResult GetDirectorNotifications(int directorId)
        {
            try
            {
                var notifications = db.Notifications
                    .Where(n => n.UserId == directorId.ToString() && n.UserType == "Director")
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
                        n.PriorityLevel,
                        TimeAgo = n.GetTimeAgo()
                    })
                    .ToList();

                var unreadCount = notifications.Count(n => !n.IsRead);

                return Json(new
                {
                    success = true,
                    notifications = notifications,
                    unreadCount = unreadCount
                }, JsonRequestBehavior.AllowGet);
            }
            catch (Exception ex)
            {
                return Json(new { success = false, error = ex.Message }, JsonRequestBehavior.AllowGet);
            }
        }

        // GET: Notification/GetResidentNotifications
        public JsonResult GetResidentNotifications(int residentId)
        {
            try
            {
                var notifications = db.Notifications
                    .Where(n => n.UserId == residentId.ToString() && n.UserType == "Resident")
                    .OrderByDescending(n => n.CreatedAt)
                    .Select(n => new
                    {
                        n.NotificationId,
                        n.Title,
                        n.Message,
                        n.IsRead,
                        n.CreatedAt,
                        n.RelatedUrl,
                        TimeAgo = n.GetTimeAgo()
                    })
                    .ToList();

                return Json(notifications, JsonRequestBehavior.AllowGet);
            }
            catch (Exception ex)
            {
                return Json(new { error = ex.Message }, JsonRequestBehavior.AllowGet);
            }
        }

        // GET: Notification/GetGuardNotifications
        public JsonResult GetGuardNotifications(int guardId)
        {
            try
            {
                var notifications = db.Notifications
                    .Where(n => n.UserId == guardId.ToString() && n.UserType == "Guard")
                    .OrderByDescending(n => n.CreatedAt)
                    .Select(n => new
                    {
                        n.NotificationId,
                        n.Title,
                        n.Message,
                        n.IsRead,
                        n.CreatedAt,
                        n.RelatedUrl,
                        TimeAgo = n.GetTimeAgo()
                    })
                    .ToList();

                return Json(notifications, JsonRequestBehavior.AllowGet);
            }
            catch (Exception ex)
            {
                return Json(new { error = ex.Message }, JsonRequestBehavior.AllowGet);
            }
        }

        // GET: Notification/GetInstructorNotifications
        public JsonResult GetInstructorNotifications(int instructorId)
        {
            try
            {
                var notifications = db.Notifications
                    .Where(n => n.UserId == instructorId.ToString() && n.UserType == "Instructor")
                    .OrderByDescending(n => n.CreatedAt)
                    .Select(n => new
                    {
                        n.NotificationId,
                        n.Title,
                        n.Message,
                        n.IsRead,
                        n.CreatedAt,
                        n.RelatedUrl,
                        TimeAgo = n.GetTimeAgo()
                    })
                    .ToList();

                return Json(notifications, JsonRequestBehavior.AllowGet);
            }
            catch (Exception ex)
            {
                return Json(new { error = ex.Message }, JsonRequestBehavior.AllowGet);
            }
        }

        // POST: Notification/MarkAsRead
        [HttpPost]
        public JsonResult MarkAsRead(int id)
        {
            try
            {
                var notification = db.Notifications.Find(id);
                if (notification != null)
                {
                    notification.IsRead = true;
                    notification.DateRead = DateTime.Now;
                    db.SaveChanges();
                    return Json(new { success = true });
                }
                return Json(new { success = false, error = "Notification not found" });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, error = ex.Message });
            }
        }

        // POST: Notification/MarkAllAsRead
        [HttpPost]
        public JsonResult MarkAllAsRead(string userId, string userType)
        {
            try
            {
                var notifications = db.Notifications
                    .Where(n => n.UserId == userId && n.UserType == userType && !n.IsRead)
                    .ToList();

                foreach (var notification in notifications)
                {
                    notification.IsRead = true;
                    notification.DateRead = DateTime.Now;
                }

                db.SaveChanges();
                return Json(new { success = true });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, error = ex.Message });
            }
        }

        // POST: Notification/MarkAllAdminAsRead
        [HttpPost]
        public JsonResult MarkAllAdminAsRead()
        {
            try
            {
                var notifications = db.Notifications
                    .Where(n => (n.UserType == "Admin" || n.UserType == "Director" || string.IsNullOrEmpty(n.UserType)) && !n.IsRead)
                    .ToList();

                foreach (var notification in notifications)
                {
                    notification.IsRead = true;
                    notification.DateRead = DateTime.Now;
                }

                db.SaveChanges();
                return Json(new { success = true });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, error = ex.Message });
            }
        }

        // POST: Notification/CreateLoginNotification
        [HttpPost]
        public JsonResult CreateLoginNotification(string userType, string userName, int userId, string site = null)
        {
            try
            {
                string relatedUrl = "/Admin/Dashboard";
                string notificationUserId = "Admin";
                string notificationUserType = "Admin";

                // Determine the related URL and notification recipient based on user type
                switch (userType.ToLower())
                {
                    case "guard":
                        relatedUrl = "/Admin/ManageGuards";
                        break;
                    case "instructor":
                        relatedUrl = "/Admin/ManageInstructors";
                        break;
                    case "director":
                        relatedUrl = "/Director/Dashboard";
                        notificationUserId = userId.ToString();
                        notificationUserType = "Director";
                        break;
                    case "admin":
                        relatedUrl = "/Admin/Dashboard";
                        break;
                    default:
                        relatedUrl = "/Admin/Dashboard";
                        break;
                }

                var notification = new Notification
                {
                    Title = "Login Activity",
                    Message = $"{userType} {userName} logged in at {DateTime.Now.ToString("hh:mm tt")} on {DateTime.Now.ToString("MMM dd, yyyy")}" +
                             (string.IsNullOrEmpty(site) ? "" : $" from {site}"),
                    NotificationType = "Login",
                    UserId = notificationUserId,
                    UserType = notificationUserType,
                    CreatedAt = DateTime.Now,
                    IsRead = false,
                    Source = "LoginSystem",
                    RelatedUrl = relatedUrl,
                    PriorityLevel = 1
                };

                db.Notifications.Add(notification);
                db.SaveChanges();

                return Json(new { success = true, message = "Login notification created" });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, error = ex.Message });
            }
        }

        // Helper method to create login notification (for use in other controllers)
        public void CreateUserLoginNotification(string userType, string userName, int userId, string site = null)
        {
            try
            {
                string relatedUrl = "/Admin/Dashboard";
                string notificationUserId = "Admin";
                string notificationUserType = "Admin";

                // Determine the related URL and notification recipient based on user type
                switch (userType.ToLower())
                {
                    case "guard":
                        relatedUrl = "/Admin/ManageGuards";
                        break;
                    case "instructor":
                        relatedUrl = "/Admin/ManageInstructors";
                        break;
                    case "admin":
                        relatedUrl = "/Admin/Dashboard";
                        notificationUserId = userId.ToString();
                        notificationUserType = "Admin";
                        break;
                    case "director":
                        relatedUrl = "/Director/Dashboard";
                        notificationUserId = userId.ToString();
                        notificationUserType = "Director";
                        break;
                    default:
                        relatedUrl = "/Admin/Dashboard";
                        break;
                }

                var notification = new Notification
                {
                    Title = "Login Activity",
                    Message = $"{userType} {userName} logged in at {DateTime.Now.ToString("hh:mm tt")} on {DateTime.Now.ToString("MMM dd, yyyy")}" +
                             (string.IsNullOrEmpty(site) ? "" : $" from {site}"),
                    NotificationType = "Login",
                    UserId = notificationUserId,
                    UserType = notificationUserType,
                    CreatedAt = DateTime.Now,
                    IsRead = false,
                    Source = "LoginSystem",
                    RelatedUrl = relatedUrl,
                    PriorityLevel = 1
                };

                db.Notifications.Add(notification);
                db.SaveChanges();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error creating login notification: {ex.Message}");
            }
        }

        // Helper method to create incident notification for Director
        public void CreateDirectorIncidentNotification(int directorId, string incidentType, int incidentId, string priority = "Medium")
        {
            try
            {
                int priorityLevel;

                // Convert priority string to priority level using traditional switch
                switch (priority.ToLower())
                {
                    case "critical":
                        priorityLevel = 4;
                        break;
                    case "high":
                        priorityLevel = 3;
                        break;
                    case "medium":
                        priorityLevel = 2;
                        break;
                    default:
                        priorityLevel = 1;
                        break;
                }

                var notification = new Notification
                {
                    UserId = directorId.ToString(),
                    UserType = "Director",
                    Title = "New Incident Reported",
                    Message = $"A new {incidentType} incident (#{incidentId}) has been reported and requires your attention.",
                    IsRead = false,
                    CreatedAt = DateTime.Now,
                    RelatedUrl = $"/Director/IncidentDetails/{incidentId}",
                    NotificationType = "Incident",
                    PriorityLevel = priorityLevel,
                    RelatedIncidentId = incidentId
                };

                db.Notifications.Add(notification);
                db.SaveChanges();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error creating director incident notification: {ex.Message}");
            }
        }

        // Helper method to create system notification for Director
        public void CreateDirectorSystemNotification(int directorId, string title, string message, string notificationType = "System", int priorityLevel = 1)
        {
            try
            {
                var notification = new Notification
                {
                    UserId = directorId.ToString(),
                    UserType = "Director",
                    Title = title,
                    Message = message,
                    IsRead = false,
                    CreatedAt = DateTime.Now,
                    RelatedUrl = "/Director/Dashboard",
                    NotificationType = notificationType,
                    PriorityLevel = priorityLevel
                };

                db.Notifications.Add(notification);
                db.SaveChanges();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error creating director system notification: {ex.Message}");
            }
        }

        // Helper method to create guard activity notification for Director
        public void CreateDirectorGuardActivityNotification(int directorId, string guardName, string activityType, int guardId)
        {
            try
            {
                var notification = new Notification
                {
                    UserId = directorId.ToString(),
                    UserType = "Director",
                    Title = "Guard Activity",
                    Message = $"Guard {guardName} has {activityType}.",
                    IsRead = false,
                    CreatedAt = DateTime.Now,
                    RelatedUrl = $"/Director/GuardLogs?guardId={guardId}",
                    NotificationType = "Guard",
                    PriorityLevel = 2
                };

                db.Notifications.Add(notification);
                db.SaveChanges();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error creating director guard activity notification: {ex.Message}");
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