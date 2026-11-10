namespace Hexalith.EventStore.Contracts.Rest;

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
/// Binding values must not contain leading or trailing whitespace. When the entity source
/// is <see cref="RestQueryBindingSource.None"/>, the entity value must be null or omitted.
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
    public string AggregateValue { get; } = ValidateValue(aggregateValue);

    /// <summary>Gets the source used for the query entity identifier.</summary>
    public RestQueryBindingSource EntitySource { get; } = ValidateEntitySource(entitySource);

    /// <summary>Gets the literal entity identifier or route parameter name.</summary>
    public string? EntityValue { get; } = ValidateEntityValue(entitySource, entityValue);

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

    private static string? ValidateEntityValue(RestQueryBindingSource source, string? value)
    {
        if (source == RestQueryBindingSource.None)
        {
            if (value is not null)
            {
                throw new ArgumentException("Entity value must be omitted when entity source is None.", nameof(value));
            }

            return null;
        }

        return ValidateValue(value);
    }

    private static string ValidateValue(string? value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        if (!string.Equals(value, value.Trim(), StringComparison.Ordinal))
        {
            throw new ArgumentException("Binding values must not contain leading or trailing whitespace.", nameof(value));
        }

        return value;
    }
}
