
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
        IActorProxyFactory factory = Substitute.For<IActorProxyFactory>();
        IETagActor actor = Substitute.For<IETagActor>();
        _ = actor.GetCurrentETagAsync().Returns("abc123");
        _ = factory.CreateActorProxy<IETagActor>(
            Arg.Is<ActorId>(id => id.GetId() == "counter:tenant1"),
            ETagActor.ETagActorTypeName,
            Arg.Any<ActorProxyOptions>()).Returns(actor);

        var service = new DaprETagService(factory, NullLogger<DaprETagService>.Instance);

        // Act
        string? result = await service.GetCurrentETagAsync("counter", "tenant1");

        // Assert
        result.ShouldBe("abc123");

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
    public async Task GetCurrentETagAsync_DerivesActorId_WithColonSeparator() {
        // Arrange
        IActorProxyFactory factory = Substitute.For<IActorProxyFactory>();
        IETagActor actor = Substitute.For<IETagActor>();
        _ = actor.GetCurrentETagAsync().Returns("etag-val");
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

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public async Task GetCurrentETagAsync_ThrowsArgumentException_WhenProjectionTypeInvalid(string? projectionType) {
        // Arrange
        IActorProxyFactory factory = Substitute.For<IActorProxyFactory>();
        var service = new DaprETagService(factory, NullLogger<DaprETagService>.Instance);

        // Act & Assert
        await Should.ThrowAsync<ArgumentException>(
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
        await Should.ThrowAsync<ArgumentException>(
            () => service.GetCurrentETagAsync("counter", tenantId!));
    }
}
