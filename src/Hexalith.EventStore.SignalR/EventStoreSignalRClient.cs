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

    private long _recoveryGeneration;

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

    /// <inheritdoc/>
    public async ValueTask DisposeAsync() {
        await _disposeCts.CancelAsync().ConfigureAwait(false);
        lock (_subscriptionGate) {
            foreach (GroupSubscription subscription in _subscribedGroups.Values) {
                foreach (ProjectionCallback<bool> callback in subscription.Callbacks.Values) {
                    callback.Revoke();
                }
                foreach (ProjectionCallback<ProjectionChangedDetail> callback in subscription.DetailCallbacks.Values) {
                    callback.Revoke();
                }
            }
            _subscribedGroups.Clear();
        }

        if (_connection.State != HubConnectionState.Disconnected) {
            await _connection.StopAsync().ConfigureAwait(false);
        }

        await _connection.DisposeAsync().ConfigureAwait(false);
        _disposeCts.Dispose();
    }

    /// <summary>
    /// Starts the SignalR connection and joins all pre-subscribed groups.
    /// Groups added via <see cref="SubscribeAsync"/> before <see cref="StartAsync"/> are joined on initial connect.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    public async Task StartAsync(CancellationToken cancellationToken = default) {
        await _connection.StartAsync(cancellationToken).ConfigureAwait(false);

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
            subscription = _subscribedGroups.GetOrAdd(groupName, _ => new GroupSubscription(projectionType, tenantId, null));
            _ = subscription.Callbacks.TryAdd(callbackId, new ProjectionCallback<bool>(onChanged, _ => onChanged()));
        }

        try {
            token.ThrowIfCancellationRequested();
            if (_connection.State == HubConnectionState.Connected) {
                await _connection.InvokeAsync("JoinGroup", projectionType, tenantId, token).ConfigureAwait(false);
            }
            token.ThrowIfCancellationRequested();
        }
        catch {
            RollBackSignalCallback(groupName, subscription, callbackId);
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
            subscription = _subscribedGroups.GetOrAdd(groupName, _ => new GroupSubscription(projectionType, tenantId, normalizedScope));
            _ = subscription.DetailCallbacks.TryAdd(callbackId, new ProjectionCallback<ProjectionChangedDetail>(onChanged, onChanged));
        }

        try {
            token.ThrowIfCancellationRequested();
            if (_connection.State == HubConnectionState.Connected) {
                await _connection.InvokeAsync("JoinGroupScoped", projectionType, tenantId, normalizedScope, token).ConfigureAwait(false);
            }
            token.ThrowIfCancellationRequested();
        }
        catch {
            RollBackDetailCallback(groupName, subscription, callbackId);
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
        using var invocation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _disposeCts.Token);
        ValidateGroupPart(projectionType, nameof(projectionType));
        ValidateGroupPart(tenantId, nameof(tenantId));
        ArgumentNullException.ThrowIfNull(onChanged);
        string? normalizedScope = null;
        string groupName = BuildGroupName(projectionType, tenantId, normalizedScope);
        bool leave;
        lock (_subscriptionGate) {
            if (!_subscribedGroups.TryGetValue(groupName, out GroupSubscription? subscription)) {
                return;
            }
            foreach ((Guid key, ProjectionCallback<bool> callback) in subscription.Callbacks.ToArray()) {
                if (callback.Original == (Delegate)onChanged) {
                    callback.Revoke();
                    _ = subscription.Callbacks.TryRemove(key, out _);
                }
            }
            leave = subscription.Callbacks.IsEmpty && subscription.DetailCallbacks.IsEmpty;
            RemoveSubscriptionIfEmpty(groupName, subscription);
        }
        if (leave && _connection.State == HubConnectionState.Connected) {
            await _connection.InvokeAsync("LeaveGroup", projectionType, tenantId, invocation.Token).ConfigureAwait(false);
        }
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
        using var invocation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _disposeCts.Token);
        ValidateGroupPart(projectionType, nameof(projectionType));
        ValidateGroupPart(tenantId, nameof(tenantId));
        string? normalizedScope = null;
        string groupName = BuildGroupName(projectionType, tenantId, normalizedScope);
        bool leave;
        lock (_subscriptionGate) {
            if (!_subscribedGroups.TryGetValue(groupName, out GroupSubscription? subscription)) {
                return;
            }
            foreach ((Guid key, ProjectionCallback<bool> callback) in subscription.Callbacks.ToArray()) {
                callback.Revoke();
                _ = subscription.Callbacks.TryRemove(key, out _);
            }
            leave = subscription.Callbacks.IsEmpty && subscription.DetailCallbacks.IsEmpty;
            RemoveSubscriptionIfEmpty(groupName, subscription);
        }
        if (leave && _connection.State == HubConnectionState.Connected) {
            await _connection.InvokeAsync("LeaveGroup", projectionType, tenantId, invocation.Token).ConfigureAwait(false);
        }
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
        using var invocation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _disposeCts.Token);
        ValidateGroupPart(projectionType, nameof(projectionType));
        ValidateGroupPart(tenantId, nameof(tenantId));
        ArgumentNullException.ThrowIfNull(onChanged);
        string? normalizedScope = NormalizeScope(groupScope, nameof(groupScope));
        string groupName = BuildGroupName(projectionType, tenantId, normalizedScope);
        bool leave;
        lock (_subscriptionGate) {
            if (!_subscribedGroups.TryGetValue(groupName, out GroupSubscription? subscription)) {
                return;
            }
            foreach ((Guid key, ProjectionCallback<ProjectionChangedDetail> callback) in subscription.DetailCallbacks.ToArray()) {
                if (callback.Original == (Delegate)onChanged) {
                    callback.Revoke();
                    _ = subscription.DetailCallbacks.TryRemove(key, out _);
                }
            }
            leave = subscription.Callbacks.IsEmpty && subscription.DetailCallbacks.IsEmpty;
            RemoveSubscriptionIfEmpty(groupName, subscription);
        }
        if (leave && _connection.State == HubConnectionState.Connected) {
            await _connection.InvokeAsync("LeaveGroupScoped", projectionType, tenantId, normalizedScope, invocation.Token).ConfigureAwait(false);
        }
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
        using var invocation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _disposeCts.Token);
        ValidateGroupPart(projectionType, nameof(projectionType));
        ValidateGroupPart(tenantId, nameof(tenantId));
        string? normalizedScope = NormalizeScope(groupScope, nameof(groupScope));
        string groupName = BuildGroupName(projectionType, tenantId, normalizedScope);
        bool leave;
        lock (_subscriptionGate) {
            if (!_subscribedGroups.TryGetValue(groupName, out GroupSubscription? subscription)) {
                return;
            }
            foreach ((Guid key, ProjectionCallback<ProjectionChangedDetail> callback) in subscription.DetailCallbacks.ToArray()) {
                callback.Revoke();
                _ = subscription.DetailCallbacks.TryRemove(key, out _);
            }
            leave = subscription.Callbacks.IsEmpty && subscription.DetailCallbacks.IsEmpty;
            RemoveSubscriptionIfEmpty(groupName, subscription);
        }
        if (leave && _connection.State == HubConnectionState.Connected) {
            await _connection.InvokeAsync("LeaveGroupScoped", projectionType, tenantId, normalizedScope, invocation.Token).ConfigureAwait(false);
        }
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

    private static Task JoinSubscriptionAsync(HubConnection connection, GroupSubscription subscription, CancellationToken cancellationToken) {
        if (subscription.GroupScope is not null) {
            return connection.InvokeAsync(
                "JoinGroupScoped",
                subscription.ProjectionType,
                subscription.TenantId,
                subscription.GroupScope,
                cancellationToken);
        }

        return connection.InvokeAsync(
            "JoinGroup",
            subscription.ProjectionType,
            subscription.TenantId,
            cancellationToken);
    }

    private void RollBackSignalCallback(string groupName, GroupSubscription subscription, Guid callbackId) {
        lock (_subscriptionGate) {
            if (subscription.Callbacks.TryRemove(callbackId, out ProjectionCallback<bool>? callback)) {
                callback.Revoke();
            }
            RemoveSubscriptionIfEmpty(groupName, subscription);
        }
    }

    private void RollBackDetailCallback(string groupName, GroupSubscription subscription, Guid callbackId) {
        lock (_subscriptionGate) {
            if (subscription.DetailCallbacks.TryRemove(callbackId, out ProjectionCallback<ProjectionChangedDetail>? callback)) {
                callback.Revoke();
            }
            RemoveSubscriptionIfEmpty(groupName, subscription);
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
        foreach (KeyValuePair<string, GroupSubscription> item in _subscribedGroups.ToArray()) {
            if (_disposeCts.IsCancellationRequested) {
                break;
            }

            try {
                await JoinSubscriptionAsync(_connection, item.Value, invocation.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested && !_disposeCts.IsCancellationRequested) {
                throw;
            }
            catch (OperationCanceledException) {
                break; // Client is disposing
            }
            catch (HubException ex) {
                // Server actively rejected the rejoin (e.g., tenant authorization denied).
                // Prune the subscription so we do not retry forever and do not leave the
                // consumer with the impression the group is still authorized.
                _ = _subscribedGroups.TryRemove(item.Key, out _);
                _logger?.LogError(ex, "Server rejected rejoin for group {GroupName}; subscription pruned.", item.Key);
            }
            catch (Exception ex) {
                _logger?.LogWarning(ex, "Failed to rejoin group {GroupName}", item.Key);
            }
        }
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
        await JoinAllGroupsAsync().ConfigureAwait(false);
        Interlocked.Increment(ref _recoveryGeneration);
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
