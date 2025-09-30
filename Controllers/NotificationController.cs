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
                string relatedUrl = "/Admin/Dashboard"; // Default URL

                // Determine the related URL based on user type using traditional switch
                switch (userType.ToLower())
                {
                    case "guard":
                        relatedUrl = "/Admin/ManageGuards";
                        break;
                    case "instructor":
                        relatedUrl = "/Admin/ManageInstructors";
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
                    UserId = "Admin", // Notify admin about logins
                    UserType = "Admin",
                    CreatedAt = DateTime.Now,
                    IsRead = false,
                    Source = "LoginSystem",
                    RelatedUrl = relatedUrl
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
                string relatedUrl = "/Admin/Dashboard"; // Default URL

                // Determine the related URL based on user type using traditional switch
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
                        break;
                    case "director":
                        relatedUrl = "/Director/Dashboard";
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
                    UserId = "Admin", // Notify admin about all logins
                    UserType = "Admin",
                    CreatedAt = DateTime.Now,
                    IsRead = false,
                    Source = "LoginSystem",
                    RelatedUrl = relatedUrl
                };

                db.Notifications.Add(notification);
                db.SaveChanges();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error creating login notification: {ex.Message}");
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