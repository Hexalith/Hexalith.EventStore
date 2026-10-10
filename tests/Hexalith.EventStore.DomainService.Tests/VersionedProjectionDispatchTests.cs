using System.Text.Json;

using Hexalith.EventStore.Client.Events;
using Hexalith.EventStore.Client.Projections;
using Hexalith.EventStore.Contracts.Events;
using Hexalith.EventStore.Contracts.Projections;

using Microsoft.Extensions.DependencyInjection;

using NSubstitute;

using Shouldly;

namespace Hexalith.EventStore.DomainService.Tests;

public sealed class VersionedProjectionDispatchTests
{
    [EventPayloadVersion(2)]
    public sealed record RenamedEvent(int Value) : IEventPayload;

    internal sealed class RenameStep : IEventPayloadUpcaster
    {
        public string EventTypeName => "Old.Contracts.CounterRaised";
        public int FromVersion => 1;
        public string TargetEventTypeName => typeof(RenamedEvent).FullName!;

        public System.Text.Json.Nodes.JsonObject Upcast(System.Text.Json.Nodes.JsonObject payload)
        {
            payload["Value"] = payload["Amount"]!.GetValue<int>();
            payload.Remove("Amount");
            return payload;
        }
    }

    private sealed class CapturingHandler : IDomainProjectionHandler
    {
        public string Domain => "counter";
        public ProjectionRequest? LastRequest { get; private set; }
        public CancellationToken LastToken { get; private set; }

        public ProjectionResponse Project(ProjectionRequest request) =>
            throw new InvalidOperationException("The token-aware overload must be called.");

        public ProjectionResponse Project(ProjectionRequest request, CancellationToken cancellationToken)
        {
            LastRequest = request;
            LastToken = cancellationToken;
            return new ProjectionResponse("counter", JsonSerializer.SerializeToElement(new { count = request.Events.Length }));
        }
    }

    [Fact]
    public void Project_UpcastsStoredAliasBeforeHandlerAndPassesCallerToken()
    {
        var handler = new CapturingHandler();
        var registry = new EventPayloadEvolutionRegistry([typeof(RenamedEvent)], [new RenameStep()]);
        using ServiceProvider services = new ServiceCollection()
            .AddSingleton<IDomainProjectionHandler>(handler)
            .AddSingleton(registry)
            .BuildServiceProvider();
        var stored = new ProjectionEventDto("Old.Contracts.CounterRaised",
            JsonSerializer.SerializeToUtf8Bytes(new { Amount = 5 }), "json", 1,
            DateTimeOffset.UnixEpoch, "correlation-1") { MetadataVersion = 1, StoredPayloadVersion = 1 };
        var request = new ProjectionRequest("tenant-1", "counter", "counter-1", [stored]);
        using var source = new CancellationTokenSource();

        ProjectionResponse? response = DomainProjectionDispatcher.Project(services, request, source.Token);

        response.ShouldNotBeNull().State.GetProperty("count").GetInt32().ShouldBe(1);
        handler.LastToken.ShouldBe(source.Token);
        ProjectionEventDto delivered = handler.LastRequest.ShouldNotBeNull().Events.ShouldHaveSingleItem();
        delivered.EventTypeName.ShouldBe(typeof(RenamedEvent).FullName);
        delivered.StoredPayloadVersion.ShouldBe(1);
        JsonSerializer.Deserialize<RenamedEvent>(delivered.Payload).ShouldBe(new RenamedEvent(5));
        stored.Payload.ShouldBe(JsonSerializer.SerializeToUtf8Bytes(new { Amount = 5 }));
    }

    [Fact]
    public async Task LegacyAdapter_PassesCallerTokenIntoHandler()
    {
        var handler = new CapturingHandler();
        var adapter = new LegacyDomainProjectionHandlerAdapter(handler, "counter", "counter");
        using var source = new CancellationTokenSource();

        DomainProjectionHandlerResult result = await adapter.ProjectAsync(
            new ProjectionRequest("tenant-1", "counter", "counter-1", []), "dispatch-1", source.Token);

        result.Status.ShouldBe(ProjectionDispatchStatus.Completed);
        handler.LastToken.ShouldBe(source.Token);
    }

    [Fact]
    public void Project_MalformedKnownCurrentPayloadFailsBeforeHandler()
    {
        var handler = new CapturingHandler();
        var registry = new EventPayloadEvolutionRegistry([typeof(RenamedEvent)], [new RenameStep()]);
        using ServiceProvider services = new ServiceCollection()
            .AddSingleton<IDomainProjectionHandler>(handler)
            .AddSingleton(registry)
            .BuildServiceProvider();
        var stored = new ProjectionEventDto(typeof(RenamedEvent).FullName!, [0xFF], "json", 2,
            DateTimeOffset.UnixEpoch, "correlation-1") { StoredPayloadVersion = 2 };

        EventPayloadEvolutionException error = Should.Throw<EventPayloadEvolutionException>(() =>
            DomainProjectionDispatcher.Project(services, new ProjectionRequest("tenant-1", "counter", "counter-1", [stored])));

        error.SequenceNumber.ShouldBe(2);
        handler.LastRequest.ShouldBeNull();
    }

    [Fact]
    public void Project_RenamedKnownEventWithUnsupportedFormatFailsBeforeHandler()
    {
        var handler = new CapturingHandler();
        var registry = new EventPayloadEvolutionRegistry([typeof(RenamedEvent)], [new RenameStep()]);
        using ServiceProvider services = new ServiceCollection()
            .AddSingleton<IDomainProjectionHandler>(handler)
            .AddSingleton(registry)
            .BuildServiceProvider();
        var stored = new ProjectionEventDto("Old.Contracts.CounterRaised",
            "{\"Amount\":5}"u8.ToArray(), "xml", 4, DateTimeOffset.UnixEpoch, "correlation-1")
        { MetadataVersion = 1, StoredPayloadVersion = 1 };

        EventPayloadEvolutionException failure = Should.Throw<EventPayloadEvolutionException>(() =>
            DomainProjectionDispatcher.Project(services,
                new ProjectionRequest("tenant-1", "counter", "counter-1", [stored])));

        failure.SequenceNumber.ShouldBe(4);
        failure.StoredVersion.ShouldBe(1);
        failure.Message.ShouldContain("unsupported serialization format");
        handler.LastRequest.ShouldBeNull();
    }

    [Fact]
    public async Task DispatchAsync_RenamedV1EventReachesNamedHandlerWithCurrentPayload()
    {
        IAsyncDomainProjectionHandler handler = Substitute.For<IAsyncDomainProjectionHandler>();
        handler.Domain.Returns("counter");
        handler.ProjectionType.Returns("counter-summary");
        ProjectionRequest? delivered = null;
        handler.ProjectAsync(Arg.Any<ProjectionRequest>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                delivered = call.ArgAt<ProjectionRequest>(0);
                return DomainProjectionHandlerResult.Completed(JsonSerializer.SerializeToElement(new { count = 1 }));
            });
        using ServiceProvider services = new ServiceCollection()
            .AddSingleton(handler)
            .AddSingleton(new EventPayloadEvolutionRegistry([typeof(RenamedEvent)], [new RenameStep()]))
            .BuildServiceProvider();
        var catalog = new DomainProjectionCatalogRegistry();
        catalog.Register("fingerprint-1", [new ProjectionDispatchRoute("counter", "counter-summary")]);
        byte[] original = "{\"Amount\":5}"u8.ToArray();
        var stored = new ProjectionEventDto("Old.Contracts.CounterRaised", original, "json", 1,
            DateTimeOffset.UnixEpoch, "correlation-1") { MetadataVersion = 1, StoredPayloadVersion = 1 };

        ProjectionDispatchResponse response = await DomainProjectionDispatcher.DispatchAsync(services,
            new ProjectionDispatchRequest(
                new ProjectionRequest("tenant-1", "counter", "counter-1", [stored]),
                ["counter-summary"], "dispatch-1", "fingerprint-1"),
            new ProjectionDispatchOptions(), catalog, CancellationToken.None);

        response.Outcomes.ShouldHaveSingleItem().Status.ShouldBe(ProjectionDispatchStatus.Completed);
        ProjectionEventDto deliveredEvent = delivered.ShouldNotBeNull().Events.ShouldHaveSingleItem();
        deliveredEvent.EventTypeName.ShouldBe(typeof(RenamedEvent).FullName);
        JsonSerializer.Deserialize<RenamedEvent>(deliveredEvent.Payload).ShouldBe(new RenamedEvent(5));
        stored.Payload.ShouldBe(original);
    }
}
