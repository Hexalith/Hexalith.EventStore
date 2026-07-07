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
    private const int MaxGroupScopeLength = 64;

    private static readonly ConcurrentDictionary<string, HashSet<string>> _connectionGroups = new();

    /// <summary>
    /// Adds the calling client to the tenant-wide projection change group.
    /// Group name format: {projectionType}:{tenantId}.
    /// </summary>
    /// <param name="projectionType">The projection type name (kebab-case).</param>
    /// <param name="tenantId">The tenant identifier.</param>
    public Task JoinGroup(string projectionType, string tenantId)
        => JoinGroupCoreAsync(projectionType, tenantId, scope: null);

    /// <summary>
    /// Adds the calling client to a scoped projection change group below tenant level.
    /// Group name format: {projectionType}:{tenantId}:{scope} (or the tenant-wide group when
    /// <paramref name="scope"/> is null/empty/whitespace).
    /// <para>
    /// Exposed as a distinct hub method because SignalR does not permit overloaded hub method
    /// names; the tenant-wide <see cref="JoinGroup(string, string)"/> stays wire-compatible.
    /// </para>
    /// </summary>
    /// <param name="projectionType">The projection type name (kebab-case).</param>
    /// <param name="tenantId">The tenant identifier.</param>
    /// <param name="scope">Optional sub-tenant group scope (for example, a conversation id).</param>
    public Task JoinGroupScoped(string projectionType, string tenantId, string? scope)
        => JoinGroupCoreAsync(projectionType, tenantId, scope);

    /// <summary>
    /// Removes the calling client from the tenant-wide projection change group.
    /// </summary>
    /// <param name="projectionType">The projection type name (kebab-case).</param>
    /// <param name="tenantId">The tenant identifier.</param>
    public Task LeaveGroup(string projectionType, string tenantId)
        => LeaveGroupCoreAsync(projectionType, tenantId, scope: null);

    /// <summary>
    /// Removes the calling client from a scoped projection change group.
    /// </summary>
    /// <param name="projectionType">The projection type name (kebab-case).</param>
    /// <param name="tenantId">The tenant identifier.</param>
    /// <param name="scope">Optional sub-tenant group scope (for example, a conversation id).</param>
    public Task LeaveGroupScoped(string projectionType, string tenantId, string? scope)
        => LeaveGroupCoreAsync(projectionType, tenantId, scope);

    /// <summary>
    /// Builds the SignalR group name. A null/empty/whitespace scope yields the tenant-wide group
    /// {projectionType}:{tenantId}; a non-empty scope yields {projectionType}:{tenantId}:{scope}.
    /// </summary>
    internal static string BuildGroupName(string projectionType, string tenantId, string? scope)
        => string.IsNullOrWhiteSpace(scope)
            ? $"{projectionType}:{tenantId}"
            : $"{projectionType}:{tenantId}:{scope}";

    private async Task JoinGroupCoreAsync(string projectionType, string tenantId, string? scope) {
        ArgumentException.ThrowIfNullOrWhiteSpace(projectionType);
        ArgumentException.ThrowIfNullOrWhiteSpace(tenantId);

        // Defense-in-depth: colons are reserved as group name separator
        if (projectionType.Contains(':') || tenantId.Contains(':')) {
            throw new HubException("projectionType and tenantId must not contain colons.");
        }

        // A non-empty scope must not contain the reserved separator either; null/empty/whitespace
        // is allowed and resolves to the tenant-wide group.
        string? normalizedScope = string.IsNullOrWhiteSpace(scope) ? null : scope;
        if (normalizedScope is not null && normalizedScope.Contains(':')) {
            throw new HubException("scope must not contain colons.");
        }

        if (normalizedScope is not null && normalizedScope.Length > MaxGroupScopeLength) {
            throw new HubException($"scope must not exceed {MaxGroupScopeLength} characters.");
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

        string groupName = BuildGroupName(projectionType, tenantId, normalizedScope);

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

    private async Task LeaveGroupCoreAsync(string projectionType, string tenantId, string? scope) {
        string? normalizedScope = string.IsNullOrWhiteSpace(scope) ? null : scope;
        if (normalizedScope is not null && normalizedScope.Contains(':')) {
            throw new HubException("scope must not contain colons.");
        }

        if (normalizedScope is not null && normalizedScope.Length > MaxGroupScopeLength) {
            throw new HubException($"scope must not exceed {MaxGroupScopeLength} characters.");
        }

        string groupName = BuildGroupName(projectionType, tenantId, normalizedScope);

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
