namespace DemoMVC.Services;

public sealed class DevelopmentEmailUnavailableSender : IEmailSender
{
    public Task SendAccountActionAsync(
        string recipientEmail,
        string subject,
        string heading,
        string message,
        string actionText,
        Uri actionUri,
        CancellationToken cancellationToken = default)
    {
        throw new EmailDeliveryException(
            "Email sending is unavailable until SMTP credentials are configured.",
            new InvalidOperationException("SMTP credentials are not configured in Development."));
    }
}
