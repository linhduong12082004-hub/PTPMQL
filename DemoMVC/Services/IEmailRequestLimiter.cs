namespace DemoMVC.Services;

public interface IEmailRequestLimiter
{
    bool TryAcquire(string? email, string? clientAddress);
}
