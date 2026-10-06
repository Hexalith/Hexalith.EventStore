using System.Runtime.Serialization;
using System.Text.Json;
using System.Xml.Linq;

using Hexalith.EventStore.Contracts.Projections;
using Hexalith.EventStore.Contracts.Queries;

using Shouldly;

namespace Hexalith.EventStore.Contracts.Tests.Queries;

/// <summary>Preserves P1R signed query authority and exact projection positions across supported transports.</summary>
public sealed class P1RWireRegressionTests
{
    /// <summary>Neither supported serializer may drop original actor or signed admission evidence.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void QueryAuthoritySurvivesSupportedRoundTrip(bool dataContract)
    {
        var source = new QueryEnvelope("tenant-a", "counter", "counter-1", "get-counter", [], "corr", "user", null, false, null,
            originalActorId: "actor", authenticatedWorkloadId: "workload", isDelegated: true,
            scopes: ["counter.read"], audience: ["eventstore"], delegationId: "delegation")
        { IdentityAdmissionProof = "signed-admission-proof" };

        QueryEnvelope copy = RoundTrip(source, dataContract);

        copy.ShouldBe(source);
        copy.IdentityAdmissionProof.ShouldBe(source.IdentityAdmissionProof);
        copy.OriginalActorId.ShouldBe("actor");
        copy.AuthenticatedWorkloadId.ShouldBe("workload");
        copy.IsDelegated.ShouldBeTrue();
        copy.Scopes.ShouldBe(["counter.read"]);
        copy.Audience.ShouldBe(["eventstore"]);
    }

    /// <summary>A supported wire cannot turn position 987 into unknown or use aggregate sequence 12 instead.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ProjectionPosition987SurvivesSupportedRoundTrip(bool dataContract)
    {
        var source = new ProjectionEventDto("event", "{}"u8.ToArray(), "json", 12,
            DateTimeOffset.UnixEpoch, "corr", GlobalPosition: 987)
        { MetadataVersion = 2, StoredEventContractType = "counter.incremented", StoredPayloadVersion = 1,
          StoredSerializationFormat = "json", EffectiveEventContractType = "counter.incremented",
          EffectivePayloadVersion = 1, EffectiveSerializationFormat = "json", EffectivePayload = "{}"u8.ToArray(),
          StoredEventTypeName = "event", StoredDigest = [1, 2], RegistryFingerprint = "registry", IsAdapted = false };

        ProjectionEventDto copy = RoundTrip(source, dataContract);

        copy.GlobalPosition.ShouldBe(987);
        copy.SequenceNumber.ShouldBe(12);
        copy.Payload.ShouldBe(source.Payload);
        copy.MetadataVersion.ShouldBe(source.MetadataVersion);
        copy.StoredEventContractType.ShouldBe(source.StoredEventContractType);
        copy.StoredPayloadVersion.ShouldBe(source.StoredPayloadVersion);
        copy.StoredSerializationFormat.ShouldBe(source.StoredSerializationFormat);
        copy.EffectiveEventContractType.ShouldBe(source.EffectiveEventContractType);
        copy.EffectivePayloadVersion.ShouldBe(source.EffectivePayloadVersion);
        copy.EffectiveSerializationFormat.ShouldBe(source.EffectiveSerializationFormat);
        copy.EffectivePayload.ShouldBe(source.EffectivePayload);
        copy.StoredEventTypeName.ShouldBe(source.StoredEventTypeName);
        copy.StoredDigest.ShouldBe(source.StoredDigest);
        copy.RegistryFingerprint.ShouldBe(source.RegistryFingerprint);
        copy.IsAdapted.ShouldBe(source.IsAdapted);
    }

    /// <summary>Absence of a position on a compatible legacy wire remains unknown, never aggregate sequence.</summary>
    [Fact]
    public void LegacyProjectionDataContractWithoutPositionRemainsUnknown()
    {
        var source = new ProjectionEventDto("event", [], "json", 12, DateTimeOffset.UnixEpoch, "corr", GlobalPosition: 987);
        var serializer = new DataContractSerializer(typeof(ProjectionEventDto));
        using var stream = new MemoryStream();
        serializer.WriteObject(stream, source);
        stream.Position = 0;
        XDocument document = XDocument.Load(stream);
        document.Root!.Elements().Single(element => element.Name.LocalName == "GlobalPosition").Remove();
        using var reader = document.CreateReader();

        var copy = (ProjectionEventDto)serializer.ReadObject(reader)!;

        copy.SequenceNumber.ShouldBe(12);
        copy.GlobalPosition.ShouldBe(0);
    }

    private static T RoundTrip<T>(T source, bool dataContract)
    {
        if (!dataContract)
        {
            return JsonSerializer.Deserialize<T>(JsonSerializer.Serialize(source))!;
        }

        var serializer = new DataContractSerializer(typeof(T));
        using var stream = new MemoryStream();
        serializer.WriteObject(stream, source);
        stream.Position = 0;
        return (T)serializer.ReadObject(stream)!;
    }
}
