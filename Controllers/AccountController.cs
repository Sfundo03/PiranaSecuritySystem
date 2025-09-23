using Microsoft.AspNet.Identity;
using Microsoft.AspNet.Identity.Owin;
using Microsoft.Owin.Security;
using PiranaSecuritySystem.Models;
using System;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using System.Web;
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

        // GET: /Account/Login
        [AllowAnonymous]
        public ActionResult Login(string returnUrl)
        {
            // If user is already authenticated, handle redirect safely
            if (User.Identity.IsAuthenticated)
            {
                return HandleAuthenticatedUser();
            }

            ViewBag.ReturnUrl = returnUrl;
            return View();
        }

        // POST: /Account/Login - SIMPLIFIED VERSION
        [HttpPost]
        [AllowAnonymous]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> Login(LoginViewModel model, string returnUrl)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            // First, try default credentials
            var defaultRedirect = await TryDefaultCredentials(model);
            if (defaultRedirect != null)
            {
                return defaultRedirect;
            }

            // Regular user login
            try
            {
                // Use the email as username for login
                var result = await SignInManager.PasswordSignInAsync(model.Email, model.Password, model.RememberMe, shouldLockout: false);

                switch (result)
                {
                    case SignInStatus.Success:
                        return await HandleSuccessfulLogin(model.Email, returnUrl);

                    case SignInStatus.LockedOut:
                        return View("Lockout");

                    case SignInStatus.Failure:
                    default:
                        ModelState.AddModelError("", "Invalid login attempt.");
                        return View(model);
                }
            }
            catch (Exception ex)
            {
                ModelState.AddModelError("", "An error occurred during login: " + ex.Message);
                return View(model);
            }
        }

        private async Task<ActionResult> TryDefaultCredentials(LoginViewModel model)
        {
            // Default admin
            if (model.Email.Equals(DefaultAdminEmail, StringComparison.OrdinalIgnoreCase) &&
                model.Password == DefaultAdminPassword)
            {
                await SignInDefaultUser(DefaultAdminEmail, "Admin", "admin-default-id");
                return RedirectToAction("Dashboard", "Admin");
            }

            // Default director
            if (model.Email.Equals(DefaultDirectorEmail, StringComparison.OrdinalIgnoreCase) &&
                model.Password == DefaultDirectorPassword)
            {
                await SignInDefaultUser(DefaultDirectorEmail, "Director", "director-default-id");
                return RedirectToAction("Dashboard", "Director");
            }

            return null;
        }

        private async Task SignInDefaultUser(string email, string role, string userId)
        {
            // Sign out first
            AuthenticationManager.SignOut(DefaultAuthenticationTypes.ApplicationCookie);

            // Create identity
            var identity = new ClaimsIdentity(DefaultAuthenticationTypes.ApplicationCookie);
            identity.AddClaim(new Claim(ClaimTypes.NameIdentifier, userId));
            identity.AddClaim(new Claim(ClaimTypes.Name, $"Default {role}"));
            identity.AddClaim(new Claim(ClaimTypes.Email, email));
            identity.AddClaim(new Claim(ClaimTypes.Role, role));

            // Sign in
            AuthenticationManager.SignIn(new AuthenticationProperties { IsPersistent = false }, identity);
        }

        private async Task<ActionResult> HandleSuccessfulLogin(string email, string returnUrl)
        {
            var user = await UserManager.FindByEmailAsync(email);
            if (user == null)
            {
                ModelState.AddModelError("", "User not found.");
                return View("Login");
            }

            // Get user roles
            var roles = await UserManager.GetRolesAsync(user.Id);

            // Redirect based on first role found
            if (roles.Contains("Admin"))
                return RedirectToAction("Dashboard", "Admin");
            if (roles.Contains("Director"))
                return RedirectToAction("Dashboard", "Director");
            if (roles.Contains("Guard"))
                return RedirectToAction("Dashboard", "Guard");
            if (roles.Contains("Instructor"))
                return RedirectToAction("Dashboard", "Instructor");
            if (roles.Contains("Resident"))
                return RedirectToAction("Dashboard", "Resident"); // Use simple dashboard first

            // No roles found - redirect to home
            return RedirectToAction("Index", "Home");
        }

        private ActionResult HandleAuthenticatedUser()
        {
            // For already authenticated users, redirect based on role
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

        // GET: /Account/TestAuth
        [AllowAnonymous]
        public ActionResult TestAuth()
        {
            var info = $"<h1>Auth Test</h1>" +
                       $"<p>IsAuthenticated: {User.Identity.IsAuthenticated}</p>" +
                       $"<p>UserName: {User.Identity.Name}</p>" +
                       $"<p>UserId: {User.Identity.GetUserId()}</p>" +
                       $"<p>Roles: {string.Join(", ", GetUserRoles())}</p>" +
                       $"<p><a href='/Resident/SimpleDashboard'>Test Resident Dashboard</a></p>" +
                       $"<p><a href='/Account/Logout'>Logout</a></p>";

            return Content(info, "text/html");
        }

        private string[] GetUserRoles()
        {
            if (User.Identity.IsAuthenticated)
            {
                var userManager = HttpContext.GetOwinContext().GetUserManager<ApplicationUserManager>();
                var roles = userManager.GetRoles(User.Identity.GetUserId());
                return roles.ToArray();
            }
            return new string[0];
        }

        // POST: /Account/LogOff
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult LogOff()
        {
            AuthenticationManager.SignOut(DefaultAuthenticationTypes.ApplicationCookie);
            Session.Clear();
            return RedirectToAction("Login", "Account");
        }

        // GET: /Account/LogOff
        [HttpGet]
        public ActionResult Logout()
        {
            AuthenticationManager.SignOut(DefaultAuthenticationTypes.ApplicationCookie);
            Session.Clear();
            return RedirectToAction("Login", "Account");
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

        // Add ChallengeResult class back for ManageController
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

        private const string XsrfKey = "XsrfId";
        #endregion
    }
}