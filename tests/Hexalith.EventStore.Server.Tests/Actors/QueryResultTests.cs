
using System.Text.Json;

using Hexalith.EventStore.Server.Actors;

using Shouldly;

namespace Hexalith.EventStore.Server.Tests.Actors;

public class QueryResultTests {
    [Fact]
    public void Constructor_SuccessResult_SetsProperties() {
        JsonElement payload = JsonDocument.Parse("{\"count\":42}").RootElement;
        var sut = new QueryResult(true, payload);

        sut.Success.ShouldBeTrue();
        sut.Payload.GetProperty("count").GetInt32().ShouldBe(42);
        sut.ErrorMessage.ShouldBeNull();
    }

    [Fact]
    public void Constructor_FailedResult_SetsErrorMessage() {
        JsonElement payload = JsonDocument.Parse("{}").RootElement;
        var sut = new QueryResult(false, payload, "Something went wrong");

        sut.Success.ShouldBeFalse();
        sut.ErrorMessage.ShouldBe("Something went wrong");
    }

    [Fact]
    public void Constructor_DefaultErrorMessage_IsNull() {
        JsonElement payload = JsonDocument.Parse("{}").RootElement;
        var sut = new QueryResult(true, payload);

        sut.ErrorMessage.ShouldBeNull();
    }

    [Fact]
    public void Constructor_DefaultJsonElement_NoExceptionOnConstruction() {
        var sut = new QueryResult(true, default);

        sut.Success.ShouldBeTrue();
        sut.Payload.ValueKind.ShouldBe(JsonValueKind.Undefined);
    }

    [Fact]
    public void JsonRoundTrip_PreservesAllProperties() {
        JsonElement payload = JsonDocument.Parse("{\"count\":42}").RootElement;
        var original = new QueryResult(true, payload, null);

        string json = JsonSerializer.Serialize(original);
        QueryResult? deserialized = JsonSerializer.Deserialize<QueryResult>(json);

        deserialized.ShouldNotBeNull();
        deserialized.Success.ShouldBeTrue();
        deserialized.Payload.GetProperty("count").GetInt32().ShouldBe(42);
        deserialized.ErrorMessage.ShouldBeNull();
    }
}
