using System.Net;
using System.Net.Http.Json;
using Hexalith.EventStore.Client.Gateway;
using Hexalith.EventStore.Client.Streams;
using Hexalith.EventStore.Contracts.Identity;
using Hexalith.EventStore.Contracts.Streams;

namespace Hexalith.EventStore.DomainService.Tests;

/// <summary>Captures only the test-owned metadata transport and exact owner proof.</summary>
/// <param name="publicKey">The test owner public verification key.</param>
internal sealed class BoundOwnerMetadataCaptureHandler(string publicKey) : HttpMessageHandler
{
    /// <summary>Gets or sets the calls control.</summary>
    internal int Calls { get; private set; }
    /// <summary>Gets or sets the valid proof control.</summary>
    internal bool ValidProof { get; private set; }
    /// <summary>Gets or sets the metadata only control.</summary>
    internal bool MetadataOnly { get; private set; }
    /// <summary>Gets or sets the path control.</summary>
    internal string? Path { get; private set; }
    /// <summary>Gets or sets the assertion control.</summary>
    internal string? Assertion { get; private set; }
    /// <summary>Gets or sets the mode control.</summary>
    internal string Mode { get; set; } = "valid";

    /// <inheritdoc/>
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        Calls++;
        Path = request.RequestUri!.AbsolutePath;
        Assertion = request.Headers.GetValues("X-Hexalith-Workload-Assertion").Single();
        var body = (await request.Content!.ReadFromJsonAsync<StreamReadRequest>(cancellationToken))!;
        MetadataOnly = body.FromSequence == 0 && body.ToSequence == 0 && body.PageSize == 1 && body.RequireNativeProof;
        ValidProof = BoundStreamReadProof.Verify(request.Headers.GetValues(BoundStreamReadProof.HeaderName).Single(), body,
            "folders", publicKey, DateTimeOffset.UtcNow);
        var identity = new AggregateIdentity(body.Tenant, body.Domain, body.AggregateId!);
        var proof = NativeSourceProofCodec.CreateSeed(identity, "01ARZ3NDEKTSV4RRFFQ69G5FAV");
        var page = new StreamReadPage(Mode == "wrong-tenant" ? "foreign" : body.Tenant, body.Domain, body.AggregateId, [],
            new(0, 0, null, 0, 0, false, null) { NativeProof = Mode == "missing-proof" ? null : proof });
        return new(Mode == "denied" ? HttpStatusCode.Forbidden : HttpStatusCode.OK) { Content = JsonContent.Create(page) };
    }
}
