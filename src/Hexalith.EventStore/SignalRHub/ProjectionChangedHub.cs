using System.Collections.Concurrent;

using Hexalith.EventStore.Authorization;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Options;

namespace Hexalith.EventStore.SignalRHub;

/// <summary>
/// SignalR hub for real-time projection change notifications.
/// Clients join groups by projection type and tenant to receive targeted "changed" signals.
/// </summary>
[Authorize]
public partial class ProjectionChangedHub {
    /// <summary>
    /// The hub endpoint path. Hardcoded — no valid reason to make configurable.
    /// </summary>
    public const string HubPath = "/hubs/projection-changes";
}

/// <summary>
/// SignalR hub implementation with group management and structured logging.
/// </summary>
public partial class ProjectionChangedHub(
    ITenantValidator tenantValidator,
    IOptions<SignalROptions> options,
    ILogger<ProjectionChangedHub> logger) : Hub<IProjectionChangedClient> {
    private static readonly ConcurrentDictionary<string, HashSet<string>> _connectionGroups = new();

    /// <summary>
    /// Adds the calling client to the projection change group.
    /// Group name format: {projectionType}:{tenantId}.
    /// </summary>
    /// <param name="projectionType">The projection type name (kebab-case).</param>
    /// <param name="tenantId">The tenant identifier.</param>
    public async Task JoinGroup(string projectionType, string tenantId) {
        ArgumentException.ThrowIfNullOrWhiteSpace(projectionType);
        ArgumentException.ThrowIfNullOrWhiteSpace(tenantId);

        // Defense-in-depth: colons are reserved as group name separator
        if (projectionType.Contains(':') || tenantId.Contains(':')) {
            throw new HubException("projectionType and tenantId must not contain colons.");
        }

        if (Context.User?.Identity?.IsAuthenticated != true) {
            throw new HubException("Authentication is required to join projection change groups.");
        }

        TenantValidationResult tenantValidation;
        try {
            tenantValidation = await tenantValidator
                .ValidateAsync(Context.User, tenantId, Context.ConnectionAborted)
                .ConfigureAwait(false);
        }
        catch (OperationCanceledException) {
            throw;
        }
        catch (Exception ex) {
            Log.TenantValidatorFailed(logger, ex, Context.ConnectionId);
            throw new HubException("Tenant authorization unavailable.");
        }

        if (!tenantValidation.IsAuthorized) {
            Log.TenantAuthorizationDenied(logger, Context.ConnectionId, tenantValidation.Reason ?? "(no reason)");
            throw new HubException("Tenant authorization failed.");
        }

        string groupName = $"{projectionType}:{tenantId}";

        // Guard: limit groups per connection to prevent resource exhaustion
        HashSet<string> groups = _connectionGroups.GetOrAdd(Context.ConnectionId, _ => []);
        bool addedToTracking = false;
        lock (groups) {
            if (groups.Count >= options.Value.MaxGroupsPerConnection && !groups.Contains(groupName)) {
                throw new HubException($"Maximum groups per connection ({options.Value.MaxGroupsPerConnection}) exceeded.");
            }

            addedToTracking = groups.Add(groupName);
        }

        try {
            await Groups.AddToGroupAsync(Context.ConnectionId, groupName).ConfigureAwait(false);
        }
        catch {
            if (addedToTracking) {
                lock (groups) {
                    _ = groups.Remove(groupName);
                }

                if (groups.Count == 0) {
                    _ = _connectionGroups.TryRemove(Context.ConnectionId, out _);
                }
            }

            throw;
        }

        Log.ClientJoinedGroup(logger, Context.ConnectionId, groupName);
    }

    /// <summary>
    /// Removes the calling client from the projection change group.
    /// </summary>
    /// <param name="projectionType">The projection type name (kebab-case).</param>
    /// <param name="tenantId">The tenant identifier.</param>
    public async Task LeaveGroup(string projectionType, string tenantId) {
        string groupName = $"{projectionType}:{tenantId}";

        if (_connectionGroups.TryGetValue(Context.ConnectionId, out HashSet<string>? groups)) {
            lock (groups) {
                _ = groups.Remove(groupName);
            }
        }

        await Groups.RemoveFromGroupAsync(Context.ConnectionId, groupName).ConfigureAwait(false);
        Log.ClientLeftGroup(logger, Context.ConnectionId, groupName);
    }

    /// <inheritdoc/>
    public override Task OnConnectedAsync() {
        Log.ClientConnected(logger, Context.ConnectionId);
        return base.OnConnectedAsync();
    }

    /// <inheritdoc/>
    public override Task OnDisconnectedAsync(Exception? exception) {
        // Clean up group tracking for this connection
        _ = _connectionGroups.TryRemove(Context.ConnectionId, out _);
        Log.ClientDisconnected(logger, Context.ConnectionId, exception?.GetType().Name);
        return base.OnDisconnectedAsync(exception);
    }

    private static partial class Log {
        [LoggerMessage(EventId = 1080, Level = LogLevel.Debug,
            Message = "SignalR client {ConnectionId} joined group {GroupName}")]
        public static partial void ClientJoinedGroup(ILogger logger, string connectionId, string groupName);

        [LoggerMessage(EventId = 1081, Level = LogLevel.Debug,
            Message = "SignalR client {ConnectionId} left group {GroupName}")]
        public static partial void ClientLeftGroup(ILogger logger, string connectionId, string groupName);

        [LoggerMessage(EventId = 1082, Level = LogLevel.Debug,
            Message = "SignalR client connected: {ConnectionId}")]
        public static partial void ClientConnected(ILogger logger, string connectionId);

        [LoggerMessage(EventId = 1083, Level = LogLevel.Debug,
            Message = "SignalR client disconnected: {ConnectionId}, ExceptionType: {ExceptionType}")]
        public static partial void ClientDisconnected(ILogger logger, string connectionId, string? exceptionType);

        [LoggerMessage(EventId = 1084, Level = LogLevel.Warning,
            Message = "SignalR client {ConnectionId} tenant authorization denied: {Reason}")]
        public static partial void TenantAuthorizationDenied(ILogger logger, string connectionId, string reason);

        [LoggerMessage(EventId = 1085, Level = LogLevel.Error,
            Message = "Tenant validator failed for SignalR connection {ConnectionId}")]
        public static partial void TenantValidatorFailed(ILogger logger, Exception exception, string connectionId);
    }
}
