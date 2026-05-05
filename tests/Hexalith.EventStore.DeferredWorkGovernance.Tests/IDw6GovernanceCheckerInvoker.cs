namespace Hexalith.EventStore.DeferredWorkGovernance.Tests;

internal interface IDw6GovernanceCheckerInvoker {
    Task<Dw6GovernanceReport> CheckAsync(
        IEnumerable<string> arguments,
        CancellationToken cancellationToken = default);
}
