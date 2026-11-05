using System.Globalization;
using System.Text.Json;
using Aspire.Hosting.ApplicationModel;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Hexalith.EventStore.Aspire;

/// <summary>Observes a real selected native sidecar's initial SQLite resolver result without accessing or modifying the registry.</summary>
/// <remarks>This is initial startup history in the current AppHost's console stream, not a current-process,
/// ongoing membership or authorization assertion. Independent current process/socket/registry checks remain required.</remarks>
/// <param name="logs">The AppHost's genuine resource console stream.</param>
/// <param name="model">The actual generated resource model.</param>
/// <param name="resourceName">The selected executable resource name.</param>
/// <param name="appId">Its declared Dapr identity.</param>
internal sealed class DaprSqliteDiscoveryInitializationHealthCheck(
    ResourceLoggerService logs, DistributedApplicationModel model, string resourceName, string appId) : IHealthCheck
{
    /// <inheritdoc/>
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var resources = model.Resources.OfType<ExecutableResource>().Where(resource => resource.Name == resourceName).ToArray();
        if (resources.Length != 1) return HealthCheckResult.Unhealthy("NativeSqliteSidecarUnconfirmed");
        await foreach (var batch in logs.WatchAsync(resources[0]).WithCancellation(cancellationToken).ConfigureAwait(false))
        {
            foreach (var line in batch)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var fields = ParseNativeRecord(line.Content);
                if (fields is null || fields.GetValueOrDefault("app_id") != appId
                    || fields.GetValueOrDefault("scope") != "dapr.runtime" || fields.GetValueOrDefault("type") != "log") continue;
                if (fields.GetValueOrDefault("level") == "info" && fields.GetValueOrDefault("msg") == "Initialized name resolution to sqlite")
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    return HealthCheckResult.Healthy("NativeSqliteInitialRegistrationObserved");
                }
                if (fields.GetValueOrDefault("level") is "error" or "fatal"
                    && fields.GetValueOrDefault("msg") is { } message && message.Contains("[INIT_COMPONENT_FAILURE]", StringComparison.Ordinal)
                    && message.Contains("nr (sqlite/v1)", StringComparison.Ordinal))
                    return HealthCheckResult.Unhealthy("NativeSqliteInitializationFailed");
            }
        }
        return HealthCheckResult.Unhealthy("NativeSqliteInitializationUnconfirmed");
    }

    /// <summary>Reads bounded native JSON/logfmt fields without treating embedded application text as a native record.</summary>
    internal static Dictionary<string, string>? ParseNativeRecord(string text)
    {
        if (text.Length > 16384) return null;
        // ResourceLoggerService.CreateLogLines prefixes its actual native console entries with
        // Aspire's closed ConsoleLogsTimestampFormat before returning the observed content.
        int separator = text.IndexOf(' ');
        if (separator is > 0 and <= 33 && DateTimeOffset.TryParseExact(text[..separator],
            "yyyy-MM-dd'T'HH:mm:ss.fffffffK", CultureInfo.InvariantCulture, DateTimeStyles.None, out var timestamp)
            && timestamp.Offset == TimeSpan.Zero)
            text = text[(separator + 1)..];
        Dictionary<string, string> fields = new(StringComparer.Ordinal);
        try
        {
            if (text.StartsWith('{'))
            {
                using JsonDocument document = JsonDocument.Parse(text);
                foreach (var field in document.RootElement.EnumerateObject())
                    if (field.Value.ValueKind != JsonValueKind.String || !fields.TryAdd(field.Name, field.Value.GetString()!)) return null;
                return fields;
            }
            if (!text.StartsWith("time=", StringComparison.Ordinal)) return null;
            int offset = 0;
            while (offset < text.Length)
            {
                while (offset < text.Length && char.IsWhiteSpace(text[offset])) offset++;
                if (offset == text.Length) break;
                int start = offset;
                while (offset < text.Length && text[offset] != '=' && !char.IsWhiteSpace(text[offset])) offset++;
                if (offset == start || offset == text.Length || text[offset] != '=') return null;
                string key = text[start..offset];
                offset++;
                if (offset == text.Length) return null;
                start = offset;
                string value;
                if (text[offset] == '"')
                {
                    offset++;
                    bool closed = false;
                    while (offset < text.Length)
                    {
                        char item = text[offset++];
                        if (item == '\\') { if (offset == text.Length) return null; offset++; }
                        else if (item == '"') { closed = true; break; }
                    }
                    if (!closed) return null;
                    value = JsonSerializer.Deserialize<string>(text[start..offset])!;
                    if (offset < text.Length && !char.IsWhiteSpace(text[offset])) return null;
                }
                else
                {
                    while (offset < text.Length && !char.IsWhiteSpace(text[offset])) offset++;
                    value = text[start..offset];
                }
                if (!fields.TryAdd(key, value)) return null;
            }
            return fields;
        }
        catch (JsonException) { return null; }
        catch (InvalidOperationException) { return null; }
    }
}
