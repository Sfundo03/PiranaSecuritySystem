using Microsoft.AspNet.Identity;
using Microsoft.AspNet.Identity.Owin;
using Microsoft.Owin.Security;
using PiranaSecuritySystem.Models;
using System;
using System.Data.Entity;
using System.Globalization;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using System.Web;
using System.Web.Helpers;
using System.Web.Mvc;

namespace PiranaSecuritySystem.Controllers
{
    [Authorize]
    public class AccountController : Controller
    {
        private ApplicationSignInManager _signInManager;
        private ApplicationUserManager _userManager;

        // Default admin credentials
        private const string DefaultAdminEmail = "admin@pirana.com";
        private const string DefaultAdminPassword = "admin123";

        // Default director credentials
        private const string DefaultDirectorEmail = "director@pirana.com";
        private const string DefaultDirectorPassword = "director123";

        public AccountController()
        {
        }

        public AccountController(ApplicationUserManager userManager, ApplicationSignInManager signInManager)
        {
            UserManager = userManager;
            SignInManager = signInManager;
        }

        public ApplicationSignInManager SignInManager
        {
            get
            {
                return _signInManager ?? HttpContext.GetOwinContext().Get<ApplicationSignInManager>();
            }
            private set
            {
                _signInManager = value;
            }
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

        //
        // GET: /Account/Login
        [AllowAnonymous]
        public ActionResult Login(string returnUrl)
        {
            // If user is already authenticated, redirect to appropriate dashboard
            if (User.Identity.IsAuthenticated)
            {
                if (User.IsInRole("Admin"))
                    return RedirectToAction("Dashboard", "Admin");
                if (User.IsInRole("Director"))
                    return RedirectToAction("Dashboard", "Director");
                if (User.IsInRole("Guard"))
                    return RedirectToAction("Dashboard", "Guard");
                if (User.IsInRole("Instructor"))
                    return RedirectToAction("Dashboard", "Instructor");
                if (User.IsInRole("Resident"))
                    return RedirectToAction("Dashboard", "Resident");

                return RedirectToLocal(returnUrl);
            }

            ViewBag.ReturnUrl = returnUrl;
            return View();
        }

        // POST: /Account/Login
        [HttpPost]
        [AllowAnonymous]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> Login(LoginViewModel model, string returnUrl)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            // Check for default admin credentials
            if (model.Email.Equals(DefaultAdminEmail, StringComparison.OrdinalIgnoreCase) &&
                model.Password == DefaultAdminPassword)
            {
                // Sign out any existing authentication
                AuthenticationManager.SignOut(DefaultAuthenticationTypes.ApplicationCookie);

                // Create claims identity for default admin
                var identity = new ClaimsIdentity(DefaultAuthenticationTypes.ApplicationCookie);
                identity.AddClaim(new Claim(ClaimTypes.NameIdentifier, "admin-default-id"));
                identity.AddClaim(new Claim(ClaimTypes.Name, "Default Admin"));
                identity.AddClaim(new Claim(ClaimTypes.Email, DefaultAdminEmail));
                identity.AddClaim(new Claim(ClaimTypes.Role, "Admin"));

                // Sign in
                AuthenticationManager.SignIn(new AuthenticationProperties
                {
                    IsPersistent = model.RememberMe
                }, identity);

                // Create login notification for default admin
                CreateUserLoginNotification("Admin", "Default Admin", 0, "System");

                return RedirectToAction("Dashboard", "Admin");
            }

            // Check for default director credentials
            if (model.Email.Equals(DefaultDirectorEmail, StringComparison.OrdinalIgnoreCase) &&
                model.Password == DefaultDirectorPassword)
            {
                // Sign out any existing authentication
                AuthenticationManager.SignOut(DefaultAuthenticationTypes.ApplicationCookie);

                // Create claims identity for default director
                var identity = new ClaimsIdentity(DefaultAuthenticationTypes.ApplicationCookie);
                identity.AddClaim(new Claim(ClaimTypes.NameIdentifier, "director-default-id"));
                identity.AddClaim(new Claim(ClaimTypes.Name, "Default Director"));
                identity.AddClaim(new Claim(ClaimTypes.Email, DefaultDirectorEmail));
                identity.AddClaim(new Claim(ClaimTypes.Role, "Director"));

                // Sign in
                AuthenticationManager.SignIn(new AuthenticationProperties
                {
                    IsPersistent = model.RememberMe
                }, identity);

                // Create login notification for default director
                CreateUserLoginNotification("Director", "Default Director", 0, "System");

                return RedirectToAction("Dashboard", "Director");
            }

            // Regular ASP.NET Identity login for other users
            try
            {
                AuthenticationManager.SignOut(DefaultAuthenticationTypes.ApplicationCookie);
                var result = await SignInManager.PasswordSignInAsync(model.Email, model.Password, model.RememberMe, shouldLockout: false);

                switch (result)
                {
                    case SignInStatus.Success:
                        var user = await UserManager.FindByEmailAsync(model.Email);
                        if (user != null)
                        {
                            // Create login notification for all user types
                            await CreateUserLoginNotificationAsync(user);

                            // Check for Director role to add notification
                            if (await UserManager.IsInRoleAsync(user.Id, "Director"))
                            {
                                using (var db = new ApplicationDbContext())
                                {
                                    var notification = new Notification
                                    {
                                        UserId = user.Id,
                                        UserType = "Director",
                                        Message = $"Director {user.UserName} logged in successfully at {DateTime.Now:MM/dd/yyyy HH:mm}",
                                        IsRead = false,
                                        CreatedAt = DateTime.Now,
                                        RelatedUrl = "/Director/Dashboard",
                                        NotificationType = "Login"
                                    };

                                    db.Notifications.Add(notification);
                                    await db.SaveChangesAsync();
                                }

                                // Store DirectorId in session
                                Session["DirectorId"] = user.Id;
                            }

                            // Redirect based on role
                            if (await UserManager.IsInRoleAsync(user.Id, "Admin"))
                                return RedirectToAction("Dashboard", "Admin");
                            if (await UserManager.IsInRoleAsync(user.Id, "Director"))
                                return RedirectToAction("Dashboard", "Director");
                            if (await UserManager.IsInRoleAsync(user.Id, "Guard"))
                                return RedirectToAction("Dashboard", "Guard");
                            if (await UserManager.IsInRoleAsync(user.Id, "Instructor"))
                                return RedirectToAction("Dashboard", "Instructor");
                            if (await UserManager.IsInRoleAsync(user.Id, "Resident"))
                                return RedirectToAction("Dashboard", "Resident");

                        }
                        return RedirectToLocal(returnUrl);

                    case SignInStatus.LockedOut:
                        return View("Lockout");

                    case SignInStatus.RequiresVerification:
                        return RedirectToAction("SendCode", new { ReturnUrl = returnUrl, RememberMe = model.RememberMe });

                    case SignInStatus.Failure:
                    default:
                        ModelState.AddModelError("", "Invalid login attempt.");
                        ViewBag.ErrorMessage = "Invalid login attempt. Please check your credentials.";
                        return View(model);
                }
            }
            catch (Exception ex)
            {
                ModelState.AddModelError("", "An error occurred during login: " + ex.Message);
                ViewBag.ErrorMessage = "An error occurred during login. Please try again.";
                return View(model);
            }
        }

        // Helper method to create login notifications for all users
        private async Task CreateUserLoginNotificationAsync(ApplicationUser user)
        {
            try
            {
                using (var db = new ApplicationDbContext())
                {
                    string userType = "User";
                    string userName = user.UserName;
                    int userId = 0;
                    string site = null;
                    string relatedUrl = "/Admin/Dashboard";

                    // Determine user type and get additional info using synchronous methods
                    if (UserManager.IsInRole(user.Id, "Guard"))
                    {
                        userType = "Guard";
                        var guard = db.Guards.FirstOrDefault(g => g.UserId == user.Id);
                        if (guard != null)
                        {
                            userName = $"{guard.Guard_FName} {guard.Guard_LName}";
                            userId = guard.GuardId;
                            site = guard.Site;
                            relatedUrl = "/Admin/ManageGuards";
                        }
                    }
                    else if (UserManager.IsInRole(user.Id, "Instructor"))
                    {
                        userType = "Instructor";
                        var instructor = db.Instructors.FirstOrDefault(i => i.UserId == user.Id);
                        if (instructor != null)
                        {
                            userName = instructor.FullName;
                            userId = instructor.Id;
                            site = instructor.Site;
                            relatedUrl = "/Admin/ManageInstructors";
                        }
                    }
                    else if (UserManager.IsInRole(user.Id, "Admin"))
                    {
                        userType = "Admin";
                        userName = "Administrator";
                        relatedUrl = "/Admin/Dashboard";
                    }
                    else if (UserManager.IsInRole(user.Id, "Director"))
                    {
                        userType = "Director";
                        userName = "Director";
                        relatedUrl = "/Director/Dashboard";
                    }
                    else if (UserManager.IsInRole(user.Id, "Resident"))
                    {
                        userType = "Resident";
                        // Use ResidentInfo instead of Resident
                        var resident = db.ResidentInfos.FirstOrDefault(r => r.UserId == user.Id);
                        if (resident != null)
                        {
                            // For ResidentInfo, we don't have FirstName/LastName, so use UserName
                            userName = user.UserName;
                            userId = resident.Id;
                            relatedUrl = "/Resident/Dashboard";
                        }
                        else
                        {
                            // If no ResidentInfo found, still create notification with basic info
                            userName = user.UserName;
                            relatedUrl = "/Resident/Dashboard";
                        }
                    }

                    // Create notification for admin about the login
                    var notification = new Notification
                    {
                        Title = "Login Activity",
                        Message = $"{userType} {userName} logged in at {DateTime.Now.ToString("hh:mm tt")} on {DateTime.Now.ToString("MMM dd, yyyy")}" +
                                 (string.IsNullOrEmpty(site) ? "" : $" from {site}"),
                        NotificationType = "Login",
                        UserId = "Admin", // Notify admin about all logins
                        UserType = "Admin",
                        CreatedAt = DateTime.Now,
                        IsRead = false,
                        Source = "LoginSystem",
                        RelatedUrl = relatedUrl
                    };

                    db.Notifications.Add(notification);
                    await db.SaveChangesAsync();
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error creating login notification: {ex.Message}");
                // Don't throw - login should still succeed even if notification fails
            }
        }

        // Helper method for default users (admin/director)
        private void CreateUserLoginNotification(string userType, string userName, int userId, string site = null)
        {
            try
            {
                using (var db = new ApplicationDbContext())
                {
                    string relatedUrl = "/Admin/Dashboard"; // Default URL

                    // Determine the related URL based on user type using traditional switch
                    switch (userType.ToLower())
                    {
                        case "guard":
                            relatedUrl = "/Admin/ManageGuards";
                            break;
                        case "instructor":
                            relatedUrl = "/Admin/ManageInstructors";
                            break;
                        case "admin":
                            relatedUrl = "/Admin/Dashboard";
                            break;
                        case "director":
                            relatedUrl = "/Director/Dashboard";
                            break;
                        case "resident":
                            relatedUrl = "/Resident/Dashboard";
                            break;
                        default:
                            relatedUrl = "/Admin/Dashboard";
                            break;
                    }

                    var notification = new Notification
                    {
                        Title = "Login Activity",
                        Message = $"{userType} {userName} logged in at {DateTime.Now.ToString("hh:mm tt")} on {DateTime.Now.ToString("MMM dd, yyyy")}" +
                                 (string.IsNullOrEmpty(site) ? "" : $" from {site}"),
                        NotificationType = "Login",
                        UserId = "Admin", // Notify admin about all logins
                        UserType = "Admin",
                        CreatedAt = DateTime.Now,
                        IsRead = false,
                        Source = "LoginSystem",
                        RelatedUrl = relatedUrl
                    };

                    db.Notifications.Add(notification);
                    db.SaveChanges();
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error creating login notification: {ex.Message}");
                // Don't throw - login should still succeed even if notification fails
            }
        }

        //
        // GET: /Account/ForgotPassword
        [AllowAnonymous]
        public ActionResult ForgotPassword()
        {
            return View();
        }

        //
        // POST: /Account/ForgotPassword
        [HttpPost]
        [AllowAnonymous]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> ForgotPassword(ForgotPasswordViewModel model)
        {
            if (ModelState.IsValid)
            {
                var user = await UserManager.FindByEmailAsync(model.Email);

                // Check if user exists and is a Resident
                if (user != null && await UserManager.IsInRoleAsync(user.Id, "Resident"))
                {
                    // Generate OTP
                    var otp = GenerateOTP();
                    var otpExpiry = DateTime.Now.AddMinutes(15); // OTP valid for 15 minutes

                    // Store OTP in database
                    using (var db = new ApplicationDbContext())
                    {
                        // Invalidate any previous OTPs for this email
                        var previousOtps = db.PasswordResetOTPs.Where(o => o.Email == model.Email && !o.IsUsed);
                        foreach (var prevOtp in previousOtps)
                        {
                            prevOtp.IsUsed = true;
                        }

                        var passwordReset = new PasswordResetOTP
                        {
                            Email = model.Email,
                            OTP = otp,
                            ExpiryTime = otpExpiry,
                            IsUsed = false,
                            CreatedAt = DateTime.Now
                        };

                        db.PasswordResetOTPs.Add(passwordReset);
                        await db.SaveChangesAsync();
                    }

                    // Send OTP via email
                    await SendOTPEmail(user.Email, otp);

                    // Redirect to OTP verification page
                    return RedirectToAction("VerifyOTP", new { email = model.Email });
                }
                else
                {
                    // Don't reveal that the user does not exist or is not a resident
                    // Show same success message for security
                    return RedirectToAction("ForgotPasswordConfirmation");
                }
            }

            // If we got this far, something failed, redisplay form
            return View(model);
        }

        //
        // GET: /Account/VerifyOTP
        [AllowAnonymous]
        public ActionResult VerifyOTP(string email)
        {
            var model = new VerifyOTPViewModel { Email = email };
            return View(model);
        }

        //
        // POST: /Account/VerifyOTP
        [HttpPost]
        [AllowAnonymous]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> VerifyOTP(VerifyOTPViewModel model)
        {
            if (ModelState.IsValid)
            {
                using (var db = new ApplicationDbContext())
                {
                    // Find valid OTP
                    var otpRecord = await db.PasswordResetOTPs
                        .Where(o => o.Email == model.Email &&
                                   o.OTP == model.OTP &&
                                   o.ExpiryTime > DateTime.Now &&
                                   !o.IsUsed)
                        .OrderByDescending(o => o.CreatedAt)
                        .FirstOrDefaultAsync();

                    if (otpRecord != null)
                    {
                        // Mark OTP as used
                        otpRecord.IsUsed = true;
                        await db.SaveChangesAsync();

                        // Generate reset token and redirect to reset password
                        var user = await UserManager.FindByEmailAsync(model.Email);
                        if (user != null && await UserManager.IsInRoleAsync(user.Id, "Resident"))
                        {
                            var code = await UserManager.GeneratePasswordResetTokenAsync(user.Id);
                            return RedirectToAction("ResetPassword", new { code = code, email = model.Email });
                        }
                        else
                        {
                            ModelState.AddModelError("", "User not found or not authorized for password reset.");
                        }
                    }
                    else
                    {
                        ModelState.AddModelError("", "Invalid or expired OTP. Please try again.");
                    }
                }
            }

            return View(model);
        }

        //
        // GET: /Account/ResetPassword
        [AllowAnonymous]
        public ActionResult ResetPassword(string code, string email)
        {
            if (code == null || email == null)
            {
                return View("Error");
            }

            var model = new ResetPasswordViewModel
            {
                Code = code,
                Email = email
            };
            return View(model);
        }

        //
        // POST: /Account/ResetPassword
        [HttpPost]
        [AllowAnonymous]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> ResetPassword(ResetPasswordViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            var user = await UserManager.FindByEmailAsync(model.Email);
            if (user == null || !(await UserManager.IsInRoleAsync(user.Id, "Resident")))
            {
                // Don't reveal that the user does not exist
                return RedirectToAction("ResetPasswordConfirmation", "Account");
            }

            var result = await UserManager.ResetPasswordAsync(user.Id, model.Code, model.Password);
            if (result.Succeeded)
            {
                // Create notification for password reset
                using (var db = new ApplicationDbContext())
                {
                    var notification = new Notification
                    {
                        UserId = user.Id,
                        UserType = "Resident",
                        Title = "Password Reset",
                        Message = "Your password was reset successfully.",
                        IsRead = false,
                        CreatedAt = DateTime.Now,
                        RelatedUrl = "/Resident/Dashboard",
                        NotificationType = "Security"
                    };
                    db.Notifications.Add(notification);
                    await db.SaveChangesAsync();
                }

                return RedirectToAction("ResetPasswordConfirmation", "Account");
            }

            AddErrors(result);
            return View();
        }

        //
        // GET: /Account/ForgotPasswordConfirmation
        [AllowAnonymous]
        public ActionResult ForgotPasswordConfirmation()
        {
            return View();
        }

        //
        // GET: /Account/ResetPasswordConfirmation
        [AllowAnonymous]
        public ActionResult ResetPasswordConfirmation()
        {
            return View();
        }

        //
        // GET: /Account/Register
        [AllowAnonymous]
        public ActionResult Register()
        {
            return View();
        }

        //
        // POST: /Account/Register
        [HttpPost]
        [AllowAnonymous]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> Register(RegisterViewModel model)
        {
            if (ModelState.IsValid)
            {
                var user = new ApplicationUser { UserName = model.Email, Email = model.Email };
                var result = await UserManager.CreateAsync(user, model.Password);
                if (result.Succeeded)
                {
                    await SignInManager.SignInAsync(user, isPersistent: false, rememberBrowser: false);
                    return RedirectToAction("Index", "Home");
                }
                AddErrors(result);
            }

            // If we got this far, something failed, redisplay form
            return View(model);
        }

        //
        // POST: /Account/LogOff
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult LogOff()
        {
            AuthenticationManager.SignOut(DefaultAuthenticationTypes.ApplicationCookie);
            Session.Clear();
            return RedirectToAction("Login", "Account");
        }

        //
        // GET: /Account/LogOff (for cases where POST fails due to anti-forgery token issues)
        [AllowAnonymous]
        public ActionResult LogOffGet()
        {
            AuthenticationManager.SignOut(DefaultAuthenticationTypes.ApplicationCookie);
            Session.Clear();
            return RedirectToAction("Index", "Home");
        }

        // Helper method to generate OTP
        private string GenerateOTP()
        {
            var random = new Random();
            return random.Next(100000, 999999).ToString();
        }

        // Helper method to send OTP email
        private async Task SendOTPEmail(string email, string otp)
        {
            try
            {
                var subject = "Pirana Guarding - Password Reset OTP";
                var body = $@"
                    <!DOCTYPE html>
                    <html>
                    <head>
                        <style>
                            body {{ font-family: Arial, sans-serif; }}
                            .container {{ max-width: 600px; margin: 0 auto; padding: 20px; }}
                            .header {{ background: #0069aa; color: white; padding: 20px; text-align: center; }}
                            .content {{ padding: 20px; background: #f8f9fa; }}
                            .otp {{ font-size: 32px; font-weight: bold; color: #0069aa; text-align: center; margin: 20px 0; }}
                            .footer {{ text-align: center; color: #6c757d; font-size: 12px; margin-top: 20px; }}
                        </style>
                    </head>
                    <body>
                        <div class='container'>
                            <div class='header'>
                                <h2>PIRANA GUARDING</h2>
                                <p>Secure Access Management System</p>
                            </div>
                            <div class='content'>
                                <h3>Password Reset Request</h3>
                                <p>You have requested to reset your password. Use the OTP below to verify your identity:</p>
                                <div class='otp'>{otp}</div>
                                <p>This OTP will expire in 15 minutes.</p>
                                <p>If you didn't request this reset, please ignore this email.</p>
                            </div>
                            <div class='footer'>
                                <p>&copy; {DateTime.Now.Year} Pirana Guarding. All rights reserved.</p>
                            </div>
                        </div>
                    </body>
                    </html>";

                // Using WebMail helper to send email
                // Configure these settings in your Web.config
                WebMail.SmtpServer = "smtp.gmail.com"; // Configure your SMTP server
                WebMail.SmtpPort = 587;
                WebMail.EnableSsl = true;
                WebMail.UserName = "your-email@gmail.com"; // Configure your email
                WebMail.Password = "your-app-password"; // Configure your password
                WebMail.From = "noreply@piranaguarding.com";

                await Task.Run(() =>
                {
                    WebMail.Send(
                        to: email,
                        subject: subject,
                        body: body,
                        isBodyHtml: true
                    );
                });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Email sending failed: {ex.Message}");
                // Log the error but don't throw - we don't want to reveal email failures to users
            }
        }

        // Helper method to redirect to local URL
        private ActionResult RedirectToLocal(string returnUrl)
        {
            if (Url.IsLocalUrl(returnUrl))
            {
                return Redirect(returnUrl);
            }
            return RedirectToAction("Index", "Home");
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

                if (_signInManager != null)
                {
                    _signInManager.Dispose();
                    _signInManager = null;
                }
            }

            base.Dispose(disposing);
        }

        #region Helpers
        // Used for XSRF protection when adding external logins
        private const string XsrfKey = "XsrfId";

        private IAuthenticationManager AuthenticationManager
        {
            get
            {
                return HttpContext.GetOwinContext().Authentication;
            }
        }

        private void AddErrors(IdentityResult result)
        {
            foreach (var error in result.Errors)
            {
                ModelState.AddModelError("", error);
            }
        }

        internal class ChallengeResult : HttpUnauthorizedResult
        {
            public ChallengeResult(string provider, string redirectUri)
                : this(provider, redirectUri, null)
            {
            }

            public ChallengeResult(string provider, string redirectUri, string userId)
            {
                LoginProvider = provider;
                RedirectUri = redirectUri;
                UserId = userId;
            }

            public string LoginProvider { get; set; }
            public string RedirectUri { get; set; }
            public string UserId { get; set; }

            public override void ExecuteResult(ControllerContext context)
            {
                var properties = new AuthenticationProperties { RedirectUri = RedirectUri };
                if (UserId != null)
                {
                    properties.Dictionary[XsrfKey] = UserId;
                }
                context.HttpContext.GetOwinContext().Authentication.Challenge(properties, LoginProvider);
            }
        }
        #endregion
    }
}