
using DemoMVC.Data;
using DemoMVC.Models;
using DemoMVC.Options;
using DemoMVC.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Authorization;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);
var isDevelopment = builder.Environment.IsDevelopment();
var hasAdminSeed = HasConfigurationValues(
    builder.Configuration,
    "AdminSeed:UserName",
    "AdminSeed:Email",
    "AdminSeed:Password");
var hasSmtpCredentials = HasConfigurationValues(
    builder.Configuration,
    "Email:SmtpUserName",
    "Email:SmtpPassword");
var canInitializeIdentity = hasAdminSeed && hasSmtpCredentials;

// Add services to the container.
builder.Services.AddControllersWithViews(options =>
{
    options.Filters.Add(new AuthorizeFilter(
        new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build()));
    options.Filters.Add(new AutoValidateAntiforgeryTokenAttribute());
});

builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlite(
        builder.Configuration.GetConnectionString("DefaultConnection")
    )
);

builder.Services
    .AddIdentity<ApplicationUser, IdentityRole>(options =>
    {
        options.Password.RequiredLength = 8;
        options.Password.RequireDigit = true;
        options.Password.RequireLowercase = true;
        options.Password.RequireUppercase = true;
        options.Password.RequireNonAlphanumeric = true;
        options.User.RequireUniqueEmail = true;
        options.SignIn.RequireConfirmedEmail = true;
        options.Lockout.MaxFailedAccessAttempts = 5;
        options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
        options.Lockout.AllowedForNewUsers = true;
    })
    .AddEntityFrameworkStores<ApplicationDbContext>()
    .AddDefaultTokenProviders();

builder.Services.ConfigureApplicationCookie(options =>
{
    options.LoginPath = "/Account/Login";
    options.AccessDeniedPath = "/Account/AccessDenied";
    options.Cookie.HttpOnly = true;
    options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
    options.Cookie.SameSite = SameSiteMode.Lax;
    options.ExpireTimeSpan = TimeSpan.FromHours(8);
    options.SlidingExpiration = true;
});

builder.Services.Configure<DataProtectionTokenProviderOptions>(options =>
    options.TokenLifespan = TimeSpan.FromHours(1));
builder.Services.Configure<SecurityStampValidatorOptions>(options =>
    options.ValidationInterval = TimeSpan.Zero);

var adminSeedOptions = builder.Services
    .AddOptions<AdminSeedOptions>()
    .Bind(builder.Configuration.GetSection(AdminSeedOptions.SectionName));
if (!isDevelopment || canInitializeIdentity)
{
    adminSeedOptions
        .ValidateDataAnnotations()
        .Validate(options => !string.IsNullOrWhiteSpace(options.UserName), "AdminSeed:UserName is required.")
        .Validate(options => !string.IsNullOrWhiteSpace(options.Email), "AdminSeed:Email is required.")
        .Validate(options => !string.IsNullOrWhiteSpace(options.Password), "AdminSeed:Password is required.")
        .ValidateOnStart();
}

var emailOptions = builder.Services
    .AddOptions<EmailOptions>()
    .Bind(builder.Configuration.GetSection(EmailOptions.SectionName))
    .Validate(
        options => Uri.TryCreate(options.PublicBaseUrl, UriKind.Absolute, out var uri) &&
                   (uri.Scheme == Uri.UriSchemeHttps ||
                    (isDevelopment && uri.IsLoopback)),
        "Email:PublicBaseUrl must be an absolute HTTPS URL (HTTP loopback is allowed only in Development).")
    .ValidateOnStart();
if (!isDevelopment || hasSmtpCredentials)
{
    emailOptions.ValidateDataAnnotations();
}

builder.Services.AddScoped<IdentitySeeder>();
if (!isDevelopment || canInitializeIdentity)
{
    builder.Services.AddHostedService<IdentitySeedHostedService>();
}
builder.Services.AddSingleton<IEmailRequestLimiter, EmailRequestLimiter>();
if (isDevelopment && !hasSmtpCredentials)
{
    builder.Services.AddSingleton<IEmailSender, DevelopmentEmailUnavailableSender>();
}
else
{
    builder.Services.AddTransient<IEmailSender, SmtpEmailSender>();
}

var app = builder.Build();

if (isDevelopment && !canInitializeIdentity)
{
    app.Logger.LogWarning(
        "Development setup is incomplete. Identity seeding and email sending are disabled until AdminSeed and SMTP credentials are configured.");
}

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

app.MapStaticAssets();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}")
    .WithStaticAssets();

app.Run();

static bool HasConfigurationValues(IConfiguration configuration, params string[] keys)
{
    return keys.All(key => !string.IsNullOrWhiteSpace(configuration[key]));
}

public partial class Program { }
