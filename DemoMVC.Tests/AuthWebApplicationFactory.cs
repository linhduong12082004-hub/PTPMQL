using System.Collections.Concurrent;
using DemoMVC.Data;
using DemoMVC.Models;
using DemoMVC.Services;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;

namespace DemoMVC.Tests;

public sealed class AuthWebApplicationFactory : WebApplicationFactory<Program>
{
    private readonly string _databasePath = Path.Combine(
        Path.GetTempPath(),
        $"DemoMVC.Tests-{Guid.NewGuid():N}.db");

    public RecordingEmailSender EmailSender { get; } = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.ConfigureAppConfiguration((_, configuration) =>
        {
            configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:DefaultConnection"] = $"Data Source={_databasePath};Pooling=False",
                ["AdminSeed:UserName"] = "seed-admin",
                ["AdminSeed:Email"] = "seed-admin@example.test",
                ["AdminSeed:Password"] = "SeedAdmin!123",
                ["Email:SmtpHost"] = "smtp.gmail.com",
                ["Email:SmtpPort"] = "587",
                ["Email:SmtpUserName"] = "test-sender@example.test",
                ["Email:SmtpPassword"] = "test-only-password",
                ["Email:SenderEmail"] = "test-sender@example.test",
                ["Email:SenderName"] = "DemoMVC Test",
                ["Email:PublicBaseUrl"] = "https://localhost"
            });
        });

        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<IEmailSender>();
            services.AddSingleton<IEmailSender>(EmailSender);

            var seedHostedService = services.SingleOrDefault(descriptor =>
                descriptor.ServiceType == typeof(IHostedService) &&
                descriptor.ImplementationType == typeof(IdentitySeedHostedService));
            if (seedHostedService is not null)
            {
                services.Remove(seedHostedService);
            }

            services.AddHostedService<TestDatabaseInitializer>();
            services.AddHostedService<IdentitySeedHostedService>();
        });
    }

    public async Task<ApplicationUser> CreateUserAsync(
        string userName,
        string email,
        string role,
        string password = "ValidPass!123",
        bool emailConfirmed = true,
        bool isActive = true)
    {
        using var scope = Services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();

        if (!await roleManager.RoleExistsAsync(role))
        {
            var roleResult = await roleManager.CreateAsync(new IdentityRole(role));
            Assert.True(roleResult.Succeeded);
        }

        var user = new ApplicationUser
        {
            UserName = userName,
            Email = email,
            FullName = userName,
            EmailConfirmed = emailConfirmed,
            IsActive = isActive
        };
        var createResult = await userManager.CreateAsync(user, password);
        Assert.True(createResult.Succeeded, string.Join(", ", createResult.Errors.Select(error => error.Code)));

        var addRoleResult = await userManager.AddToRoleAsync(user, role);
        Assert.True(addRoleResult.Succeeded);
        return user;
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (!disposing)
        {
            return;
        }

        foreach (var suffix in new[] { string.Empty, "-shm", "-wal" })
        {
            var filePath = _databasePath + suffix;
            if (File.Exists(filePath))
            {
                File.Delete(filePath);
            }
        }
    }
}

public sealed class RecordingEmailSender : IEmailSender
{
    private readonly ConcurrentQueue<SentAccountEmail> _messages = new();

    public IReadOnlyCollection<SentAccountEmail> Messages => _messages.ToArray();

    public Task SendAccountActionAsync(
        string recipientEmail,
        string subject,
        string heading,
        string message,
        string actionText,
        Uri actionUri,
        CancellationToken cancellationToken = default)
    {
        _messages.Enqueue(new SentAccountEmail(
            recipientEmail,
            subject,
            heading,
            message,
            actionText,
            actionUri));
        return Task.CompletedTask;
    }
}

public sealed record SentAccountEmail(
    string RecipientEmail,
    string Subject,
    string Heading,
    string Message,
    string ActionText,
    Uri ActionUri);

internal sealed class TestDatabaseInitializer(IServiceScopeFactory scopeFactory) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        await context.Database.MigrateAsync(cancellationToken);
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }
}
