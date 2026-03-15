
using System.Diagnostics;

using Hexalith.EventStore.Server.Commands;
using Hexalith.EventStore.Server.Pipeline.Commands;

using Shouldly;

namespace Hexalith.EventStore.Server.Tests.Commands;

public class SubmitCommandExtensionsTests {
    private const string TestActivitySourceName = "Hexalith.EventStore.Tests.SubmitCommandExtensions";

    private static SubmitCommand CreateTestCommand(
        string userId = "test-user",
        Dictionary<string, string>? extensions = null) => new(
        MessageId: Guid.NewGuid().ToString(),
        Tenant: "test-tenant",
        Domain: "test-domain",
        AggregateId: "agg-001",
        CommandType: "CreateOrder",
        Payload: [1, 2, 3],
        CorrelationId: "corr-123",
        UserId: userId,
        Extensions: extensions);

    [Fact]
    public void ToCommandEnvelope_ValidCommand_MapsAllFields() {
        // Arrange
        var extensions = new Dictionary<string, string> { ["key"] = "value" };
        SubmitCommand command = CreateTestCommand(extensions: extensions);

        // Act
        var envelope = command.ToCommandEnvelope();

        // Assert
        envelope.TenantId.ShouldBe("test-tenant");
        envelope.Domain.ShouldBe("test-domain");
        envelope.AggregateId.ShouldBe("agg-001");
        envelope.CommandType.ShouldBe("CreateOrder");
        envelope.Payload.ShouldBe([1, 2, 3]);
        envelope.CorrelationId.ShouldBe("corr-123");
        _ = envelope.Extensions.ShouldNotBeNull();
        envelope.Extensions["key"].ShouldBe("value");
    }

    [Fact]
    public void ToCommandEnvelope_NullExtensions_MapsAsNull() {
        // Arrange
        SubmitCommand command = CreateTestCommand(extensions: null);

        // Act
        var envelope = command.ToCommandEnvelope();

        // Assert
        envelope.Extensions.ShouldBeNull();
    }

    [Fact]
    public void ToCommandEnvelope_CausationId_EqualsMessageId() {
        // Arrange
        SubmitCommand command = CreateTestCommand();

        // Act
        var envelope = command.ToCommandEnvelope();

        // Assert — CausationId is the MessageId of the originating SubmitCommand
        envelope.CausationId.ShouldBe(command.MessageId);
    }

    [Fact]
    public void ToCommandEnvelope_NullCommand_ThrowsArgumentNullException() =>
        // Act & Assert
        Should.Throw<ArgumentNullException>(
            () => SubmitCommandExtensions.ToCommandEnvelope(null!));

    [Fact]
    public void ToCommandEnvelope_UserId_MapsFromCommand() {
        // Arrange
        SubmitCommand command = CreateTestCommand(userId: "jwt-sub-user");

        // Act
        var envelope = command.ToCommandEnvelope();

        // Assert
        envelope.UserId.ShouldBe("jwt-sub-user");
    }

    [Fact]
    public void ToCommandEnvelope_WhenActivityCurrent_AddsTraceParentExtension() {
        // Arrange
        using var listener = new ActivityListener {
            ShouldListenTo = source => source.Name == TestActivitySourceName,
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded,
        };
        ActivitySource.AddActivityListener(listener);
        using var activitySource = new ActivitySource(TestActivitySourceName);

        SubmitCommand command = CreateTestCommand(extensions: null);

        // Act
        using Activity? activity = activitySource.StartActivity("test", ActivityKind.Internal);
        var envelope = command.ToCommandEnvelope();

        // Assert
        _ = envelope.Extensions.ShouldNotBeNull();
        envelope.Extensions.ContainsKey("traceparent").ShouldBeTrue();
        envelope.Extensions["traceparent"].ShouldNotBeNullOrWhiteSpace();
    }

    [Fact]
    public void ToCommandEnvelope_WhenExtensionAlreadyExists_OverwritesWithCurrentTraceContext() {
        // Arrange
        using var listener = new ActivityListener {
            ShouldListenTo = source => source.Name == TestActivitySourceName,
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded,
        };
        ActivitySource.AddActivityListener(listener);
        using var activitySource = new ActivitySource(TestActivitySourceName);

        var extensions = new Dictionary<string, string> {
            ["traceparent"] = "00-aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa-bbbbbbbbbbbbbbbb-01",
            ["custom"] = "value",
        };
        SubmitCommand command = CreateTestCommand(extensions: extensions);

        // Act
        using Activity? activity = activitySource.StartActivity("test", ActivityKind.Internal);
        var envelope = command.ToCommandEnvelope();

        // Assert
        _ = envelope.Extensions.ShouldNotBeNull();
        envelope.Extensions["custom"].ShouldBe("value");
        envelope.Extensions["traceparent"].ShouldBe(activity?.Id);
    }
}
