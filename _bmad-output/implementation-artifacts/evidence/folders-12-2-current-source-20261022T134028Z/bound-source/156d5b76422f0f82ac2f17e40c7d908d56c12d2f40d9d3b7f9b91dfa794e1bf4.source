
using System.ComponentModel;
using System.Net;
using System.Reflection;
using System.Text.Json;

using Hexalith.EventStore.Admin.Abstractions.Models.Health;

using ModelContextProtocol.Server;

namespace Hexalith.EventStore.Admin.Mcp.Tools;
/// <summary>
/// MCP tools for server connectivity and diagnostics.
/// </summary>
[McpServerToolType]
internal sealed class ServerTools {
    private static readonly string _serverName = "hexalith-eventstore-admin";
    private static readonly string _serverVersion = typeof(ServerTools).Assembly
        .GetCustomAttribute<AssemblyInformationalVersionAttribute>()
        ?.InformationalVersion ?? "0.0.0";

    /// <summary>
    /// Check connectivity to the EventStore Admin API and return server health status.
    /// </summary>
    /// <param name="adminApiClient">The Admin API client.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>A JSON string with connectivity status and health information.</returns>
    [McpServerTool(Name = "ping")]
    [Description("Check connectivity to the EventStore Admin API and return server health status")]
    public static async Task<string> Ping(
        AdminApiClient adminApiClient,
        CancellationToken cancellationToken) {
        try {
            SystemHealthReport? health = await adminApiClient.GetSystemHealthAsync(cancellationToken).ConfigureAwait(false);
            return ToolHelper.SerializeResult(new {
                serverName = _serverName,
                serverVersion = _serverVersion,
                adminApiStatus = health is null ? "error" : "reachable",
                health,
                message = health is null ? "Admin API returned empty health response" : null,
            });
        }
        catch (HttpRequestException ex) when (ex.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden) {
            return ToolHelper.SerializeResult(new {
                serverName = _serverName,
                serverVersion = _serverVersion,
                adminApiStatus = "unauthorized",
                message = "Token may be expired or invalid. Check EVENTSTORE_ADMIN_TOKEN.",
            });
        }
        catch (HttpRequestException ex) when (ex.StatusCode is not null) {
            return ToolHelper.SerializeResult(new {
                serverName = _serverName,
                serverVersion = _serverVersion,
                adminApiStatus = "error",
                message = $"HTTP {(int)ex.StatusCode} {ex.StatusCode}",
            });
        }
        catch (HttpRequestException) {
            return ToolHelper.SerializeResult(new {
                serverName = _serverName,
                serverVersion = _serverVersion,
                adminApiStatus = "unreachable",
                message = "Admin API is unreachable.",
            });
        }
        catch (TaskCanceledException) {
            return ToolHelper.SerializeResult(new {
                serverName = _serverName,
                serverVersion = _serverVersion,
                adminApiStatus = "unreachable",
                message = "Request timed out or was cancelled.",
            });
        }
        catch (JsonException) {
            return ToolHelper.SerializeResult(new {
                serverName = _serverName,
                serverVersion = _serverVersion,
                adminApiStatus = "error",
                message = "Invalid response from Admin API.",
            });
        }
    }

    /// <summary>
    /// Get comprehensive system health including event throughput, error rates, and DAPR component status.
    /// </summary>
    /// <param name="adminApiClient">The Admin API client.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>A JSON string with full health report.</returns>
    [McpServerTool(Name = "health-status")]
    [Description("Get comprehensive system health including event throughput, error rates, and DAPR component status")]
    public static async Task<string> GetHealthStatus(
        AdminApiClient adminApiClient,
        CancellationToken cancellationToken) {
        try {
            SystemHealthReport? health = await adminApiClient.GetSystemHealthAsync(cancellationToken).ConfigureAwait(false);
            return health is null
                ? ToolHelper.SerializeError("not-found", "Admin API returned empty health response")
                : ToolHelper.SerializeResult(health);
        }
        catch (Exception ex) {
            return ToolHelper.HandleException(ex);
        }
    }

    /// <summary>
    /// Get DAPR infrastructure component health status.
    /// </summary>
    /// <param name="adminApiClient">The Admin API client.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>A JSON string with DAPR component health list.</returns>
    [McpServerTool(Name = "health-dapr")]
    [Description("Get DAPR infrastructure component health status")]
    public static async Task<string> GetDaprHealth(
        AdminApiClient adminApiClient,
        CancellationToken cancellationToken) {
        try {
            IReadOnlyList<DaprComponentHealth> components = await adminApiClient.GetDaprComponentStatusAsync(cancellationToken).ConfigureAwait(false);
            return ToolHelper.SerializeResult(components);
        }
        catch (Exception ex) {
            return ToolHelper.HandleException(ex);
        }
    }
}
