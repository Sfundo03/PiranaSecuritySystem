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

                // Load notifications for admin - SIMPLIFIED QUERY
                var notifications = db.Notifications
                    .Where(n => n.UserType == "Admin" || n.UserId == "Admin")
                    .OrderByDescending(n => n.CreatedAt)
                    .Take(10)
                    .ToList();

                dashboardStats.Notifications = notifications ?? new List<Notification>();
                dashboardStats.UnreadNotificationCount = notifications.Count(n => !n.IsRead);

                // Load recent activity
                ViewBag.RecentGuards = db.Guards.OrderByDescending(g => g.DateRegistered).Take(5).ToList();
                ViewBag.RecentInstructors = db.Instructors.OrderByDescending(i => i.DateRegistered).Take(5).ToList();

                return View(dashboardStats);
            }
            catch (Exception ex)
            {
                var errorStats = new DashboardStats
                {
                    TotalGuards = 0,
                    TotalInstructors = 0,
                    ActiveGuards = 0,
                    ActiveInstructors = 0,
                    UnreadNotificationCount = 0,
                    Notifications = new List<Notification>()
                };

                ViewBag.Error = "Error loading statistics: " + ex.Message;
                return View(errorStats);
            }
        }

        // GET: Admin/Notifications
        public ActionResult Notifications()
        {
            try
            {
                var notifications = db.Notifications
                    .Where(n => n.UserType == "Admin" || n.UserId == "Admin")
                    .OrderByDescending(n => n.CreatedAt)
                    .ToList();

                ViewBag.UnreadCount = notifications.Count(n => !n.IsRead);
                return View(notifications);
            }
            catch (Exception ex)
            {
                ViewBag.Error = "Error loading notifications: " + ex.Message;
                return View(new List<Notification>());
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

                    // Send email with credentials
                    string fullName = $"{model.Guard_FName} {model.Guard_LName}";
                    bool emailSent = await SendGuardCredentialsEmail(model.Email, model.Password, fullName);

                    if (!emailSent)
                    {
                        // Log email failure but don't prevent registration
                        System.Diagnostics.Debug.WriteLine($"Email sending failed for guard: {model.Email}");
                    }

                    // Create notifications for admin and director
                    CreateNewStaffNotification("Guard", fullName, guard.GuardId, model.Site);
                    CreateDirectorNotification("Guard", fullName, guard.GuardId, model.Site);

                    TempData["SuccessMessage"] = $"Guard {fullName} has been registered successfully! {(emailSent ? "Credentials have been sent to their email." : "Credentials email failed to send.")}";
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
                        UserId = "Admin",
                        UserType = "Admin"
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

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<JsonResult> UpdateGuardStatusAjax(int id, bool isActive)
        {
            try
            {
                var guard = db.Guards.Find(id);
                if (guard == null)
                {
                    return Json(new { success = false, message = "Guard not found." });
                }

                var oldStatus = guard.IsActive;
                guard.IsActive = isActive;

                var user = await UserManager.FindByIdAsync(guard.UserId);
                if (user != null)
                {
                    if (!isActive)
                    {
                        user.LockoutEnabled = true;
                        user.LockoutEndDateUtc = DateTime.UtcNow.AddYears(100);
                        user.EmailConfirmed = false;

                        if (await UserManager.HasPasswordAsync(user.Id))
                        {
                            await UserManager.RemovePasswordAsync(user.Id);
                        }
                    }
                    else
                    {
                        user.LockoutEnabled = false;
                        user.LockoutEndDateUtc = null;
                        user.EmailConfirmed = true;

                        var tempPassword = GenerateTemporaryPassword();
                        if (!await UserManager.HasPasswordAsync(user.Id))
                        {
                            var addPasswordResult = await UserManager.AddPasswordAsync(user.Id, tempPassword);
                            if (addPasswordResult.Succeeded)
                            {
                                await SendGuardReactivationEmail(guard.Email, tempPassword, $"{guard.Guard_FName} {guard.Guard_LName}");
                            }
                        }
                    }
                    await UserManager.UpdateAsync(user);
                }

                db.SaveChanges();

                return Json(new
                {
                    success = true,
                    message = $"Guard status updated to {(isActive ? "Active" : "Inactive")} successfully!",
                    newStatus = isActive ? "Active" : "Inactive"
                });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error updating guard status: {ex.Message}");
                return Json(new { success = false, message = "Error updating status: " + ex.Message });
            }
        }

        private async Task SendGuardReactivationEmail(string email, string tempPassword, string fullName)
        {
            try
            {
                string subject = "Your Pirana Security System Account Has Been Reactivated";
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
            <h2>Pirana Security System - Account Reactivated</h2>
        </div>
        <div class='content'>
            <h3>Dear {fullName},</h3>
            <p>Your guard account has been reactivated. You can now login to the system.</p>
            
            <div class='credentials'>
                <strong>Email:</strong> {email}<br>
                <strong>Temporary Password:</strong> {tempPassword}
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
                System.Diagnostics.Debug.WriteLine($"Error sending reactivation email to {email}: {ex.Message}");
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
            var model = new RegisterInstructorViewModel
            {
                SiteOptions = new List<SelectListItem>
                {
                    new SelectListItem { Value = "Site A", Text = "Site A" },
                    new SelectListItem { Value = "Site B", Text = "Site B" },
                    new SelectListItem { Value = "Site C", Text = "Site C" }
                },
                GroupOptions = new List<SelectListItem>
                {
                    new SelectListItem { Value = "Group A", Text = "Group A" },
                    new SelectListItem { Value = "Group B", Text = "Group B" },
                    new SelectListItem { Value = "Group C", Text = "Group C" },
                    new SelectListItem { Value = "Group D", Text = "Group D" }
                }
            };
            return View(model);
        }

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
                new SelectListItem { Value = "Group C", Text = "Group C" },
                new SelectListItem { Value = "Group D", Text = "Group D" }
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

                    string employeeId = GenerateEmployeeId();

                    var instructor = new Instructor
                    {
                        EmployeeId = employeeId,
                        FullName = model.FullName,
                        Email = model.Email,
                        PhoneNumber = model.PhoneNumber,
                        Specialization = model.Specialization,
                        DateRegistered = DateTime.Now,
                        IsActive = true,
                        UserId = user.Id,
                        Site = model.Site,
                        SiteUsername = siteUsername,
                        Group = model.Group
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

                    bool emailSent = await SendInstructorCredentialsEmail(model.Email, model.Password, model.FullName);

                    if (!emailSent)
                    {
                        System.Diagnostics.Debug.WriteLine($"Email sending failed for instructor: {model.Email}");
                    }

                    CreateNewStaffNotification("Instructor", model.FullName, instructor.Id, model.Site);
                    CreateDirectorNotification("Instructor", model.FullName, instructor.Id, model.Site);

                    TempData["SuccessMessage"] = $"Instructor {model.FullName} has been registered successfully! Employee ID: {employeeId}. {(emailSent ? "Credentials have been sent to their email." : "Credentials email failed to send.")}";
                    return RedirectToAction("ManageInstructors");
                }

                AddErrors(result);
            }

            return View(model);
        }

        private string GenerateEmployeeId()
        {
            string year = DateTime.Now.Year.ToString();
            int instructorCount = db.Instructors
                .Count(i => i.DateRegistered.Year == DateTime.Now.Year) + 1;
            return $"INST-{year}-{instructorCount:D3}";
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

        // GET: Admin/ManageInstructors
        public ActionResult ManageInstructors()
        {
            if (TempData["SuccessMessage"] != null)
            {
                ViewBag.SuccessMessage = TempData["SuccessMessage"];
            }

            var instructors = db.Instructors.OrderBy(i => i.FullName).ToList();
            return View(instructors);
        }

        // GET: Admin/EditInstructor/5
        public ActionResult EditInstructor(int id)
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
                EmployeeId = instructor.EmployeeId,
                Email = instructor.Email,
                PhoneNumber = instructor.PhoneNumber,
                Specialization = instructor.Specialization,
                Site = instructor.Site,
                Group = instructor.Group,
                IsActive = instructor.IsActive
            };

            return View(viewModel);
        }

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

                    instructor.FullName = model.FullName;
                    instructor.EmployeeId = model.EmployeeId;
                    instructor.Email = model.Email;
                    instructor.PhoneNumber = model.PhoneNumber;
                    instructor.Specialization = model.Specialization;
                    instructor.IsActive = model.IsActive;
                    instructor.Site = model.Site;
                    instructor.Group = model.Group;

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

        // GET: Admin/UpdateInstructorStatus/5
        public ActionResult UpdateInstructorStatus(int id)
        {
            var instructor = db.Instructors.Find(id);
            if (instructor == null)
            {
                return HttpNotFound();
            }
            return View(instructor);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> UpdateInstructorStatus(int id, bool isActive, string statusReason = null)
        {
            try
            {
                var instructor = db.Instructors.Find(id);
                if (instructor == null)
                {
                    TempData["ErrorMessage"] = "Instructor not found.";
                    return RedirectToAction("ManageInstructors");
                }

                var oldStatus = instructor.IsActive;
                instructor.IsActive = isActive;

                var user = await UserManager.FindByIdAsync(instructor.UserId);
                if (user != null)
                {
                    if (!isActive)
                    {
                        var removePasswordResult = await UserManager.RemovePasswordAsync(user.Id);
                        if (!removePasswordResult.Succeeded)
                        {
                            user.LockoutEnabled = true;
                            user.LockoutEndDateUtc = DateTime.UtcNow.AddYears(100);
                        }
                        user.EmailConfirmed = false;
                    }
                    else
                    {
                        user.LockoutEnabled = false;
                        user.LockoutEndDateUtc = null;
                        user.EmailConfirmed = true;

                        if (!await UserManager.HasPasswordAsync(user.Id))
                        {
                            var tempPassword = GenerateTemporaryPassword();
                            var addPasswordResult = await UserManager.AddPasswordAsync(user.Id, tempPassword);
                            if (addPasswordResult.Succeeded)
                            {
                                await SendReactivationEmail(instructor.Email, tempPassword, instructor.FullName);
                            }
                        }
                    }

                    await UserManager.UpdateAsync(user);
                }

                db.SaveChanges();

                if (oldStatus != isActive)
                {
                    var notification = new Notification
                    {
                        Title = "Instructor Status Updated",
                        Message = $"Instructor {instructor.FullName} status changed to {(isActive ? "Active" : "Inactive")}. Reason: {statusReason ?? "Not specified"}",
                        NotificationType = "Instructor",
                        CreatedAt = DateTime.Now,
                        IsRead = false,
                        UserId = "Admin",
                        UserType = "Admin"
                    };
                    db.Notifications.Add(notification);
                    db.SaveChanges();
                }

                TempData["SuccessMessage"] = $"Instructor status updated successfully to {(isActive ? "Active" : "Inactive")}. {(isActive ? "A reactivation email has been sent." : "They can no longer login.")}";
                return RedirectToAction("ManageInstructors");
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = "Error updating instructor status: " + ex.Message;
                return RedirectToAction("UpdateInstructorStatus", new { id = id });
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<JsonResult> UpdateInstructorStatusAjax(int id, bool isActive)
        {
            try
            {
                var instructor = db.Instructors.Find(id);
                if (instructor == null)
                {
                    return Json(new { success = false, message = "Instructor not found." });
                }

                var oldStatus = instructor.IsActive;
                instructor.IsActive = isActive;

                var user = await UserManager.FindByIdAsync(instructor.UserId);
                if (user != null)
                {
                    if (!isActive)
                    {
                        user.LockoutEnabled = true;
                        user.LockoutEndDateUtc = DateTime.UtcNow.AddYears(100);
                        user.EmailConfirmed = false;

                        if (await UserManager.HasPasswordAsync(user.Id))
                        {
                            await UserManager.RemovePasswordAsync(user.Id);
                        }
                    }
                    else
                    {
                        user.LockoutEnabled = false;
                        user.LockoutEndDateUtc = null;
                        user.EmailConfirmed = true;

                        var tempPassword = GenerateTemporaryPassword();
                        if (!await UserManager.HasPasswordAsync(user.Id))
                        {
                            var addPasswordResult = await UserManager.AddPasswordAsync(user.Id, tempPassword);
                            if (addPasswordResult.Succeeded)
                            {
                                await SendReactivationEmail(instructor.Email, tempPassword, instructor.FullName);
                            }
                        }
                    }
                    await UserManager.UpdateAsync(user);
                }

                db.SaveChanges();

                return Json(new
                {
                    success = true,
                    message = $"Instructor status updated to {(isActive ? "Active" : "Inactive")} successfully!",
                    newStatus = isActive ? "Active" : "Inactive"
                });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error updating instructor status: {ex.Message}");
                return Json(new { success = false, message = "Error updating status: " + ex.Message });
            }
        }

        private string GenerateTemporaryPassword()
        {
            const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789";
            var random = new Random();
            return new string(Enumerable.Repeat(chars, 10)
                .Select(s => s[random.Next(s.Length)]).ToArray());
        }

        private async Task SendReactivationEmail(string email, string tempPassword, string fullName)
        {
            try
            {
                string subject = "Your Pirana Security System Account Has Been Reactivated";
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
            <h2>Pirana Security System - Account Reactivated</h2>
        </div>
        <div class='content'>
            <h3>Dear {fullName},</h3>
            <p>Your instructor account has been reactivated. You can now login to the system.</p>
            
            <div class='credentials'>
                <strong>Email:</strong> {email}<br>
                <strong>Temporary Password:</strong> {tempPassword}
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
                System.Diagnostics.Debug.WriteLine($"Error sending reactivation email to {email}: {ex.Message}");
            }
        }

        // GET: Redirect to Payroll
        public ActionResult Payroll()
        {
            return RedirectToAction("Index", "Payroll");
        }

        // NOTIFICATION METHODS

        [HttpGet]
        public JsonResult GetNotifications()
        {
            try
            {
                var allNotifications = db.Notifications.ToList();
                var adminNotifications = allNotifications
                    .Where(n => n.UserType == "Admin" || n.UserId == "Admin")
                    .OrderByDescending(n => n.CreatedAt)
                    .Take(20)
                    .ToList();

                var notificationData = adminNotifications.Select(n => new
                {
                    Id = n.NotificationId,
                    Title = n.Title ?? "Notification",
                    Message = n.Message ?? "No message content",
                    IsRead = n.IsRead,
                    CreatedAt = n.CreatedAt.ToString("MMM dd, yyyy HH:mm"),
                    Type = n.NotificationType ?? "System",
                    RelatedUrl = n.RelatedUrl ?? "#",
                    TimeAgo = GetTimeAgo(n.CreatedAt)
                }).ToList();

                var unreadCount = adminNotifications.Count(n => !n.IsRead);

                return Json(new
                {
                    success = true,
                    notifications = notificationData,
                    unreadCount = unreadCount
                }, JsonRequestBehavior.AllowGet);
            }
            catch (Exception ex)
            {
                return Json(new
                {
                    success = true,
                    notifications = new List<object>(),
                    unreadCount = 0,
                    error = ex.Message
                }, JsonRequestBehavior.AllowGet);
            }
        }

        private string GetTimeAgo(DateTime date)
        {
            try
            {
                if (date == DateTime.MinValue || date.Year <= 2000)
                    return "Just now";

                TimeSpan timeSince = DateTime.Now - date;

                if (timeSince.TotalSeconds < 60)
                    return "Just now";
                else if (timeSince.TotalMinutes < 60)
                    return $"{(int)timeSince.TotalMinutes} minute{(timeSince.TotalMinutes >= 2 ? "s" : "")} ago";
                else if (timeSince.TotalHours < 24)
                    return $"{(int)timeSince.TotalHours} hour{(timeSince.TotalHours >= 2 ? "s" : "")} ago";
                else if (timeSince.TotalDays < 7)
                    return $"{(int)timeSince.TotalDays} day{(timeSince.TotalDays >= 2 ? "s" : "")} ago";
                else if (timeSince.TotalDays < 30)
                    return $"{(int)(timeSince.TotalDays / 7)} week{((int)(timeSince.TotalDays / 7) >= 2 ? "s" : "")} ago";
                else if (timeSince.TotalDays < 365)
                    return $"{(int)(timeSince.TotalDays / 30)} month{((int)(timeSince.TotalDays / 30) >= 2 ? "s" : "")} ago";
                else
                    return date.ToString("MMM dd, yyyy");
            }
            catch
            {
                return "Just now";
            }
        }

        [HttpPost]
        public JsonResult MarkNotificationAsRead(int id)
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

        [HttpPost]
        public JsonResult MarkAllNotificationsAsRead()
        {
            try
            {
                var allNotifications = db.Notifications.ToList();
                var unreadNotifications = allNotifications
                    .Where(n => (n.UserType == "Admin" || n.UserId == "Admin") && !n.IsRead)
                    .ToList();

                foreach (var notification in unreadNotifications)
                {
                    notification.IsRead = true;
                    notification.DateRead = DateTime.Now;
                }

                db.SaveChanges();
                return Json(new { success = true, message = $"{unreadNotifications.Count} notifications marked as read" });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, error = ex.Message });
            }
        }

        private void CreateNewStaffNotification(string staffType, string staffName, int staffId, string site = null)
        {
            try
            {
                var notification = new Notification
                {
                    Title = $"New {staffType} Registered",
                    Message = $"You registered a {staffType.ToLower()} ({staffName}) with ID: {staffId} at {DateTime.Now.ToString("hh:mm tt")} on {DateTime.Now.ToString("MMM dd, yyyy")}" +
                             (string.IsNullOrEmpty(site) ? "" : $" at {site}"),
                    NotificationType = staffType,
                    RelatedUrl = staffType.ToLower() == "guard"
                        ? Url.Action("ManageGuards", "Admin")
                        : Url.Action("ManageInstructors", "Admin"),
                    UserId = "Admin",
                    UserType = "Admin",
                    CreatedAt = DateTime.Now,
                    IsRead = false,
                    Source = "Registration"
                };

                db.Notifications.Add(notification);
                db.SaveChanges();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error creating notification: {ex.Message}");
            }
        }

        private void CreateDirectorNotification(string staffType, string staffName, int staffId, string site = null)
        {
            try
            {
                var notification = new Notification
                {
                    Title = $"New {staffType} Registration",
                    Message = $"A new {staffType.ToLower()} ({staffName}) with ID: {staffId} was registered at {DateTime.Now.ToString("hh:mm tt")} on {DateTime.Now.ToString("MMM dd, yyyy")}" +
                             (string.IsNullOrEmpty(site) ? "" : $" at {site}"),
                    NotificationType = staffType,
                    RelatedUrl = staffType.ToLower() == "guard"
                        ? Url.Action("ManageGuards", "Admin")
                        : Url.Action("ManageInstructors", "Admin"),
                    UserId = "Director",
                    UserType = "Director",
                    CreatedAt = DateTime.Now,
                    IsRead = false,
                    Source = "Registration"
                };

                db.Notifications.Add(notification);
                db.SaveChanges();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error creating director notification: {ex.Message}");
            }
        }

        // FIXED EMAIL METHODS

        private async Task<bool> SendGuardCredentialsEmail(string email, string password, string fullName)
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

                return await SendEmailAsync(email, subject, body);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error sending email to {email}: {ex.Message}");
                return false;
            }
        }

        private async Task<bool> SendInstructorCredentialsEmail(string email, string password, string fullName)
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

                return await SendEmailAsync(email, subject, body);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error sending email to {email}: {ex.Message}");
                return false;
            }
        }

        private async Task<bool> SendEmailAsync(string toEmail, string subject, string body)
        {
            try
            {
                // Get configuration from Web.config
                string fromEmail = ConfigurationManager.AppSettings["EmailFrom"] ?? "g54759296@gmail.com";
                string smtpServer = ConfigurationManager.AppSettings["SmtpServer"] ?? "smtp.gmail.com";
                int smtpPort = int.Parse(ConfigurationManager.AppSettings["SmtpPort"] ?? "587");
                string smtpUsername = ConfigurationManager.AppSettings["SmtpUsername"] ?? "g54759296@gmail.com";
                string smtpPassword = ConfigurationManager.AppSettings["SmtpPassword"] ?? "byzycmehhdaargqi";
                bool enableSsl = bool.Parse(ConfigurationManager.AppSettings["EnableSsl"] ?? "true");
                string displayName = ConfigurationManager.AppSettings["EmailDisplayName"] ?? "Pirana Security System";
                int timeout = int.Parse(ConfigurationManager.AppSettings["SmtpTimeout"] ?? "30000");

                using (var message = new MailMessage())
                {
                    message.From = new MailAddress(fromEmail, displayName);
                    message.To.Add(new MailAddress(toEmail));
                    message.Subject = subject;
                    message.Body = body;
                    message.IsBodyHtml = true;

                    // Add reply-to header
                    message.ReplyToList.Add(new MailAddress(fromEmail, displayName));

                    using (var smtpClient = new SmtpClient(smtpServer, smtpPort))
                    {
                        smtpClient.Credentials = new NetworkCredential(smtpUsername, smtpPassword);
                        smtpClient.EnableSsl = enableSsl;
                        smtpClient.Timeout = timeout;
                        smtpClient.DeliveryMethod = SmtpDeliveryMethod.Network;

                        // For Gmail, you might need to configure these additional settings
                        ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;
                        ServicePointManager.ServerCertificateValidationCallback = (s, certificate, chain, sslPolicyErrors) => true;

                        await smtpClient.SendMailAsync(message);
                    }
                }

                System.Diagnostics.Debug.WriteLine($"Email sent successfully to: {toEmail}");
                return true;
            }
            catch (SmtpException smtpEx)
            {
                System.Diagnostics.Debug.WriteLine($"SMTP Error sending email to {toEmail}: {smtpEx.Message}");
                System.Diagnostics.Debug.WriteLine($"SMTP Status Code: {smtpEx.StatusCode}");
                if (smtpEx.InnerException != null)
                {
                    System.Diagnostics.Debug.WriteLine($"Inner Exception: {smtpEx.InnerException.Message}");
                }
                return false;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"General Error sending email to {toEmail}: {ex.Message}");
                if (ex.InnerException != null)
                {
                    System.Diagnostics.Debug.WriteLine($"Inner Exception: {ex.InnerException.Message}");
                }
                return false;
            }
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