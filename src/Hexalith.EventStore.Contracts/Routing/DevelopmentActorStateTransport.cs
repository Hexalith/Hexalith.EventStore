namespace Hexalith.EventStore.Contracts.Routing;

/// <summary>Marks only the existing SDK Admin owner's read-only sidecar path; the global guard independently validates scope, coordinates and credential.</summary>
public static class DevelopmentActorStateTransport
{
    private static readonly HttpRequestOptionsKey<string> Owner = new("Hexalith.SDK.Admin.OwnerStateRead");

    /// <summary>Reads the SDK-owned request marker; it supplies no authority without independently configured coordinates and the actual receiver scope.</summary>
    public static string? GetOwner(HttpRequestMessage request)
    {
        ArgumentNullException.ThrowIfNull(request);
        return request.Options.TryGetValue(Owner, out string? owner) ? owner : null;
    }

    /// <summary>Sends an owner-state or metadata GET using the independently configured owner's token when private qualification is installed.</summary>
    public static Task<HttpResponseMessage> SendAsync(HttpClient client, HttpRequestMessage request, string ownerAppId, CancellationToken token)
    {
        ArgumentNullException.ThrowIfNull(client);
        ArgumentNullException.ThrowIfNull(request);
        ArgumentException.ThrowIfNullOrWhiteSpace(ownerAppId);
        request.Options.Set(Owner, ownerAppId);
        return client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, token);
    }
}
