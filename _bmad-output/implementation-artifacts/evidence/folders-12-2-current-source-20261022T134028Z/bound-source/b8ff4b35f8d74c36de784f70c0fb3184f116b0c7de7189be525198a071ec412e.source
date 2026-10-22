using Hexalith.EventStore.Client.Handlers;
using Hexalith.EventStore.Contracts.Commands;
using Hexalith.EventStore.Contracts.Results;

namespace Hexalith.EventStore.Sample.Tests.Counter;

/// <summary>Returns a supplied result to exercise the sample's real bounded router intake.</summary>
/// <param name="result">The result emitted through the production request router.</param>
/// <param name="afterProcessing">An optional cancellation control at the domain boundary.</param>
internal sealed class BoundedCounterResultProcessor(DomainResult result, Action? afterProcessing = null) : IAsyncDomainProcessor
{
    /// <summary>Gets the exact token delivered to domain processing.</summary>
    internal CancellationToken ObservedToken { get; private set; }

    /// <inheritdoc/>
    public Task<DomainResult> ProcessAsync(CommandEnvelope command, object? currentState, CancellationToken cancellationToken)
    {
        ObservedToken = cancellationToken;
        afterProcessing?.Invoke();
        return Task.FromResult(result);
    }
}
