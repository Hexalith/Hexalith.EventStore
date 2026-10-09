using System.Text.Json;

using Hexalith.EventStore.Client.Events;
using Hexalith.EventStore.Contracts.Events;
using Hexalith.EventStore.Contracts.Projections;

using Microsoft.Extensions.DependencyInjection;

using Shouldly;

namespace Hexalith.EventStore.DomainService.Tests;

public sealed class VersionedProjectionDispatchTests
{
    [EventPayloadVersion(2)]
    public sealed record RenamedEvent(int Value) : IEventPayload;

    private sealed class RenameStep : IEventPayloadUpcaster
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
}
