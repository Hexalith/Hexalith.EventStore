using System.Text.Json;

using Hexalith.EventStore.Contracts.Identity;
using Hexalith.EventStore.Client.Projections;

using Hexalith.EventStore.Server.Events;

using NSubstitute;

using Shouldly;

namespace Hexalith.EventStore.Server.Tests.Events;

/// <summary>Published shape and persisted-state compatibility of aggregate metadata.</summary>
public sealed class AggregateMetadataTests
{
    private static readonly AggregateIdentity SourceIdentity = new("tenant-a", "counter", "aggregate-1");
    private const string Incarnation = "01ARZ3NDEKTSV4RRFFQ69G5FAV";

    [Fact]
    public void SourceV1GoldenVectorMatchesApprovedBytes()
    {
        SourceProof seed = SourceProofCodec.CreateSeed(SourceIdentity, Incarnation);
        EventEnvelope first = SourceEvent(1);
        EventEnvelope second = SourceEvent(2);

        seed.CumulativeDigest.ShouldBe("30f9f4f61a5a25d9ec628880b34d5d6c96e046362034ce02dc15bcaef367f5da");
        SourceProofCodec.ComputeEventHash(first).ShouldBe("1c3bd96cc9af2f16a0ea23426ab177bb4d63b659d0047ee1deb850d36100cd6b");
        SourceProof one = SourceProofCodec.Extend(seed, first);
        one.CumulativeDigest.ShouldBe("62e3e50f78399c37449cfc3b04776579ad58e348914bed344d4951205d299fa2");
        SourceProofCodec.ComputeEventHash(second).ShouldBe("08998bcf008547ee819b50d51c5855039ebacf5cf0527c1e458975934094b0a5");
        SourceProofCodec.Extend(one, second).CumulativeDigest.ShouldBe("6c18fec56e7c7bc1ba0c4eb760c12a09b2b976ce1fd2887f3d5bc32220a0c636");
        Should.Throw<ArgumentException>(() => SourceProofCodec.Extend(seed, second));
        Should.Throw<ArgumentException>(() => SourceProofCodec.Extend(one, first));
    }

    [Fact]
    public void SourceV1BindsEveryStoredFieldAndDistinguishesAbsence()
    {
        EventEnvelope original = SourceEvent(1);
        EventEnvelope[] changes =
        [
            original with { MessageId = "other" }, original with { AggregateId = "other" },
            original with { AggregateType = "other" }, original with { TenantId = "other" },
            original with { Domain = "other" }, original with { SequenceNumber = 2 },
            original with { GlobalPosition = 43 }, original with { Timestamp = original.Timestamp.AddTicks(1) },
            original with { Timestamp = original.Timestamp.ToOffset(TimeSpan.FromHours(1)) },
            original with { CorrelationId = "other" }, original with { CausationId = "other" },
            original with { UserId = "other" }, original with { DomainServiceVersion = "other" },
            original with { EventTypeName = "other" }, original with { MetadataVersion = 2 },
            original with { SerializationFormat = "other" }, original with { Payload = "{\"amount\":2}"u8.ToArray() },
            original with { Extensions = new Dictionary<string, string>() },
            original with { EventContractType = "" }, original with { PayloadVersion = 1 },
            original with { ApplicationPayloadDigest = "" },
        ];
        string expected = SourceProofCodec.ComputeEventHash(original);
        foreach (EventEnvelope changed in changes)
        {
            SourceProofCodec.ComputeEventHash(changed).ShouldNotBe(expected);
        }

        EventEnvelope ordered = original with { Extensions = new Dictionary<string, string> { ["a"] = "1", ["b"] = "2" } };
        EventEnvelope reversed = original with { Extensions = new Dictionary<string, string> { ["b"] = "2", ["a"] = "1" } };
        SourceProofCodec.ComputeEventHash(ordered).ShouldBe(SourceProofCodec.ComputeEventHash(reversed));
        SourceProofCodec.ComputeEventHash(ordered with { Extensions = new Dictionary<string, string> { ["a"] = "2", ["b"] = "2" } })
            .ShouldNotBe(SourceProofCodec.ComputeEventHash(ordered));
        SourceProofCodec.ComputeEventHash(ordered with { Extensions = new Dictionary<string, string> { ["c"] = "1", ["b"] = "2" } })
            .ShouldNotBe(SourceProofCodec.ComputeEventHash(ordered));
    }

    [Fact]
    public void SourceV1RejectsDuplicateExtensionKeysBeforeExtendingTheChain()
    {
        KeyValuePair<string, string>[] entries = [new("duplicate", "one"), new("duplicate", "two")];
        IDictionary<string, string> extensions = Substitute.For<IDictionary<string, string>>();
        extensions.Count.Returns(entries.Length);
        extensions.GetEnumerator().Returns(_ => ((IEnumerable<KeyValuePair<string, string>>)entries).GetEnumerator());
        extensions.When(value => value.CopyTo(Arg.Any<KeyValuePair<string, string>[]>(), Arg.Any<int>()))
            .Do(call => entries.CopyTo(call.ArgAt<KeyValuePair<string, string>[]>(0), call.ArgAt<int>(1)));
        extensions.Select(static entry => entry.Key).ShouldBe(new[] { "duplicate", "duplicate" });
        SourceProof seed = SourceProofCodec.CreateSeed(SourceIdentity, Incarnation);
        using ProjectionDeliveryCostScope cost = ProjectionDeliveryCostDiagnostics.BeginScope();

        ArgumentException failure = Should.Throw<ArgumentException>(() => SourceProofCodec.Extend(seed,
            SourceEvent(1) with { Extensions = extensions }));

        failure.ParamName.ShouldBe("envelope");
        cost[ProjectionDeliveryCost.SourceChainExtensions].ShouldBe(0);
        seed.HeadSequence.ShouldBe(0);
        seed.SourceIncarnation.ShouldBe(Incarnation);
        seed.CumulativeDigest.ShouldBe("30f9f4f61a5a25d9ec628880b34d5d6c96e046362034ce02dc15bcaef367f5da");
    }

    [Fact]
    public void SourceProofRoundTripsWithPublishedMetadataShapeAndSurvivesRewrite()
    {
        SourceProof proof = SourceProofCodec.Extend(SourceProofCodec.CreateSeed(SourceIdentity, Incarnation), SourceEvent(1));
        var original = new AggregateMetadata(1, DateTimeOffset.UnixEpoch, null) { SourceProof = proof };
        AggregateMetadata rewritten = original with { LastModified = DateTimeOffset.UnixEpoch.AddDays(1), ETag = "new" };
        AggregateMetadata copy = JsonSerializer.Deserialize<AggregateMetadata>(
            JsonSerializer.Serialize(rewritten, JsonSerializerOptions.Web), JsonSerializerOptions.Web)!;
        copy.SourceProof.ShouldBe(proof);
        (long sequence, _, _) = copy;
        sequence.ShouldBe(1);
    }

    private static EventEnvelope SourceEvent(int sequence) => new(
        sequence == 1 ? "01ARZ3NDEKTSV4RRFFQ69G5FAW" : "01ARZ3NDEKTSV4RRFFQ69G5FAX",
        "aggregate-1", "Counter", "tenant-a", "counter", sequence, 41 + sequence,
        new DateTimeOffset(639276192000000000, TimeSpan.Zero), "correlation-1", "causation-1", "user-1", "v1",
        "CounterIncremented", 1, "json", System.Text.Encoding.UTF8.GetBytes($"{{\"amount\":{sequence}}}"), null);

    /// <summary>The published three-parameter constructor and deconstruction remain available.</summary>
    [Fact]
    public void ThreeParameterShapeDefaultsRetainedFloorToOne()
    {
        var modified = new DateTimeOffset(2026, 9, 29, 8, 0, 0, TimeSpan.Zero);
        var metadata = new AggregateMetadata(5, modified, "etag-1");

        (long sequence, DateTimeOffset lastModified, string? etag) = metadata;

        metadata.RetainedFloor.ShouldBe(1);
        sequence.ShouldBe(5);
        lastModified.ShouldBe(modified);
        etag.ShouldBe("etag-1");
    }

    /// <summary>Actor-state JSON preserves a retained floor and defaults a legacy record to one.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void JsonRoundTripPreservesRetainedFloor(bool webDefaults)
    {
        JsonSerializerOptions options = webDefaults ? new(JsonSerializerDefaults.Web) : new();
        var metadata = new AggregateMetadata(9, DateTimeOffset.UnixEpoch, null, 8);

        AggregateMetadata copy = JsonSerializer.Deserialize<AggregateMetadata>(
            JsonSerializer.Serialize(metadata, options), options)!;
        AggregateMetadata legacy = JsonSerializer.Deserialize<AggregateMetadata>(
            webDefaults
                ? "{\"currentSequence\":3,\"lastModified\":\"1970-01-01T00:00:00+00:00\",\"eTag\":null}"
                : "{\"CurrentSequence\":3,\"LastModified\":\"1970-01-01T00:00:00+00:00\",\"ETag\":null}",
            options)!;

        copy.ShouldBe(metadata);
        legacy.RetainedFloor.ShouldBe(1);
        legacy.CurrentSequence.ShouldBe(3);
    }
}
