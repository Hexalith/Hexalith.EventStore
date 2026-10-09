namespace Hexalith.EventStore.Client.Queries;
/// <summary>Retains one actor-owned logical row and its originating type/operation witness.</summary>
/// <param name = "Key">The exact logical key.</param>
/// <param name = "ValueTypeName">The originating logical-operation value type.</param>
/// <param name = "Payload">The original payload, absent for an explicitly witnessed absent row.</param>
/// <param name = "ExpiresAt">The authoritative UTC visibility expiry, when present.</param>
/// <param name = "OriginOperationId">The retained originating operation identity.</param>
internal sealed record DaprLogicalQueryRow(string Key, string ValueTypeName, byte[]? Payload, DateTimeOffset? ExpiresAt, string OriginOperationId);
