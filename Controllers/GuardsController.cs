using Microsoft.AspNet.Identity;
using PiranaSecuritySystem.Models;
using PiranaSecuritySystem.ViewModels;
using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using System.Web.Mvc;

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
                ViewBag.UpcomingShifts = db.ShiftRosters.Count(s => s.GuardId == guard.GuardId && s.RosterDate >= DateTime.Today);

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

        // POST: Guard/ValidateGuardByUsername
        [HttpPost]
        public JsonResult ValidateGuardByUsername(string siteUsername)
        {
            try
            {
                if (string.IsNullOrEmpty(siteUsername))
                {
                    return Json(new { isValid = false, message = "Site username is required" });
                }

                // Check if guard exists with the provided site username
                var guard = db.Guards.FirstOrDefault(g =>
                    g.SiteUsername.Equals(siteUsername, StringComparison.OrdinalIgnoreCase) &&
                    g.IsActive);

                if (guard != null)
                {
                    return Json(new
                    {
                        isValid = true,
                        guardData = new
                        {
                            guardId = guard.GuardId,
                            fullName = guard.Guard_FName + " " + guard.Guard_LName,
                            siteUsername = guard.SiteUsername,
                            email = guard.Email,
                            phoneNumber = guard.PhoneNumber
                        },
                        message = "Guard validated successfully"
                    });
                }
                else
                {
                    return Json(new
                    {
                        isValid = false,
                        message = "Site username not recognized"
                    });
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error in ValidateGuardByUsername: {ex.Message}");
                return Json(new
                {
                    isValid = false,
                    message = "An error occurred while validating guard"
                });
            }
        }

        // POST: Guard/SaveCheckIn
        [HttpPost]
        public JsonResult SaveCheckIn(GuardCheckInViewModel checkInData)
        {
            try
            {
                if (checkInData == null || checkInData.GuardId <= 0)
                {
                    return Json(new { success = false, message = "Invalid check-in data" });
                }

                // Verify the guard exists and is active
                var guard = db.Guards.FirstOrDefault(g => g.GuardId == checkInData.GuardId && g.IsActive);
                if (guard == null)
                {
                    return Json(new { success = false, message = "Guard not found or inactive" });
                }

                // Create a new check-in record using your GuardCheckIn model
                var checkIn = new GuardCheckIn
                {
                    GuardId = checkInData.GuardId,
                    CheckInTime = DateTime.Now, // Use current datetime
                    Status = checkInData.Status,
                    CreatedDate = DateTime.Now,
                    IsLate = checkInData.IsLate,
                    ExpectedTime = checkInData.ExpectedTime,
                    ActualTime = checkInData.ActualTime,
                    SiteUsername = checkInData.SiteUsername
                };

                db.GuardCheckIns.Add(checkIn);
                db.SaveChanges();

                // Create a notification for the check-in
                var currentUserId = User.Identity.GetUserId();
                var notification = new Notification
                {
                    GuardId = checkInData.GuardId,
                    UserId = currentUserId,
                    UserType = "Guard",
                    Title = "Check-in Recorded",
                    Message = $"{guard.Guard_FName} {guard.Guard_LName} has {checkInData.Status.ToLower()} at {checkInData.ActualTime}",
                    NotificationType = "CheckIn",
                    IsRead = false,
                    CreatedAt = DateTime.Now,
                    IsImportant = checkInData.IsLate
                };

                db.Notifications.Add(notification);
                db.SaveChanges();

                return Json(new { success = true, message = "Check-in recorded successfully" });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error in SaveCheckIn: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"Stack Trace: {ex.StackTrace}");
                return Json(new { success = false, message = $"An error occurred while saving check-in: {ex.Message}" });
            }
        }

        // GET: Guard/GetTodayCheckIns
        [HttpGet]
        public JsonResult GetTodayCheckIns(int guardId, string date)
        {
            try
            {
                if (guardId <= 0)
                {
                    return Json(new { success = false, message = "Invalid guard ID" }, JsonRequestBehavior.AllowGet);
                }

                DateTime targetDate;
                if (!DateTime.TryParse(date, out targetDate))
                {
                    targetDate = DateTime.Today;
                }

                var checkIns = db.GuardCheckIns
                    .Where(c => c.GuardId == guardId &&
                               DbFunctions.TruncateTime(c.CheckInTime) == targetDate.Date)
                    .OrderByDescending(c => c.CheckInTime)
                    .Select(c => new
                    {
                        id = c.CheckInId,
                        guardId = c.GuardId,
                        time = c.CheckInTime,
                        status = c.Status,
                        date = c.CheckInTime.Date,
                        isLate = c.IsLate,
                        expectedTime = c.ExpectedTime,
                        actualTime = c.ActualTime
                    })
                    .ToList();

                return Json(new { success = true, checkins = checkIns }, JsonRequestBehavior.AllowGet);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error in GetTodayCheckIns: {ex.Message}");
                return Json(new { success = false, message = "Error retrieving check-ins" }, JsonRequestBehavior.AllowGet);
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

        
        // GET: Guard/Calendar
        public ActionResult Calendar()
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

                // Calculate dates outside the LINQ query
                DateTime today = DateTime.Today;
                DateTime sevenDaysAgo = today.AddDays(-7);

                // Get upcoming shifts - use calculated date variable
                var shifts = db.ShiftRosters
                    .Where(s => s.GuardId == guard.GuardId && s.RosterDate >= today)
                    .OrderBy(s => s.RosterDate)
                    .ToList();

                // Get check-ins for these shifts - use calculated date variable
                var checkIns = db.GuardCheckIns
                    .Where(c => c.GuardId == guard.GuardId && c.CheckInTime >= sevenDaysAgo)
                    .OrderByDescending(c => c.CheckInTime)
                    .ToList();

                // Create view model
                var viewModel = new GuardCalendarViewModel
                {
                    GuardId = guard.GuardId,
                    GuardName = guard.Guard_FName + " " + guard.Guard_LName,
                    Shifts = shifts.Select(s => new CalendarShift
                    {
                        ShiftId = s.RosterId,
                        ShiftDate = s.RosterDate,
                        ShiftType = s.ShiftType,
                        StartTime = GetShiftStartTime(s.ShiftType),
                        EndTime = GetShiftEndTime(s.ShiftType),
                        Location = s.Location ?? "Main Gate",
                        Status = s.Status ?? "Scheduled",
                        HasCheckIn = checkIns.Any(c => c.GuardId == guard.GuardId &&
                                                     c.CheckInTime.Date == s.RosterDate.Date &&
                                                     c.Status == "Present"),
                        HasCheckOut = checkIns.Any(c => c.GuardId == guard.GuardId &&
                                                      c.CheckInTime.Date == s.RosterDate.Date &&
                                                      c.Status == "Checked Out"),
                        IsToday = s.RosterDate.Date == today
                    }).ToList(),
                    CheckIns = checkIns
                };

                return View("~/Views/Guard/Calendar.cshtml", viewModel);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"ERROR in Calendar: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"StackTrace: {ex.StackTrace}");

                TempData["ErrorMessage"] = $"An error occurred while loading the calendar. Please try again.";
                return RedirectToAction("Dashboard");
            }
        }

        // POST: Guard/CheckIn
        [HttpPost]
        public JsonResult CheckIn(int RosterId)
        {
            try
            {
                var currentUserId = User.Identity.GetUserId();
                var guard = db.Guards.FirstOrDefault(g => g.UserId == currentUserId);

                if (guard == null)
                    return Json(new { success = false, message = "Guard not found" });

                var shift = db.ShiftRosters.FirstOrDefault(s => s.RosterId == RosterId && s.GuardId == guard.GuardId);

                if (shift == null)
                    return Json(new { success = false, message = "Shift not found" });

                // derive expected time based on shift type
                string expectedTime = shift.ShiftType == "Day" ? "06:00" :
                                      shift.ShiftType == "Night" ? "18:00" : null;

                if (expectedTime == null)
                    return Json(new { success = false, message = "Off-duty shift. No check-in required." });

                string actualTime = DateTime.Now.ToString("HH:mm");
                bool isLate = TimeSpan.Parse(actualTime) > TimeSpan.Parse(expectedTime);

                // Create check-in record
                var checkIn = new GuardCheckIn
                {
                    GuardId = guard.GuardId,
                    CheckInTime = DateTime.Now,
                    Status = isLate ? "Late Arrival" : "Present",
                    CreatedDate = DateTime.Now,
                    IsLate = isLate,
                    ExpectedTime = expectedTime,
                    ActualTime = actualTime,
                    SiteUsername = guard.SiteUsername
                };

                db.GuardCheckIns.Add(checkIn);

                // Update shift status
                shift.Status = "In Progress";
                db.SaveChanges();

                // Notification
                var notification = new Notification
                {
                    GuardId = guard.GuardId,
                    UserId = currentUserId,
                    UserType = "Guard",
                    Title = "Check-In Successful",
                    Message = $"You have checked in for your {shift.ShiftType} shift at {shift.Location}.",
                    NotificationType = "CheckIn",
                    IsRead = false,
                    CreatedAt = DateTime.Now,
                    IsImportant = true
                };

                db.Notifications.Add(notification);
                db.SaveChanges();

                return Json(new { success = true, message = isLate ? "Checked in (Late Arrival)" : "Check-in successful!" });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error in CheckIn: {ex.Message}");
                return Json(new { success = false, message = "Error during check-in" });
            }
        }

        // POST: Guard/CheckOut
        [HttpPost]
        public JsonResult CheckOut(int RosterId)
        {
            try
            {
                var currentUserId = User.Identity.GetUserId();
                var guard = db.Guards.FirstOrDefault(g => g.UserId == currentUserId);

                if (guard == null)
                    return Json(new { success = false, message = "Guard not found" });

                var shift = db.ShiftRosters.FirstOrDefault(s => s.RosterId == RosterId && s.GuardId == guard.GuardId);

                if (shift == null)
                    return Json(new { success = false, message = "Shift not found" });

                // derive expected checkout time
                string expectedTime = shift.ShiftType == "Day" ? "18:00" :
                                      shift.ShiftType == "Night" ? "06:00" : null;

                if (expectedTime == null)
                    return Json(new { success = false, message = "Off-duty shift. No check-out required." });

                string actualTime = DateTime.Now.ToString("HH:mm");
                bool isLate = TimeSpan.Parse(actualTime) > TimeSpan.Parse(expectedTime);

                var checkOut = new GuardCheckIn
                {
                    GuardId = guard.GuardId,
                    CheckInTime = DateTime.Now,
                    Status = isLate ? "Late Departure" : "Checked Out",
                    CreatedDate = DateTime.Now,
                    IsLate = isLate,
                    ExpectedTime = expectedTime,
                    ActualTime = actualTime,
                    SiteUsername = guard.SiteUsername
                };

                db.GuardCheckIns.Add(checkOut);

                // Update shift status
                shift.Status = "Completed";
                db.SaveChanges();

                var notification = new Notification
                {
                    GuardId = guard.GuardId,
                    UserId = currentUserId,
                    UserType = "Guard",
                    Title = "Check-Out Successful",
                    Message = $"You have successfully checked out from your {shift.ShiftType} shift at {shift.Location}.",
                    NotificationType = "CheckOut",
                    IsRead = false,
                    CreatedAt = DateTime.Now,
                    IsImportant = false
                };

                db.Notifications.Add(notification);
                db.SaveChanges();

                return Json(new { success = true, message = isLate ? "Checked out (Late Departure)" : "Check-out successful!" });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error in CheckOut: {ex.Message}");
                return Json(new { success = false, message = "Error during check-out" });
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

       

        // Helper methods for shift times
        private string GetShiftStartTime(string shiftType)
        {
            switch (shiftType)
            {
                case "Night": return "18:00";
                case "Day": return "06:00";
                default: return "00:00";
            }
        }

        private string GetShiftEndTime(string shiftType)
        {
            switch (shiftType)
            {
                case "Night": return "06:00";
                case "Day": return "18:00";
                default: return "00:00";
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