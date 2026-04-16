namespace Hexalith.EventStore.Admin.Abstractions.Models.TypeCatalog;

/// <summary>
/// Information about a registered command type.
/// </summary>
/// <param name="TypeName">The fully qualified command type name.</param>
/// <param name="Domain">The domain that owns this command type.</param>
/// <param name="TargetAggregateType">The aggregate type this command targets.</param>
public record CommandTypeInfo(string TypeName, string Domain, string TargetAggregateType) {
    /// <summary>Gets the fully qualified command type name.</summary>
    public string TypeName { get; } = !string.IsNullOrWhiteSpace(TypeName)
        ? TypeName
        : throw new ArgumentException("TypeName cannot be null, empty, or whitespace.", nameof(TypeName));

    /// <summary>Gets the domain that owns this command type.</summary>
    public string Domain { get; } = !string.IsNullOrWhiteSpace(Domain)
        ? Domain
        : throw new ArgumentException("Domain cannot be null, empty, or whitespace.", nameof(Domain));

    /// <summary>Gets the aggregate type this command targets.</summary>
    public string TargetAggregateType { get; } = !string.IsNullOrWhiteSpace(TargetAggregateType)
        ? TargetAggregateType
        : throw new ArgumentException("TargetAggregateType cannot be null, empty, or whitespace.", nameof(TargetAggregateType));
}
