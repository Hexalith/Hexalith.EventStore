using Hexalith.EventStore.Client.Events;

using Microsoft.Extensions.Hosting;

namespace Hexalith.EventStore.Client.Registration;

/// <summary>Forces event evolution validation before a host accepts work.</summary>
internal sealed class EventPayloadEvolutionStartupValidation(EventPayloadEvolutionRegistry registry) : IHostedService
{
    /// <inheritdoc/>
    public Task StartAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        _ = registry;
        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
