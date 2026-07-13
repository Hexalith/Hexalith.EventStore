using System.Net;
using System.Text;

namespace Hexalith.EventStore.Server.Tests.Projections;

internal sealed class ProjectionDispatchHttpMessageHandler(string responseJson) : HttpMessageHandler {
    private readonly string _responseJson = responseJson;

    public int CallCount { get; private set; }

    public string? RequestJson { get; private set; }

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken) {
        CallCount++;
        RequestJson = request.Content is null
            ? null
            : await request.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        return new HttpResponseMessage(HttpStatusCode.OK) {
            Content = new StringContent(_responseJson, Encoding.UTF8, "application/json"),
        };
    }
}
