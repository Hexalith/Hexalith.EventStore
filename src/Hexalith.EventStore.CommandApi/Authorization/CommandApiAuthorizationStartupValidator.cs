namespace Hexalith.EventStore.CommandApi.Authorization;

/// <summary>
/// Validates authorization service registrations during application startup.
/// This forces unsupported actor-based authorization configuration to fail fast
/// instead of surfacing on the first request.
/// </summary>
internal sealed class CommandApiAuthorizationStartupValidator(IServiceScopeFactory scopeFactory) : IHostedService {
    public Task StartAsync(CancellationToken cancellationToken) {
        using IServiceScope scope = scopeFactory.CreateScope();
        _ = scope.ServiceProvider.GetRequiredService<ITenantValidator>();
        _ = scope.ServiceProvider.GetRequiredService<IRbacValidator>();
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
