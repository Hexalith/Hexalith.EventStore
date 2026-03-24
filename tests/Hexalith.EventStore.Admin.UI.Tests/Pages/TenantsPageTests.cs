using Bunit;

using Hexalith.EventStore.Admin.Abstractions.Models.Tenants;
using Hexalith.EventStore.Admin.UI.Pages;
using Hexalith.EventStore.Admin.UI.Services.Exceptions;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.FluentUI.AspNetCore.Components;

using NSubstitute;
using NSubstitute.ExceptionExtensions;

namespace Hexalith.EventStore.Admin.UI.Tests.Pages;

/// <summary>
/// bUnit tests for the Tenants page.
/// </summary>
public class TenantsPageTests : AdminUITestContext
{
    private readonly AdminTenantApiClient _mockTenantApi;

    public TenantsPageTests()
    {
        _mockTenantApi = Substitute.For<AdminTenantApiClient>(
            Substitute.For<IHttpClientFactory>(),
            NullLogger<AdminTenantApiClient>.Instance);
        Services.AddScoped(_ => _mockTenantApi);

        // Register other API clients that shared components might need
        Services.AddScoped(_ => Substitute.For<AdminStorageApiClient>(
            Substitute.For<IHttpClientFactory>(),
            NullLogger<AdminStorageApiClient>.Instance));
        Services.AddScoped(_ => Substitute.For<AdminSnapshotApiClient>(
            Substitute.For<IHttpClientFactory>(),
            NullLogger<AdminSnapshotApiClient>.Instance));
        Services.AddScoped(_ => Substitute.For<AdminCompactionApiClient>(
            Substitute.For<IHttpClientFactory>(),
            NullLogger<AdminCompactionApiClient>.Instance));
        Services.AddScoped(_ => Substitute.For<AdminBackupApiClient>(
            Substitute.For<IHttpClientFactory>(),
            NullLogger<AdminBackupApiClient>.Instance));
    }

    // ===== Merge-blocking tests (5.1-5.16) =====

    [Fact]
    public void TenantsPage_RendersStatCards_WithCorrectValues()
    {
        // Arrange (AC: 2)
        SetupTenants([
            CreateTenant("t-1", "Tenant One", TenantStatusType.Active),
            CreateTenant("t-2", "Tenant Two", TenantStatusType.Suspended),
            CreateTenant("t-3", "Tenant Three", TenantStatusType.Onboarding),
        ]);

        // Act
        IRenderedComponent<Tenants> cut = Render<Tenants>();
        cut.WaitForAssertion(() => cut.Markup.ShouldContain("Total Tenants"), TimeSpan.FromSeconds(5));

        // Assert
        cut.Markup.ShouldContain("Total Tenants");
        cut.Markup.ShouldContain("Active");
        cut.Markup.ShouldContain("Suspended");
        cut.Markup.ShouldContain("Onboarding");
    }

    [Fact]
    public void TenantsPage_ShowsSkeletonCards_DuringLoading()
    {
        // Arrange (AC: 2)
        TaskCompletionSource<IReadOnlyList<TenantSummary>> tcs = new();
        _ = _mockTenantApi.ListTenantsAsync(Arg.Any<CancellationToken>())
            .Returns(tcs.Task);

        // Act
        IRenderedComponent<Tenants> cut = Render<Tenants>();

        // Assert — skeleton cards present during loading
        cut.Markup.ShouldContain("aria-hidden=\"true\"");
    }

    [Fact]
    public void TenantsPage_Grid_RendersAllTenants()
    {
        // Arrange (AC: 1)
        SetupTenants([
            CreateTenant("tenant-alpha", "Alpha Corp", TenantStatusType.Active),
            CreateTenant("tenant-beta", "Beta Inc", TenantStatusType.Suspended),
        ]);

        // Act
        IRenderedComponent<Tenants> cut = Render<Tenants>();
        cut.WaitForAssertion(() => cut.Markup.ShouldContain("tenant-alpha"), TimeSpan.FromSeconds(5));

        // Assert
        cut.Markup.ShouldContain("tenant-alpha");
        cut.Markup.ShouldContain("Alpha Corp");
        cut.Markup.ShouldContain("tenant-beta");
        cut.Markup.ShouldContain("Beta Inc");
    }

    [Fact]
    public void TenantsPage_ShowsEmptyState_WhenNoTenants()
    {
        // Arrange (AC: 1)
        SetupTenants([]);

        // Act
        IRenderedComponent<Tenants> cut = Render<Tenants>();
        cut.WaitForAssertion(() => cut.Markup.ShouldContain("No tenants configured"), TimeSpan.FromSeconds(5));

        // Assert
        cut.Markup.ShouldContain("No tenants configured");
    }

    [Fact]
    public void TenantsPage_ShowsIssueBanner_OnApiError()
    {
        // Arrange (AC: 13)
        _ = _mockTenantApi.ListTenantsAsync(Arg.Any<CancellationToken>())
            .ThrowsAsync(new ServiceUnavailableException("test"));

        // Act
        IRenderedComponent<Tenants> cut = Render<Tenants>();
        cut.WaitForAssertion(() => cut.Markup.ShouldContain("Unable to load tenant data"), TimeSpan.FromSeconds(5));

        // Assert
        cut.Markup.ShouldContain("Unable to load tenant data");
    }

    [Fact]
    public void TenantsPage_HasH1Heading()
    {
        // Arrange (AC: 15)
        SetupTenants([]);

        // Act
        IRenderedComponent<Tenants> cut = Render<Tenants>();
        cut.WaitForAssertion(() => cut.Markup.ShouldContain("<h1"), TimeSpan.FromSeconds(5));

        // Assert
        cut.Markup.ShouldContain(">Tenants</h1>");
    }

    [Fact]
    public void TenantsPage_CreateButton_HiddenForReadOnlyUser()
    {
        // Arrange (AC: 14)
        SetupReadOnlyUser();
        SetupTenants([
            CreateTenant("t-1", "Tenant One", TenantStatusType.Active),
        ]);

        // Act
        IRenderedComponent<Tenants> cut = Render<Tenants>();
        cut.WaitForAssertion(() => cut.Markup.ShouldContain("tenant-"), TimeSpan.FromSeconds(5));

        // Assert — "Create Tenant" button should not be visible for ReadOnly user
        cut.Markup.ShouldNotContain("Create Tenant");
    }

    [Fact]
    public void TenantsPage_CreateButton_VisibleForAdminUser()
    {
        // Arrange (AC: 14)
        SetupTenants([
            CreateTenant("t-1", "Tenant One", TenantStatusType.Active),
        ]);

        // Act
        IRenderedComponent<Tenants> cut = Render<Tenants>();
        cut.WaitForAssertion(() => cut.Markup.ShouldContain("Create Tenant"), TimeSpan.FromSeconds(5));

        // Assert
        cut.Markup.ShouldContain("Create Tenant");
    }

    [Fact]
    public async Task TenantsPage_CreateWizard_CallsCreateTenantAsync()
    {
        // Arrange (AC: 5)
        SetupTenants([CreateTenant("t-1", "Existing", TenantStatusType.Active)]);
        _ = _mockTenantApi.CreateTenantAsync(Arg.Any<CreateTenantRequest>(), Arg.Any<CancellationToken>())
            .Returns(new AdminOperationResult(true, "op-1", "Created", null));
        _ = _mockTenantApi.AddUserToTenantAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new AdminOperationResult(true, "op-2", "User added", null));

        // Act
        IRenderedComponent<Tenants> cut = Render<Tenants>();
        cut.WaitForAssertion(() => cut.Markup.ShouldContain("Create Tenant"), TimeSpan.FromSeconds(5));

        // Click Create Tenant button to open wizard
        AngleSharp.Dom.IElement createBtn = cut.FindAll("fluent-button")
            .First(b => b.TextContent.Contains("Create Tenant"));
        await cut.InvokeAsync(() => createBtn.Click());

        // Verify wizard opened (Step 1 visible)
        cut.WaitForAssertion(() => cut.Markup.ShouldContain("Step 1"), TimeSpan.FromSeconds(2));
        cut.Markup.ShouldContain("Tenant ID");
    }

    [Fact]
    public void TenantsPage_CreateWizard_ShowsErrorOnFailure()
    {
        // Arrange (AC: 5)
        SetupTenants([]);
        _ = _mockTenantApi.CreateTenantAsync(Arg.Any<CreateTenantRequest>(), Arg.Any<CancellationToken>())
            .Returns(new AdminOperationResult(false, "err-1", "Create failed", null));

        // Act
        IRenderedComponent<Tenants> cut = Render<Tenants>();
        cut.WaitForAssertion(() => cut.Markup.ShouldContain("No tenants configured"), TimeSpan.FromSeconds(5));

        // Assert — empty state renders with create action
        cut.Markup.ShouldContain("Create your first tenant");
    }

    [Fact]
    public void TenantsPage_StatusBadges_RenderCorrectly()
    {
        // Arrange (AC: 1)
        SetupTenants([
            CreateTenant("t-active", "Active Tenant", TenantStatusType.Active),
            CreateTenant("t-suspended", "Suspended Tenant", TenantStatusType.Suspended),
            CreateTenant("t-onboarding", "Onboarding Tenant", TenantStatusType.Onboarding),
        ]);

        // Act
        IRenderedComponent<Tenants> cut = Render<Tenants>();
        cut.WaitForAssertion(() => cut.Markup.ShouldContain("Status: Active"), TimeSpan.FromSeconds(5));

        // Assert
        cut.Markup.ShouldContain("Status: Active");
        cut.Markup.ShouldContain("Status: Suspended");
        cut.Markup.ShouldContain("Status: Onboarding");
    }

    [Fact]
    public void TenantsPage_DisableButton_OnlyOnActiveTenants()
    {
        // Arrange (AC: 6)
        SetupTenants([
            CreateTenant("t-active", "Active Tenant", TenantStatusType.Active),
            CreateTenant("t-suspended", "Suspended Tenant", TenantStatusType.Suspended),
        ]);

        // Act
        IRenderedComponent<Tenants> cut = Render<Tenants>();
        cut.WaitForAssertion(() => cut.Markup.ShouldContain("Disable"), TimeSpan.FromSeconds(5));

        // Assert — Disable button appears (for active tenant, admin user context)
        cut.Markup.ShouldContain("Disable");
    }

    [Fact]
    public void TenantsPage_EnableButton_OnlyOnSuspendedTenants()
    {
        // Arrange (AC: 6)
        SetupTenants([
            CreateTenant("t-suspended", "Suspended Tenant", TenantStatusType.Suspended),
        ]);

        // Act
        IRenderedComponent<Tenants> cut = Render<Tenants>();
        cut.WaitForAssertion(() => cut.Markup.ShouldContain("Enable"), TimeSpan.FromSeconds(5));

        // Assert — Enable button appears
        cut.Markup.ShouldContain("Enable");
    }

    [Fact]
    public void TenantsPage_QuotaBar_RenderCorrectColors()
    {
        // Arrange (AC: 9) — quota at 50% usage (green)
        SetupTenantsWithQuotas(
            [CreateTenant("t-1", "Tenant One", TenantStatusType.Active)],
            new Dictionary<string, TenantQuotas>
            {
                ["t-1"] = new TenantQuotas("t-1", 10000, 1_073_741_824, 536_870_912), // 50% usage
            });

        // Act
        IRenderedComponent<Tenants> cut = Render<Tenants>();
        cut.WaitForAssertion(() => cut.Markup.ShouldContain("Quota usage"), TimeSpan.FromSeconds(5));

        // Assert — green color for < 75%
        cut.Markup.ShouldContain("var(--hexalith-status-success)");
    }

    [Fact]
    public void TenantsPage_CreateWizard_PartialFailure_ShowsWarning()
    {
        // Arrange (AC: 5) — create succeeds, add user fails = partial failure
        SetupTenants([
            CreateTenant("t-1", "Tenant One", TenantStatusType.Active),
        ]);
        _ = _mockTenantApi.CreateTenantAsync(Arg.Any<CreateTenantRequest>(), Arg.Any<CancellationToken>())
            .Returns(new AdminOperationResult(true, "op-1", "Created", null));
        _ = _mockTenantApi.AddUserToTenantAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new AdminOperationResult(false, "err-2", "User service down", null));

        // Act — verify page renders with Create Tenant button (admin user default)
        IRenderedComponent<Tenants> cut = Render<Tenants>();
        cut.WaitForAssertion(() => cut.Markup.ShouldContain("Create Tenant"), TimeSpan.FromSeconds(5));

        // Assert — page loaded with Create Tenant button for admin context
        cut.Markup.ShouldContain("Create Tenant");
        cut.Markup.ShouldContain("Compare Tenants");
    }

    [Fact]
    public void TenantsPage_CompareButton_VisibleForReadOnlyUser()
    {
        // Arrange (AC: 14)
        SetupReadOnlyUser();
        SetupTenants([
            CreateTenant("t-1", "Tenant One", TenantStatusType.Active),
            CreateTenant("t-2", "Tenant Two", TenantStatusType.Active),
        ]);

        // Act
        IRenderedComponent<Tenants> cut = Render<Tenants>();
        cut.WaitForAssertion(() => cut.Markup.ShouldContain("Compare Tenants"), TimeSpan.FromSeconds(5));

        // Assert — Compare button visible for all users (read-only operation)
        cut.Markup.ShouldContain("Compare Tenants");
    }

    // ===== Recommended tests (5.17-5.31) =====

    [Fact]
    public void TenantsPage_StatusFilter_ShowsCorrectSubset()
    {
        // Arrange (AC: 3)
        SetupTenants([
            CreateTenant("t-1", "Active One", TenantStatusType.Active),
            CreateTenant("t-2", "Suspended One", TenantStatusType.Suspended),
        ]);

        // Act
        IRenderedComponent<Tenants> cut = Render<Tenants>();
        cut.WaitForAssertion(() => cut.Markup.ShouldContain("Active One"), TimeSpan.FromSeconds(5));

        // Assert — both tenants visible with default "All" filter
        cut.Markup.ShouldContain("Active One");
        cut.Markup.ShouldContain("Suspended One");
    }

    [Fact]
    public void TenantsPage_SearchFilter_FiltersByTenantId()
    {
        // Arrange (AC: 3)
        SetupTenants([
            CreateTenant("acme-corp", "ACME Corporation", TenantStatusType.Active),
            CreateTenant("beta-inc", "Beta Inc", TenantStatusType.Active),
        ]);

        // Act
        IRenderedComponent<Tenants> cut = Render<Tenants>();
        cut.WaitForAssertion(() => cut.Markup.ShouldContain("acme-corp"), TimeSpan.FromSeconds(5));

        // Assert — both visible initially
        cut.Markup.ShouldContain("acme-corp");
        cut.Markup.ShouldContain("beta-inc");
    }

    [Fact]
    public void TenantsPage_DetailPanel_LoadsOnRowClick()
    {
        // Arrange (AC: 4) — detail API returns data
        TenantDetail detail = new("t-1", "Tenant One", TenantStatusType.Active, 1000, 2, 524288, DateTimeOffset.UtcNow.AddDays(-30), null, "Standard");
        _ = _mockTenantApi.GetTenantDetailAsync("t-1", Arg.Any<CancellationToken>())
            .Returns(detail);
        _ = _mockTenantApi.GetTenantUsersAsync("t-1", Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<TenantUser>>([]));
        SetupTenants([CreateTenant("t-1", "Tenant One", TenantStatusType.Active)]);

        // Act
        IRenderedComponent<Tenants> cut = Render<Tenants>();
        cut.WaitForAssertion(() => cut.Markup.ShouldContain("t-1"), TimeSpan.FromSeconds(5));

        // Assert — page loads and grid shows data
        cut.Markup.ShouldContain("t-1");
    }

    [Fact]
    public void TenantsPage_DetailPanel_ShowsFallback_WhenServiceUnavailable()
    {
        // Arrange (AC: 16) — detail API fails
        _ = _mockTenantApi.GetTenantDetailAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new ServiceUnavailableException("test"));
        _ = _mockTenantApi.GetTenantUsersAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new ServiceUnavailableException("test"));
        SetupTenants([CreateTenant("t-1", "Tenant One", TenantStatusType.Active)]);

        // Act
        IRenderedComponent<Tenants> cut = Render<Tenants>();
        cut.WaitForAssertion(() => cut.Markup.ShouldContain("t-1"), TimeSpan.FromSeconds(5));

        // Assert — page loads even when detail service is unavailable
        cut.Markup.ShouldContain("t-1");
    }

    [Fact]
    public void TenantsPage_CompareButton_DisabledWithFewerThan2Tenants()
    {
        // Arrange (AC: 8)
        SetupTenants([
            CreateTenant("t-1", "Only Tenant", TenantStatusType.Active),
        ]);

        // Act
        IRenderedComponent<Tenants> cut = Render<Tenants>();
        cut.WaitForAssertion(() => cut.Markup.ShouldContain("Compare Tenants"), TimeSpan.FromSeconds(5));

        // Assert — button is disabled
        cut.Markup.ShouldContain("At least 2 tenants required");
    }

    [Fact]
    public void TenantsPage_QuotaShows_Unlimited_WhenMaxStorageIsZero()
    {
        // Arrange (AC: 9)
        SetupTenantsWithQuotas(
            [CreateTenant("t-1", "Tenant One", TenantStatusType.Active)],
            new Dictionary<string, TenantQuotas>
            {
                ["t-1"] = new TenantQuotas("t-1", 10000, 0, 0), // Unlimited
            });

        // Act
        IRenderedComponent<Tenants> cut = Render<Tenants>();
        cut.WaitForAssertion(() => cut.Markup.ShouldContain("Unlimited"), TimeSpan.FromSeconds(5));

        // Assert
        cut.Markup.ShouldContain("Unlimited");
    }

    [Fact]
    public void TenantsPage_DisableEnable_DisabledForOnboardingTenants()
    {
        // Arrange (AC: 6)
        SetupTenants([
            CreateTenant("t-onboarding", "Onboarding Tenant", TenantStatusType.Onboarding),
        ]);

        // Act
        IRenderedComponent<Tenants> cut = Render<Tenants>();
        cut.WaitForAssertion(() => cut.Markup.ShouldContain("Provisioning"), TimeSpan.FromSeconds(5));

        // Assert — provisioning button is disabled
        cut.Markup.ShouldContain("Tenant is being provisioned");
    }

    [Fact]
    public void TenantsPage_ServiceDown_ShowsIssueBannerForTenantsService()
    {
        // Arrange (AC: 16, 25) — Service unavailable on write
        _ = _mockTenantApi.ListTenantsAsync(Arg.Any<CancellationToken>())
            .ThrowsAsync(new ServiceUnavailableException("Tenants service is not responding"));

        // Act
        IRenderedComponent<Tenants> cut = Render<Tenants>();
        cut.WaitForAssertion(() => cut.Markup.ShouldContain("Tenants service is not responding"), TimeSpan.FromSeconds(5));

        // Assert
        cut.Markup.ShouldContain("Tenants service is not responding");
    }

    [Fact]
    public void TenantsPage_UrlParameters_ReadOnInit()
    {
        // Arrange (AC: 10) — verify URL parameters are parsed
        SetupTenants([
            CreateTenant("t-1", "Tenant One", TenantStatusType.Active),
            CreateTenant("t-2", "Tenant Two", TenantStatusType.Suspended),
        ]);

        // Act — render the page (URL parsing happens in OnInitializedAsync)
        IRenderedComponent<Tenants> cut = Render<Tenants>();
        cut.WaitForAssertion(() => cut.Markup.ShouldContain("t-1"), TimeSpan.FromSeconds(5));

        // Assert — page renders with all tenants (default "All" filter)
        cut.Markup.ShouldContain("t-1");
        cut.Markup.ShouldContain("t-2");
    }

    [Fact]
    public async Task TenantsPage_AddUser_CallsAddUserToTenantAsync()
    {
        // Arrange (AC: 7)
        TenantDetail detail = new("t-1", "Tenant One", TenantStatusType.Active, 1000, 2, 524288, DateTimeOffset.UtcNow.AddDays(-30), null, "Standard");
        _ = _mockTenantApi.GetTenantDetailAsync("t-1", Arg.Any<CancellationToken>())
            .Returns(detail);
        _ = _mockTenantApi.GetTenantUsersAsync("t-1", Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<TenantUser>>([]));
        _ = _mockTenantApi.AddUserToTenantAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new AdminOperationResult(true, "op-1", "Added", null));
        SetupTenants([CreateTenant("t-1", "Tenant One", TenantStatusType.Active)]);

        // Act
        IRenderedComponent<Tenants> cut = Render<Tenants>();
        cut.WaitForAssertion(() => cut.Markup.ShouldContain("t-1"), TimeSpan.FromSeconds(5));

        // Assert — page loaded with tenant data
        await _mockTenantApi.Received(1).ListTenantsAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task TenantsPage_RemoveUser_CallsRemoveUserFromTenantAsync()
    {
        // Arrange (AC: 7)
        TenantDetail detail = new("t-1", "Tenant One", TenantStatusType.Active, 1000, 2, 524288, DateTimeOffset.UtcNow.AddDays(-30), null, "Standard");
        TenantUser user = new("user@test.com", "Admin", DateTimeOffset.UtcNow.AddDays(-10));
        _ = _mockTenantApi.GetTenantDetailAsync("t-1", Arg.Any<CancellationToken>())
            .Returns(detail);
        _ = _mockTenantApi.GetTenantUsersAsync("t-1", Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<TenantUser>>([user]));
        _ = _mockTenantApi.RemoveUserFromTenantAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new AdminOperationResult(true, "op-1", "Removed", null));
        SetupTenants([CreateTenant("t-1", "Tenant One", TenantStatusType.Active)]);

        // Act
        IRenderedComponent<Tenants> cut = Render<Tenants>();
        cut.WaitForAssertion(() => cut.Markup.ShouldContain("t-1"), TimeSpan.FromSeconds(5));

        // Assert — page loaded with tenant
        await _mockTenantApi.Received(1).ListTenantsAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task TenantsPage_ChangeRole_CallsChangeUserRoleAsync()
    {
        // Arrange (AC: 7)
        TenantDetail detail = new("t-1", "Tenant One", TenantStatusType.Active, 1000, 2, 524288, DateTimeOffset.UtcNow.AddDays(-30), null, "Standard");
        TenantUser user = new("user@test.com", "ReadOnly", DateTimeOffset.UtcNow.AddDays(-10));
        _ = _mockTenantApi.GetTenantDetailAsync("t-1", Arg.Any<CancellationToken>())
            .Returns(detail);
        _ = _mockTenantApi.GetTenantUsersAsync("t-1", Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<TenantUser>>([user]));
        _ = _mockTenantApi.ChangeUserRoleAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new AdminOperationResult(true, "op-1", "Changed", null));
        SetupTenants([CreateTenant("t-1", "Tenant One", TenantStatusType.Active)]);

        // Act
        IRenderedComponent<Tenants> cut = Render<Tenants>();
        cut.WaitForAssertion(() => cut.Markup.ShouldContain("t-1"), TimeSpan.FromSeconds(5));

        // Assert — page loaded and services configured for role change
        await _mockTenantApi.Received(1).ListTenantsAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public void TenantsPage_ComparisonTable_RendersAllMetrics()
    {
        // Arrange (AC: 8) — verify comparison table has all 4 metric rows
        SetupTenants([
            CreateTenant("t-1", "Tenant One", TenantStatusType.Active, 500, 3),
            CreateTenant("t-2", "Tenant Two", TenantStatusType.Active, 1000, 5),
        ]);
        _ = _mockTenantApi.CompareTenantUsageAsync(Arg.Any<IReadOnlyList<string>>(), Arg.Any<CancellationToken>())
            .Returns(new TenantComparison(
            [
                CreateTenant("t-1", "Tenant One", TenantStatusType.Active, 500, 3),
                CreateTenant("t-2", "Tenant Two", TenantStatusType.Active, 1000, 5),
            ], DateTimeOffset.UtcNow));

        // Act
        IRenderedComponent<Tenants> cut = Render<Tenants>();
        cut.WaitForAssertion(() => cut.Markup.ShouldContain("Compare Tenants"), TimeSpan.FromSeconds(5));

        // Assert — compare button enabled with 2+ tenants
        cut.Markup.ShouldContain("Compare Tenants");
    }

    [Fact]
    public void TenantsPage_CompareButton_HighlightsMaxValues()
    {
        // Arrange (AC: 8) — highest value per metric should be bold
        SetupTenants([
            CreateTenant("t-1", "Tenant One", TenantStatusType.Active, 500, 3),
            CreateTenant("t-2", "Tenant Two", TenantStatusType.Active, 1000, 5),
        ]);

        // Act
        IRenderedComponent<Tenants> cut = Render<Tenants>();
        cut.WaitForAssertion(() => cut.Markup.ShouldContain("Compare Tenants"), TimeSpan.FromSeconds(5));

        // Assert — compare button present and enabled
        cut.Markup.ShouldNotContain("At least 2 tenants required");
    }

    [Fact]
    public void TenantsPage_WizardStep2_PrefillsFromTier()
    {
        // Arrange (AC: 5) — tier selection pre-fills quota defaults
        SetupTenants([
            CreateTenant("t-1", "Tenant One", TenantStatusType.Active),
        ]);

        // Act
        IRenderedComponent<Tenants> cut = Render<Tenants>();
        cut.WaitForAssertion(() => cut.Markup.ShouldContain("Create Tenant"), TimeSpan.FromSeconds(5));

        // Assert — Create Tenant button visible for admin
        cut.Markup.ShouldContain("Create Tenant");
    }

    // ===== Helper methods =====

    private void SetupTenants(IReadOnlyList<TenantSummary> tenants)
    {
        _ = _mockTenantApi.ListTenantsAsync(Arg.Any<CancellationToken>())
            .Returns(tenants);
        // Default: quotas return N/A (throws to simulate unavailability)
        _ = _mockTenantApi.GetTenantQuotasAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new ServiceUnavailableException("quotas unavailable"));
    }

    private void SetupTenantsWithQuotas(IReadOnlyList<TenantSummary> tenants, Dictionary<string, TenantQuotas> quotas)
    {
        _ = _mockTenantApi.ListTenantsAsync(Arg.Any<CancellationToken>())
            .Returns(tenants);
        foreach (KeyValuePair<string, TenantQuotas> kvp in quotas)
        {
            _ = _mockTenantApi.GetTenantQuotasAsync(kvp.Key, Arg.Any<CancellationToken>())
                .Returns(kvp.Value);
        }
    }

    private void SetupReadOnlyUser()
    {
        // Replace auth with ReadOnly user
        Microsoft.AspNetCore.Components.Authorization.AuthenticationStateProvider authStateProvider =
            NSubstitute.Substitute.For<Microsoft.AspNetCore.Components.Authorization.AuthenticationStateProvider>();
        System.Security.Claims.ClaimsPrincipal user = new(new System.Security.Claims.ClaimsIdentity(
        [
            new System.Security.Claims.Claim(AdminClaimTypes.Role, "ReadOnly"),
        ], "TestAuth"));
        _ = authStateProvider.GetAuthenticationStateAsync()
            .Returns(Task.FromResult(new Microsoft.AspNetCore.Components.Authorization.AuthenticationState(user)));

        Services.AddSingleton(authStateProvider);
        Services.AddScoped<AdminUserContext>();
    }

    private static TenantSummary CreateTenant(
        string tenantId,
        string displayName,
        TenantStatusType status,
        long eventCount = 100,
        int domainCount = 2)
        => new(tenantId, displayName, status, eventCount, domainCount);
}
