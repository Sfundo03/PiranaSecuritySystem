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
                // Get guard's current rate
                var guardRate = db.GuardRates
                    .Where(r => r.GuardId == guardId && r.IsActive)
                    .OrderByDescending(r => r.EffectiveDate)
                    .FirstOrDefault();

                if (guardRate == null)
                {
                    ModelState.AddModelError("", "No active rate found for this guard.");
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
                NotifyDirectorAboutPayroll(payroll.PayrollId, payroll.Guard.FullName);

                return RedirectToAction("Details", new { id = payroll.PayrollId });
            }
            catch (Exception ex)
            {
                ModelState.AddModelError("", "An error occurred while generating payroll: " + ex.Message);
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

            ViewBag.GuardId = new SelectList(db.Guards, "GuardId", "FullName", payroll.GuardId);
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
                return RedirectToAction("Index");
            }

            ViewBag.GuardId = new SelectList(db.Guards, "GuardId", "FullName", payroll.GuardId);
            return View(payroll);
        }

        // GET: Payroll/Delete/5
        [Authorize(Roles = "Admin")]
        public ActionResult Delete(int? id)
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

        // POST: Payroll/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin")]
        public ActionResult DeleteConfirmed(int id)
        {
            Payroll payroll = db.Payrolls.Find(id);
            db.Payrolls.Remove(payroll);
            db.SaveChanges();
            return RedirectToAction("Index");
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

                return RedirectToAction("ManageRates");
            }

            ViewBag.GuardId = new SelectList(db.Guards.Where(g => g.IsActive), "GuardId", "FullName", guardRate.GuardId);
            return View(guardRate);
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

                ViewBag.Message = "Tax configuration updated successfully.";
                return View(taxConfig);
            }

            return View(taxConfig);
        }

        // Method to notify director about payroll creation
        private void NotifyDirectorAboutPayroll(int payrollId, string guardName)
        {
            try
            {
                // Create a notification for the director
                var notification = new Notification
                {
                    UserId = "Director", // This will be set to the actual director ID
                    UserType = "Director",
                    Title = "Payroll Created",
                    Message = $"Payroll has been created for guard {guardName}",
                    NotificationType = "Report",
                    CreatedAt = DateTime.Now,
                    IsImportant = true,
                    RelatedUrl = Url.Action("Details", "Payroll", new { id = payrollId })
                };

                // Find all directors to notify them
                var directors = db.Directors.ToList();
                foreach (var director in directors)
                {
                    notification.UserId = director.DirectorId.ToString();
                    db.Notifications.Add(notification);

                    // Create a new instance for each director
                    notification = new Notification
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
                }

                db.SaveChanges();
            }
            catch (Exception ex)
            {
                // Log the error but don't break the payroll generation process
                System.Diagnostics.Debug.WriteLine("Error creating payroll notification: " + ex.Message);
            }
        }

        // Alternative method using the DirectorController's notification system
        private void NotifyDirectorUsingController(int payrollId, string guardName)
        {
            try
            {
                // Create an instance of DirectorController
                var directorController = new DirectorController();

                // Set the controller context to allow URL generation
                directorController.ControllerContext = new ControllerContext(
                    this.ControllerContext.RequestContext,
                    directorController
                );

                // Call the notification method
                directorController.NotifyPayrollCreated(payrollId, guardName);
            }
            catch (Exception ex)
            {
                // Log the error but don't break the payroll generation process
                System.Diagnostics.Debug.WriteLine("Error notifying director: " + ex.Message);
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