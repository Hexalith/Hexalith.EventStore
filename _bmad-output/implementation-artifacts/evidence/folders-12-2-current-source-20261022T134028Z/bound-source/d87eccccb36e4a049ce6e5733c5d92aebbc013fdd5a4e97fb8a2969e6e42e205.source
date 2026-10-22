namespace Hexalith.EventStore.Contracts.Rest;

/// <summary>
/// Identifies how a generated REST query endpoint should populate the EventStore query envelope.
/// </summary>
public enum RestQueryBindingSource
{
    /// <summary>No explicit value is supplied.</summary>
    None,

    /// <summary>Use the literal value supplied by the attribute.</summary>
    Constant,

    /// <summary>Use the value of a route parameter named by the attribute.</summary>
    Route,
}

/// <summary>
/// Overrides the generated EventStore query envelope aggregate and entity identifiers.
/// </summary>
/// <param name="aggregateSource">The source used for the query aggregate identifier.</param>
/// <param name="aggregateValue">The literal aggregate identifier or route parameter name.</param>
/// <param name="entitySource">The source used for the query entity identifier.</param>
/// <param name="entityValue">The literal entity identifier or route parameter name.</param>
/// <remarks>
/// Use this attribute when the public REST route cannot be inferred into the domain query
/// envelope. For example, <c>~/api/users/{userId}/tenants</c> may need aggregate
/// <c>index</c> and entity <c>{userId}</c>.
/// </remarks>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
public sealed class RestQueryBindingAttribute(
    RestQueryBindingSource aggregateSource,
    string aggregateValue,
    RestQueryBindingSource entitySource = RestQueryBindingSource.None,
    string? entityValue = null) : Attribute
{
    /// <summary>Gets the source used for the query aggregate identifier.</summary>
    public RestQueryBindingSource AggregateSource { get; } = ValidateAggregateSource(aggregateSource);

    /// <summary>Gets the literal aggregate identifier or route parameter name.</summary>
    public string AggregateValue { get; } = ValidateValue(aggregateSource, aggregateValue);

    /// <summary>Gets the source used for the query entity identifier.</summary>
    public RestQueryBindingSource EntitySource { get; } = ValidateEntitySource(entitySource);

    /// <summary>Gets the literal entity identifier or route parameter name.</summary>
    public string? EntityValue { get; } = entitySource == RestQueryBindingSource.None
        ? entityValue
        : ValidateValue(entitySource, entityValue);

    private static RestQueryBindingSource ValidateAggregateSource(RestQueryBindingSource source)
    {
        if (source is RestQueryBindingSource.Constant or RestQueryBindingSource.Route)
        {
            return source;
        }

        throw new ArgumentOutOfRangeException(nameof(source), source, "Aggregate binding source must be Constant or Route.");
    }

    private static RestQueryBindingSource ValidateEntitySource(RestQueryBindingSource source)
    {
        if (source is RestQueryBindingSource.None or RestQueryBindingSource.Constant or RestQueryBindingSource.Route)
        {
            return source;
        }

        throw new ArgumentOutOfRangeException(nameof(source), source, "Entity binding source is not supported.");
    }

    private static string ValidateValue(RestQueryBindingSource source, string? value)
    {
        if (source == RestQueryBindingSource.None)
        {
            return value ?? string.Empty;
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        return value;
    }
}
