
using System.Runtime.Serialization;
using System.Text.Json;

using Hexalith.EventStore.Server.Actors;

using Shouldly;

namespace Hexalith.EventStore.Server.Tests.Actors;

public class QueryResultTests {
    [Fact]
    public void FromPayload_SetsPayloadBytesAndSuccess() {
        JsonElement payload = JsonDocument.Parse("{\"count\":42}").RootElement;

        var sut = QueryResult.FromPayload(payload);

        sut.Success.ShouldBeTrue();
        _ = sut.PayloadBytes.ShouldNotBeNull();
        sut.PayloadBytes!.Length.ShouldBeGreaterThan(0);
        sut.ErrorMessage.ShouldBeNull();
    }

    [Fact]
    public void GetPayload_ReturnsOriginalJsonStructure() {
        JsonElement payload = JsonDocument.Parse("{\"count\":42}").RootElement;

        var sut = QueryResult.FromPayload(payload);

        JsonElement restored = sut.GetPayload();
        restored.GetProperty("count").GetInt32().ShouldBe(42);
    }

    [Fact]
    public void Failure_SetsErrorMessageAndNoPayload() {
        var sut = QueryResult.Failure("Something went wrong");

        sut.Success.ShouldBeFalse();
        sut.PayloadBytes.ShouldBeNull();
        sut.ErrorMessage.ShouldBe("Something went wrong");
    }

    [Fact]
    public void GetPayload_NullPayloadBytes_ReturnsDefaultJsonElement() {
        var sut = new QueryResult(true);

        JsonElement result = sut.GetPayload();

        result.ValueKind.ShouldBe(JsonValueKind.Undefined);
    }

    [Fact]
    public void FromPayload_WithProjectionType_SetsProjectionType() {
        JsonElement payload = JsonDocument.Parse("{}").RootElement;

        var sut = QueryResult.FromPayload(payload, "counter");

        sut.ProjectionType.ShouldBe("counter");
    }

    [Fact]
    public void DataContractSerializer_RoundTrip_PreservesPayload() {
        // This is the critical regression test: DAPR actor remoting uses DataContractSerializer.
        // The old JsonElement-based QueryResult failed this serialization silently.
        JsonElement payload = JsonDocument.Parse("{\"count\":42,\"name\":\"test\"}").RootElement;
        var original = QueryResult.FromPayload(payload, "counter");

        // Serialize with DataContractSerializer (same as DAPR actor remoting)
        var serializer = new DataContractSerializer(typeof(QueryResult));
        using var stream = new MemoryStream();
        serializer.WriteObject(stream, original);

        // Deserialize
        stream.Position = 0;
        var deserialized = (QueryResult?)serializer.ReadObject(stream);

        _ = deserialized.ShouldNotBeNull();
        deserialized!.Success.ShouldBeTrue();
        deserialized.ProjectionType.ShouldBe("counter");

        JsonElement restored = deserialized.GetPayload();
        restored.GetProperty("count").GetInt32().ShouldBe(42);
        restored.GetProperty("name").GetString().ShouldBe("test");
    }

    [Fact]
    public void DataContractSerializer_RoundTrip_Failure_PreservesErrorMessage() {
        var original = QueryResult.Failure("No projection state");

        var serializer = new DataContractSerializer(typeof(QueryResult));
        using var stream = new MemoryStream();
        serializer.WriteObject(stream, original);

        stream.Position = 0;
        var deserialized = (QueryResult?)serializer.ReadObject(stream);

        _ = deserialized.ShouldNotBeNull();
        deserialized!.Success.ShouldBeFalse();
        deserialized.ErrorMessage.ShouldBe("No projection state");
        deserialized.PayloadBytes.ShouldBeNull();
    }
}
