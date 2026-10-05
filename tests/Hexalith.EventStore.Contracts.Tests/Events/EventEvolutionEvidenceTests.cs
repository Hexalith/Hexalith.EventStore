using System.Text.Json;

using Hexalith.EventStore.Contracts.Projections;
using Hexalith.EventStore.Contracts.Replay;

namespace Hexalith.EventStore.Contracts.Tests.Events;

/// <summary>Checks additive evolution transport names, null omission and preserved positional DTO APIs.</summary>
public sealed class EventEvolutionEvidenceTests
{
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web);

    /// <summary>Checks every additive replay and projection hint remains absent from legacy JSON.</summary>
    [Fact]
    public void LegacyReplayAndProjectionWireOmitAllEvolutionHints()
    {
        var replay = Replay();
        var projection = Projection();
        var (sequence, type, payload, format, metadata, message, correlation, causation) = replay;
        sequence.ShouldBe(1);
        type.ShouldBe("legacy");
        payload.ShouldBe([123, 125]);
        format.ShouldBe("json");
        metadata.ShouldBe(1);
        message.ShouldBe("message");
        correlation.ShouldBeNull();
        causation.ShouldBeNull();
        var (projectionType, projectionPayload, projectionFormat, projectionSequence, timestamp, projectionCorrelation, projectionMessage, user) = projection;
        projectionType.ShouldBe("legacy");
        projectionPayload.ShouldBe(payload);
        projectionFormat.ShouldBe("json");
        projectionSequence.ShouldBe(1);
        timestamp.ShouldBe(DateTimeOffset.UnixEpoch);
        projectionCorrelation.ShouldBe("correlation");
        projectionMessage.ShouldBe("message");
        user.ShouldBeNull();

        foreach (object value in new object[] { replay, projection, new ProjectionRequest("tenant", "d", "aggregate", [projection]) })
        {
            string json = JsonSerializer.Serialize(value, value.GetType(), Options);
            json.ShouldNotContain("storedEventContractType");
            json.ShouldNotContain("storedPayloadVersion");
            json.ShouldNotContain("effectivePayload");
            json.ShouldNotContain("storedDigest");
            json.ShouldNotContain("registryFingerprint");
            json.ShouldNotContain("isAdapted");
            json.ShouldNotContain("verifiedEffectiveEvents");
            json.ShouldNotContain("eventEvolutionProof");
        }
    }

    /// <summary>Checks exact stored/effective provenance names survive JSON while original payload evidence remains intact.</summary>
    [Fact]
    public void ReplayAndProjectionRoundTripStoredAndEffectiveProvenance()
    {
        ReplayEventEnvelope replay = Replay() with
        {
            StoredEventContractType = "evt", StoredPayloadVersion = 1, StoredSerializationFormat = "json",
            EffectiveEventContractType = "evt", EffectivePayloadVersion = 2, EffectiveSerializationFormat = "json",
            EffectivePayload = [1], StoredEventTypeName = "legacy", StoredDigest = new byte[32],
            RegistryFingerprint = new string('a', 64), IsAdapted = true,
        };
        string replayJson = JsonSerializer.Serialize(replay, Options);
        replayJson.ShouldContain("\"storedEventContractType\":\"evt\"");
        ReplayEventEnvelope replayCopy = JsonSerializer.Deserialize<ReplayEventEnvelope>(replayJson, Options)!;
        replayCopy.StoredPayloadVersion.ShouldBe(1);
        replayCopy.EffectivePayloadVersion.ShouldBe(2);
        replayCopy.Payload.ShouldBe([123, 125]);
        replayCopy.EffectivePayload.ShouldBe([1]);
        replayCopy.IsAdapted.ShouldBe(true);

        ProjectionEventDto projection = Projection() with
        {
            MetadataVersion = 2, StoredEventContractType = "evt", StoredPayloadVersion = 1,
            StoredSerializationFormat = "json", EffectiveEventContractType = "evt", EffectivePayloadVersion = 2,
            EffectiveSerializationFormat = "json", EffectivePayload = [1], StoredEventTypeName = "legacy",
            StoredDigest = new byte[32], RegistryFingerprint = new string('a', 64), IsAdapted = true,
        };
        ProjectionEventDto copy = JsonSerializer.Deserialize<ProjectionEventDto>(JsonSerializer.Serialize(projection, Options), Options)!;
        copy.MetadataVersion.ShouldBe(2);
        copy.StoredEventContractType.ShouldBe("evt");
        copy.StoredPayloadVersion.ShouldBe(1);
        copy.EffectivePayloadVersion.ShouldBe(2);
        copy.Payload.ShouldBe([123, 125]);
        copy.EffectivePayload.ShouldBe([1]);
    }

    private static ReplayEventEnvelope Replay() => new(1, "legacy", [123, 125], "json", 1, "message", null, null);

    private static ProjectionEventDto Projection() => new("legacy", [123, 125], "json", 1, DateTimeOffset.UnixEpoch, "correlation", "message");
}
