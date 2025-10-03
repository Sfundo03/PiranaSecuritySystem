using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using System.Web.Mvc;
using PiranaSecuritySystem.Models;

namespace PiranaSecuritySystem.Controllers
{
    public class PayrollController : Controller
    {
        private ApplicationDbContext db = new ApplicationDbContext();

        // GET: Payroll
        [Authorize(Roles = "Admin")]
        public ActionResult Index()
        {
            var payrolls = db.Payrolls.Include(p => p.Guard).ToList();
            return View(payrolls);
        }

        // GET: Payroll/Generate
        [Authorize(Roles = "Admin")]
        public ActionResult Generate()
        {
            ViewBag.GuardId = new SelectList(db.Guards.Where(g => g.IsActive), "GuardId", "FullName");
            return View();
        }

        // POST: Payroll/Generate
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin")]
        public ActionResult Generate(DateTime payPeriodStart, DateTime payPeriodEnd, int guardId)
        {
            try
            {
                // Validate that pay period is within the same month
                if (payPeriodStart.Month != payPeriodEnd.Month || payPeriodStart.Year != payPeriodEnd.Year)
                {
                    ModelState.AddModelError("", "Pay period must be within the same month.");
                    ViewBag.GuardId = new SelectList(db.Guards.Where(g => g.IsActive), "GuardId", "FullName");
                    return View();
                }

                // Check if payroll already exists for this guard in the same month
                bool payrollExists = db.Payrolls.Any(p =>
                    p.GuardId == guardId &&
                    p.PayPeriodStart.Year == payPeriodStart.Year &&
                    p.PayPeriodStart.Month == payPeriodStart.Month);

                if (payrollExists)
                {
                    var existingPayroll = db.Payrolls
                        .Include(p => p.Guard)
                        .FirstOrDefault(p =>
                            p.GuardId == guardId &&
                            p.PayPeriodStart.Year == payPeriodStart.Year &&
                            p.PayPeriodStart.Month == payPeriodStart.Month);

                    var guardName = existingPayroll?.Guard?.FullName ?? "the guard";
                    ModelState.AddModelError("", $"Payroll for {guardName} already exists for {payPeriodStart:MMMM yyyy}. Please edit the existing payroll instead.");
                    ViewBag.GuardId = new SelectList(db.Guards.Where(g => g.IsActive), "GuardId", "FullName");
                    return View();
                }

                // Get guard information for error messages
                var guard = db.Guards.Find(guardId);
                if (guard == null)
                {
                    ModelState.AddModelError("", "Guard not found.");
                    ViewBag.GuardId = new SelectList(db.Guards.Where(g => g.IsActive), "GuardId", "FullName");
                    return View();
                }

                // Get guard's current ACTIVE rate
                var guardRate = db.GuardRates
                    .Where(r => r.GuardId == guardId && r.IsActive)
                    .OrderByDescending(r => r.EffectiveDate)
                    .FirstOrDefault();

                if (guardRate == null)
                {
                    ModelState.AddModelError("", $"No active rate found for {guard.FullName}. Please activate or create a new rate.");
                    ViewBag.GuardId = new SelectList(db.Guards.Where(g => g.IsActive), "GuardId", "FullName");
                    return View();
                }

                // Get attendance records for the period with proper hours calculation
                var attendances = GetAttendanceRecordsForPeriod(guardId, payPeriodStart, payPeriodEnd);

                double totalHours = attendances.Sum(a => a.HoursWorked);

                if (totalHours <= 0)
                {
                    ModelState.AddModelError("", $"No valid attendance records with hours worked found for {guard.FullName} in the selected period.");
                    ViewBag.GuardId = new SelectList(db.Guards.Where(g => g.IsActive), "GuardId", "FullName");
                    return View();
                }

                // Calculate pay
                decimal grossPay = (decimal)totalHours * guardRate.Rate;

                // Get active tax configuration
                var taxConfig = db.TaxConfigurations.FirstOrDefault(t => t.IsActive);

                // If no active tax config, create a default one
                if (taxConfig == null)
                {
                    taxConfig = new TaxConfiguration
                    {
                        TaxYear = DateTime.Now.Year,
                        TaxPercentage = 15, // 15% tax
                        TaxThreshold = 1, // R1 threshold - tax applies to all earnings
                        IsActive = true
                    };
                    db.TaxConfigurations.Add(taxConfig);
                    db.SaveChanges();
                }

                // Apply tax to ALL earnings (15% on gross pay)
                decimal taxAmount = grossPay * (taxConfig.TaxPercentage / 100);
                decimal netPay = grossPay - taxAmount;

                // Create payroll record
                var payroll = new Payroll
                {
                    GuardId = guardId,
                    PayPeriodStart = payPeriodStart,
                    PayPeriodEnd = payPeriodEnd,
                    TotalHours = totalHours,
                    HourlyRate = guardRate.Rate,
                    GrossPay = grossPay,
                    TaxAmount = taxAmount,
                    NetPay = netPay,
                    PayDate = DateTime.Now,
                    Status = "Processed",
                    PaymentMethod = "Bank Transfer" // Default value
                };

                db.Payrolls.Add(payroll);
                db.SaveChanges();

                // Notify director about payroll creation
                NotifyDirectorAboutPayroll(payroll.PayrollId, guard.FullName, totalHours, grossPay, taxAmount, netPay);

                TempData["SuccessMessage"] = $"Payroll for {guard.FullName} has been generated successfully! Total Hours: {totalHours:F2}, Gross Pay: R{grossPay:F2}, Tax: R{taxAmount:F2}, Net Pay: R{netPay:F2}";
                return RedirectToAction("Details", new { id = payroll.PayrollId });
            }
            catch (Exception ex)
            {
                // Log the actual error for debugging
                System.Diagnostics.Debug.WriteLine("Payroll generation error: " + ex.ToString());

                ModelState.AddModelError("", "An error occurred while generating payroll. Please try again.");
                ViewBag.GuardId = new SelectList(db.Guards.Where(g => g.IsActive), "GuardId", "FullName");
                return View();
            }
        }

        // Helper method to get attendance records with proper hours calculation
        private List<Attendance> GetAttendanceRecordsForPeriod(int guardId, DateTime startDate, DateTime endDate)
        {
            // Get all attendance records with calculated hours
            var attendances = db.Attendances
                .Where(a => a.GuardId == guardId &&
                           a.AttendanceDate >= startDate &&
                           a.AttendanceDate <= endDate &&
                           a.CheckOutTime != null &&
                           a.HoursWorked > 0)
                .OrderBy(a => a.AttendanceDate)
                .ToList();

            return attendances;
        }

        // New method to get payroll-ready data
        public List<PayrollAttendance> GetPayrollAttendanceData(int guardId, DateTime startDate, DateTime endDate)
        {
            var payrollData = db.Attendances
                .Where(a => a.GuardId == guardId &&
                           a.AttendanceDate >= startDate &&
                           a.AttendanceDate <= endDate &&
                           a.CheckOutTime != null &&
                           a.HoursWorked > 0)
                .Select(a => new PayrollAttendance
                {
                    Date = a.AttendanceDate,
                    CheckInTime = a.CheckInTime,
                    CheckOutTime = a.CheckOutTime.Value,
                    HoursWorked = a.HoursWorked,
                    ShiftType = a.ShiftRoster.ShiftType,
                    RosterId = (int)a.RosterId
                })
                .OrderBy(a => a.Date)
                .ToList();

            return payrollData;
        }

        // Helper method to sync GuardCheckIn records with Attendance records
        private void SyncCheckInsWithAttendance(int guardId, DateTime startDate, DateTime endDate)
        {
            // Get all check-in/check-out pairs for the period
            var checkIns = db.GuardCheckIns
                .Where(c => c.GuardId == guardId &&
                           c.CheckInTime >= startDate &&
                           c.CheckInTime <= endDate)
                .OrderBy(c => c.CheckInTime)
                .ToList();

            foreach (var checkIn in checkIns)
            {
                // Find if attendance record already exists for this check-in
                var existingAttendance = db.Attendances
                    .FirstOrDefault(a => a.CheckInId == checkIn.CheckInId);

                if (existingAttendance == null)
                {
                    // Create new attendance record from check-in
                    var attendance = new Attendance
                    {
                        GuardId = guardId,
                        CheckInTime = checkIn.CheckInTime,
                        AttendanceDate = checkIn.CheckInTime.Date,
                        CheckInId = checkIn.CheckInId
                    };

                    // If this is a check-out, find the corresponding check-in and calculate hours
                    if (checkIn.Status == "Checked Out" || checkIn.Status == "Late Departure")
                    {
                        // Find the corresponding check-in for this day
                        var correspondingCheckIn = checkIns
                            .Where(c => c.GuardId == guardId &&
                                       c.CheckInTime.Date == checkIn.CheckInTime.Date &&
                                       (c.Status == "Present" || c.Status == "Late Arrival") &&
                                       c.CheckInTime < checkIn.CheckInTime)
                            .OrderByDescending(c => c.CheckInTime)
                            .FirstOrDefault();

                        if (correspondingCheckIn != null)
                        {
                            attendance.CheckInTime = correspondingCheckIn.CheckInTime;
                            attendance.CheckOutTime = checkIn.CheckInTime;
                            attendance.HoursWorked = CalculateHoursWorked(correspondingCheckIn.CheckInTime, checkIn.CheckInTime);
                        }
                    }

                    db.Attendances.Add(attendance);
                }
                else
                {
                    // Update existing attendance record if needed
                    if ((checkIn.Status == "Checked Out" || checkIn.Status == "Late Departure") &&
                        existingAttendance.CheckOutTime == null)
                    {
                        existingAttendance.CheckOutTime = checkIn.CheckInTime;
                        existingAttendance.HoursWorked = CalculateHoursWorked(existingAttendance.CheckInTime, checkIn.CheckInTime);
                    }
                }
            }

            db.SaveChanges();
        }

        // Helper method to calculate hours between check-in and check-out
        private double CalculateHoursWorked(DateTime checkInTime, DateTime checkOutTime)
        {
            if (checkOutTime <= checkInTime)
                return 0;

            TimeSpan timeWorked = checkOutTime - checkInTime;
            return Math.Round(timeWorked.TotalHours, 2); // Round to 2 decimal places
        }

        // GET: Payroll/Details/5
        [Authorize(Roles = "Admin")]
        public ActionResult Details(int? id)
        {
            if (id == null)
            {
                return HttpNotFound();
            }

            Payroll payroll = db.Payrolls.Include(p => p.Guard).FirstOrDefault(p => p.PayrollId == id);
            if (payroll == null)
            {
                return HttpNotFound();
            }

            // Get attendance records for this payroll period
            var attendances = db.Attendances
                .Where(a => a.GuardId == payroll.GuardId &&
                           a.AttendanceDate >= payroll.PayPeriodStart &&
                           a.AttendanceDate <= payroll.PayPeriodEnd &&
                           a.CheckOutTime != null)
                .OrderBy(a => a.AttendanceDate)
                .ToList();

            ViewBag.AttendanceRecords = attendances;
            return View(payroll);
        }

        // GET: Payroll/Edit/5
        [Authorize(Roles = "Admin")]
        public ActionResult Edit(int? id)
        {
            if (id == null)
            {
                return HttpNotFound();
            }

            Payroll payroll = db.Payrolls.Find(id);
            if (payroll == null)
            {
                return HttpNotFound();
            }

            ViewBag.GuardId = new SelectList(db.Guards.Where(g => g.IsActive), "GuardId", "FullName", payroll.GuardId);
            return View(payroll);
        }

        // POST: Payroll/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin")]
        public ActionResult Edit(Payroll payroll)
        {
            if (ModelState.IsValid)
            {
                db.Entry(payroll).State = EntityState.Modified;
                db.SaveChanges();

                TempData["SuccessMessage"] = "Payroll updated successfully!";
                return RedirectToAction("Details", new { id = payroll.PayrollId });
            }

            ViewBag.GuardId = new SelectList(db.Guards.Where(g => g.IsActive), "GuardId", "FullName", payroll.GuardId);
            return View(payroll);
        }

        // POST: Payroll/Delete/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin")]
        public ActionResult Delete(int id)
        {
            try
            {
                Payroll payroll = db.Payrolls.Include(p => p.Guard).FirstOrDefault(p => p.PayrollId == id);
                if (payroll == null)
                {
                    TempData["ErrorMessage"] = "Payroll record not found.";
                    return RedirectToAction("Index");
                }

                // Store guard name and period for notification
                string guardName = payroll.Guard?.FullName ?? "Unknown Guard";
                string period = $"{payroll.PayPeriodStart:yyyy-MM-dd} to {payroll.PayPeriodEnd:yyyy-MM-dd}";

                db.Payrolls.Remove(payroll);
                db.SaveChanges();

                // Notify about deletion
                NotifyDirectorAboutPayrollDeletion(guardName, period);

                TempData["SuccessMessage"] = $"Payroll for {guardName} ({period}) has been deleted successfully.";
                return RedirectToAction("Index");
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = "An error occurred while deleting the payroll: " + ex.Message;
                return RedirectToAction("Details", new { id = id });
            }
        }

        // GET: Payroll/ManageRates
        [Authorize(Roles = "Admin")]
        public ActionResult ManageRates()
        {
            var rates = db.GuardRates.Include(r => r.Guard).ToList();
            return View(rates);
        }

        // GET: Payroll/CreateRate
        [Authorize(Roles = "Admin")]
        public ActionResult CreateRate()
        {
            ViewBag.GuardId = new SelectList(db.Guards.Where(g => g.IsActive), "GuardId", "FullName");
            return View();
        }

        // POST: Payroll/CreateRate
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin")]
        public ActionResult CreateRate(GuardRate guardRate)
        {
            if (ModelState.IsValid)
            {
                // Deactivate previous active rates for this guard
                var previousRates = db.GuardRates
                    .Where(r => r.GuardId == guardRate.GuardId && r.IsActive)
                    .ToList();

                foreach (var rate in previousRates)
                {
                    rate.IsActive = false;
                }

                // Add new rate
                guardRate.IsActive = true;
                db.GuardRates.Add(guardRate);
                db.SaveChanges();

                TempData["SuccessMessage"] = "Guard rate created successfully!";
                return RedirectToAction("ManageRates");
            }

            ViewBag.GuardId = new SelectList(db.Guards.Where(g => g.IsActive), "GuardId", "FullName", guardRate.GuardId);
            return View(guardRate);
        }

        // GET: Payroll/EditRate/5
        [Authorize(Roles = "Admin")]
        public ActionResult EditRate(int? id)
        {
            if (id == null)
            {
                return HttpNotFound();
            }

            GuardRate guardRate = db.GuardRates.Find(id);
            if (guardRate == null)
            {
                return HttpNotFound();
            }

            ViewBag.GuardId = new SelectList(db.Guards.Where(g => g.IsActive), "GuardId", "FullName", guardRate.GuardId);
            return View(guardRate);
        }

        // POST: Payroll/EditRate/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin")]
        public ActionResult EditRate(GuardRate guardRate)
        {
            if (ModelState.IsValid)
            {
                db.Entry(guardRate).State = EntityState.Modified;
                db.SaveChanges();

                TempData["SuccessMessage"] = "Guard rate updated successfully!";
                return RedirectToAction("ManageRates");
            }

            ViewBag.GuardId = new SelectList(db.Guards.Where(g => g.IsActive), "GuardId", "FullName", guardRate.GuardId);
            return View(guardRate);
        }

        // POST: Payroll/DeactivateRate/5
        [HttpPost]
        [Authorize(Roles = "Admin")]
        public JsonResult DeactivateRate(int id)
        {
            try
            {
                var guardRate = db.GuardRates.Find(id);
                if (guardRate == null)
                {
                    return Json(new { success = false, message = "Guard rate not found." });
                }

                guardRate.IsActive = false;
                db.SaveChanges();

                return Json(new { success = true, message = "Rate deactivated successfully." });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Error deactivating rate: " + ex.Message });
            }
        }

        // POST: Payroll/ActivateRate/5
        [HttpPost]
        [Authorize(Roles = "Admin")]
        public JsonResult ActivateRate(int id)
        {
            try
            {
                var guardRate = db.GuardRates.Find(id);
                if (guardRate == null)
                {
                    return Json(new { success = false, message = "Guard rate not found." });
                }

                // Deactivate other active rates for the same guard
                var otherActiveRates = db.GuardRates
                    .Where(r => r.GuardId == guardRate.GuardId && r.GuardRateId != id && r.IsActive)
                    .ToList();

                foreach (var rate in otherActiveRates)
                {
                    rate.IsActive = false;
                }

                guardRate.IsActive = true;
                db.SaveChanges();

                return Json(new { success = true, message = "Rate activated successfully." });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Error activating rate: " + ex.Message });
            }
        }

        // POST: Payroll/DeleteRate/5
        [HttpPost]
        [Authorize(Roles = "Admin")]
        public JsonResult DeleteRate(int id)
        {
            try
            {
                var guardRate = db.GuardRates.Find(id);
                if (guardRate == null)
                {
                    return Json(new { success = false, message = "Guard rate not found." });
                }

                db.GuardRates.Remove(guardRate);
                db.SaveChanges();

                return Json(new { success = true, message = "Rate deleted successfully." });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Error deleting rate: " + ex.Message });
            }
        }

        // GET: Payroll/TaxConfig
        [Authorize(Roles = "Admin")]
        public ActionResult TaxConfig()
        {
            var taxConfig = db.TaxConfigurations.FirstOrDefault(t => t.IsActive);
            if (taxConfig == null)
            {
                // Create a default tax configuration if none exists
                taxConfig = new TaxConfiguration
                {
                    TaxYear = DateTime.Now.Year,
                    TaxPercentage = 15, // 15% tax
                    TaxThreshold = 1, // R1 threshold - tax applies to all earnings
                    IsActive = true
                };
                db.TaxConfigurations.Add(taxConfig);
                db.SaveChanges();
            }

            return View(taxConfig);
        }

        // POST: Payroll/TaxConfig
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin")]
        public ActionResult TaxConfig(TaxConfiguration taxConfig)
        {
            if (ModelState.IsValid)
            {
                // Deactivate all other tax configurations
                var allConfigs = db.TaxConfigurations.ToList();
                foreach (var config in allConfigs)
                {
                    config.IsActive = false;
                }

                // Set this as active
                taxConfig.IsActive = true;

                if (taxConfig.TaxConfigId == 0)
                {
                    db.TaxConfigurations.Add(taxConfig);
                }
                else
                {
                    db.Entry(taxConfig).State = EntityState.Modified;
                }

                db.SaveChanges();

                TempData["SuccessMessage"] = "Tax configuration updated successfully!";
                return View(taxConfig);
            }

            return View(taxConfig);
        }

        // GET: Payroll/CheckExisting
        [Authorize(Roles = "Admin")]
        public JsonResult CheckExisting(int guardId, DateTime payPeriodStart)
        {
            try
            {
                bool exists = db.Payrolls.Any(p =>
                    p.GuardId == guardId &&
                    p.PayPeriodStart.Year == payPeriodStart.Year &&
                    p.PayPeriodStart.Month == payPeriodStart.Month);

                var existingPayroll = db.Payrolls
                    .Include(p => p.Guard)
                    .FirstOrDefault(p =>
                        p.GuardId == guardId &&
                        p.PayPeriodStart.Year == payPeriodStart.Year &&
                        p.PayPeriodStart.Month == payPeriodStart.Month);

                var guard = db.Guards.Find(guardId);
                var guardName = guard?.FullName ?? "Unknown Guard";

                return Json(new
                {
                    exists = exists,
                    message = exists ? $"Payroll for {guardName} already exists for {payPeriodStart:MMMM yyyy}" : "No existing payroll found",
                    payrollId = existingPayroll?.PayrollId
                }, JsonRequestBehavior.AllowGet);
            }
            catch (Exception ex)
            {
                return Json(new { error = ex.Message }, JsonRequestBehavior.AllowGet);
            }
        }

        // Enhanced payroll calculation
        public ActionResult GeneratePayrollReport(int? month, int? year)
        {
            var targetMonth = month ?? DateTime.Now.Month;
            var targetYear = year ?? DateTime.Now.Year;

            var payrollData = db.Payrolls
                .Include(p => p.Guard)
                .Where(p => p.PayPeriodStart.Month == targetMonth &&
                           p.PayPeriodStart.Year == targetYear)
                .ToList();

            // Calculate summary statistics
            var summary = new
            {
                TotalGuards = payrollData.Count,
                TotalHours = payrollData.Sum(p => p.TotalHours),
                TotalGrossPay = payrollData.Sum(p => p.GrossPay),
                TotalTaxAmount = payrollData.Sum(p => p.TaxAmount),
                TotalNetPay = payrollData.Sum(p => p.NetPay),
                AverageHours = payrollData.Average(p => p.TotalHours)
            };

            ViewBag.Summary = summary;
            ViewBag.SelectedMonth = targetMonth;
            ViewBag.SelectedYear = targetYear;

            return View(payrollData);
        }

        // Enhanced payroll generation with detailed breakdown
        private PayrollBreakdown CalculatePayrollBreakdown(int guardId, DateTime startDate, DateTime endDate)
        {
            var attendances = db.Attendances
                .Where(a => a.GuardId == guardId &&
                           a.AttendanceDate >= startDate &&
                           a.AttendanceDate <= endDate &&
                           a.CheckOutTime != null)
                .ToList();

            var breakdown = new PayrollBreakdown
            {
                TotalHours = attendances.Sum(a => a.HoursWorked),
                RegularHours = attendances.Sum(a => a.HoursWorked <= 12 ? a.HoursWorked : 12),
                OvertimeHours = attendances.Sum(a => a.HoursWorked > 12 ? a.HoursWorked - 12 : 0),
                DaysWorked = attendances.Select(a => a.AttendanceDate).Distinct().Count(),
                ShiftDetails = attendances.GroupBy(a => a.AttendanceDate)
                    .Select(g => new ShiftDetail
                    {
                        Date = g.Key,
                        Hours = g.Sum(a => a.HoursWorked),
                        IsOvertime = g.Sum(a => a.HoursWorked) > 12
                    }).ToList()
            };

            return breakdown;
        }

        // GET: Payroll/GetAttendanceSummary
        [Authorize(Roles = "Admin")]
        public JsonResult GetAttendanceSummary(int guardId, DateTime startDate, DateTime endDate)
        {
            try
            {
                var attendances = GetAttendanceRecordsForPeriod(guardId, startDate, endDate);
                double totalHours = attendances.Sum(a => a.HoursWorked);
                int daysWorked = attendances.Select(a => a.AttendanceDate).Distinct().Count();

                return Json(new
                {
                    success = true,
                    totalHours = totalHours,
                    daysWorked = daysWorked,
                    attendanceCount = attendances.Count
                }, JsonRequestBehavior.AllowGet);
            }
            catch (Exception ex)
            {
                return Json(new { success = false, error = ex.Message }, JsonRequestBehavior.AllowGet);
            }
        }

        // Updated method to notify director about payroll creation
        private void NotifyDirectorAboutPayroll(int payrollId, string guardName, double totalHours, decimal grossPay, decimal taxAmount, decimal netPay)
        {
            try
            {
                // Find all directors to notify them
                var directors = db.Directors.ToList();
                foreach (var director in directors)
                {
                    var notification = new Notification
                    {
                        UserId = director.DirectorId.ToString(),
                        UserType = "Director",
                        Title = "Payroll Created",
                        Message = $"Payroll has been created for guard {guardName}. Total Hours: {totalHours:F2}, Gross Pay: R{grossPay:F2}, Tax: R{taxAmount:F2}, Net Pay: R{netPay:F2}",
                        NotificationType = "Report",
                        CreatedAt = DateTime.Now,
                        IsImportant = true,
                        RelatedUrl = Url.Action("Details", "Payroll", new { id = payrollId })
                    };

                    db.Notifications.Add(notification);
                }

                db.SaveChanges();
            }
            catch (Exception ex)
            {
                // Log the error but don't break the payroll generation process
                System.Diagnostics.Debug.WriteLine("Error creating payroll notification: " + ex.Message);
            }
        }

        // Method to notify director about payroll deletion
        private void NotifyDirectorAboutPayrollDeletion(string guardName, string period)
        {
            try
            {
                // Find all directors to notify them
                var directors = db.Directors.ToList();
                foreach (var director in directors)
                {
                    var notification = new Notification
                    {
                        UserId = director.DirectorId.ToString(),
                        UserType = "Director",
                        Title = "Payroll Deleted",
                        Message = $"Payroll for guard {guardName} ({period}) has been deleted",
                        NotificationType = "Report",
                        CreatedAt = DateTime.Now,
                        IsImportant = true,
                        RelatedUrl = Url.Action("Index", "Payroll")
                    };

                    db.Notifications.Add(notification);
                }

                db.SaveChanges();
            }
            catch (Exception ex)
            {
                // Log the error but don't break the deletion process
                System.Diagnostics.Debug.WriteLine("Error creating payroll deletion notification: " + ex.Message);
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

    // Helper classes for payroll breakdown
    public class PayrollBreakdown
    {
        public double TotalHours { get; set; }
        public double RegularHours { get; set; }
        public double OvertimeHours { get; set; }
        public int DaysWorked { get; set; }
        public List<ShiftDetail> ShiftDetails { get; set; }
    }

    public class ShiftDetail
    {
        public DateTime Date { get; set; }
        public double Hours { get; set; }
        public bool IsOvertime { get; set; }
    }

    public class PayrollAttendance
    {
        public DateTime Date { get; set; }
        public DateTime CheckInTime { get; set; }
        public DateTime CheckOutTime { get; set; }
        public double HoursWorked { get; set; }
        public string ShiftType { get; set; }
        public int RosterId { get; set; }
    }
}