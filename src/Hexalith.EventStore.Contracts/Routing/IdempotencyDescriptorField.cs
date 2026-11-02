using System.Text.Json.Serialization;

namespace Hexalith.EventStore.Contracts.Routing;

/// <summary>A required field in a canonical descriptor schema; contains no field value.</summary>
/// <param name="Name">Stable field name within its source.</param>
/// <param name="Type">Exactly string, int64, boolean, or string-list.</param>
public sealed record IdempotencyDescriptorField(string Name, string Type)
{
    /// <summary>Describes the existing canonical value without changing its encoding.</summary>
    /// <param name="Name">Stable field name within its source.</param>
    /// <param name="Type">Exactly string, int64, boolean, or string-list.</param>
    /// <param name="Nullable">Whether the existing canonical field permits an explicit null value.</param>
    /// <param name="Source">Null for the legacy payload schema, or payload, target, semantic-option, delegated-task-scope or credential-scope.</param>
    [JsonConstructor]
    public IdempotencyDescriptorField(string Name, string Type, bool Nullable = false, string? Source = null) : this(Name, Type)
    {
        this.Nullable = Nullable;
        this.Source = Source;
    }

    /// <summary>Gets whether the existing field permits an explicit null value.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public bool Nullable { get; init; }

    /// <summary>Gets the exact canonical input location, or null for legacy payload schemas.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Source { get; init; }
}
