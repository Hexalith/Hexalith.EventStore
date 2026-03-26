namespace Hexalith.EventStore.Admin.Mcp.Tools;

using System.Net;
using System.Text.Json;
using System.Text.Json.Serialization;

/// <summary>
/// Shared helper for JSON serialization and error handling across all MCP tools.
/// </summary>
internal static class ToolHelper
{
    /// <summary>
    /// Shared JSON serializer options: camelCase, indented, enums as strings.
    /// </summary>
    internal static JsonSerializerOptions JsonOptions { get; } = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
    };

    /// <summary>
    /// Serializes a success result to JSON.
    /// </summary>
    /// <typeparam name="T">The result type.</typeparam>
    /// <param name="data">The data to serialize.</param>
    /// <returns>A JSON string.</returns>
    internal static string SerializeResult<T>(T data)
        => JsonSerializer.Serialize(data, JsonOptions);

    /// <summary>
    /// Serializes an approval gate preview response to JSON.
    /// </summary>
    /// <param name="action">The action identifier (e.g., "projection-pause").</param>
    /// <param name="description">A human-readable description of what the operation would do.</param>
    /// <param name="endpoint">The HTTP endpoint that would be called.</param>
    /// <param name="parameters">The operation parameters.</param>
    /// <param name="warning">Risk context specific to the operation.</param>
    /// <returns>A JSON string with the preview shape.</returns>
    internal static string SerializePreview(
        string action,
        string description,
        string endpoint,
        object parameters,
        string warning)
        => SerializeResult(new { preview = true, action, description, endpoint, parameters, warning });

    /// <summary>
    /// Serializes a standard error response to JSON.
    /// </summary>
    /// <param name="adminApiStatus">The error status category.</param>
    /// <param name="message">The error detail message.</param>
    /// <returns>A JSON string with error shape.</returns>
    internal static string SerializeError(string adminApiStatus, string message)
        => JsonSerializer.Serialize(new { error = true, adminApiStatus, message }, JsonOptions);

    /// <summary>
    /// Validates that required path-segment parameters are non-empty.
    /// Returns an error JSON string if any parameter is empty or whitespace, otherwise <c>null</c>.
    /// </summary>
    /// <param name="parameters">Tuples of (value, parameterName) to validate.</param>
    /// <returns>An error JSON string, or <c>null</c> if all parameters are valid.</returns>
    internal static string? ValidateRequired(params (string value, string name)[] parameters)
    {
        foreach ((string value, string name) in parameters)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return SerializeError("invalid-input", $"Parameter '{name}' is required and must not be empty.");
            }
        }

        return null;
    }

    /// <summary>
    /// Entry point for exception handling in all tool methods.
    /// Catches <see cref="HttpRequestException"/>, <see cref="TaskCanceledException"/>,
    /// and <see cref="JsonException"/>.
    /// </summary>
    /// <param name="ex">The exception.</param>
    /// <returns>A structured error JSON string.</returns>
    internal static string HandleException(Exception ex) => ex switch
    {
        HttpRequestException httpEx => HandleHttpException(httpEx),
        JsonException => SerializeError("server-error", "Server returned non-JSON response"),
        TaskCanceledException => SerializeError("timeout", "Request timed out after 10 seconds"),
        _ => SerializeError("server-error", ex.Message),
    };

    /// <summary>
    /// Categorizes an <see cref="HttpRequestException"/> into a standard error response.
    /// </summary>
    /// <param name="ex">The HTTP request exception.</param>
    /// <returns>A structured error JSON string.</returns>
    internal static string HandleHttpException(HttpRequestException ex) => ex.StatusCode switch
    {
        HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden
            => SerializeError("unauthorized", "Token may be expired or invalid. Check EVENTSTORE_ADMIN_TOKEN."),
        HttpStatusCode.NotFound
            => SerializeError("not-found", ex.Message),
        HttpStatusCode.Conflict
            => SerializeError("conflict", $"Operation conflict: {ex.Message}"),
        HttpStatusCode.UnprocessableEntity
            => SerializeError("invalid-operation", $"Operation rejected: {ex.Message}"),
        HttpStatusCode.ServiceUnavailable
            => SerializeError("service-unavailable", "Tenant service temporarily unavailable. Retry shortly."),
        not null when (int)ex.StatusCode >= 500
            => SerializeError("server-error", $"HTTP {(int)ex.StatusCode} {ex.StatusCode}"),
        null
            => SerializeError("unreachable", ex.Message),
        _
            => SerializeError("server-error", $"HTTP {(int)ex.StatusCode} {ex.StatusCode}"),
    };
}
