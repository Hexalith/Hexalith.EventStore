using Dapr.Actors;
using Dapr.Actors.Client;

using Hexalith.EventStore.Contracts.Identity;
using Hexalith.EventStore.Server.Actors;
using Hexalith.EventStore.Server.Projections;

using NSubstitute;

using Shouldly;

namespace Hexalith.EventStore.Server.Tests.Projections;

/// <summary>
/// Tests for <see cref="DaprProjectionLifecycleGateway"/>. Mirror the weak/JSON invocation
/// discipline pinned by <c>DefaultProjectionActorInvokerTests</c>: the gateway must compose the
/// (tenant, domain, aggregate, projection) actor id and dispatch through the weak
/// <see cref="IActorProxyFactory.Create(ActorId,string,ActorProxyOptions)"/> proxy — never the
/// strongly typed dispatch proxy — and it must reject reserved-character projection names.
/// </summary>
public class DaprProjectionLifecycleGatewayTests {
    private static readonly AggregateIdentity TestIdentity = new("tenant1", "domain1", "agg1");

    [Fact]
    public async Task TryAdmitDeliveryWriteAsync_ComposesActorIdAndUsesWeakProxyPath() {
        IActorProxyFactory factory = Substitute.For<IActorProxyFactory>();
        var gateway = new DaprProjectionLifecycleGateway(factory);

        try {
            _ = await gateway.TryAdmitDeliveryWriteAsync(TestIdentity, "counter", CancellationToken.None);
        }
        catch (NullReferenceException) {
            // Expected: the factory substitute returns a null ActorProxy (internal constructor
            // prevents NSubstitute from building a usable instance), so the subsequent weak
            // InvokeMethodAsync NREs. The recorded Create(...) call proves the weak-path behavior.
        }

        _ = factory.Received(1).Create(
            Arg.Is<ActorId>(id => id.ToString() == "tenant1:domain1:agg1:counter"),
            ProjectionLifecycleActor.ActorTypeName,
            Arg.Any<ActorProxyOptions?>());

        _ = factory.DidNotReceive().CreateActorProxy<IProjectionLifecycleActor>(
            Arg.Any<ActorId>(),
            Arg.Any<string>());
        _ = factory.DidNotReceive().CreateActorProxy<IProjectionLifecycleActor>(
            Arg.Any<ActorId>(),
            Arg.Any<string>(),
            Arg.Any<ActorProxyOptions?>());
    }

    [Fact]
    public async Task BeginEraseAsync_ComposesActorIdAndUsesWeakProxyPath() {
        IActorProxyFactory factory = Substitute.For<IActorProxyFactory>();
        var gateway = new DaprProjectionLifecycleGateway(factory);

        try {
            _ = await gateway.BeginEraseAsync(TestIdentity, "counter", "op-1", "digest-1", CancellationToken.None);
        }
        catch (NullReferenceException) {
            // Expected — see TryAdmit test.
        }

        _ = factory.Received(1).Create(
            Arg.Is<ActorId>(id => id.ToString() == "tenant1:domain1:agg1:counter"),
            ProjectionLifecycleActor.ActorTypeName,
            Arg.Any<ActorProxyOptions?>());
    }

    [Theory]
    [InlineData("a:b")]
    [InlineData("a|b")]
    [InlineData("a\0b")]
    [InlineData("a\rb")]
    [InlineData("a\nb")]
    [InlineData("   ")]
    [InlineData("")]
    public async Task TryAdmitDeliveryWriteAsync_ReservedOrBlankProjectionName_ThrowsArgumentExceptionBeforeCreate(string projectionName) {
        IActorProxyFactory factory = Substitute.For<IActorProxyFactory>();
        var gateway = new DaprProjectionLifecycleGateway(factory);

        _ = await Should.ThrowAsync<ArgumentException>(
            () => gateway.TryAdmitDeliveryWriteAsync(TestIdentity, projectionName, CancellationToken.None));

        _ = factory.DidNotReceiveWithAnyArgs().Create(default!, default!, default);
    }

    [Fact]
    public async Task BeginEraseAsync_ReservedProjectionName_ThrowsArgumentExceptionBeforeCreate() {
        IActorProxyFactory factory = Substitute.For<IActorProxyFactory>();
        var gateway = new DaprProjectionLifecycleGateway(factory);

        _ = await Should.ThrowAsync<ArgumentException>(
            () => gateway.BeginEraseAsync(TestIdentity, "bad:name", "op-1", "digest-1", CancellationToken.None));

        _ = factory.DidNotReceiveWithAnyArgs().Create(default!, default!, default);
    }

    [Fact]
    public async Task TryAdmitDeliveryWriteAsync_NullIdentity_ThrowsArgumentNullException() {
        IActorProxyFactory factory = Substitute.For<IActorProxyFactory>();
        var gateway = new DaprProjectionLifecycleGateway(factory);

        _ = await Should.ThrowAsync<ArgumentNullException>(
            () => gateway.TryAdmitDeliveryWriteAsync(null!, "counter", CancellationToken.None));
    }

    [Fact]
    public void Constructor_NullFactory_ThrowsArgumentNullException()
        => _ = Should.Throw<ArgumentNullException>(() => new DaprProjectionLifecycleGateway(null!));
}
