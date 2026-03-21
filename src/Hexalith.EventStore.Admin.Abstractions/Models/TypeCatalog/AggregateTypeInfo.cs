namespace Hexalith.EventStore.Admin.Abstractions.Models.TypeCatalog;

/// <summary>
/// Information about a registered aggregate type.
/// </summary>
/// <param name="TypeName">The fully qualified aggregate type name.</param>
/// <param name="Domain">The domain that owns this aggregate type.</param>
/// <param name="EventCount">The number of event types associated with this aggregate.</param>
/// <param name="CommandCount">The number of command types associated with this aggregate.</param>
/// <param name="HasProjections">Whether any projections subscribe to events from this aggregate.</param>
public record AggregateTypeInfo(string TypeName, string Domain, int EventCount, int CommandCount, bool HasProjections)
{
    /// <summary>Gets the fully qualified aggregate type name.</summary>
    public string TypeName { get; } = !string.IsNullOrWhiteSpace(TypeName)
        ? TypeName
        : throw new ArgumentException("TypeName cannot be null, empty, or whitespace.", nameof(TypeName));

    /// <summary>Gets the domain that owns this aggregate type.</summary>
    public string Domain { get; } = !string.IsNullOrWhiteSpace(Domain)
        ? Domain
        : throw new ArgumentException("Domain cannot be null, empty, or whitespace.", nameof(Domain));
}
