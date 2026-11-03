using System.Net.Http.Headers;
using System.Text.Json;

using Hexalith.EventStore.Contracts.Routing;

namespace Hexalith.EventStore.ServiceDefaults.Routing;

/// <summary>Captures only the exact committed SDK content-free native metadata frame; possession grants no authority.</summary>
internal static class RouteCatalogMetadataRequest
{
    private const string Method = "api/v1/streams/read/workload";
    private const int MaximumBodyBytes = 32768;
    private static readonly HashSet<string> Fields = new(StringComparer.Ordinal)
    { "tenant", "domain", "aggregateId", "fromSequence", "toSequence", "checkpoint", "continuationToken", "pageSize", "projectionName", "requireNativeProof" };
    private static readonly HashSet<string> Headers = new(StringComparer.OrdinalIgnoreCase)
    { "Authorization", "dapr-app-id", "dapr-api-token", "X-Hexalith-Workload-Assertion", "X-Hexalith-Bound-Stream-Read-Proof", "X-Correlation-Id" };

    /// <summary>Requires one actual committed SDK gateway/action and the exact metadata method and verb.</summary>
    internal static bool IsGateway(CommittedRouteCatalogSnapshot snapshot, string appId, string method, HttpMethod verb)
    {
        if (method != Method || verb.Method != "POST" || snapshot.ActivationBindingDigest is null || snapshot.Envelope.SchemaVersion != 3) return false;
        var gateways = snapshot.TechnicalHttpReceivers.Where(receiver => receiver.FenceVersion == RouteCatalogTechnicalHttpPolicy.FenceVersion
            && receiver.Endpoints.Any(endpoint => endpoint.ActionIdentity.StartsWith("Hexalith.EventStore.Gateway,", StringComparison.Ordinal))).ToArray();
        if (gateways.Length != 1 || gateways[0].AppId != appId
            || gateways[0].Invocations?.Any(invocation => invocation.CallerAppId == snapshot.LocalAppId
                && invocation.Method == Method && invocation.HttpMethods.Contains("POST", StringComparer.Ordinal)) != true) return false;
        var actions = snapshot.AuxiliaryOperations.Where(action => action.Method == Method && action.Domain == "gateway"
            && action.Operation == "eventstore:stream-read" && action.DomainAuxiliary is null && action.Sidecar is null
            && action.Http is null && action.NativeActor is null && action.DaprCallback is null).ToArray();
        return actions.Length == 1 && actions[0].AppId == appId;
    }

    /// <summary>Freezes URI, finite needed headers and bounded exact body before asynchronous validation.</summary>
    internal static async Task<HttpRequestMessage?> CaptureAsync(HttpRequestMessage request, string appId, string method, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        if (request.RequestUri is not { IsAbsoluteUri: true } originalUri || request.Content is not { } content) return null;
        var uri = new Uri(originalUri.AbsoluteUri, UriKind.Absolute);
        var verb = request.Method;
        var version = request.Version;
        var versionPolicy = request.VersionPolicy;
        var headers = request.Headers.Where(header => Headers.Contains(header.Key)).Select(header =>
            new KeyValuePair<string, string[]>(header.Key, header.Value.ToArray())).ToArray();
        var contentType = content.Headers.ContentType?.ToString();
        if (content.Headers.ContentEncoding.Count != 0 || contentType is null
            || !MediaTypeHeaderValue.TryParse(contentType, out var type) || type.MediaType != "application/json"
            || type.CharSet is not (null or "utf-8" or "UTF-8")) return null;
        string path = uri.AbsolutePath;
        string expected = "/v1.0/invoke/" + Uri.EscapeDataString(appId) + "/method/" + method;
        var targets = headers.Where(header => header.Key.Equals("dapr-app-id", StringComparison.OrdinalIgnoreCase)).SelectMany(header => header.Value).ToArray();
        if (uri.Query.Length != 0 || uri.Fragment.Length != 0 || uri.UserInfo.Length != 0 || verb.Method != "POST"
            || !(path == expected && (targets.Length == 0 || targets.SequenceEqual([appId]))
                || path == "/" + method && targets.SequenceEqual([appId])))
            throw new RouteCatalogUnavailableException("The captured metadata target changed or is ambiguous.");
        await content.LoadIntoBufferAsync(MaximumBodyBytes, token).ConfigureAwait(false);
        byte[] body = await content.ReadAsByteArrayAsync(token).ConfigureAwait(false);
        token.ThrowIfCancellationRequested();
        if (body.Length is 0 or > MaximumBodyBytes) throw new RouteCatalogUnavailableException("Metadata request body is outside its bound.");
        using JsonDocument json = JsonDocument.Parse(body, new JsonDocumentOptions { MaxDepth = 16 });
        JsonElement root = json.RootElement;
        if (root.ValueKind != JsonValueKind.Object || root.EnumerateObject().Any(property => !Fields.Contains(property.Name))
            || root.EnumerateObject().GroupBy(property => property.Name, StringComparer.OrdinalIgnoreCase).Any(group => group.Count() != 1))
            throw new RouteCatalogUnavailableException("Metadata request fields are unknown or ambiguous.");
        bool Text(string name) => root.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String
            && value.GetString() is { Length: > 0 and <= 1024 } text && text == text.Trim() && !text.Any(char.IsControl);
        bool Zero(string name) => root.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.Number && value.TryGetInt64(out long number) && number == 0;
        bool Absent(string name) => !root.TryGetProperty(name, out var value) || value.ValueKind == JsonValueKind.Null;
        if (!Text("tenant") || !Text("domain") || !Text("aggregateId") || !Zero("fromSequence") || !Zero("toSequence")
            || !Absent("checkpoint") || !Absent("continuationToken") || !Absent("projectionName")
            || !root.TryGetProperty("requireNativeProof", out var proof) || proof.ValueKind != JsonValueKind.True
            || !root.TryGetProperty("pageSize", out var page) || page.ValueKind != JsonValueKind.Number || !page.TryGetInt32(out int size) || size is < 1 or > 255) return null;
        var copy = new HttpRequestMessage(verb, uri) { Version = version, VersionPolicy = versionPolicy, Content = new ByteArrayContent(body.ToArray()) };
        copy.Content.Headers.ContentType = MediaTypeHeaderValue.Parse(contentType);
        foreach (var header in headers)
        {
            if (header.Value.Length != 1 || !copy.Headers.TryAddWithoutValidation(header.Key, header.Value))
            { copy.Dispose(); throw new RouteCatalogUnavailableException("Metadata request credential headers are ambiguous."); }
        }
        return copy;
    }
}
