namespace Hexalith.EventStore.Server.LiveSidecar.Tests.DomainServices;

/// <summary>
/// Test command used by the round-trip aggregate to prove state rehydration succeeded.
/// </summary>
internal sealed record TestDecrementCounter;
