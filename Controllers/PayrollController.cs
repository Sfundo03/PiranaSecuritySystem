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

                // Get attendance records for the period
                var attendances = db.Attendances
                    .Where(a => a.GuardId == guardId &&
                                a.AttendanceDate >= payPeriodStart &&
                                a.AttendanceDate <= payPeriodEnd &&
                                a.CheckOutTime != null)
                    .ToList();

                double totalHours = attendances.Sum(a => a.HoursWorked);

                if (totalHours <= 0)
                {
                    ModelState.AddModelError("", $"No valid attendance records found for {guard.FullName} in the selected period.");
                    ViewBag.GuardId = new SelectList(db.Guards.Where(g => g.IsActive), "GuardId", "FullName");
                    return View();
                }

                // Calculate pay
                decimal grossPay = (decimal)totalHours * guardRate.Rate;

                // Get active tax configuration
                var taxConfig = db.TaxConfigurations.FirstOrDefault(t => t.IsActive);
                decimal taxAmount = 0;

                if (taxConfig != null && grossPay > taxConfig.TaxThreshold)
                {
                    taxAmount = grossPay * (taxConfig.TaxPercentage / 100);
                }

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
                NotifyDirectorAboutPayroll(payroll.PayrollId, guard.FullName);

                TempData["SuccessMessage"] = $"Payroll for {guard.FullName} has been generated successfully!";
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
                    TaxThreshold = 10000, // R10,000 threshold
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

        // Method to notify director about payroll creation
        private void NotifyDirectorAboutPayroll(int payrollId, string guardName)
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
                        Message = $"Payroll has been created for guard {guardName}",
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
}