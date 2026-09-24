namespace Hexalith.EventStore.Client.Projections;

/// <summary>Atomic proof that one tenant scope was included in a global control index.</summary>
internal sealed record SharedProjectionControlIndexReceipt(string TenantId, string Digest);
