
using Dapr.Actors;
using Dapr.Actors.Client;

using Hexalith.EventStore.Contracts.Queries;
using Hexalith.EventStore.Server.Actors;
using Hexalith.EventStore.Server.Queries;

using NSubstitute;

using Shouldly;

namespace Hexalith.EventStore.Server.Tests.Queries;

/// <summary>
/// Regression-sensitive tests for <c>DefaultProjectionActorInvoker</c>. These would fail
/// against the original implementation that created a strongly typed dispatch proxy via
/// <see cref="IActorProxyFactory.CreateActorProxy{TActorInterface}(ActorId,string,ActorProxyOptions)"/>
/// and then cast it back to <see cref="ActorProxy"/> — that proxy did not initialize the
/// weak/JSON invocation state, producing a runtime <see cref="NullReferenceException"/>.
/// </summary>
public class DefaultProjectionActorInvokerTests {
    private static QueryEnvelope CreateEnvelope(
        string tenantId = "system",
        string domain = "tenants",
        string aggregateId = "index",
        string queryType = "list-tenants",
        string? entityId = "index",
        byte[]? payload = null) => new(
            tenantId,
            domain,
            aggregateId,
            queryType,
            payload ?? [],
            "corr-1",
            "user-1",
            entityId);

    private static string FindRepositoryRoot() {
        DirectoryInfo? directory = new(Directory.GetCurrentDirectory());
        while (directory is not null) {
            if (File.Exists(Path.Combine(directory.FullName, "Directory.Packages.props"))
                && Directory.Exists(Path.Combine(directory.FullName, "src", "Hexalith.EventStore.Server"))) {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Could not locate repository root from the test working directory.");
    }

    [Fact]
    public async Task InvokeAsync_CallsIActorProxyFactoryCreate_NotCreateActorProxyGeneric() {
        IActorProxyFactory factory = Substitute.For<IActorProxyFactory>();
        var invoker = new DefaultProjectionActorInvoker(factory);

        try {
            _ = await invoker.InvokeAsync(
                "tenants:system:index",
                "TenantsProjectionActor",
                CreateEnvelope(),
                CancellationToken.None);
        }
        catch (NullReferenceException) {
            // Expected: factory substitute returns null for ActorProxy (internal constructor
            // prevents NSubstitute from creating a usable instance), so the subsequent
            // InvokeMethodAsync<,> call NREs. The call recording on `factory.Create(...)`
            // above is what proves correct weak-path behavior.
        }

        _ = factory.Received(1).Create(
            Arg.Is<ActorId>(id => id.ToString() == "tenants:system:index"),
            "TenantsProjectionActor",
            Arg.Any<ActorProxyOptions?>());

        _ = factory.DidNotReceive().CreateActorProxy<IDaprProjectionActor>(
            Arg.Any<ActorId>(),
            Arg.Any<string>());
        _ = factory.DidNotReceive().CreateActorProxy<IDaprProjectionActor>(
            Arg.Any<ActorId>(),
            Arg.Any<string>(),
            Arg.Any<ActorProxyOptions?>());
    }

    [Fact]
    public void IDaprProjectionActor_QueryAsync_MirrorsPublicProjectionContractSignature() {
        System.Reflection.MethodInfo publicMethod = typeof(IProjectionActor)
            .GetMethod(nameof(IProjectionActor.QueryAsync), [typeof(QueryEnvelope)])
            .ShouldNotBeNull();
        System.Reflection.MethodInfo daprMethod = typeof(IDaprProjectionActor)
            .GetMethod(nameof(IDaprProjectionActor.QueryAsync), [typeof(QueryEnvelope)])
            .ShouldNotBeNull();

        daprMethod.Name.ShouldBe(publicMethod.Name);
        daprMethod.ReturnType.ShouldBe(publicMethod.ReturnType);
        daprMethod.GetParameters()
            .Select(p => p.ParameterType)
            .ShouldBe(publicMethod.GetParameters().Select(p => p.ParameterType));
    }

    [Fact]
    public void InvokeAsync_SourceUsesWeakQueryAsyncInvocationWithPublicContractName() {
        string sourcePath = Path.Combine(
            FindRepositoryRoot(),
            "src",
            "Hexalith.EventStore.Server",
            "Queries",
            "DefaultProjectionActorInvoker.cs");
        string source = File.ReadAllText(sourcePath);

        source.ShouldContain("_actorProxyFactory.Create(new ActorId(actorId), actorTypeName)");
        source.ShouldContain("proxy.InvokeMethodAsync<QueryEnvelope, QueryResult>(");
        source.ShouldContain("nameof(IProjectionActor.QueryAsync)");
        source.ShouldNotContain("CreateActorProxy<IDaprProjectionActor>");
    }

    [Fact]
    public async Task InvokeAsync_PreCancelledToken_ThrowsBeforeFactoryCall() {
        IActorProxyFactory factory = Substitute.For<IActorProxyFactory>();
        var invoker = new DefaultProjectionActorInvoker(factory);
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        _ = await Should.ThrowAsync<OperationCanceledException>(
            () => invoker.InvokeAsync("a:b:c", "ProjectionActor", CreateEnvelope(), cts.Token));

        _ = factory.DidNotReceive().Create(
            Arg.Any<ActorId>(),
            Arg.Any<string>(),
            Arg.Any<ActorProxyOptions?>());
    }

    [Fact]
    public async Task InvokeAsync_NullActorId_ThrowsArgumentException() {
        IActorProxyFactory factory = Substitute.For<IActorProxyFactory>();
        var invoker = new DefaultProjectionActorInvoker(factory);

        _ = await Should.ThrowAsync<ArgumentException>(
            () => invoker.InvokeAsync(null!, "ProjectionActor", CreateEnvelope(), CancellationToken.None));
    }

    [Fact]
    public async Task InvokeAsync_NullActorTypeName_ThrowsArgumentException() {
        IActorProxyFactory factory = Substitute.For<IActorProxyFactory>();
        var invoker = new DefaultProjectionActorInvoker(factory);

        _ = await Should.ThrowAsync<ArgumentException>(
            () => invoker.InvokeAsync("a:b:c", null!, CreateEnvelope(), CancellationToken.None));
    }

    [Fact]
    public async Task InvokeAsync_NullEnvelope_ThrowsArgumentNullException() {
        IActorProxyFactory factory = Substitute.For<IActorProxyFactory>();
        var invoker = new DefaultProjectionActorInvoker(factory);

        _ = await Should.ThrowAsync<ArgumentNullException>(
            () => invoker.InvokeAsync("a:b:c", "ProjectionActor", null!, CancellationToken.None));
    }

    [Fact]
    public void Constructor_NullFactory_ThrowsArgumentNullException() => _ = Should.Throw<ArgumentNullException>(() => new DefaultProjectionActorInvoker(null!));
}
