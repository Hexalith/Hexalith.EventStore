using Dapr.Actors;
using Dapr.Actors.Client;
using Dapr.Actors.Runtime;
using Hexalith.EventStore.Client.Streams;
using Hexalith.EventStore.Contracts.Identity;
using Hexalith.EventStore.Contracts.Results;
using Hexalith.EventStore.Contracts.Security;
using Hexalith.EventStore.Contracts.Streams;
using Hexalith.EventStore.Server.Actors;
using Hexalith.EventStore.Server.Configuration;
using Hexalith.EventStore.Server.Events;
using Hexalith.EventStore.Server.Tests.TestUtilities;
using Hexalith.EventStore.Server.Streams;
using Hexalith.EventStore.Testing.Fakes;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using NSubstitute;
using Shouldly;
using static Hexalith.EventStore.Server.Tests.Actors.AggregateActorTestHelper;

namespace Hexalith.EventStore.Server.Tests.Actors;

/// <summary>Actual aggregate persistence never commits events ahead of a configured namespace registration.</summary>
public sealed class AggregateActorPublicationRegistrationTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ConfiguredRegistrationPrecedesEventCommitAndRefusalPreventsAppend(bool refuse)
    {
        var backend = new InMemoryStateManager(); var context = CreateActor(stateManager: backend);
        context.Invoker.InvokeAsync(Arg.Any<Hexalith.EventStore.Contracts.Commands.CommandEnvelope>(), Arg.Any<object?>(), Arg.Any<CancellationToken>())
            .Returns(DomainResult.Success([new TestEvent()]));
        var registration = Substitute.For<ISourcePublicationWriterRegistration>();
        int registered = 0;
        registration.RegisterBeforeWriteAsync(Arg.Any<AggregateIdentity>(), Arg.Any<CancellationToken>()).Returns(call =>
        {
            registered++; backend.CommittedState.Values.OfType<EventEnvelope>().ShouldBeEmpty();
            call.Arg<AggregateIdentity>().ActorId.ShouldBe("test-tenant:test-domain:agg-001");
            return refuse ? Task.FromException(new InvalidOperationException("Controlled namespace registration refusal.")) : Task.CompletedTask;
        });
        using var services = new ServiceCollection().AddSingleton(registration).BuildServiceProvider();
        var actor = new AggregateActor(ActorHost.CreateForTest<AggregateActor>(new ActorTestOptions { ActorId = new("test-tenant:test-domain:agg-001") }),
            context.Logger, context.Invoker, context.SnapshotManager, new NoOpEventPayloadProtectionService(), context.StatusStore,
            context.EventPublisher, Options.Create(new EventDrainOptions()), Options.Create(new BackpressureOptions()), context.DeadLetterPublisher,
            serviceProvider: services, commandAggregateTypeResolver: context.AggregateTypeResolver);
        ActorStateManagerTestHelper.SetStateManager(actor, backend);
        var result = await actor.ProcessCommandAsync(CreateTestEnvelope());
        registered.ShouldBe(1);
        if (refuse) { backend.CommittedState.Values.OfType<EventEnvelope>().ShouldBeEmpty(); result.Accepted.ShouldBeFalse(); }
        else { backend.CommittedState.Values.OfType<EventEnvelope>().Count().ShouldBe(1); result.Accepted.ShouldBeTrue(); }
    }

    /// <summary>Actual registration cannot release an aggregate append after independently qualified coverage changes during its final transport awaits.</summary>
    [Theory]
    [InlineData("register", "withdraw")]
    [InlineData("register", "expire")]
    [InlineData("read", "withdraw")]
    [InlineData("read", "expire")]
    [InlineData("read", "current")]
    public async Task FinalRegistrationRosterRequiresFreshIndependentQualification(string suspendedOperation, string change)
    {
        ArgumentNullException.ThrowIfNull(suspendedOperation); ArgumentNullException.ThrowIfNull(change);
        var clock = new FakeTimeProvider(DateTimeOffset.Parse("2026-10-08T12:00:00Z", System.Globalization.CultureInfo.InvariantCulture));
        var identity = new AggregateIdentity("test-tenant", "test-domain", "agg-001");
        var scope = new SourcePublicationScope(identity.TenantId, identity.Domain, "deletion", "installation-1");
        var namespaceBackend = new InMemoryStateManager(); var operations = Substitute.For<ISourcePublicationOperationAuthority>();
        operations.ReadNamespaceAsync(scope).Returns(true);
        operations.InstallNamespaceAsync(Arg.Any<SourcePublicationNamespaceState>()).Returns(true);
        operations.RegisterSourceAsync(scope, 1, identity).Returns(true);
        var namespaceActor = new SourcePublicationNamespaceActor(ActorHost.CreateForTest<SourcePublicationNamespaceActor>(
            new ActorTestOptions { ActorId = new(scope.ActorId) }), operations);
        ActorStateManagerTestHelper.SetStateManager(namespaceActor, namespaceBackend);
        (await namespaceActor.InstallAsync(new(scope, 1, "installed", "coverage", "writers", []))).ShouldBeTrue();
        bool qualified = true; var authorization = new SourcePublicationNamespaceAuthorization("qualified", clock.GetUtcNow().AddSeconds(10));
        var authority = Substitute.For<ISourcePublicationNamespaceAuthority>(); var authorizedRosters = new List<SourcePublicationNamespaceState>();
        authority.AuthorizeAsync(Arg.Any<SourcePublicationNamespaceState>(), Arg.Any<CancellationToken>()).Returns(call =>
        {
            authorizedRosters.Add(call.Arg<SourcePublicationNamespaceState>());
            return qualified ? authorization : null;
        });
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var transport = Substitute.For<ISourcePublicationNamespaceActor>(); int reads = 0;
        transport.ReadAsync(scope).Returns(async _ =>
        {
            var state = await namespaceActor.ReadAsync(scope);
            if (++reads == 2 && suspendedOperation == "read") { entered.TrySetResult(); await release.Task; }
            return state;
        });
        transport.RegisterAsync(scope, 1, identity).Returns(async _ =>
        {
            bool result = await namespaceActor.RegisterAsync(scope, 1, identity);
            if (suspendedOperation == "register") { entered.TrySetResult(); await release.Task; }
            return result;
        });
        var proxies = Substitute.For<IActorProxyFactory>();
        proxies.CreateActorProxy<ISourcePublicationNamespaceActor>(new ActorId(scope.ActorId), SourcePublicationNamespaceActor.ActorTypeName).Returns(transport);
        var registration = new DaprSourcePublicationWriterRegistration([scope], proxies, authority, clock);
        var backend = new InMemoryStateManager(); var context = CreateActor(stateManager: backend);
        context.Invoker.InvokeAsync(Arg.Any<Hexalith.EventStore.Contracts.Commands.CommandEnvelope>(), Arg.Any<object?>(), Arg.Any<CancellationToken>())
            .Returns(DomainResult.Success([new TestEvent()]));
        using var services = new ServiceCollection().AddSingleton<ISourcePublicationWriterRegistration>(registration).BuildServiceProvider();
        var actor = new AggregateActor(ActorHost.CreateForTest<AggregateActor>(new ActorTestOptions { ActorId = new(identity.ActorId) }),
            context.Logger, context.Invoker, context.SnapshotManager, new NoOpEventPayloadProtectionService(), context.StatusStore,
            context.EventPublisher, Options.Create(new EventDrainOptions()), Options.Create(new BackpressureOptions()), context.DeadLetterPublisher,
            serviceProvider: services, commandAggregateTypeResolver: context.AggregateTypeResolver);
        ActorStateManagerTestHelper.SetStateManager(actor, backend);
        var pending = actor.ProcessCommandAsync(CreateTestEnvelope());
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        backend.CommittedState.Values.OfType<EventEnvelope>().ShouldBeEmpty();
        if (change == "withdraw") { qualified = false; }
        if (change == "expire") { clock.Advance(TimeSpan.FromSeconds(11)); }
        release.TrySetResult();
        var result = await pending.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        result.Accepted.ShouldBe(change == "current");
        backend.CommittedState.Values.OfType<EventEnvelope>().Count().ShouldBe(change == "current" ? 1 : 0);
        var retained = namespaceBackend.CommittedState.Single().Value.ShouldBeOfType<SourcePublicationNamespaceState>();
        retained.Revision.ShouldBe(2); retained.Sources.ShouldBe(new[] { identity });
        authorizedRosters.Count.ShouldBe(2); authorizedRosters[0].Sources.ShouldBeEmpty();
        System.Text.Json.JsonSerializer.Serialize(authorizedRosters[1]).ShouldBe(System.Text.Json.JsonSerializer.Serialize(retained));
    }
    /// <summary>Registration may complete durably while the exact execution capability expires/withdraws; protection and event staging must not begin.</summary>
    [Theory]
    [InlineData("withdraw")]
    [InlineData("expire")]
    [InlineData("current")]
    public async Task RegistrationAwaitRechecksExecutionBeforeProtection(string change)
    {
        var clock = new FakeTimeProvider(DateTimeOffset.Parse("2026-10-09T00:00:00Z", System.Globalization.CultureInfo.InvariantCulture));
        DateTimeOffset validUntil = clock.GetUtcNow().AddSeconds(10); bool current = true;
        var envelope = CreateTestEnvelope();
        var capabilityOwner = Substitute.For<IIdempotencyAdmissionActor>();
        capabilityOwner.ValidateAuthorityAsync(Arg.Any<IdempotencyAdmissionAuthorityRequest>()).Returns(call =>
        {
            var request = call.Arg<IdempotencyAdmissionAuthorityRequest>();
            request.FencingToken.ShouldBe(7); request.ExecutionMessageId.ShouldBe(envelope.MessageId);
            request.ExecutionCorrelationId.ShouldBe(envelope.CorrelationId);
            return current && clock.GetUtcNow() < validUntil ? Task.CompletedTask
                : Task.FromException(new InvalidOperationException("Current exact execution authority withdrawn or expired"));
        });
        var proxies = Substitute.For<IActorProxyFactory>();
        proxies.CreateActorProxy<IIdempotencyAdmissionActor>(Arg.Any<ActorId>(), IdempotencyAdmissionActor.ActorTypeName).Returns(capabilityOwner);
        var protector = new Hexalith.EventStore.Server.Commands.IdempotencyExecutionContextProtector(
            new Hexalith.EventStore.Server.Commands.StaticIdempotencyDigestKeyProvider("v1",
                new Dictionary<string, byte[]> { ["v1"] = "0123456789abcdef0123456789abcdef"u8.ToArray() }, []), proxies);
        var execution = await protector.ProtectAsync("test-tenant:v1:retained-key-digest", 7, "v1",
            new Hexalith.EventStore.Server.Pipeline.Commands.SubmitCommand(envelope.MessageId, envelope.TenantId, envelope.Domain,
                envelope.AggregateId, envelope.CommandType, envelope.Payload, envelope.CorrelationId, envelope.UserId), TestContext.Current.CancellationToken);
        var identity = envelope.AggregateIdentity;
        var scope = new SourcePublicationScope(identity.TenantId, identity.Domain, "deletion", "installation-1");
        var namespaceBackend = new InMemoryStateManager(); var namespacePermission = Substitute.For<ISourcePublicationOperationAuthority>();
        namespacePermission.ReadNamespaceAsync(scope).Returns(true); namespacePermission.InstallNamespaceAsync(Arg.Any<SourcePublicationNamespaceState>()).Returns(true);
        namespacePermission.RegisterSourceAsync(scope, 1, identity).Returns(true);
        var namespaceActor = new SourcePublicationNamespaceActor(ActorHost.CreateForTest<SourcePublicationNamespaceActor>(new ActorTestOptions { ActorId = new(scope.ActorId) }), namespacePermission);
        ActorStateManagerTestHelper.SetStateManager(namespaceActor, namespaceBackend);
        (await namespaceActor.InstallAsync(new(scope, 1, "installed", "coverage", "writers", []))).ShouldBeTrue();
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var registration = Substitute.For<ISourcePublicationWriterRegistration>();
        registration.RegisterBeforeWriteAsync(identity, Arg.Any<CancellationToken>()).Returns(async _ =>
        {
            (await namespaceActor.RegisterAsync(scope, 1, identity)).ShouldBeTrue(); entered.TrySetResult(); await release.Task;
        });
        var backend = new InMemoryStateManager(); var protection = Substitute.For<IEventPayloadProtectionService>();
        protection.ProtectEventPayloadAsync(Arg.Any<AggregateIdentity>(), Arg.Any<Hexalith.EventStore.Contracts.Events.IEventPayload>(), Arg.Any<string>(), Arg.Any<byte[]>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(call => new PayloadProtectionResult(call.Arg<byte[]>().ToArray(), call.ArgAt<string>(4)));
        var context = CreateActor(stateManager: backend);
        context.Invoker.InvokeAsync(Arg.Any<Hexalith.EventStore.Contracts.Commands.CommandEnvelope>(), Arg.Any<object?>(), Arg.Any<CancellationToken>()).Returns(DomainResult.Success([new TestEvent()]));
        using var services = new ServiceCollection().AddSingleton(registration).BuildServiceProvider();
        var actor = new AggregateActor(ActorHost.CreateForTest<AggregateActor>(new ActorTestOptions { ActorId = new(identity.ActorId) }),
            context.Logger, context.Invoker, context.SnapshotManager, protection, context.StatusStore, context.EventPublisher,
            Options.Create(new EventDrainOptions()), Options.Create(new BackpressureOptions()), context.DeadLetterPublisher,
            serviceProvider: services, commandAggregateTypeResolver: context.AggregateTypeResolver, executionContextProtector: protector);
        ActorStateManagerTestHelper.SetStateManager(actor, backend);
        var pending = actor.ProcessFencedCommandAsync(new(envelope, execution));
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(2), TestContext.Current.CancellationToken);
        byte[] namespaceOriginal = System.Text.Json.JsonSerializer.SerializeToUtf8Bytes(namespaceBackend.CommittedState.Single().Value);
        protection.ReceivedCalls().ShouldBeEmpty(); backend.CommittedState.Values.OfType<EventEnvelope>().ShouldBeEmpty();
        if (change == "withdraw") { current = false; }
        if (change == "expire") { clock.Advance(TimeSpan.FromSeconds(11)); }
        release.TrySetResult();
        try
        {
            var result = await pending.WaitAsync(TimeSpan.FromSeconds(2), TestContext.Current.CancellationToken);
            result.Accepted.ShouldBe(change == "current");
        }
        catch (InvalidOperationException) when (change != "current") { }
        System.Text.Json.JsonSerializer.SerializeToUtf8Bytes(namespaceBackend.CommittedState.Single().Value).ShouldBe(namespaceOriginal);
        if (change == "current") { backend.CommittedState.Values.OfType<EventEnvelope>().Count().ShouldBe(1); }
        else
        {
            protection.ReceivedCalls().ShouldBeEmpty();
            backend.CommittedState.Values.OfType<EventEnvelope>().ShouldBeEmpty();
        }
    }

}
