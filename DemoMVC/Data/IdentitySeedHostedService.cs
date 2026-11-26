namespace DemoMVC.Data;

public sealed class IdentitySeedHostedService(
    IServiceScopeFactory scopeFactory,
    ILogger<IdentitySeedHostedService> logger) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        try
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            var seeder = scope.ServiceProvider.GetRequiredService<IdentitySeeder>();
            await seeder.SeedAsync(cancellationToken);
        }
        catch (Exception exception) when (exception is InvalidOperationException or
                                           Microsoft.EntityFrameworkCore.DbUpdateException)
        {
            logger.LogCritical(
                "Identity initialization failed ({FailureType}); application startup was stopped.",
                exception.GetType().Name);
            throw;
        }
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }
}
