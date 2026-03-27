using System.Text.Json;

using Hexalith.EventStore.Admin.Abstractions.Models.Streams;

namespace Hexalith.EventStore.Admin.Abstractions.Tests.Models.Streams;

public class SandboxCommandRequestTests
{
    [Fact]
    public void Constructor_WithValidInputs_CreatesInstance()
    {
        var request = new SandboxCommandRequest(
            "Hexalith.EventStore.Sample.Counter.Commands.IncrementCounter",
            "{\"Amount\":5}",
            3,
            "corr-42",
            "user-1");

        request.CommandType.ShouldBe("Hexalith.EventStore.Sample.Counter.Commands.IncrementCounter");
        request.PayloadJson.ShouldBe("{\"Amount\":5}");
        request.AtSequence.ShouldBe(3);
        request.CorrelationId.ShouldBe("corr-42");
        request.UserId.ShouldBe("user-1");
    }

    [Fact]
    public void Constructor_WithNullCommandType_DefaultsToEmpty()
    {
        var request = new SandboxCommandRequest(
            null!,
            "{\"Amount\":5}",
            null,
            null,
            null);

        request.CommandType.ShouldBe(string.Empty);
        request.PayloadJson.ShouldBe("{\"Amount\":5}");
        request.AtSequence.ShouldBeNull();
        request.CorrelationId.ShouldBeNull();
        request.UserId.ShouldBeNull();
    }

    [Fact]
    public void ToString_RedactsPayloadJson()
    {
        var request = new SandboxCommandRequest(
            "IncrementCounter",
            "{\"Secret\":\"super-secret-value\"}",
            5,
            "corr-1",
            "user-1");

        string result = request.ToString();

        result.ShouldContain("[REDACTED]");
        result.ShouldContain("IncrementCounter");
        result.ShouldNotContain("super-secret-value");
        result.ShouldNotContain("Secret");
    }

    [Fact]
    public void SerializationRoundTrip_PreservesAllProperties()
    {
        var original = new SandboxCommandRequest(
            "IncrementCounter",
            "{\"Amount\":10}",
            7,
            "corr-99",
            "user-42");

        string json = JsonSerializer.Serialize(original);
        SandboxCommandRequest? deserialized = JsonSerializer.Deserialize<SandboxCommandRequest>(json);

        deserialized.ShouldNotBeNull();
        deserialized!.CommandType.ShouldBe("IncrementCounter");
        deserialized.PayloadJson.ShouldBe("{\"Amount\":10}");
        deserialized.AtSequence.ShouldBe(7);
        deserialized.CorrelationId.ShouldBe("corr-99");
        deserialized.UserId.ShouldBe("user-42");
    }
}
