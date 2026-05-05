using Microsoft.AspNetCore.Identity;
using Serilog;
using TimeManagementBackend.Models;

namespace TimeManagementBackend.Data;

public static class Seeder
{
    public static async Task SeedAdminAsync(IServiceProvider services)
    {
        var email    = Environment.GetEnvironmentVariable("ADMIN_SEED_EMAIL");
        var password = Environment.GetEnvironmentVariable("ADMIN_SEED_PASSWORD");

        if (string.IsNullOrEmpty(email) || string.IsNullOrEmpty(password))
            return;

        using var scope       = services.CreateScope();
        var userManager       = scope.ServiceProvider.GetRequiredService<UserManager<User>>();
        var roleManager       = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();

        if (!await roleManager.RoleExistsAsync("Admin"))
            await roleManager.CreateAsync(new IdentityRole("Admin"));

        if (await userManager.FindByEmailAsync(email) is not null)
        {
            Log.Information("Seed admin already exists, skipping: {Email}", email);
            return;
        }

        var admin  = new User { UserName = email, Email = email, EmailConfirmed = true };
        var result = await userManager.CreateAsync(admin, password);

        if (result.Succeeded)
        {
            await userManager.AddToRoleAsync(admin, "Admin");
            Log.Information("Seed admin created: {Email}", email);
        }
        else
        {
            Log.Warning("Seed admin creation failed: {Errors}", string.Join(", ", result.Errors.Select(e => e.Description)));
        }
    }
}
