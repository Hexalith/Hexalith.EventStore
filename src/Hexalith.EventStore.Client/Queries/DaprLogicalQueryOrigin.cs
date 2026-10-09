namespace Hexalith.EventStore.Client.Queries;
/// <summary>Binds actual originating actor operation readback to one exact root row.</summary>
/// <param name = "OperationId">The retained originating operation identity.</param>
/// <param name = "Tenant">The exact authenticated tenant.</param>
/// <param name = "Domain">The canonical owning domain.</param>
/// <param name = "StoreName">The declared state-store identity.</param>
/// <param name = "Key">The exact logical row key.</param>
/// <param name = "ValueTypeName">The original value type, empty for absence/deletion.</param>
/// <param name = "PayloadHash">The exact original payload hash, absent for absence/deletion.</param>
/// <param name = "ExpiresAt">The original visibility expiry, absent for absence/deletion or no TTL.</param>
internal sealed record DaprLogicalQueryOrigin(string OperationId, string Tenant, string Domain, string StoreName, string Key, string ValueTypeName, byte[]? PayloadHash, DateTimeOffset? ExpiresAt);
