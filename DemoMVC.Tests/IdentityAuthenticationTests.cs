using System.Net;
using System.Text.RegularExpressions;
using DemoMVC.Data;
using DemoMVC.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace DemoMVC.Tests;

public sealed class IdentityAuthenticationTests
{
    [Fact]
    public async Task AnonymousPagesArePublicAndBusinessPagesRequireAuthentication()
    {
        using var factory = new AuthWebApplicationFactory();
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            BaseAddress = new Uri("https://localhost")
        });

        var homeResponse = await client.GetAsync("/Home/Privacy");
        var businessResponse = await client.GetAsync("/Student/Index");
        var loginResponse = await client.GetAsync("/Account/Login");
        var stylesheetResponse = await client.GetAsync("/css/site.css");

        Assert.Equal(HttpStatusCode.OK, homeResponse.StatusCode);
        Assert.Equal(HttpStatusCode.Redirect, businessResponse.StatusCode);
        Assert.Contains("/Account/Login", businessResponse.Headers.Location!.ToString());
        Assert.Equal(HttpStatusCode.OK, loginResponse.StatusCode);
        Assert.Equal(HttpStatusCode.OK, stylesheetResponse.StatusCode);
    }

    [Fact]
    public async Task LoginRejectsUnconfirmedAccountsAndUsesOnlyLocalReturnUrls()
    {
        using var factory = new AuthWebApplicationFactory();
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            BaseAddress = new Uri("https://localhost")
        });
        const string password = "ValidPass!123";
        await factory.CreateUserAsync("reader", "reader@example.test", "User", password);
        await factory.CreateUserAsync(
            "pending",
            "pending@example.test",
            "User",
            password,
            emailConfirmed: false,
            isActive: false);

        var token = await GetAntiforgeryTokenAsync(client, "/Account/Login");
        var failedLogin = await PostFormAsync(client, "/Account/Login", token, new Dictionary<string, string>
        {
            ["Email"] = "pending@example.test",
            ["Password"] = password,
            ["ReturnUrl"] = "/Student/Index"
        });
        Assert.Equal(HttpStatusCode.OK, failedLogin.StatusCode);
        Assert.Contains(
            "không hợp lệ",
            WebUtility.HtmlDecode(await failedLogin.Content.ReadAsStringAsync()),
            StringComparison.OrdinalIgnoreCase);

        var successfulLogin = await PostFormAsync(client, "/Account/Login", token, new Dictionary<string, string>
        {
            ["Email"] = "reader@example.test",
            ["Password"] = password,
            ["ReturnUrl"] = "https://attacker.example/"
        });
        Assert.Equal(HttpStatusCode.Redirect, successfulLogin.StatusCode);
        Assert.Equal("/Student", successfulLogin.Headers.Location!.OriginalString);

        var businessResponse = await client.GetAsync("/Student/Index");
        var departmentResponse = await client.GetAsync("/Department/Index");
        var employeeResponse = await client.GetAsync("/NhanVien/Index");
        var adminResponse = await client.GetAsync("/Users/Index");
        Assert.Equal(HttpStatusCode.OK, businessResponse.StatusCode);
        Assert.Equal(HttpStatusCode.OK, departmentResponse.StatusCode);
        Assert.Equal(HttpStatusCode.OK, employeeResponse.StatusCode);
        Assert.Equal(HttpStatusCode.Redirect, adminResponse.StatusCode);
        Assert.Contains("/Account/AccessDenied", adminResponse.Headers.Location!.ToString());

        var departmentToken = await GetAntiforgeryTokenAsync(client, "/Department/Create");
        var createDepartmentResponse = await PostFormAsync(
            client,
            "/Department/Create",
            departmentToken,
            new Dictionary<string, string>
            {
                ["DepartmentCode"] = "AUTH-TEST",
                ["Name"] = "Auth Test Department"
            });
        Assert.Equal(HttpStatusCode.Redirect, createDepartmentResponse.StatusCode);
        using (var scope = factory.Services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            Assert.True(await context.Departments.AnyAsync(department => department.DepartmentCode == "AUTH-TEST"));
        }

        var logoutToken = await GetAntiforgeryTokenAsync(client, "/Student/Index");
        var logoutResponse = await PostFormAsync(client, "/Account/Logout", logoutToken, new Dictionary<string, string>());
        Assert.Equal(HttpStatusCode.Redirect, logoutResponse.StatusCode);

        var afterLogoutResponse = await client.GetAsync("/Student/Index");
        Assert.Equal(HttpStatusCode.Redirect, afterLogoutResponse.StatusCode);
        Assert.Contains("/Account/Login", afterLogoutResponse.Headers.Location!.ToString());
    }

    [Fact]
    public async Task RepeatedInvalidLoginsLockAccount()
    {
        using var factory = new AuthWebApplicationFactory();
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            BaseAddress = new Uri("https://localhost")
        });
        const string email = "lockout-user@example.test";
        const string password = "ValidPass!123";
        await factory.CreateUserAsync("lockout-user", email, "User", password);
        var token = await GetAntiforgeryTokenAsync(client, "/Account/Login");

        for (var attempt = 0; attempt < 5; attempt++)
        {
            var response = await PostFormAsync(client, "/Account/Login", token, new Dictionary<string, string>
            {
                ["Email"] = email,
                ["Password"] = "WrongPass!123"
            });
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }

        using var scope = factory.Services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var user = await userManager.FindByEmailAsync(email);
        Assert.NotNull(user);
        Assert.True(await userManager.IsLockedOutAsync(user));

        var correctPasswordResponse = await PostFormAsync(
            client,
            "/Account/Login",
            token,
            new Dictionary<string, string>
            {
                ["Email"] = email,
                ["Password"] = password
            });
        Assert.Equal(HttpStatusCode.OK, correctPasswordResponse.StatusCode);
        Assert.Contains(
            "bị khóa",
            WebUtility.HtmlDecode(await correctPasswordResponse.Content.ReadAsStringAsync()),
            StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ForgotPasswordSendsResetLinkAndTokenCannotBeReused()
    {
        using var factory = new AuthWebApplicationFactory();
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            BaseAddress = new Uri("https://localhost")
        });
        const string email = "reset-user@example.test";
        const string oldPassword = "OldPass!123";
        const string newPassword = "NewPass!456";
        await factory.CreateUserAsync("reset-user", email, "User", oldPassword);

        var forgotToken = await GetAntiforgeryTokenAsync(client, "/Account/ForgotPassword");
        var forgotResponse = await PostFormAsync(
            client,
            "/Account/ForgotPassword",
            forgotToken,
            new Dictionary<string, string> { ["Email"] = email });
        Assert.Equal(HttpStatusCode.OK, forgotResponse.StatusCode);
        Assert.Contains(
            "Nếu email",
            WebUtility.HtmlDecode(await forgotResponse.Content.ReadAsStringAsync()),
            StringComparison.OrdinalIgnoreCase);

        var sentEmail = Assert.Single(factory.EmailSender.Messages);
        Assert.Equal("Đặt lại mật khẩu", sentEmail.Subject);
        var query = QueryHelpers.ParseQuery(sentEmail.ActionUri.Query);
        var resetPath = sentEmail.ActionUri.PathAndQuery;
        var resetPage = await client.GetAsync(resetPath);
        Assert.Equal(HttpStatusCode.OK, resetPage.StatusCode);
        Assert.True(resetPage.Headers.CacheControl!.NoStore);
        Assert.Equal("no-referrer", Assert.Single(resetPage.Headers.GetValues("Referrer-Policy")));
        var resetToken = ExtractAntiforgeryToken(await resetPage.Content.ReadAsStringAsync());
        var resetResponse = await PostFormAsync(client, "/Account/ResetPassword", resetToken, new Dictionary<string, string>
        {
            ["Email"] = query["email"].ToString(),
            ["Token"] = query["token"].ToString(),
            ["Password"] = newPassword,
            ["ConfirmPassword"] = newPassword
        });

        Assert.Equal(HttpStatusCode.OK, resetResponse.StatusCode);
        Assert.True(resetResponse.Headers.CacheControl!.NoStore);
        Assert.Equal("no-referrer", Assert.Single(resetResponse.Headers.GetValues("Referrer-Policy")));
        Assert.Contains(
            "thành công",
            WebUtility.HtmlDecode(await resetResponse.Content.ReadAsStringAsync()),
            StringComparison.OrdinalIgnoreCase);

        using var scope = factory.Services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var user = await userManager.FindByEmailAsync(email);
        Assert.NotNull(user);
        Assert.True(await userManager.CheckPasswordAsync(user, newPassword));
        Assert.False(await userManager.CheckPasswordAsync(user, oldPassword));

        var replayToken = await GetAntiforgeryTokenAsync(client, resetPath);
        var replayResponse = await PostFormAsync(client, "/Account/ResetPassword", replayToken, new Dictionary<string, string>
        {
            ["Email"] = query["email"].ToString(),
            ["Token"] = query["token"].ToString(),
            ["Password"] = "ThirdPass!789",
            ["ConfirmPassword"] = "ThirdPass!789"
        });
        Assert.Equal(HttpStatusCode.OK, replayResponse.StatusCode);

        using var verificationScope = factory.Services.CreateScope();
        var verificationUserManager = verificationScope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var resetUser = await verificationUserManager.FindByEmailAsync(email);
        Assert.NotNull(resetUser);
        Assert.True(await verificationUserManager.CheckPasswordAsync(resetUser, newPassword));
        Assert.False(await verificationUserManager.CheckPasswordAsync(resetUser, "ThirdPass!789"));
    }

    [Fact]
    public async Task AdministratorInvitationConfirmsEmailAndSetsInitialPassword()
    {
        using var factory = new AuthWebApplicationFactory();
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            BaseAddress = new Uri("https://localhost")
        });
        await factory.CreateUserAsync(
            "test-admin",
            "test-admin@example.test",
            "Administrator",
            "AdminPass!123");

        var loginToken = await GetAntiforgeryTokenAsync(client, "/Account/Login");
        var loginResponse = await PostFormAsync(client, "/Account/Login", loginToken, new Dictionary<string, string>
        {
            ["Email"] = "test-admin@example.test",
            ["Password"] = "AdminPass!123"
        });
        Assert.Equal(HttpStatusCode.Redirect, loginResponse.StatusCode);

        var createToken = await GetAntiforgeryTokenAsync(client, "/Users/Create");
        var createResponse = await PostFormAsync(client, "/Users/Create", createToken, new Dictionary<string, string>
        {
            ["UserName"] = "invited",
            ["Email"] = "invited@example.test",
            ["FullName"] = "Invited User",
            ["PhoneNumber"] = ""
        });
        Assert.Equal(HttpStatusCode.Redirect, createResponse.StatusCode);

        var invitation = Assert.Single(factory.EmailSender.Messages);
        Assert.Equal("Kích hoạt tài khoản DemoMVC", invitation.Subject);
        using (var scope = factory.Services.CreateScope())
        {
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var initialInvitedUser = await userManager.FindByEmailAsync("invited@example.test");
            Assert.NotNull(initialInvitedUser);
            Assert.False(initialInvitedUser.EmailConfirmed);
            Assert.False(initialInvitedUser.IsActive);
            Assert.False(await userManager.HasPasswordAsync(initialInvitedUser));
            Assert.Contains("User", await userManager.GetRolesAsync(initialInvitedUser));
        }

        var invitationPath = invitation.ActionUri.PathAndQuery;
        var acceptPage = await client.GetAsync(invitationPath);
        Assert.Equal(HttpStatusCode.OK, acceptPage.StatusCode);
        Assert.True(acceptPage.Headers.CacheControl!.NoStore);
        Assert.Equal("no-referrer", Assert.Single(acceptPage.Headers.GetValues("Referrer-Policy")));
        var acceptHtml = await acceptPage.Content.ReadAsStringAsync();
        var acceptToken = ExtractAntiforgeryToken(acceptHtml);
        var acceptResponse = await PostFormAsync(client, "/Account/AcceptInvitation", acceptToken, new Dictionary<string, string>
        {
            ["UserId"] = ExtractInputValue(acceptHtml, "UserId"),
            ["EmailToken"] = ExtractInputValue(acceptHtml, "EmailToken"),
            ["PasswordToken"] = ExtractInputValue(acceptHtml, "PasswordToken"),
            ["Password"] = "InvitedPass!123",
            ["ConfirmPassword"] = "InvitedPass!123"
        });

        Assert.Equal(HttpStatusCode.OK, acceptResponse.StatusCode);
        Assert.True(acceptResponse.Headers.CacheControl!.NoStore);
        Assert.Contains(
            "đã được kích hoạt",
            WebUtility.HtmlDecode(await acceptResponse.Content.ReadAsStringAsync()),
            StringComparison.OrdinalIgnoreCase);

        using var verificationScope = factory.Services.CreateScope();
        var verificationUserManager = verificationScope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var invitedUser = await verificationUserManager.FindByEmailAsync("invited@example.test");
        Assert.NotNull(invitedUser);
        Assert.True(invitedUser.EmailConfirmed);
        Assert.True(invitedUser.IsActive);
        Assert.True(await verificationUserManager.CheckPasswordAsync(invitedUser, "InvitedPass!123"));
    }

    [Fact]
    public async Task SeedIsIdempotentAndDoesNotOverwriteExistingAdminPassword()
    {
        using var factory = new AuthWebApplicationFactory();
        using var scope = factory.Services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var user = await userManager.FindByNameAsync("seed-admin");
        Assert.NotNull(user);
        Assert.False(user.EmailConfirmed);
        Assert.False(user.IsActive);
        Assert.Contains("Administrator", await userManager.GetRolesAsync(user));

        const string replacementPassword = "ChangedSeed!456";
        var resetToken = await userManager.GeneratePasswordResetTokenAsync(user);
        var resetResult = await userManager.ResetPasswordAsync(user, resetToken, replacementPassword);
        Assert.True(resetResult.Succeeded);

        await scope.ServiceProvider.GetRequiredService<IdentitySeeder>().SeedAsync();

        var usersWithSeedEmail = await userManager.Users
            .Where(candidate => candidate.Email == "seed-admin@example.test")
            .CountAsync();
        Assert.Equal(1, usersWithSeedEmail);
        Assert.True(await userManager.CheckPasswordAsync(user, replacementPassword));
    }

    private static async Task<string> GetAntiforgeryTokenAsync(HttpClient client, string path)
    {
        var response = await client.GetAsync(path);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return ExtractAntiforgeryToken(await response.Content.ReadAsStringAsync());
    }

    private static string ExtractAntiforgeryToken(string html)
    {
        var token = Regex.Match(
            html,
            "<input\\b(?=[^>]*\\bname=\"__RequestVerificationToken\")(?=[^>]*\\bvalue=\"(?<value>[^\"]+)\")[^>]*>",
            RegexOptions.IgnoreCase);
        Assert.True(token.Success, "The form did not render an antiforgery token.");
        return WebUtility.HtmlDecode(token.Groups["value"].Value);
    }

    private static string ExtractInputValue(string html, string name)
    {
        var input = Regex.Match(
            html,
            $"<input\\b(?=[^>]*\\bname=\"{Regex.Escape(name)}\")(?=[^>]*\\bvalue=\"(?<value>[^\"]*)\")[^>]*>",
            RegexOptions.IgnoreCase);
        Assert.True(input.Success, $"The form did not render {name}.");
        return WebUtility.HtmlDecode(input.Groups["value"].Value);
    }

    private static Task<HttpResponseMessage> PostFormAsync(
        HttpClient client,
        string path,
        string antiforgeryToken,
        IDictionary<string, string> values)
    {
        var form = new Dictionary<string, string>(values)
        {
            ["__RequestVerificationToken"] = antiforgeryToken
        };
        return client.PostAsync(path, new FormUrlEncodedContent(form));
    }
}
