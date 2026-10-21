
using System.Runtime.Serialization;
using System.Text.Json;
using System.Security.Cryptography;

using Hexalith.EventStore.Contracts.Projections;

using Hexalith.EventStore.Server.Actors;

using Shouldly;

namespace Hexalith.EventStore.Server.Tests.Actors;

public class ProjectionStateTests {
    [Fact]
    public void OptionalDetail_LegacyJsonAndDataContractBytesRemainUnchanged()
    {
        byte[] bytes = "{\"count\":7}"u8.ToArray();
        var legacy = new LegacyProjectionState("counter", "tenant-a", bytes);
        var current = new ProjectionState("counter", "tenant-a", bytes);
        string legacyJson = JsonSerializer.Serialize(legacy);
        JsonSerializer.Serialize(current).ShouldBe(legacyJson);
        using var legacyWire = new MemoryStream();
        using var currentWire = new MemoryStream();
        new DataContractSerializer(typeof(LegacyProjectionState)).WriteObject(legacyWire, legacy);
        new DataContractSerializer(typeof(ProjectionState)).WriteObject(currentWire, current);
        currentWire.ToArray().ShouldBe(legacyWire.ToArray());
        legacyWire.Position = 0;
        var restored = (ProjectionState)new DataContractSerializer(typeof(ProjectionState)).ReadObject(legacyWire)!;
        restored.StateBytes.ShouldBe(bytes);
        restored.NotificationMetadata.ShouldBeNull();
        restored.NotificationGroupScope.ShouldBeNull();
    }

    [Fact]
    public void OptionalDetail_DataContractRoundTrip_PreservesBoundedHintsAndModelFingerprint()
    {
        using JsonDocument document = JsonDocument.Parse("""{"count":7}""");
        var metadata = Enumerable.Range(0, 20).ToDictionary(index => $"k{index:D2}", _ => new string('é', 100));
        var response = new ProjectionResponse("counter", document.RootElement.Clone())
        {
            NotificationGroupScope = "counter-status", NotificationMetadata = metadata,
        };
        var original = ProjectionState.FromProjectionResponse(response, "tenant-a");
        original.NotificationMetadata.ShouldNotBeNull().Count.ShouldBeLessThanOrEqualTo(16);
        JsonSerializer.SerializeToUtf8Bytes(original.NotificationMetadata).Length.ShouldBeLessThanOrEqualTo(2_048);
        original.NotificationMetadata.Count.ShouldBeLessThan(metadata.Count);
        metadata.Clear();
        original.NotificationMetadata.Count.ShouldBeGreaterThan(0, "the actor's bounded metadata must be an independent copy");
        byte[] fingerprint = SHA256.HashData(original.StateBytes);
        SHA256.HashData(ProjectionState.FromJsonElement("counter", "tenant-a", document.RootElement).StateBytes)
            .ShouldBe(fingerprint, "notification detail is not authoritative model content");
        using var wire = new MemoryStream();
        var serializer = new DataContractSerializer(typeof(ProjectionState));
        serializer.WriteObject(wire, original);
        wire.Position = 0;
        var restored = (ProjectionState)serializer.ReadObject(wire)!;
        restored.NotificationGroupScope.ShouldBe(original.NotificationGroupScope);
        restored.NotificationMetadata.ShouldBe(original.NotificationMetadata);
        SHA256.HashData(restored.StateBytes).ShouldBe(fingerprint);
        wire.Position = 0;
        var legacy = (LegacyProjectionState)new DataContractSerializer(typeof(LegacyProjectionState)).ReadObject(wire)!;
        legacy.StateBytes.ShouldBe(original.StateBytes, "an old actor reader must still consume the same model");
    }

    [Theory]
    [InlineData("forged:group")]
    [InlineData("aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa")]
    public void OptionalDetail_InvalidScopeIsRejectedBeforePersistence(string scope)
    {
        var state = new ProjectionState("counter", "tenant-a", "{}"u8.ToArray()) { NotificationGroupScope = scope };
        Should.Throw<ArgumentException>(() => state.WithBoundedNotification());
    }

    [Fact]
    public void OptionalDetail_BlankScopeAndEmptyMetadataPreserveExplicitDetailIntent()
    {
        var state = new ProjectionState("counter", "tenant-a", "{}"u8.ToArray())
        {
            NotificationGroupScope = "  ", NotificationMetadata = [],
        };
        ProjectionState normalized = state.WithBoundedNotification();
        normalized.NotificationGroupScope.ShouldBeNull();
        normalized.NotificationMetadata.ShouldNotBeNull().ShouldBeEmpty();
        normalized.ProjectionType.ShouldBe("counter");
        normalized.TenantId.ShouldBe("tenant-a");
    }

    [Fact]
    public void OptionalDetail_BlankScopeWithoutMetadata_RemainsSignalOnly()
    {
        var state = new ProjectionState("counter", "tenant-a", "{}"u8.ToArray()) { NotificationGroupScope = "  " };
        ProjectionState normalized = state.WithBoundedNotification();
        normalized.NotificationGroupScope.ShouldBeNull();
        normalized.NotificationMetadata.ShouldBeNull();
        JsonSerializer.Serialize(normalized).ShouldBe(JsonSerializer.Serialize(new ProjectionState("counter", "tenant-a", state.StateBytes)));
    }

    [Fact]
    public void FromJsonElement_CreatesStateWithSerializedBytes() {
        JsonElement state = JsonDocument.Parse("{\"count\":42}").RootElement;

        var sut = ProjectionState.FromJsonElement("counter", "tenant-a", state);

        sut.ProjectionType.ShouldBe("counter");
        sut.TenantId.ShouldBe("tenant-a");
        _ = sut.StateBytes.ShouldNotBeNull();
        sut.StateBytes.Length.ShouldBeGreaterThan(0);
    }

    [Fact]
    public void GetState_ReturnsOriginalJsonStructure() {
        JsonElement state = JsonDocument.Parse("{\"count\":42,\"active\":true}").RootElement;

        var sut = ProjectionState.FromJsonElement("counter", "tenant-a", state);

        JsonElement restored = sut.GetState();
        restored.GetProperty("count").GetInt32().ShouldBe(42);
        restored.GetProperty("active").GetBoolean().ShouldBeTrue();
    }

    [Fact]
    public void DataContractSerializer_RoundTrip_PreservesAllFields() {
        // Critical regression test: DAPR actor remoting uses DataContractSerializer.
        // The old JsonElement-based ProjectionState caused InvalidOperationException
        // when the ProjectionUpdateOrchestrator wrote state to the ProjectionActor.
        JsonElement state = JsonDocument.Parse("{\"count\":7,\"label\":\"test\"}").RootElement;
        var original = ProjectionState.FromJsonElement("counter", "tenant-a", state);

        var serializer = new DataContractSerializer(typeof(ProjectionState));
        using var stream = new MemoryStream();
        serializer.WriteObject(stream, original);

        stream.Position = 0;
        var deserialized = (ProjectionState?)serializer.ReadObject(stream);

        _ = deserialized.ShouldNotBeNull();
        deserialized!.ProjectionType.ShouldBe("counter");
        deserialized.TenantId.ShouldBe("tenant-a");

        JsonElement restored = deserialized.GetState();
        restored.GetProperty("count").GetInt32().ShouldBe(7);
        restored.GetProperty("label").GetString().ShouldBe("test");
    }

    [Fact]
    public void DataContractSerializer_RoundTrip_EmptyJsonObject() {
        JsonElement state = JsonDocument.Parse("{}").RootElement;
        var original = ProjectionState.FromJsonElement("empty", "t1", state);

        var serializer = new DataContractSerializer(typeof(ProjectionState));
        using var stream = new MemoryStream();
        serializer.WriteObject(stream, original);

        stream.Position = 0;
        var deserialized = (ProjectionState?)serializer.ReadObject(stream);

        _ = deserialized.ShouldNotBeNull();
        deserialized!.GetState().ValueKind.ShouldBe(JsonValueKind.Object);
    }
}
