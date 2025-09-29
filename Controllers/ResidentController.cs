using Microsoft.AspNet.Identity;
using Microsoft.AspNet.Identity.EntityFramework;
using Microsoft.AspNet.Identity.Owin;
using PiranaSecuritySystem.Models;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Data.Entity.Validation;
using System.Linq;
using System.Threading.Tasks;
using System.Web;
using System.Web.Mvc;

namespace PiranaSecuritySystem.Controllers
{
    public class ResidentController : Controller
    {
        private ApplicationDbContext db = new ApplicationDbContext();
        private ApplicationUserManager _userManager;
        private RoleManager<IdentityRole> _roleManager;

        public ResidentController()
        {
            _userManager = new ApplicationUserManager(new UserStore<ApplicationUser>(db));
            _roleManager = new RoleManager<IdentityRole>(new RoleStore<IdentityRole>(db));
        }

        public ResidentController(ApplicationUserManager userManager, RoleManager<IdentityRole> roleManager)
        {
            _userManager = userManager;
            _roleManager = roleManager;
        }

        public ApplicationUserManager UserManager
        {
            get
            {
                return _userManager ?? HttpContext.GetOwinContext().GetUserManager<ApplicationUserManager>();
            }
            private set
            {
                _userManager = value;
            }
        }

        public RoleManager<IdentityRole> RoleManager
        {
            get
            {
                return _roleManager ?? new RoleManager<IdentityRole>(new RoleStore<IdentityRole>(db));
            }
            private set
            {
                _roleManager = value;
            }
        }

        // GET: Resident/Register
        public ActionResult Register()
        {
            // If user is already logged in, redirect to dashboard
            if (User.Identity.IsAuthenticated)
            {
                return RedirectToAction("Dashboard");
            }
            return View();
        }

        // POST: Resident/Register
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> Register(RegisterResidentViewModel model)
        {
            try
            {
                if (ModelState.IsValid)
                {
                    // Check if email already exists
                    var existingUser = await UserManager.FindByEmailAsync(model.Email);
                    if (existingUser != null)
                    {
                        ViewBag.ErrorMessage = "Email already registered. Please use a different email address.";
                        return View(model);
                    }

                    // Create new resident (as ApplicationUser)
                    var resident = new ApplicationUser
                    {
                        UserName = model.Email,
                        Email = model.Email,
                        FullName = model.FullName?.Trim(),
                        PhoneNumber = model.PhoneNumber?.Trim(),
                        IsActive = true,
                        CreatedAt = DateTime.Now
                    };

                    // Create user with password
                    var result = await UserManager.CreateAsync(resident, model.Password);

                    if (result.Succeeded)
                    {
                        // Ensure Resident role exists
                        if (!await RoleManager.RoleExistsAsync("Resident"))
                        {
                            var roleResult = await RoleManager.CreateAsync(new IdentityRole("Resident"));
                            if (!roleResult.Succeeded)
                            {
                                // Log role creation error but continue with user creation
                                System.Diagnostics.Debug.WriteLine("Failed to create Resident role: " +
                                    string.Join("; ", roleResult.Errors));
                            }
                        }

                        // Try to add to Resident role
                        try
                        {
                            var addToRoleResult = await UserManager.AddToRoleAsync(resident.Id, "Resident");
                            if (!addToRoleResult.Succeeded)
                            {
                                System.Diagnostics.Debug.WriteLine("Failed to assign Resident role: " +
                                    string.Join("; ", addToRoleResult.Errors));
                            }
                        }
                        catch (Exception roleEx)
                        {
                            System.Diagnostics.Debug.WriteLine("Role assignment error: " + roleEx.Message);
                            // Continue with registration even if role assignment fails
                        }

                        // Store additional resident info in separate table
                        var residentInfo = new ResidentInfo
                        {
                            UserId = resident.Id,
                            Address = model.Address?.Trim(),
                            UnitNumber = model.UnitNumber?.Trim(),
                            EmergencyContact = model.EmergencyContact?.Trim(),
                            DateRegistered = DateTime.Now
                        };
                        db.ResidentInfos.Add(residentInfo);

                        // Create success notification
                        var notification = new Notification
                        {
                            UserId = resident.Id,
                            UserType = "Resident",
                            Message = "Welcome to Pirana Security System! Your account has been created successfully.",
                            IsRead = false,
                            CreatedAt = DateTime.Now,
                            RelatedUrl = Url.Action("Dashboard", "Resident")
                        };
                        db.Notifications.Add(notification);

                        // Notify directors about new registration
                        try
                        {
                            var directors = db.Directors.Where(d => d.IsActive).ToList();
                            foreach (var director in directors)
                            {
                                var directorNotification = new Notification
                                {
                                    UserId = director.DirectorId.ToString(),
                                    UserType = "Director",
                                    Message = $"New resident registered: {resident.FullName} ({resident.Email})",
                                    IsRead = false,
                                    CreatedAt = DateTime.Now,
                                    RelatedUrl = Url.Action("ResidentDetails", "Director", new { id = resident.Id })
                                };
                                db.Notifications.Add(directorNotification);
                            }
                        }
                        catch (Exception notifyEx)
                        {
                            System.Diagnostics.Debug.WriteLine("Director notification error: " + notifyEx.Message);
                            // Continue even if director notifications fail
                        }

                        try
                        {
                            await db.SaveChangesAsync();
                        }
                        catch (Exception saveEx)
                        {
                            System.Diagnostics.Debug.WriteLine("Save changes error: " + saveEx.Message);
                            // User is created, so we can continue
                        }

                        TempData["SuccessMessage"] = "Registration successful! Please login with your credentials.";
                        return RedirectToAction("Login", "Account"); // Redirect to Account/Login
                    }
                    else
                    {
                        // Format error messages for better user experience
                        var errorList = result.Errors.ToList();
                        var userFriendlyErrors = new List<string>();

                        foreach (var error in errorList)
                        {
                            if (error.Contains("Password"))
                                userFriendlyErrors.Add("Password must be at least 6 characters and contain at least one uppercase letter, one lowercase letter, one digit, and one special character.");
                            else if (error.Contains("Email"))
                                userFriendlyErrors.Add("Please enter a valid email address.");
                            else
                                userFriendlyErrors.Add(error);
                        }

                        ViewBag.ErrorMessage = string.Join("<br/>", userFriendlyErrors);
                        return View(model);
                    }
                }

                // If we got this far, something failed, redisplay form
                ViewBag.ErrorMessage = "Please correct the validation errors below.";
                return View(model);
            }
            catch (DbEntityValidationException ex)
            {
                // Log detailed validation errors
                var errorMessages = new List<string>();
                foreach (var validationErrors in ex.EntityValidationErrors)
                {
                    foreach (var validationError in validationErrors.ValidationErrors)
                    {
                        errorMessages.Add($"{validationError.PropertyName}: {validationError.ErrorMessage}");
                    }
                }

                System.Diagnostics.Debug.WriteLine("Validation errors: " + string.Join("; ", errorMessages));
                ViewBag.ErrorMessage = "Please correct the following errors: " + string.Join("; ", errorMessages);
                return View(model);
            }
            catch (System.Data.Entity.Infrastructure.DbUpdateException dbEx)
            {
                System.Diagnostics.Debug.WriteLine("Database update error: " + dbEx.Message);
                if (dbEx.InnerException != null)
                {
                    System.Diagnostics.Debug.WriteLine("Inner exception: " + dbEx.InnerException.Message);
                    // Check for specific database errors
                    if (dbEx.InnerException.Message.Contains("PK_") || dbEx.InnerException.Message.Contains("primary key"))
                    {
                        ViewBag.ErrorMessage = "Database configuration error. Please contact administrator.";
                    }
                    else if (dbEx.InnerException.Message.Contains("FK_") || dbEx.InnerException.Message.Contains("foreign key"))
                    {
                        ViewBag.ErrorMessage = "Reference error. Please check your input data.";
                    }
                    else
                    {
                        ViewBag.ErrorMessage = "Database error occurred. Please check your input data and try again.";
                    }
                }
                else
                {
                    ViewBag.ErrorMessage = "Database error occurred. Please check your input data and try again.";
                }
                return View(model);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("Registration error: " + ex.Message);
                if (ex.InnerException != null)
                {
                    System.Diagnostics.Debug.WriteLine("Inner exception: " + ex.InnerException.Message);
                }
                ViewBag.ErrorMessage = "An unexpected error occurred during registration. Please try again later.";
                return View(model);
            }
        }

        // GET: Resident/Index
        public ActionResult Index()
        {
            // If user is already logged in, redirect to dashboard
            if (User.Identity.IsAuthenticated)
            {
                return RedirectToAction("Dashboard");
            }

            // Clear any existing messages
            TempData.Remove("ErrorMessage");
            TempData.Remove("SuccessMessage");

            return View();
        }

        // GET: Resident/Dashboard
        [Authorize(Roles = "Resident")]
        public ActionResult Dashboard()
        {
            try
            {
                var residentId = User.Identity.GetUserId();
                var resident = UserManager.FindById(residentId);

                if (resident == null)
                {
                    TempData["ErrorMessage"] = "Resident not found.";
                    return RedirectToAction("Index");
                }

                // Get resident info
                var residentInfo = db.ResidentInfos.FirstOrDefault(r => r.UserId == residentId);

                // Get resident's incidents
                var recentIncidents = db.IncidentReports
                    .Where(i => i.ResidentId == residentId)
                    .OrderByDescending(i => i.ReportDate)
                    .Take(5)
                    .ToList();

                // Create dashboard view model
                var viewModel = new ResidentDashboardViewModel
                {
                    Resident = resident,
                    ResidentInfo = residentInfo,
                    RecentIncidents = recentIncidents,
                    TotalIncidents = db.IncidentReports.Count(i => i.ResidentId == residentId),
                    PendingIncidents = db.IncidentReports.Count(i => i.ResidentId == residentId && i.Status == "Pending"),
                    InProgressIncidents = db.IncidentReports.Count(i => i.ResidentId == residentId && i.Status == "In Progress"),
                    ResolvedIncidents = db.IncidentReports.Count(i => i.ResidentId == residentId && i.Status == "Resolved")
                };

                return View(viewModel);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("Dashboard error: " + ex.Message);
                TempData["ErrorMessage"] = "An error occurred while loading the dashboard.";
                return RedirectToAction("Index");
            }
        }

        // GET: Resident/Profile
        [Authorize(Roles = "Resident")]
        public new ActionResult Profile()
        {
            try
            {
                var residentId = User.Identity.GetUserId();
                var resident = UserManager.FindById(residentId);

                if (resident == null)
                {
                    TempData["ErrorMessage"] = "Resident not found.";
                    return RedirectToAction("Dashboard");
                }

                // Get resident info
                var residentInfo = db.ResidentInfos.FirstOrDefault(r => r.UserId == residentId);

                var model = new UpdateResidentViewModel
                {
                    FullName = resident.FullName,
                    Email = resident.Email,
                    PhoneNumber = resident.PhoneNumber,
                    EmergencyContact = residentInfo?.EmergencyContact,
                    Address = residentInfo?.Address,
                    UnitNumber = residentInfo?.UnitNumber
                };

                return View(model);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("Profile error: " + ex.Message);
                TempData["ErrorMessage"] = "An error occurred while loading your profile.";
                return RedirectToAction("Dashboard");
            }
        }

        // POST: Resident/UpdateProfile
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Resident")]
        public async Task<ActionResult> UpdateProfile(UpdateResidentViewModel model)
        {
            try
            {
                var residentId = User.Identity.GetUserId();
                var resident = await UserManager.FindByIdAsync(residentId);

                if (resident == null)
                {
                    TempData["ErrorMessage"] = "Resident not found.";
                    return RedirectToAction("Dashboard");
                }

                if (ModelState.IsValid)
                {
                    // Update resident details
                    resident.FullName = model.FullName?.Trim();
                    resident.Email = model.Email?.Trim();
                    resident.PhoneNumber = model.PhoneNumber?.Trim();

                    var result = await UserManager.UpdateAsync(resident);
                    if (!result.Succeeded)
                    {
                        TempData["ErrorMessage"] = "Failed to update details: " + string.Join("; ", result.Errors);
                        return RedirectToAction("Profile");
                    }

                    // Update resident info
                    var residentInfo = db.ResidentInfos.FirstOrDefault(r => r.UserId == residentId);
                    if (residentInfo != null)
                    {
                        residentInfo.EmergencyContact = model.EmergencyContact?.Trim();
                        residentInfo.Address = model.Address?.Trim();
                        residentInfo.UnitNumber = model.UnitNumber?.Trim();
                    }
                    else
                    {
                        // Create new resident info if it doesn't exist
                        residentInfo = new ResidentInfo
                        {
                            UserId = residentId,
                            EmergencyContact = model.EmergencyContact?.Trim(),
                            Address = model.Address?.Trim(),
                            UnitNumber = model.UnitNumber?.Trim(),
                            DateRegistered = DateTime.Now
                        };
                        db.ResidentInfos.Add(residentInfo);
                    }

                    await db.SaveChangesAsync();

                    // Create notification for profile update
                    var notification = new Notification
                    {
                        UserId = residentId,
                        UserType = "Resident",
                        Message = "Your profile details have been updated successfully.",
                        IsRead = false,
                        CreatedAt = DateTime.Now,
                        RelatedUrl = Url.Action("Dashboard", "Resident")
                    };
                    db.Notifications.Add(notification);
                    await db.SaveChangesAsync();

                    TempData["SuccessMessage"] = "Your details have been updated successfully!";
                    return RedirectToAction("Profile");
                }

                // If we got this far, something failed
                return View("Profile", model);
            }
            catch (DbEntityValidationException ex)
            {
                var errorMessages = ex.EntityValidationErrors
                    .SelectMany(x => x.ValidationErrors)
                    .Select(x => x.ErrorMessage);

                System.Diagnostics.Debug.WriteLine("Validation errors: " + string.Join("; ", errorMessages));
                TempData["ErrorMessage"] = "Validation failed: " + string.Join("; ", errorMessages);
                return RedirectToAction("Profile");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("UpdateProfile error: " + ex.Message);
                TempData["ErrorMessage"] = "An error occurred while updating your details.";
                return RedirectToAction("Profile");
            }
        }

        // GET: Resident/ReportIncident
        [Authorize(Roles = "Resident")]
        public ActionResult ReportIncident()
        {
            try
            {
                var residentId = User.Identity.GetUserId();
                var resident = UserManager.FindById(residentId);

                if (resident == null)
                {
                    TempData["ErrorMessage"] = "Resident not found.";
                    return RedirectToAction("Dashboard");
                }

                // Get resident info for location/address
                var residentInfo = db.ResidentInfos.FirstOrDefault(r => r.UserId == residentId);

                var model = new ReportIncidentViewModel
                {
                    ResidentId = residentId,
                    EmergencyContact = resident.PhoneNumber,
                    Location = residentInfo?.Address
                };

                // Populate locations for dropdown
                ViewBag.Locations = GetCommonLocations();

                return View(model);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("ReportIncident GET error: " + ex.Message);
                TempData["ErrorMessage"] = "An error occurred while loading the incident report form.";
                return RedirectToAction("Dashboard");
            }
        }

        // Helper method to get common locations
        private List<SelectListItem> GetCommonLocations()
        {
            return new List<SelectListItem>
    {
        new SelectListItem { Value = "Main Entrance", Text = "Main Entrance" },
        new SelectListItem { Value = "Parking Lot", Text = "Parking Lot" },
        new SelectListItem { Value = "Lobby Area", Text = "Lobby Area" },
        new SelectListItem { Value = "Elevator", Text = "Elevator" },
        new SelectListItem { Value = "Stairwell", Text = "Stairwell" },
        new SelectListItem { Value = "Common Room", Text = "Common Room" },
        new SelectListItem { Value = "Gym", Text = "Gym" },
        new SelectListItem { Value = "Pool Area", Text = "Pool Area" },
        new SelectListItem { Value = "Park", Text = "Park" },
        new SelectListItem { Value = "Other", Text = "Other" }
    };
        }

        // POST: Resident/ReportIncident
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Resident")]
        public async Task<ActionResult> ReportIncident(ReportIncidentViewModel model)
        {
            try
            {
                // Repopulate locations for dropdown in case of validation errors
                ViewBag.Locations = GetCommonLocations();

                if (ModelState.IsValid)
                {
                    var resident = await UserManager.FindByIdAsync(model.ResidentId);
                    if (resident == null)
                    {
                        TempData["ErrorMessage"] = "Resident not found. Please log in again.";
                        return View(model);
                    }

                    // Create new incident report
                    var incident = new IncidentReport
                    {
                        ResidentId = model.ResidentId,
                        IncidentType = model.IncidentType,
                        Description = model.Description,
                        Location = model.Location,
                        EmergencyContact = model.EmergencyContact,
                        ReportDate = DateTime.Now,
                        Status = "Pending",
                        Priority = model.Priority,
                        ReportedBy = "Resident",
                        CreatedBy = resident.FullName,
                        CreatedDate = DateTime.Now,
                    };

                    db.IncidentReports.Add(incident);
                    await db.SaveChangesAsync();

                    // Create notification for the resident
                    var residentNotification = new Notification
                    {
                        UserId = model.ResidentId,
                        UserType = "Resident",
                        Message = $"Your incident report (#{incident.IncidentReportId}) has been submitted successfully.",
                        IsRead = false,
                        CreatedAt = DateTime.Now,
                        RelatedUrl = Url.Action("MyIncidents", "Resident")
                    };
                    db.Notifications.Add(residentNotification);

                    // Notify directors about new incident report
                    try
                    {
                        var directors = db.Directors.Where(d => d.IsActive).ToList();
                        foreach (var director in directors)
                        {
                            var directorNotification = new Notification
                            {
                                UserId = director.DirectorId.ToString(),
                                UserType = "Director",
                                Message = $"New incident reported by {resident.FullName}: {model.IncidentType} (Priority: {incident.Priority})",
                                IsRead = false,
                                CreatedAt = DateTime.Now,
                                RelatedUrl = Url.Action("IncidentDetails", "Director", new { id = incident.IncidentReportId })
                            };
                            db.Notifications.Add(directorNotification);
                        }
                    }
                    catch (Exception notifyEx)
                    {
                        System.Diagnostics.Debug.WriteLine("Director notification error: " + notifyEx.Message);
                    }

                    await db.SaveChangesAsync();

                    TempData["SuccessMessage"] = $"Incident reported successfully! Your report ID is #{incident.IncidentReportId}.";
                    return RedirectToAction("MyIncidents"); // Redirect to MyIncidents page after successful submission
                }

                // If we got this far, something failed - return to the same view with validation errors
                return View(model);
            }
            catch (DbEntityValidationException ex)
            {
                var errorMessages = new List<string>();
                foreach (var validationErrors in ex.EntityValidationErrors)
                {
                    foreach (var validationError in validationErrors.ValidationErrors)
                    {
                        errorMessages.Add($"{validationError.PropertyName}: {validationError.ErrorMessage}");
                    }
                }

                TempData["ErrorMessage"] = "Database validation error: " + string.Join("; ", errorMessages);
                ViewBag.Locations = GetCommonLocations();
                return View(model);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("ReportIncident POST error: " + ex.Message);
                TempData["ErrorMessage"] = "An error occurred while submitting the incident report. Please try again.";
                ViewBag.Locations = GetCommonLocations();
                return View(model);
            }
        }


        // GET: Resident/MyIncidents
        [Authorize(Roles = "Resident")]
        public ActionResult MyIncidents()
        {
            try
            {
                var residentId = User.Identity.GetUserId();
                var incidents = db.IncidentReports
                    .Where(i => i.ResidentId == residentId)
                    .OrderByDescending(i => i.ReportDate)
                    .ToList();

                return View(incidents);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("MyIncidents error: " + ex.Message);
                TempData["ErrorMessage"] = "An error occurred while loading your incidents.";
                return RedirectToAction("Dashboard");
            }
        }

        // GET: Resident/IncidentDetails/{id}
        [Authorize(Roles = "Resident")]
        public ActionResult IncidentDetails(int id)
        {
            try
            {
                var residentId = User.Identity.GetUserId();
                var incident = db.IncidentReports
                    .FirstOrDefault(i => i.IncidentReportId == id && i.ResidentId == residentId);

                if (incident == null)
                {
                    TempData["ErrorMessage"] = "Incident report not found or you don't have permission to view it.";
                    return RedirectToAction("MyIncidents");
                }

                return View(incident);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("IncidentDetails error: " + ex.Message);
                TempData["ErrorMessage"] = "An error occurred while loading incident details.";
                return RedirectToAction("MyIncidents");
            }
        }

        // POST: Resident/Logout
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Resident")]
        public ActionResult Logout()
        {
            try
            {
                // Sign out the user
                var authenticationManager = HttpContext.GetOwinContext().Authentication;
                authenticationManager.SignOut(DefaultAuthenticationTypes.ApplicationCookie);

                // Clear session
                Session.Clear();

                TempData["SuccessMessage"] = "You have been logged out successfully.";
                return RedirectToAction("Login", "Account");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("Logout error: " + ex.Message);
                TempData["ErrorMessage"] = "An error occurred during logout.";
                return RedirectToAction("Dashboard");
            }
        }

        // GET: Resident/NotificationBellPartial
        [ChildActionOnly]
        public ActionResult NotificationBellPartial()
        {
            if (!User.Identity.IsAuthenticated)
            {
                return PartialView("_NotificationBell", new List<Notification>());
            }

            var residentId = User.Identity.GetUserId();
            var notifications = db.Notifications
                .Where(n => n.UserId == residentId && n.UserType == "Resident")
                .OrderByDescending(n => n.CreatedAt)
                .Take(10)
                .ToList();

            return PartialView("_NotificationBell", notifications);
        }

        // GET: Resident/GetIncidentFeedback
        [Authorize(Roles = "Resident")]
        public ActionResult GetIncidentFeedback(int id)
        {
            try
            {
                var residentId = User.Identity.GetUserId();
                var incident = db.IncidentReports
                    .FirstOrDefault(i => i.IncidentReportId == id && i.ResidentId == residentId);

                if (incident == null)
                {
                    return Json(new { error = "Incident not found or access denied" }, JsonRequestBehavior.AllowGet);
                }

                return Json(new
                {
                    feedback = incident.Feedback,
                    hasAttachment = !string.IsNullOrEmpty(incident.FeedbackAttachment),
                    attachmentName = incident.FeedbackAttachment
                }, JsonRequestBehavior.AllowGet);
            }
            catch (Exception ex)
            {
                return Json(new { error = "Error retrieving feedback" }, JsonRequestBehavior.AllowGet);
            }
        }

        // GET: Resident/DownloadFeedbackAttachment
        [Authorize(Roles = "Resident")]
        public ActionResult DownloadFeedbackAttachment(int id)
        {
            try
            {
                var residentId = User.Identity.GetUserId();
                var incident = db.IncidentReports
                    .FirstOrDefault(i => i.IncidentReportId == id && i.ResidentId == residentId);

                if (incident == null || string.IsNullOrEmpty(incident.FeedbackAttachment))
                {
                    TempData["ErrorMessage"] = "Attachment not found or access denied";
                    return RedirectToAction("MyIncidents");
                }

                // For now, return a simple file result
                // You'll need to implement actual file download logic based on your storage system
                return Content("File download functionality would be implemented here");
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = "Error downloading attachment";
                return RedirectToAction("MyIncidents");
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                db.Dispose();
                if (_userManager != null)
                {
                    _userManager.Dispose();
                    _userManager = null;
                }
                if (_roleManager != null)
                {
                    _roleManager.Dispose();
                    _roleManager = null;
                }
            }
            base.Dispose(disposing);
        }
    }

    // ViewModel for Resident Registration
    public class RegisterResidentViewModel
    {
        [Required(ErrorMessage = "Full Name is required")]
        [Display(Name = "Full Name")]
        [StringLength(100, ErrorMessage = "Full Name cannot exceed 100 characters")]
        public string FullName { get; set; }

        public string Street { get; set; }
        public string City { get; set; }

        public string HouseNumber { get; set; }
        public string PostalCode { get; set; }

        [Required(ErrorMessage = "Email is required")]
        [EmailAddress(ErrorMessage = "Please enter a valid email address")]
        [StringLength(100, ErrorMessage = "Email cannot exceed 100 characters")]
        public string Email { get; set; }

        [Required(ErrorMessage = "Phone Number is required")]
        [Display(Name = "Phone Number")]
        [Phone(ErrorMessage = "Please enter a valid phone number")]
        [StringLength(20, ErrorMessage = "Phone Number cannot exceed 20 characters")]
        public string PhoneNumber { get; set; }

        [Required(ErrorMessage = "Address is required")]
        [StringLength(200, ErrorMessage = "Address cannot exceed 200 characters")]
        public string Address { get; set; }

        [Required(ErrorMessage = "Unit Number is required")]
        [Display(Name = "Unit Number")]
        [StringLength(20, ErrorMessage = "Unit Number cannot exceed 20 characters")]
        public string UnitNumber { get; set; }

        [Required(ErrorMessage = "Password is required")]
        [DataType(DataType.Password)]
        [MinLength(6, ErrorMessage = "Password must be at least 6 characters long")]
        [StringLength(100, ErrorMessage = "Password cannot exceed 100 characters")]
        public string Password { get; set; }

        [DataType(DataType.Password)]
        [Display(Name = "Confirm Password")]
        [System.ComponentModel.DataAnnotations.Compare("Password", ErrorMessage = "The password and confirmation password do not match.")]
        public string ConfirmPassword { get; set; }

        [Display(Name = "Emergency Contact")]
        [StringLength(100, ErrorMessage = "Emergency Contact cannot exceed 100 characters")]
        public string EmergencyContact { get; set; }
    }

    // ViewModel for Resident Update
    public class UpdateResidentViewModel
    {
        [Required(ErrorMessage = "Full Name is required")]
        [Display(Name = "Full Name")]
        [StringLength(100, ErrorMessage = "Full Name cannot exceed 100 characters")]
        public string FullName { get; set; }

        [Required(ErrorMessage = "Email is required")]
        [EmailAddress(ErrorMessage = "Please enter a valid email address")]
        [StringLength(100, ErrorMessage = "Email cannot exceed 100 characters")]
        public string Email { get; set; }

        [Required(ErrorMessage = "Phone Number is required")]
        [Display(Name = "Phone Number")]
        [Phone(ErrorMessage = "Please enter a valid phone number")]
        [StringLength(20, ErrorMessage = "Phone Number cannot exceed 20 characters")]
        public string PhoneNumber { get; set; }

        [Display(Name = "Emergency Contact")]
        [StringLength(100, ErrorMessage = "Emergency Contact cannot exceed 100 characters")]
        public string EmergencyContact { get; set; }

        [Required(ErrorMessage = "Address is required")]
        [StringLength(200, ErrorMessage = "Address cannot exceed 200 characters")]
        public string Address { get; set; }

        [Required(ErrorMessage = "Unit Number is required")]
        [Display(Name = "Unit Number")]
        [StringLength(20, ErrorMessage = "Unit Number cannot exceed 20 characters")]
        public string UnitNumber { get; set; }
    }

    // ViewModel for Resident Dashboard
    public class ResidentDashboardViewModel
    {
        public ApplicationUser Resident { get; set; }
        public ResidentInfo ResidentInfo { get; set; }
        public int TotalIncidents { get; set; }
        public int ResolvedIncidents { get; set; }
        public int PendingIncidents { get; set; }
        public int InProgressIncidents { get; set; }
        public List<Notification> Notifications { get; set; }
        public List<IncidentReport> RecentIncidents { get; set; }
    }

    // ViewModel for Reporting Incidents
    public class ReportIncidentViewModel
    {
        [Required]
        public string ResidentId { get; set; }



        [Required(ErrorMessage = "Incident type is required")]
        [Display(Name = "Incident Type")]
        public string IncidentType { get; set; }

        [Required(ErrorMessage = "Description is required")]
        [Display(Name = "Description")]
        [StringLength(1000, ErrorMessage = "Description cannot exceed 1000 characters")]
        public string Description { get; set; }

        [Display(Name = "Location")]
        [StringLength(200, ErrorMessage = "Location cannot exceed 200 characters")]
        public string Location { get; set; }

        [Required(ErrorMessage = "Emergency contact is required")]
        [Display(Name = "Emergency Contact")]
        [Phone(ErrorMessage = "Please enter a valid phone number")]
        public string EmergencyContact { get; set; }

        [Required(ErrorMessage = "Priority is required")]
        [Display(Name = "Priority")]
        public string Priority { get; set; }
    }
}