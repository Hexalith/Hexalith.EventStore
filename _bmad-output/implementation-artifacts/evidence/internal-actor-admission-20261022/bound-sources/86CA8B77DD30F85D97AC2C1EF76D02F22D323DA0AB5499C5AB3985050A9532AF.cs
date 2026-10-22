namespace Hexalith.EventStore.Client.Projections;

/// <summary>Identifies an immutable physical value that a promoted sparse root no longer references.</summary>
public sealed record SharedProjectionRetiredValue(long Generation, string LogicalKey)
{
    internal const string MarkerPrefix = "__cow-retired:";

    /// <summary>Stages one token-independent numeric retirement marker for post-promotion cleanup.</summary>
    public static ReadModelBatchOperation Stage(long ordinal, long generation, string logicalKey)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(ordinal);
        ArgumentOutOfRangeException.ThrowIfNegative(generation);
        ArgumentException.ThrowIfNullOrWhiteSpace(logicalKey);
        return ReadModelBatchOperation.Write(MarkerPrefix
            + ordinal.ToString(System.Globalization.CultureInfo.InvariantCulture),
            new SharedProjectionRetiredValue(generation, logicalKey),
            ReadModelBatchConcurrency.LastWrite);
    }
}
