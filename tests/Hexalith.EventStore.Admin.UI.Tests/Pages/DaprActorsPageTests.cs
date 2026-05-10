using Bunit;

using Hexalith.EventStore.Admin.Abstractions.Models.Dapr;
using Hexalith.EventStore.Admin.UI.Pages;

using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

using NSubstitute;

namespace Hexalith.EventStore.Admin.UI.Tests.Pages;

/// <summary>
/// bUnit tests for the DaprActors page.
/// </summary>
public class DaprActorsPageTests : AdminUITestContext {
    private static readonly DaprActorRuntimeConfig _defaultConfig = new(
        TimeSpan.FromMinutes(60),
        TimeSpan.FromSeconds(30),
        TimeSpan.FromSeconds(60),
        true,
        false,
        32);

    private readonly AdminActorApiClient _mockApiClient;

    public DaprActorsPageTests() {
        _mockApiClient = Substitute.For<AdminActorApiClient>(
            Substitute.For<IHttpClientFactory>(),
            NullLogger<AdminActorApiClient>.Instance);
        _ = Services.AddScoped(_ => _mockApiClient);
    }

    [Fact]
    public void DaprActorsPage_RendersTitle() {
        _ = _mockApiClient.GetActorRuntimeInfoAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<DaprActorRuntimeInfo?>(CreateRuntimeInfo()));

        IRenderedComponent<DaprActors> cut = Render<DaprActors>();
        cut.WaitForAssertion(() => cut.Markup.ShouldContain("DAPR Actor Inspector"), TimeSpan.FromSeconds(5));

        cut.Markup.ShouldContain("DAPR Actor Inspector");
    }

    [Fact]
    public void DaprActorsPage_RendersBackLink() {
        _ = _mockApiClient.GetActorRuntimeInfoAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<DaprActorRuntimeInfo?>(CreateRuntimeInfo()));

        IRenderedComponent<DaprActors> cut = Render<DaprActors>();
        cut.WaitForAssertion(() => cut.Markup.ShouldContain("DAPR Infrastructure"), TimeSpan.FromSeconds(5));

        cut.Markup.ShouldContain("DAPR Infrastructure");
    }

    [Fact]
    public void DaprActorsPage_RendersStatCards() {
        _ = _mockApiClient.GetActorRuntimeInfoAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<DaprActorRuntimeInfo?>(CreateRuntimeInfo()));

        IRenderedComponent<DaprActors> cut = Render<DaprActors>();
        cut.WaitForAssertion(() => cut.Markup.ShouldContain("Registered Types"), TimeSpan.FromSeconds(5));

        string markup = cut.Markup;
        markup.ShouldContain("Registered Types");
        markup.ShouldContain("Total Active Actors");
        markup.ShouldContain("Inspected Actor State Size");
    }

    [Fact]
    public void DaprActorsPage_RendersPartialInventoryWithoutTotalLanguage_WhenCountsUnavailable() {
        DaprActorRuntimeInfo runtimeInfo = new(
            [
                new DaprActorTypeInfo("AggregateActor", -1, "Processes commands", "tenant:domain:id", DaprActorCountStatus.Unavailable, "RemoteEventStoreSidecarMetadata"),
                new DaprActorTypeInfo("ETagActor", 1, "Manages ETags", "ProjectionType:TenantId", DaprActorCountStatus.Exact, "RemoteEventStoreSidecarMetadata"),
                new DaprActorTypeInfo("ProjectionActor", -1, "Handles projections", "QueryType:TenantId", DaprActorCountStatus.Unavailable, "RemoteEventStoreSidecarMetadata"),
            ],
            1,
            _defaultConfig,
            RemoteMetadataStatus.Available,
            "http://localhost:3501",
            IsInventoryComplete: false,
            InventorySource: "RemoteEventStoreSidecarMetadata",
            InventoryMessage: "Partial actor inventory: some known EventStore actor type counts are unavailable.",
            ObservedAtUtc: DateTimeOffset.UtcNow,
            TotalKnownTypes: 3);
        _ = _mockApiClient.GetActorRuntimeInfoAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<DaprActorRuntimeInfo?>(runtimeInfo));

        IRenderedComponent<DaprActors> cut = Render<DaprActors>();
        cut.WaitForAssertion(() => cut.Markup.ShouldContain("Known Active Actors"), TimeSpan.FromSeconds(5));

        cut.Markup.ShouldContain("Known Active Actors");
        cut.Markup.ShouldContain("unavailable");
        cut.Markup.ShouldContain("Actor inventory is partial");
        cut.Markup.ShouldNotContain("Total Active Actors");
        cut.Markup.ShouldNotContain("N/A");
    }

    [Fact]
    public void DaprActorsPage_RendersActorTypeGrid() {
        _ = _mockApiClient.GetActorRuntimeInfoAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<DaprActorRuntimeInfo?>(CreateRuntimeInfo()));

        IRenderedComponent<DaprActors> cut = Render<DaprActors>();
        cut.WaitForAssertion(() => cut.Markup.ShouldContain("AggregateActor"), TimeSpan.FromSeconds(5));

        string markup = cut.Markup;
        markup.ShouldContain("AggregateActor");
        markup.ShouldContain("ETagActor");
        markup.ShouldContain("Type Name");
        markup.ShouldContain("Active Instances");
    }

    [Fact]
    public void DaprActorsPage_RendersConfigurationCard() {
        _ = _mockApiClient.GetActorRuntimeInfoAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<DaprActorRuntimeInfo?>(CreateRuntimeInfo()));

        IRenderedComponent<DaprActors> cut = Render<DaprActors>();
        cut.WaitForAssertion(() => cut.Markup.ShouldContain("Runtime Configuration"), TimeSpan.FromSeconds(5));

        string markup = cut.Markup;
        markup.ShouldContain("Idle Timeout");
        markup.ShouldContain("Scan Interval");
        markup.ShouldContain("Reentrancy Enabled");
        markup.ShouldContain("Defaults");
    }

    [Fact]
    public void DaprActorsPage_RendersLookupForm() {
        _ = _mockApiClient.GetActorRuntimeInfoAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<DaprActorRuntimeInfo?>(CreateRuntimeInfo()));

        IRenderedComponent<DaprActors> cut = Render<DaprActors>();
        cut.WaitForAssertion(() => cut.Markup.ShouldContain("Instance Lookup"), TimeSpan.FromSeconds(5));

        string markup = cut.Markup;
        markup.ShouldContain("Instance Lookup");
        markup.ShouldContain("Actor Type");
        markup.ShouldContain("Actor ID");
        markup.ShouldContain("Inspect");
    }

    [Fact]
    public void DaprActorsPage_RendersEmptyState_WhenNoActorTypes() {
        DaprActorRuntimeInfo emptyInfo = new([], 0, _defaultConfig, RemoteMetadataStatus.NotConfigured, null);
        _ = _mockApiClient.GetActorRuntimeInfoAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<DaprActorRuntimeInfo?>(emptyInfo));

        IRenderedComponent<DaprActors> cut = Render<DaprActors>();
        cut.WaitForAssertion(() => cut.Markup.ShouldContain("No actor types found"), TimeSpan.FromSeconds(5));

        cut.Markup.ShouldContain("No actor types found");
        cut.Markup.ShouldContain("EventStoreDaprHttpEndpoint");
    }

    [Fact]
    public void DaprActorsPage_RendersEmptyState_WhenNoActorsButMetadataAvailable() {
        DaprActorRuntimeInfo emptyInfo = new([], 0, _defaultConfig, RemoteMetadataStatus.Available, "http://localhost:3501");
        _ = _mockApiClient.GetActorRuntimeInfoAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<DaprActorRuntimeInfo?>(emptyInfo));

        IRenderedComponent<DaprActors> cut = Render<DaprActors>();
        cut.WaitForAssertion(() => cut.Markup.ShouldContain("No actor types registered"), TimeSpan.FromSeconds(5));

        cut.Markup.ShouldContain("No actor types registered");
    }

    [Fact]
    public void DaprActorsPage_RendersIssueBanner_WhenApiUnavailable() {
        _ = _mockApiClient.GetActorRuntimeInfoAsync(Arg.Any<CancellationToken>())
            .Returns<DaprActorRuntimeInfo?>(_ => throw new InvalidOperationException("API down"));

        IRenderedComponent<DaprActors> cut = Render<DaprActors>();
        cut.WaitForAssertion(() => cut.Markup.ShouldContain("Unable to load actor information"), TimeSpan.FromSeconds(5));

        cut.Markup.ShouldContain("Unable to load actor information");
    }

    [Fact]
    public void DaprActorsPage_RendersPlacementNote() {
        _ = _mockApiClient.GetActorRuntimeInfoAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<DaprActorRuntimeInfo?>(CreateRuntimeInfo()));

        IRenderedComponent<DaprActors> cut = Render<DaprActors>();
        cut.WaitForAssertion(() => cut.Markup.ShouldContain("consistent-hashing"), TimeSpan.FromSeconds(5));

        cut.Markup.ShouldContain("consistent-hashing");
    }

    [Fact]
    public void DaprActorsPage_RendersLookupUnavailableBanner_DifferentFromNotFound() {
        _ = _mockApiClient.GetActorRuntimeInfoAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<DaprActorRuntimeInfo?>(CreateRuntimeInfo()));
        _ = _mockApiClient.GetActorInstanceStateAsync("ETagActor", "counter:tenant-a", Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<DaprActorInstanceState?>(new DaprActorInstanceState(
                "ETagActor",
                "counter:tenant-a",
                [],
                0,
                DateTimeOffset.UtcNow,
                DaprActorLookupStatus.LookupUnavailable,
                "eventstore",
                "statestore",
                "DaprStateStoreActorKeys",
                "Actor lookup unavailable: state store read failed.")));

        Services.GetRequiredService<NavigationManager>().NavigateTo("/dapr/actors?type=ETagActor&id=counter%3Atenant-a");

        IRenderedComponent<DaprActors> cut = Render<DaprActors>();
        cut.WaitForAssertion(() => cut.Markup.ShouldContain("Actor lookup unavailable"), TimeSpan.FromSeconds(5));

        cut.Markup.ShouldContain("state store read failed");
        cut.Markup.ShouldNotContain("Actor instance not found");
    }

    [Fact]
    public async Task DaprActorsPage_DiscardsStaleInspectResult_WhenActorInputChangesBeforeResponse() {
        _ = _mockApiClient.GetActorRuntimeInfoAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<DaprActorRuntimeInfo?>(CreateRuntimeInfo()));
        TaskCompletionSource<DaprActorInstanceState?> staleLookup = new();
        _ = _mockApiClient.GetActorInstanceStateAsync("ETagActor", "counter:old", Arg.Any<CancellationToken>())
            .Returns(staleLookup.Task);

        IRenderedComponent<DaprActors> cut = Render<DaprActors>();
        cut.WaitForAssertion(() => cut.Markup.ShouldContain("Instance Lookup"), TimeSpan.FromSeconds(5));

        SetPrivateField(cut.Instance, "_selectedActorType", "ETagActor");
        SetPrivateField(cut.Instance, "_actorId", "counter:old");
        Task inspectTask = cut.InvokeAsync(() => InvokeInspectAsync(cut.Instance));

        SetPrivateField(cut.Instance, "_actorId", "counter:new");
        staleLookup.SetResult(new DaprActorInstanceState(
            "ETagActor",
            "counter:old",
            [],
            0,
            DateTimeOffset.UtcNow,
            DaprActorLookupStatus.NotFound,
            Message: "Actor instance not found for old input."));
        await inspectTask;

        cut.Markup.ShouldNotContain("Actor instance not found");
        cut.Markup.ShouldNotContain("old input");
    }

    // ===== Helper methods =====

    private static DaprActorRuntimeInfo CreateRuntimeInfo() => new(
        [
            new DaprActorTypeInfo("AggregateActor", 10, "Processes commands", "tenant:domain:id"),
            new DaprActorTypeInfo("ETagActor", 5, "Manages ETags", "ProjectionType:TenantId"),
        ],
        15,
        _defaultConfig,
        RemoteMetadataStatus.Available,
        "http://localhost:3501");

    private static void SetPrivateField(object instance, string name, object? value) {
        System.Reflection.FieldInfo field = typeof(DaprActors)
            .GetField(name, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
            ?? throw new InvalidOperationException($"Field {name} was not found.");
        field.SetValue(instance, value);
    }

    private static Task InvokeInspectAsync(DaprActors instance) {
        System.Reflection.MethodInfo method = typeof(DaprActors)
            .GetMethod("InspectActorAsync", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("InspectActorAsync was not found.");
        return (Task)(method.Invoke(instance, []) ?? throw new InvalidOperationException("InspectActorAsync returned null."));
    }
}
