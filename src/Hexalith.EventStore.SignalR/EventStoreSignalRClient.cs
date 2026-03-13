using System.Collections.Concurrent;

using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.Logging;

namespace Hexalith.EventStore.SignalR;

/// <summary>
/// Client helper for receiving real-time projection change signals via SignalR.
/// Handles automatic reconnection and group rejoin (FR59).
/// Implements <see cref="IAsyncDisposable"/> — callers MUST dispose when done.
/// </summary>
public sealed class EventStoreSignalRClient : IAsyncDisposable {
    private readonly HubConnection _connection;
    private readonly ConcurrentDictionary<string, Action> _subscribedGroups = new();
    private readonly CancellationTokenSource _disposeCts = new();
    private readonly ILogger<EventStoreSignalRClient>? _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="EventStoreSignalRClient"/> class.
    /// </summary>
    /// <param name="options">The SignalR client options containing the hub URL.</param>
    /// <param name="logger">Optional logger for diagnostic output.</param>
    public EventStoreSignalRClient(EventStoreSignalRClientOptions options, ILogger<EventStoreSignalRClient>? logger = null) {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentException.ThrowIfNullOrWhiteSpace(options.HubUrl);
        if (!Uri.TryCreate(options.HubUrl, UriKind.Absolute, out _)) {
            throw new ArgumentException("HubUrl must be an absolute URL.", nameof(options));
        }

        _logger = logger;

        HubConnectionBuilder builder = new();
        builder.WithUrl(options.HubUrl);
        builder.WithAutomaticReconnect();

        _connection = builder.Build();

        _connection.Reconnected += OnReconnectedAsync;
        _connection.On<string, string>("ProjectionChanged", OnProjectionChanged);
    }

    /// <summary>
    /// Subscribes to projection change signals for the given projection type and tenant.
    /// The callback is invoked when a "changed" signal is received.
    /// </summary>
    /// <param name="projectionType">The projection type name (kebab-case).</param>
    /// <param name="tenantId">The tenant identifier.</param>
    /// <param name="onChanged">Callback invoked on projection change signal.</param>
    public async Task SubscribeAsync(string projectionType, string tenantId, Action onChanged) {
        ValidateGroupPart(projectionType, nameof(projectionType));
        ValidateGroupPart(tenantId, nameof(tenantId));
        ArgumentNullException.ThrowIfNull(onChanged);

        string groupName = $"{projectionType}:{tenantId}";
        _subscribedGroups[groupName] = onChanged;

        if (_connection.State == HubConnectionState.Connected) {
            await _connection.InvokeAsync("JoinGroup", projectionType, tenantId).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Unsubscribes from projection change signals for the given projection type and tenant.
    /// </summary>
    /// <param name="projectionType">The projection type name (kebab-case).</param>
    /// <param name="tenantId">The tenant identifier.</param>
    public async Task UnsubscribeAsync(string projectionType, string tenantId) {
        ValidateGroupPart(projectionType, nameof(projectionType));
        ValidateGroupPart(tenantId, nameof(tenantId));

        string groupName = $"{projectionType}:{tenantId}";
        _ = _subscribedGroups.TryRemove(groupName, out _);

        if (_connection.State == HubConnectionState.Connected) {
            await _connection.InvokeAsync("LeaveGroup", projectionType, tenantId).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Starts the SignalR connection and joins all pre-subscribed groups.
    /// Groups added via <see cref="SubscribeAsync"/> before <see cref="StartAsync"/> are joined on initial connect.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    public async Task StartAsync(CancellationToken cancellationToken = default) {
        await _connection.StartAsync(cancellationToken).ConfigureAwait(false);

        // Join all pre-subscribed groups on initial connect
        await JoinAllGroupsAsync().ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async ValueTask DisposeAsync() {
        await _disposeCts.CancelAsync().ConfigureAwait(false);
        _subscribedGroups.Clear();

        if (_connection.State != HubConnectionState.Disconnected) {
            await _connection.StopAsync().ConfigureAwait(false);
        }

        await _connection.DisposeAsync().ConfigureAwait(false);
        _disposeCts.Dispose();
    }

    /// <summary>
    /// FR59: Auto-rejoin all subscribed groups on reconnection.
    /// </summary>
    private Task OnReconnectedAsync(string? connectionId) => JoinAllGroupsAsync();

    /// <summary>
    /// Joins all tracked groups. Used by both <see cref="StartAsync"/> (initial connect) and
    /// <see cref="OnReconnectedAsync"/> (reconnect). Respects dispose cancellation.
    /// </summary>
    private async Task JoinAllGroupsAsync() {
        foreach (string groupName in _subscribedGroups.Keys) {
            if (_disposeCts.IsCancellationRequested) {
                break;
            }

            // Parse "projectionType:tenantId" back to components
            int separatorIndex = groupName.IndexOf(':');
            if (separatorIndex > 0 && separatorIndex < groupName.Length - 1) {
                string projectionType = groupName[..separatorIndex];
                string tenantId = groupName[(separatorIndex + 1)..];

                try {
                    await _connection.InvokeAsync(
                        "JoinGroup", projectionType, tenantId,
                        _disposeCts.Token).ConfigureAwait(false);
                }
                catch (OperationCanceledException) {
                    break; // Client is disposing
                }
                catch (Exception ex) {
                    _logger?.LogWarning(ex, "Failed to rejoin group {GroupName}", groupName);
                }
            }
        }
    }

    private void OnProjectionChanged(string projectionType, string tenantId) {
        string groupName = $"{projectionType}:{tenantId}";
        if (_subscribedGroups.TryGetValue(groupName, out Action? callback)) {
            callback();
        }
    }

    private static void ValidateGroupPart(string value, string paramName) {
        ArgumentException.ThrowIfNullOrWhiteSpace(value, paramName);

        if (value.Contains(':')) {
            throw new ArgumentException($"{paramName} must not contain ':'.", paramName);
        }
    }
}
