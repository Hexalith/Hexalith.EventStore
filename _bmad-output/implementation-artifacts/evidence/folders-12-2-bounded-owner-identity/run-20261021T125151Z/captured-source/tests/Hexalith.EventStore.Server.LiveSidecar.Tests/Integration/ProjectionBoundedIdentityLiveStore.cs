using System.Text.Json;
using Dapr.Client;
using Hexalith.EventStore.Client.Projections;
using Microsoft.Extensions.Logging.Abstractions;

namespace Hexalith.EventStore.Server.LiveSidecar.Tests.Integration;

/// <summary>Runs the unchanged production Dapr protocol with byte observation and a process-exit fault seam.</summary>
internal sealed class ProjectionBoundedIdentityLiveStore(DaprClient client, string? crashPrefix = null) :
    IReadModelStore, IReadModelBatchStore, IReadModelBatchStagingStore, IReadModelConditionalEraser, IReadModelPendingBatchRecoveryStore
{
    private readonly DaprReadModelStore _store = new(client);
    internal ProjectionBoundedIdentityLiveMeasurements Measurements { get; } = new();
    internal Action? BeforeProcessExit { get; set; }
    private ReadModelBatchProtocol Protocol(string storeName) => new(new ProjectionBoundedIdentityLiveAccessor(
        new DaprReadModelBatchStateAccessor(client, storeName), Measurements), new ReadModelBatchOptions(), NullLogger.Instance);
    public Task<ReadModelEntry<T>> GetAsync<T>(string storeName, string key, CancellationToken cancellationToken = default) where T : class
        => _store.GetAsync<T>(storeName, key, cancellationToken);
    public Task SaveAsync<T>(string storeName, string key, T value, CancellationToken cancellationToken = default) where T : class
    {
        Measurements.ObserveValue(key, JsonSerializer.SerializeToUtf8Bytes(value, JsonSerializerOptions.Web).Length);
        return _store.SaveAsync(storeName, key, value, cancellationToken);
    }
    public Task<bool> TrySaveAsync<T>(string storeName, string key, T value, string etag, CancellationToken cancellationToken = default) where T : class
    {
        Measurements.ObserveValue(key, JsonSerializer.SerializeToUtf8Bytes(value, JsonSerializerOptions.Web).Length);
        return _store.TrySaveAsync(storeName, key, value, etag, cancellationToken);
    }
    public Task<ReadModelBatchResult> ExecuteAsync(ReadModelBatch batch, CancellationToken cancellationToken = default)
    {
        Measurements.ObserveBatch(batch);
        IReadModelBatchFaultInjector? injector = crashPrefix is not null && batch.Scope.BatchId.StartsWith(crashPrefix, StringComparison.Ordinal)
            ? new ProjectionBoundedIdentityProcessExitInjector(() => BeforeProcessExit?.Invoke()) : null;
        return Protocol(batch.Scope.StoreName).ExecuteAsync(batch, injector, cancellationToken);
    }
    public Task<ReadModelBatchStagingResult> StageAsync(ReadModelBatch batch, CancellationToken cancellationToken = default)
    { Measurements.ObserveBatch(batch); return Protocol(batch.Scope.StoreName).StageAsync(batch, null, cancellationToken); }
    public Task<ReadModelBatchStagingResult> CommitAsync(ReadModelBatch batch, CancellationToken cancellationToken = default)
    { Measurements.ObserveBatch(batch); return Protocol(batch.Scope.StoreName).CommitStagedAsync(batch, null, cancellationToken); }
    public Task<ReadModelBatchStagingResult> AbortAsync(ReadModelBatch batch, CancellationToken cancellationToken = default)
    { Measurements.ObserveBatch(batch); return Protocol(batch.Scope.StoreName).AbortStagedAsync(batch, null, cancellationToken); }
    public Task<ReadModelBatchStagingResult> VerifyAsync(ReadModelBatch batch, CancellationToken cancellationToken = default)
    { Measurements.ObserveBatch(batch); return Protocol(batch.Scope.StoreName).VerifyStagedAsync(batch, null, cancellationToken); }
    public Task<bool> TryEraseAsync(string storeName, string key, string etag, CancellationToken cancellationToken = default)
        => _store.TryEraseAsync(storeName, key, etag, cancellationToken);
    public Task<(bool Present, string Etag)> TryReadEtagAsync(string storeName, string key, CancellationToken cancellationToken = default)
        => _store.TryReadEtagAsync(storeName, key, cancellationToken);
    public Task<ReadModelPendingBatchRecoveryResult> RecoverPendingAsync(ReadModelBatchScope scope, string key, CancellationToken cancellationToken = default)
        => ReadModelPendingBatchRecovery.RecoverAsync(new ProjectionBoundedIdentityLiveAccessor(
            new DaprReadModelBatchStateAccessor(client, scope.StoreName), Measurements), new ReadModelBatchOptions(),
            NullLogger.Instance, scope, key, null, cancellationToken);
}
