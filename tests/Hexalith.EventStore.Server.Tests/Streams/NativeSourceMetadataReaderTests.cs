using System.Text.Json;
using Hexalith.EventStore.Client.Streams;
using Hexalith.EventStore.Contracts.Identity;
using Hexalith.EventStore.Server.Actors;
using Hexalith.EventStore.Server.Events;
using Microsoft.Extensions.Configuration;
using Shouldly;
namespace Hexalith.EventStore.Server.Tests.Streams;

public sealed class NativeSourceMetadataReaderTests
{
    [Fact]
    public async Task PersistedNativeEmptySeedRemainsDifferentFromMissingMetadata()
    {
        var identity = new AggregateIdentity("tenant-a", "conversations", "conversation-a");
        var proof = SourceProofCodec.CreateSeed(identity, "01ARZ3NDEKTSV4RRFFQ69G5FAV");
        var native = new SourceProofInspection(SourceProofInspectionStatus.Available, true, 0, false, proof);
        using var handler = new NativeSourceMetadataHttpHandler(JsonSerializer.SerializeToUtf8Bytes(native, JsonSerializerOptions.Web));
        using var http = new HttpClient(handler);
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["DAPR_HTTP_ENDPOINT"] = "http://localhost:3500" }).Build();
        var result = await new DaprNativeSourceMetadataReader(http, config, TimeProvider.System).ReadAsync(identity, TestContext.Current.CancellationToken);
        result.ShouldNotBeNull(); result.Proof.HeadSequence.ShouldBe(0); result.Proof.HeadMessageId.ShouldBeNull();
        result.Proof.CumulativeDigest.ShouldBe(proof.CumulativeDigest);
    }

    [Fact]
    public async Task ExistingActorWireProofIsReadWithoutEventsOrServerClientDependency()
    {
        var identity = new AggregateIdentity("tenant-a", "conversations", "conversation-a");
        var proof = Proof(identity);
        var native = new SourceProofInspection(SourceProofInspectionStatus.Available, true, 5, false, proof);
        using var handler = new NativeSourceMetadataHttpHandler(JsonSerializer.SerializeToUtf8Bytes(native, JsonSerializerOptions.Web));
        using var http = new HttpClient(handler);
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
            { ["DAPR_HTTP_ENDPOINT"] = "http://localhost:3500", ["DAPR_API_TOKEN"] = "test-sidecar-credential" }).Build();
        var result = await new DaprNativeSourceMetadataReader(http, config, TimeProvider.System)
            .ReadAsync(identity, TestContext.Current.CancellationToken);
        result.ShouldNotBeNull(); result.Proof.HeadSequence.ShouldBe(5);
        result.Proof.SourceIncarnation.ShouldBe(proof.SourceIncarnation); result.Proof.CumulativeDigest.ShouldBe(proof.CumulativeDigest);
        handler.LastUri!.AbsolutePath.ShouldEndWith("/method/InspectSourceProofAsync");
        handler.LastUri.AbsolutePath.ShouldNotContain("GetEvents"); handler.ApiToken.ShouldBe("test-sidecar-credential");
    }

    [Theory]
    [InlineData("absent")]
    [InlineData("unsupported")]
    [InlineData("erased")]
    [InlineData("wrong-tenant")]
    [InlineData("wrong-head")]
    [InlineData("oversized")]
    [InlineData("invalid-incarnation")]
    [InlineData("uppercase-digest")]
    [InlineData("invalid-message")]
    public async Task UnsupportedMalformedOrForeignNativeProofNeverBecomesAuthority(string change)
    {
        var identity = new AggregateIdentity("tenant-a", "conversations", "conversation-a");
        var proof = Proof(identity);
        var native = new SourceProofInspection(SourceProofInspectionStatus.Available, true, 5, false, proof);
        native = change switch
        {
            "absent" => native with { Exists = false, Proof = null },
            "unsupported" => native with { Status = SourceProofInspectionStatus.Unsupported },
            "erased" => native with { Erased = true },
            "wrong-tenant" => native with { Proof = proof with { TenantId = "tenant-b" } },
            "wrong-head" => native with { CurrentSequence = 6 },
            "invalid-incarnation" => native with { Proof = proof with { SourceIncarnation = "not-a-native-ulid" } },
            "uppercase-digest" => native with { Proof = proof with { CumulativeDigest = proof.CumulativeDigest.ToUpperInvariant() } },
            "invalid-message" => native with { Proof = proof with { HeadMessageId = "native\nmessage" } },
            _ => native,
        };
        byte[] body = change == "oversized" ? new byte[8193] : JsonSerializer.SerializeToUtf8Bytes(native, JsonSerializerOptions.Web);
        using var handler = new NativeSourceMetadataHttpHandler(body); using var http = new HttpClient(handler);
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["DAPR_HTTP_ENDPOINT"] = "http://localhost:3500" }).Build();
        (await new DaprNativeSourceMetadataReader(http, config, TimeProvider.System).ReadAsync(identity, TestContext.Current.CancellationToken)).ShouldBeNull();
    }
    [Theory]
    [InlineData("not-a-valid-uri")]
    [InlineData("file:///tmp/source")]
    [InlineData("http://user:password@localhost:3500")]
    public async Task MalformedOrUnsupportedSidecarEndpointFailsClosed(string endpoint)
    {
        using var handler = new NativeSourceMetadataHttpHandler([]); using var http = new HttpClient(handler);
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["DAPR_HTTP_ENDPOINT"] = endpoint }).Build();
        (await new DaprNativeSourceMetadataReader(http, config, TimeProvider.System).ReadAsync(
            new("tenant-a", "conversations", "conversation-a"), TestContext.Current.CancellationToken)).ShouldBeNull();
        handler.LastUri.ShouldBeNull();
    }

    private static SourceProof Proof(AggregateIdentity identity)
    {
        SourceProof proof = SourceProofCodec.CreateSeed(identity, "01ARZ3NDEKTSV4RRFFQ69G5FAV");
        for (long sequence = 1; sequence <= 5; sequence++)
        {
            var envelope = new EventEnvelope("native-message-" + sequence, identity.AggregateId, "Conversation", identity.TenantId,
                identity.Domain, sequence, sequence, DateTimeOffset.UnixEpoch, "correlation", "cause", "actor", "1.0.0",
                "NativeEvent", 1, "json", "{}"u8.ToArray(), null);
            proof = SourceProofCodec.Extend(proof, envelope);
        }
        SourceProofCodec.IsValid(proof, identity, 5, 1).ShouldBeTrue();
        return proof;
    }

}
