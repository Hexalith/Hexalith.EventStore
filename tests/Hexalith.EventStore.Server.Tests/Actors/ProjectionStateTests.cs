
using System.Runtime.Serialization;
using System.Text.Json;

using Hexalith.EventStore.Server.Actors;

using Shouldly;

namespace Hexalith.EventStore.Server.Tests.Actors;

public class ProjectionStateTests {
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
