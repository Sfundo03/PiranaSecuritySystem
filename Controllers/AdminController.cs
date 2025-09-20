using Microsoft.AspNet.Identity;
using Microsoft.AspNet.Identity.EntityFramework;
using PiranaSecuritySystem.Models;
using PiranaSecuritySystem.ViewModels;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data.Entity.Validation;
using System.Linq;
using System.Net;
using System.Net.Mail;
using System.Threading.Tasks;
using System.Web.Mvc;

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
            var model = new RegisterGuardViewModel
            {
                SiteOptions = new List<SelectListItem>
                {
                    new SelectListItem { Value = "Site A", Text = "Site A" },
                    new SelectListItem { Value = "Site B", Text = "Site B" },
                    new SelectListItem { Value = "Site C", Text = "Site C" }
                }
            };
            return View(model);
        }

        // POST: Admin/RegisterGuard
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> RegisterGuard(RegisterGuardViewModel model)
        {
            model.SiteOptions = new List<SelectListItem>
            {
                new SelectListItem { Value = "Site A", Text = "Site A" },
                new SelectListItem { Value = "Site B", Text = "Site B" },
                new SelectListItem { Value = "Site C", Text = "Site C" }
            };

            if (ModelState.IsValid)
            {
                EnsureRolesExist();

                if (db.Users.Any(u => u.Email == model.Email))
                {
                    ModelState.AddModelError("", "Email address is already registered.");
                    return View(model);
                }

                string sitePrefix = GetSitePrefix(model.Site);
                string siteUsername = GenerateSiteUsername(sitePrefix);

                if (db.Guards.Any(g => g.SiteUsername == siteUsername))
                {
                    ModelState.AddModelError("", "Username generation error. Please try again.");
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
                        City = model.City,
                        PostalCode = model.PostalCode,
                        Site = model.Site,
                        SiteUsername = siteUsername
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

                    string fullName = $"{model.Guard_FName} {model.Guard_LName}";
                    await SendGuardCredentialsEmail(model.Email, model.Password, fullName);
                    CreateNewStaffNotification("Guard", fullName);

                    TempData["SuccessMessage"] = $"Guard {fullName} has been registered successfully! Credentials have been sent to their email.";
                    return RedirectToAction("ManageGuards");
                }

                AddErrors(result);
            }

            return View(model);
        }

        private string GetSitePrefix(string site)
        {
            switch (site)
            {
                case "Site A": return "GA";
                case "Site B": return "GB";
                case "Site C": return "GC";
                default: return "GX";
            }
        }

        private string GenerateSiteUsername(string sitePrefix)
        {
            var existingUsernames = db.Guards
                .Where(g => g.SiteUsername.StartsWith(sitePrefix))
                .Select(g => g.SiteUsername)
                .ToList();

            int nextNumber = 1;

            if (existingUsernames.Any())
            {
                var numbers = existingUsernames
                    .Select(username =>
                    {
                        string numberPart = username.Substring(sitePrefix.Length);
                        if (int.TryParse(numberPart, out int number))
                        {
                            return number;
                        }
                        return 0;
                    })
                    .Where(n => n > 0)
                    .ToList();

                if (numbers.Any())
                {
                    nextNumber = numbers.Max() + 1;
                }
            }

            return $"{sitePrefix}{nextNumber:D3}";
        }

        // GET: Admin/ManageGuards
        public ActionResult ManageGuards()
        {
            if (TempData["SuccessMessage"] != null)
            {
                ViewBag.SuccessMessage = TempData["SuccessMessage"];
            }

            var guards = db.Guards.OrderBy(g => g.Guard_FName + " " + g.Guard_LName).ToList();
            return View(guards);
        }

        // GET: Admin/UpdateGuardStatus/5
        public ActionResult UpdateGuardStatus(int id)
        {
            var guard = db.Guards.Find(id);
            if (guard == null)
            {
                return HttpNotFound();
            }
            return View(guard);
        }

        // POST: Admin/UpdateGuardStatus/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult UpdateGuardStatus(int id, bool isActive, string statusReason = null)
        {
            try
            {
                var guard = db.Guards.Find(id);
                if (guard == null)
                {
                    return HttpNotFound();
                }

                var oldStatus = guard.IsActive;
                guard.IsActive = isActive;

                var user = db.Users.FirstOrDefault(u => u.Id == guard.UserId);
                if (user != null)
                {
                    var isActiveProperty = user.GetType().GetProperty("IsActive");
                    if (isActiveProperty != null && isActiveProperty.CanWrite)
                    {
                        isActiveProperty.SetValue(user, isActive);
                    }
                }

                db.SaveChanges();

                if (oldStatus != isActive)
                {
                    var notification = new Notification
                    {
                        Title = "Guard Status Updated",
                        Message = $"Guard {guard.Guard_FName} {guard.Guard_LName} status changed to {(isActive ? "Active" : "Inactive")}. Reason: {statusReason ?? "Not specified"}",
                        NotificationType = "Guard",
                        CreatedAt = DateTime.Now,
                        IsRead = false,
                        UserId = "Admin"
                    };
                    db.Notifications.Add(notification);
                    db.SaveChanges();
                }

                TempData["SuccessMessage"] = $"Guard status updated successfully to {(isActive ? "Active" : "Inactive")}.";
                return RedirectToAction("ManageGuards");
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = "Error updating guard status: " + ex.Message;
                return RedirectToAction("UpdateGuardStatus", new { id = id });
            }
        }

        // GET: Admin/EditGuard/5
        public ActionResult EditGuard(int id)
        {
            var guard = db.Guards.Find(id);
            if (guard == null)
            {
                return HttpNotFound();
            }
            return View(guard);
        }

        // POST: Admin/EditGuard/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult EditGuard(Guard model)
        {
            try
            {
                if (ModelState.IsValid)
                {
                    var guard = db.Guards.Find(model.GuardId);
                    if (guard == null)
                    {
                        return HttpNotFound();
                    }

                    guard.Guard_FName = model.Guard_FName;
                    guard.Guard_LName = model.Guard_LName;
                    guard.IdentityNumber = model.IdentityNumber;
                    guard.Gender = model.Gender;
                    guard.Email = model.Email;
                    guard.PhoneNumber = model.PhoneNumber;
                    guard.Emergency_CellNo = model.Emergency_CellNo;
                    guard.Address = model.Address;
                    guard.Street = model.Street;
                    guard.City = model.City;
                    guard.PostalCode = model.PostalCode;
                    guard.IsActive = model.IsActive;

                    var user = db.Users.FirstOrDefault(u => u.Id == guard.UserId);
                    if (user != null && user.Email != model.Email)
                    {
                        user.Email = model.Email;
                        user.UserName = model.Email;
                    }

                    db.SaveChanges();

                    TempData["SuccessMessage"] = $"Guard {model.Guard_FName} {model.Guard_LName} updated successfully!";
                    return RedirectToAction("ManageGuards");
                }

                return View(model);
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = "Error updating guard: " + ex.Message;
                return View(model);
            }
        }

        // GET: Admin/RegisterInstructor
        public ActionResult RegisterInstructor()
        {
            var model = new RegisterInstructorViewModel();
            return View(model);
        }

        // POST: Admin/RegisterInstructor
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> RegisterInstructor(RegisterInstructorViewModel model)
        {
            model.SiteOptions = new List<SelectListItem>
            {
                new SelectListItem { Value = "Site A", Text = "Site A" },
                new SelectListItem { Value = "Site B", Text = "Site B" },
                new SelectListItem { Value = "Site C", Text = "Site C" }
            };

            model.GroupOptions = new List<SelectListItem>
            {
                new SelectListItem { Value = "Group A", Text = "Group A" },
                new SelectListItem { Value = "Group B", Text = "Group B" },
                new SelectListItem { Value = "Group C", Text = "Group C" }
            };

            if (ModelState.IsValid)
            {
                EnsureRolesExist();

                if (db.Users.Any(u => u.Email == model.Email))
                {
                    ModelState.AddModelError("", "Email address is already registered.");
                    return View(model);
                }

                string sitePrefix = GetInstructorSitePrefix(model.Site);
                string siteUsername = GenerateInstructorSiteUsername(sitePrefix);
                if (db.Instructors.Any(i => i.SiteUsername == siteUsername))
                {
                    ModelState.AddModelError("", "Username generation error. Please try again.");
                    return View(model);
                }

                string employeeId = GenerateEmployeeId();

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
                        EmployeeId = employeeId,
                        FullName = model.FullName,
                        Group = model.Group,
                        Email = model.Email,
                        PhoneNumber = model.PhoneNumber,
                        Specialization = model.Specialization,
                        DateRegistered = DateTime.Now,
                        IsActive = true,
                        UserId = user.Id,
                        Site = model.Site,
                        SiteUsername = siteUsername
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

                    await SendInstructorCredentialsEmail(model.Email, model.Password, model.FullName, employeeId);
                    CreateNewStaffNotification("Instructor", model.FullName, employeeId);

                    TempData["SuccessMessage"] = $"Instructor {model.FullName} (ID: {employeeId}) has been registered successfully! Credentials have been sent to their email.";
                    return RedirectToAction("ManageInstructors");
                }

                AddErrors(result);
            }

            return View(model);
        }

        private string GetInstructorSitePrefix(string site)
        {
            switch (site)
            {
                case "Site A": return "IA";
                case "Site B": return "IB";
                case "Site C": return "IC";
                default: return "IX";
            }
        }

        private string GenerateInstructorSiteUsername(string sitePrefix)
        {
            var existingUsernames = db.Instructors
                .Where(i => i.SiteUsername.StartsWith(sitePrefix))
                .Select(i => i.SiteUsername)
                .ToList();

            int nextNumber = 1;
            if (existingUsernames.Any())
            {
                var numbers = existingUsernames
                    .Select(username =>
                    {
                        string numberPart = username.Substring(sitePrefix.Length);
                        if (int.TryParse(numberPart, out int number))
                        {
                            return number;
                        }
                        return 0;
                    })
                    .Where(n => n > 0)
                    .ToList();
                if (numbers.Any())
                {
                    nextNumber = numbers.Max() + 1;
                }
            }
            return $"{sitePrefix}{nextNumber:D3}";
        }

        private string GenerateEmployeeId()
        {
            return $"INST-{DateTime.Now:yyyyMMdd-HHmmss}";
        }

        // GET: Admin/ManageInstructors
        public ActionResult ManageInstructors()
        {
            var instructors = db.Instructors.OrderBy(i => i.FullName).ToList();
            return View(instructors);
        }

        // GET: Admin/EditInstructor/5
        public ActionResult EditInstructor(int id)
        {
            try
            {
                var instructor = db.Instructors.Find(id);
                if (instructor == null)
                {
                    return HttpNotFound();
                }

                var viewModel = new EditInstructorViewModel
                {
                    Id = instructor.Id,
                    FullName = instructor.FullName,
                    Group = instructor.Group,
                    Email = instructor.Email,
                    PhoneNumber = instructor.PhoneNumber,
                    Specialization = instructor.Specialization,
                    Site = instructor.Site,
                    IsActive = instructor.IsActive
                };

                return View(viewModel);
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = "Error loading instructor: " + ex.Message;
                return RedirectToAction("ManageInstructors");
            }
        }

        // POST: Admin/EditInstructor/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult EditInstructor(EditInstructorViewModel model)
        {
            try
            {
                if (ModelState.IsValid)
                {
                    var instructor = db.Instructors.Find(model.Id);
                    if (instructor == null)
                    {
                        return HttpNotFound();
                    }

                    // Check if email is already used by another instructor
                    if (db.Instructors.Any(i => i.Email == model.Email && i.Id != model.Id))
                    {
                        ModelState.AddModelError("Email", "Email address is already registered to another instructor.");
                        return View(model);
                    }

                    instructor.FullName = model.FullName;
                    instructor.Group = model.Group;
                    instructor.Email = model.Email;
                    instructor.PhoneNumber = model.PhoneNumber;
                    instructor.Specialization = model.Specialization;
                    instructor.Site = model.Site;
                    instructor.IsActive = model.IsActive;

                    // Update associated user email if changed
                    var user = db.Users.FirstOrDefault(u => u.Id == instructor.UserId);
                    if (user != null && user.Email != model.Email)
                    {
                        user.Email = model.Email;
                        user.UserName = model.Email;
                    }

                    db.SaveChanges();

                    TempData["SuccessMessage"] = $"Instructor {model.FullName} updated successfully!";
                    return RedirectToAction("ManageInstructors");
                }

                return View(model);
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = "Error updating instructor: " + ex.Message;
                return View(model);
            }
        }
        


        // POST: Admin/UpdateInstructorStatus/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult UpdateInstructorStatus(int id, bool isActive)
        {
            try
            {
                var instructor = db.Instructors.Find(id);
                if (instructor == null)
                {
                    return HttpNotFound();
                }

                instructor.IsActive = isActive;

                var user = db.Users.FirstOrDefault(u => u.Id == instructor.UserId);
                if (user != null)
                {
                    var isActiveProperty = user.GetType().GetProperty("IsActive");
                    if (isActiveProperty != null && isActiveProperty.CanWrite)
                    {
                        isActiveProperty.SetValue(user, isActive);
                    }
                }

                db.SaveChanges();

                TempData["SuccessMessage"] = $"Instructor {instructor.FullName} status updated successfully to {(isActive ? "Active" : "Inactive")}.";
                return RedirectToAction("ManageInstructors");
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = "Error updating instructor status: " + ex.Message;
                return RedirectToAction("ManageInstructors");
            }
        }



        // GET: Redirect to Payroll
        public ActionResult Payroll()
        {
            return RedirectToAction("Index", "Payroll");
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

        private void CreateNewStaffNotification(string staffType, string staffName, string employeeId = null)
        {
            try
            {
                var message = employeeId != null
                    ? $"A new {staffType.ToLower()} ({staffName}, ID: {employeeId}) has been registered in the system"
                    : $"A new {staffType.ToLower()} ({staffName}) has been registered in the system";

                var notification = new Notification
                {
                    Title = $"New {staffType} Added",
                    Message = message,
                    NotificationType = staffType.ToLower(),
                    RelatedUrl = staffType.ToLower() == "guard"
                        ? Url.Action("ManageGuards", "Admin")
                        : Url.Action("ManageInstructors", "Admin"),
                    UserId = "Director",
                    UserType = "Director",
                    CreatedAt = DateTime.Now,
                    IsRead = false
                };

                db.Notifications.Add(notification);
                db.SaveChanges();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error creating notification: {ex.Message}");
            }
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
            }
        }

        private async Task SendInstructorCredentialsEmail(string email, string password, string fullName, string employeeId)
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
            
            <div class='credentials'>
                <strong>Employee ID:</strong> {employeeId}<br>
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
            }
        }

        private async Task SendEmailAsync(string toEmail, string subject, string body)
        {
            try
            {
                string fromEmail = "noreply@piranasecurity.com";
                string smtpServer = "smtp.your-email-provider.com";
                int smtpPort = 587;
                string smtpUsername = "your-email@domain.com";
                string smtpPassword = "your-email-password";

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
                throw;
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