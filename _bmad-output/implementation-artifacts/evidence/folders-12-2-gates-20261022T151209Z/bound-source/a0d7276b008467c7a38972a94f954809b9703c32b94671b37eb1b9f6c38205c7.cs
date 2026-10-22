using System.Text.Json;

using Hexalith.EventStore.Client.Discovery;
using Hexalith.EventStore.Client.Projections;
using Hexalith.EventStore.Client.Tests.Projections;
using Hexalith.EventStore.Contracts.Projections;
using Hexalith.EventStore.DomainService;

using Microsoft.Extensions.DependencyInjection;

using NSubstitute;

namespace Hexalith.EventStore.Server.Tests.Projections;

/// <summary>An unchanged generic handler, explicitly bound by its host, over the production Dapr batch store.</summary>
internal sealed class ReplayRecoveryProductionFixture : IDisposable
{
    internal const string StoreName = "existing-models";
    internal const string Route = "counter-detail";
    internal RecordingDaprClient Backend { get; } = new() { PersistTypedState = true };
    internal DaprReadModelStore Models { get; }
    internal IAsyncDomainProjectionRebuildHandler Handler => Handlers[0];
    internal IAsyncDomainProjectionRebuildHandler[] Handlers { get; }
    internal ProjectionDispatchRequest Request { get; }
    internal AdminOperationalIndexMetadata.Response HistoricalMetadata { get; }
    internal AdminOperationalIndexMetadata.Response Metadata { get; }
    internal string Header { get; }
    internal int Preparations { get; private set; }

    internal ReplayRecoveryProductionFixture(int head = 3, int routeCount = 1)
    {
        Models = new(Backend);
        Handlers = new IAsyncDomainProjectionRebuildHandler[routeCount];
        for (int routeIndex = 0; routeIndex < routeCount; routeIndex++)
        {
            IAsyncDomainProjectionRebuildHandler handler = Substitute.For<IAsyncDomainProjectionRebuildHandler>();
            Handlers[routeIndex] = handler;
            string route = routeIndex == 0 ? Route : "counter-index";
            string suffix = routeIndex == 0 ? string.Empty : ":" + route;
            handler.Domain.Returns("counter");
            handler.ProjectionType.Returns(route);
            handler.OrdinaryDeliveryEnabled.Returns(true);
            handler.RebuildSemantics.Returns(DomainProjectionRebuildSemantics.FullReplay);
            handler.PrepareRebuildAsync(Arg.Any<ProjectionRequest>(), Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(async call =>
            {
                Preparations++;
                ProjectionRequest input = call.ArgAt<ProjectionRequest>(0);
                ReadModelEntry<string> detail = await Models.GetAsync<string>(StoreName, "detail" + suffix, call.ArgAt<CancellationToken>(2));
                ReadModelEntry<string> index = await Models.GetAsync<string>(StoreName, "index" + suffix, call.ArgAt<CancellationToken>(2));
                return new DomainProjectionRebuildPlan(StoreName,
                [ReadModelBatchOperation.Write("detail" + suffix, "count:" + input.Events.Length,
                    detail.Value is null ? ReadModelBatchConcurrency.CreateOnly : ReadModelBatchConcurrency.Match(detail.ETag!)),
                    ReadModelBatchOperation.Write("index" + suffix, "index:" + input.Events.Length,
                    index.Value is null ? ReadModelBatchConcurrency.CreateOnly : ReadModelBatchConcurrency.Match(index.ETag!))]);
            });
        }
        HistoricalMetadata = AdminOperationalIndexMetadata.Create(new DiscoveryResult([], []), ["counter"], [], Handlers,
            "sample", "v1", new ProjectionDispatchOptions(), ["counter"]);
        using ServiceProvider services = Services();
        Metadata = DomainProjectionReplayRecovery.BindMetadata(services, HistoricalMetadata);
        Header = JsonSerializer.Serialize(Metadata.Domains.Single().ReplayRecoveryBindings!.ToDictionary(static pair => pair.Key,
            static pair => pair.Value.BindingFingerprint, StringComparer.Ordinal), JsonSerializerOptions.Web);
        ProjectionEventDto[] events = [.. Enumerable.Range(1, head).Select(index => new ProjectionEventDto("counter-incremented",
            "{}"u8.ToArray(), "json", index, DateTimeOffset.UnixEpoch, "correlation", $"01J{index:D23}", "user"))];
        Request = new(new("tenant-a", "counter", "counter-1", events)
            { RebuildSourceHeadSequence = head, RebuildSourceHeadMessageId = events[^1].MessageId }, [.. Handlers.Select(static handler => handler.ProjectionType)], events[^1].MessageId!,
            HistoricalMetadata.CatalogFingerprint!);
    }

    internal ServiceProvider Services(string? store = StoreName, IReadModelStore? recoveryStore = null)
    {
        var services = new ServiceCollection();
        foreach (IAsyncDomainProjectionRebuildHandler handler in Handlers)
        {
            services.AddSingleton<IAsyncDomainProjectionHandler>(handler);
            if (store is not null) services.AddSingleton(new DomainProjectionReplayRecoveryRegistration("counter", handler.ProjectionType, store));
        }
        services.AddSingleton<IReadModelStore>(recoveryStore ?? Models);
        services.AddSingleton<IReadModelBatchStagingStore>(Models);
        return services.BuildServiceProvider();
    }

    internal Task<ProjectionDispatchResponse> ExecuteAsync(DomainProjectionRebuildBatchAction action,
        string? store = StoreName, string? header = null, ProjectionDispatchRequest? request = null, CancellationToken token = default)
        => ExecuteWithFreshServicesAsync(action, store, header ?? Header, request ?? Request, token);

    private async Task<ProjectionDispatchResponse> ExecuteWithFreshServicesAsync(DomainProjectionRebuildBatchAction action,
        string? store, string header, ProjectionDispatchRequest request, CancellationToken token)
    {
        using ServiceProvider provider = Services(store);
        return await DomainProjectionReplayRecovery.ExecuteAsync(provider, request, new ProjectionDispatchOptions(),
            new() { AppId = "sample", ServiceVersion = "v1" }, action, header, token);
    }

    public void Dispose() => Backend.Dispose();
}
