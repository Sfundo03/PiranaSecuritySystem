using Microsoft.AspNet.Identity;
using Microsoft.AspNet.Identity.EntityFramework;
using PiranaSecuritySystem.Models;
using PiranaSecuritySystem.ViewModels;
using System;
using System.Data.Entity.Validation;
using System.Linq;
using System.Net.Mail;
using System.Net;
using System.Threading.Tasks;
using System.Web.Mvc;
using System.Configuration;

namespace PiranaSecuritySystem.Controllers
{
    [Authorize(Roles = "Admin")]
    public class AdminController : Controller
    {
        private ApplicationDbContext db = new ApplicationDbContext();
        private ApplicationUserManager _userManager;

        public ApplicationUserManager UserManager
        {
            get
            {
                return _userManager ?? new ApplicationUserManager(new UserStore<ApplicationUser>(db));
            }
            private set
            {
                _userManager = value;
            }
        }

        // GET: Admin/Dashboard
        public ActionResult Dashboard()
        {
            try
            {
                var dashboardStats = new DashboardStats
                {
                    TotalGuards = db.Guards.Count(),
                    TotalInstructors = db.Instructors.Count(),
                    ActiveGuards = db.Guards.Count(g => g.IsActive),
                    ActiveInstructors = db.Instructors.Count(i => i.IsActive)
                };

                return View(dashboardStats);
            }
            catch (Exception ex)
            {
                var errorStats = new DashboardStats
                {
                    TotalGuards = 0,
                    TotalInstructors = 0,
                    ActiveGuards = 0,
                    ActiveInstructors = 0
                };

                ViewBag.Error = "Error loading statistics: " + ex.Message;
                return View(errorStats);
            }
        }

        // GET: Admin/RegisterGuard
        public ActionResult RegisterGuard()
        {
            return View();
        }

        // POST: Admin/RegisterGuard
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> RegisterGuard(RegisterGuardViewModel model)
        {
            if (ModelState.IsValid)
            {
                EnsureRolesExist();

                if (db.Users.Any(u => u.Email == model.Email))
                {
                    ModelState.AddModelError("", "Email address is already registered.");
                    return View(model);
                }

                if (db.Guards.Any(g => g.PSIRAnumber == model.PSIRAnumber))
                {
                    ModelState.AddModelError("", "PSIRA number is already registered.");
                    return View(model);
                }

                var user = new ApplicationUser { UserName = model.Email, Email = model.Email };
                var result = await UserManager.CreateAsync(user, model.Password);

                if (result.Succeeded)
                {
                    try
                    {
                        await UserManager.AddToRoleAsync(user.Id, "Guard");
                    }
                    catch (InvalidOperationException ex)
                    {
                        if (ex.Message.Contains("does not exist"))
                        {
                            var roleManager = new RoleManager<IdentityRole>(new RoleStore<IdentityRole>(db));
                            roleManager.Create(new IdentityRole("Guard"));
                            await UserManager.AddToRoleAsync(user.Id, "Guard");
                        }
                        else
                        {
                            throw;
                        }
                    }

                    var guard = new Guard
                    {
                        PSIRAnumber = model.PSIRAnumber,
                        Guard_FName = model.Guard_FName,
                        Guard_LName = model.Guard_LName,
                        Email = model.Email,
                        PhoneNumber = model.PhoneNumber,
                        DateRegistered = DateTime.Now,
                        IsActive = true,
                        UserId = user.Id,
                        IdentityNumber = model.IdentityNumber,
                        Gender = model.Gender,
                        Emergency_CellNo = model.Emergency_CellNo,
                        Address = model.Address,
                        Street = model.Street,
                        HouseNumber = model.HouseNumber,
                        City = model.City,
                        PostalCode = model.PostalCode
                    };

                    db.Guards.Add(guard);

                    try
                    {
                        db.SaveChanges();
                    }
                    catch (DbEntityValidationException ex)
                    {
                        var errorMessages = ex.EntityValidationErrors
                            .SelectMany(x => x.ValidationErrors)
                            .Select(x => x.ErrorMessage);

                        var fullErrorMessage = string.Join("; ", errorMessages);
                        System.Diagnostics.Debug.WriteLine($"Validation errors: {fullErrorMessage}");

                        ModelState.AddModelError("", "Validation failed: " + fullErrorMessage);
                        return View(model);
                    }
                    catch (Exception ex)
                    {
                        ModelState.AddModelError("", "Error saving guard: " + ex.Message);
                        return View(model);
                    }

                    // Send email with credentials
                    string fullName = $"{model.Guard_FName} {model.Guard_LName}";
                    await SendGuardCredentialsEmail(model.Email, model.Password, fullName);

                    TempData["SuccessMessage"] = $"Guard {fullName} has been registered successfully! Credentials have been sent to their email.";
                    return RedirectToAction("ManageGuards");
                }

                AddErrors(result);
            }

            return View(model);
        }

        // GET: Admin/RegisterInstructor
        public ActionResult RegisterInstructor()
        {
            return View();
        }

        // POST: Admin/RegisterInstructor
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> RegisterInstructor(RegisterInstructorViewModel model)
        {
            if (ModelState.IsValid)
            {
                EnsureRolesExist();

                if (db.Users.Any(u => u.Email == model.Email))
                {
                    ModelState.AddModelError("", "Email address is already registered.");
                    return View(model);
                }

                if (db.Instructors.Any(i => i.EmployeeId == model.EmployeeId))
                {
                    ModelState.AddModelError("", "Employee ID is already registered.");
                    return View(model);
                }

                var user = new ApplicationUser { UserName = model.Email, Email = model.Email };
                var result = await UserManager.CreateAsync(user, model.Password);

                if (result.Succeeded)
                {
                    try
                    {
                        await UserManager.AddToRoleAsync(user.Id, "Instructor");
                    }
                    catch (InvalidOperationException ex)
                    {
                        if (ex.Message.Contains("does not exist"))
                        {
                            var roleManager = new RoleManager<IdentityRole>(new RoleStore<IdentityRole>(db));
                            roleManager.Create(new IdentityRole("Instructor"));
                            await UserManager.AddToRoleAsync(user.Id, "Instructor");
                        }
                        else
                        {
                            throw;
                        }
                    }

                    var instructor = new Instructor
                    {
                        EmployeeId = model.EmployeeId,
                        FullName = model.FullName,
                        Email = model.Email,
                        PhoneNumber = model.PhoneNumber,
                        Specialization = model.Specialization,
                        DateRegistered = DateTime.Now,
                        IsActive = true,
                        UserId = user.Id
                    };

                    db.Instructors.Add(instructor);

                    try
                    {
                        db.SaveChanges();
                    }
                    catch (DbEntityValidationException ex)
                    {
                        var errorMessages = ex.EntityValidationErrors
                            .SelectMany(x => x.ValidationErrors)
                            .Select(x => x.ErrorMessage);

                        var fullErrorMessage = string.Join("; ", errorMessages);
                        System.Diagnostics.Debug.WriteLine($"Validation errors: {fullErrorMessage}");

                        ModelState.AddModelError("", "Validation failed: " + fullErrorMessage);
                        return View(model);
                    }
                    catch (Exception ex)
                    {
                        ModelState.AddModelError("", "Error saving instructor: " + ex.Message);
                        return View(model);
                    }

                    // Send email with credentials
                    await SendInstructorCredentialsEmail(model.Email, model.Password, model.FullName);

                    TempData["SuccessMessage"] = $"Instructor {model.FullName} has been registered successfully! Credentials have been sent to their email.";
                    return RedirectToAction("ManageInstructors");
                }

                AddErrors(result);
            }

            return View(model);
        }

        // GET: Admin/ManageGuards
        public ActionResult ManageGuards()
        {
            var guards = db.Guards.OrderBy(g => g.Guard_FName + " " + g.Guard_LName).ToList();
            return View(guards);
        }

        // GET: Admin/ManageInstructors
        public ActionResult ManageInstructors()
        {
            var instructors = db.Instructors.OrderBy(i => i.FullName).ToList();
            return View(instructors);
        }

        private void EnsureRolesExist()
        {
            var roleManager = new RoleManager<IdentityRole>(new RoleStore<IdentityRole>(db));

            if (!roleManager.RoleExists("Guard"))
                roleManager.Create(new IdentityRole("Guard"));

            if (!roleManager.RoleExists("Instructor"))
                roleManager.Create(new IdentityRole("Instructor"));

            if (!roleManager.RoleExists("Admin"))
                roleManager.Create(new IdentityRole("Admin"));

            if (!roleManager.RoleExists("Director"))
                roleManager.Create(new IdentityRole("Director"));
        }

        private async Task SendGuardCredentialsEmail(string email, string password, string fullName)
        {
            try
            {
                string subject = "Your Pirana Security System Account";
                string body = $@"
<!DOCTYPE html>
<html>
<head>
    <style>
        body {{ font-family: Arial, sans-serif; line-height: 1.6; color: #333; }}
        .container {{ max-width: 600px; margin: 0 auto; padding: 20px; }}
        .header {{ background-color: #007bff; color: white; padding: 20px; text-align: center; }}
        .content {{ background-color: #f8f9fa; padding: 20px; }}
        .credentials {{ background-color: #e9ecef; padding: 15px; border-radius: 5px; margin: 15px 0; }}
        .footer {{ text-align: center; margin-top: 20px; color: #6c757d; font-size: 12px; }}
    </style>
</head>
<body>
    <div class='container'>
        <div class='header'>
            <h2>Pirana Security System</h2>
        </div>
        <div class='content'>
            <h3>Dear {fullName},</h3>
            <p>You have been successfully registered on the Pirana Security System as a Guard.</p>
            <p>Please use the following credentials to login to your account:</p>
            
            <div class='credentials'>
                <strong>Email:</strong> {email}<br>
                <strong>Password:</strong> {password}
            </div>

            <p><strong>Login URL:</strong> {Url.Action("Login", "Account", null, Request.Url.Scheme)}</p>
            
            <p><strong>Important Security Notice:</strong><br>
            For security reasons, we strongly recommend that you change your password immediately after your first login.</p>
        </div>
        <div class='footer'>
            <p>This is an automated message. Please do not reply to this email.</p>
            <p>&copy; {DateTime.Now.Year} Pirana Security System. All rights reserved.</p>
        </div>
    </div>
</body>
</html>";

                await SendEmailAsync(email, subject, body);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error sending email to {email}: {ex.Message}");
                // You might want to log this error or handle it appropriately
            }
        }

        private async Task SendInstructorCredentialsEmail(string email, string password, string fullName)
        {
            try
            {
                string subject = "Your Pirana Security System Instructor Account";
                string body = $@"
<!DOCTYPE html>
<html>
<head>
    <style>
        body {{ font-family: Arial, sans-serif; line-height: 1.6; color: #333; }}
        .container {{ max-width: 600px; margin: 0 auto; padding: 20px; }}
        .header {{ background-color: #28a745; color: white; padding: 20px; text-align: center; }}
        .content {{ background-color: #f8f9fa; padding: 20px; }}
        .credentials {{ background-color: #e9ecef; padding: 15px; border-radius: 5px; margin: 15px 0; }}
        .footer {{ text-align: center; margin-top: 20px; color: #6c757d; font-size: 12px; }}
    </style>
</head>
<body>
    <div class='container'>
        <div class='header'>
            <h2>Pirana Security System - Instructor Portal</h2>
        </div>
        <div class='content'>
            <h3>Dear {fullName},</h3>
            <p>You have been successfully registered on the Pirana Security System as an Instructor.</p>
            <p>Please use the following credentials to login to your account:</p>
            
            <div class='credentials'>
                <strong>Email:</strong> {email}<br>
                <strong>Password:</strong> {password}
            </div>

            <p><strong>Login URL:</strong> {Url.Action("Login", "Account", null, Request.Url.Scheme)}</p>
            
            <p><strong>Important Security Notice:</strong><br>
            For security reasons, we strongly recommend that you change your password immediately after your first login.</p>
        </div>
        <div class='footer'>
            <p>This is an automated message. Please do not reply to this email.</p>
            <p>&copy; {DateTime.Now.Year} Pirana Security System. All rights reserved.</p>
        </div>
    </div>
</body>
</html>";

                await SendEmailAsync(email, subject, body);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error sending email to {email}: {ex.Message}");
                // You might want to log this error or handle it appropriately
            }
        }

        private async Task SendEmailAsync(string toEmail, string subject, string body)
        {
            try
            {
                // Configure these settings in your web.config or app settings
                string fromEmail = "noreply@piranasecurity.com"; // Change this to your email
                string smtpServer = "smtp.your-email-provider.com"; // Change to your SMTP server
                int smtpPort = 587; // Change to your SMTP port
                string smtpUsername = "your-email@domain.com"; // Change to your SMTP username
                string smtpPassword = "your-email-password"; // Change to your SMTP password

                using (var message = new MailMessage())
                {
                    message.From = new MailAddress(fromEmail, "Pirana Security System");
                    message.To.Add(new MailAddress(toEmail));
                    message.Subject = subject;
                    message.Body = body;
                    message.IsBodyHtml = true;

                    using (var smtpClient = new SmtpClient(smtpServer, smtpPort))
                    {
                        smtpClient.Credentials = new NetworkCredential(smtpUsername, smtpPassword);
                        smtpClient.EnableSsl = true;

                        await smtpClient.SendMailAsync(message);
                    }
                }

                System.Diagnostics.Debug.WriteLine($"Email sent successfully to: {toEmail}");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error sending email: {ex.Message}");
                throw; // Re-throw to handle in calling method
            }
        }

        private void AddErrors(IdentityResult result)
        {
            foreach (var error in result.Errors)
            {
                ModelState.AddModelError("", error);
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                if (_userManager != null)
                {
                    _userManager.Dispose();
                    _userManager = null;
                }
                db.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}