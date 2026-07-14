using System.Buffers.Binary;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net;
using System.Net.Http.Headers;
using System.Runtime.ExceptionServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

using Hexalith.EventStore.Contracts.Identity;
using Hexalith.EventStore.Contracts.Security;
using Hexalith.EventStore.Server.Actors;
using Hexalith.EventStore.Server.Events;

namespace Hexalith.EventStore.Testing.Integration.Benchmarking;

/// <summary>
/// Creates production-readable benchmark event streams through Dapr's official actor-state API.
/// </summary>
/// <remarks>
/// Use fresh identities in disposable benchmark storage. Dapr 1.18.1 requires an active target actor
/// for actor-state transactions, so the helper activates each actor through its production metadata
/// reader, performs external writes while no actor method is in flight, and explicitly deactivates it
/// before read-back. Dataset construction is test infrastructure; it does not execute domain commands,
/// publish events, or establish performance-requirement evidence.
/// </remarks>
public sealed class BenchmarkDatasetBuilder : IDisposable {
    private const int EmptyTransactionBytes = 2;
    private static readonly JsonSerializerOptions _actorStateJsonOptions = new();

    private readonly string _aggregateActorTypeName;
    private readonly IBenchmarkActorLifecycle _actorLifecycle;
    private readonly Uri _daprHttpEndpoint;
    private readonly bool _disposeHttpClient;
    private readonly IGlobalPositionAllocator _globalPositionAllocator;
    private readonly HttpClient _httpClient;
    private readonly BenchmarkDatasetBuilderOptions _options;
    private bool _disposed;

    /// <summary>
    /// Initializes a builder with an internally owned HTTP client.
    /// </summary>
    /// <param name="daprHttpEndpoint">The Dapr sidecar HTTP endpoint.</param>
    /// <param name="globalPositionAllocator">The production global-position range allocator.</param>
    /// <param name="aggregateActorTypeName">The registered aggregate actor type name.</param>
    /// <param name="actorLifecycle">The production aggregate actor lifecycle.</param>
    /// <param name="options">Optional transaction bounds.</param>
    public BenchmarkDatasetBuilder(
        Uri daprHttpEndpoint,
        IGlobalPositionAllocator globalPositionAllocator,
        string aggregateActorTypeName,
        IBenchmarkActorLifecycle actorLifecycle,
        BenchmarkDatasetBuilderOptions? options = null)
        : this(
            new HttpClient(),
            daprHttpEndpoint,
            globalPositionAllocator,
            aggregateActorTypeName,
            actorLifecycle,
            options,
            disposeHttpClient: true) {
    }

    /// <summary>
    /// Initializes a builder with a caller-owned HTTP client.
    /// </summary>
    /// <param name="httpClient">The HTTP client used to call the Dapr sidecar.</param>
    /// <param name="daprHttpEndpoint">The Dapr sidecar HTTP endpoint.</param>
    /// <param name="globalPositionAllocator">The production global-position range allocator.</param>
    /// <param name="aggregateActorTypeName">The registered aggregate actor type name.</param>
    /// <param name="actorLifecycle">The production aggregate actor lifecycle.</param>
    /// <param name="options">Optional transaction bounds.</param>
    public BenchmarkDatasetBuilder(
        HttpClient httpClient,
        Uri daprHttpEndpoint,
        IGlobalPositionAllocator globalPositionAllocator,
        string aggregateActorTypeName,
        IBenchmarkActorLifecycle actorLifecycle,
        BenchmarkDatasetBuilderOptions? options = null)
        : this(
            httpClient,
            daprHttpEndpoint,
            globalPositionAllocator,
            aggregateActorTypeName,
            actorLifecycle,
            options,
            disposeHttpClient: false) {
    }

    private BenchmarkDatasetBuilder(
        HttpClient httpClient,
        Uri daprHttpEndpoint,
        IGlobalPositionAllocator globalPositionAllocator,
        string aggregateActorTypeName,
        IBenchmarkActorLifecycle actorLifecycle,
        BenchmarkDatasetBuilderOptions? options,
        bool disposeHttpClient) {
        ArgumentNullException.ThrowIfNull(httpClient);
        ArgumentNullException.ThrowIfNull(daprHttpEndpoint);
        ArgumentNullException.ThrowIfNull(globalPositionAllocator);
        ArgumentNullException.ThrowIfNull(actorLifecycle);
        ArgumentException.ThrowIfNullOrWhiteSpace(aggregateActorTypeName);
        if (!daprHttpEndpoint.IsAbsoluteUri) {
            throw new ArgumentException("The Dapr HTTP endpoint must be absolute.", nameof(daprHttpEndpoint));
        }

        _options = options ?? new BenchmarkDatasetBuilderOptions();
        ValidateOptions(_options);
        _httpClient = httpClient;
        _daprHttpEndpoint = new Uri(string.Concat(daprHttpEndpoint.AbsoluteUri.TrimEnd('/'), "/"), UriKind.Absolute);
        _globalPositionAllocator = globalPositionAllocator;
        _aggregateActorTypeName = aggregateActorTypeName;
        _actorLifecycle = actorLifecycle;
        _disposeHttpClient = disposeHttpClient;
    }

    /// <summary>
    /// Validates, writes, and reads back a benchmark dataset.
    /// </summary>
    /// <param name="definition">The complete deterministic dataset definition.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A receipt only after every persisted invariant passes read-back validation.</returns>
    public async Task<BenchmarkDatasetReceipt> SeedAsync(
        BenchmarkDatasetDefinition definition,
        CancellationToken cancellationToken = default) {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(definition);

        var totalStopwatch = Stopwatch.StartNew();
        var validationStopwatch = Stopwatch.StartNew();
        IReadOnlyList<BenchmarkAggregateDefinition> aggregates = ValidateAndOrderDefinition(definition);
        int totalEventCount = aggregates.Sum(static aggregate => aggregate.Events.Count);
        string fingerprint = ComputeFingerprint(definition.DatasetId, aggregates);

        await ExecuteForEachAggregateAsync(
            aggregates,
            async (aggregate, token) => {
                AggregateMetadata? existing = await GetActorStateAsync<AggregateMetadata>(
                    aggregate.Identity,
                    aggregate.Identity.MetadataKey,
                    token).ConfigureAwait(false);
                if (existing is not null) {
                    throw new InvalidOperationException(
                        $"Benchmark dataset cannot overwrite existing aggregate stream '{aggregate.Identity.ActorId}'.");
                }
            },
            cancellationToken).ConfigureAwait(false);
        await ActivateFreshAggregatesAsync(aggregates, cancellationToken).ConfigureAwait(false);
        validationStopwatch.Stop();

        var allocationStopwatch = new Stopwatch();
        var writeStopwatch = new Stopwatch();
        long firstGlobalPosition = 0;
        long lastGlobalPosition = 0;
        var positionedAggregates = new List<(BenchmarkAggregateDefinition Aggregate, int EventOffset)>(aggregates.Count);
        Exception? seedException = null;
        try {
            allocationStopwatch.Start();
            firstGlobalPosition = await _globalPositionAllocator
                .AllocateAsync(totalEventCount, cancellationToken)
                .ConfigureAwait(false);
            if (firstGlobalPosition <= 0) {
                throw new InvalidOperationException("The global-position allocator returned a non-positive range start.");
            }

            lastGlobalPosition = checked(firstGlobalPosition + totalEventCount - 1L);
            allocationStopwatch.Stop();

            int eventOffset = 0;
            foreach (BenchmarkAggregateDefinition aggregate in aggregates) {
                positionedAggregates.Add((aggregate, eventOffset));
                eventOffset = checked(eventOffset + aggregate.Events.Count);
            }

            writeStopwatch.Start();
            await ExecuteForEachAsync(
                positionedAggregates,
                (positioned, token) => WriteAggregateAsync(
                    positioned.Aggregate,
                    checked(firstGlobalPosition + positioned.EventOffset),
                    token),
                cancellationToken).ConfigureAwait(false);
            writeStopwatch.Stop();
        }
        catch (Exception ex) {
            seedException = ex;
            throw;
        }
        finally {
            try {
                await DeactivateAggregatesAsync(aggregates, CancellationToken.None).ConfigureAwait(false);
            }
            catch (Exception deactivationException) when (seedException is not null) {
                RecordSecondaryDeactivationFailure(seedException, deactivationException);
            }
        }

        var readBackStopwatch = Stopwatch.StartNew();
        await ExecuteForEachAsync(
            positionedAggregates,
            (positioned, token) => ValidatePersistedAggregateAsync(
                positioned.Aggregate,
                checked(firstGlobalPosition + positioned.EventOffset),
                token),
            cancellationToken).ConfigureAwait(false);
        readBackStopwatch.Stop();
        totalStopwatch.Stop();

        BenchmarkAggregateReceipt[] aggregateReceipts = [
            .. aggregates.Select(static aggregate => new BenchmarkAggregateReceipt(
                aggregate.Identity,
                aggregate.Events.Count,
                aggregate.Snapshot is not null)),
        ];

        return new BenchmarkDatasetReceipt(
            definition.DatasetId,
            fingerprint,
            aggregates.Count,
            totalEventCount,
            aggregates.Count(static aggregate => aggregate.Snapshot is not null),
            firstGlobalPosition,
            lastGlobalPosition,
            validationStopwatch.Elapsed,
            allocationStopwatch.Elapsed,
            writeStopwatch.Elapsed,
            readBackStopwatch.Elapsed,
            totalStopwatch.Elapsed,
            aggregateReceipts);
    }

    /// <summary>
    /// Deletes every state key described by a dataset definition using bounded actor transactions.
    /// </summary>
    /// <param name="definition">The definition whose state keys should be removed.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <remarks>The global-position range is intentionally not rolled back.</remarks>
    public async Task CleanupAsync(
        BenchmarkDatasetDefinition definition,
        CancellationToken cancellationToken = default) {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(definition);
        IReadOnlyList<BenchmarkAggregateDefinition> aggregates = ValidateAndOrderDefinition(definition);
        await ExecuteForEachAggregateAsync(
            aggregates,
            (aggregate, token) => CleanupWithActorLifecycleAsync(
                aggregate.Identity,
                aggregate.Events.Count,
                aggregate.Snapshot is not null,
                token),
            cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Deletes every state key recorded by a successful dataset receipt using bounded actor transactions.
    /// </summary>
    /// <param name="receipt">The successful seed receipt.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <remarks>The global-position range is intentionally not rolled back.</remarks>
    public async Task CleanupAsync(
        BenchmarkDatasetReceipt receipt,
        CancellationToken cancellationToken = default) {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(receipt);
        await ExecuteForEachAsync(
            receipt.Aggregates,
            (aggregate, token) => CleanupWithActorLifecycleAsync(
                aggregate.Identity,
                aggregate.EventCount,
                aggregate.HasSnapshot,
                token),
            cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public void Dispose() {
        if (_disposed) {
            return;
        }

        if (_disposeHttpClient) {
            _httpClient.Dispose();
        }

        _disposed = true;
    }

    private static EventEnvelope CreateStorageEvent(
        BenchmarkAggregateDefinition aggregate,
        BenchmarkEventDefinition source,
        long sequenceNumber,
        long globalPosition) {
        IDictionary<string, string>? extensions = source.Extensions is null
            ? null
            : new Dictionary<string, string>(source.Extensions, StringComparer.Ordinal);
        extensions = EventStorePayloadProtectionMetadataCarrier.Write(extensions, source.ProtectionMetadata);

        return new EventEnvelope(
            source.MessageId,
            aggregate.Identity.AggregateId,
            aggregate.AggregateType,
            aggregate.Identity.TenantId,
            aggregate.Identity.Domain,
            sequenceNumber,
            globalPosition,
            source.Timestamp,
            source.CorrelationId,
            source.CausationId,
            source.UserId,
            source.DomainServiceVersion,
            source.EventTypeName,
            source.MetadataVersion,
            source.SerializationFormat,
            source.Payload,
            extensions);
    }

    private static SnapshotRecord CreateStorageSnapshot(
        BenchmarkAggregateDefinition aggregate,
        BenchmarkSnapshotDefinition source) =>
        new(
            source.SequenceNumber,
            source.State,
            source.CreatedAt,
            aggregate.Identity.Domain,
            aggregate.Identity.AggregateId,
            aggregate.Identity.TenantId,
            source.ProtectionMetadata);

    private static byte[] CreateDeleteOperation(string key) =>
        JsonSerializer.SerializeToUtf8Bytes(new {
            operation = "delete",
            request = new { key },
        });

    private static byte[] CreateUpsertOperation(string key, object value) {
        JsonElement state = JsonSerializer.SerializeToElement(value, value.GetType(), _actorStateJsonOptions);
        return JsonSerializer.SerializeToUtf8Bytes(new {
            operation = "upsert",
            request = new { key, value = state },
        });
    }

    private static byte[] CreateTransactionBody(IReadOnlyList<byte[]> operations, int transactionBytes) {
        var payload = new byte[transactionBytes];
        int offset = 0;
        payload[offset++] = (byte)'[';
        for (int i = 0; i < operations.Count; i++) {
            if (i > 0) {
                payload[offset++] = (byte)',';
            }

            byte[] operation = operations[i];
            operation.CopyTo(payload, offset);
            offset += operation.Length;
        }

        payload[offset] = (byte)']';
        return payload;
    }

    private static string ComputeFingerprint(
        string datasetId,
        IReadOnlyList<BenchmarkAggregateDefinition> aggregates) {
        using IncrementalHash hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        AppendFingerprintValue(hash, datasetId);
        foreach (BenchmarkAggregateDefinition aggregate in aggregates) {
            AppendFingerprintValue(hash, aggregate.Identity.ActorId);
            AppendFingerprintValue(hash, aggregate.AggregateType);
            foreach (BenchmarkEventDefinition item in aggregate.Events) {
                AppendFingerprintValue(hash, item.MessageId);
                AppendFingerprintValue(hash, item.Timestamp.ToString("O", System.Globalization.CultureInfo.InvariantCulture));
                AppendFingerprintValue(hash, item.CorrelationId);
                AppendFingerprintValue(hash, item.CausationId);
                AppendFingerprintValue(hash, item.UserId);
                AppendFingerprintValue(hash, item.DomainServiceVersion);
                AppendFingerprintValue(hash, item.EventTypeName);
                AppendFingerprintValue(hash, item.MetadataVersion.ToString(System.Globalization.CultureInfo.InvariantCulture));
                AppendFingerprintValue(hash, item.SerializationFormat);
                AppendFingerprintBytes(hash, item.Payload);
                AppendProtectionFingerprint(hash, item.ProtectionMetadata);
                if (item.Extensions is not null) {
                    foreach (KeyValuePair<string, string> extension in item.Extensions.OrderBy(static pair => pair.Key, StringComparer.Ordinal)) {
                        AppendFingerprintValue(hash, extension.Key);
                        AppendFingerprintValue(hash, extension.Value);
                    }
                }
            }

            if (aggregate.Snapshot is not null) {
                AppendFingerprintValue(hash, aggregate.Snapshot.SequenceNumber.ToString(System.Globalization.CultureInfo.InvariantCulture));
                AppendFingerprintValue(hash, aggregate.Snapshot.State.GetRawText());
                AppendFingerprintValue(hash, aggregate.Snapshot.CreatedAt.ToString("O", System.Globalization.CultureInfo.InvariantCulture));
                AppendProtectionFingerprint(hash, aggregate.Snapshot.ProtectionMetadata);
            }
        }

        return Convert.ToHexString(hash.GetHashAndReset()).ToLowerInvariant();
    }

    private static BenchmarkAggregateDefinition MaterializeAggregate(BenchmarkAggregateDefinition aggregate) {
        BenchmarkEventDefinition[] events = [
            .. aggregate.Events.Select(static item => item with {
                Payload = [.. item.Payload],
                ProtectionMetadata = MaterializeProtectionMetadata(item.ProtectionMetadata),
                Extensions = item.Extensions is null
                    ? null
                    : new Dictionary<string, string>(item.Extensions, StringComparer.Ordinal),
            }),
        ];
        BenchmarkSnapshotDefinition? snapshot = aggregate.Snapshot is null
            ? null
            : aggregate.Snapshot with {
                State = aggregate.Snapshot.State.Clone(),
                ProtectionMetadata = MaterializeProtectionMetadata(aggregate.Snapshot.ProtectionMetadata),
            };
        return aggregate with { Events = events, Snapshot = snapshot };
    }

    private static EventStorePayloadProtectionMetadata MaterializeProtectionMetadata(
        EventStorePayloadProtectionMetadata metadata) =>
        metadata with {
            CompatibilityFlags = metadata.CompatibilityFlags is null
                ? null
                : new Dictionary<string, string>(metadata.CompatibilityFlags, StringComparer.Ordinal),
        };

    private static void AppendFingerprintBytes(IncrementalHash hash, ReadOnlySpan<byte> value) {
        Span<byte> length = stackalloc byte[sizeof(int)];
        BinaryPrimitives.WriteInt32LittleEndian(length, value.Length);
        hash.AppendData(length);
        hash.AppendData(value);
    }

    private static void AppendFingerprintValue(IncrementalHash hash, string value) =>
        AppendFingerprintBytes(hash, Encoding.UTF8.GetBytes(value));

    private static void AppendProtectionFingerprint(
        IncrementalHash hash,
        EventStorePayloadProtectionMetadata metadata) {
        AppendFingerprintValue(hash, ((int)metadata.State).ToString(System.Globalization.CultureInfo.InvariantCulture));
        AppendFingerprintValue(hash, metadata.MetadataVersion.ToString(System.Globalization.CultureInfo.InvariantCulture));
        AppendFingerprintOptionalValue(hash, metadata.Scheme);
        AppendFingerprintOptionalValue(hash, metadata.KeyAlias);
        AppendFingerprintOptionalValue(hash, metadata.ContentHint);
        if (metadata.CompatibilityFlags is null) {
            AppendFingerprintValue(hash, "flags:null");
            return;
        }

        AppendFingerprintValue(
            hash,
            metadata.CompatibilityFlags.Count.ToString(System.Globalization.CultureInfo.InvariantCulture));
        foreach (KeyValuePair<string, string> flag in metadata.CompatibilityFlags.OrderBy(
            static pair => pair.Key,
            StringComparer.Ordinal)) {
            AppendFingerprintValue(hash, flag.Key);
            AppendFingerprintValue(hash, flag.Value);
        }
    }

    private static void AppendFingerprintOptionalValue(IncrementalHash hash, string? value) =>
        AppendFingerprintValue(hash, value is null ? "null" : string.Concat("value:", value));

    private static void ValidateEventDefinition(
        BenchmarkAggregateDefinition aggregate,
        BenchmarkEventDefinition item,
        int sequenceNumber,
        BenchmarkDatasetBuilderOptions options) {
        ArgumentNullException.ThrowIfNull(item);
        ArgumentException.ThrowIfNullOrWhiteSpace(item.MessageId);
        ArgumentException.ThrowIfNullOrWhiteSpace(item.CorrelationId);
        ArgumentException.ThrowIfNullOrWhiteSpace(item.CausationId);
        ArgumentException.ThrowIfNullOrWhiteSpace(item.UserId);
        ArgumentException.ThrowIfNullOrWhiteSpace(item.DomainServiceVersion);
        ArgumentException.ThrowIfNullOrWhiteSpace(item.EventTypeName);
        ArgumentException.ThrowIfNullOrWhiteSpace(item.SerializationFormat);
        ArgumentNullException.ThrowIfNull(item.Payload);
        ArgumentNullException.ThrowIfNull(item.ProtectionMetadata);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(item.MetadataVersion);
        if (!EventStorePayloadProtectionMetadataCarrier.TryValidate(item.ProtectionMetadata, out string? reason)) {
            throw new ArgumentException(
                $"Benchmark event protection metadata is invalid for aggregate '{aggregate.Identity.ActorId}', sequence {sequenceNumber}: {reason}",
                nameof(item));
        }

        if (item.Extensions is not null) {
            if (item.Extensions.ContainsKey(EventStorePayloadProtectionMetadataCarrier.ExtensionKey)) {
                throw new ArgumentException(
                    $"Benchmark event extensions for aggregate '{aggregate.Identity.ActorId}', sequence {sequenceNumber} must not supply the reserved protection key.",
                    nameof(item));
            }

            foreach (KeyValuePair<string, string> extension in item.Extensions) {
                ArgumentException.ThrowIfNullOrWhiteSpace(extension.Key);
                ArgumentNullException.ThrowIfNull(extension.Value);
            }
        }

        EventEnvelope worstCase = CreateStorageEvent(aggregate, item, sequenceNumber, long.MaxValue);
        EnsureOperationFits(CreateUpsertOperation($"{aggregate.Identity.EventStreamKeyPrefix}{sequenceNumber}", worstCase), options);
    }

    private static void ValidateOptions(BenchmarkDatasetBuilderOptions options) {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(options.MaxOperationsPerTransaction);
        ArgumentOutOfRangeException.ThrowIfLessThan(options.MaxTransactionBytes, 256);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(options.MaxConcurrentActors);
    }

    private static void ValidateSnapshotDefinition(
        BenchmarkAggregateDefinition aggregate,
        BenchmarkSnapshotDefinition snapshot,
        BenchmarkDatasetBuilderOptions options) {
        if (snapshot.SequenceNumber <= 0 || snapshot.SequenceNumber > aggregate.Events.Count) {
            throw new ArgumentOutOfRangeException(
                nameof(snapshot),
                $"Benchmark snapshot sequence for aggregate '{aggregate.Identity.ActorId}' must be within its event range.");
        }

        if (snapshot.State.ValueKind is JsonValueKind.Undefined or JsonValueKind.Null) {
            throw new ArgumentException(
                $"Benchmark snapshot state is missing for aggregate '{aggregate.Identity.ActorId}'.",
                nameof(snapshot));
        }

        ArgumentNullException.ThrowIfNull(snapshot.ProtectionMetadata);
        if (!EventStorePayloadProtectionMetadataCarrier.TryValidate(snapshot.ProtectionMetadata, out string? reason)) {
            throw new ArgumentException(
                $"Benchmark snapshot protection metadata is invalid for aggregate '{aggregate.Identity.ActorId}': {reason}",
                nameof(snapshot));
        }

        EnsureOperationFits(CreateUpsertOperation(aggregate.Identity.SnapshotKey, CreateStorageSnapshot(aggregate, snapshot)), options);
    }

    private static IReadOnlyList<BenchmarkAggregateDefinition> ValidateDefinitionShape(
        BenchmarkDatasetDefinition definition) {
        ArgumentException.ThrowIfNullOrWhiteSpace(definition.DatasetId);
        ArgumentNullException.ThrowIfNull(definition.Aggregates);
        if (definition.Aggregates.Count == 0) {
            throw new ArgumentException("A benchmark dataset must contain at least one aggregate.", nameof(definition));
        }

        var actorIds = new HashSet<string>(StringComparer.Ordinal);
        int totalEventCount = 0;
        foreach (BenchmarkAggregateDefinition aggregate in definition.Aggregates) {
            ArgumentNullException.ThrowIfNull(aggregate);
            ArgumentNullException.ThrowIfNull(aggregate.Identity);
            ArgumentException.ThrowIfNullOrWhiteSpace(aggregate.AggregateType);
            ArgumentNullException.ThrowIfNull(aggregate.Events);
            if (aggregate.Events.Count == 0) {
                throw new ArgumentException(
                    $"Benchmark aggregate '{aggregate.Identity.ActorId}' must contain at least one event.",
                    nameof(definition));
            }

            if (!actorIds.Add(aggregate.Identity.ActorId)) {
                throw new ArgumentException(
                    $"Benchmark dataset contains duplicate aggregate identity '{aggregate.Identity.ActorId}'.",
                    nameof(definition));
            }

            totalEventCount = checked(totalEventCount + aggregate.Events.Count);
        }

        if (totalEventCount <= 0) {
            throw new ArgumentException("A benchmark dataset must contain at least one event.", nameof(definition));
        }

        return [.. definition.Aggregates.OrderBy(static aggregate => aggregate.Identity.ActorId, StringComparer.Ordinal)];
    }

    private static void EnsureOperationFits(byte[] operation, BenchmarkDatasetBuilderOptions options) {
        EnsureOperationsFit([operation], options, "An actor-state operation");
    }

    private static int EnsureOperationsFit(
        IReadOnlyList<byte[]> operations,
        BenchmarkDatasetBuilderOptions options,
        string operationDescription) {
        ArgumentNullException.ThrowIfNull(operations);
        ArgumentException.ThrowIfNullOrWhiteSpace(operationDescription);
        if (operations.Count == 0) {
            throw new ArgumentException("An actor-state transaction must contain at least one operation.", nameof(operations));
        }

        if (operations.Count > options.MaxOperationsPerTransaction) {
            throw new ArgumentException(
                $"{operationDescription} requires {operations.Count} operations in one actor-state transaction, exceeding the configured {options.MaxOperationsPerTransaction}-operation bound.");
        }

        int transactionBytes = EmptyTransactionBytes;
        for (int index = 0; index < operations.Count; index++) {
            transactionBytes = checked(transactionBytes + (index == 0 ? 0 : 1) + operations[index].Length);
        }

        if (transactionBytes > options.MaxTransactionBytes) {
            throw new ArgumentException(
                $"{operationDescription} requires {transactionBytes} bytes in one actor-state transaction, exceeding the configured {options.MaxTransactionBytes}-byte transaction bound.");
        }

        return transactionBytes;
    }

    private async Task<int> AppendOperationAsync(
        AggregateIdentity identity,
        List<byte[]> operations,
        int transactionBytes,
        byte[] operation,
        CancellationToken cancellationToken) {
        EnsureOperationFits(operation, _options);
        int separatorBytes = operations.Count == 0 ? 0 : 1;
        if (operations.Count > 0
            && (operations.Count >= _options.MaxOperationsPerTransaction
                || checked(transactionBytes + separatorBytes + operation.Length) > _options.MaxTransactionBytes)) {
            await SendActorStateTransactionAsync(identity, operations, transactionBytes, cancellationToken).ConfigureAwait(false);
            operations.Clear();
            transactionBytes = EmptyTransactionBytes;
            separatorBytes = 0;
        }

        operations.Add(operation);
        transactionBytes = checked(transactionBytes + separatorBytes + operation.Length);
        if (operations.Count >= _options.MaxOperationsPerTransaction) {
            await SendActorStateTransactionAsync(identity, operations, transactionBytes, cancellationToken).ConfigureAwait(false);
            operations.Clear();
            transactionBytes = EmptyTransactionBytes;
        }

        return transactionBytes;
    }

    private async Task CleanupAggregateAsync(
        AggregateIdentity identity,
        int eventCount,
        bool hasSnapshot,
        CancellationToken cancellationToken) {
        ArgumentNullException.ThrowIfNull(identity);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(eventCount);

        byte[] metadataDelete = CreateDeleteOperation(identity.MetadataKey);
        await SendActorStateTransactionAsync(
            identity,
            [metadataDelete],
            EmptyTransactionBytes + metadataDelete.Length,
            cancellationToken).ConfigureAwait(false);

        var operations = new List<byte[]>(_options.MaxOperationsPerTransaction);
        int transactionBytes = EmptyTransactionBytes;
        for (int sequence = 1; sequence <= eventCount; sequence++) {
            transactionBytes = await AppendOperationAsync(
                identity,
                operations,
                transactionBytes,
                CreateDeleteOperation($"{identity.EventStreamKeyPrefix}{sequence}"),
                cancellationToken).ConfigureAwait(false);
        }

        if (hasSnapshot) {
            transactionBytes = await AppendOperationAsync(
                identity,
                operations,
                transactionBytes,
                CreateDeleteOperation(identity.SnapshotKey),
                cancellationToken).ConfigureAwait(false);
        }

        if (operations.Count > 0) {
            await SendActorStateTransactionAsync(identity, operations, transactionBytes, cancellationToken).ConfigureAwait(false);
        }
    }

    private async Task CleanupWithActorLifecycleAsync(
        AggregateIdentity identity,
        int eventCount,
        bool hasSnapshot,
        CancellationToken cancellationToken) {
        bool activationAttempted = false;
        Exception? cleanupException = null;
        try {
            activationAttempted = true;
            _ = await _actorLifecycle.ActivateAsync(identity, cancellationToken).ConfigureAwait(false);
            await CleanupAggregateAsync(identity, eventCount, hasSnapshot, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) {
            cleanupException = ex;
            throw;
        }
        finally {
            if (activationAttempted) {
                try {
                    await _actorLifecycle.DeactivateAsync(identity, CancellationToken.None).ConfigureAwait(false);
                }
                catch (Exception deactivationException) when (cleanupException is not null) {
                    RecordSecondaryDeactivationFailure(cleanupException, deactivationException);
                }
            }
        }
    }

    private async Task ActivateFreshAggregatesAsync(
        IReadOnlyList<BenchmarkAggregateDefinition> aggregates,
        CancellationToken cancellationToken) {
        var activationAttempts = new ConcurrentBag<AggregateIdentity>();
        try {
            await ExecuteForEachAggregateAsync(
                aggregates,
                async (aggregate, token) => {
                    activationAttempts.Add(aggregate.Identity);
                    AggregateStreamMetadata metadata = await _actorLifecycle
                        .ActivateAsync(aggregate.Identity, token)
                        .ConfigureAwait(false);
                    if (metadata.Exists || metadata.CurrentSequence != 0) {
                        throw new InvalidOperationException(
                            $"Benchmark dataset cannot overwrite production-visible aggregate stream '{aggregate.Identity.ActorId}'.");
                    }
                },
                cancellationToken).ConfigureAwait(false);
        }
        catch (Exception activationException) {
            try {
                await DeactivateIdentitiesAsync([.. activationAttempts], CancellationToken.None).ConfigureAwait(false);
            }
            catch (Exception deactivationException) {
                RecordSecondaryDeactivationFailure(activationException, deactivationException);
            }

            throw;
        }
    }

    private async Task DeactivateAggregatesAsync(
        IReadOnlyList<BenchmarkAggregateDefinition> aggregates,
        CancellationToken cancellationToken) =>
        await DeactivateIdentitiesAsync(
            [.. aggregates.Select(static aggregate => aggregate.Identity)],
            cancellationToken).ConfigureAwait(false);

    private async Task DeactivateIdentitiesAsync(
        IReadOnlyList<AggregateIdentity> identities,
        CancellationToken cancellationToken) {
        var failures = new ConcurrentQueue<Exception>();
        await ExecuteForEachAsync(
            identities,
            async (identity, token) => {
                try {
                    await _actorLifecycle.DeactivateAsync(identity, token).ConfigureAwait(false);
                }
                catch (Exception ex) {
                    failures.Enqueue(ex);
                }
            },
            cancellationToken).ConfigureAwait(false);
        if (failures.Count == 1 && failures.TryDequeue(out Exception? singleFailure)) {
            ExceptionDispatchInfo.Capture(singleFailure).Throw();
        }

        if (!failures.IsEmpty) {
            throw new AggregateException("One or more benchmark aggregate actors could not be deactivated.", failures);
        }
    }

    private static void RecordSecondaryDeactivationFailure(
        Exception primaryException,
        Exception deactivationException) =>
        primaryException.Data["BenchmarkActorDeactivationFailure"] =
            deactivationException.GetType().FullName ?? deactivationException.GetType().Name;

    private async Task ExecuteForEachAggregateAsync(
        IReadOnlyList<BenchmarkAggregateDefinition> aggregates,
        Func<BenchmarkAggregateDefinition, CancellationToken, Task> action,
        CancellationToken cancellationToken) =>
        await ExecuteForEachAsync(aggregates, action, cancellationToken).ConfigureAwait(false);

    private async Task ExecuteForEachAsync<T>(
        IReadOnlyList<T> items,
        Func<T, CancellationToken, Task> action,
        CancellationToken cancellationToken) {
        var parallelOptions = new ParallelOptions {
            CancellationToken = cancellationToken,
            MaxDegreeOfParallelism = _options.MaxConcurrentActors,
        };
        await Parallel.ForEachAsync(
            items,
            parallelOptions,
            async (item, token) => await action(item, token).ConfigureAwait(false)).ConfigureAwait(false);
    }

    private async Task<T?> GetActorStateAsync<T>(
        AggregateIdentity identity,
        string stateKey,
        CancellationToken cancellationToken)
        where T : class {
        Uri uri = BuildActorStateUri(identity, stateKey);
        using HttpRequestMessage request = new(HttpMethod.Get, uri);
        using HttpResponseMessage response = await _httpClient
            .SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken)
            .ConfigureAwait(false);
        if (response.StatusCode == HttpStatusCode.NoContent) {
            return null;
        }

        if (!response.IsSuccessStatusCode) {
            throw new InvalidOperationException(
                $"Dapr actor-state read failed for aggregate '{identity.ActorId}' with HTTP {(int)response.StatusCode}.");
        }

        byte[] content = await response.Content.ReadAsByteArrayAsync(cancellationToken).ConfigureAwait(false);
        try {
            return JsonSerializer.Deserialize<T>(content, _actorStateJsonOptions)
                ?? throw new JsonException("The actor-state response was empty.");
        }
        catch (JsonException ex) {
            throw new InvalidOperationException(
                $"Dapr actor-state read returned an invalid persisted shape for aggregate '{identity.ActorId}', key '{stateKey}'.",
                ex);
        }
    }

    private async Task SendActorStateTransactionAsync(
        AggregateIdentity identity,
        IReadOnlyList<byte[]> operations,
        int transactionBytes,
        CancellationToken cancellationToken) {
        byte[] payload = CreateTransactionBody(operations, transactionBytes);
        using HttpRequestMessage request = new(HttpMethod.Post, BuildActorStateUri(identity));
        request.Content = new ByteArrayContent(payload);
        request.Content.Headers.ContentType = new MediaTypeHeaderValue("application/json");
        using HttpResponseMessage response = await _httpClient
            .SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken)
            .ConfigureAwait(false);
        if (!response.IsSuccessStatusCode) {
            string? daprErrorCode = await ReadDaprErrorCodeAsync(response, cancellationToken).ConfigureAwait(false);
            string errorCodeDescription = daprErrorCode is null ? string.Empty : $", Dapr error code '{daprErrorCode}'";
            throw new InvalidOperationException(
                $"Dapr actor-state transaction failed for aggregate '{identity.ActorId}' with HTTP {(int)response.StatusCode}{errorCodeDescription}; operation count {operations.Count}, request bytes {transactionBytes}.");
        }
    }

    private static async Task<string?> ReadDaprErrorCodeAsync(
        HttpResponseMessage response,
        CancellationToken cancellationToken) {
        try {
            byte[] content = await response.Content.ReadAsByteArrayAsync(cancellationToken).ConfigureAwait(false);
            using JsonDocument document = JsonDocument.Parse(content);
            return document.RootElement.TryGetProperty("errorCode", out JsonElement errorCode)
                ? errorCode.GetString()
                : null;
        }
        catch (JsonException) {
            return null;
        }
    }

    private Uri BuildActorStateUri(AggregateIdentity identity, string? stateKey = null) {
        string relative = $"v1.0/actors/{Uri.EscapeDataString(_aggregateActorTypeName)}/{Uri.EscapeDataString(identity.ActorId)}/state";
        if (stateKey is not null) {
            relative = string.Concat(relative, "/", Uri.EscapeDataString(stateKey));
        }

        return new Uri(_daprHttpEndpoint, relative);
    }

    private async Task ValidatePersistedAggregateAsync(
        BenchmarkAggregateDefinition aggregate,
        long firstGlobalPosition,
        CancellationToken cancellationToken) {
        AggregateMetadata metadata = await GetActorStateAsync<AggregateMetadata>(
            aggregate.Identity,
            aggregate.Identity.MetadataKey,
            cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidOperationException(
                $"Benchmark aggregate metadata is missing after write for '{aggregate.Identity.ActorId}'.");
        if (metadata.CurrentSequence != aggregate.Events.Count
            || metadata.LastModified != aggregate.Events[^1].Timestamp
            || metadata.ETag is not null) {
            throw new InvalidOperationException(
                $"Benchmark aggregate metadata read-back validation failed for '{aggregate.Identity.ActorId}'.");
        }

        await ValidatePersistedEventAsync(aggregate, 1, firstGlobalPosition, cancellationToken).ConfigureAwait(false);
        if (aggregate.Events.Count > 1) {
            await ValidatePersistedEventAsync(
                aggregate,
                aggregate.Events.Count,
                checked(firstGlobalPosition + aggregate.Events.Count - 1L),
                cancellationToken).ConfigureAwait(false);
        }

        if (aggregate.Snapshot is not null) {
            SnapshotRecord snapshot = await GetActorStateAsync<SnapshotRecord>(
                aggregate.Identity,
                aggregate.Identity.SnapshotKey,
                cancellationToken).ConfigureAwait(false)
                ?? throw new InvalidOperationException(
                    $"Benchmark snapshot is missing after write for '{aggregate.Identity.ActorId}'.");
            if (snapshot.SequenceNumber != aggregate.Snapshot.SequenceNumber
                || !string.Equals(snapshot.Domain, aggregate.Identity.Domain, StringComparison.Ordinal)
                || !string.Equals(snapshot.AggregateId, aggregate.Identity.AggregateId, StringComparison.Ordinal)
                || !string.Equals(snapshot.TenantId, aggregate.Identity.TenantId, StringComparison.Ordinal)
                || snapshot.ProtectionMetadata != aggregate.Snapshot.ProtectionMetadata
                || snapshot.State is not JsonElement persistedState
                || !JsonElement.DeepEquals(persistedState, aggregate.Snapshot.State)) {
                throw new InvalidOperationException(
                    $"Benchmark snapshot read-back validation failed for '{aggregate.Identity.ActorId}'.");
            }
        }
    }

    private async Task ValidatePersistedEventAsync(
        BenchmarkAggregateDefinition aggregate,
        int sequenceNumber,
        long expectedGlobalPosition,
        CancellationToken cancellationToken) {
        string stateKey = $"{aggregate.Identity.EventStreamKeyPrefix}{sequenceNumber}";
        EventEnvelope persisted = await GetActorStateAsync<EventEnvelope>(
            aggregate.Identity,
            stateKey,
            cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidOperationException(
                $"Benchmark event {sequenceNumber} is missing after write for '{aggregate.Identity.ActorId}'.");
        BenchmarkEventDefinition expected = aggregate.Events[sequenceNumber - 1];
        if (!string.Equals(persisted.MessageId, expected.MessageId, StringComparison.Ordinal)
            || !string.Equals(persisted.AggregateId, aggregate.Identity.AggregateId, StringComparison.Ordinal)
            || !string.Equals(persisted.AggregateType, aggregate.AggregateType, StringComparison.Ordinal)
            || !string.Equals(persisted.TenantId, aggregate.Identity.TenantId, StringComparison.Ordinal)
            || !string.Equals(persisted.Domain, aggregate.Identity.Domain, StringComparison.Ordinal)
            || persisted.SequenceNumber != sequenceNumber
            || persisted.GlobalPosition != expectedGlobalPosition
            || persisted.Timestamp != expected.Timestamp
            || !string.Equals(persisted.CorrelationId, expected.CorrelationId, StringComparison.Ordinal)
            || !string.Equals(persisted.CausationId, expected.CausationId, StringComparison.Ordinal)
            || !string.Equals(persisted.UserId, expected.UserId, StringComparison.Ordinal)
            || !string.Equals(persisted.DomainServiceVersion, expected.DomainServiceVersion, StringComparison.Ordinal)
            || !string.Equals(persisted.EventTypeName, expected.EventTypeName, StringComparison.Ordinal)
            || persisted.MetadataVersion != expected.MetadataVersion
            || !string.Equals(persisted.SerializationFormat, expected.SerializationFormat, StringComparison.Ordinal)
            || !persisted.Payload.AsSpan().SequenceEqual(expected.Payload)
            || EventStorePayloadProtectionMetadataCarrier.Read(persisted.Extensions) != expected.ProtectionMetadata) {
            throw new InvalidOperationException(
                $"Benchmark event read-back validation failed for '{aggregate.Identity.ActorId}', sequence {sequenceNumber}.");
        }

        IDictionary<string, string>? sourceExtensions = expected.Extensions is null
            ? null
            : new Dictionary<string, string>(expected.Extensions, StringComparer.Ordinal);
        IDictionary<string, string> expectedExtensions = EventStorePayloadProtectionMetadataCarrier.Write(
            sourceExtensions,
            expected.ProtectionMetadata);
        if (persisted.Extensions is null || persisted.Extensions.Count != expectedExtensions.Count) {
            throw new InvalidOperationException(
                $"Benchmark event extension read-back validation failed for '{aggregate.Identity.ActorId}', sequence {sequenceNumber}.");
        }

        foreach (KeyValuePair<string, string> extension in expectedExtensions) {
            if (!persisted.Extensions.TryGetValue(extension.Key, out string? value)
                || !string.Equals(value, extension.Value, StringComparison.Ordinal)) {
                throw new InvalidOperationException(
                    $"Benchmark event extension read-back validation failed for '{aggregate.Identity.ActorId}', sequence {sequenceNumber}.");
            }
        }
    }

    private async Task WriteAggregateAsync(
        BenchmarkAggregateDefinition aggregate,
        long firstGlobalPosition,
        CancellationToken cancellationToken) {
        var operations = new List<byte[]>(_options.MaxOperationsPerTransaction);
        int transactionBytes = EmptyTransactionBytes;
        for (int index = 0; index < aggregate.Events.Count; index++) {
            int sequenceNumber = index + 1;
            EventEnvelope envelope = CreateStorageEvent(
                aggregate,
                aggregate.Events[index],
                sequenceNumber,
                checked(firstGlobalPosition + index));
            transactionBytes = await AppendOperationAsync(
                aggregate.Identity,
                operations,
                transactionBytes,
                CreateUpsertOperation($"{aggregate.Identity.EventStreamKeyPrefix}{sequenceNumber}", envelope),
                cancellationToken).ConfigureAwait(false);
        }

        if (operations.Count > 0) {
            await SendActorStateTransactionAsync(
                aggregate.Identity,
                operations,
                transactionBytes,
                cancellationToken).ConfigureAwait(false);
            operations.Clear();
        }

        BenchmarkEventDefinition lastEvent = aggregate.Events[^1];
        var metadata = new AggregateMetadata(
            aggregate.Events.Count,
            lastEvent.Timestamp,
            ETag: null);
        var finalOperations = new List<byte[]>(2);
        if (aggregate.Snapshot is not null) {
            finalOperations.Add(
                CreateUpsertOperation(
                    aggregate.Identity.SnapshotKey,
                    CreateStorageSnapshot(aggregate, aggregate.Snapshot)));
        }

        finalOperations.Add(CreateUpsertOperation(aggregate.Identity.MetadataKey, metadata));
        int finalTransactionBytes = EnsureOperationsFit(
            finalOperations,
            _options,
            "The benchmark snapshot and aggregate metadata visibility barrier");
        await SendActorStateTransactionAsync(
            aggregate.Identity,
            finalOperations,
            finalTransactionBytes,
            cancellationToken).ConfigureAwait(false);
    }

    private IReadOnlyList<BenchmarkAggregateDefinition> ValidateAndOrderDefinition(
        BenchmarkDatasetDefinition definition) {
        IReadOnlyList<BenchmarkAggregateDefinition> aggregates = ValidateDefinitionShape(definition);
        foreach (BenchmarkAggregateDefinition aggregate in aggregates) {
            for (int index = 0; index < aggregate.Events.Count; index++) {
                ValidateEventDefinition(aggregate, aggregate.Events[index], index + 1, _options);
            }

            BenchmarkEventDefinition lastEvent = aggregate.Events[^1];
            var finalOperations = new List<byte[]>(2);
            if (aggregate.Snapshot is not null) {
                ValidateSnapshotDefinition(aggregate, aggregate.Snapshot, _options);
                finalOperations.Add(
                    CreateUpsertOperation(
                        aggregate.Identity.SnapshotKey,
                        CreateStorageSnapshot(aggregate, aggregate.Snapshot)));
            }

            finalOperations.Add(
                CreateUpsertOperation(
                    aggregate.Identity.MetadataKey,
                    new AggregateMetadata(aggregate.Events.Count, lastEvent.Timestamp, null)));
            EnsureOperationsFit(
                finalOperations,
                _options,
                "The benchmark snapshot and aggregate metadata visibility barrier");
        }

        return [.. aggregates.Select(MaterializeAggregate)];
    }

    private void ThrowIfDisposed() => ObjectDisposedException.ThrowIf(_disposed, this);
}
