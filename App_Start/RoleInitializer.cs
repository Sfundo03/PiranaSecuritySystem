using Microsoft.AspNet.Identity;
using Microsoft.AspNet.Identity.EntityFramework;
using PiranaSecuritySystem.Models;
using System;
using System.Data.Entity;

namespace PiranaSecuritySystem
{
    public class RoleInitializer
    {
        public static void InitializeRoles()
        {
            using (var context = new ApplicationDbContext())
            {
                var roleManager = new RoleManager<IdentityRole>(new RoleStore<IdentityRole>(context));

                // Create Resident role if it doesn't exist
                if (!roleManager.RoleExists("Resident"))
                {
                    roleManager.Create(new IdentityRole("Resident"));
                }

                // Create Director role if it doesn't exist
                if (!roleManager.RoleExists("Director"))
                {
                    roleManager.Create(new IdentityRole("Director"));
                }

                // Create Admin role if it doesn't exist
                if (!roleManager.RoleExists("Admin"))
                {
                    roleManager.Create(new IdentityRole("Admin"));
                }

                context.SaveChanges();
            }
        }
    }
}