using DemoMVC.Services;

namespace DemoMVC.Tests;

public sealed class EmailRequestLimiterTests
{
    [Fact]
    public void LimitsRequestsByAccountAndByClientAddress()
    {
        var limiter = new EmailRequestLimiter();

        for (var attempt = 0; attempt < 5; attempt++)
        {
            Assert.True(limiter.TryAcquire("user@example.test", "192.0.2.10"));
        }

        Assert.False(limiter.TryAcquire("user@example.test", "192.0.2.10"));
        Assert.False(limiter.TryAcquire("another@example.test", "192.0.2.10"));
        Assert.False(limiter.TryAcquire("user@example.test", "192.0.2.11"));
        Assert.True(limiter.TryAcquire("another@example.test", "192.0.2.11"));
    }
}
