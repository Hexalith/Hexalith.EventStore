using System.Net;
using System.Net.Http.Headers;
using System.Reflection;

namespace Hexalith.EventStore.Server.Tests.Projections;

/// <summary>Runs the installed real Dapr/gRPC request serializer, observing its protobuf object and actual rented send array.</summary>
internal sealed class ProofGrpcSendHandler(Action<object, ReadOnlyMemory<byte>> observe) : HttpMessageHandler
{
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token)
    {
        HttpContent content = request.Content ?? throw new InvalidOperationException("missing_grpc_content");
        object payload = content.GetType().GetField("_request", BindingFlags.NonPublic | BindingFlags.Instance)?.GetValue(content)
            ?? throw new InvalidOperationException("unobserved_grpc_request");
        using var stream = new ObservedGrpcWriteStream(buffer => observe(payload, buffer));
        await content.CopyToAsync(stream, token).ConfigureAwait(false);
        var response = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Version = HttpVersion.Version20,
            RequestMessage = request,
            Content = new ByteArrayContent([0, 0, 0, 0, 0]),
        };
        response.Content.Headers.ContentType = new MediaTypeHeaderValue("application/grpc");
        response.TrailingHeaders.TryAddWithoutValidation("grpc-status", "0");
        return response;
    }
}
