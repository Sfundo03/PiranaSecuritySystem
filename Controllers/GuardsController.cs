using Microsoft.AspNet.Identity;
using PiranaSecuritySystem.Models;
using PiranaSecuritySystem.ViewModels;
using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using System.Web;
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
                ViewBag.TotalIncidents = db.IncidentReports.Count(r => r.GuardId == guard.GuardId);
                ViewBag.PendingIncidents = db.IncidentReports.Count(r => r.GuardId == guard.GuardId && r.Status == "Pending Review");
                ViewBag.ResolvedIncidents = db.IncidentReports.Count(r => r.GuardId == guard.GuardId && r.Status == "Resolved");
                ViewBag.UpcomingShifts = db.ShiftRosters.Count(s => s.GuardId == guard.GuardId && s.RosterDate >= DateTime.Today);

                // Get recent incidents for dashboard using PROJECTION to avoid circular references
                var recentIncidents = db.IncidentReports
                    .Where(r => r.GuardId == guard.GuardId)
                    .OrderByDescending(r => r.ReportDate)
                    .Take(5)
                    .Select(r => new // Using anonymous type projection
                    {
                        r.IncidentReportId,
                        r.ReportDate,
                        r.IncidentType,
                        r.Location,
                        r.Status,
                        r.Description,
                        r.Priority,
                        // Only include scalar properties, avoid navigation properties
                        // Don't include: r.Guard, r.Resident, etc.
                    })
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
                System.Diagnostics.Debug.WriteLine($"ValidateGuardByUsername called with: {siteUsername}");

                if (string.IsNullOrEmpty(siteUsername))
                {
                    return Json(new { isValid = false, message = "Site username is required" });
                }

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

        // GET: Guard/GetTodaysShift
        [HttpGet]
        public JsonResult GetTodaysShift(int guardId, string date)
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

                var shift = db.ShiftRosters
                    .FirstOrDefault(s => s.GuardId == guardId &&
                                       DbFunctions.TruncateTime(s.RosterDate) == targetDate.Date);

                if (shift != null)
                {
                    return Json(new
                    {
                        success = true,
                        shift = new
                        {
                            rosterId = shift.RosterId,
                            shiftType = shift.ShiftType,
                            location = shift.Location ?? "Main Gate",
                            date = shift.RosterDate,
                            status = shift.Status ?? "Scheduled"
                        }
                    }, JsonRequestBehavior.AllowGet);
                }
                else
                {

                    return Json(new { success = true, shift = (object)null }, JsonRequestBehavior.AllowGet);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error in GetTodaysShift: {ex.Message}");
                return Json(new { success = false, message = "Error retrieving shift data" }, JsonRequestBehavior.AllowGet);
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

                // Validate if guard is off duty
                if (checkInData.RosterId.HasValue)
                {
                    var shift = db.ShiftRosters.Find(checkInData.RosterId.Value);
                    if (shift != null && shift.ShiftType == "Off")
                    {
                        return Json(new { success = false, message = "You are off duty today. Cannot check in/out." });
                    }
                }

                DateTime today = DateTime.Today;

                // REMOVED: The problematic query that was causing the error
                // We don't need to check existing records since we removed validation restrictions

                // Create a new check-in/out record
                var checkIn = new GuardCheckIn
                {
                    GuardId = checkInData.GuardId,
                    CheckInTime = DateTime.Now,
                    Status = checkInData.Status,
                    CreatedDate = DateTime.Now,
                    IsLate = checkInData.IsLate,
                    ExpectedTime = checkInData.ExpectedTime,
                    ActualTime = checkInData.ActualTime,
                    SiteUsername = checkInData.SiteUsername,
                    RosterId = checkInData.RosterId
                };

                db.GuardCheckIns.Add(checkIn);
                db.SaveChanges(); // Save to get the CheckInId

                // Create or update attendance record
                UpdateAttendanceRecord(checkIn);

                // Update shift status if RosterId is provided
                if (checkInData.RosterId.HasValue)
                {
                    var shift = db.ShiftRosters.Find(checkInData.RosterId.Value);
                    if (shift != null)
                    {
                        if (checkInData.Status == "Present" || checkInData.Status == "Late Arrival")
                        {
                            shift.Status = "In Progress";
                        }
                        else if (checkInData.Status == "Checked Out" || checkInData.Status == "Late Departure")
                        {
                            shift.Status = "Completed";
                        }
                        db.SaveChanges(); // Save shift status changes
                    }
                }

                // Create notification for check-in/check-out
                CreateGuardNotification(
                    guard.GuardId,
                    checkInData.Status == "Present" || checkInData.Status == "Late Arrival" ? "Check-In Successful" : "Check-Out Successful",
                    $"You have {checkInData.Status.ToLower()} at {DateTime.Now:MMM dd, yyyy 'at' hh:mm tt}",
                    checkInData.Status.Contains("Check") ? "CheckOut" : "CheckIn",
                    false
                );

                return Json(new { success = true, message = $"{checkInData.Status} recorded successfully" });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error in SaveCheckIn: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"Stack Trace: {ex.StackTrace}");
                return Json(new { success = false, message = $"An error occurred while saving check-in: {ex.Message}" });
            }
        }

        // Helper method to update attendance records
        private void UpdateAttendanceRecord(GuardCheckIn checkIn)
        {
            try
            {
                if (checkIn.Status == "Present" || checkIn.Status == "Late Arrival")
                {
                    // Create new attendance record for check-in
                    var attendance = new Attendance
                    {
                        GuardId = checkIn.GuardId,
                        CheckInTime = checkIn.CheckInTime,
                        AttendanceDate = checkIn.CheckInTime.Date,
                        CheckInId = checkIn.CheckInId,
                        HoursWorked = 0 // Will be updated on check-out
                    };
                    db.Attendances.Add(attendance);
                }
                else if (checkIn.Status == "Checked Out" || checkIn.Status == "Late Departure")
                {
                    // Find the corresponding check-in for today using DbFunctions
                    var checkInRecord = db.GuardCheckIns
                        .Where(c => c.GuardId == checkIn.GuardId &&
                                   DbFunctions.TruncateTime(c.CheckInTime) == DbFunctions.TruncateTime(checkIn.CheckInTime) &&
                                   (c.Status == "Present" || c.Status == "Late Arrival") &&
                                   c.CheckInTime < checkIn.CheckInTime)
                        .OrderByDescending(c => c.CheckInTime)
                        .FirstOrDefault();

                    if (checkInRecord != null)
                    {
                        // Find or create attendance record
                        var attendance = db.Attendances
                            .FirstOrDefault(a => a.CheckInId == checkInRecord.CheckInId) ??
                            new Attendance
                            {
                                GuardId = checkIn.GuardId,
                                CheckInTime = checkInRecord.CheckInTime,
                                AttendanceDate = checkInRecord.CheckInTime.Date,
                                CheckInId = checkInRecord.CheckInId
                            };

                        // Update check-out time and calculate hours
                        attendance.CheckOutTime = checkIn.CheckInTime;
                        attendance.HoursWorked = CalculateHoursWorked(attendance.CheckInTime, checkIn.CheckInTime);

                        if (attendance.AttendanceId == 0)
                        {
                            db.Attendances.Add(attendance);
                        }
                    }
                    else
                    {
                        // Create a standalone check-out attendance record if no check-in found
                        var attendance = new Attendance
                        {
                            GuardId = checkIn.GuardId,
                            CheckInTime = checkIn.CheckInTime.AddHours(-1), // Default 1 hour before checkout
                            CheckOutTime = checkIn.CheckInTime,
                            AttendanceDate = checkIn.CheckInTime.Date,
                            CheckInId = checkIn.CheckInId,
                            HoursWorked = 1.0 // Default 1 hour
                        };
                        db.Attendances.Add(attendance);
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error in UpdateAttendanceRecord: {ex.Message}");
                // Don't throw, just log the error - we don't want to fail the main check-in/out operation
            }
        }

        // Helper method to calculate hours worked
        private double CalculateHoursWorked(DateTime checkInTime, DateTime checkOutTime)
        {
            if (checkOutTime <= checkInTime)
                return 0;

            TimeSpan timeWorked = checkOutTime - checkInTime;
            return Math.Round(timeWorked.TotalHours, 2);
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

                DateTime today = DateTime.Today;
                DateTime sevenDaysAgo = today.AddDays(-7);

                var shifts = db.ShiftRosters
                    .Where(s => s.GuardId == guard.GuardId && s.RosterDate >= today)
                    .OrderBy(s => s.RosterDate)
                    .ToList();

                var checkIns = db.GuardCheckIns
                    .Where(c => c.GuardId == guard.GuardId && c.CheckInTime >= sevenDaysAgo)
                    .OrderByDescending(c => c.CheckInTime)
                    .ToList();

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
                        Location = (string)(s.Location ?? "Main Gate"),
                        Status = s.Status ?? "Scheduled",
                        HasCheckIn = checkIns.Any(c => c.RosterId == s.RosterId && (c.Status == "Present" || c.Status == "Late Arrival")),
                        HasCheckOut = checkIns.Any(c => c.RosterId == s.RosterId && (c.Status == "Checked Out" || c.Status == "Late Departure")),
                        IsToday = s.RosterDate.Date == today,
                        RosterId = s.RosterId
                    }).ToList(),
                    CheckIns = checkIns
                };

                return View("~/Views/Guard/Calendar.cshtml", viewModel);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"ERROR in Calendar: {ex.Message}");
                TempData["ErrorMessage"] = $"An error occurred while loading the calendar. Please try again.";
                return RedirectToAction("Dashboard");
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
                        date = c.CheckInTime,
                        isLate = c.IsLate,
                        expectedTime = c.ExpectedTime,
                        actualTime = c.ActualTime,
                        rosterId = c.RosterId
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

        // GET: Guard/GetGuardAttendanceStatus
        [HttpGet]
        public JsonResult GetGuardAttendanceStatus(int guardId)
        {
            try
            {
                if (guardId <= 0)
                {
                    return Json(new { success = false, message = "Invalid guard ID" }, JsonRequestBehavior.AllowGet);
                }

                var today = DateTime.Today;
                var shifts = db.ShiftRosters
                    .Where(s => s.GuardId == guardId && s.RosterDate >= today.AddDays(-7) && s.RosterDate <= today.AddDays(30))
                    .OrderBy(s => s.RosterDate)
                    .ToList();

                var checkIns = db.GuardCheckIns
                    .Where(c => c.GuardId == guardId && c.CheckInTime >= today.AddDays(-7))
                    .ToList();

                var attendanceStatus = shifts.Select(shift => new
                {
                    date = shift.RosterDate,
                    shiftType = shift.ShiftType,
                    location = shift.Location ?? "Main Gate",
                    status = shift.Status ?? "Scheduled",
                    checkIn = checkIns.FirstOrDefault(c => c.RosterId == shift.RosterId &&
                                                         (c.Status == "Present" || c.Status == "Late Arrival")),
                    checkOut = checkIns.FirstOrDefault(c => c.RosterId == shift.RosterId &&
                                                          (c.Status == "Checked Out" || c.Status == "Late Departure"))
                }).ToList();

                return Json(new { success = true, attendance = attendanceStatus }, JsonRequestBehavior.AllowGet);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error in GetGuardAttendanceStatus: {ex.Message}");
                return Json(new { success = false, message = "Error retrieving attendance status" }, JsonRequestBehavior.AllowGet);
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
                    GuardId = guard.GuardId, // Use GuardId instead of ResidentId
                    ResidentId = null, // Set ResidentId to null
                    ReportDate = DateTime.Now,
                    Status = "Pending Review",
                    CreatedBy = guard.FullName,
                    ReportedBy = "Guard" // Indicate this is a guard report
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
                    // Set properties for guard-reported incident
                    incidentReport.GuardId = guard.GuardId; // Use GuardId instead of ResidentId
                    incidentReport.ResidentId = null; // Set ResidentId to null for guard incidents
                    incidentReport.ReportDate = DateTime.Now;
                    incidentReport.Status = "Pending Review";
                    incidentReport.CreatedBy = guard.FullName;
                    incidentReport.CreatedDate = DateTime.Now;
                    incidentReport.ReportedBy = "Guard"; // Indicate this was reported by a guard

                    // Set default values for required fields
                    if (string.IsNullOrEmpty(incidentReport.IncidentType))
                        incidentReport.IncidentType = "Other";

                    if (string.IsNullOrEmpty(incidentReport.Priority))
                        incidentReport.Priority = "Medium";

                    if (string.IsNullOrEmpty(incidentReport.Location))
                        incidentReport.Location = "Main Gate";

                    db.IncidentReports.Add(incidentReport);
                    db.SaveChanges();

                    // Create notification for guard about the filed incident
                    CreateGuardNotification(
                        guard.GuardId,
                        "Incident Report Filed",
                        $"You have successfully filed a {incidentReport.IncidentType.ToLower()} incident report at {incidentReport.Location} on {DateTime.Now.ToString("MMM dd, yyyy 'at' hh:mm tt")}",
                        "Incident",
                        true
                    );

                    // Create notification for directors about new incident from guard
                    var directors = db.Directors.Where(d => d.IsActive).ToList();
                    foreach (var director in directors)
                    {
                        var directorNotification = new Notification
                        {
                            UserId = director.DirectorId.ToString(),
                            UserType = "Director",
                            Title = "New Incident Reported by Guard",
                            Message = $"A new {incidentReport.IncidentType} incident has been filed by Guard {guard.Guard_FName} {guard.Guard_LName} (ID: {guard.GuardId}) at {incidentReport.Location} on {DateTime.Now.ToString("MMM dd, yyyy 'at' hh:mm tt")}",
                            IsRead = false,
                            CreatedAt = DateTime.Now,
                            RelatedUrl = Url.Action("IncidentDetails", "Director", new { id = incidentReport.IncidentReportId }),
                            NotificationType = "Incident",
                            IsImportant = incidentReport.Priority == "High" || incidentReport.Priority == "Critical",
                            PriorityLevel = incidentReport.Priority == "Critical" ? 4 : incidentReport.Priority == "High" ? 3 : 2
                        };
                        db.Notifications.Add(directorNotification);
                    }

                    db.SaveChanges();

                    TempData["SuccessMessage"] = "Incident reported successfully!";
                    return RedirectToAction("Dashboard");
                }
                else
                {
                    // Log validation errors
                    foreach (var error in ModelState.Values.SelectMany(v => v.Errors))
                    {
                        System.Diagnostics.Debug.WriteLine($"Validation Error: {error.ErrorMessage}");
                    }

                    TempData["ErrorMessage"] = "Please fix the validation errors below.";
                }

                // Repopulate dropdowns
                ViewBag.IncidentTypes = GetIncidentTypes();
                ViewBag.Locations = GetLocations();
                ViewBag.PriorityLevels = GetPriorityLevels();

                return View("~/Views/Guard/Create.cshtml", incidentReport);
            }
            catch (System.Data.Entity.Infrastructure.DbUpdateException dbUpdateEx)
            {
                // Get the inner exception for more details
                var innerException = dbUpdateEx.InnerException;
                string detailedError = innerException?.Message ?? dbUpdateEx.Message;

                System.Diagnostics.Debug.WriteLine($"DbUpdateException: {detailedError}");

                TempData["ErrorMessage"] = $"Database error: {detailedError}";

                // Repopulate dropdowns
                ViewBag.IncidentTypes = GetIncidentTypes();
                ViewBag.Locations = GetLocations();
                ViewBag.PriorityLevels = GetPriorityLevels();

                return View("~/Views/Guard/Create.cshtml", incidentReport);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error in Create (POST): {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"Stack Trace: {ex.StackTrace}");

                TempData["ErrorMessage"] = $"An error occurred: {ex.Message}";

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

                // Filter by GuardId instead of ResidentId
                var incidents = db.IncidentReports
                    .Where(r => r.GuardId == guard.GuardId) // Changed from ResidentId to GuardId
                    .OrderByDescending(r => r.ReportDate)
                    .ToList();

                return View("~/Views/Guard/MyIncidentReports.cshtml", incidents);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error in MyIncidentReports: {ex.Message}");
                TempData["ErrorMessage"] = "An error occurred while loading your incident reports.";
                return RedirectToAction("Dashboard");
            }
        }

        // GET: Guard/Details
        public ActionResult Details(int id)
        {
            try
            {
                var currentUserId = User.Identity.GetUserId();
                var guard = db.Guards.FirstOrDefault(g => g.UserId == currentUserId);

                if (guard == null)
                {
                    TempData["ErrorMessage"] = "Guard profile not found.";
                    return RedirectToAction("Dashboard");
                }

                var incident = db.IncidentReports.FirstOrDefault(i => i.IncidentReportId == id && i.GuardId == guard.GuardId);

                if (incident == null)
                {
                    TempData["ErrorMessage"] = "Incident not found or you don't have permission to view it.";
                    return RedirectToAction("MyIncidentReports");
                }

                return View("~/Views/Guard/Details.cshtml", incident);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error in Details: {ex.Message}");
                TempData["ErrorMessage"] = "An error occurred while loading incident details.";
                return RedirectToAction("MyIncidentReports");
            }
        }

        // GET: Guard/DownloadFeedbackAttachment
        public ActionResult DownloadFeedbackAttachment(int id)
        {
            try
            {
                var currentUserId = User.Identity.GetUserId();
                var guard = db.Guards.FirstOrDefault(g => g.UserId == currentUserId);

                if (guard == null)
                {
                    TempData["ErrorMessage"] = "Guard profile not found.";
                    return RedirectToAction("Dashboard");
                }

                var incident = db.IncidentReports.FirstOrDefault(i => i.IncidentReportId == id && i.GuardId == guard.GuardId);

                if (incident == null)
                {
                    TempData["ErrorMessage"] = "Incident not found or you don't have permission to access it.";
                    return RedirectToAction("MyIncidentReports");
                }

                // Check if there's a file attachment
                if (!string.IsNullOrEmpty(incident.FeedbackFileData))
                {
                    // Handle base64 encoded file data
                    byte[] fileBytes = Convert.FromBase64String(incident.FeedbackFileData);
                    string fileName = incident.FeedbackFileName ?? $"Feedback_Incident_{id}.pdf";
                    string contentType = incident.FeedbackFileType ?? "application/pdf";

                    return File(fileBytes, contentType, fileName);
                }
                else if (!string.IsNullOrEmpty(incident.FeedbackAttachment))
                {
                    // Handle file path attachment (legacy support)
                    string filePath = Server.MapPath(incident.FeedbackAttachment);
                    if (System.IO.File.Exists(filePath))
                    {
                        string fileName = System.IO.Path.GetFileName(filePath);
                        byte[] fileBytes = System.IO.File.ReadAllBytes(filePath);
                        string contentType = MimeMapping.GetMimeMapping(fileName);

                        return File(fileBytes, contentType, fileName);
                    }
                    else
                    {
                        TempData["ErrorMessage"] = "Attachment file not found.";
                        return RedirectToAction("Details", new { id = id });
                    }
                }
                else
                {
                    TempData["ErrorMessage"] = "No attachment available for this incident.";
                    return RedirectToAction("Details", new { id = id });
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error in DownloadFeedbackAttachment: {ex.Message}");
                TempData["ErrorMessage"] = "An error occurred while downloading the attachment.";
                return RedirectToAction("Details", new { id = id });
            }
        }

        // GET: Guard/GetIncidentFeedback
        [HttpGet]
        public JsonResult GetIncidentFeedback(int id)
        {
            try
            {
                var currentUserId = User.Identity.GetUserId();
                var guard = db.Guards.FirstOrDefault(g => g.UserId == currentUserId);

                if (guard == null)
                {
                    return Json(new { error = "Guard profile not found" }, JsonRequestBehavior.AllowGet);
                }

                var incident = db.IncidentReports.FirstOrDefault(i => i.IncidentReportId == id && i.GuardId == guard.GuardId);

                if (incident == null)
                {
                    return Json(new { error = "Incident not found" }, JsonRequestBehavior.AllowGet);
                }

                bool hasAttachment = !string.IsNullOrEmpty(incident.FeedbackFileData) || !string.IsNullOrEmpty(incident.FeedbackAttachment);

                return Json(new
                {
                    feedback = incident.Feedback,
                    hasAttachment = hasAttachment,
                    fileName = incident.FeedbackFileName,
                    fileSize = incident.FeedbackFileSize,
                    fileType = incident.FeedbackFileType,
                    feedbackDate = incident.FeedbackDate?.ToString("MMM dd, yyyy 'at' hh:mm tt")
                }, JsonRequestBehavior.AllowGet);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error in GetIncidentFeedback: {ex.Message}");
                return Json(new { error = "Error loading feedback" }, JsonRequestBehavior.AllowGet);
            }
        }

        // POST: Guard/UpdateIncidentStatus
        [HttpPost]
        public JsonResult UpdateIncidentStatus(int incidentId, string newStatus)
        {
            try
            {
                var currentUserId = User.Identity.GetUserId();
                var guard = db.Guards.FirstOrDefault(g => g.UserId == currentUserId);

                if (guard == null)
                {
                    return Json(new { success = false, message = "Guard not found" });
                }

                var incident = db.IncidentReports.FirstOrDefault(i => i.IncidentReportId == incidentId && i.GuardId == guard.GuardId);

                if (incident == null)
                {
                    return Json(new { success = false, message = "Incident not found" });
                }

                string oldStatus = incident.Status;
                incident.Status = newStatus;
                db.SaveChanges();

                // Create notification for status change
                CreateGuardNotification(
                    guard.GuardId,
                    "Incident Status Updated",
                    $"Your incident report #{incident.IncidentReportId} ({incident.IncidentType}) status has been changed from '{oldStatus}' to '{newStatus}'",
                    "Incident",
                    true
                );

                return Json(new { success = true, message = "Incident status updated successfully" });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error in UpdateIncidentStatus: {ex.Message}");
                return Json(new { success = false, message = "Error updating incident status" });
            }
        }

        // GET: Guard/Attendance
        public ActionResult Attendance()
        {
            // Redirect to the static attendance page
            return Redirect("~/Content/Guard/Index.html");
        }

        // NEW: Get current logged-in guard information
        [HttpGet]
        public JsonResult GetCurrentGuardInfo()
        {
            try
            {
                var currentUserId = User.Identity.GetUserId();

                if (string.IsNullOrEmpty(currentUserId))
                {
                    return Json(new { success = false, message = "User not authenticated. Please log in again." }, JsonRequestBehavior.AllowGet);
                }

                var guard = db.Guards.FirstOrDefault(g => g.UserId == currentUserId && g.IsActive);

                if (guard == null)
                {
                    return Json(new { success = false, message = "Guard profile not found or inactive." }, JsonRequestBehavior.AllowGet);
                }

                return Json(new
                {
                    success = true,
                    guardData = new
                    {
                        guardId = guard.GuardId,
                        fullName = guard.Guard_FName + " " + guard.Guard_LName,
                        siteUsername = guard.SiteUsername,
                        email = guard.Email,
                        phoneNumber = guard.PhoneNumber
                    },
                    message = "Guard auto-validated successfully"
                }, JsonRequestBehavior.AllowGet);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error in GetCurrentGuardInfo: {ex.Message}");
                return Json(new
                {
                    success = false,
                    message = "An error occurred while auto-validating guard"
                }, JsonRequestBehavior.AllowGet);
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

                // Get notifications for the guard with proper time formatting
                var notifications = db.Notifications
                    .Where(n => n.GuardId == guard.GuardId)
                    .OrderByDescending(n => n.CreatedAt)
                    .ToList()
                    .Select(n => new NotificationViewModel
                    {
                        NotificationId = n.NotificationId,
                        Title = n.Title,
                        Message = FormatNotificationMessage(n.Message), // Format the message
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

                return View("~/Views/Guard/Notifications.cshtml", notifications);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error in Notifications: {ex.Message}");
                TempData["ErrorMessage"] = "An error occurred while loading notifications.";
                return RedirectToAction("Dashboard");
            }
        }


        [HttpGet]
        public JsonResult GetGuardTrainingSessions()
        {
            try
            {
                var currentUserId = User.Identity.GetUserId();
                var guard = db.Guards.FirstOrDefault(g => g.UserId == currentUserId);

                if (guard == null)
                {
                    return Json(new { success = false, message = "Guard not found" }, JsonRequestBehavior.AllowGet);
                }

                var trainingSessions = db.TrainingEnrollments
                    .Where(e => e.GuardId == guard.GuardId)
                    .Include(e => e.TrainingSession)
                    .Where(e => e.TrainingSession.StartDate >= DateTime.Now)
                    .OrderBy(e => e.TrainingSession.StartDate)
                    .Take(5)
                    .Select(e => new
                    {
                        id = e.TrainingSession.Id,
                        title = e.TrainingSession.Title,
                        startDate = e.TrainingSession.StartDate,
                        endDate = e.TrainingSession.EndDate,
                        site = e.TrainingSession.Site
                    })
                    .ToList()
                    .Select(t => new
                    {
                        id = t.id,
                        title = t.title,
                        startDate = t.startDate.ToString("yyyy-MM-ddTHH:mm:ss"), // ISO format for JavaScript
                        endDate = t.endDate.ToString("yyyy-MM-ddTHH:mm:ss"), // ISO format for JavaScript
                        site = t.site
                    })
                    .ToList();

                return Json(new { success = true, sessions = trainingSessions }, JsonRequestBehavior.AllowGet);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error in GetGuardTrainingSessions: {ex.Message}");
                return Json(new { success = false, message = "Error loading training sessions" }, JsonRequestBehavior.AllowGet);
            }
        }

        [HttpGet]
        public JsonResult GetRecentNotifications()
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
                    .Take(5)
                    .ToList()
                    .Select(n => new
                    {
                        notificationId = n.NotificationId,
                        title = n.Title,
                        message = FormatNotificationMessage(n.Message),
                        notificationType = n.NotificationType,
                        isRead = n.IsRead,
                        createdAt = n.CreatedAt,
                        timeAgo = GetTimeAgo(n.CreatedAt),
                        isImportant = n.IsImportant
                    })
                    .ToList();

                return Json(new { success = true, notifications = notifications }, JsonRequestBehavior.AllowGet);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error in GetRecentNotifications: {ex.Message}");
                return Json(new { success = false, message = "Error loading notifications" }, JsonRequestBehavior.AllowGet);
            }
        }

        // API: Get notifications for guard
        // Make sure your existing GetGuardNotifications method looks like this:
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
                        message = FormatNotificationMessage(n.Message),
                        notificationType = n.NotificationType,
                        isRead = n.IsRead,
                        createdAt = n.CreatedAt,
                        timeAgo = GetTimeAgo(n.CreatedAt),
                        isImportant = n.IsImportant
                    })
                    .ToList();

                return Json(new { success = true, notifications = notifications }, JsonRequestBehavior.AllowGet);
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
                    Message = FormatNotificationMessage(message), // Format the message
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

        // Helper method to create guard notifications
        private void CreateGuardNotification(int guardId, string title, string message, string notificationType, bool isImportant = false)
        {
            try
            {
                var currentUserId = User.Identity.GetUserId();
                var notification = new Notification
                {
                    GuardId = guardId,
                    UserId = currentUserId,
                    UserType = "Guard",
                    Title = title,
                    Message = FormatNotificationMessage(message),
                    NotificationType = notificationType,
                    IsRead = false,
                    CreatedAt = DateTime.Now,
                    IsImportant = isImportant,
                    PriorityLevel = isImportant ? 3 : 1
                };

                db.Notifications.Add(notification);
                db.SaveChanges();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error creating guard notification: {ex.Message}");
                // Don't throw, just log the error
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

        // Helper method to get time ago string (fixed to prevent NaN)
        private string GetTimeAgo(DateTime date)
        {
            try
            {
                if (date == DateTime.MinValue || date.Year <= 2000)
                    return "Just now";

                var timeSpan = DateTime.Now - date;

                if (timeSpan <= TimeSpan.FromSeconds(60))
                    return "Just now";
                else if (timeSpan <= TimeSpan.FromMinutes(60))
                    return $"{(int)timeSpan.TotalMinutes} minute{(timeSpan.TotalMinutes >= 2 ? "s" : "")} ago";
                else if (timeSpan <= TimeSpan.FromHours(24))
                    return $"{(int)timeSpan.TotalHours} hour{(timeSpan.TotalHours >= 2 ? "s" : "")} ago";
                else if (timeSpan <= TimeSpan.FromDays(7))
                    return $"{(int)timeSpan.TotalDays} day{(timeSpan.TotalDays >= 2 ? "s" : "")} ago";
                else if (timeSpan <= TimeSpan.FromDays(30))
                    return $"{(int)(timeSpan.TotalDays / 7)} week{((int)(timeSpan.TotalDays / 7) >= 2 ? "s" : "")} ago";
                else if (timeSpan <= TimeSpan.FromDays(365))
                    return $"{(int)(timeSpan.TotalDays / 30)} month{((int)(timeSpan.TotalDays / 30) >= 2 ? "s" : "")} ago";
                else
                    return date.ToString("MMM dd, yyyy");
            }
            catch
            {
                return "Just now";
            }
        }

        // Helper method to format notification messages
        private string FormatNotificationMessage(string message)
        {
            if (string.IsNullOrEmpty(message))
                return message;

            // Remove unwanted text like "CheckIn Important"
            message = message.Replace("CheckIn Important", "")
                            .Replace("CheckIn", "")
                            .Replace("Important", "")
                            .Trim();

            // Fix tense issues - ensure present tense
            message = message.Replace("checked in", "has checked in")
                            .Replace("arrived at", "has arrived at")
                            .Replace("reported", "has reported")
                            .Replace("completed", "has completed");

            // Remove any double spaces
            while (message.Contains("  "))
                message = message.Replace("  ", " ");

            return message.Trim();
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