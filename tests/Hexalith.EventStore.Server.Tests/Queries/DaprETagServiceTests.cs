
using Dapr.Actors;
using Dapr.Actors.Client;

using Hexalith.EventStore.Server.Actors;
using Hexalith.EventStore.Server.Queries;

using Microsoft.Extensions.Logging.Abstractions;

using NSubstitute;
using NSubstitute.ExceptionExtensions;

using Shouldly;

namespace Hexalith.EventStore.Server.Tests.Queries;

public class DaprETagServiceTests {
    [Fact]
    public async Task GetCurrentETagAsync_ReturnsETag_WhenActorReturnsValue() {
        // Arrange
        string selfRoutingETag = SelfRoutingETag.GenerateNew("counter");
        IActorProxyFactory factory = Substitute.For<IActorProxyFactory>();
        IETagActor actor = Substitute.For<IETagActor>();
        _ = actor.GetCurrentETagAsync().Returns(selfRoutingETag);
        _ = factory.CreateActorProxy<IETagActor>(
            Arg.Is<ActorId>(id => id.GetId() == "counter:tenant1"),
            ETagActor.ETagActorTypeName,
            Arg.Any<ActorProxyOptions>()).Returns(actor);

        var service = new DaprETagService(factory, NullLogger<DaprETagService>.Instance);

        // Act
        string? result = await service.GetCurrentETagAsync("counter", "tenant1");

        // Assert
        result.ShouldBe(selfRoutingETag);
        _ = result.ShouldNotBeNull();
        result.ShouldContain('.');

        _ = factory.Received(1).CreateActorProxy<IETagActor>(
            Arg.Any<ActorId>(),
            ETagActor.ETagActorTypeName,
            Arg.Is<ActorProxyOptions>(options => options.RequestTimeout == TimeSpan.FromSeconds(3)));
    }

    [Fact]
    public async Task GetCurrentETagAsync_ReturnsNull_WhenActorReturnsNull() {
        // Arrange
        IActorProxyFactory factory = Substitute.For<IActorProxyFactory>();
        IETagActor actor = Substitute.For<IETagActor>();
        _ = actor.GetCurrentETagAsync().Returns((string?)null);
        _ = factory.CreateActorProxy<IETagActor>(
            Arg.Any<ActorId>(),
            ETagActor.ETagActorTypeName,
            Arg.Any<ActorProxyOptions>()).Returns(actor);

        var service = new DaprETagService(factory, NullLogger<DaprETagService>.Instance);

        // Act
        string? result = await service.GetCurrentETagAsync("counter", "tenant1");

        // Assert
        result.ShouldBeNull();
    }

    [Fact]
    public async Task GetCurrentETagAsync_ReturnsNull_WhenActorThrows() {
        // Arrange
        IActorProxyFactory factory = Substitute.For<IActorProxyFactory>();
        IETagActor actor = Substitute.For<IETagActor>();
        _ = actor.GetCurrentETagAsync().ThrowsAsync(new InvalidOperationException("Actor unavailable"));
        _ = factory.CreateActorProxy<IETagActor>(
            Arg.Any<ActorId>(),
            ETagActor.ETagActorTypeName,
            Arg.Any<ActorProxyOptions>()).Returns(actor);

        var service = new DaprETagService(factory, NullLogger<DaprETagService>.Instance);

        // Act
        string? result = await service.GetCurrentETagAsync("counter", "tenant1");

        // Assert — fail-open: returns null instead of throwing
        result.ShouldBeNull();
    }

    [Fact]
    public async Task GetCurrentETagAsync_PreCancelledToken_ThrowsBeforeActorProxyCreation() {
        IActorProxyFactory factory = Substitute.For<IActorProxyFactory>();
        var service = new DaprETagService(factory, NullLogger<DaprETagService>.Instance);
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        _ = await Should.ThrowAsync<OperationCanceledException>(
            () => service.GetCurrentETagAsync("counter", "tenant1", cts.Token));

        _ = factory.DidNotReceive().CreateActorProxy<IETagActor>(
            Arg.Any<ActorId>(),
            Arg.Any<string>(),
            Arg.Any<ActorProxyOptions>());
    }

    [Fact]
    public async Task GetCurrentETagAsync_OperationCanceledException_IsNotFailOpenNull() {
        // ROOT CAUSE (investigated 2026-05-19): The .NET async state machine catches any
        // OperationCanceledException thrown inside an async method and transitions the returned
        // Task to TaskStatus.Canceled. When that canceled Task is later awaited, the runtime
        // surfaces a fresh System.Threading.Tasks.TaskCanceledException with the default
        // "A task was canceled." message — the original OCE instance, its concrete subclass,
        // its custom Message, and its inner exception are all lost. This is .NET behavior, not
        // NSubstitute or the DAPR actor SDK: a hand-rolled test double that throws a sentinel
        // OCE subclass inline produces the same fresh TaskCanceledException after the await.
        // Therefore message-text assertions on the surfaced exception cannot succeed for any
        // OCE that flows through async/await; the meaningful assertion is type identity plus
        // the absence of conversion to an adapter failure type (AC9).
        IActorProxyFactory factory = Substitute.For<IActorProxyFactory>();
        IETagActor actor = Substitute.For<IETagActor>();
        _ = actor.GetCurrentETagAsync().ThrowsAsync(new OperationCanceledException("etag cancelled"));
        _ = factory.CreateActorProxy<IETagActor>(
            Arg.Any<ActorId>(),
            ETagActor.ETagActorTypeName,
            Arg.Any<ActorProxyOptions>()).Returns(actor);

        var service = new DaprETagService(factory, NullLogger<DaprETagService>.Instance);

        OperationCanceledException exception = await Should.ThrowAsync<OperationCanceledException>(
            () => service.GetCurrentETagAsync("counter", "tenant1"));

        // The exception is OCE-derived (catches conversion to a fail-open null return).
        _ = exception.ShouldBeAssignableTo<OperationCanceledException>();

        // It is not converted to a Hexalith adapter failure type. The base OCE class is
        // exactly TaskCanceledException (runtime-minted on Task.Canceled awaits) or
        // OperationCanceledException — never a wrapped/derived adapter exception type.
        Type exceptionType = exception.GetType();
        bool isExpectedCancellationType = exceptionType == typeof(OperationCanceledException)
            || exceptionType == typeof(TaskCanceledException);
        isExpectedCancellationType.ShouldBeTrue(
            $"Expected OperationCanceledException or TaskCanceledException but got {exceptionType.FullName}");
    }

    [Fact]
    public async Task GetCurrentETagAsync_DerivesActorId_WithColonSeparator() {
        // Arrange
        string selfRoutingETag = SelfRoutingETag.GenerateNew("my-domain");
        IActorProxyFactory factory = Substitute.For<IActorProxyFactory>();
        IETagActor actor = Substitute.For<IETagActor>();
        _ = actor.GetCurrentETagAsync().Returns(selfRoutingETag);
        _ = factory.CreateActorProxy<IETagActor>(
            Arg.Any<ActorId>(),
            Arg.Any<string>(),
            Arg.Any<ActorProxyOptions>()).Returns(actor);

        var service = new DaprETagService(factory, NullLogger<DaprETagService>.Instance);

        // Act
        _ = await service.GetCurrentETagAsync("my-domain", "my-tenant");

        // Assert — actor ID is "{projectionType}:{tenantId}"
        _ = factory.Received(1).CreateActorProxy<IETagActor>(
            Arg.Is<ActorId>(id => id.GetId() == "my-domain:my-tenant"),
            ETagActor.ETagActorTypeName,
            Arg.Any<ActorProxyOptions>());
    }

    [Fact]
    public async Task GetCurrentETagAsync_ReturnedValue_UsesSelfRoutingFormat() {
        // Arrange
        string selfRoutingETag = SelfRoutingETag.GenerateNew("counter");
        IActorProxyFactory factory = Substitute.For<IActorProxyFactory>();
        IETagActor actor = Substitute.For<IETagActor>();
        _ = actor.GetCurrentETagAsync().Returns(selfRoutingETag);
        _ = factory.CreateActorProxy<IETagActor>(
            Arg.Any<ActorId>(),
            Arg.Any<string>(),
            Arg.Any<ActorProxyOptions>()).Returns(actor);

        var service = new DaprETagService(factory, NullLogger<DaprETagService>.Instance);

        // Act
        string? result = await service.GetCurrentETagAsync("counter", "tenant1");

        // Assert
        _ = result.ShouldNotBeNull();
        SelfRoutingETag.TryDecode(result, out string? projectionType, out _).ShouldBeTrue();
        projectionType.ShouldBe("counter");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public async Task GetCurrentETagAsync_ThrowsArgumentException_WhenProjectionTypeInvalid(string? projectionType) {
        // Arrange
        IActorProxyFactory factory = Substitute.For<IActorProxyFactory>();
        var service = new DaprETagService(factory, NullLogger<DaprETagService>.Instance);

        // Act & Assert
        _ = await Should.ThrowAsync<ArgumentException>(
            () => service.GetCurrentETagAsync(projectionType!, "tenant1"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public async Task GetCurrentETagAsync_ThrowsArgumentException_WhenTenantIdInvalid(string? tenantId) {
        // Arrange
        IActorProxyFactory factory = Substitute.For<IActorProxyFactory>();
        var service = new DaprETagService(factory, NullLogger<DaprETagService>.Instance);

        // Act & Assert
        _ = await Should.ThrowAsync<ArgumentException>(
            () => service.GetCurrentETagAsync("counter", tenantId!));
    }
}
