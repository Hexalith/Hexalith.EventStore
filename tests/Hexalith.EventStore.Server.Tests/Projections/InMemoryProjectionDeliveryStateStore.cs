using Hexalith.EventStore.Contracts.Identity;
using Hexalith.EventStore.Server.Projections;

namespace Hexalith.EventStore.Server.Tests.Projections;

internal sealed class InMemoryProjectionDeliveryStateStore(ProjectionDeliveryState? initialState = null)
    : IProjectionDeliveryStateStore {
    private readonly object _gate = new();
    private readonly List<ProjectionDeliveryReconciliationWork> _reconciliationWork = [];
    private int _revision = initialState is null ? 0 : 1;
    private ProjectionDeliveryState? _state = initialState;

    public int SaveFailuresRemaining { get; set; }

    public IReadOnlyList<ProjectionDeliveryReconciliationWork> ReconciliationWork {
        get {
            lock (_gate) {
                return [.. _reconciliationWork];
            }
        }
    }

    public ProjectionDeliveryState? State {
        get {
            lock (_gate) {
                return _state;
            }
        }
    }

    public Task<ProjectionDeliveryStateReadResult> ReadAsync(
        AggregateIdentity identity,
        string projectionName,
        CancellationToken cancellationToken = default) {
        cancellationToken.ThrowIfCancellationRequested();
        lock (_gate) {
            return Task.FromResult(new ProjectionDeliveryStateReadResult(
                _state,
                _revision == 0 ? string.Empty : _revision.ToString(System.Globalization.CultureInfo.InvariantCulture),
                ProjectionDeliveryStateClassifier.Classify(_state, protocolV2Active: true),
                true));
        }
    }

    public Task<bool> TrySaveAsync(
        AggregateIdentity identity,
        string projectionName,
        ProjectionDeliveryState state,
        string etag,
        CancellationToken cancellationToken = default) {
        cancellationToken.ThrowIfCancellationRequested();
        lock (_gate) {
            if (SaveFailuresRemaining > 0) {
                SaveFailuresRemaining--;
                return Task.FromResult(false);
            }

            string currentEtag = _revision == 0
                ? string.Empty
                : _revision.ToString(System.Globalization.CultureInfo.InvariantCulture);
            if (!string.Equals(etag, currentEtag, StringComparison.Ordinal)) {
                return Task.FromResult(false);
            }

            _state = state;
            _revision++;
            return Task.FromResult(true);
        }
    }

    public Task<bool> TrySaveWithReconciliationAsync(
        AggregateIdentity identity,
        string projectionName,
        ProjectionDeliveryState state,
        ProjectionDeliveryReconciliationWork work,
        string etag,
        CancellationToken cancellationToken = default) {
        cancellationToken.ThrowIfCancellationRequested();
        lock (_gate) {
            if (SaveFailuresRemaining > 0) {
                SaveFailuresRemaining--;
                return Task.FromResult(false);
            }

            string currentEtag = _revision == 0
                ? string.Empty
                : _revision.ToString(System.Globalization.CultureInfo.InvariantCulture);
            if (!string.Equals(etag, currentEtag, StringComparison.Ordinal)) {
                return Task.FromResult(false);
            }

            _state = state;
            _reconciliationWork.Add(work);
            _revision++;
            return Task.FromResult(true);
        }
    }

    public Task<ProjectionDeliveryWriterProtocol?> ReadWriterProtocolAsync(
        CancellationToken cancellationToken = default) =>
        Task.FromResult<ProjectionDeliveryWriterProtocol?>(null);

    public Task<bool> TryActivateWriterProtocolAsync(
        ProjectionDeliveryWriterProtocol marker,
        CancellationToken cancellationToken = default) => Task.FromResult(true);

    public Task RecordReconciliationAsync(
        AggregateIdentity identity,
        string projectionName,
        ProjectionDeliveryReconciliationWork work,
        CancellationToken cancellationToken = default) {
        cancellationToken.ThrowIfCancellationRequested();
        lock (_gate) {
            _reconciliationWork.Add(work);
        }

        return Task.CompletedTask;
    }
}
