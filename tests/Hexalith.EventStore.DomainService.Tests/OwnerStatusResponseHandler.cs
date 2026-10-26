using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Hexalith.EventStore.Client.Gateway;
using Hexalith.EventStore.Contracts.Queries;
using Hexalith.EventStore.ServiceDefaults.Authentication;
namespace Hexalith.EventStore.DomainService.Tests;
internal sealed class OwnerStatusResponseHandler(HttpStatusCode status, TimeSpan? delay = null,
    TimeSpan? bodyDelay = null, TimeSpan? disposalDelay = null) : HttpMessageHandler
{
    internal BoundOwnerQueryRequest? Request { get; private set; }
    internal string? Proof { get; private set; }
    internal string? Assertion { get; private set; }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        if (delay is { } latency) await Task.Delay(latency, cancellationToken).ConfigureAwait(false);
        Request = await request.Content!.ReadFromJsonAsync<BoundOwnerQueryRequest>(cancellationToken).ConfigureAwait(false);
        Proof = request.Headers.GetValues(BoundOwnerQueryProof.HeaderName).Single();
        Assertion = request.Headers.GetValues(EventStoreWorkloadAuthenticationDefaults.AssertionHeaderName).Single();
        var response = new HttpResponseMessage(status);
        if (status == HttpStatusCode.OK)
        {
            var result = new SubmitQueryResponse("owner-corr", JsonSerializer.SerializeToElement(new { allowed = true }));
            response.Content = bodyDelay is not null || disposalDelay is not null
                ? new DelayedOwnerResponseContent(JsonSerializer.SerializeToUtf8Bytes(result, JsonSerializerOptions.Web), bodyDelay ?? TimeSpan.Zero, disposalDelay ?? TimeSpan.Zero)
                : JsonContent.Create(result);
        }
        return response;
    }
}
