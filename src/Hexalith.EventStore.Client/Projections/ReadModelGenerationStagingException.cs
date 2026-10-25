namespace Hexalith.EventStore.Client.Projections;

/// <summary>Preserves the structured SDK outcome when a generation group cannot complete.</summary>
/// <param name="result">The durable staging outcome.</param>
public sealed class ReadModelGenerationStagingException(ReadModelBatchResult result)
    : InvalidOperationException("read-model-generation-staging-" + result.Status)
{
    /// <summary>Gets the exact outcome for protocol-level retry classification.</summary>
    public ReadModelBatchResult Result { get; } = result;
}
