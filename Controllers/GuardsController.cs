using Microsoft.AspNet.Identity;
using PiranaSecuritySystem.Models;
using PiranaSecuritySystem.ViewModels;
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
                    ViewBag.ErrorMessage = "User not authenticated. Please log in again.";
                    return View("Error");
                }

                var guard = db.Guards.FirstOrDefault(g => g.UserId == currentUserId);

                if (guard == null)
                {
                    ViewBag.ErrorMessage = "Guard profile not found. Please contact administrator.";
                    return View("ProfileNotFound");
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

                return View(guard);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error in Dashboard: {ex.Message}");
                ViewBag.ErrorMessage = "An error occurred while loading the dashboard.";
                return View("Error");
            }
        }

        // GET: Guard/Calendar
        public ActionResult Calendar(int? year, int? month)
        {
            try
            {
                var currentUserId = User.Identity.GetUserId();

                if (string.IsNullOrEmpty(currentUserId))
                {
                    ViewBag.ErrorMessage = "User not authenticated. Please log in again.";
                    return View("Error");
                }

                var guard = db.Guards.FirstOrDefault(g => g.UserId == currentUserId);

                if (guard == null)
                {
                    ViewBag.ErrorMessage = "Guard profile not found. Please contact administrator.";
                    return View("ProfileNotFound");
                }

                // Set current year/month if not provided
                var currentDate = DateTime.Now;
                int viewYear = year ?? currentDate.Year;
                int viewMonth = month ?? currentDate.Month;

                // Get the calendar data
                var calendarData = GetGuardCalendarData(guard.GuardId, viewYear, viewMonth);

                if (calendarData == null)
                {
                    ViewBag.ErrorMessage = "Could not load calendar data.";
                    return View("Error");
                }

                ViewBag.Year = viewYear;
                ViewBag.Month = viewMonth;
                ViewBag.MonthName = new DateTime(viewYear, viewMonth, 1).ToString("MMMM");
                ViewBag.PrevMonth = viewMonth == 1 ? new { year = viewYear - 1, month = 12 } : new { year = viewYear, month = viewMonth - 1 };
                ViewBag.NextMonth = viewMonth == 12 ? new { year = viewYear + 1, month = 1 } : new { year = viewYear, month = viewMonth + 1 };

                return View(calendarData);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error in Calendar: {ex.Message}");
                ViewBag.ErrorMessage = "An error occurred while loading the calendar.";
                return View("Error");
            }
        }

        private GuardCalendarViewModel GetGuardCalendarData(int guardId, int year, int month)
        {
            try
            {
                var guard = db.Guards.Find(guardId);
                if (guard == null) return null;

                var viewModel = new GuardCalendarViewModel
                {
                    GuardId = guardId,
                    GuardName = $"{guard.Guard_FName} {guard.Guard_LName}",
                    Year = year,
                    Month = month,
                    Days = new List<CalendarDay>()
                };

                // Get the first day of the month
                var firstDayOfMonth = new DateTime(year, month, 1);

                // Get the last day of the month
                var lastDayOfMonth = firstDayOfMonth.AddMonths(1).AddDays(-1);

                // Get all shifts for this guard in the month
                var shifts = db.Shifts
                    .Where(s => s.GuardId == guardId &&
                               s.ShiftDate >= firstDayOfMonth &&
                               s.ShiftDate <= lastDayOfMonth)
                    .ToList();

                // Get all check-ins for this guard in the month
                var checkIns = db.GuardCheckIns
                    .Where(c => c.GuardId == guardId &&
                               c.CheckInTime >= firstDayOfMonth &&
                               c.CheckInTime < lastDayOfMonth.AddDays(1))
                    .ToList();

                // Create calendar days
                for (var day = firstDayOfMonth; day <= lastDayOfMonth; day = day.AddDays(1))
                {
                    var shift = shifts.FirstOrDefault(s => s.ShiftDate.Date == day.Date);
                    var shiftType = shift?.ShiftType ?? "Not Scheduled";

                    // Check if guard checked in on this day
                    var hasCheckIn = checkIns.Any(c => c.CheckInTime.Date == day.Date && c.Status == "Present");
                    var hasCheckOut = checkIns.Any(c => c.CheckInTime.Date == day.Date && c.Status == "Checked Out");

                    string status;
                    if (shiftType == "Off")
                    {
                        status = "OffDuty";
                    }
                    else if (hasCheckIn && hasCheckOut)
                    {
                        status = "CheckedIn";
                    }
                    else if (day.Date < DateTime.Today && shiftType != "Off" && !hasCheckIn)
                    {
                        status = "NotCheckedIn";
                    }
                    else
                    {
                        status = "Normal";
                    }

                    viewModel.Days.Add(new CalendarDay
                    {
                        Date = day,
                        DayOfWeek = day.DayOfWeek.ToString(),
                        ShiftType = shiftType,
                        Status = status,
                        HasCheckIn = hasCheckIn,
                        IsToday = day.Date == DateTime.Today
                    });
                }

                return viewModel;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error in GetGuardCalendarData: {ex.Message}");
                return null;
            }
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

                var guard = db.Guards.Find(guardId);
                if (guard == null)
                {
                    return Json(new { success = false, message = "Guard not found" });
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

                // Create notification for director
                CreateGuardCheckInNotification(guardId, $"{guard.Guard_FName} {guard.Guard_LName}", status);

                return Json(new { success = true, message = "Check-in recorded successfully" });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error in SaveCheckIn: {ex.Message}");
                return Json(new { success = false, message = "An error occurred while saving check-in" });
            }
        }

        private void CreateGuardCheckInNotification(int guardId, string guardName, string status)
        {
            try
            {
                var notification = new Notification
                {
                    Title = "Guard Check-in",
                    Message = $"{guardName} has {status.ToLower()} for duty at {DateTime.Now:HH:mm}",
                    NotificationType = "Checkin",
                    RelatedUrl = Url.Action("GuardLogs", "Director"),
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
                System.Diagnostics.Debug.WriteLine($"Error creating guard check-in notification: {ex.Message}");
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