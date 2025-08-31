using Microsoft.Owin;
using Owin;
using System.Security.Claims;
using System.Web.Helpers;

[assembly: OwinStartupAttribute(typeof(PiranaSecuritySystem.Startup))]
namespace PiranaSecuritySystem
{
    public partial class Startup
    {
        public void Configuration(IAppBuilder app)
        {
            ConfigureAuth(app);
        }

        protected void Application_Start()
        {
            // Other configuration...
            AntiForgeryConfig.UniqueClaimTypeIdentifier = ClaimTypes.NameIdentifier;
            // Or if using a different claim type:
            // AntiForgeryConfig.UniqueClaimTypeIdentifier = "sub";
        }
    }
}
