
using System.Net;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

using Hexalith.EventStore.Admin.Abstractions.Models;
using Hexalith.EventStore.Admin.Abstractions.Security;

namespace Hexalith.EventStore.Admin.Mcp.Tools;
/// <summary>
/// Shared helper for JSON serialization and error handling across all MCP tools.
/// </summary>
internal static class ToolHelper {
    private const int MaxSanitizeDepth = 64;

    /// <summary>
    /// Shared JSON serializer options: camelCase, indented, enums as strings.
    /// </summary>
    internal static JsonSerializerOptions JsonOptions { get; } = new() {
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
        => JsonSerializer.Serialize(SanitizeNode(JsonSerializer.SerializeToNode(data, JsonOptions), "mcp-result", propertyName: null, depth: 0), JsonOptions);

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
        => SerializeResult(new {
            preview = true,
            action,
            description = SafeText(description, "Preview description redacted."),
            endpoint = SafeText(endpoint, "Preview endpoint redacted."),
            parameters,
            warning = SafeText(warning, "Preview warning redacted."),
        });

    /// <summary>
    /// Serializes a standard error response to JSON.
    /// </summary>
    /// <param name="adminApiStatus">The error status category.</param>
    /// <param name="message">The error detail message.</param>
    /// <returns>A JSON string with error shape.</returns>
    internal static string SerializeError(string adminApiStatus, string message)
        => JsonSerializer.Serialize(new { error = true, adminApiStatus, message = SafeText(message, "Protected diagnostic text redacted.") }, JsonOptions);

    /// <summary>
    /// Validates that required path-segment parameters are non-empty.
    /// Returns an error JSON string if any parameter is empty or whitespace, otherwise <c>null</c>.
    /// </summary>
    /// <param name="parameters">Tuples of (value, parameterName) to validate.</param>
    /// <returns>An error JSON string, or <c>null</c> if all parameters are valid.</returns>
    internal static string? ValidateRequired(params (string value, string name)[] parameters) {
        foreach ((string value, string name) in parameters) {
            if (string.IsNullOrWhiteSpace(value)) {
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
    internal static string HandleException(Exception ex) => ex switch {
        HttpRequestException httpEx => HandleHttpException(httpEx),
        JsonException => SerializeError("server-error", "Server returned non-JSON response"),
        TaskCanceledException => SerializeError("timeout", "Request timed out after 10 seconds"),
        _ => SerializeError("server-error", "Unexpected Admin MCP tool error."),
    };

    /// <summary>
    /// Categorizes an <see cref="HttpRequestException"/> into a standard error response.
    /// </summary>
    /// <param name="ex">The HTTP request exception.</param>
    /// <returns>A structured error JSON string.</returns>
    internal static string HandleHttpException(HttpRequestException ex) => ex.StatusCode switch {
        HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden
            => SerializeError("unauthorized", "Token may be expired or invalid. Check EVENTSTORE_ADMIN_TOKEN."),
        HttpStatusCode.NotFound
            => SerializeError("not-found", "Requested Admin API resource was not found."),
        HttpStatusCode.Conflict
            => SerializeError("conflict", "Operation conflict. Inspect safe status fields or retry after refreshing state."),
        HttpStatusCode.UnprocessableEntity
            => SerializeError("invalid-operation", "Operation rejected by Admin API policy."),
        HttpStatusCode.ServiceUnavailable
            => SerializeError("service-unavailable", "Tenant service temporarily unavailable. Retry shortly."),
        not null when (int)ex.StatusCode >= 500
            => SerializeError("server-error", $"HTTP {(int)ex.StatusCode} {ex.StatusCode}"),
        null
            => SerializeError("unreachable", "Admin API is unreachable."),
        _
            => SerializeError("server-error", $"HTTP {(int)ex.StatusCode} {ex.StatusCode}"),
    };

    private static JsonNode? SanitizeNode(JsonNode? node, string stage, string? propertyName, int depth) {
        if (node is null) {
            return null;
        }

        if (depth >= MaxSanitizeDepth) {
            return JsonValue.Create("[redacted: maximum JSON depth exceeded]");
        }

        if (node is JsonObject obj) {
            var result = new JsonObject();
            foreach (KeyValuePair<string, JsonNode?> property in obj) {
                if (IsRawCapableProperty(property.Key)) {
                    result[ToDescriptorPropertyName(property.Key)] = CreateRedactedDescriptor(property.Key, stage);
                    continue;
                }

                result[property.Key] = SanitizeNode(property.Value, stage, property.Key, depth + 1);
            }

            return result;
        }

        if (node is JsonArray array) {
            var result = new JsonArray();
            foreach (JsonNode? item in array) {
                result.Add(SanitizeNode(item, stage, propertyName, depth + 1));
            }

            return result;
        }

        // Per D2: only apply marker-based string replacement when the immediate property key is in
        // the raw-capable list. Other string values are preserved verbatim so safe descriptor text
        // mentioning marker substrings (e.g., "connectionString" in operator guidance) is not corrupted.
        if (node is JsonValue value && value.TryGetValue(out string? text)) {
            if (propertyName is not null && IsRawCapableProperty(propertyName) && UnsafeMarkerDetection.ContainsUnsafeMarker(text)) {
                return AdminRedactedContent.DefaultPlaceholder;
            }

            return text;
        }

        return node.DeepClone();
    }

    private static JsonNode? CreateRedactedDescriptor(string propertyName, string stage)
        => JsonSerializer.SerializeToNode(
            AdminRedactedContent.Protected(
                contentKind: ToContentKind(propertyName),
                reasonCode: "protected-content-redacted",
                stage: stage,
                metadataVersion: null,
                retryable: false,
                permanent: false,
                safeNextAction: "Use safe descriptor metadata or inspect the Admin API protection status."),
            JsonOptions);

    private static string SafeText(string? value, string replacement)
        => string.IsNullOrEmpty(value)
            ? replacement
            : UnsafeMarkerDetection.ContainsUnsafeMarker(value) ? replacement : value;

    private static bool IsRawCapableProperty(string propertyName)
        => propertyName.Equals("payloadJson", StringComparison.OrdinalIgnoreCase)
        || propertyName.Equals("stateJson", StringComparison.OrdinalIgnoreCase)
        || propertyName.Equals("eventPayloadJson", StringComparison.OrdinalIgnoreCase)
        || propertyName.Equals("resultingStateJson", StringComparison.OrdinalIgnoreCase)
        || propertyName.Equals("oldValue", StringComparison.OrdinalIgnoreCase)
        || propertyName.Equals("newValue", StringComparison.OrdinalIgnoreCase)
        || propertyName.Equals("commandPayload", StringComparison.OrdinalIgnoreCase)
        || propertyName.Equals("providerMetadata", StringComparison.OrdinalIgnoreCase)
        || propertyName.Equals("providerPrivateMetadata", StringComparison.OrdinalIgnoreCase)
        || propertyName.Equals("stateStoreKey", StringComparison.OrdinalIgnoreCase)
        || propertyName.Equals("connectionString", StringComparison.OrdinalIgnoreCase)
        || propertyName.Equals("keyAlias", StringComparison.OrdinalIgnoreCase)
        || propertyName.Equals("rawResponseBody", StringComparison.OrdinalIgnoreCase)
        || propertyName.Equals("stackTrace", StringComparison.OrdinalIgnoreCase)
        || propertyName.Equals("exceptionText", StringComparison.OrdinalIgnoreCase);

    private static string ToContentKind(string propertyName)
        => propertyName.ToLowerInvariant() switch {
            "payloadjson" => "event-payload",
            "statejson" => "snapshot-state",
            "eventpayloadjson" => "event-payload",
            "resultingstatejson" => "reconstructed-state",
            "oldvalue" => "field-value",
            "newvalue" => "field-value",
            "commandpayload" => "command-payload",
            "providermetadata" or "providerprivatemetadata" => "provider-metadata",
            "statestorekey" => "state-store-key",
            "connectionstring" => "connection-string",
            "keyalias" => "key-alias",
            "rawresponsebody" => "api-response",
            "stacktrace" or "exceptiontext" => "exception-text",
            _ => "protected-content",
        };

    private static string ToDescriptorPropertyName(string propertyName)
        => propertyName.ToLowerInvariant() switch {
            "payloadjson" => "payload",
            "statejson" => "state",
            "eventpayloadjson" => "eventPayload",
            "resultingstatejson" => "resultingState",
            "oldvalue" => "oldContent",
            "newvalue" => "newContent",
            "commandpayload" => "payload",
            "providermetadata" or "providerprivatemetadata" => "metadata",
            "statestorekey" => "stateStoreKeyStatus",
            "connectionstring" => "connectionStatus",
            "keyalias" => "keyStatus",
            "rawresponsebody" => "responseStatus",
            "stacktrace" or "exceptiontext" => "diagnostic",
            _ => "redactedContent",
        };
}
