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
    [McpServerTool]
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
            });
        }
        catch (HttpRequestException ex) when (ex.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
        {
            return JsonSerializer.Serialize(new
            {
                serverName = _serverName,
                serverVersion = _serverVersion,
                adminApiStatus = "unauthorized",
                details = "Token may be expired or invalid. Check EVENTSTORE_ADMIN_TOKEN.",
            });
        }
        catch (HttpRequestException ex) when (ex.StatusCode is not null)
        {
            return JsonSerializer.Serialize(new
            {
                serverName = _serverName,
                serverVersion = _serverVersion,
                adminApiStatus = "error",
                details = $"HTTP {(int)ex.StatusCode} {ex.StatusCode}",
            });
        }
        catch (HttpRequestException ex)
        {
            return JsonSerializer.Serialize(new
            {
                serverName = _serverName,
                serverVersion = _serverVersion,
                adminApiStatus = "unreachable",
                details = ex.Message,
            });
        }
        catch (TaskCanceledException)
        {
            return JsonSerializer.Serialize(new
            {
                serverName = _serverName,
                serverVersion = _serverVersion,
                adminApiStatus = "unreachable",
                details = "Request timed out or was cancelled.",
            });
        }
        catch (JsonException ex)
        {
            return JsonSerializer.Serialize(new
            {
                serverName = _serverName,
                serverVersion = _serverVersion,
                adminApiStatus = "error",
                details = $"Invalid response from Admin API: {ex.Message}",
            });
        }
    }
}
