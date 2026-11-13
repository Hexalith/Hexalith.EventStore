using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

using Hexalith.EventStore.Contracts.Routing;
using Hexalith.EventStore.ServiceDefaults.Routing;

namespace Hexalith.EventStore.Server.Tests.Routing;

/// <summary>Supplies test-owned fresh HTTP state bodies and exact ETag writes to the real Dapr catalog store.</summary>
internal sealed class ReleaseBindingHttpHandler(RouteCatalogActivationState initial) : HttpMessageHandler
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private int _version = 1;

    /// <summary>Gets or sets the current backend state, independently serialized for every actual GET.</summary>
    internal RouteCatalogActivationState State { get; set; } = initial;
    /// <summary>Gets or sets whether subsequent operations belong to completed-lease release.</summary>
    internal bool Releasing { get; set; }
    /// <summary>Gets or sets whether scopes are retained for assertions; benchmarks retain only numeric counts.</summary>
    internal bool RecordScopes { get; set; } = true;
    /// <summary>Gets or sets one first-write conflict or response-loss behavior.</summary>
    internal string? Fault { get; set; }
    /// <summary>Gets or sets a callback immediately before a fresh release body is produced.</summary>
    internal Action? BeforeReleaseRead { get; set; }
    /// <summary>Gets the number of fresh release reads, including actual 500 readbacks.</summary>
    internal int ReleaseReads { get; private set; }
    /// <summary>Gets the number of attempted release writes.</summary>
    internal int ReleaseWrites { get; private set; }
    /// <summary>Gets actual fresh-read computation contexts; parent authority contexts must never leak here.</summary>
    internal List<(bool Release, RouteCatalogBindingScope? Binding, RouteCatalogDecodeScope? Decode, RouteCatalogImageDigestScope? Image)> Reads { get; } = [];
    /// <summary>Gets actual write ETags and contexts after the real store finished binding validation.</summary>
    internal List<(string ETag, RouteCatalogBindingScope? Binding, RouteCatalogImageDigestScope? Image)> Writes { get; } = [];

    /// <inheritdoc/>
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        if (request.Method == HttpMethod.Get)
        {
            if (request.RequestUri?.Query != "?consistency=strong") throw new InvalidOperationException("A release must preserve the fresh strong state read.");
            if (Releasing) { ReleaseReads++; BeforeReleaseRead?.Invoke(); }
            if (RecordScopes) Reads.Add((Releasing, RouteCatalogBindingScope.Current, RouteCatalogDecodeScope.Current, RouteCatalogImageDigestScope.Current));
            var response = new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent.Create(State, options: JsonOptions) };
            response.Headers.TryAddWithoutValidation("ETag", "\"" + _version.ToString(System.Globalization.CultureInfo.InvariantCulture) + "\"");
            return response;
        }
        if (request.Method != HttpMethod.Post) throw new InvalidOperationException("Unexpected catalog state method.");
        using JsonDocument document = JsonDocument.Parse(await request.Content!.ReadAsByteArrayAsync(cancellationToken));
        JsonElement write = document.RootElement[0];
        string etag = write.GetProperty("etag").GetString()!;
        if (RecordScopes && Releasing) Writes.Add((etag, RouteCatalogBindingScope.Current, RouteCatalogImageDigestScope.Current));
        if (etag != _version.ToString(System.Globalization.CultureInfo.InvariantCulture)) return new(HttpStatusCode.Conflict);
        if (write.GetProperty("options").GetProperty("concurrency").GetString() != "first-write"
            || write.GetProperty("options").GetProperty("consistency").GetString() != "strong")
            throw new InvalidOperationException("The original ETag/CAS options changed.");
        if (Releasing) ReleaseWrites++;
        if (Releasing && ReleaseWrites == 1)
        {
            if (Fault == "conflict") { _version++; return new(HttpStatusCode.Conflict); }
            if (Fault == "500-readback") { _version++; return new(HttpStatusCode.InternalServerError); }
            if (Fault == "500-no-change") return new(HttpStatusCode.InternalServerError);
            if (Fault == "loss-before") throw new HttpRequestException("Controlled response loss before persistence.");
        }
        State = write.GetProperty("value").Deserialize<RouteCatalogActivationState>(JsonOptions)!;
        _version++;
        if (Releasing && ReleaseWrites == 1 && Fault == "loss-after") throw new HttpRequestException("Controlled response loss after persistence.");
        return new(HttpStatusCode.NoContent);
    }
}
