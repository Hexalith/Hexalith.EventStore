namespace Hexalith.EventStore.Client.Projections;

/// <summary>Content-free discovery authority retained before any original private manifest.</summary>
/// <param name="Scope">The exact batch scope.</param>
/// <param name="Fingerprint">The original canonical digest.</param>
/// <param name="OperationCount">The bounded original operation count.</param>
internal sealed record ReadModelRecoveryRegistration(ReadModelBatchScope Scope, string Fingerprint, int OperationCount);
