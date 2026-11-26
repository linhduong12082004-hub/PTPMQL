using DemoMVC.Models;
using DemoMVC.Options;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace DemoMVC.Data;

public sealed class IdentitySeeder(
    ApplicationDbContext context,
    RoleManager<IdentityRole> roleManager,
    UserManager<ApplicationUser> userManager,
    IOptions<AdminSeedOptions> seedOptions,
    ILogger<IdentitySeeder> logger)
{
    private static readonly string[] DefaultRoles = ["Administrator", "User"];

    public async Task SeedAsync(CancellationToken cancellationToken = default)
    {
        var settings = seedOptions.Value;
        await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);

        foreach (var roleName in DefaultRoles)
        {
            if (await roleManager.RoleExistsAsync(roleName))
            {
                continue;
            }

            var roleResult = await roleManager.CreateAsync(new IdentityRole(roleName));
            EnsureSucceeded(roleResult, $"creating the {roleName} role");
        }

        var userName = settings.UserName.Trim();
        var email = settings.Email.Trim();
        var admin = await userManager.FindByNameAsync(userName);
        if (admin is null)
        {
            if (await userManager.FindByEmailAsync(email) is not null)
            {
                throw new InvalidOperationException(
                    "The configured administrator email already belongs to a different account.");
            }

            admin = new ApplicationUser
            {
                UserName = userName,
                Email = email,
                FullName = "Administrator",
                IsActive = false,
                EmailConfirmed = false
            };

            var createResult = await userManager.CreateAsync(admin, settings.Password);
            EnsureSucceeded(createResult, "creating the configured administrator");

            var roleResult = await userManager.AddToRoleAsync(admin, "Administrator");
            EnsureSucceeded(roleResult, "assigning the Administrator role to the configured seed account");
        }
        else
        {
            if (!string.Equals(admin.Email, email, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(
                    "The configured administrator username already exists with a different email address.");
            }

            if (!await userManager.IsInRoleAsync(admin, "Administrator"))
            {
                throw new InvalidOperationException(
                    "The configured administrator account exists without the Administrator role. " +
                    "Assign the role explicitly before starting the application; the seeder will not elevate an existing account.");
            }
        }

        await transaction.CommitAsync(cancellationToken);
        logger.LogInformation("Identity roles and the configured seed account are ready.");
    }

    private static void EnsureSucceeded(IdentityResult result, string operation)
    {
        if (!result.Succeeded)
        {
            var errorCodes = string.Join(", ", result.Errors.Select(error => error.Code));
            throw new InvalidOperationException(
                $"Identity seeding failed while {operation}. Identity error codes: {errorCodes}");
        }
    }
}
