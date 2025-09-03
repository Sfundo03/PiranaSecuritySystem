using PiranaSecuritySystem.Models;
using System;
using System.Collections.Generic;
using System.Data.Entity.Validation;
using System.IO;
using System.Linq;
using System.Web.Mvc;
using System.Web.Security;
using System.Security.Cryptography;
using System.Text;

namespace PiranaSecuritySystem.Controllers
{
    public class ResidentController : Controller
    {
        private ApplicationDbContext db = new ApplicationDbContext();

        // GET: Resident/Register
        public ActionResult Register()
        {
            // If user is already logged in, redirect to dashboard
            if (Session["ResidentId"] != null)
            {
                return RedirectToAction("Dashboard");
            }
            return View();
        }

        // POST: Resident/Register
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Register(Resident resident)
        {
            try
            {
                if (ModelState.IsValid)
                {
                    // Check if email already exists
                    if (db.Residents.Any(r => r.Email == resident.Email))
                    {
                        ViewBag.ErrorMessage = "Email already registered.";
                        return View(resident);
                    }

                    // Hash the password before saving
                    resident.Password = HashPassword(resident.Password);
                    resident.IsActive = true;
                    resident.CreatedAt = DateTime.Now;

                    db.Residents.Add(resident);
                    db.SaveChanges();

                    // Create success notification
                    var notification = new Notification
                    {
                        UserId = resident.ResidentId.ToString(),
                        UserType = "Resident",
                        Message = "Welcome to Pirana Security System! Your account has been created successfully.",
                        IsRead = false,
                        CreatedAt = DateTime.Now,
                        RelatedUrl = Url.Action("Dashboard", "Resident")
                    };
                    db.Notifications.Add(notification);

                    // Notify directors about new registration
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
                            RelatedUrl = Url.Action("ResidentDetails", "Director", new { id = resident.ResidentId })
                        };
                        db.Notifications.Add(directorNotification);
                    }

                    db.SaveChanges();

                    TempData["SuccessMessage"] = "Registration successful! Please login with your credentials.";
                    return RedirectToAction("Index");
                }

                // If we got this far, something failed, redisplay form
                return View(resident);
            }
            catch (DbEntityValidationException ex)
            {
                // Log detailed validation errors
                var errorMessages = new List<string>();
                foreach (var validationErrors in ex.EntityValidationErrors)
                {
                    foreach (var validationError in validationErrors.ValidationErrors)
                    {
                        errorMessages.Add($"Property: {validationError.PropertyName} Error: {validationError.ErrorMessage}");
                    }
                }

                System.Diagnostics.Debug.WriteLine("Validation errors: " + string.Join("; ", errorMessages));
                ViewBag.ErrorMessage = "Validation failed: " + string.Join("; ", errorMessages);
                return View(resident);
            }
            catch (System.Data.Entity.Infrastructure.DbUpdateException dbEx)
            {
                System.Diagnostics.Debug.WriteLine("Database update error: " + dbEx.Message);
                if (dbEx.InnerException != null)
                {
                    System.Diagnostics.Debug.WriteLine("Inner exception: " + dbEx.InnerException.Message);
                }
                ViewBag.ErrorMessage = "Database error occurred. Please check your input data.";
                return View(resident);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("Registration error: " + ex.Message);
                if (ex.InnerException != null)
                {
                    System.Diagnostics.Debug.WriteLine("Inner exception: " + ex.InnerException.Message);
                }
                ViewBag.ErrorMessage = "An error occurred during registration. Please try again. Error: " + ex.Message;
                return View(resident);
            }
        }

        // GET: Resident/Index
        public ActionResult Index()
        {
            // If user is already logged in, redirect to dashboard
            if (Session["ResidentId"] != null)
            {
                return RedirectToAction("Dashboard");
            }
            return View();
        }

        // POST: Resident/Login
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Login(string email, string password)
        {
            try
            {
                if (string.IsNullOrEmpty(email) || string.IsNullOrEmpty(password))
                {
                    TempData["ErrorMessage"] = "Please enter both email and password.";
                    return RedirectToAction("Index");
                }

                // Hash the password for comparison
                string hashedPassword = HashPassword(password);

                var resident = db.Residents.FirstOrDefault(r => r.Email == email && r.Password == hashedPassword && r.IsActive);

                if (resident != null)
                {
                    // Set session or authentication cookie
                    Session["ResidentId"] = resident.ResidentId;
                    Session["ResidentName"] = resident.FullName;

                    // Create login notification for resident
                    var residentNotification = new Notification
                    {
                        UserId = resident.ResidentId.ToString(),
                        UserType = "Resident",
                        Message = "You have successfully logged in to your account.",
                        IsRead = false,
                        CreatedAt = DateTime.Now,
                        RelatedUrl = Url.Action("Dashboard", "Resident")
                    };
                    db.Notifications.Add(residentNotification);

                    // Create notification for all directors
                    var directors = db.Directors.Where(d => d.IsActive).ToList();
                    foreach (var director in directors)
                    {
                        var directorNotification = new Notification
                        {
                            UserId = director.DirectorId.ToString(),
                            UserType = "Director",
                            Message = $"Resident {resident.FullName} has logged into the system.",
                            IsRead = false,
                            CreatedAt = DateTime.Now,
                            RelatedUrl = Url.Action("ResidentDetails", "Director", new { id = resident.ResidentId })
                        };
                        db.Notifications.Add(directorNotification);
                    }

                    db.SaveChanges();

                    TempData["SuccessMessage"] = "Welcome back, " + resident.FullName + "!";
                    return RedirectToAction("Dashboard");
                }

                TempData["ErrorMessage"] = "Invalid email or password.";
                return RedirectToAction("Index");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("Login error: " + ex.Message);
                TempData["ErrorMessage"] = "An error occurred during login. Please try again.";
                return RedirectToAction("Index");
            }
        }

        // POST: Resident/Logout
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Logout()
        {
            try
            {
                Session.Clear();
                Session.Abandon();

                // Clear authentication cookies if you're using them
                FormsAuthentication.SignOut();

                TempData["SuccessMessage"] = "You have been logged out successfully.";
                return RedirectToAction("Index", "Resident");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("Logout error: " + ex.Message);
                TempData["ErrorMessage"] = "An error occurred during logout.";
                return RedirectToAction("Dashboard");
            }
        }

        // GET: Resident/Dashboard
        public ActionResult Dashboard()
        {
            try
            {
                if (Session["ResidentId"] == null)
                {
                    TempData["ErrorMessage"] = "Please login to access the dashboard.";
                    return RedirectToAction("Index");
                }

                var residentId = (int)Session["ResidentId"];
                var resident = db.Residents.Find(residentId);

                if (resident == null)
                {
                    TempData["ErrorMessage"] = "Resident not found.";
                    return RedirectToAction("Index");
                }

                // Get notifications for this resident
                var notifications = db.Notifications
                    .Where(n => n.UserId == residentId.ToString() && n.UserType == "Resident")
                    .OrderByDescending(n => n.CreatedAt)
                    .ToList();

                ViewBag.Notifications = notifications;

                // Get dashboard statistics for the resident
                var dashboardStats = new ResidentDashboardViewModel
                {
                    TotalIncidents = db.IncidentReports.Count(i => i.ResidentId == residentId),
                    ResolvedIncidents = db.IncidentReports.Count(i => i.ResidentId == residentId && i.Status == "Resolved"),
                    PendingIncidents = db.IncidentReports.Count(i => i.ResidentId == residentId && i.Status == "Pending"),
                    InProgressIncidents = db.IncidentReports.Count(i => i.ResidentId == residentId && i.Status == "In Progress"),
                    RecentIncidents = db.IncidentReports
                        .Where(i => i.ResidentId == residentId)
                        .OrderByDescending(i => i.ReportDate)
                        .Take(5)
                        .ToList()
                };

                ViewBag.DashboardStats = dashboardStats;
                return View(resident);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("Dashboard error: " + ex.Message);
                TempData["ErrorMessage"] = "An error occurred while loading the dashboard.";
                return RedirectToAction("Index");
            }
        }

        // GET: Resident/ReportIncident
        public ActionResult ReportIncident()
        {
            try
            {
                if (Session["ResidentId"] == null)
                {
                    TempData["ErrorMessage"] = "Please login to report an incident.";
                    return RedirectToAction("Index");
                }

                var residentId = (int)Session["ResidentId"];
                var resident = db.Residents.Find(residentId);

                if (resident == null)
                {
                    TempData["ErrorMessage"] = "Resident not found.";
                    return RedirectToAction("Dashboard");
                }

                // Pre-fill the incident report with resident data
                var incidentReport = new IncidentReport
                {
                    Location = $"{resident.Address}, Unit {resident.UnitNumber}",
                    EmergencyContact = resident.PhoneNumber
                };

                return View(incidentReport);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("ReportIncident GET error: " + ex.Message);
                TempData["ErrorMessage"] = "An error occurred while loading the incident report form.";
                return RedirectToAction("Dashboard");
            }
        }

        // POST: Resident/ReportIncident
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult ReportIncident(IncidentReport incidentReport)
        {
            try
            {
                if (Session["ResidentId"] == null)
                {
                    TempData["ErrorMessage"] = "Please login to report an incident.";
                    return RedirectToAction("Index");
                }

                var residentId = (int)Session["ResidentId"];
                var resident = db.Residents.Find(residentId);

                if (resident == null)
                {
                    TempData["ErrorMessage"] = "Resident not found.";
                    return RedirectToAction("Dashboard");
                }

                // Validate required fields manually
                if (string.IsNullOrEmpty(incidentReport.IncidentType?.Trim()))
                {
                    ModelState.AddModelError("IncidentType", "Incident type is required.");
                }

                if (string.IsNullOrEmpty(incidentReport.Description?.Trim()))
                {
                    ModelState.AddModelError("Description", "Description is required.");
                }

                if (string.IsNullOrEmpty(incidentReport.Priority?.Trim()))
                {
                    ModelState.AddModelError("Priority", "Priority is required.");
                }

                if (!ModelState.IsValid)
                {
                    // Return to the form with validation errors
                    return View(incidentReport);
                }

                // Create new incident report with validated data
                var newIncident = new IncidentReport
                {
                    ResidentId = residentId,
                    IncidentType = incidentReport.IncidentType.Trim(),
                    Location = string.IsNullOrEmpty(incidentReport.Location?.Trim()) ?
                              $"{resident.Address}, Unit {resident.UnitNumber}" :
                              incidentReport.Location.Trim(),
                    EmergencyContact = !string.IsNullOrEmpty(incidentReport.EmergencyContact?.Trim()) ?
                                      incidentReport.EmergencyContact.Trim() :
                                      resident.PhoneNumber,
                    ReportDate = DateTime.Now,
                    Status = "Pending",
                    Description = incidentReport.Description.Trim(),
                    Priority = incidentReport.Priority.Trim(),
                    ReportedBy = resident.FullName,
                    Feedback = "", // Initialize with empty feedback
                    FeedbackAttachment = null,
                    FeedbackDate = null
                };

                db.IncidentReports.Add(newIncident);
                db.SaveChanges();

                // Create notification for resident
                var residentNotification = new Notification
                {
                    UserId = residentId.ToString(),
                    UserType = "Resident",
                    Message = $"You have reported a {newIncident.IncidentType} incident.",
                    IsRead = false,
                    CreatedAt = DateTime.Now,
                    RelatedUrl = Url.Action("MyIncidents", "Resident")
                };
                db.Notifications.Add(residentNotification);

                // Create notification for all directors
                var directors = db.Directors.Where(d => d.IsActive).ToList();
                foreach (var director in directors)
                {
                    var directorNotification = new Notification
                    {
                        UserId = director.DirectorId.ToString(),
                        UserType = "Director",
                        Message = $"Resident {resident.FullName} has reported a {newIncident.IncidentType} incident.",
                        IsRead = false,
                        CreatedAt = DateTime.Now,
                        RelatedUrl = Url.Action("IncidentDetails", "Director", new { id = newIncident.IncidentReportId })
                    };
                    db.Notifications.Add(directorNotification);
                }

                db.SaveChanges();

                TempData["SuccessMessage"] = "Incident reported successfully! Your incident ID is #" + newIncident.IncidentReportId + ". Our team will contact you shortly.";
                return RedirectToAction("MyIncidents");
            }
            catch (DbEntityValidationException ex)
            {
                var errorMessages = ex.EntityValidationErrors
                    .SelectMany(x => x.ValidationErrors)
                    .Select(x => x.ErrorMessage);

                System.Diagnostics.Debug.WriteLine("Validation errors: " + string.Join("; ", errorMessages));
                TempData["ErrorMessage"] = "Validation failed: " + string.Join("; ", errorMessages);
                return View(incidentReport);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("ReportIncident error: " + ex.Message);
                TempData["ErrorMessage"] = "An error occurred while reporting the incident. Please try again.";
                return View(incidentReport);
            }
        }

        // GET: Resident/GetIncidentFeedback
        public JsonResult GetIncidentFeedback(int id)
        {
            try
            {
                if (Session["ResidentId"] == null)
                {
                    return Json(new { error = "Please login to view feedback." }, JsonRequestBehavior.AllowGet);
                }

                var residentId = (int)Session["ResidentId"];
                var incident = db.IncidentReports.FirstOrDefault(i => i.IncidentReportId == id && i.ResidentId == residentId);

                if (incident == null)
                {
                    return Json(new { error = "Incident not found or access denied." }, JsonRequestBehavior.AllowGet);
                }

                // Return both text feedback and attachment info
                return Json(new
                {
                    feedback = incident.Feedback,
                    hasAttachment = !string.IsNullOrEmpty(incident.FeedbackAttachment),
                    attachmentName = incident.FeedbackAttachment,
                    feedbackDate = incident.FeedbackDate?.ToString("yyyy-MM-dd HH:mm"),
                    status = incident.Status
                }, JsonRequestBehavior.AllowGet);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("GetIncidentFeedback error: " + ex.Message);
                return Json(new { error = "An error occurred while retrieving feedback." }, JsonRequestBehavior.AllowGet);
            }
        }

        // GET: Resident/DownloadFeedbackAttachment/{id}
        public ActionResult DownloadFeedbackAttachment(int id)
        {
            try
            {
                if (Session["ResidentId"] == null)
                {
                    TempData["ErrorMessage"] = "Please login to download feedback attachments.";
                    return RedirectToAction("Index");
                }

                var residentId = (int)Session["ResidentId"];
                var incident = db.IncidentReports
                    .FirstOrDefault(i => i.IncidentReportId == id && i.ResidentId == residentId);

                if (incident == null || string.IsNullOrEmpty(incident.FeedbackAttachment))
                {
                    TempData["ErrorMessage"] = "Feedback attachment not found.";
                    return RedirectToAction("MyIncidents");
                }

                var path = Path.Combine(Server.MapPath("~/App_Data/FeedbackFiles"), incident.FeedbackAttachment);
                if (!System.IO.File.Exists(path))
                {
                    TempData["ErrorMessage"] = "Feedback file not found on server.";
                    return RedirectToAction("MyIncidents");
                }

                // Determine content type based on file extension
                string contentType = "application/octet-stream";
                if (incident.FeedbackAttachment.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase))
                {
                    contentType = "application/pdf";
                }
                else if (incident.FeedbackAttachment.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase) ||
                         incident.FeedbackAttachment.EndsWith(".jpeg", StringComparison.OrdinalIgnoreCase))
                {
                    contentType = "image/jpeg";
                }
                else if (incident.FeedbackAttachment.EndsWith(".png", StringComparison.OrdinalIgnoreCase))
                {
                    contentType = "image/png";
                }

                return File(path, contentType, incident.FeedbackAttachment);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("DownloadFeedbackAttachment error: " + ex.Message);
                TempData["ErrorMessage"] = "Error downloading feedback attachment.";
                return RedirectToAction("MyIncidents");
            }
        }

        // POST: Resident/UpdateDetails
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult UpdateDetails(Resident updatedResident)
        {
            try
            {
                if (Session["ResidentId"] == null)
                {
                    TempData["ErrorMessage"] = "Please login to update your details.";
                    return RedirectToAction("Index");
                }

                var residentId = (int)Session["ResidentId"];
                var resident = db.Residents.Find(residentId);

                if (resident == null)
                {
                    TempData["ErrorMessage"] = "Resident not found.";
                    return RedirectToAction("Dashboard");
                }

                // Validate required fields
                if (string.IsNullOrEmpty(updatedResident.FullName?.Trim()))
                {
                    TempData["ErrorMessage"] = "Full name is required.";
                    return RedirectToAction("Dashboard");
                }

                if (string.IsNullOrEmpty(updatedResident.Email?.Trim()))
                {
                    TempData["ErrorMessage"] = "Email is required.";
                    return RedirectToAction("Dashboard");
                }

                if (string.IsNullOrEmpty(updatedResident.PhoneNumber?.Trim()))
                {
                    TempData["ErrorMessage"] = "Phone number is required.";
                    return RedirectToAction("Dashboard");
                }

                // Update only allowed fields
                resident.FullName = updatedResident.FullName.Trim();
                resident.Email = updatedResident.Email.Trim();
                resident.PhoneNumber = updatedResident.PhoneNumber.Trim();

                db.SaveChanges();
                Session["ResidentName"] = resident.FullName;

                // Create notification for profile update
                var notification = new Notification
                {
                    UserId = residentId.ToString(),
                    UserType = "Resident",
                    Message = "Your profile details have been updated successfully.",
                    IsRead = false,
                    CreatedAt = DateTime.Now,
                    RelatedUrl = Url.Action("Dashboard", "Resident")
                };
                db.Notifications.Add(notification);
                db.SaveChanges();

                TempData["SuccessMessage"] = "Your details have been updated successfully!";
                return RedirectToAction("Dashboard");
            }
            catch (DbEntityValidationException ex)
            {
                var errorMessages = ex.EntityValidationErrors
                    .SelectMany(x => x.ValidationErrors)
                    .Select(x => x.ErrorMessage);

                System.Diagnostics.Debug.WriteLine("Validation errors: " + string.Join("; ", errorMessages));
                TempData["ErrorMessage"] = "Validation failed: " + string.Join("; ", errorMessages);
                return RedirectToAction("Dashboard");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("UpdateDetails error: " + ex.Message);
                TempData["ErrorMessage"] = "An error occurred while updating your details.";
                return RedirectToAction("Dashboard");
            }
        }

        // GET: Resident/NotificationBellPartial
        [ChildActionOnly]
        public ActionResult NotificationBellPartial()
        {
            if (Session["ResidentId"] == null)
            {
                return PartialView("_NotificationBell", new List<Notification>());
            }

            var residentId = (int)Session["ResidentId"];
            var notifications = db.Notifications
                .Where(n => n.UserId == residentId.ToString() && n.UserType == "Resident")
                .OrderByDescending(n => n.CreatedAt)
                .Take(10) // Show only the 10 most recent notifications
                .ToList();

            return PartialView("_NotificationBell", notifications);
        }

        // GET: Resident/MyIncidents
        public ActionResult MyIncidents()
        {
            try
            {
                if (Session["ResidentId"] == null)
                {
                    TempData["ErrorMessage"] = "Please login to view your incidents.";
                    return RedirectToAction("Index");
                }

                var residentId = (int)Session["ResidentId"];
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

        // Password hashing method
        private string HashPassword(string password)
        {
            using (var sha256 = SHA256.Create())
            {
                var hashedBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(password));
                return BitConverter.ToString(hashedBytes).Replace("-", "").ToLower();
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

    // ViewModel for Resident Dashboard
    public class ResidentDashboardViewModel
    {
        public int TotalIncidents { get; set; }
        public int ResolvedIncidents { get; set; }
        public int PendingIncidents { get; set; }
        public int InProgressIncidents { get; set; }
        public List<IncidentReport> RecentIncidents { get; set; }
    }
}