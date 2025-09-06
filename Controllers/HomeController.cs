using System.Web.Mvc;

namespace PiranaSecuritySystem.Controllers
{
    public class HomeController : Controller
    {
        public ActionResult Index()
        {
            // Redirect to login page if user is not authenticated
            if (!User.Identity.IsAuthenticated)
            {
                return RedirectToAction("Login", "Account");
            }

            // If user is authenticated, redirect based on role
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

            return RedirectToAction("Login", "Account");
        }

        public ActionResult About()
        {
            ViewBag.Message = "Your application description page.";
            return View();
        }

        public ActionResult Contact()
        {
            ViewBag.Message = "Your contact page.";
            return View();
        }
    }
}