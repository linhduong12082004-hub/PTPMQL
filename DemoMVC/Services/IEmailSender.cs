namespace DemoMVC.Services;

public interface IEmailSender
{
    Task SendAccountActionAsync(
        string recipientEmail,
        string subject,
        string heading,
        string message,
        string actionText,
        Uri actionUri,
        CancellationToken cancellationToken = default);
}
