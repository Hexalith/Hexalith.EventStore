namespace Hexalith.EventStore.PayloadProtection.Tests;

/// <summary>Exercises nullable value objects in properties and collection elements.</summary>
internal sealed class NestedNullablePolicyPayload
{
    /// <summary>Gets or sets an optional sensitive value object.</summary>
    public NestedSensitiveValue? Value { get; set; }

    /// <summary>Gets or sets optional sensitive value objects in a list.</summary>
    public List<NestedSensitiveValue?> Items { get; set; } = [];
}
