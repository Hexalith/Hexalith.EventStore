using Hexalith.EventStore.Client.Gateway;
using Hexalith.EventStore.Client.Streams;
using Hexalith.EventStore.Client.Events;
using Hexalith.EventStore.Client.Tests.Events;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Hexalith.EventStore.Contracts.Identity;
using Hexalith.EventStore.Contracts.Streams;
using NSubstitute;
using Shouldly;

namespace Hexalith.EventStore.Client.Tests.Streams;

/// <summary>Native Source Scanner Tests.</summary>
public sealed class NativeSourceScannerTests
{
    private static readonly AggregateIdentity Identity = new("tenant-a", "folders", "folder-a");

    /// <summary>Bounded Pages Fold Histories Beyond Complete Reader Bounds.</summary>
    [Theory]
    [InlineData(0)]
    [InlineData(10001)]
    [InlineData(32769)]
    public async Task BoundedPagesFoldHistoriesBeyondCompleteReaderBounds(int head)
    {
        var proof = Proof(head);
        var gateway = Substitute.For<IEventStoreGatewayClient>();
        gateway.ReadNativeSourceScanPageAsync(Arg.Any<StreamReadRequest>(), Arg.Any<CancellationToken>())
            .Returns(call => Page(call.Arg<StreamReadRequest>(), proof));
        long count = 0;
        var fold = Substitute.For<INativeSourceScanFold<long>>();
        fold.TryApply(Arg.Any<StreamReadEvent>()).Returns(call => { call.Arg<StreamReadEvent>().SequenceNumber.ShouldBe(++count); return true; });
        fold.Complete().Returns(_ => count);
        var result = await new NativeSourceScanner(gateway, TimeProvider.System).ScanAsync(Identity, () => fold, TestContext.Current.CancellationToken);
        result.ReasonCode.ShouldBeNull(); result.Value.ShouldBe(head); result.Observation!.Proof.ShouldBe(proof);
        gateway.ReceivedCalls().Count().ShouldBe(2 + (head + 254) / 255);
        await gateway.DidNotReceiveWithAnyArgs().ReadStreamAsync(default!);
    }

    /// <summary>Unverifiable Source Never Releases Selected Value.</summary>
    [Theory]
    [InlineData("missing-proof")]
    [InlineData("gap")]
    [InlineData("prefix")]
    [InlineData("unsupported")]
    [InlineData("foreign")]
    [InlineData("oversized")]
    public async Task UnverifiableSourceNeverReleasesSelectedValue(string scenario)
    {
        var proof = Proof(2); var gateway = Substitute.For<IEventStoreGatewayClient>();
        gateway.ReadNativeSourceScanPageAsync(Arg.Any<StreamReadRequest>(), Arg.Any<CancellationToken>()).Returns(call =>
        {
            var request = call.Arg<StreamReadRequest>(); var page = Page(request, proof);
            if (scenario == "missing-proof") return page with { Metadata = page.Metadata with { NativeProof = null } };
            if (scenario == "foreign") return page with { Tenant = "tenant-b" };
            if (request.ToSequence == 0) return page;
            if (scenario == "gap") return page with { Events = [Event(1), Event(3)] };
            if (scenario == "prefix") return page with { Events = [Event(1), Event(2) with { NativeEventDigest = new string('b', 64) }] };
            if (scenario == "oversized") return page with { Events = [Event(1) with { Payload = new byte[2_097_153] }, Event(2)] };
            return page;
        });
        var fold = Substitute.For<INativeSourceScanFold<string>>();
        fold.TryApply(Arg.Any<StreamReadEvent>()).Returns(scenario != "unsupported"); fold.Complete().Returns("secret");
        var result = await new NativeSourceScanner(gateway, TimeProvider.System).ScanAsync(Identity, () => fold, TestContext.Current.CancellationToken);
        result.Observation.ShouldBeNull(); result.Value.ShouldBeNull(); result.ReasonCode.ShouldNotBeNull();
        fold.DidNotReceive().Complete();
    }

    /// <summary>Growth Discards Partial Fold And Starts Fresh.</summary>
    [Fact]
    public async Task GrowthDiscardsPartialFoldAndStartsFresh()
    {
        var gateway = Substitute.For<IEventStoreGatewayClient>(); var one = Proof(1); var two = Proof(2); int calls = 0;
        gateway.ReadNativeSourceScanPageAsync(Arg.Any<StreamReadRequest>(), Arg.Any<CancellationToken>())
            .Returns(call => Page(call.Arg<StreamReadRequest>(), ++calls <= 2 ? one : two));
        int created = 0; long count = 0;
        var result = await new NativeSourceScanner(gateway, TimeProvider.System).ScanAsync(Identity, () =>
        {
            created++; count = 0; var fold = Substitute.For<INativeSourceScanFold<long>>();
            fold.TryApply(Arg.Any<StreamReadEvent>()).Returns(_ => { count++; return true; }); fold.Complete().Returns(_ => count); return fold;
        }, TestContext.Current.CancellationToken);
        result.Value.ShouldBe(2); result.Observation!.Proof.ShouldBe(two); created.ShouldBe(2);
    }

    /// <summary>Noncooperative Provider Cannot Release Evidence After Cancellation.</summary>
    [Fact]
    public async Task NoncooperativeProviderCannotReleaseEvidenceAfterCancellation()
    {
        var gateway = Substitute.For<IEventStoreGatewayClient>();
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource<StreamReadPage>(TaskCreationOptions.RunContinuationsAsynchronously);
        gateway.ReadNativeSourceScanPageAsync(Arg.Any<StreamReadRequest>(), Arg.Any<CancellationToken>()).Returns(_ => { entered.TrySetResult(); return release.Task; });
        using var caller = new CancellationTokenSource();
        var pending = new NativeSourceScanner(gateway, TimeProvider.System).ScanAsync(Identity, () => Substitute.For<INativeSourceScanFold<string>>(), caller.Token);
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        await caller.CancelAsync();
        await Should.ThrowAsync<OperationCanceledException>(async () => await pending);
        release.SetResult(Page(new(Identity.TenantId, Identity.Domain, Identity.AggregateId, ToSequence: 0, PageSize: 1), Proof(0)));
        await release.Task;
    }

    /// <summary>Every operation/page/resource input and the finite proof lifetime is independently bound.</summary>
    [Fact]
    public void ExactScanProofCannotAuthorizeChangedPagesScopesOrOrdinaryReadPurpose()
    {
        using var key = System.Security.Cryptography.RSA.Create(2048);
        string secret = Convert.ToBase64String(key.ExportPkcs8PrivateKey());
        string publicKey = Convert.ToBase64String(key.ExportSubjectPublicKeyInfo());
        var now = DateTimeOffset.UtcNow;
        var original = new StreamReadRequest("tenant-a", "folders", "folder-a", 0, 5, PageSize: 2) { RequireNativeProof = true };
        string proof = BoundNativeSourceScanProof.Issue(original, "folders", secret, now);
        BoundNativeSourceScanProof.Verify(proof, original, "folders", publicKey, now).ShouldBeTrue();
        foreach (var altered in new[] { original with { Tenant = "tenant-b" }, original with { Domain = "other-domain" },
            original with { AggregateId = "folder-b" }, original with { FromSequence = 1 }, original with { ToSequence = 6 },
            original with { PageSize = 3 }, original with { RequireNativeProof = false }, original with { ProjectionName = "other-projection" } })
            BoundNativeSourceScanProof.Verify(proof, altered, "folders", publicKey, now).ShouldBeFalse();
        BoundNativeSourceScanProof.Verify(proof, original, "unenrolled", publicKey, now).ShouldBeFalse();
        BoundNativeSourceScanProof.Verify(proof, original, "folders", publicKey, now.AddSeconds(30)).ShouldBeFalse();
        BoundNativeSourceScanProof.Verify(BoundStreamReadProof.Issue(original, "folders", secret, now), original, "folders", publicKey, now).ShouldBeFalse();
        typeof(NativeSourceScanResult<string>).GetConstructors().ShouldBeEmpty();
    }

    /// <summary>Even valid signatures cannot turn overflowing signed lifetime arithmetic into a permanent grant.</summary>
    [Theory]
    [InlineData(long.MinValue, long.MaxValue)]
    [InlineData(long.MinValue, 253402300799L)]
    [InlineData(-62135596800L, long.MaxValue)]
    [InlineData(long.MaxValue - 1, long.MaxValue)]
    public void SignedExtremeLifetimeIsRefused(long issued, long expires)
    {
        using RSA key = RSA.Create(2048);
        var request = new StreamReadRequest("tenant-a", "folders", "folder-a", 0, 0, PageSize: 1) { RequireNativeProof = true };
        var json = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        var payload = new NativeSourceScanProofPayload(1, EventStoreGatewayWorkloadOperations.NativeSourceScan, "folders",
            Convert.ToHexStringLower(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(request, json))), issued, expires, new string('a', 32));
        static string Encode(byte[] bytes) => Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
        string body = Encode(JsonSerializer.SerializeToUtf8Bytes(payload, json));
        string proof = body + "." + Encode(key.SignData(Encoding.ASCII.GetBytes(body), HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1));
        BoundNativeSourceScanProof.Verify(proof, request, "folders", Convert.ToBase64String(key.ExportSubjectPublicKeyInfo()), DateTimeOffset.UtcNow).ShouldBeFalse();
    }

    /// <summary>A real stored-version selector failure is a safe refusal with no selected value or observation.</summary>
    [Fact]
    public async Task UnsupportedStoredPayloadVersionRefusesWithoutReleasingSelectorFacts()
    {
        var gateway = Substitute.For<IEventStoreGatewayClient>();
        gateway.ReadNativeSourceScanPageAsync(Arg.Any<StreamReadRequest>(), Arg.Any<CancellationToken>()).Returns(call =>
        {
            var page = Page(call.Arg<StreamReadRequest>(), Proof(1));
            return page with { Events = page.Events.Select(source => source with { EventTypeName = typeof(LegacyTestEvent).FullName!,
                PayloadVersion = 999, Payload = "{\"Amount\":1}"u8.ToArray() }).ToArray() };
        });
        var registry = new EventPayloadEvolutionRegistry([typeof(LegacyTestEvent)], []);
        var fold = Substitute.For<INativeSourceScanFold<string>>();
        fold.TryApply(Arg.Any<StreamReadEvent>()).Returns(call =>
        {
            var source = call.Arg<StreamReadEvent>();
            _ = registry.Read(source.EventTypeName, source.PayloadVersion, source.Payload, source.SequenceNumber);
            return true;
        });
        var result = await new NativeSourceScanner(gateway, TimeProvider.System).ScanAsync(Identity, () => fold, TestContext.Current.CancellationToken);
        result.Value.ShouldBeNull(); result.Observation.ShouldBeNull(); result.ReasonCode.ShouldBe("source-unavailable");
        fold.DidNotReceive().Complete();
    }

    private static StreamReadEvent Event(long sequence) => new(sequence, "Example", [1], "json", 1,
        $"message-{sequence}", null, null, DateTimeOffset.UnixEpoch, null) { NativeEventDigest = new string('a', 64) };

    private static NativeSourceProof Proof(int head)
    {
        var proof = NativeSourceProofCodec.CreateSeed(Identity, "01ARZ3NDEKTSV4RRFFQ69G5FAV");
        for (int index = 1; index <= head; index++) proof = NativeSourceProofCodec.ExtendDigest(proof, Identity, index, $"message-{index}", new string('a', 64));
        return proof;
    }

    private static StreamReadPage Page(StreamReadRequest request, NativeSourceProof proof)
    {
        var events = request.ToSequence == 0 ? [] : Enumerable.Range(checked((int)request.FromSequence + 1),
            checked((int)Math.Min(request.PageSize, Math.Min(request.ToSequence!.Value, proof.HeadSequence) - request.FromSequence)))
            .Select(index => Event(index)).ToArray();
        return new(Identity.TenantId, Identity.Domain, Identity.AggregateId, events,
            new(request.FromSequence, request.ToSequence, events.LastOrDefault()?.SequenceNumber, proof.HeadSequence, events.Length,
                events.Length > 0 && events[^1].SequenceNumber < request.ToSequence, null) { NativeProof = proof });
    }
}
