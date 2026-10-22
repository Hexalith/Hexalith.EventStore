using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

using Hexalith.EventStore.Client.Discovery;
using Hexalith.EventStore.Client.Projections;
using Hexalith.EventStore.Client.Tests.Projections;
using Hexalith.EventStore.Contracts.Projections;
using Hexalith.EventStore.ServiceDefaults.Authentication;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

using NSubstitute;

using Shouldly;

namespace Hexalith.EventStore.DomainService.Tests;

public sealed partial class DomainServiceTrustBoundaryTests
{
    [Theory]
    [InlineData("stage")]
    [InlineData("commit")]
    [InlineData("verify")]
    public async Task RecoveryBoundReplay_ActualSdkMappingsReuseOnePersistentManifestAcrossRetryAndFreshHosts(string retryAction)
    {
        using var backend = new RecordingDaprClient { PersistTypedState = true };
        var models = new DaprReadModelStore(backend);
        IAsyncDomainProjectionRebuildHandler handler = Substitute.For<IAsyncDomainProjectionRebuildHandler>();
        handler.Domain.Returns("counter");
        handler.ProjectionType.Returns("counter-detail");
        handler.OrdinaryDeliveryEnabled.Returns(true);
        handler.RebuildSemantics.Returns(DomainProjectionRebuildSemantics.FullReplay);
        int preparations = 0;
        handler.PrepareRebuildAsync(Arg.Any<ProjectionRequest>(), Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(async call =>
        {
            preparations++;
            ReadModelEntry<string> detail = await models.GetAsync<string>("models", "detail", call.ArgAt<CancellationToken>(2));
            ReadModelEntry<string> index = await models.GetAsync<string>("models", "index", call.ArgAt<CancellationToken>(2));
            return new DomainProjectionRebuildPlan("models",
            [ReadModelBatchOperation.Write("detail", "count:3", detail.Value is null ? ReadModelBatchConcurrency.CreateOnly : ReadModelBatchConcurrency.Match(detail.ETag!)),
                ReadModelBatchOperation.Write("index", "index:3", index.Value is null ? ReadModelBatchConcurrency.CreateOnly : ReadModelBatchConcurrency.Match(index.ETag!))]);
        });
        Action<IServiceCollection> configure = services =>
        {
            services.RemoveAll<IDomainProjectionHandler>();
            services.RemoveAll<IAsyncDomainProjectionHandler>();
            services.AddSingleton<IAsyncDomainProjectionHandler>(handler);
            services.AddSingleton<IReadModelStore>(models);
            services.AddSingleton<IReadModelBatchStagingStore>(models);
            services.AddSingleton(new DomainProjectionReplayRecoveryRegistration("counter", "counter-detail", "models"));
            services.Configure<DomainProjectionIdentityOptions>(options => { options.AppId = AppId; options.ServiceVersion = "v1"; });
        };
        ProjectionEventDto[] events = [.. Enumerable.Range(1, 3).Select(sequence => new ProjectionEventDto("CounterChanged", "{}"u8.ToArray(),
            "json", sequence, DateTimeOffset.UnixEpoch, "correlation", $"01J{sequence:D23}", "user"))];
        var route = new ProjectionDispatchRoute("counter", "counter-detail");
        string catalog = ProjectionRouteCatalogFingerprint.Compute(AppId, "v1", [route], [route]);
        var input = new ProjectionDispatchRequest(new("tenant-a", "counter", "counter-1", events)
        { RebuildSourceHeadSequence = 3 }, [route.ProjectionType], events[^1].MessageId!, catalog);
        string header;
        string manifestKey = DomainProjectionReplayRecovery.Key(input);
        byte[] original;
        await using (WebApplication app = await StartAsync(configureServices: configure))
        {
            var metadata = AdminOperationalIndexMetadata.Create(new DiscoveryResult([], []), ["counter"], [], [handler],
                AppId, "v1", new ProjectionDispatchOptions(), ["counter"]);
            metadata.CatalogFingerprint.ShouldBe(catalog);
            metadata = DomainProjectionReplayRecovery.BindMetadata(app.Services, metadata);
            header = JsonSerializer.Serialize(metadata.Domains.Single().ReplayRecoveryBindings!.ToDictionary(static pair => pair.Key,
                static pair => pair.Value.BindingFingerprint, StringComparer.Ordinal));
            await SendRecoveryActionAsync(app, "stage", input, header);
            original = backend.ByteStoreValue(manifestKey).ToArray();
            if (retryAction == "stage") await SendRecoveryActionAsync(app, "stage", input, header);
            await SendRecoveryActionAsync(app, "commit", input, header);
        }
        await using (WebApplication restarted = await StartAsync(configureServices: configure))
        {
            await SendRecoveryActionAsync(restarted, retryAction, input, header);
            await SendRecoveryActionAsync(restarted, "verify", input, header);
            backend.ByteStoreValue(manifestKey).ShouldBe(original);
            preparations.ShouldBe(1);
            (await models.GetAsync<string>("models", "detail")).Value.ShouldBe("count:3");
            (await models.GetAsync<string>("models", "index")).Value.ShouldBe("index:3");
            DomainProjectionReplayRecoveryManifest record = (await models.GetAsync<DomainProjectionReplayRecoveryManifest>("models", manifestKey)).Value.ShouldNotBeNull();
            record.Manifest.Length.ShouldBeGreaterThan(0);
            await SendRecoveryActionAsync(restarted, "reclaim-recovery", input with { Request = input.Request with { Events = [] } }, header);
            (await models.GetAsync<DomainProjectionReplayRecoveryManifest>("models", manifestKey)).Value.ShouldNotBeNull().Manifest.ShouldBeEmpty();
            using HttpRequestMessage delayed = RecoveryActionRequest("stage", input, header);
            using HttpResponseMessage rejected = await restarted.GetTestClient().SendAsync(delayed, TestContext.Current.CancellationToken);
            rejected.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
            preparations.ShouldBe(1);
        }
    }

    private static async Task SendRecoveryActionAsync(WebApplication app, string action, ProjectionDispatchRequest input, string binding)
    {
        using HttpRequestMessage request = RecoveryActionRequest(action, input, binding);
        using HttpResponseMessage response = await app.GetTestClient().SendAsync(request, TestContext.Current.CancellationToken);
        response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        ProjectionDispatchResponse result = (await response.Content.ReadFromJsonAsync<ProjectionDispatchResponse>(TestContext.Current.CancellationToken)).ShouldNotBeNull();
        result.Outcomes.ShouldAllBe(outcome => outcome.Status == ProjectionDispatchStatus.Completed);
    }

    private static HttpRequestMessage RecoveryActionRequest(string action, ProjectionDispatchRequest input, string binding)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/project/rebuild/" + action + "/v1") { Content = JsonContent.Create(input) };
        AddInternalHeaders(request, Assertion([EventStoreWorkloadOperations.DomainServiceProject]), [ChannelToken]);
        request.Headers.Add(ProjectionReplayRecoveryBinding.HeaderName, binding);
        request.Headers.Add("x-hexalith-projection-replay-context", JsonSerializer.Serialize(new
        { version = 1, headMessageId = input.DispatchId, ownerGenerations = (object?)null, ownerIdentities = (object?)null }));
        return request;
    }
}
