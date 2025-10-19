using Models.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;

namespace Utilities.Utils
{
    public static class SeedData
    {
        public static async Task InitializeAsync(IServiceProvider serviceProvider)
        {
            var roleManager = serviceProvider.GetRequiredService<RoleManager<IdentityRole<Guid>>>();
            var userManager = serviceProvider.GetRequiredService<UserManager<User>>();

            string[] roleNames = { "admin", "user", "company", "banned" };

            foreach (var roleName in roleNames)
            {
                if (!await roleManager.RoleExistsAsync(roleName))
                {
                    var role = new IdentityRole<Guid> { Name = roleName };
                    await roleManager.CreateAsync(role);
                }
            }

            // default admin user
            var adminEmail = "admin@gmail.com";
            var adminPassword = "123123";
            var companyEmail = "company@gmail.com";
            var userEmail = "user@gmail.com";

            var adminUser = await userManager.FindByEmailAsync(adminEmail);
            var companyUser = await userManager.FindByEmailAsync(companyEmail);
            var userUser = await userManager.FindByEmailAsync(userEmail);
            if (adminUser == null)
            {
                adminUser = new User
                {
                    UserName = "Admin",
                    Email = adminEmail,
                    EmailConfirmed = true
                };
                var result = await userManager.CreateAsync(adminUser, adminPassword);

                if (result.Succeeded)
                {
                    await userManager.AddToRoleAsync(adminUser, "admin");
                }
            }

            if (companyUser == null)
            {
                companyUser = new User
                {
                    UserName = "Company",
                    Email = companyEmail,
                    EmailConfirmed = true,
                    IsBanned = false,
                    IsCompany = true
                };
                var result = await userManager.CreateAsync(companyUser, adminPassword);

                if (result.Succeeded)
                {
                    await userManager.AddToRoleAsync(companyUser, "company");
                }
            }

            if (userUser == null)
            {
                userUser = new User
                {
                    UserName = "User",
                    Email = userEmail,
                    EmailConfirmed = true,
                    IsBanned = false,
                    IsCompany = true
                };
                var result = await userManager.CreateAsync(userUser, adminPassword);

                if (result.Succeeded)
                {
                    await userManager.AddToRoleAsync(userUser, "user");
                }
            }
        }

    }
}
