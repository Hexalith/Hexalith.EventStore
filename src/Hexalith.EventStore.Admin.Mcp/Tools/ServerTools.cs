namespace Hexalith.EventStore.Admin.Mcp.Tools;

using System.ComponentModel;
using System.Net;
using System.Reflection;
using System.Text.Json;

using ModelContextProtocol.Server;

/// <summary>
/// MCP tools for server connectivity and diagnostics.
/// </summary>
[McpServerToolType]
internal sealed class ServerTools
{
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
        CancellationToken cancellationToken)
    {
        try
        {
            var health = await adminApiClient.GetSystemHealthAsync(cancellationToken).ConfigureAwait(false);
            return JsonSerializer.Serialize(new
            {
                serverName = _serverName,
                serverVersion = _serverVersion,
                adminApiStatus = health is null ? "error" : "reachable",
                details = health is null ? (object)"Admin API returned empty health response" : health,
            }, ToolHelper.JsonOptions);
        }
        catch (HttpRequestException ex) when (ex.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
        {
            return JsonSerializer.Serialize(new
            {
                serverName = _serverName,
                serverVersion = _serverVersion,
                adminApiStatus = "unauthorized",
                details = "Token may be expired or invalid. Check EVENTSTORE_ADMIN_TOKEN.",
            }, ToolHelper.JsonOptions);
        }
        catch (HttpRequestException ex) when (ex.StatusCode is not null)
        {
            return JsonSerializer.Serialize(new
            {
                serverName = _serverName,
                serverVersion = _serverVersion,
                adminApiStatus = "error",
                details = $"HTTP {(int)ex.StatusCode} {ex.StatusCode}",
            }, ToolHelper.JsonOptions);
        }
        catch (HttpRequestException ex)
        {
            return JsonSerializer.Serialize(new
            {
                serverName = _serverName,
                serverVersion = _serverVersion,
                adminApiStatus = "unreachable",
                details = ex.Message,
            }, ToolHelper.JsonOptions);
        }
        catch (TaskCanceledException)
        {
            return JsonSerializer.Serialize(new
            {
                serverName = _serverName,
                serverVersion = _serverVersion,
                adminApiStatus = "unreachable",
                details = "Request timed out or was cancelled.",
            }, ToolHelper.JsonOptions);
        }
        catch (JsonException ex)
        {
            return JsonSerializer.Serialize(new
            {
                serverName = _serverName,
                serverVersion = _serverVersion,
                adminApiStatus = "error",
                details = $"Invalid response from Admin API: {ex.Message}",
            }, ToolHelper.JsonOptions);
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
        CancellationToken cancellationToken)
    {
        try
        {
            var health = await adminApiClient.GetSystemHealthAsync(cancellationToken).ConfigureAwait(false);
            return health is null
                ? ToolHelper.SerializeError("not-found", "Admin API returned empty health response")
                : ToolHelper.SerializeResult(health);
        }
        catch (Exception ex)
        {
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
        CancellationToken cancellationToken)
    {
        try
        {
            var components = await adminApiClient.GetDaprComponentStatusAsync(cancellationToken).ConfigureAwait(false);
            return ToolHelper.SerializeResult(components);
        }
        catch (Exception ex)
        {
            return ToolHelper.HandleException(ex);
        }
    }
}
