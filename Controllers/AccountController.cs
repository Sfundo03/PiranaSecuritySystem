using Microsoft.AspNet.Identity;
using Microsoft.AspNet.Identity.Owin;
using Microsoft.Owin.Security;
using PiranaSecuritySystem.Models;
using System;
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
                return RedirectToDashboard();
            }

            ViewBag.ReturnUrl = returnUrl;
            return View();
        }

        // Helper method to redirect to appropriate dashboard based on role
        private ActionResult RedirectToDashboard()
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

            return RedirectToAction("Index", "Home");
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

                            // Check for Resident role to add notification
                            if (await UserManager.IsInRoleAsync(user.Id, "Resident"))
                            {
                                using (var db = new ApplicationDbContext())
                                {
                                    var notification = new Notification
                                    {
                                        UserId = user.Id,
                                        UserType = "Resident",
                                        Message = $"Resident {user.UserName} logged in successfully at {DateTime.Now:MM/dd/yyyy HH:mm}",
                                        IsRead = false,
                                        CreatedAt = DateTime.Now,
                                        RelatedUrl = "/Resident/Dashboard",
                                        NotificationType = "Login"
                                    };

                                    db.Notifications.Add(notification);
                                    await db.SaveChangesAsync();
                                }
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

        // Helper method to redirect to local URL
        private ActionResult RedirectToLocal(string returnUrl)
        {
            if (Url.IsLocalUrl(returnUrl))
            {
                return Redirect(returnUrl);
            }
            return RedirectToAction("Index", "Home");
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
                var user = await UserManager.FindByNameAsync(model.Email);
                if (user == null || !(await UserManager.IsEmailConfirmedAsync(user.Id)))
                {
                    // Don't reveal that the user does not exist or is not confirmed
                    return View("ForgotPasswordConfirmation");
                }
            }

            // If we got this far, something failed, redisplay form
            return View(model);
        }

        //
        // GET: /Account/ForgotPasswordConfirmation
        [AllowAnonymous]
        public ActionResult ForgotPasswordConfirmation()
        {
            return View();
        }

        //
        // GET: /Account/ResetPassword
        [AllowAnonymous]
        public ActionResult ResetPassword(string code)
        {
            return code == null ? View("Error") : View();
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
            var user = await UserManager.FindByNameAsync(model.Email);
            if (user == null)
            {
                // Don't reveal that the user does not exist
                return RedirectToAction("ResetPasswordConfirmation", "Account");
            }
            var result = await UserManager.ResetPasswordAsync(user.Id, model.Code, model.Password);
            if (result.Succeeded)
            {
                return RedirectToAction("ResetPasswordConfirmation", "Account");
            }
            AddErrors(result);
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