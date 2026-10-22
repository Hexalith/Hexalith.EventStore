namespace Hexalith.EventStore.RestApi.Generators;

internal readonly struct RestApiQueryBindingDescriptor : IEquatable<RestApiQueryBindingDescriptor>
{
    public RestApiQueryBindingDescriptor(
        string aggregateSource,
        string aggregateValue,
        string entitySource,
        string? entityValue)
    {
        AggregateSource = aggregateSource;
        AggregateValue = aggregateValue;
        EntitySource = entitySource;
        EntityValue = entityValue;
    }

    public string AggregateSource { get; }

    public string AggregateValue { get; }

    public string EntitySource { get; }

    public string? EntityValue { get; }

    public bool Equals(RestApiQueryBindingDescriptor other)
        => string.Equals(AggregateSource, other.AggregateSource, StringComparison.Ordinal)
            && string.Equals(AggregateValue, other.AggregateValue, StringComparison.Ordinal)
            && string.Equals(EntitySource, other.EntitySource, StringComparison.Ordinal)
            && string.Equals(EntityValue, other.EntityValue, StringComparison.Ordinal);

    public override bool Equals(object? obj) => obj is RestApiQueryBindingDescriptor other && Equals(other);

    public override int GetHashCode()
    {
        unchecked
        {
            int hash = (StringComparer.Ordinal.GetHashCode(AggregateSource) * 397)
                ^ StringComparer.Ordinal.GetHashCode(AggregateValue);
            hash = (hash * 397) ^ StringComparer.Ordinal.GetHashCode(EntitySource);
            hash = (hash * 397) ^ (EntityValue is null ? 0 : StringComparer.Ordinal.GetHashCode(EntityValue));
            return hash;
        }
    }
}
