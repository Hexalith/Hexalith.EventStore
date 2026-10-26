namespace Hexalith.EventStore.Contracts.Routing;

/// <summary>A required field in a canonical descriptor schema; contains no field value.</summary>
/// <param name="Name">Stable field name.</param>
/// <param name="Type">Exactly string, int64, boolean, or string-list.</param>
public sealed record IdempotencyDescriptorField(string Name, string Type);
