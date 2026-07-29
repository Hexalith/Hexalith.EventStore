using System.Net;
using System.Text;

namespace Hexalith.EventStore.Server.Tests.Projections;

internal sealed class ProjectionDispatchHttpMessageHandler(
    string responseJson,
    HttpStatusCode statusCode = HttpStatusCode.OK,
    IReadOnlyDictionary<string, string>? responsesByPath = null) : HttpMessageHandler {
    private readonly string _responseJson = responseJson;

    public int CallCount { get; private set; }

    public string? RequestJson { get; private set; }

    public Uri? RequestUri { get; private set; }

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken) {
        CallCount++;
        RequestUri = request.RequestUri;
        RequestJson = request.Content is null
            ? null
            : await request.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        string selectedResponse = responsesByPath?
            .FirstOrDefault(pair => request.RequestUri?.AbsolutePath.EndsWith(pair.Key, StringComparison.Ordinal) == true)
            .Value ?? _responseJson;
        return new HttpResponseMessage(statusCode) {
            Content = new StringContent(selectedResponse, Encoding.UTF8, "application/json"),
        };
    }
}
