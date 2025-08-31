using Microsoft.Owin;
using Owin;

[assembly: OwinStartupAttribute(typeof(PiranaSecuritySystem.Startup))]
namespace PiranaSecuritySystem
{
    public partial class Startup
    {
        public void Configuration(IAppBuilder app)
        {
            ConfigureAuth(app);
        }
    }
}
