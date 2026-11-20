using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Threading.Tasks;
using InvoiceScanner.Models;

namespace InvoiceScanner.Data
{
    public class AdminSeedData
    {

        public static async Task CreateUsersAndRoles(IServiceProvider serviceProvider)
        {
            //initializing custom roles 
            var RoleManager = serviceProvider.GetRequiredService<RoleManager<ApplicationRole>>();
            var UserManager = serviceProvider.GetRequiredService<UserManager<ApplicationUser>>();

            string[] roleNames =
            {
                "Administrator",
                "Business Operator",
                "Business Administrator",
                "Supervisor"
            };
            IdentityResult roleResult;

            foreach (var roleName in roleNames)
            {
                var roleExist = await RoleManager.RoleExistsAsync(roleName);
                // ensure that the role does not exist
                if (!roleExist)
                {
                    //create the roles and seed them to the database: 
                    roleResult = await RoleManager.CreateAsync(new ApplicationRole(roleName));
                }
            }

            // find the user with the admin email 
            var _user = await UserManager.FindByNameAsync("joshua@webcorporation.co.tz");
            // check if the user exists
            if (_user == null)
            {
                //Here you could create the super admin who will maintain the web app
                var poweruser = new ApplicationUser
                {
                    UserName = "joshua@webcorporation.co.tz",
                    Email = "joshua@webcorporation.co.tz",
                    Name = "Joshua Nelson"
                };
                string adminPassword = "Mpanda.923";

                var createPowerUser = await UserManager.CreateAsync(poweruser, adminPassword);
                if (createPowerUser.Succeeded)
                {
                    //here we tie the new user to the role
                    await UserManager.AddToRoleAsync(poweruser, "Administrator");
                }
            }
        }
    }
}
