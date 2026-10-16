namespace Hexalith.EventStore.DomainService;

/// <summary>Explicit SDK host binding around an unchanged full-replay handler; no store is inferred from a handler invocation.</summary>
/// <param name="Domain">The exact domain.</param>
/// <param name="ProjectionType">The exact full-replay route.</param>
/// <param name="StoreName">The already configured owning batch/manifest component.</param>
public sealed record DomainProjectionReplayRecoveryRegistration(string Domain, string ProjectionType, string StoreName);
