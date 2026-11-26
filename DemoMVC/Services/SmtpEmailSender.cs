using System.Text.Encodings.Web;
using DemoMVC.Options;
using MailKit;
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Options;
using MimeKit;

namespace DemoMVC.Services;

public sealed class SmtpEmailSender(
    IOptions<EmailOptions> emailOptions,
    ILogger<SmtpEmailSender> logger) : IEmailSender
{
    public async Task SendAccountActionAsync(
        string recipientEmail,
        string subject,
        string heading,
        string message,
        string actionText,
        Uri actionUri,
        CancellationToken cancellationToken = default)
    {
        var settings = emailOptions.Value;
        var htmlHeading = HtmlEncoder.Default.Encode(heading);
        var htmlMessage = HtmlEncoder.Default.Encode(message);
        var htmlActionText = HtmlEncoder.Default.Encode(actionText);
        var htmlLink = HtmlEncoder.Default.Encode(actionUri.AbsoluteUri);

        var email = new MimeMessage();
        email.From.Add(new MailboxAddress(settings.SenderName, settings.SenderEmail));
        email.To.Add(MailboxAddress.Parse(recipientEmail));
        email.Subject = subject;
        email.Body = new BodyBuilder
        {
            HtmlBody = $"""
                <!doctype html>
                <html lang="vi">
                <body style="margin:0;background:#f4f6f8;font-family:Arial,sans-serif;color:#212529">
                    <main style="max-width:600px;margin:32px auto;padding:32px;background:#fff;border-radius:8px">
                        <h1 style="font-size:22px">{htmlHeading}</h1>
                        <p style="line-height:1.6">{htmlMessage}</p>
                        <p style="margin:28px 0">
                            <a href="{htmlLink}" style="display:inline-block;padding:12px 20px;background:#0d6efd;color:#fff;text-decoration:none;border-radius:4px">
                                {htmlActionText}
                            </a>
                        </p>
                        <p style="font-size:13px;color:#6c757d">Nếu bạn không yêu cầu thao tác này, hãy bỏ qua email.</p>
                    </main>
                </body>
                </html>
                """,
            TextBody = $"{heading}\n\n{message}\n\n{actionText}: {actionUri.AbsoluteUri}\n\nNếu bạn không yêu cầu thao tác này, hãy bỏ qua email."
        }.ToMessageBody();

        try
        {
            using var client = new SmtpClient();
            client.Timeout = 15000;
            await client.ConnectAsync(
                settings.SmtpHost,
                settings.SmtpPort,
                SecureSocketOptions.StartTls,
                cancellationToken);
            await client.AuthenticateAsync(
                settings.SmtpUserName,
                settings.SmtpPassword,
                cancellationToken);
            await client.SendAsync(email, cancellationToken);
            await client.DisconnectAsync(true, cancellationToken);
            logger.LogInformation("Account email delivered through the configured SMTP provider.");
        }
        catch (Exception exception) when (
            exception is SmtpCommandException or
                SmtpProtocolException or
                ServiceNotConnectedException or
                ServiceNotAuthenticatedException or
                MailKit.Security.AuthenticationException or
                IOException)
        {
            logger.LogError(
                "SMTP email delivery failed ({FailureType}); recipient and credentials were omitted.",
                exception.GetType().Name);
            throw new EmailDeliveryException(
                "The configured email provider could not deliver the message.",
                exception);
        }
    }
}
