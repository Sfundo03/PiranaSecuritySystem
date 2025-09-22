using System.Web.Mvc;

namespace PiranaSecuritySystem.Filters
{
    public class RoleAuthorizationFilter : ActionFilterAttribute
    {
        public override void OnActionExecuting(ActionExecutingContext filterContext)
        {
            var controllerName = filterContext.ActionDescriptor.ControllerDescriptor.ControllerName;
            var actionName = filterContext.ActionDescriptor.ActionName;

            // Skip authorization for Account controller and Error action
            if (controllerName == "Account" || actionName == "Error")
            {
                return;
            }

            var userRole = filterContext.HttpContext.Session["UserRole"]?.ToString();

            // Allow access to login page even if no role is set
            if (string.IsNullOrEmpty(userRole))
            {
                filterContext.Result = new RedirectResult("~/Account/Login");
                return;
            }

            base.OnActionExecuting(filterContext);
        }
    }
}