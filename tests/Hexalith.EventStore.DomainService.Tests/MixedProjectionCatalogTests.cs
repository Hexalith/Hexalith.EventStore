using System.Security.Cryptography;
using System.Text.Json;

using Hexalith.EventStore.Client.Discovery;
using Hexalith.EventStore.Client.Projections;
using Hexalith.EventStore.Contracts.Projections;
using Hexalith.EventStore.Testing.Fakes;

using Microsoft.Extensions.DependencyInjection;

using Shouldly;

namespace Hexalith.EventStore.DomainService.Tests;

public sealed class MixedProjectionCatalogTests
{
    [Theory]
    [InlineData("ordinary-identity")]
    [InlineData("ordinary-registry")]
    [InlineData("reconcile")]
    [InlineData("full-rebuild")]
    [InlineData("streamed-rebuild")]
    public async Task ReceivingPathsUseAdvertisedMixedCatalogAndRetainRebuildAccess(string path)
    {
        ArgumentNullException.ThrowIfNull(path);
        var enabled = new Handler("enabled", true);
        var disabled = new Handler("disabled", false);
        var store = new InMemoryReadModelStore();
        using ServiceProvider provider = Provider(store, enabled, disabled);
        var options = new ProjectionDispatchOptions();
        var identity = new DomainProjectionIdentityOptions { AppId = "widget-service", ServiceVersion = "v1" };
        string fingerprint = AdminOperationalIndexMetadata.Create(new DiscoveryResult([], []), ["widget"], [],
            [enabled, disabled], identity.AppId, identity.ServiceVersion, options, ["widget"]).CatalogFingerprint!;
        string old = ProjectionRouteCatalogFingerprint.Compute(identity.AppId, identity.ServiceVersion,
            [new("widget", "enabled"), new("widget", "disabled")]);
        fingerprint.ShouldNotBe(old);
        bool rebuild = path.Contains("rebuild", StringComparison.Ordinal);
        Handler selected = rebuild ? disabled : enabled;
        var request = new ProjectionDispatchRequest(new ProjectionRequest("tenant-a", "widget", "owner",
            [new("event", [], "json", 1, DateTimeOffset.UnixEpoch, "corr")] ) { RebuildSourceHeadSequence = 1 },
            [selected.ProjectionType], "dispatch", fingerprint);
        var registry = new DomainProjectionCatalogRegistry();
        registry.Register(fingerprint, [new("widget", "enabled"), new("widget", "disabled")]);
        if (path == "streamed-rebuild")
        {
            byte[] bytes = JsonSerializer.SerializeToUtf8Bytes(request.Request.Events, JsonSerializerOptions.Web);
            request = request with { Request = request.Request with { Events = [] },
                RebuildTransport = new(1, Convert.ToHexString(SHA256.HashData(bytes))) };
            _ = await Should.ThrowAsync<ProjectionDispatchValidationException>(() => DomainProjectionRebuildTransport.StoreAsync(provider,
                new(request with { CatalogFingerprint = old }, 0, bytes), options, identity, TestContext.Current.CancellationToken));
            await DomainProjectionRebuildTransport.StoreAsync(provider, new(request, 0, bytes), options, identity, TestContext.Current.CancellationToken);
        }
        else
            _ = await Should.ThrowAsync<ProjectionDispatchValidationException>(() => Receive(request with { CatalogFingerprint = old }));
        selected.Calls.ShouldBe(0);
        var response = await Receive(request);
        response.Outcomes.ShouldHaveSingleItem().Status.ShouldBe(ProjectionDispatchStatus.Completed);
        selected.Calls.ShouldBe(1);
        (rebuild ? enabled : disabled).Calls.ShouldBe(0);
        if (path == "full-rebuild")
            (await store.GetAsync<int[]>("statestore", "view-disabled", TestContext.Current.CancellationToken)).Value.ShouldBe([1]);

        Task<ProjectionDispatchResponse> Receive(ProjectionDispatchRequest value) => path switch
        {
            "ordinary-identity" => DomainProjectionDispatcher.DispatchAsync(provider, value, options, identity, TestContext.Current.CancellationToken),
            "ordinary-registry" => DomainProjectionDispatcher.DispatchAsync(provider, value, options, registry, TestContext.Current.CancellationToken),
            "reconcile" => DomainProjectionDispatcher.ReconcileAsync(provider, value, options, identity, TestContext.Current.CancellationToken),
            "full-rebuild" => DomainProjectionDispatcher.RebuildAsync(provider, value, options, identity, TestContext.Current.CancellationToken),
            _ => DomainProjectionDispatcher.StageRebuildAsync(provider, value, options, identity, TestContext.Current.CancellationToken),
        };
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task BothOrdinaryOverloadsRejectDisabledHandlerBeforeFolding(bool registryOverload)
    {
        var enabled = new Handler("enabled", true);
        var disabled = new Handler("disabled", false);
        using ServiceProvider provider = Provider(new InMemoryReadModelStore(), enabled, disabled);
        var options = new ProjectionDispatchOptions();
        var identity = new DomainProjectionIdentityOptions { AppId = "widget-service", ServiceVersion = "v1" };
        string fingerprint = AdminOperationalIndexMetadata.Create(new DiscoveryResult([], []), ["widget"], [],
            [enabled, disabled], identity.AppId, identity.ServiceVersion, options, ["widget"]).CatalogFingerprint!;
        var request = new ProjectionDispatchRequest(new ProjectionRequest("tenant-a", "widget", "owner", []), ["disabled"], "dispatch", fingerprint);
        var registry = new DomainProjectionCatalogRegistry();
        registry.Register(fingerprint, [new("widget", "enabled"), new("widget", "disabled")]);
        _ = await Should.ThrowAsync<ProjectionDispatchValidationException>(() => registryOverload
            ? DomainProjectionDispatcher.DispatchAsync(provider, request, options, registry, TestContext.Current.CancellationToken)
            : DomainProjectionDispatcher.DispatchAsync(provider, request, options, identity, TestContext.Current.CancellationToken));
        disabled.Calls.ShouldBe(0); enabled.Calls.ShouldBe(0);
    }

    private static ServiceProvider Provider(InMemoryReadModelStore store, params Handler[] handlers)
    {
        var services = new ServiceCollection();
        _ = services.AddSingleton<IReadModelStore>(store);
        _ = services.AddSingleton<IReadModelBatchStore>(store);
        _ = services.AddSingleton<IReadModelBatchStagingStore>(store);
        foreach (Handler handler in handlers) _ = services.AddSingleton<IAsyncDomainProjectionHandler>(handler);
        return services.BuildServiceProvider();
    }

    private sealed class Handler(string type, bool ordinary) : IAsyncDomainProjectionRebuildHandler,
        IAsyncDomainProjectionReconciliationHandler, IAsyncDomainProjectionStreamedRebuildHandler
    {
        public string Domain => "widget";
        public string ProjectionType => type;
        public bool OrdinaryDeliveryEnabled => ordinary;
        public DomainProjectionRebuildSemantics RebuildSemantics => DomainProjectionRebuildSemantics.FullReplay;
        public string RebuildTransportStoreName => "statestore";
        public int Calls { get; private set; }
        public Task<DomainProjectionHandlerResult> ProjectAsync(ProjectionRequest request, string dispatchId, CancellationToken cancellationToken)
        { Calls++; return Task.FromResult(DomainProjectionHandlerResult.Completed(JsonSerializer.SerializeToElement(1))); }
        public Task<DomainProjectionHandlerResult> ReconcileAsync(ProjectionRequest request, string dispatchId, CancellationToken cancellationToken)
            => ProjectAsync(request, dispatchId, cancellationToken);
        public Task<DomainProjectionRebuildPlan> PrepareRebuildAsync(ProjectionRequest request, string operationId, CancellationToken cancellationToken)
        { Calls++; return Task.FromResult(Plan(request.Events.Length)); }
        public async Task<DomainProjectionRebuildPlan> PrepareStreamedRebuildAsync(ProjectionRequest header,
            IAsyncEnumerable<ProjectionEventDto> events, string operationId, CancellationToken cancellationToken)
        {
            int count = 0;
            await foreach (var item in events.WithCancellation(cancellationToken).ConfigureAwait(false)) count++;
            Calls++; return Plan(count);
        }
        private DomainProjectionRebuildPlan Plan(int count) => new("statestore",
            [ReadModelBatchOperation.Write("view-" + type, new[] { count }, ReadModelBatchConcurrency.LastWrite)]);
    }
}
