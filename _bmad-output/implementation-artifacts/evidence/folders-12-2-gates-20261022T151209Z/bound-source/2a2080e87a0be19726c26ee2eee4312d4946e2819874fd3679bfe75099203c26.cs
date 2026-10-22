using System.Net;
using System.Text;

namespace Hexalith.EventStore.Server.Tests.Projections;

internal sealed class ProjectionDispatchHttpMessageHandler(
    string responseJson,
    HttpStatusCode statusCode = HttpStatusCode.OK,
    IReadOnlyDictionary<string, string>? responsesByPath = null,
    bool retainPartBodies = true) : HttpMessageHandler {
    private readonly string _responseJson = responseJson;

    public int CallCount { get; private set; }

    public string? RequestJson { get; private set; }

    public Uri? RequestUri { get; private set; }

    public List<(string Path, string? Body)> Requests { get; } = [];

    public int LargestPartBodyBytes { get; private set; }

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken) {
        CallCount++;
        RequestUri = request.RequestUri;
        if (!retainPartBodies && request.RequestUri?.AbsolutePath.EndsWith(
                "/project/rebuild/part/v1", StringComparison.Ordinal) == true) {
            byte[] body = request.Content is null ? []
                : await request.Content.ReadAsByteArrayAsync(cancellationToken).ConfigureAwait(false);
            LargestPartBodyBytes = Math.Max(LargestPartBodyBytes, body.Length);
            RequestJson = null;
        }
        else {
            RequestJson = request.Content is null
                ? null
                : await request.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        }
        Requests.Add((request.RequestUri?.AbsolutePath ?? string.Empty, RequestJson));
        string selectedResponse = responsesByPath?
            .FirstOrDefault(pair => request.RequestUri?.AbsolutePath.EndsWith(pair.Key, StringComparison.Ordinal) == true)
            .Value ?? _responseJson;
        return new HttpResponseMessage(statusCode) {
            Content = new StringContent(selectedResponse, Encoding.UTF8, "application/json"),
        };
    }
}
