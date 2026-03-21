namespace Hexalith.EventStore.Admin.Abstractions.Models.TypeCatalog;

/// <summary>
/// Information about a registered event type.
/// </summary>
/// <param name="TypeName">The fully qualified event type name.</param>
/// <param name="Domain">The domain that owns this event type.</param>
/// <param name="IsRejection">Whether this is a rejection event.</param>
/// <param name="SchemaVersion">The schema version of the event type.</param>
public record EventTypeInfo(string TypeName, string Domain, bool IsRejection, int SchemaVersion)
{
    /// <summary>Gets the fully qualified event type name.</summary>
    public string TypeName { get; } = !string.IsNullOrWhiteSpace(TypeName)
        ? TypeName
        : throw new ArgumentException("TypeName cannot be null, empty, or whitespace.", nameof(TypeName));

    /// <summary>Gets the domain that owns this event type.</summary>
    public string Domain { get; } = !string.IsNullOrWhiteSpace(Domain)
        ? Domain
        : throw new ArgumentException("Domain cannot be null, empty, or whitespace.", nameof(Domain));
}
