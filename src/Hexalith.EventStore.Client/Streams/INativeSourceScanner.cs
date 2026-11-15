using Hexalith.EventStore.Contracts.Identity;

namespace Hexalith.EventStore.Client.Streams;

/// <summary>Folds a bounded-memory native stream between independently verified equal current observations.</summary>
public interface INativeSourceScanner
{
    /// <summary>Creates a fresh selector for every attempt and releases its value only after native prefix verification.</summary>
    Task<NativeSourceScanResult<T>> ScanAsync<T>(AggregateIdentity identity,
        Func<INativeSourceScanFold<T>> createFold, CancellationToken cancellationToken = default);
}
