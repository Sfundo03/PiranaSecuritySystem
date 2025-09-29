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
                // Initialize site options
                SiteOptions = new List<SelectListItem>
                {
                    new SelectListItem { Value = "Site A", Text = "Site A" },
                    new SelectListItem { Value = "Site B", Text = "Site B" },
                    new SelectListItem { Value = "Site C", Text = "Site C" }
                }
            };
            return View(model);
        }

        // POST: Admin/RegisterGuard - UPDATED VERSION
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> RegisterGuard(RegisterGuardViewModel model)
        {
            // Ensure site options are populated if we need to return the view
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
                        Site = model.Site, // Save the site
                        SiteUsername = siteUsername // Save the site-specific username
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

                    // Send email with credentials - UPDATED: Don't await, fire and forget
                    string fullName = $"{model.Guard_FName} {model.Guard_LName}";

                    // Fire and forget email sending to prevent hanging
                    _ = Task.Run(async () =>
                    {
                        try
                        {
                            await SendGuardCredentialsEmail(model.Email, model.Password, fullName);
                        }
                        catch (Exception ex)
                        {
                            System.Diagnostics.Debug.WriteLine($"Email sending failed (non-critical): {ex.Message}");
                            // Don't throw - email failure shouldn't prevent registration
                        }
                    });

                    // Create notification for director
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

        // Helper method to generate site username
        private string GenerateSiteUsername(string sitePrefix)
        {
            // Get all existing usernames for this site
            var existingUsernames = db.Guards
                .Where(g => g.SiteUsername.StartsWith(sitePrefix))
                .Select(g => g.SiteUsername)
                .ToList();

            int nextNumber = 1;

            if (existingUsernames.Any())
            {
                // Extract the number part from all existing usernames and find the maximum
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

            // Format with leading zeros (001, 002, etc.)
            return $"{sitePrefix}{nextNumber:D3}";
        }

        // GET: Admin/ManageGuards
        public ActionResult ManageGuards()
        {
            // Check for success message from registration
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

                // Store old status for notification
                var oldStatus = guard.IsActive;
                guard.IsActive = isActive;

                // Update the user account status as well if needed
                var user = db.Users.FirstOrDefault(u => u.Id == guard.UserId);
                if (user != null)
                {
                    // Check if ApplicationUser has IsActive property using reflection
                    var isActiveProperty = user.GetType().GetProperty("IsActive");
                    if (isActiveProperty != null && isActiveProperty.CanWrite)
                    {
                        isActiveProperty.SetValue(user, isActive);
                    }
                    // If not, we'll just update the guard status without affecting the user
                }

                db.SaveChanges();

                // Create notification about status change
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

        // NEW: Simple status update method for AJAX calls from the ManageGuards view
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

                // Store old status
                var oldStatus = guard.IsActive;
                guard.IsActive = isActive;

                // Update user account to prevent/login allow login
                var user = await UserManager.FindByIdAsync(guard.UserId);
                if (user != null)
                {
                    if (!isActive)
                    {
                        // Deactivate - prevent login
                        user.LockoutEnabled = true;
                        user.LockoutEndDateUtc = DateTime.UtcNow.AddYears(100);
                        user.EmailConfirmed = false;

                        // Also try to remove password as additional security
                        if (await UserManager.HasPasswordAsync(user.Id))
                        {
                            await UserManager.RemovePasswordAsync(user.Id);
                        }
                    }
                    else
                    {
                        // Reactivate - allow login
                        user.LockoutEnabled = false;
                        user.LockoutEndDateUtc = null;
                        user.EmailConfirmed = true;

                        // Generate and set a new temporary password
                        var tempPassword = GenerateTemporaryPassword();
                        if (!await UserManager.HasPasswordAsync(user.Id))
                        {
                            var addPasswordResult = await UserManager.AddPasswordAsync(user.Id, tempPassword);
                            if (addPasswordResult.Succeeded)
                            {
                                // Send reactivation email with temporary password
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
                // Log the error
                System.Diagnostics.Debug.WriteLine($"Error updating guard status: {ex.Message}");
                return Json(new { success = false, message = "Error updating status: " + ex.Message });
            }
        }

        // Helper method to send guard reactivation email
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

                    // Update guard properties
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

                    // Also update the associated user's email if it exists
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
                // Initialize site options
                SiteOptions = new List<SelectListItem>
                {
                    new SelectListItem { Value = "Site A", Text = "Site A" },
                    new SelectListItem { Value = "Site B", Text = "Site B" },
                    new SelectListItem { Value = "Site C", Text = "Site C" }
                },
                // Initialize group options
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

        // POST: Admin/RegisterInstructor - UPDATED VERSION
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> RegisterInstructor(RegisterInstructorViewModel model)
        {
            // Ensure site options and group options are populated if we need to return the view
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

                    // Auto-generate Employee ID
                    string employeeId = GenerateEmployeeId();

                    var instructor = new Instructor
                    {
                        EmployeeId = employeeId, // Use auto-generated ID instead of model.EmployeeId
                        FullName = model.FullName,
                        Email = model.Email,
                        PhoneNumber = model.PhoneNumber,
                        Specialization = model.Specialization,
                        DateRegistered = DateTime.Now,
                        IsActive = true,
                        UserId = user.Id,
                        Site = model.Site, // Save the site
                        SiteUsername = siteUsername, // Save the site-specific user
                        Group = model.Group // Add Group property
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

                    // Send email with credentials - UPDATED: Don't await, fire and forget
                    _ = Task.Run(async () =>
                    {
                        try
                        {
                            await SendInstructorCredentialsEmail(model.Email, model.Password, model.FullName);
                        }
                        catch (Exception ex)
                        {
                            System.Diagnostics.Debug.WriteLine($"Email sending failed (non-critical): {ex.Message}");
                            // Don't throw - email failure shouldn't prevent registration
                        }
                    });

                    // Create notification for director
                    CreateNewStaffNotification("Instructor", model.FullName);

                    TempData["SuccessMessage"] = $"Instructor {model.FullName} has been registered successfully! Employee ID: {employeeId}. Credentials have been sent to their email.";
                    return RedirectToAction("ManageInstructors");
                }

                AddErrors(result);
            }

            return View(model);
        }

        // Add this method to generate Employee ID
        private string GenerateEmployeeId()
        {
            // Get the current year
            string year = DateTime.Now.Year.ToString();

            // Get the count of instructors for this year
            int instructorCount = db.Instructors
                .Count(i => i.DateRegistered.Year == DateTime.Now.Year) + 1;

            // Format: INST-YYYY-001, INST-YYYY-002, etc.
            return $"INST-{year}-{instructorCount:D3}";
        }

        // Add these helper methods for instructors
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
            // Get all existing usernames for this site
            var existingUsernames = db.Instructors
                .Where(i => i.SiteUsername.StartsWith(sitePrefix))
                .Select(i => i.SiteUsername)
                .ToList();

            int nextNumber = 1;
            if (existingUsernames.Any())
            {
                // Extract the number part from all existing usernames and find the maximum
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
            // Format with leading zeros (001, 002, etc.)
            return $"{sitePrefix}{nextNumber:D3}";
        }

        // GET: Admin/ManageInstructors
        public ActionResult ManageInstructors()
        {
            // Check for success message from registration
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

            // Map Instructor to EditInstructorViewModel
            var viewModel = new EditInstructorViewModel
            {
                Id = instructor.Id,
                FullName = instructor.FullName,
                EmployeeId = instructor.EmployeeId, // Add this line
                Email = instructor.Email,
                PhoneNumber = instructor.PhoneNumber,
                Specialization = instructor.Specialization,
                Site = instructor.Site,
                Group = instructor.Group,
                IsActive = instructor.IsActive
                // SiteOptions and GroupOptions are already set in the constructor
            };

            return View(viewModel);
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

                    // Update instructor properties
                    instructor.FullName = model.FullName;
                    instructor.EmployeeId = model.EmployeeId; // Add this line
                    instructor.Email = model.Email;
                    instructor.PhoneNumber = model.PhoneNumber;
                    instructor.Specialization = model.Specialization;
                    instructor.IsActive = model.IsActive;
                    instructor.Site = model.Site;
                    instructor.Group = model.Group;

                    // Update user email if changed
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

        // POST: Admin/UpdateInstructorStatus/5 - UPDATED VERSION
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

                // Store old status for notification
                var oldStatus = instructor.IsActive;
                instructor.IsActive = isActive;

                // Update the user account status to prevent login
                var user = await UserManager.FindByIdAsync(instructor.UserId);
                if (user != null)
                {
                    if (!isActive)
                    {
                        // Deactivate the user account - prevent login
                        // Method 1: Remove password (most effective)
                        var removePasswordResult = await UserManager.RemovePasswordAsync(user.Id);
                        if (!removePasswordResult.Succeeded)
                        {
                            // If removing password fails, try locking out
                            user.LockoutEnabled = true;
                            user.LockoutEndDateUtc = DateTime.UtcNow.AddYears(100); // Lock for 100 years
                        }

                        // Method 2: Add to archived role or mark as archived
                        user.EmailConfirmed = false; // This will prevent login
                    }
                    else
                    {
                        // Reactivate the user account
                        user.LockoutEnabled = false;
                        user.LockoutEndDateUtc = null;
                        user.EmailConfirmed = true;

                        // If password was removed, set a temporary one
                        if (!await UserManager.HasPasswordAsync(user.Id))
                        {
                            var tempPassword = GenerateTemporaryPassword();
                            var addPasswordResult = await UserManager.AddPasswordAsync(user.Id, tempPassword);
                            if (addPasswordResult.Succeeded)
                            {
                                // Send email with new temporary password
                                await SendReactivationEmail(instructor.Email, tempPassword, instructor.FullName);
                            }
                        }
                    }

                    await UserManager.UpdateAsync(user);
                }

                db.SaveChanges();

                // Create notification about status change
                if (oldStatus != isActive)
                {
                    var notification = new Notification
                    {
                        Title = "Instructor Status Updated",
                        Message = $"Instructor {instructor.FullName} status changed to {(isActive ? "Active" : "Inactive")}. Reason: {statusReason ?? "Not specified"}",
                        NotificationType = "Instructor",
                        CreatedAt = DateTime.Now,
                        IsRead = false,
                        UserId = "Admin"
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

        // NEW: Simple status update method for AJAX calls from the ManageInstructors view
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

                // Store old status
                var oldStatus = instructor.IsActive;
                instructor.IsActive = isActive;

                // Update user account to prevent/login allow login
                var user = await UserManager.FindByIdAsync(instructor.UserId);
                if (user != null)
                {
                    if (!isActive)
                    {
                        // Deactivate - prevent login
                        user.LockoutEnabled = true;
                        user.LockoutEndDateUtc = DateTime.UtcNow.AddYears(100);
                        user.EmailConfirmed = false;

                        // Also try to remove password as additional security
                        if (await UserManager.HasPasswordAsync(user.Id))
                        {
                            await UserManager.RemovePasswordAsync(user.Id);
                        }
                    }
                    else
                    {
                        // Reactivate - allow login
                        user.LockoutEnabled = false;
                        user.LockoutEndDateUtc = null;
                        user.EmailConfirmed = true;

                        // Generate and set a new temporary password
                        var tempPassword = GenerateTemporaryPassword();
                        if (!await UserManager.HasPasswordAsync(user.Id))
                        {
                            var addPasswordResult = await UserManager.AddPasswordAsync(user.Id, tempPassword);
                            if (addPasswordResult.Succeeded)
                            {
                                // Send reactivation email with temporary password
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
                // Log the error
                System.Diagnostics.Debug.WriteLine($"Error updating instructor status: {ex.Message}");
                return Json(new { success = false, message = "Error updating status: " + ex.Message });
            }
        }

        // Helper method to generate temporary password
        private string GenerateTemporaryPassword()
        {
            const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789";
            var random = new Random();
            return new string(Enumerable.Repeat(chars, 10)
                .Select(s => s[random.Next(s.Length)]).ToArray());
        }

        // Helper method to send reactivation email
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

        private void CreateNewStaffNotification(string staffType, string staffName)
        {
            try
            {
                var notification = new Notification
                {
                    Title = $"New {staffType} Added",
                    Message = $"A new {staffType.ToLower()} ({staffName}) has been registered in the system",
                    NotificationType = staffType.ToLower(),
                    RelatedUrl = staffType.ToLower() == "guard"
                        ? Url.Action("ManageGuards", "Admin")
                        : Url.Action("ManageInstructors", "Admin"),
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
                System.Diagnostics.Debug.WriteLine($"Error creating notification: {ex.Message}");
                // Don't throw, just log the error
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
                // Don't throw - email failure shouldn't prevent registration
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
                // Don't throw - email failure shouldn't prevent registration
            }
        }

        // UPDATED: Email sending method with timeout and better error handling
        private async Task SendEmailAsync(string toEmail, string subject, string body)
        {
            try
            {
                // Configure these settings in your web.config or app settings
                string fromEmail = ConfigurationManager.AppSettings["EmailFrom"] ?? "noreply@piranasecurity.com";
                string smtpServer = ConfigurationManager.AppSettings["SmtpServer"] ?? "smtp.gmail.com";
                int smtpPort = int.Parse(ConfigurationManager.AppSettings["SmtpPort"] ?? "587");
                string smtpUsername = ConfigurationManager.AppSettings["SmtpUsername"] ?? "your-email@gmail.com";
                string smtpPassword = ConfigurationManager.AppSettings["SmtpPassword"] ?? "your-app-password";
                bool enableSsl = bool.Parse(ConfigurationManager.AppSettings["EnableSsl"] ?? "true");

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
                        smtpClient.EnableSsl = enableSsl;
                        smtpClient.Timeout = 30000; // 30 seconds timeout

                        await smtpClient.SendMailAsync(message);
                    }
                }

                System.Diagnostics.Debug.WriteLine($"Email sent successfully to: {toEmail}");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error sending email to {toEmail}: {ex.Message}");
                // Don't throw - let the calling method handle gracefully
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