namespace Hexalith.EventStore.Contracts.Security;

/// <summary>
/// Returns the bounded erasure state and strongly observed lifecycle epoch.
/// Implements Story 8.1 section 9 under normative digest
/// <c>de9ba8866fd98a480629890ee2b89a492fbad96d4d5a927388e6aaa0fdd72b4e</c>.
/// </summary>
/// <param name="State">The observed erasure state.</param>
/// <param name="LifecycleEpoch">The strongly observed unsigned lifecycle epoch.</param>
/// <param name="ReasonCode">The bounded constructive reason for the state.</param>
/// <param name="ObservedAtUtc">The UTC observation time.</param>
public sealed record PayloadErasureStateResult(
    PayloadErasureState State,
    ulong LifecycleEpoch,
    PayloadErasureReasonCode ReasonCode,
    DateTimeOffset ObservedAtUtc);
