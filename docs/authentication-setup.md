# Authentication setup

The application uses ASP.NET Core Identity with the existing SQLite `ApplicationDbContext`. Identity tables are added by an EF Core migration; existing student, employee, and department tables are retained.

## Local configuration

Do not put administrator passwords or SMTP credentials in `appsettings*.json` or source control. The project has a User Secrets ID. From the repository root, configure development values with:

```powershell
dotnet user-secrets set "AdminSeed:UserName" "admin" --project "DemoMVC\DemoMVC.csproj"
dotnet user-secrets set "AdminSeed:Email" "your-admin-address@example.com" --project "DemoMVC\DemoMVC.csproj"
dotnet user-secrets set "AdminSeed:Password" "<a-unique-strong-password>" --project "DemoMVC\DemoMVC.csproj"
dotnet user-secrets set "Email:SmtpUserName" "your-gmail-address@gmail.com" --project "DemoMVC\DemoMVC.csproj"
dotnet user-secrets set "Email:SmtpPassword" "<gmail-app-password>" --project "DemoMVC\DemoMVC.csproj"
```

For Gmail SMTP, enable two-step verification and use an App Password. Do not use the normal Google account password. `Email:SenderEmail` must be the Gmail mailbox or a sender alias authorized by that account; the committed `no-reply@example.com` value is only a placeholder. Set the real sender in User Secrets for development and in the secret store/environment for deployment.

The default development link base is `https://localhost:7127`. It is only suitable for a browser running on the development machine. Set `Email:PublicBaseUrl` to the externally reachable HTTPS origin before sending links to users outside that machine.

Supported settings can also be supplied as environment variables, for example `AdminSeed__Password`, `Email__SmtpUserName`, and `Email__SmtpPassword`. Missing required seed or SMTP settings fail startup validation; no default password or SMTP credential is generated.

## Database and seed

The application does not apply schema changes automatically at startup. From the `DemoMVC` directory, apply the additive Identity migration to the development SQLite database:

```powershell
dotnet ef database update
```

The migration creates the Identity tables and adds `FullName` and `IsActive` to `AspNetUsers`. It does not delete or rewrite business tables. For production, review and apply the migration through the deployment process after taking a database backup.

At startup, the seeder creates the `Administrator` and `User` roles and the configured administrator only if they do not already exist. It never resets an existing password or elevates an existing account. A newly seeded administrator remains inactive until its email is confirmed. Use the public **Gửi lại email xác minh** action to send the Identity confirmation link after SMTP is configured.

Administrators create standard users from **Quản lý người dùng**. The user receives a one-hour invitation link, confirms the email address, and sets their own initial password. Self-registration is disabled.

## Email and account behavior

- SMTP uses `smtp.gmail.com:587` with STARTTLS. MailKit sends HTML and text alternatives.
- Confirmation, invitation, and password-reset links use Identity tokens with a one-hour lifetime.
- Forgot-password responses do not disclose whether an address exists. Email requests are limited to five per account and client address per hour in the current process.
- The email limiter is in-memory and resets on process restart. Multi-instance deployments should replace it with a shared distributed limiter.
- Passwords require at least eight characters, upper- and lower-case letters, a digit, and a non-alphanumeric character. Failed login attempts are locked for 15 minutes after five failures.
- `User` can perform CRUD in the three business modules. `Administrator` can also manage users and roles. The final active administrator cannot be disabled or demoted.

## Verification

Run the integration tests, which use temporary SQLite databases and a fake email sender:

```powershell
dotnet test NetCoreAPI.sln
```

The tests do not contact Gmail. Configure valid SMTP credentials and an accessible sender address separately to verify real delivery.
