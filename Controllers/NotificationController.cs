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
                        n.Message,
                        n.IsRead,
                        n.CreatedAt,
                        n.RelatedUrl
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
                }

                db.SaveChanges();
                return Json(new { success = true });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, error = ex.Message });
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