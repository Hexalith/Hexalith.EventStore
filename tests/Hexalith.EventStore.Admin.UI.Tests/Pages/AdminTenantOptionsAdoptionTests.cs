using Bunit;

using Hexalith.EventStore.Admin.Abstractions.Models.Commands;
using Hexalith.EventStore.Admin.Abstractions.Models.Projections;
using Hexalith.EventStore.Admin.Abstractions.Models.Streams;
using Hexalith.EventStore.Admin.UI.Pages;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

using NSubstitute;

namespace Hexalith.EventStore.Admin.UI.Tests.Pages;

/// <summary>
/// bUnit smoke tests proving each filter-page consumes the shared AdminTenantOptionsProvider
/// and surfaces its outcome — including the canonical empty-state copy and partial-failure diagnostic —
/// without falling back to a per-page tenant fetch.
/// </summary>
public class AdminTenantOptionsAdoptionTests : AdminUITestContext {
    private readonly AdminStreamApiClient _streamApi;
    private readonly AdminProjectionApiClient _projectionApi;
    private readonly AdminTenantOptionsProvider _provider;

    public AdminTenantOptionsAdoptionTests() {
        _streamApi = Substitute.For<AdminStreamApiClient>(
            Substitute.For<IHttpClientFactory>(),
            NullLogger<AdminStreamApiClient>.Instance);
        _ = Services.AddScoped(_ => _streamApi);

        _projectionApi = Substitute.For<AdminProjectionApiClient>(
            Substitute.For<IHttpClientFactory>(),
            NullLogger<AdminProjectionApiClient>.Instance);
        _ = Services.AddScoped(_ => _projectionApi);

        _provider = Substitute.For<AdminTenantOptionsProvider>(
            Substitute.For<AdminTenantApiClient>(
                Substitute.For<IHttpClientFactory>(),
                NullLogger<AdminTenantApiClient>.Instance),
            Substitute.For<AdminStreamApiClient>(
                Substitute.For<IHttpClientFactory>(),
                NullLogger<AdminStreamApiClient>.Instance),
            NullLogger<AdminTenantOptionsProvider>.Instance);
        _ = Services.AddScoped(_ => _provider);

        // Default empty stream/command/projection responses so each page can render without arranging data.
        _ = _streamApi.GetRecentlyActiveStreamsAsync(
                Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new PagedResult<StreamSummary>([], 0, null)));
        _ = _streamApi.GetRecentCommandsAsync(
                Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new PagedResult<CommandSummary>([], 0, null)));
        _ = _projectionApi.ListProjectionsAsync(Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<ProjectionStatus>>([]));
    }

    private void StubProvider(TenantOptionsResult result) =>
        _provider.GetTenantOptionsAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(result));

    private static TenantOptionsResult Loaded(params (string Id, string Name, TenantProvenance Provenance)[] options) {
        IReadOnlyList<TenantOption> opts = [.. options.Select(o => new TenantOption(o.Id, o.Name, o.Provenance))];
        return new TenantOptionsResult(opts, TenantOptionsLoadStatus.Loaded, null);
    }

    private static TenantOptionsResult Empty() =>
        new([], TenantOptionsLoadStatus.Empty, AdminTenantOptionsProvider.EmptyMessage);

    private static TenantOptionsResult Partial(params (string Id, string Name, TenantProvenance Provenance)[] options) {
        IReadOnlyList<TenantOption> opts = [.. options.Select(o => new TenantOption(o.Id, o.Name, o.Provenance))];
        return new TenantOptionsResult(opts, TenantOptionsLoadStatus.Partial,
            "Tenant directory is partially loaded; some sources are temporarily unavailable.");
    }

    // ---- /streams ----

    [Fact]
    public void StreamsPage_RegisteredAndObservedTenants_BothAppearInDropdown() {
        StubProvider(Loaded(
            ("tenant-a", "Acme", TenantProvenance.Registered),
            ("tenant-b", "tenant-b", TenantProvenance.ObservedOnly)));

        IRenderedComponent<Streams> cut = Render<Streams>();

        cut.WaitForAssertion(
            () => cut.Markup.ShouldContain("Acme"),
            TimeSpan.FromSeconds(5));
        cut.Markup.ShouldContain("tenant-b");
    }

    [Fact]
    public void StreamsPage_EmptyTenantOptions_ShowsCanonicalEmptyMessage() {
        StubProvider(Empty());

        IRenderedComponent<Streams> cut = Render<Streams>();

        cut.WaitForAssertion(
            () => cut.Markup.ShouldContain(AdminTenantOptionsProvider.EmptyMessage),
            TimeSpan.FromSeconds(5));
    }

    [Fact]
    public void StreamsPage_PartialOutcome_RendersDiagnostic() {
        StubProvider(Partial(("tenant-a", "Acme", TenantProvenance.Registered)));

        IRenderedComponent<Streams> cut = Render<Streams>();

        cut.WaitForAssertion(
            () => cut.Markup.ShouldContain("partially loaded"),
            TimeSpan.FromSeconds(5));
        cut.Markup.ShouldContain("Acme");
    }

    [Fact]
    public void StreamsPage_DoesNotCallLegacyGetTenantsAsync() {
        StubProvider(Loaded(("tenant-a", "Acme", TenantProvenance.Registered)));

        _ = Render<Streams>();

        // Page must rely on the shared provider, not the legacy AdminStreamApiClient.GetTenantsAsync().
        _ = _streamApi.DidNotReceive().GetTenantsAsync(Arg.Any<CancellationToken>());
    }

    // ---- /commands ----

    [Fact]
    public void CommandsPage_RegisteredAndObservedTenants_BothAppearInDropdown() {
        StubProvider(Loaded(
            ("tenant-a", "Acme", TenantProvenance.Registered),
            ("tenant-b", "tenant-b", TenantProvenance.ObservedOnly)));

        IRenderedComponent<Commands> cut = Render<Commands>();

        cut.WaitForAssertion(
            () => cut.Markup.ShouldContain("Acme"),
            TimeSpan.FromSeconds(5));
        cut.Markup.ShouldContain("tenant-b");
    }

    [Fact]
    public void CommandsPage_EmptyTenantOptions_ShowsCanonicalEmptyMessage() {
        StubProvider(Empty());

        IRenderedComponent<Commands> cut = Render<Commands>();

        cut.WaitForAssertion(
            () => cut.Markup.ShouldContain(AdminTenantOptionsProvider.EmptyMessage),
            TimeSpan.FromSeconds(5));
    }

    [Fact]
    public void CommandsPage_DoesNotCallLegacyGetTenantsAsync() {
        StubProvider(Loaded(("tenant-a", "Acme", TenantProvenance.Registered)));

        _ = Render<Commands>();

        _ = _streamApi.DidNotReceive().GetTenantsAsync(Arg.Any<CancellationToken>());
    }

    // ---- /events ----

    [Fact]
    public void EventsPage_RegisteredAndObservedTenants_BothAppearInDropdown() {
        StubProvider(Loaded(
            ("tenant-a", "Acme", TenantProvenance.Registered),
            ("tenant-b", "tenant-b", TenantProvenance.ObservedOnly)));

        IRenderedComponent<Events> cut = Render<Events>();

        cut.WaitForAssertion(
            () => cut.Markup.ShouldContain("Acme"),
            TimeSpan.FromSeconds(5));
        cut.Markup.ShouldContain("tenant-b");
    }

    [Fact]
    public void EventsPage_EmptyTenantOptions_ShowsCanonicalEmptyMessage() {
        StubProvider(Empty());

        IRenderedComponent<Events> cut = Render<Events>();

        cut.WaitForAssertion(
            () => cut.Markup.ShouldContain(AdminTenantOptionsProvider.EmptyMessage),
            TimeSpan.FromSeconds(5));
    }

    [Fact]
    public void EventsPage_DoesNotCallLegacyGetTenantsAsync() {
        StubProvider(Loaded(("tenant-a", "Acme", TenantProvenance.Registered)));

        _ = Render<Events>();

        _ = _streamApi.DidNotReceive().GetTenantsAsync(Arg.Any<CancellationToken>());
    }

    // ---- /projections ----

    [Fact]
    public void ProjectionsPage_AdoptsSharedProvider_RendersTenantOptions() {
        StubProvider(Loaded(
            ("tenant-a", "Acme", TenantProvenance.Registered),
            ("tenant-b", "tenant-b", TenantProvenance.ObservedOnly)));

        IRenderedComponent<Projections> cut = Render<Projections>();

        cut.WaitForAssertion(
            () => _provider.Received().GetTenantOptionsAsync(Arg.Any<CancellationToken>()),
            TimeSpan.FromSeconds(5));
    }

    [Fact]
    public void ProjectionsPage_EmptyTenantOptions_ShowsCanonicalEmptyMessage() {
        StubProvider(Empty());

        IRenderedComponent<Projections> cut = Render<Projections>();

        cut.WaitForAssertion(
            () => cut.Markup.ShouldContain(AdminTenantOptionsProvider.EmptyMessage),
            TimeSpan.FromSeconds(5));
    }
}
