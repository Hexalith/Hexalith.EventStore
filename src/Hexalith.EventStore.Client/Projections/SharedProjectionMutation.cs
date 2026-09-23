namespace Hexalith.EventStore.Client.Projections;

/// <summary>A durable, immutable logical mutation prepared for one catch-up delivery.</summary>
public sealed record SharedProjectionMutation(
    string Key,
    ReadModelBatchOperationKind Kind,
    string ValueTypeName,
    byte[] CanonicalValue,
    TimeSpan? TimeToLive = null)
{
    /// <summary>Captures a batch operation before it is assigned a physical generation.</summary>
    public static SharedProjectionMutation FromOperation(ReadModelBatchOperation operation)
    {
        ArgumentNullException.ThrowIfNull(operation);
        return new SharedProjectionMutation(
            operation.Key,
            operation.Kind,
            operation.ValueTypeName,
            operation.CanonicalValue.ToArray(),
            operation.TimeToLive);
    }

    internal ReadModelBatchOperation ToOperation(string physicalKey) => Kind switch
    {
        ReadModelBatchOperationKind.Write => ReadModelBatchOperation.WriteCanonical(
            physicalKey,
            ValueTypeName,
            CanonicalValue,
            TimeToLive),
        ReadModelBatchOperationKind.Delete => ReadModelBatchOperation.Delete(
            physicalKey,
            ReadModelBatchConcurrency.IdempotentAbsent),
        _ => throw new InvalidOperationException("The journal contains an unknown mutation kind."),
    };
}
