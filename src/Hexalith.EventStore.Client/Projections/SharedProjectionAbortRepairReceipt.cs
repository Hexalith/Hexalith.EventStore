namespace Hexalith.EventStore.Client.Projections;

/// <summary>Retains immutable repair identity and original evidence across completion and restart.</summary>
/// <param name="Fingerprint">The immutable request digest.</param>
/// <param name="Request">The certified original observations.</param>
public sealed record SharedProjectionAbortRepairReceipt(string Fingerprint, SharedProjectionAbortRepairRequest Request);
