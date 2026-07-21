using System.Net;
using System.Text.Json;

namespace Hexalith.EventStore.IntegrationTests.Security;

/// <summary>
/// Classifies disposable-topology writer-protocol activation responses.
/// </summary>
internal static class ProjectionDeliveryWriterProtocolCutoverPolicy {
    /// <summary>
    /// Returns whether an activation response is transient and must be retried.
    /// </summary>
    /// <param name="statusCode">The activation response status.</param>
    /// <returns><see langword="true"/> for request timeout, throttling, and server failures.</returns>
    internal static bool IsTransientActivationStatus(HttpStatusCode statusCode)
        => statusCode == HttpStatusCode.RequestTimeout
            || statusCode == HttpStatusCode.TooManyRequests
            || (int)statusCode >= 500;

    /// <summary>
    /// Returns whether the writer-protocol marker is the only unhealthy health check.
    /// </summary>
    /// <param name="healthBody">The EventStore health response.</param>
    /// <param name="writerProtocolHealthCheck">The marker health-check name.</param>
    /// <returns><see langword="true"/> when cutover is safe to attempt.</returns>
    internal static bool WriterProtocolIsOnlyUnhealthyCheck(
        string healthBody,
        string writerProtocolHealthCheck) {
        try {
            using JsonDocument document = JsonDocument.Parse(healthBody);
            if (!document.RootElement.TryGetProperty("results", out JsonElement results)
                || results.ValueKind != JsonValueKind.Object) {
                return false;
            }

            bool markerIsUnhealthy = false;
            foreach (JsonProperty result in results.EnumerateObject()) {
                if (!result.Value.TryGetProperty("status", out JsonElement statusElement)) {
                    return false;
                }

                string? status = statusElement.GetString();
                if (string.Equals(result.Name, writerProtocolHealthCheck, StringComparison.Ordinal)) {
                    markerIsUnhealthy = string.Equals(status, "Unhealthy", StringComparison.Ordinal);
                }
                else if (string.Equals(status, "Unhealthy", StringComparison.Ordinal)) {
                    return false;
                }
            }

            return markerIsUnhealthy;
        }
        catch (JsonException) {
            return false;
        }
    }
}
