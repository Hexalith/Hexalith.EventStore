using Hexalith.EventStore.Contracts.Security;

namespace Hexalith.EventStore.PayloadProtection.Tests;

/// <summary>
/// Stands in for the separately owned <c>parties-pdenc-v1</c> reader without reimplementing v1 cryptography.
/// </summary>
/// <param name="readerId">The advertised reader identifier.</param>
internal sealed class FakeLegacyPayloadReader(string readerId = PayloadCompatibilityRouter.PartiesV1ReaderId) : ILegacyPayloadReader
{
    private int _eventCalls;
    private int _snapshotCalls;

    /// <summary>Gets or sets the event outcome; the default returns fresh plaintext JSON.</summary>
    internal Func<CompatibilityEventRecord, CancellationToken, CoreUnprotectionResult>? OnEvent { get; set; }

    /// <summary>Gets or sets the snapshot outcome; the default returns fresh plaintext snapshot JSON.</summary>
    internal Func<CompatibilitySnapshotRecord, CancellationToken, CoreUnprotectionResult>? OnSnapshot { get; set; }

    /// <summary>Gets the number of event reads.</summary>
    internal int EventCalls => Volatile.Read(ref _eventCalls);

    /// <summary>Gets the number of snapshot reads.</summary>
    internal int SnapshotCalls => Volatile.Read(ref _snapshotCalls);

    /// <summary>Gets the metadata supplied to the latest read.</summary>
    internal EventStorePayloadProtectionMetadata? LastMetadata { get; private set; }

    /// <inheritdoc/>
    public string ReaderId { get; } = readerId;

    /// <inheritdoc/>
    public ValueTask<CoreUnprotectionResult> ReadEventAsync(
        CompatibilityEventRecord record,
        EventStorePayloadProtectionMetadata metadata,
        CancellationToken cancellationToken)
    {
        _ = Interlocked.Increment(ref _eventCalls);
        LastMetadata = metadata;
        cancellationToken.ThrowIfCancellationRequested();
        return ValueTask.FromResult(OnEvent is null
            ? CoreUnprotectionResult.Readable(CompatibilityTestData.PlainJson())
            : OnEvent(record, cancellationToken));
    }

    /// <inheritdoc/>
    public ValueTask<CoreUnprotectionResult> ReadSnapshotAsync(
        CompatibilitySnapshotRecord record,
        EventStorePayloadProtectionMetadata metadata,
        CancellationToken cancellationToken)
    {
        _ = Interlocked.Increment(ref _snapshotCalls);
        LastMetadata = metadata;
        cancellationToken.ThrowIfCancellationRequested();
        return ValueTask.FromResult(OnSnapshot is null
            ? CoreUnprotectionResult.Readable(CompatibilityTestData.SnapshotPlaintext())
            : OnSnapshot(record, cancellationToken));
    }
}
