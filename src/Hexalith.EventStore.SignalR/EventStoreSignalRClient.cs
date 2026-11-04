using System.Collections.Concurrent;
using System.Collections.ObjectModel;
using System.Diagnostics;

using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.Logging;

namespace Hexalith.EventStore.SignalR;

/// <summary>
/// Client helper for receiving real-time projection change signals via SignalR.
/// Handles automatic reconnection and group rejoin (FR59).
/// Implements <see cref="IAsyncDisposable"/> — callers MUST dispose when done.
/// </summary>
public sealed partial class EventStoreSignalRClient : IAsyncDisposable {
    private const string CategoryName = "Hexalith.EventStore.SignalR.EventStoreSignalRClient";
    private const int MaxGroupScopeLength = 64;
    private static readonly TimeSpan MembershipAttemptBudget = TimeSpan.FromSeconds(5);
    private long _connectionRecoveryEpoch = -1;
    private int _membershipRecoveryPending;
    private long _rejectedIntentGeneration = -1;
    private HubException? _membershipRejection;

    private long _recoveryGeneration;
    private Task? _disposeTask;
    private IProjectionCallback[] _disposingCallbacks = [];
    private long _intentGeneration;
    private readonly SemaphoreSlim _membershipGate = new(1, 1);
    private int _membershipProtocol;
    private string? _membershipConnectionId;
    private long _connectionEpoch;
    private long _membershipEpoch;
    private readonly Dictionary<string, GroupSubscription> _legacyGroups = new(StringComparer.Ordinal);
    private readonly HashSet<string> _legacyUncertainGroups = new(StringComparer.Ordinal);
    private int _repairRunning;
    private readonly object _repairGate = new();
    private Task _repairTask = Task.CompletedTask;
    private long _repairRequested;
    private readonly Dictionary<string, List<IProjectionCallback>> _retiredCallbacks = new(StringComparer.Ordinal);

    private readonly object _subscriptionGate = new();

    private delegate void ReconnectConfigurator(HubConnectionBuilder builder, IRetryPolicy? retryPolicy);

    private readonly HubConnection _connection;

    private readonly CancellationTokenSource _disposeCts = new();

    private readonly ILogger<EventStoreSignalRClient>? _logger;

    private readonly ConcurrentDictionary<string, GroupSubscription> _subscribedGroups = new();

    private static readonly IReadOnlyDictionary<string, string> EmptyMetadata =
        new ReadOnlyDictionary<string, string>(new Dictionary<string, string>(StringComparer.Ordinal));

    /// <summary>
    /// Initializes a new instance of the <see cref="EventStoreSignalRClient"/> class.
    /// </summary>
    /// <param name="options">The SignalR client options containing the hub URL.</param>
    /// <param name="logger">Optional logger for diagnostic output.</param>
    public EventStoreSignalRClient(EventStoreSignalRClientOptions options, ILogger<EventStoreSignalRClient>? logger = null)
        : this(options, logger, null) {
    }

    private EventStoreSignalRClient(
        EventStoreSignalRClientOptions options,
        ILogger<EventStoreSignalRClient>? logger,
        ReconnectConfigurator? reconnectConfigurator) {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentException.ThrowIfNullOrWhiteSpace(options.HubUrl);
        if (!Uri.TryCreate(options.HubUrl, UriKind.Absolute, out _)) {
            throw new ArgumentException("HubUrl must be an absolute URL.", nameof(options));
        }

        _logger = logger;

        HubConnectionBuilder builder = new();
        _ = builder.WithUrl(options.HubUrl, connectionOptions => {
            if (options.AccessTokenProvider is not null) {
                connectionOptions.AccessTokenProvider = options.AccessTokenProvider;
            }

            options.ConfigureHttpConnection?.Invoke(connectionOptions);
        });
        (reconnectConfigurator ?? ConfigureAutomaticReconnect)(builder, options.RetryPolicy);

        _connection = builder.Build();

        _connection.Reconnected += OnReconnectedAsync;
        _connection.Reconnecting += OnReconnectingAsync;
        _connection.Closed += OnClosedAsync;
        _ = _connection.On<string, string>("ProjectionChanged", OnProjectionChanged);
        _ = _connection.On<string, string, string?, IReadOnlyDictionary<string, string>>(
            "ProjectionChangedDetail",
            OnProjectionChangedDetail);
    }

    /// <summary>Gets a durable generation incremented on connection loss and after rejoin attempts, including transitions between consumer interval ticks.</summary>
    public long RecoveryGeneration => Interlocked.Read(ref _recoveryGeneration);

    /// <summary>Gets whether the underlying SignalR hub connection is currently connected.</summary>
    public bool IsConnected => _connection.State == HubConnectionState.Connected;

    /// <summary>Gets whether this connection negotiated generation-ordered membership acknowledgements.</summary>
    public bool SupportsAcknowledgedMembership => Volatile.Read(ref _membershipProtocol) == 1;

    /// <summary>Revokes immediately. Self-disposal returns; external disposal awaits the owned callback drain and transport shutdown.</summary>
    public ValueTask DisposeAsync() {
        lock (_subscriptionGate) {
            if (_disposeTask is null) {
                _disposingCallbacks = [.. _subscribedGroups.Values.SelectMany(subscription =>
                    subscription.Callbacks.Values.Cast<IProjectionCallback>().Concat(subscription.DetailCallbacks.Values)),
                    .. _retiredCallbacks.Values.SelectMany(callbacks => callbacks)];
                foreach (IProjectionCallback callback in _disposingCallbacks) { _ = callback.RevokeAndDrainAsync(); }
                _subscribedGroups.Clear();
                // Shutdown cannot begin on a synchronous dispatch frame: StopAsync can wait
                // for that frame. Retain one task which every external disposer can await.
                _disposeTask = Task.Run(DisposeCoreAsync);
            }
            return _disposingCallbacks.Any(callback => callback.IsExecuting)
                ? ValueTask.CompletedTask : new ValueTask(_disposeTask);
        }
    }

    private async Task DisposeCoreAsync() {
        List<Exception> failures = [];
        try { await _disposeCts.CancelAsync().ConfigureAwait(false); }
        catch (Exception exception) { failures.Add(exception); }
        try { await Task.WhenAll(_disposingCallbacks.Select(callback => callback.Completion)).ConfigureAwait(false); }
        catch (Exception exception) { failures.Add(exception); }
        Task repair;
        lock (_repairGate) { repair = _repairTask; }
        try { await repair.ConfigureAwait(false); }
        catch (Exception exception) { failures.Add(exception); }
        try {
            if (_connection.State != HubConnectionState.Disconnected) { await _connection.StopAsync().ConfigureAwait(false); }
        }
        catch (Exception exception) { failures.Add(exception); }
        try { await _connection.DisposeAsync().ConfigureAwait(false); }
        catch (Exception exception) { failures.Add(exception); }
        _disposeCts.Dispose();
        if (failures.Count > 0) { throw new AggregateException("Projection client teardown failed.", failures); }
    }

    /// <summary>
    /// Starts the SignalR connection and joins all pre-subscribed groups.
    /// Groups added via <see cref="SubscribeAsync"/> before <see cref="StartAsync"/> are joined on initial connect.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    public async Task StartAsync(CancellationToken cancellationToken = default) {
        ThrowIfDisposing();
        await _connection.StartAsync(cancellationToken).ConfigureAwait(false);
        Interlocked.Increment(ref _connectionEpoch);

        // Join all pre-subscribed groups on initial connect
        await JoinAllGroupsAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Subscribes to projection change signals for the given projection type and tenant.
    /// The callback is invoked when a "changed" signal is received.
    /// </summary>
    /// <param name="projectionType">The projection type name (kebab-case).</param>
    /// <param name="tenantId">The tenant identifier.</param>
    /// <param name="onChanged">Callback invoked on projection change signal.</param>
    public Task SubscribeAsync(string projectionType, string tenantId, Action onChanged)
        => SubscribeAsync(projectionType, tenantId, onChanged, CancellationToken.None);

    /// <summary>Performs the group operation with cancellation propagated to the SignalR invocation. Local removal precedes any leave acknowledgement.</summary>
    /// <param name="projectionType">The projectionType argument.</param>
    /// <param name="tenantId">The tenantId argument.</param>
    /// <param name="onChanged">The onChanged argument.</param>
    /// <param name="cancellationToken">Cancels the underlying RPC, settling its awaiter.</param>
    public async Task SubscribeAsync(string projectionType, string tenantId, Action onChanged, CancellationToken cancellationToken) {
        ThrowIfDisposing();
        using var invocation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _disposeCts.Token);
        CancellationToken token = invocation.Token;
        ValidateGroupPart(projectionType, nameof(projectionType));
        ValidateGroupPart(tenantId, nameof(tenantId));
        ArgumentNullException.ThrowIfNull(onChanged);

        token.ThrowIfCancellationRequested();
        string groupName = $"{projectionType}:{tenantId}";
        GroupSubscription subscription;
        Guid callbackId = Guid.NewGuid();
        lock (_subscriptionGate) {
            ObjectDisposedException.ThrowIf(_disposeTask is not null, this);
            subscription = _subscribedGroups.GetOrAdd(groupName, _ => new GroupSubscription(projectionType, tenantId, null));
            _ = subscription.Callbacks.TryAdd(callbackId, new ProjectionCallback<bool>(onChanged, _ => onChanged()));
            _intentGeneration++;
        }

        try {
            token.ThrowIfCancellationRequested();
            if (_connection.State == HubConnectionState.Connected) {
                await ReconcileMembershipAsync(token).ConfigureAwait(false);
            }
            token.ThrowIfCancellationRequested();
        }
        catch {
            await RollBackSignalCallbackAsync(groupName, subscription, callbackId).ConfigureAwait(false);
            ScheduleMembershipRepair();
            throw;
        }
    }

    /// <summary>
    /// Subscribes to tenant-wide metadata-rich projection change detail signals.
    /// </summary>
    /// <param name="projectionType">The projection type name (kebab-case).</param>
    /// <param name="tenantId">The tenant identifier.</param>
    /// <param name="onChanged">Callback invoked on projection change detail signal.</param>
    public Task SubscribeDetailAsync(
        string projectionType,
        string tenantId,
        Action<ProjectionChangedDetail> onChanged)
        => SubscribeDetailAsync(projectionType, tenantId, groupScope: null, onChanged);

    /// <summary>Subscribes to tenant-wide detail signals with cancellation of the join invocation.</summary>
    /// <param name="projectionType">The projection type.</param>
    /// <param name="tenantId">The authorized tenant.</param>
    /// <param name="onChanged">The detail callback.</param>
    /// <param name="cancellationToken">The join invocation lifetime.</param>
    public Task SubscribeDetailAsync(string projectionType, string tenantId, Action<ProjectionChangedDetail> onChanged, CancellationToken cancellationToken)
        => SubscribeDetailAsync(projectionType, tenantId, null, onChanged, cancellationToken);

    /// <summary>
    /// Subscribes to metadata-rich projection change detail signals for an optional scoped group.
    /// </summary>
    /// <param name="projectionType">The projection type name (kebab-case).</param>
    /// <param name="tenantId">The tenant identifier.</param>
    /// <param name="groupScope">Optional sub-tenant group scope. Null/empty/whitespace targets the tenant-wide group.</param>
    /// <param name="onChanged">Callback invoked on projection change detail signal.</param>
    public Task SubscribeDetailAsync(
        string projectionType,
        string tenantId,
        string? groupScope,
        Action<ProjectionChangedDetail> onChanged)
        => SubscribeDetailAsync(projectionType, tenantId, groupScope, onChanged, CancellationToken.None);

    /// <summary>Performs the group operation with cancellation propagated to the SignalR invocation. Local removal precedes any leave acknowledgement.</summary>
    /// <param name="projectionType">The projectionType argument.</param>
    /// <param name="tenantId">The tenantId argument.</param>
    /// <param name="groupScope">The groupScope argument.</param>
    /// <param name="onChanged">The onChanged argument.</param>
    /// <param name="cancellationToken">Cancels the underlying RPC, settling its awaiter.</param>
    public async Task SubscribeDetailAsync(
        string projectionType,
        string tenantId,
        string? groupScope,
        Action<ProjectionChangedDetail> onChanged, CancellationToken cancellationToken) {
        ThrowIfDisposing();
        using var invocation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _disposeCts.Token);
        CancellationToken token = invocation.Token;
        ValidateGroupPart(projectionType, nameof(projectionType));
        ValidateGroupPart(tenantId, nameof(tenantId));
        string? normalizedScope = NormalizeScope(groupScope, nameof(groupScope));
        ArgumentNullException.ThrowIfNull(onChanged);

        token.ThrowIfCancellationRequested();
        string groupName = BuildGroupName(projectionType, tenantId, normalizedScope);
        GroupSubscription subscription;
        Guid callbackId = Guid.NewGuid();
        lock (_subscriptionGate) {
            ObjectDisposedException.ThrowIf(_disposeTask is not null, this);
            subscription = _subscribedGroups.GetOrAdd(groupName, _ => new GroupSubscription(projectionType, tenantId, normalizedScope));
            _ = subscription.DetailCallbacks.TryAdd(callbackId, new ProjectionCallback<ProjectionChangedDetail>(onChanged, onChanged));
            _intentGeneration++;
        }

        try {
            token.ThrowIfCancellationRequested();
            if (_connection.State == HubConnectionState.Connected) {
                await ReconcileMembershipAsync(token).ConfigureAwait(false);
            }
            token.ThrowIfCancellationRequested();
        }
        catch {
            await RollBackDetailCallbackAsync(groupName, subscription, callbackId).ConfigureAwait(false);
            ScheduleMembershipRepair();
            throw;
        }
    }

    /// <summary>
    /// Unsubscribes a specific callback from projection change signals for the given projection type and tenant.
    /// Leaves the SignalR group only when no callbacks remain for the group.
    /// </summary>
    /// <param name="projectionType">The projection type name (kebab-case).</param>
    /// <param name="tenantId">The tenant identifier.</param>
    /// <param name="onChanged">The callback previously registered via <see cref="SubscribeAsync"/>.</param>
    public Task UnsubscribeAsync(string projectionType, string tenantId, Action onChanged)
        => UnsubscribeAsync(projectionType, tenantId, onChanged, CancellationToken.None);

    /// <summary>Performs the group operation with cancellation propagated to the SignalR invocation. Local removal precedes any leave acknowledgement.</summary>
    /// <param name="projectionType">The projectionType argument.</param>
    /// <param name="tenantId">The tenantId argument.</param>
    /// <param name="onChanged">The onChanged argument.</param>
    /// <param name="cancellationToken">Cancels the underlying RPC, settling its awaiter.</param>
    public async Task UnsubscribeAsync(string projectionType, string tenantId, Action onChanged, CancellationToken cancellationToken) {
        ArgumentNullException.ThrowIfNull(onChanged);
        await RemoveCallbacksAsync(projectionType, tenantId, null, onChanged, false, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Unsubscribes from projection change signals for the given projection type and tenant.
    /// </summary>
    /// <param name="projectionType">The projection type name (kebab-case).</param>
    /// <param name="tenantId">The tenant identifier.</param>
    public Task UnsubscribeAsync(string projectionType, string tenantId)
        => UnsubscribeAsync(projectionType, tenantId, CancellationToken.None);

    /// <summary>Performs the group operation with cancellation propagated to the SignalR invocation. Local removal precedes any leave acknowledgement.</summary>
    /// <param name="projectionType">The projectionType argument.</param>
    /// <param name="tenantId">The tenantId argument.</param>
    /// <param name="cancellationToken">Cancels the underlying RPC, settling its awaiter.</param>
    public async Task UnsubscribeAsync(string projectionType, string tenantId, CancellationToken cancellationToken) {
        await RemoveCallbacksAsync(projectionType, tenantId, null, null, false, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Unsubscribes a specific callback from tenant-wide metadata-rich projection change detail signals.
    /// </summary>
    /// <param name="projectionType">The projection type name (kebab-case).</param>
    /// <param name="tenantId">The tenant identifier.</param>
    /// <param name="onChanged">The callback previously registered via <see cref="SubscribeDetailAsync(string, string, Action{ProjectionChangedDetail})"/>.</param>
    public Task UnsubscribeDetailAsync(
        string projectionType,
        string tenantId,
        Action<ProjectionChangedDetail> onChanged)
        => UnsubscribeDetailAsync(projectionType, tenantId, groupScope: null, onChanged);

    /// <summary>
    /// Unsubscribes a specific callback from metadata-rich projection change detail signals.
    /// </summary>
    /// <param name="projectionType">The projection type name (kebab-case).</param>
    /// <param name="tenantId">The tenant identifier.</param>
    /// <param name="groupScope">Optional sub-tenant group scope. Null/empty/whitespace targets the tenant-wide group.</param>
    /// <param name="onChanged">The callback previously registered via <see cref="SubscribeDetailAsync(string, string, string?, Action{ProjectionChangedDetail})"/>.</param>
    public Task UnsubscribeDetailAsync(
        string projectionType,
        string tenantId,
        string? groupScope,
        Action<ProjectionChangedDetail> onChanged)
        => UnsubscribeDetailAsync(projectionType, tenantId, groupScope, onChanged, CancellationToken.None);

    /// <summary>Performs the group operation with cancellation propagated to the SignalR invocation. Local removal precedes any leave acknowledgement.</summary>
    /// <param name="projectionType">The projectionType argument.</param>
    /// <param name="tenantId">The tenantId argument.</param>
    /// <param name="groupScope">The groupScope argument.</param>
    /// <param name="onChanged">The onChanged argument.</param>
    /// <param name="cancellationToken">Cancels the underlying RPC, settling its awaiter.</param>
    public async Task UnsubscribeDetailAsync(
        string projectionType,
        string tenantId,
        string? groupScope,
        Action<ProjectionChangedDetail> onChanged, CancellationToken cancellationToken) {
        ArgumentNullException.ThrowIfNull(onChanged);
        await RemoveCallbacksAsync(projectionType, tenantId, groupScope, onChanged, true, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Unsubscribes from tenant-wide metadata-rich projection change detail signals.
    /// </summary>
    /// <param name="projectionType">The projection type name (kebab-case).</param>
    /// <param name="tenantId">The tenant identifier.</param>
    public Task UnsubscribeDetailAsync(string projectionType, string tenantId)
        => UnsubscribeDetailAsync(projectionType, tenantId, groupScope: null);

    /// <summary>
    /// Unsubscribes from metadata-rich projection change detail signals for an optional scoped group.
    /// </summary>
    /// <param name="projectionType">The projection type name (kebab-case).</param>
    /// <param name="tenantId">The tenant identifier.</param>
    /// <param name="groupScope">Optional sub-tenant group scope. Null/empty/whitespace targets the tenant-wide group.</param>
    public Task UnsubscribeDetailAsync(string projectionType, string tenantId, string? groupScope)
        => UnsubscribeDetailAsync(projectionType, tenantId, groupScope, CancellationToken.None);

    /// <summary>Performs the group operation with cancellation propagated to the SignalR invocation. Local removal precedes any leave acknowledgement.</summary>
    /// <param name="projectionType">The projectionType argument.</param>
    /// <param name="tenantId">The tenantId argument.</param>
    /// <param name="groupScope">The groupScope argument.</param>
    /// <param name="cancellationToken">Cancels the underlying RPC, settling its awaiter.</param>
    public async Task UnsubscribeDetailAsync(string projectionType, string tenantId, string? groupScope, CancellationToken cancellationToken) {
        await RemoveCallbacksAsync(projectionType, tenantId, groupScope, null, true, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Removes a tenant-wide detail callback with cancellation of the leave invocation.</summary>
    /// <param name="projectionType">The projection type.</param>
    /// <param name="tenantId">The authorized tenant.</param>
    /// <param name="onChanged">The detail callback.</param>
    /// <param name="cancellationToken">The leave invocation lifetime.</param>
    public Task UnsubscribeDetailAsync(string projectionType, string tenantId, Action<ProjectionChangedDetail> onChanged, CancellationToken cancellationToken)
        => UnsubscribeDetailAsync(projectionType, tenantId, null, onChanged, cancellationToken);

    /// <summary>Removes tenant-wide detail callbacks with cancellation of the leave invocation.</summary>
    /// <param name="projectionType">The projection type.</param>
    /// <param name="tenantId">The authorized tenant.</param>
    /// <param name="cancellationToken">The leave invocation lifetime.</param>
    public Task UnsubscribeDetailAsync(string projectionType, string tenantId, CancellationToken cancellationToken)
        => UnsubscribeDetailAsync(projectionType, tenantId, groupScope: null, cancellationToken);

    private void ThrowIfDisposing() {
        lock (_subscriptionGate) { ObjectDisposedException.ThrowIf(_disposeTask is not null, this); }
    }

    private async Task RemoveCallbacksAsync(string projectionType, string tenantId, string? scope, Delegate? original,
        bool detail, CancellationToken cancellationToken) {
        using var invocation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _disposeCts.Token);
        ValidateGroupPart(projectionType, nameof(projectionType));
        ValidateGroupPart(tenantId, nameof(tenantId));
        string? normalizedScope = NormalizeScope(scope, nameof(scope));
        string groupName = BuildGroupName(projectionType, tenantId, normalizedScope);
        Task[] drains;
        bool reentrant;
        lock (_subscriptionGate) {
            ObjectDisposedException.ThrowIf(_disposeTask is not null, this);
            if (_subscribedGroups.TryGetValue(groupName, out GroupSubscription? subscription)) {
                if (detail) {
                    foreach ((Guid id, ProjectionCallback<ProjectionChangedDetail> callback) in subscription.DetailCallbacks.ToArray()) {
                        if (original is null || callback.Original == original) {
                            _ = subscription.DetailCallbacks.TryRemove(id, out _);
                            RetireCallback(groupName, callback);
                        }
                    }
                }
                else {
                    foreach ((Guid id, ProjectionCallback<bool> callback) in subscription.Callbacks.ToArray()) {
                        if (original is null || callback.Original == original) {
                            _ = subscription.Callbacks.TryRemove(id, out _);
                            RetireCallback(groupName, callback);
                        }
                    }
                }
                RemoveSubscriptionIfEmpty(groupName, subscription);
                _intentGeneration++;
            }
            if (!_retiredCallbacks.TryGetValue(groupName, out List<IProjectionCallback>? retired)) { return; }
            reentrant = IsExecutingCallback();
            drains = [.. retired.Select(callback => callback.Completion)];
        }
        // Group-wide self-removal cannot await any active frame: two callbacks could
        // otherwise each wait for the other's return. External removers retain all drains.
        if (reentrant) { ScheduleMembershipRepair(); return; }
        await Task.WhenAll(drains).ConfigureAwait(false);
        try { await ReconcileMembershipAsync(invocation.Token).ConfigureAwait(false); }
        catch { ScheduleMembershipRepair(); throw; }
    }

    private bool IsExecutingCallback() => _subscribedGroups.Values.Any(subscription =>
        subscription.Callbacks.Values.Any(callback => callback.IsExecuting)
        || subscription.DetailCallbacks.Values.Any(callback => callback.IsExecuting))
        || _retiredCallbacks.Values.Any(callbacks => callbacks.Any(callback => callback.IsExecuting));

    private void RetireCallback(string groupName, IProjectionCallback callback) {
        foreach (string key in _retiredCallbacks.Keys.ToArray()) {
            _retiredCallbacks[key].RemoveAll(item => item.Completion.IsCompleted);
            if (_retiredCallbacks[key].Count == 0) { _retiredCallbacks.Remove(key); }
        }
        _ = callback.RevokeAndDrainAsync();
        if (!_retiredCallbacks.TryGetValue(groupName, out List<IProjectionCallback>? retired)) {
            _retiredCallbacks[groupName] = retired = [];
        }
        retired.RemoveAll(item => item.Completion.IsCompleted);
        retired.Add(callback);
    }

    private static string BuildGroupName(string projectionType, string tenantId, string? groupScope)
        => string.IsNullOrWhiteSpace(groupScope)
            ? $"{projectionType}:{tenantId}"
            : $"{projectionType}:{tenantId}:{groupScope}";

    private static string? NormalizeScope(string? groupScope, string paramName) {
        if (string.IsNullOrWhiteSpace(groupScope)) {
            return null;
        }

        if (groupScope.Contains(':', StringComparison.Ordinal)) {
            throw new ArgumentException($"{paramName} must not contain ':'.", paramName);
        }

        if (groupScope.Length > MaxGroupScopeLength) {
            throw new ArgumentException($"{paramName} must not exceed {MaxGroupScopeLength} characters.", paramName);
        }

        return groupScope;
    }

    private static IReadOnlyDictionary<string, string> CopyMetadata(IReadOnlyDictionary<string, string>? metadata)
        => metadata is null || metadata.Count == 0
            ? EmptyMetadata
            : new ReadOnlyDictionary<string, string>(
                new Dictionary<string, string>(metadata, StringComparer.Ordinal));

    private async Task RollBackSignalCallbackAsync(string groupName, GroupSubscription subscription, Guid callbackId) {
        Task drain = Task.CompletedTask;
        lock (_subscriptionGate) {
            if (subscription.Callbacks.TryRemove(callbackId, out ProjectionCallback<bool>? callback)) {
                RetireCallback(groupName, callback);
                drain = IsExecutingCallback() ? Task.CompletedTask : callback.Completion;
                _intentGeneration++;
            }
            RemoveSubscriptionIfEmpty(groupName, subscription);
        }
        await drain.ConfigureAwait(false);
    }

    private async Task RollBackDetailCallbackAsync(string groupName, GroupSubscription subscription, Guid callbackId) {
        Task drain = Task.CompletedTask;
        lock (_subscriptionGate) {
            if (subscription.DetailCallbacks.TryRemove(callbackId, out ProjectionCallback<ProjectionChangedDetail>? callback)) {
                RetireCallback(groupName, callback);
                drain = IsExecutingCallback() ? Task.CompletedTask : callback.Completion;
                _intentGeneration++;
            }
            RemoveSubscriptionIfEmpty(groupName, subscription);
        }
        await drain.ConfigureAwait(false);
    }

    private async Task ReconcileMembershipAsync(CancellationToken cancellationToken, bool pruneUnauthorized = false) {
        if (_connection.State != HubConnectionState.Connected) { return; }
        using var attempt = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _disposeCts.Token);
        attempt.CancelAfter(MembershipAttemptBudget);
        CancellationToken token = attempt.Token;
        bool acquired = false;
        long epoch = -1;
        long attemptedGeneration = -1;
        try {
            await _membershipGate.WaitAsync(token).ConfigureAwait(false);
            acquired = true;
            if (_connection.State != HubConnectionState.Connected) { return; }
            epoch = Interlocked.Read(ref _connectionEpoch);
            if (Interlocked.Read(ref _connectionRecoveryEpoch) >= 0) {
                throw new TimeoutException("The original projection connection must terminate before membership can resume.");
            }
            if (_membershipEpoch != epoch || _membershipConnectionId != _connection.ConnectionId) {
                _membershipEpoch = epoch;
                _membershipConnectionId = _connection.ConnectionId;
                _membershipProtocol = 0;
                _legacyGroups.Clear();
                _legacyUncertainGroups.Clear();
            }
            if (_membershipProtocol == 0) {
                try {
                    int protocol = await _connection.InvokeAsync<int>("GetProjectionMembershipProtocol", token).ConfigureAwait(false);
                    if (protocol != 1) { throw new HubException("Unsupported projection membership protocol."); }
                    _membershipProtocol = 1;
                }
                catch (HubException exception) when (exception.Message.Contains("Unknown hub method", StringComparison.OrdinalIgnoreCase)
                    || exception.Message.Contains("does not exist", StringComparison.OrdinalIgnoreCase)) {
                    _membershipProtocol = -1;
                }
            }
            while (_connection.State == HubConnectionState.Connected) {
                GroupSubscription[] desired;
                long generation;
                lock (_subscriptionGate) {
                    desired = [.. _subscribedGroups.Values];
                    generation = _intentGeneration;
                    attemptedGeneration = generation;
                    if (generation == Interlocked.Read(ref _rejectedIntentGeneration) && _membershipRejection is not null) {
                        throw _membershipRejection;
                    }
                    if (_membershipProtocol == -1) {
                        foreach (GroupSubscription group in desired) {
                            _legacyGroups[BuildGroupName(group.ProjectionType, group.TenantId, group.GroupScope)] = group;
                        }
                    }
                }
                if (_membershipProtocol == 1) {
                    string[][] groups = [.. desired.Select(group => new[] { group.ProjectionType, group.TenantId, group.GroupScope ?? string.Empty })];
                    try {
                        long acknowledged = await _connection.InvokeAsync<long>("ReconcileProjectionMembership", generation, groups, token).ConfigureAwait(false);
                        if (acknowledged < generation) { throw new HubException("Projection membership acknowledgement is stale."); }
                    }
                    catch (HubException exception) when (pruneUnauthorized && IsAuthorizationRejection(exception)) {
                        foreach (GroupSubscription group in desired) {
                            try {
                                await _connection.InvokeAsync("ValidateProjectionGroup", group.ProjectionType,
                                    group.TenantId, group.GroupScope, token).ConfigureAwait(false);
                            }
                            catch (HubException rejected) when (IsAuthorizationRejection(rejected)) {
                                await PruneRejectedGroupAsync(group).ConfigureAwait(false);
                            }
                        }
                        continue;
                    }
                }
                else {
                    // Legacy servers have no fence for cancelled invocations. Retain all uncertain groups
                    // until connection termination; this fallback does not claim acknowledged membership.
                    // Retire obsolete groups first so correcting an offline over-quota intent
                    // can converge without a rejected join monopolizing the legacy iteration.
                    foreach (GroupSubscription group in _legacyGroups.Values.OrderBy(group => desired.Contains(group)).ToArray()) {
                        string name = BuildGroupName(group.ProjectionType, group.TenantId, group.GroupScope);
                        bool present = desired.Contains(group);
                        try {
                            string method = present ? "JoinGroupScoped" : "LeaveGroupScoped";
                            if (group.GroupScope is null) {
                                await _connection.InvokeAsync(present ? "JoinGroup" : "LeaveGroup", group.ProjectionType, group.TenantId, token).ConfigureAwait(false);
                            }
                            else {
                                await _connection.InvokeAsync(method, group.ProjectionType, group.TenantId, group.GroupScope, token).ConfigureAwait(false);
                            }
                            // An ordinary acknowledged leave can retire bookkeeping. A prior canceled
                            // invocation could still finish later on an old server, so its obligation
                            // survives further acknowledgements until this physical connection ends.
                            if (!present && !_legacyUncertainGroups.Contains(name)) { _legacyGroups.Remove(name); }
                        }
                        catch (HubException exception) when (present && IsAuthorizationRejection(exception)) {
                            await PruneRejectedGroupAsync(group).ConfigureAwait(false);
                            if (!pruneUnauthorized) { throw; }
                        }
                        catch {
                            _legacyUncertainGroups.Add(name);
                            throw;
                        }
                    }
                }
                lock (_subscriptionGate) {
                    if (_intentGeneration == generation) {
                        if (_connection.State == HubConnectionState.Connected && epoch == Interlocked.Read(ref _connectionEpoch)
                            && Interlocked.Exchange(ref _membershipRecoveryPending, 0) == 1) {
                            Interlocked.Increment(ref _recoveryGeneration);
                        }
                        return;
                    }
                }
            }
        }
        catch (OperationCanceledException) when (attempt.IsCancellationRequested && !cancellationToken.IsCancellationRequested && !_disposeCts.IsCancellationRequested) {
            // Cancelling the invocation cannot prove remote absence. End this exact physical
            // connection before dropping its uncertainty, then restore every retained intent.
            if (acquired && epoch >= 0) { Interlocked.Exchange(ref _connectionRecoveryEpoch, epoch); }
            ScheduleMembershipRepair();
            throw new TimeoutException("Projection membership exceeded its five-second attempt budget.");
        }
        catch (HubException exception) when (IsPermanentMembershipRejection(exception)) {
            _membershipRejection = exception;
            Interlocked.Exchange(ref _rejectedIntentGeneration, attemptedGeneration >= 0 ? attemptedGeneration : Interlocked.Read(ref _intentGeneration));
            throw;
        }
        catch { ScheduleMembershipRepair(); throw; }
        finally { if (acquired) { _membershipGate.Release(); } }
    }

    private void ScheduleMembershipRepair() {
        lock (_repairGate) {
            _repairRequested++;
            if (_disposeCts.IsCancellationRequested || HasRejectedCurrentIntent || (!IsConnected && Interlocked.Read(ref _connectionRecoveryEpoch) < 0) || _repairRunning != 0) { return; }
            _repairRunning = 1;
            _repairTask = Task.Run(RepairMembershipAsync);
        }
    }

    private async Task RepairMembershipAsync() {
        long observed = 0;
        int failures = 0;
        try {
            while (!_disposeCts.IsCancellationRequested && (IsConnected || Interlocked.Read(ref _connectionRecoveryEpoch) >= 0)) {
                observed = Interlocked.Read(ref _repairRequested);
                try {
                    if (Interlocked.Read(ref _connectionRecoveryEpoch) >= 0) { await RecoverPhysicalConnectionAsync().ConfigureAwait(false); }
                    await ReconcileMembershipAsync(_disposeCts.Token, pruneUnauthorized: true).ConfigureAwait(false);
                    failures = 0;
                    if (observed == Interlocked.Read(ref _repairRequested)) { return; }
                }
                catch (OperationCanceledException) when (_disposeCts.IsCancellationRequested) { return; }
                catch (HubException exception) when (IsPermanentMembershipRejection(exception)) { return; }
                catch (Exception exception) {
                    _logger?.LogWarning(exception, "Projection membership remains uncertain; reconciliation will retry.");
                    double ceiling = Math.Min(5000, 200 * Math.Pow(2, Math.Min(failures++, 5)));
                    // Positive jitter avoids synchronized repairs while retaining a five-second cap.
                    await Task.Delay(TimeSpan.FromMilliseconds(ceiling * (0.75 + Random.Shared.NextDouble() * 0.25)), _disposeCts.Token).ConfigureAwait(false);
                }
            }
        }
        catch (OperationCanceledException) when (_disposeCts.IsCancellationRequested) { }
        finally {
            lock (_repairGate) {
                _repairRunning = 0;
                if (!_disposeCts.IsCancellationRequested && !HasRejectedCurrentIntent && (IsConnected || Interlocked.Read(ref _connectionRecoveryEpoch) >= 0) && observed != _repairRequested) {
                    _repairRunning = 1;
                    _repairTask = Task.Run(RepairMembershipAsync);
                }
            }
        }
    }

    private void RemoveSubscriptionIfEmpty(string groupName, GroupSubscription subscription) {
        if (subscription.Callbacks.IsEmpty && subscription.DetailCallbacks.IsEmpty) {
            _ = ((ICollection<KeyValuePair<string, GroupSubscription>>)_subscribedGroups).Remove(new(groupName, subscription));
        }
    }

    private static void ValidateGroupPart(string value, string paramName) {
        ArgumentException.ThrowIfNullOrWhiteSpace(value, paramName);

        if (value.Contains(':', StringComparison.Ordinal)) {
            throw new ArgumentException($"{paramName} must not contain ':'.", paramName);
        }
    }

    /// <summary>
    /// Joins all tracked groups. Used by both <see cref="StartAsync"/> (initial connect) and
    /// <see cref="OnReconnectedAsync"/> (reconnect). Respects dispose cancellation.
    /// </summary>
    private Task JoinAllGroupsAsync() => JoinAllGroupsAsync(_disposeCts.Token);

    private async Task JoinAllGroupsAsync(CancellationToken cancellationToken) {
        using var invocation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _disposeCts.Token);
        try { await ReconcileMembershipAsync(invocation.Token, pruneUnauthorized: true).ConfigureAwait(false); }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { ScheduleMembershipRepair(); throw; }
        catch (HubException exception) when (IsPermanentMembershipRejection(exception)) { throw; }
        catch (Exception exception) {
            _logger?.LogWarning(exception, "Failed to reconcile projection membership.");
            ScheduleMembershipRepair();
        }
    }

    private bool HasRejectedCurrentIntent => Interlocked.Read(ref _rejectedIntentGeneration) == Interlocked.Read(ref _intentGeneration);

    private static bool IsPermanentMembershipRejection(HubException exception)
        => exception.Message.Contains("Invalid projection membership snapshot.", StringComparison.Ordinal)
            || exception.Message.Contains("Maximum groups per connection", StringComparison.Ordinal)
            || exception.Message.Contains("Unsupported projection membership protocol.", StringComparison.Ordinal)
            || exception.Message.Contains("Tenant scope is invalid or reserved.", StringComparison.Ordinal);

    private static bool IsAuthorizationRejection(HubException exception)
        => exception.Message.Contains("Tenant authorization failed.", StringComparison.Ordinal);

    private Task PruneRejectedGroupAsync(GroupSubscription subscription) {
        lock (_subscriptionGate) {
            string name = BuildGroupName(subscription.ProjectionType, subscription.TenantId, subscription.GroupScope);
            if (_subscribedGroups.TryGetValue(name, out GroupSubscription? current) && ReferenceEquals(current, subscription)) {
                foreach (IProjectionCallback callback in subscription.Callbacks.Values.Cast<IProjectionCallback>().Concat(subscription.DetailCallbacks.Values)) {
                    RetireCallback(name, callback);
                }
                _ = _subscribedGroups.TryRemove(name, out _);
                _intentGeneration++;
            }
        }
        // Revoked invocations remain owned by the retired inventory. External removal and
        // disposal drain them; membership recovery never holds its gate across user code.
        return Task.CompletedTask;
    }

    private async Task RecoverPhysicalConnectionAsync() {
        using var attempt = CancellationTokenSource.CreateLinkedTokenSource(_disposeCts.Token);
        attempt.CancelAfter(MembershipAttemptBudget);
        await _membershipGate.WaitAsync(attempt.Token).ConfigureAwait(false);
        try {
            long recoveryEpoch = Interlocked.Read(ref _connectionRecoveryEpoch);
            if (recoveryEpoch < 0) { return; }
            if (recoveryEpoch == Interlocked.Read(ref _connectionEpoch)) {
                await _connection.StopAsync(attempt.Token).ConfigureAwait(false);
                // Stop completion establishes termination; cancellation alone never does.
                Interlocked.Increment(ref _connectionEpoch);
                Interlocked.Increment(ref _recoveryGeneration);
                Interlocked.Exchange(ref _membershipRecoveryPending, 1);
            }
            if (_connection.State == HubConnectionState.Disconnected) {
                await _connection.StartAsync(attempt.Token).ConfigureAwait(false);
                Interlocked.Increment(ref _connectionEpoch);
            }
            if (_connection.State != HubConnectionState.Connected) { throw new InvalidOperationException("Projection connection recovery has not completed."); }
            Interlocked.Exchange(ref _connectionRecoveryEpoch, -1);
        }
        finally { _membershipGate.Release(); }
    }

    private static void ConfigureAutomaticReconnect(HubConnectionBuilder builder, IRetryPolicy? retryPolicy) => _ = retryPolicy is not null
            ? builder.WithAutomaticReconnect(retryPolicy)
            : builder.WithAutomaticReconnect();

    private void OnProjectionChanged(string projectionType, string tenantId) {
        string groupName = $"{projectionType}:{tenantId}";
        if (_subscribedGroups.TryGetValue(groupName, out GroupSubscription? subscription)
            && !subscription.Callbacks.IsEmpty) {
            ProjectionCallback<bool>[] callbacks = [.. subscription.Callbacks.Values];
            if (_logger is not null) {
                Log.ProjectionChangedReceived(
                    _logger,
                    projectionType,
                    tenantId,
                    groupName,
                    DateTimeOffset.UtcNow,
                    _connection.State.ToString(),
                    callbacks.Length,
                    Activity.Current?.TraceId.ToString(),
                    Activity.Current?.SpanId.ToString(),
                    CategoryName);
            }

            foreach (ProjectionCallback<bool> callback in callbacks) {
                callback.Invoke(false);
            }
        }
    }

    private void OnProjectionChangedDetail(
        string projectionType,
        string tenantId,
        string? groupScope,
        IReadOnlyDictionary<string, string>? metadata) {
        string? normalizedScope = string.IsNullOrWhiteSpace(groupScope) ? null : groupScope;
        string groupName = BuildGroupName(projectionType, tenantId, normalizedScope);
        if (_subscribedGroups.TryGetValue(groupName, out GroupSubscription? subscription)
            && !subscription.DetailCallbacks.IsEmpty) {
            ProjectionCallback<ProjectionChangedDetail>[] callbacks = [.. subscription.DetailCallbacks.Values];
            IReadOnlyDictionary<string, string> safeMetadata = CopyMetadata(metadata);
            var detail = new ProjectionChangedDetail(projectionType, tenantId, normalizedScope, safeMetadata);

            if (_logger is not null) {
                Log.ProjectionChangedDetailReceived(
                    _logger,
                    projectionType,
                    tenantId,
                    normalizedScope,
                    groupName,
                    safeMetadata.Count,
                    DateTimeOffset.UtcNow,
                    _connection.State.ToString(),
                    callbacks.Length,
                    Activity.Current?.TraceId.ToString(),
                    Activity.Current?.SpanId.ToString(),
                    CategoryName);
            }

            foreach (ProjectionCallback<ProjectionChangedDetail> callback in callbacks) {
                callback.Invoke(detail);
            }
        }
    }

    /// <summary>
    /// Logs a warning when the connection closes unexpectedly.
    /// Consumers decide whether to restart the connection.
    /// </summary>
    private Task OnClosedAsync(Exception? exception) {
        Interlocked.Increment(ref _recoveryGeneration);
        if (!_disposeCts.IsCancellationRequested && exception is not null) {
            _logger?.LogWarning(exception, "SignalR connection closed unexpectedly.");
        }

        return Task.CompletedTask;
    }

    /// <summary>
    /// FR59: Auto-rejoin all subscribed groups on reconnection.
    /// </summary>
    private async Task OnReconnectedAsync(string? connectionId) {
        Interlocked.Increment(ref _recoveryGeneration);
        Interlocked.Increment(ref _connectionEpoch);
        Interlocked.Exchange(ref _membershipRecoveryPending, 1);
        await JoinAllGroupsAsync().ConfigureAwait(false);
    }

    private Task OnReconnectingAsync(Exception? exception) {
        Interlocked.Increment(ref _recoveryGeneration);
        return Task.CompletedTask;
    }

    private sealed class GroupSubscription(string projectionType, string tenantId, string? groupScope) {
        public string ProjectionType { get; } = projectionType;

        public string TenantId { get; } = tenantId;

        public string? GroupScope { get; } = groupScope;

        public ConcurrentDictionary<Guid, ProjectionCallback<bool>> Callbacks { get; } = new();

        public ConcurrentDictionary<Guid, ProjectionCallback<ProjectionChangedDetail>> DetailCallbacks { get; } = new();
    }

    private static partial class Log {
        [LoggerMessage(EventId = 2090, Level = LogLevel.Information,
            Message = "SignalR client received projection change. ProjectionType: {ProjectionType}, TenantId: {TenantId}, Group: {GroupName}, ReceiptUtc: {ReceiptUtc}, ConnectionState: {ConnectionState}, CallbackCount: {CallbackCount}, TraceId: {TraceId}, SpanId: {SpanId}, CategoryName: {CategoryName}")]
        public static partial void ProjectionChangedReceived(ILogger logger, string projectionType, string tenantId, string groupName, DateTimeOffset receiptUtc, string connectionState, int callbackCount, string? traceId, string? spanId, string categoryName);

        [LoggerMessage(EventId = 2091, Level = LogLevel.Information,
            Message = "SignalR client received projection change detail. ProjectionType: {ProjectionType}, TenantId: {TenantId}, GroupScope: {GroupScope}, Group: {GroupName}, MetadataCount: {MetadataCount}, ReceiptUtc: {ReceiptUtc}, ConnectionState: {ConnectionState}, CallbackCount: {CallbackCount}, TraceId: {TraceId}, SpanId: {SpanId}, CategoryName: {CategoryName}")]
        public static partial void ProjectionChangedDetailReceived(ILogger logger, string projectionType, string tenantId, string? groupScope, string groupName, int metadataCount, DateTimeOffset receiptUtc, string connectionState, int callbackCount, string? traceId, string? spanId, string categoryName);
    }
}
