using System.Security.Cryptography;
using Hexalith.EventStore.Client.Aggregates;
using Hexalith.EventStore.Client.Events;

namespace Hexalith.EventStore.Server.Events;
/// <summary>Retains a supplied pure projection declaration; this local pin is not the authoritative handler catalog.</summary>
internal sealed class DaprLogicalProjectionFold : IDisposable
{
    private EventBufferReservation? _charge;
    private byte[]? _backend;
    private byte[]? _namespace;
    private byte[]? _fingerprint;
    private readonly string _tenant;
    private readonly string _domain;
    private readonly string _aggregate;
    private readonly string _type;
    /// <summary>Pins the exact reconstructed state binding and bounded local projection/backend configuration.</summary>
    internal DaprLogicalProjectionFold(DaprLogicalSourceBinding source, string route, string actorId, string store, ReadOnlySpan<byte> backend, RegisteredLogicalReplayBinding reconstruction, EventBufferBudget budget)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(reconstruction);
        ArgumentNullException.ThrowIfNull(budget);
        _ = DaprLogicalCheckpointCodec.TextSize(route);
        _ = DaprLogicalCheckpointCodec.TextSize(actorId);
        _ = DaprLogicalCheckpointCodec.TextSize(store);
        if (backend.Length != 32)
        {
            throw new ArgumentException("CheckpointHold: exact declared backend pin required.", nameof(backend));
        }

        _charge = budget.Reserve(8192);
        try
        {
            Route = route;
            ActorId = actorId;
            Store = store;
            Reconstruction = reconstruction;
            Budget = budget;
            _tenant = source.Identity.TenantId;
            _domain = source.Identity.Domain;
            _aggregate = source.Identity.AggregateId;
            _type = source.AggregateType;
            _backend = backend.ToArray();
            _namespace = DaprLogicalCheckpointCodec.Namespace(source, route, actorId, store, _backend);
            _fingerprint = DaprLogicalCheckpointCodec.Fold(_namespace, reconstruction.Fingerprint.Span);
        }
        catch
        {
            Dispose();
            throw;
        }
    }

    /// <summary>Gets the declared exact single projection route.</summary>
    internal string Route { get; }
    /// <summary>Gets the actual target actor identifier.</summary>
    internal string ActorId { get; }
    /// <summary>Gets the declared local store name.</summary>
    internal string Store { get; }
    /// <summary>Gets the exact pure fold/codec declaration whose completed state is required.</summary>
    internal RegisteredLogicalReplayBinding Reconstruction { get; }
    /// <summary>Gets the retained shared parent.</summary>
    internal EventBufferBudget Budget { get; }
    /// <summary>Gets a borrowed namespace hash under this declaration's retained lifetime.</summary>
    internal ReadOnlyMemory<byte> NamespaceHash => _namespace ?? throw new ObjectDisposedException(nameof(DaprLogicalProjectionFold));
    /// <summary>Gets a borrowed exact declaration hash.</summary>
    internal ReadOnlyMemory<byte> Fingerprint => _fingerprint ?? throw new ObjectDisposedException(nameof(DaprLogicalProjectionFold));
    /// <summary>Gets a declared configuration pin, without claiming provider authentication.</summary>
    internal ReadOnlyMemory<byte> Backend => _backend ?? throw new ObjectDisposedException(nameof(DaprLogicalProjectionFold));

    /// <summary>Recomputes local immutable pins and exact loaded callable/options identity before later work.</summary>
    internal void RequireCurrent(DaprLogicalSourceBinding source, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        ObjectDisposedException.ThrowIf(_charge is null, this);
        Reconstruction.RequireSource(source, token);
        if (source.Identity.TenantId != _tenant || source.Identity.Domain != _domain || source.Identity.AggregateId != _aggregate || source.AggregateType != _type)
        {
            throw new InvalidOperationException("CheckpointHold: projection source namespace changed.");
        }

        byte[] scope = DaprLogicalCheckpointCodec.Namespace(source, Route, ActorId, Store, _backend);
        byte[] fold = DaprLogicalCheckpointCodec.Fold(scope, Reconstruction.Fingerprint.Span);
        try
        {
            if (!scope.AsSpan().SequenceEqual(_namespace) || !fold.AsSpan().SequenceEqual(_fingerprint))
            {
                throw new InvalidOperationException("CheckpointHold: private projection declaration changed.");
            }
        }
        finally
        {
            CryptographicOperations.ZeroMemory(scope);
            CryptographicOperations.ZeroMemory(fold);
            token.ThrowIfCancellationRequested();
        }
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        foreach (byte[]? hash in new[]
        {
            Interlocked.Exchange(ref _backend, null),
            Interlocked.Exchange(ref _namespace, null),
            Interlocked.Exchange(ref _fingerprint, null)
        }

        )
        {
            if (hash is not null)
            {
                CryptographicOperations.ZeroMemory(hash);
            }
        }

        Interlocked.Exchange(ref _charge, null)?.Dispose();
    }
}
