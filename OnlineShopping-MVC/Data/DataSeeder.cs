using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using OnlineShopping_MVC.Models.Identity;

namespace OnlineShopping_MVC.Data;

public static class DataSeeder
{
    public static async Task SeedAsync(IServiceProvider services)
    {
        using var scope = services.CreateScope();
        var roles = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();

        foreach (var role in new[] { AppRoles.Admin, AppRoles.Seller, AppRoles.Customer })
        {
            if (!await roles.RoleExistsAsync(role))
                await roles.CreateAsync(new IdentityRole(role));
        }

        var adminEmail = "admin@shop.local";
        var admin = await users.FindByEmailAsync(adminEmail);
        if (admin is null)
        {
            admin = new AppUser { UserName = adminEmail, Email = adminEmail, EmailConfirmed = true, DisplayName = "Admin" };
            await users.CreateAsync(admin, "Passw0rd!");
            await users.AddToRoleAsync(admin, AppRoles.Admin);
        }
    }
}
