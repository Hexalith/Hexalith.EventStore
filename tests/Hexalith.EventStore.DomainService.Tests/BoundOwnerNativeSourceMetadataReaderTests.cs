using System.Security.Cryptography;
using Hexalith.EventStore.Contracts.Identity;
using Hexalith.EventStore.ServiceDefaults.Authentication;
using NSubstitute;
using Shouldly;

namespace Hexalith.EventStore.DomainService.Tests;

/// <summary>Exact owner proof, real SDK metadata shape, finite scope and failure controls.</summary>
public sealed class BoundOwnerNativeSourceMetadataReaderTests
{
    /// <summary>Verifies owner Metadata Uses Bound Sdk Transport And Never Falls Back.</summary>
    /// <param name="mode">The mode control.</param>
    [Theory]
    [InlineData("valid")]
    [InlineData("wrong-tenant")]
    [InlineData("missing-proof")]
    [InlineData("denied")]
    [InlineData("missing-assertion")]
    public async Task OwnerMetadataUsesBoundSdkTransportAndNeverFallsBack(string mode)
    {
        using RSA rsa = RSA.Create(2048);
        using var handler = new BoundOwnerMetadataCaptureHandler(Convert.ToBase64String(rsa.ExportSubjectPublicKeyInfo())) { Mode = mode };
        var factory = Substitute.For<IHttpClientFactory>();
        factory.CreateClient("owner-metadata").Returns(_ => new HttpClient(handler, disposeHandler: false)
            { BaseAddress = new Uri("http://127.0.0.1:3500/v1.0/invoke/eventstore/method/") });
        var issuer = Substitute.For<IWorkloadAssertionIssuer>();
        issuer.IssueAsync(Arg.Any<WorkloadAssertionRequest>(), Arg.Any<CancellationToken>())
            .Returns(new ValueTask<string?>(mode == "missing-assertion" ? null : "real-issuer-assertion"));
        var reader = new BoundOwnerNativeSourceMetadataReader(factory, "owner-metadata", issuer, "folders",
            Convert.ToBase64String(rsa.ExportPkcs8PrivateKey()), ["folders"], TimeProvider.System);
        var result = await reader.ReadAsync(new AggregateIdentity("tenant-one", "folders", "organization"), TestContext.Current.CancellationToken);
        if (mode == "valid") result.ShouldNotBeNull();
        else result.ShouldBeNull();
        await issuer.Received(1).IssueAsync(Arg.Is<WorkloadAssertionRequest>(request => request.Audience == "eventstore"
            && request.Operation == "eventstore:stream-read" && request.Bindings!.Count == 2
            && request.Bindings[EventStoreWorkloadAuthenticationDefaults.TenantBindingClaimType] == "tenant-one"
            && request.Bindings[EventStoreWorkloadAuthenticationDefaults.DomainBindingClaimType] == "folders"), Arg.Any<CancellationToken>());
        if (mode == "missing-assertion") handler.Calls.ShouldBe(0);
        else
        {
            handler.Calls.ShouldBe(1);
            handler.MetadataOnly.ShouldBeTrue();
            handler.ValidProof.ShouldBeTrue();
            handler.Assertion.ShouldBe("real-issuer-assertion");
            handler.Path.ShouldBe("/v1.0/invoke/eventstore/method/api/v1/streams/read/workload");
        }
        (await reader.ReadAsync(new AggregateIdentity("tenant-one", "foreign-domain", "organization"), TestContext.Current.CancellationToken)).ShouldBeNull();
        await issuer.Received(1).IssueAsync(Arg.Any<WorkloadAssertionRequest>(), Arg.Any<CancellationToken>());
        using var canceled = new CancellationTokenSource();
        canceled.Cancel();
        await Should.ThrowAsync<OperationCanceledException>(() => reader.ReadAsync(new AggregateIdentity("tenant-one", "folders", "organization"), canceled.Token));
    }
}
