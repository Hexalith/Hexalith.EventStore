using Bunit;

using Hexalith.EventStore.Admin.UI.Services;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

using NSubstitute;
using NSubstitute.ExceptionExtensions;

namespace Hexalith.EventStore.Admin.UI.Tests.Pages;

/// <summary>
/// Test 9.10: Each stub page renders with correct PageTitle and EmptyState content (AC: 8, 9).
/// Merge-blocking test.
/// </summary>
public class StubPageTests : AdminUITestContext {
    [Fact]
    public void CommandsPage_RendersCorrectContent() {
        AdminStreamApiClient mockStreamApi = Substitute.For<AdminStreamApiClient>(
            Substitute.For<IHttpClientFactory>(),
            NullLogger<AdminStreamApiClient>.Instance);
        _ = mockStreamApi.GetRecentCommandsAsync(
            Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new PagedResult<Hexalith.EventStore.Admin.Abstractions.Models.Commands.CommandSummary>([], 0, null)));
        _ = mockStreamApi.GetTenantsAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<Hexalith.EventStore.Admin.Abstractions.Models.Tenants.TenantSummary>>([]));
        Services.AddScoped(_ => mockStreamApi);

        IRenderedComponent<Hexalith.EventStore.Admin.UI.Pages.Commands> cut = Render<Hexalith.EventStore.Admin.UI.Pages.Commands>();
        cut.WaitForAssertion(() => cut.Markup.ShouldContain("No commands processed yet"), TimeSpan.FromSeconds(5));
    }

    [Fact]
    public void EventsPage_RendersCorrectContent() {
        AdminStreamApiClient mockStreamApi = Substitute.For<AdminStreamApiClient>(
            Substitute.For<IHttpClientFactory>(),
            NullLogger<AdminStreamApiClient>.Instance);
        _ = mockStreamApi.GetRecentlyActiveStreamsAsync(
            Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new PagedResult<Hexalith.EventStore.Admin.Abstractions.Models.Streams.StreamSummary>([], 0, null)));
        _ = mockStreamApi.GetTenantsAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<Hexalith.EventStore.Admin.Abstractions.Models.Tenants.TenantSummary>>([]));
        Services.AddScoped(_ => mockStreamApi);

        IRenderedComponent<Hexalith.EventStore.Admin.UI.Pages.Events> cut = Render<Hexalith.EventStore.Admin.UI.Pages.Events>();
        cut.WaitForAssertion(() => cut.Markup.ShouldContain("No events recorded yet"), TimeSpan.FromSeconds(5));
    }

    [Fact]
    public void HealthPage_RendersCorrectContent() {
        IRenderedComponent<Hexalith.EventStore.Admin.UI.Pages.Health> cut = Render<Hexalith.EventStore.Admin.UI.Pages.Health>();
        cut.WaitForAssertion(() => cut.Markup.ShouldContain("Health"), TimeSpan.FromSeconds(5));
        cut.Markup.ShouldContain("Refresh");
    }

    [Fact]
    public void DeadLettersPage_RendersCorrectContent() {
        // Register AdminDeadLetterApiClient (DeadLetters page is now a full implementation, not a stub)
        AdminDeadLetterApiClient mockDeadLetterApi = Substitute.For<AdminDeadLetterApiClient>(
            Substitute.For<IHttpClientFactory>(),
            NullLogger<AdminDeadLetterApiClient>.Instance);
        _ = mockDeadLetterApi.GetDeadLettersAsync(
            Arg.Any<string?>(), Arg.Any<int>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new PagedResult<Hexalith.EventStore.Admin.Abstractions.Models.DeadLetters.DeadLetterEntry>([], 0, null)));
        Services.AddScoped(_ => mockDeadLetterApi);

        IRenderedComponent<Hexalith.EventStore.Admin.UI.Pages.DeadLetters> cut = Render<Hexalith.EventStore.Admin.UI.Pages.DeadLetters>();
        cut.WaitForAssertion(() => cut.Markup.ShouldContain("No dead letters. All commands processed successfully."), TimeSpan.FromSeconds(5));
    }

    [Fact]
    public void TenantsPage_RendersCorrectContent() {
        // Register AdminTenantApiClient (Tenants page is now a full implementation, not a stub)
        AdminTenantApiClient mockTenantApi = Substitute.For<AdminTenantApiClient>(
            Substitute.For<IHttpClientFactory>(),
            NullLogger<AdminTenantApiClient>.Instance);
        _ = mockTenantApi.ListTenantsAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<Hexalith.EventStore.Admin.Abstractions.Models.Tenants.TenantSummary>>([]));
        Services.AddScoped(_ => mockTenantApi);

        IRenderedComponent<Hexalith.EventStore.Admin.UI.Pages.Tenants> cut = Render<Hexalith.EventStore.Admin.UI.Pages.Tenants>();
        cut.WaitForAssertion(() => cut.Markup.ShouldContain("No tenants configured"), TimeSpan.FromSeconds(5));
        cut.Markup.ShouldContain("No tenants configured");
    }

    [Fact]
    public void ServicesPage_RendersCorrectContent() {
        IRenderedComponent<Hexalith.EventStore.Admin.UI.Pages.Services> cut = Render<Hexalith.EventStore.Admin.UI.Pages.Services>();
        cut.Markup.ShouldContain("EventStore is running. 0 domain services connected.");
        cut.Markup.ShouldContain("Read the domain service registration guide");
    }

    [Fact]
    public void SettingsPage_RendersCorrectContent() {
        IRenderedComponent<Hexalith.EventStore.Admin.UI.Pages.Settings> cut = Render<Hexalith.EventStore.Admin.UI.Pages.Settings>();
        cut.Markup.ShouldContain("Configure admin dashboard preferences.");
    }

    [Fact]
    public void LandingPage_RendersStatCardGrid() {
        IRenderedComponent<Hexalith.EventStore.Admin.UI.Pages.Index> cut = Render<Hexalith.EventStore.Admin.UI.Pages.Index>();

        // Landing page always renders the stat card grid
        cut.Markup.ShouldContain("stat-card-grid");
    }
}
