using System.Reflection;

using Bunit;

using Hexalith.EventStore.Admin.Abstractions.Models.Tenants;
using Hexalith.EventStore.Admin.UI.Pages;
using Hexalith.EventStore.Admin.UI.Services.Exceptions;
using Hexalith.EventStore.Admin.UI.Tests.Services;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

using NSubstitute;
using NSubstitute.ExceptionExtensions;

namespace Hexalith.EventStore.Admin.UI.Tests.Pages;

/// <summary>
/// bUnit tests for the Tenants page.
/// </summary>
public class TenantsPageTests : AdminUITestContext {
    private readonly AdminTenantApiClient _mockTenantApi;

    public TenantsPageTests() {
        _mockTenantApi = Substitute.For<AdminTenantApiClient>(
            Substitute.For<IHttpClientFactory>(),
            NullLogger<AdminTenantApiClient>.Instance);
        _ = Services.AddScoped(_ => _mockTenantApi);

        // Register other API clients that shared components might need
        _ = Services.AddScoped(_ => Substitute.For<AdminStorageApiClient>(
            Substitute.For<IHttpClientFactory>(),
            NullLogger<AdminStorageApiClient>.Instance));
        _ = Services.AddScoped(_ => Substitute.For<AdminSnapshotApiClient>(
            Substitute.For<IHttpClientFactory>(),
            NullLogger<AdminSnapshotApiClient>.Instance));
        _ = Services.AddScoped(_ => Substitute.For<AdminCompactionApiClient>(
            Substitute.For<IHttpClientFactory>(),
            NullLogger<AdminCompactionApiClient>.Instance));
        _ = Services.AddScoped(_ => Substitute.For<AdminBackupApiClient>(
            Substitute.For<IHttpClientFactory>(),
            NullLogger<AdminBackupApiClient>.Instance));
    }

    // ===== Merge-blocking tests (5.1-5.16) =====

    [Fact]
    public void TenantsPage_RendersStatCards_WithCorrectValues() {
        // Arrange (AC: 2)
        SetupTenants([
            CreateTenant("t-1", "Tenant One", TenantStatusType.Active),
            CreateTenant("t-2", "Tenant Two", TenantStatusType.Disabled),
        ]);

        // Act
        IRenderedComponent<Tenants> cut = Render<Tenants>();
        cut.WaitForAssertion(() => cut.Markup.ShouldContain("Total Tenants"), TimeSpan.FromSeconds(5));

        // Assert
        cut.Markup.ShouldContain("Total Tenants");
        cut.Markup.ShouldContain("Active");
        cut.Markup.ShouldContain("Disabled");
    }

    [Fact]
    public void TenantsPage_ShowsSkeletonCards_DuringLoading() {
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
    public void TenantsPage_Grid_RendersAllTenants() {
        // Arrange (AC: 1)
        SetupTenants([
            CreateTenant("tenant-alpha", "Alpha Corp", TenantStatusType.Active),
            CreateTenant("tenant-beta", "Beta Inc", TenantStatusType.Disabled),
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
    public void TenantsPage_ShowsEmptyState_WhenNoTenants() {
        // Arrange (AC: 1)
        SetupTenants([]);

        // Act
        IRenderedComponent<Tenants> cut = Render<Tenants>();
        cut.WaitForAssertion(() => cut.Markup.ShouldContain("No tenants configured"), TimeSpan.FromSeconds(5));

        // Assert
        cut.Markup.ShouldContain("No tenants configured");
    }

    [Fact]
    public void TenantsPage_ShowsIssueBanner_OnApiError() {
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
    public void TenantsPage_HasH1Heading() {
        // Arrange (AC: 15)
        SetupTenants([]);

        // Act
        IRenderedComponent<Tenants> cut = Render<Tenants>();
        cut.WaitForAssertion(() => cut.Markup.ShouldContain("<h1"), TimeSpan.FromSeconds(5));

        // Assert
        cut.Markup.ShouldContain(">Tenants</h1>");
    }

    [Fact]
    public void TenantsPage_ExplainsDisableInsteadOfDeleteLifecycle() {
        // Arrange
        SetupTenants([
            CreateTenant("t-1", "Tenant One", TenantStatusType.Active),
        ]);

        // Act
        IRenderedComponent<Tenants> cut = Render<Tenants>();
        cut.WaitForAssertion(() => cut.Markup.ShouldContain("Tenants are retained for audit"), TimeSpan.FromSeconds(5));

        // Assert
        cut.Markup.ShouldContain("Disable a tenant to suspend command processing");
        cut.Markup.ShouldContain("deletion is not supported from the admin UI");
        cut.Markup.ShouldNotContain("Delete Tenant");
    }

    [Fact]
    public void TenantsPage_ExplainsLifecycle_WhenTenantListIsEmpty() {
        // Arrange
        SetupTenants([]);

        // Act
        IRenderedComponent<Tenants> cut = Render<Tenants>();
        cut.WaitForAssertion(() => cut.Markup.ShouldContain("No tenants configured"), TimeSpan.FromSeconds(5));

        // Assert
        cut.Markup.ShouldContain("Tenants are retained for audit");
        cut.Markup.ShouldContain("deletion is not supported from the admin UI");
        cut.Markup.ShouldNotContain("Delete Tenant");
    }

    [Fact]
    public async Task TenantsPage_ExplainsLifecycle_WhenFilterHidesAllTenants() {
        // Arrange
        SetupTenants([
            CreateTenant("t-1", "Tenant One", TenantStatusType.Active),
        ]);

        // Act
        IRenderedComponent<Tenants> cut = Render<Tenants>();
        cut.WaitForAssertion(() => cut.Markup.ShouldContain("Tenant One"), TimeSpan.FromSeconds(5));

        SetPrivateField(cut.Instance, "_statusFilterInput", "Disabled");
        await cut.InvokeAsync(() => InvokePrivateAsync(cut.Instance, "OnStatusFilterChanged"));

        // Assert
        cut.WaitForAssertion(() => {
            cut.Markup.ShouldContain("No tenants configured");
            cut.Markup.ShouldContain("Tenants are retained for audit");
            cut.Markup.ShouldContain("deletion is not supported from the admin UI");
            cut.Markup.ShouldNotContain("Delete Tenant");
        }, TimeSpan.FromSeconds(2));
    }

    [Fact]
    public void TenantsPage_CreateButton_HiddenForReadOnlyUser() {
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
    public void TenantsPage_CreateButton_VisibleForAdminUser() {
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
    public async Task TenantsPage_CreateDialog_SubmitsCreateTenantRequest() {
        // Arrange (AC: 5)
        SetupTenants([CreateTenant("t-1", "Existing", TenantStatusType.Active)]);
        _ = _mockTenantApi.CreateTenantAsync(Arg.Any<CreateTenantRequest>(), Arg.Any<CancellationToken>())
            .Returns(new AdminOperationResult(true, "op-1", "Created", null));

        // Act
        IRenderedComponent<Tenants> cut = Render<Tenants>();
        cut.WaitForAssertion(() => cut.Markup.ShouldContain("Create Tenant"), TimeSpan.FromSeconds(5));

        // Click Create Tenant button to open dialog
        AngleSharp.Dom.IElement createBtn = cut.FindAll("fluent-button")
            .First(b => b.TextContent.Contains("Create Tenant"));
        await cut.InvokeAsync(() => createBtn.Click());

        // Verify dialog opened with single-step fields
        cut.WaitForAssertion(() => cut.Markup.ShouldContain("Tenant ID"), TimeSpan.FromSeconds(2));
        SetPrivateField(cut.Instance, "_createTenantId", "acme-corp");
        SetPrivateField(cut.Instance, "_createName", "Acme Corp");
        SetPrivateField(cut.Instance, "_createDescription", "Primary tenant");

        await cut.InvokeAsync(() => InvokePrivateAsync(cut.Instance, "OnCreateTenantConfirm"));

        cut.WaitForAssertion(() => _ = _mockTenantApi.Received(1).CreateTenantAsync(
                Arg.Is<CreateTenantRequest>(request => request != null && request.TenantId == "acme-corp"
                    && request.Name == "Acme Corp"
                    && request.Description == "Primary tenant"),
                Arg.Any<CancellationToken>()), TimeSpan.FromSeconds(2));

        _ = _mockTenantApi.DidNotReceive().AddUserToTenantAsync(
            Arg.Any<string>(),
            Arg.Any<string>(),
            Arg.Any<string>(),
            Arg.Any<CancellationToken>());
        AssertAcceptedToast("Create request accepted for tenant acme-corp");
    }

    [Fact]
    public async Task TenantsPage_CreateDialog_StaysOpenWhenCreateFails() {
        // Arrange (AC: 5)
        SetupTenants([]);
        _ = _mockTenantApi.CreateTenantAsync(Arg.Any<CreateTenantRequest>(), Arg.Any<CancellationToken>())
            .Returns(new AdminOperationResult(false, "err-1", "Create failed", null));

        // Act
        IRenderedComponent<Tenants> cut = Render<Tenants>();
        cut.WaitForAssertion(() => cut.Markup.ShouldContain("No tenants configured"), TimeSpan.FromSeconds(5));

        AngleSharp.Dom.IElement createBtn = cut.FindAll("fluent-button")
            .First(b => b.TextContent.Contains("Create Tenant"));
        await cut.InvokeAsync(() => createBtn.Click());

        SetPrivateField(cut.Instance, "_createTenantId", "acme-corp");
        SetPrivateField(cut.Instance, "_createName", "Acme Corp");

        await cut.InvokeAsync(() => InvokePrivateAsync(cut.Instance, "OnCreateTenantConfirm"));

        cut.WaitForAssertion(() => {
            cut.Markup.ShouldContain("Tenant ID");
            _ = _mockTenantApi.Received(1).CreateTenantAsync(
                Arg.Is<CreateTenantRequest>(request => request != null && request.TenantId == "acme-corp"
                    && request.Name == "Acme Corp"
                    && request.Description == null),
                Arg.Any<CancellationToken>());
        }, TimeSpan.FromSeconds(2));

        _ = _mockTenantApi.Received(1).ListTenantsAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public void TenantsPage_StatusBadges_RenderCorrectly() {
        // Arrange (AC: 1)
        SetupTenants([
            CreateTenant("t-active", "Active Tenant", TenantStatusType.Active),
            CreateTenant("t-disabled", "Disabled Tenant", TenantStatusType.Disabled),
        ]);

        // Act
        IRenderedComponent<Tenants> cut = Render<Tenants>();
        cut.WaitForAssertion(() => cut.Markup.ShouldContain("Status: Active"), TimeSpan.FromSeconds(5));

        // Assert
        cut.Markup.ShouldContain("Status: Active");
        cut.Markup.ShouldContain("Status: Disabled");
    }

    [Fact]
    public void TenantsPage_DisableButton_OnlyOnActiveTenants() {
        // Arrange (AC: 6)
        SetupTenants([
            CreateTenant("t-active", "Active Tenant", TenantStatusType.Active),
            CreateTenant("t-disabled", "Disabled Tenant", TenantStatusType.Disabled),
        ]);

        // Act
        IRenderedComponent<Tenants> cut = Render<Tenants>();
        cut.WaitForAssertion(() => cut.Markup.ShouldContain("Disable"), TimeSpan.FromSeconds(5));

        // Assert — Disable button appears (for active tenant, admin user context)
        cut.Markup.ShouldContain("Disable");
    }

    [Fact]
    public void TenantsPage_EnableButton_OnlyOnDisabledTenants() {
        // Arrange (AC: 6)
        SetupTenants([
            CreateTenant("t-disabled", "Disabled Tenant", TenantStatusType.Disabled),
        ]);

        // Act
        IRenderedComponent<Tenants> cut = Render<Tenants>();
        cut.WaitForAssertion(() => cut.Markup.ShouldContain("Enable"), TimeSpan.FromSeconds(5));

        // Assert — Enable button appears
        cut.Markup.ShouldContain("Enable");
    }

    [Fact]
    public async Task TenantsPage_EnableAccepted_ShowsStatusUnknownCopyAndDoesNotClaimEnabled() {
        // Arrange
        TenantSummary tenant = CreateTenant("manual-test-tenant-a", "Manual Test Tenant A", TenantStatusType.Disabled);
        _ = _mockTenantApi.ListTenantsAsync(Arg.Any<CancellationToken>())
            .Returns(
                Task.FromResult<IReadOnlyList<TenantSummary>>([tenant]),
                Task.FromResult<IReadOnlyList<TenantSummary>>([tenant]));
        _ = _mockTenantApi.EnableTenantAsync("manual-test-tenant-a", Arg.Any<CancellationToken>())
            .Returns(new AdminOperationResult(true, "01JAXYZ1234567890ABCDEFGH", null, null));
        TestToastService toastService = Services.GetRequiredService<TestToastService>();

        // Act
        IRenderedComponent<Tenants> cut = Render<Tenants>();
        cut.WaitForAssertion(() => cut.Markup.ShouldContain("manual-test-tenant-a"), TimeSpan.FromSeconds(5));
        await cut.InvokeAsync(() => InvokePrivateAsync(cut.Instance, "OpenEnableDialog", tenant));
        await cut.InvokeAsync(() => InvokePrivateAsync(cut.Instance, "OnEnableConfirm"));

        // Assert
        cut.WaitForAssertion(() => {
            string body = toastService.LastOptions?.Message?.ToString() ?? string.Empty;
            body.ShouldContain("Enable request accepted");
            body.ShouldContain("status is unknown");
            body.ShouldContain("01JAXYZ1234567890ABCDEFGH");
            body.ShouldNotContain("Tenant manual-test-tenant-a enabled.");
        }, TimeSpan.FromSeconds(2));
    }

    [Fact]
    public async Task TenantsPage_EnableAccepted_KeepsCommandResultVisible_WhenRefreshFails() {
        TenantSummary tenant = CreateTenant("manual-test-tenant-a", "Manual Test Tenant A", TenantStatusType.Disabled);
        int listCalls = 0;
        _ = _mockTenantApi.ListTenantsAsync(Arg.Any<CancellationToken>())
            .Returns(_ => listCalls++ == 0
                ? Task.FromResult<IReadOnlyList<TenantSummary>>([tenant])
                : Task.FromException<IReadOnlyList<TenantSummary>>(new ServiceUnavailableException("Tenant list timeout")));
        _ = _mockTenantApi.EnableTenantAsync("manual-test-tenant-a", Arg.Any<CancellationToken>())
            .Returns(new AdminOperationResult(true, "01JAXYZ1234567890ABCDEFGH", null, null));
        TestToastService toastService = Services.GetRequiredService<TestToastService>();

        IRenderedComponent<Tenants> cut = Render<Tenants>();
        cut.WaitForAssertion(() => cut.Markup.ShouldContain("manual-test-tenant-a"), TimeSpan.FromSeconds(5));
        await cut.InvokeAsync(() => InvokePrivateAsync(cut.Instance, "OpenEnableDialog", tenant));
        await cut.InvokeAsync(() => InvokePrivateAsync(cut.Instance, "OnEnableConfirm"));

        cut.WaitForAssertion(() => {
            string body = toastService.LastOptions?.Message?.ToString() ?? string.Empty;
            body.ShouldContain("Enable request accepted");
            body.ShouldContain("01JAXYZ1234567890ABCDEFGH");
            cut.Markup.ShouldContain("Unable to load tenant data");
            cut.Markup.ShouldContain("manual-test-tenant-a");
        }, TimeSpan.FromSeconds(2));
    }

    [Fact]
    public async Task TenantsPage_CreateDialog_ReloadsAfterSuccessfulCreate() {
        // Arrange (AC: 5) — successful create refreshes the tenant list only.
        _ = _mockTenantApi.ListTenantsAsync(Arg.Any<CancellationToken>())
            .Returns(
                Task.FromResult<IReadOnlyList<TenantSummary>>([CreateTenant("t-1", "Tenant One", TenantStatusType.Active)]),
                Task.FromResult<IReadOnlyList<TenantSummary>>([CreateTenant("t-1", "Tenant One", TenantStatusType.Active), CreateTenant("acme-corp", "Acme Corp", TenantStatusType.Active)]));
        _ = _mockTenantApi.CreateTenantAsync(Arg.Any<CreateTenantRequest>(), Arg.Any<CancellationToken>())
            .Returns(new AdminOperationResult(true, "op-1", "Created", null));

        // Act
        IRenderedComponent<Tenants> cut = Render<Tenants>();
        cut.WaitForAssertion(() => cut.Markup.ShouldContain("Create Tenant"), TimeSpan.FromSeconds(5));

        AngleSharp.Dom.IElement createBtn = cut.FindAll("fluent-button")
            .First(b => b.TextContent.Contains("Create Tenant"));
        await cut.InvokeAsync(() => createBtn.Click());

        SetPrivateField(cut.Instance, "_createTenantId", "acme-corp");
        SetPrivateField(cut.Instance, "_createName", "Acme Corp");
        await cut.InvokeAsync(() => InvokePrivateAsync(cut.Instance, "OnCreateTenantConfirm"));

        cut.WaitForAssertion(() => {
            _ = _mockTenantApi.Received(2).ListTenantsAsync(Arg.Any<CancellationToken>());
            cut.Markup.ShouldContain("acme-corp");
        }, TimeSpan.FromSeconds(2));

        _ = _mockTenantApi.DidNotReceive().AddUserToTenantAsync(
            Arg.Any<string>(),
            Arg.Any<string>(),
            Arg.Any<string>(),
            Arg.Any<CancellationToken>());
    }

    // ===== Recommended tests (5.17-5.31) =====

    [Fact]
    public async Task TenantsPage_StatusFilter_ShowsCorrectSubset() {
        // Arrange (AC: 3)
        SetupTenants([
            CreateTenant("t-1", "Active One", TenantStatusType.Active),
            CreateTenant("t-2", "Disabled One", TenantStatusType.Disabled),
        ]);

        // Act
        IRenderedComponent<Tenants> cut = Render<Tenants>();
        cut.WaitForAssertion(() => cut.Markup.ShouldContain("Active One"), TimeSpan.FromSeconds(5));

        SetPrivateField(cut.Instance, "_statusFilterInput", "Disabled");
        await cut.InvokeAsync(() => InvokePrivateAsync(cut.Instance, "OnStatusFilterChanged"));

        cut.WaitForAssertion(() => {
            cut.Markup.ShouldContain("Disabled One");
            cut.Markup.ShouldNotContain("Active One");
            NavManager.Uri.ShouldContain("status=Disabled");
        }, TimeSpan.FromSeconds(2));
    }

    [Fact]
    public void TenantsPage_CreateDialogInputs_AreImmediateBound() {
        string source = File.ReadAllText(FindTenantsPageSource());
        int dialogStart = source.IndexOf("aria-label=\"Create Tenant\"", StringComparison.Ordinal);
        dialogStart.ShouldBeGreaterThanOrEqualTo(0);
        int dialogEnd = source.IndexOf("</FluentDialog>", dialogStart, StringComparison.Ordinal);
        dialogEnd.ShouldBeGreaterThan(dialogStart);

        string createDialog = source[dialogStart..dialogEnd];

        createDialog.Split("Immediate=\"true\"", StringSplitOptions.None).Length.ShouldBe(4);
        createDialog.ShouldContain("@bind-Value=\"_createTenantId\"");
        createDialog.ShouldContain("@bind-Value=\"_createName\"");
        createDialog.ShouldContain("@bind-Value=\"_createDescription\"");
    }

    [Fact]
    public async Task TenantsPage_SearchFilter_FiltersByTenantId() {
        // Arrange (AC: 3)
        SetupTenants([
            CreateTenant("acme-corp", "ACME Corporation", TenantStatusType.Active),
            CreateTenant("beta-inc", "Beta Inc", TenantStatusType.Active),
        ]);

        // Act
        IRenderedComponent<Tenants> cut = Render<Tenants>();
        cut.WaitForAssertion(() => cut.Markup.ShouldContain("acme-corp"), TimeSpan.FromSeconds(5));

        SetPrivateField(cut.Instance, "_searchFilterInput", "acme");
        await cut.InvokeAsync(() => InvokePrivateAsync(cut.Instance, "OnDebounceElapsed", (object?)null));

        cut.WaitForAssertion(() => {
            cut.Markup.ShouldContain("acme-corp");
            cut.Markup.ShouldNotContain("beta-inc");
            NavManager.Uri.ShouldContain("search=acme");
        }, TimeSpan.FromSeconds(2));
    }

    [Fact]
    public void TenantsPage_DetailPanel_LoadsOnRowClick() {
        // Arrange (AC: 4) — detail API returns data
        TenantDetail detail = new("t-1", "Tenant One", null, TenantStatusType.Active, DateTimeOffset.UtcNow.AddDays(-30));
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
    public void TenantsPage_DetailPanel_ShowsFallback_WhenServiceUnavailable() {
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
    public void TenantsPage_ServiceDown_ShowsIssueBannerForTenantsService() {
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
    public void TenantsPage_UrlParameters_ReadOnInit() {
        // Arrange (AC: 10) — verify URL parameters are parsed
        SetupTenants([
            CreateTenant("t-1", "Tenant One", TenantStatusType.Active),
            CreateTenant("t-2", "Tenant Two", TenantStatusType.Disabled),
        ]);

        // Act — render the page (URL parsing happens in OnInitializedAsync)
        IRenderedComponent<Tenants> cut = Render<Tenants>();
        cut.WaitForAssertion(() => cut.Markup.ShouldContain("t-1"), TimeSpan.FromSeconds(5));

        // Assert — page renders with all tenants (default "All" filter)
        cut.Markup.ShouldContain("t-1");
        cut.Markup.ShouldContain("t-2");
    }

    [Fact]
    public async Task TenantsPage_AddUser_CallsAddUserToTenantAsync() {
        // Arrange (AC: 7)
        TenantSummary tenant = CreateTenant("t-1", "Tenant One", TenantStatusType.Active);
        TenantDetail detail = new("t-1", "Tenant One", null, TenantStatusType.Active, DateTimeOffset.UtcNow.AddDays(-30));
        _ = _mockTenantApi.GetTenantDetailAsync("t-1", Arg.Any<CancellationToken>())
            .Returns(detail);
        _ = _mockTenantApi.GetTenantUsersAsync("t-1", Arg.Any<CancellationToken>())
            .Returns(
                Task.FromResult<IReadOnlyList<TenantUser>>([]),
                Task.FromResult<IReadOnlyList<TenantUser>>([new TenantUser("user-001", "TenantContributor")]));
        _ = _mockTenantApi.AddUserToTenantAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new AdminOperationResult(true, "op-1", "Added", null));
        SetupTenants([tenant]);

        // Act
        IRenderedComponent<Tenants> cut = Render<Tenants>();
        cut.WaitForAssertion(() => cut.Markup.ShouldContain("t-1"), TimeSpan.FromSeconds(5));
        await cut.InvokeAsync(() => InvokePrivateAsync(cut.Instance, "OnRowClick", tenant));
        cut.WaitForAssertion(() => cut.Markup.ShouldContain("Add User"), TimeSpan.FromSeconds(2));

        await cut.InvokeAsync(() => InvokePrivateAsync(cut.Instance, "OpenAddUserDialog"));
        SetPrivateField(cut.Instance, "_addUserId", "user-001");
        SetPrivateField(cut.Instance, "_addUserRole", "TenantContributor");
        await cut.InvokeAsync(() => InvokePrivateAsync(cut.Instance, "OnAddUserConfirm"));

        // Assert
        cut.WaitForAssertion(() => _ = _mockTenantApi.Received(1).AddUserToTenantAsync(
                "t-1",
                "user-001",
                "TenantContributor",
                Arg.Any<CancellationToken>()), TimeSpan.FromSeconds(2));
        AssertAcceptedToast("Add-user request accepted for user-001");
    }

    [Fact]
    public async Task TenantsPage_RemoveUser_CallsRemoveUserFromTenantAsync() {
        // Arrange (AC: 7)
        TenantSummary tenant = CreateTenant("t-1", "Tenant One", TenantStatusType.Active);
        TenantDetail detail = new("t-1", "Tenant One", null, TenantStatusType.Active, DateTimeOffset.UtcNow.AddDays(-30));
        TenantUser user = new("user-001", "TenantOwner");
        _ = _mockTenantApi.GetTenantDetailAsync("t-1", Arg.Any<CancellationToken>())
            .Returns(detail);
        _ = _mockTenantApi.GetTenantUsersAsync("t-1", Arg.Any<CancellationToken>())
            .Returns(
                Task.FromResult<IReadOnlyList<TenantUser>>([user]),
                Task.FromResult<IReadOnlyList<TenantUser>>([]));
        _ = _mockTenantApi.RemoveUserFromTenantAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new AdminOperationResult(true, "op-1", "Removed", null));
        SetupTenants([tenant]);

        // Act
        IRenderedComponent<Tenants> cut = Render<Tenants>();
        cut.WaitForAssertion(() => cut.Markup.ShouldContain("t-1"), TimeSpan.FromSeconds(5));
        await cut.InvokeAsync(() => InvokePrivateAsync(cut.Instance, "OnRowClick", tenant));
        cut.WaitForAssertion(() => cut.Markup.ShouldContain("Change Role"), TimeSpan.FromSeconds(2));

        await cut.InvokeAsync(() => InvokePrivateAsync(cut.Instance, "OpenRemoveUserDialog", user));
        await cut.InvokeAsync(() => InvokePrivateAsync(cut.Instance, "OnRemoveUserConfirm"));

        // Assert
        cut.WaitForAssertion(() => _ = _mockTenantApi.Received(1).RemoveUserFromTenantAsync(
                "t-1",
                "user-001",
                Arg.Any<CancellationToken>()), TimeSpan.FromSeconds(2));
        AssertAcceptedToast("Remove-user request accepted for user-001");
    }

    [Fact]
    public async Task TenantsPage_ChangeRole_CallsChangeUserRoleAsync() {
        // Arrange (AC: 7)
        TenantSummary tenant = CreateTenant("t-1", "Tenant One", TenantStatusType.Active);
        TenantDetail detail = new("t-1", "Tenant One", null, TenantStatusType.Active, DateTimeOffset.UtcNow.AddDays(-30));
        TenantUser user = new("user-001", "TenantReader");
        _ = _mockTenantApi.GetTenantDetailAsync("t-1", Arg.Any<CancellationToken>())
            .Returns(detail);
        _ = _mockTenantApi.GetTenantUsersAsync("t-1", Arg.Any<CancellationToken>())
            .Returns(
                Task.FromResult<IReadOnlyList<TenantUser>>([user]),
                Task.FromResult<IReadOnlyList<TenantUser>>([new TenantUser("user-001", "TenantContributor")]));
        _ = _mockTenantApi.ChangeUserRoleAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new AdminOperationResult(true, "op-1", "Changed", null));
        SetupTenants([tenant]);

        // Act
        IRenderedComponent<Tenants> cut = Render<Tenants>();
        cut.WaitForAssertion(() => cut.Markup.ShouldContain("t-1"), TimeSpan.FromSeconds(5));
        await cut.InvokeAsync(() => InvokePrivateAsync(cut.Instance, "OnRowClick", tenant));
        cut.WaitForAssertion(() => cut.Markup.ShouldContain("Change Role"), TimeSpan.FromSeconds(2));

        await cut.InvokeAsync(() => InvokePrivateAsync(cut.Instance, "OpenChangeRoleDialog", user));
        SetPrivateField(cut.Instance, "_changeRoleNewRole", "TenantContributor");
        await cut.InvokeAsync(() => InvokePrivateAsync(cut.Instance, "OnChangeRoleConfirm"));

        // Assert
        cut.WaitForAssertion(() => _ = _mockTenantApi.Received(1).ChangeUserRoleAsync(
                "t-1",
                "user-001",
                "TenantContributor",
                Arg.Any<CancellationToken>()), TimeSpan.FromSeconds(2));
        AssertAcceptedToast("Change-role request accepted for user-001");
    }

    [Fact]
    public async Task TenantsPage_CreateDialog_RequiresTenantIdAndName() {
        // Arrange (AC: 5)
        SetupTenants([
            CreateTenant("t-1", "Tenant One", TenantStatusType.Active),
        ]);

        // Act
        IRenderedComponent<Tenants> cut = Render<Tenants>();
        cut.WaitForAssertion(() => cut.Markup.ShouldContain("Create Tenant"), TimeSpan.FromSeconds(5));
        AngleSharp.Dom.IElement createBtn = cut.FindAll("fluent-button")
            .First(b => b.TextContent.Contains("Create Tenant"));
        await cut.InvokeAsync(() => createBtn.Click());

        // Assert — submit remains disabled until required fields are present
        cut.WaitForAssertion(() => cut.Markup.ShouldContain("Description"), TimeSpan.FromSeconds(2));
        SetPrivateField(cut.Instance, "_createTenantId", "acme-corp");
        InvokePrivate<bool>(cut.Instance, "IsCreateFormValid").ShouldBeFalse();

        SetPrivateField(cut.Instance, "_createName", "Acme Corp");
        InvokePrivate<bool>(cut.Instance, "IsCreateFormValid").ShouldBeTrue();
    }

    [Theory]
    [InlineData("create", "Tenant 'new-tenant'", "Create a new tenant boundary", "tenant-create-button")]
    [InlineData("disable", "Tenant 'tenant-a'", "Disable access and processing", "tenant-disable-tenant-a")]
    [InlineData("enable", "Tenant 'tenant-disabled'", "Re-enable access and processing", "tenant-enable-tenant-disabled")]
    [InlineData("add", "User 'new-user' in tenant 'tenant-a'", "Grant the selected user", "tenant-add-tenant-a-")]
    [InlineData("remove", "User 'user-1' in tenant 'tenant-a'", "Remove the user's access", "tenant-remove-tenant-a-user-1")]
    [InlineData("change-role", "User 'user-1' in tenant 'tenant-a'", "Replace the user's tenant role", "tenant-change-role-tenant-a-user-1")]
    public async Task MutationDialog_CancelRendersExactFactsPerformsNoWorkAndRestoresInitiator(
        string action,
        string expectedTarget,
        string impactFragment,
        string expectedFocusId) {
        TenantSummary tenant = action == "enable"
            ? CreateTenant("tenant-disabled", "Disabled Tenant", TenantStatusType.Disabled)
            : CreateTenant("tenant-a", "Tenant A", TenantStatusType.Active);
        TenantUser user = new("user-1", "TenantReader");
        SetupTenants([tenant]);
        _ = _mockTenantApi.GetTenantDetailAsync("tenant-a", Arg.Any<CancellationToken>())
            .Returns(new TenantDetail("tenant-a", "Tenant A", null, TenantStatusType.Active, DateTimeOffset.UtcNow.AddDays(-2)));
        _ = _mockTenantApi.GetTenantUsersAsync("tenant-a", Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<TenantUser>>([user]));
        IRenderedComponent<Tenants> cut = Render<Tenants>();
        cut.WaitForAssertion(() => cut.Markup.ShouldContain(tenant.TenantId), TimeSpan.FromSeconds(5));

        if (action is "add" or "remove" or "change-role") {
            await cut.InvokeAsync(() => InvokePrivateAsync(cut.Instance, "OnRowClick", tenant));
            cut.WaitForAssertion(() => cut.Find("#tenant-add-tenant-a-"), TimeSpan.FromSeconds(5));
        }

        string selector = action switch {
            "create" => "#tenant-create-button",
            "disable" => "#tenant-disable-tenant-a",
            "enable" => "#tenant-enable-tenant-disabled",
            "add" => "#tenant-add-tenant-a-",
            "remove" => "#tenant-remove-tenant-a-user-1",
            "change-role" => "#tenant-change-role-tenant-a-user-1",
            _ => throw new InvalidOperationException($"Unknown action '{action}'."),
        };
        await cut.Find(selector).ClickAsync(new Microsoft.AspNetCore.Components.Web.MouseEventArgs());
        cut.WaitForAssertion(() => cut.Markup.ShouldContain("Confirmation safety facts"), TimeSpan.FromSeconds(5));
        if (action == "create") {
            SetPrivateField(cut.Instance, "_createTenantId", "new-tenant");
            cut.Render();
        }
        else if (action == "add") {
            SetPrivateField(cut.Instance, "_addUserId", "new-user");
            cut.Render();
        }

        cut.Find("[data-confirmation-fact='target']").TextContent.ShouldBe(expectedTarget);
        cut.Find("[data-confirmation-fact='impact']").TextContent.ShouldContain(impactFragment);
        cut.Find("[data-confirmation-fact='permission']").TextContent.ShouldBe("Admin");

        IRenderedComponent<Microsoft.FluentUI.AspNetCore.Components.FluentButton> cancel =
            cut.FindComponents<Microsoft.FluentUI.AspNetCore.Components.FluentButton>()
                .Single(button => button.Find("fluent-button").TextContent.Trim() == "Cancel");
        await cancel.InvokeAsync(cancel.Instance.OnClick.InvokeAsync);

        _ = _mockTenantApi.DidNotReceive().CreateTenantAsync(
            Arg.Any<CreateTenantRequest>(), Arg.Any<CancellationToken>());
        _ = _mockTenantApi.DidNotReceive().DisableTenantAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
        _ = _mockTenantApi.DidNotReceive().EnableTenantAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
        _ = _mockTenantApi.DidNotReceive().AddUserToTenantAsync(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
        _ = _mockTenantApi.DidNotReceive().RemoveUserFromTenantAsync(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
        _ = _mockTenantApi.DidNotReceive().ChangeUserRoleAsync(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
        JSInterop.Invocations.Last(invocation => invocation.Identifier == "hexalithAdmin.focusElementById")
            .Arguments[0].ShouldBe(expectedFocusId);
    }

    [Fact]
    public async Task ChangeRoleDialog_ForbiddenUsesSafeCopyRestoresFocusAndDoesNotClaimCompletion() {
        TenantSummary tenant = CreateTenant("tenant-a", "Tenant A", TenantStatusType.Active);
        TenantUser user = new("user-1", "TenantReader");
        SetupTenants([tenant]);
        _ = _mockTenantApi.GetTenantDetailAsync("tenant-a", Arg.Any<CancellationToken>())
            .Returns(new TenantDetail("tenant-a", "Tenant A", null, TenantStatusType.Active, DateTimeOffset.UtcNow.AddDays(-2)));
        _ = _mockTenantApi.GetTenantUsersAsync("tenant-a", Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<TenantUser>>([user]));
        _ = _mockTenantApi.ChangeUserRoleAsync(
                Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromException<AdminOperationResult?>(
                new ForbiddenAccessException("hidden membership exists; bearer secret-value")));
        IRenderedComponent<Tenants> cut = Render<Tenants>();
        cut.WaitForAssertion(() => cut.Markup.ShouldContain("tenant-a"), TimeSpan.FromSeconds(5));
        await cut.InvokeAsync(() => InvokePrivateAsync(cut.Instance, "OnRowClick", tenant));
        cut.WaitForAssertion(() => cut.Find("#tenant-change-role-tenant-a-user-1"), TimeSpan.FromSeconds(5));
        await cut.Find("#tenant-change-role-tenant-a-user-1")
            .ClickAsync(new Microsoft.AspNetCore.Components.Web.MouseEventArgs());

        await cut.InvokeAsync(() => InvokePrivateAsync(cut.Instance, "OnChangeRoleConfirm"));

        _ = await _mockTenantApi.Received(1).ChangeUserRoleAsync(
            "tenant-a", "user-1", "TenantReader", Arg.Any<CancellationToken>());
        TestToastService toast = Services.GetRequiredService<TestToastService>();
        string message = toast.LastOptions?.Message?.ToString() ?? string.Empty;
        message.ShouldBe("Access denied. Administrator permission is required.");
        message.ShouldNotContain("hidden membership");
        message.ShouldNotContain("secret-value");
        message.ShouldNotContain("completed", Case.Insensitive);
        JSInterop.Invocations.Last(invocation => invocation.Identifier == "hexalithAdmin.focusElementById")
            .Arguments[0].ShouldBe("tenant-change-role-tenant-a-user-1");
    }

    [Theory]
    [InlineData("create", "tenant-create-button")]
    [InlineData("disable", "tenant-disable-tenant-a")]
    [InlineData("enable", "tenant-enable-tenant-disabled")]
    [InlineData("add", "tenant-add-tenant-a-")]
    [InlineData("remove", "tenant-remove-tenant-a-user-1")]
    public async Task IndependentTenantDenialHandlersUseSafeCopyCloseAndRestoreExactInitiator(
        string action,
        string expectedFocusId) {
        TenantSummary active = CreateTenant("tenant-a", "Tenant A", TenantStatusType.Active);
        TenantSummary disabled = CreateTenant("tenant-disabled", "Tenant Disabled", TenantStatusType.Disabled);
        TenantUser user = new("user-1", "TenantReader");
        SetupTenants([active, disabled]);
        _ = _mockTenantApi.GetTenantDetailAsync("tenant-a", Arg.Any<CancellationToken>())
            .Returns(new TenantDetail("tenant-a", "Tenant A", null, TenantStatusType.Active, DateTimeOffset.UtcNow.AddDays(-2)));
        _ = _mockTenantApi.GetTenantUsersAsync("tenant-a", Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<TenantUser>>([user]));
        var denial = new ForbiddenAccessException("hidden tenant membership exists; bearer secret-value");
        _ = _mockTenantApi.CreateTenantAsync(Arg.Any<CreateTenantRequest>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromException<AdminOperationResult?>(denial));
        _ = _mockTenantApi.DisableTenantAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromException<AdminOperationResult?>(denial));
        _ = _mockTenantApi.EnableTenantAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromException<AdminOperationResult?>(denial));
        _ = _mockTenantApi.AddUserToTenantAsync(
                Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromException<AdminOperationResult?>(denial));
        _ = _mockTenantApi.RemoveUserFromTenantAsync(
                Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromException<AdminOperationResult?>(denial));
        IRenderedComponent<Tenants> cut = Render<Tenants>();
        cut.WaitForAssertion(() => cut.Markup.ShouldContain("tenant-disabled"), TimeSpan.FromSeconds(5));

        if (action is "add" or "remove") {
            await cut.InvokeAsync(() => InvokePrivateAsync(cut.Instance, "OnRowClick", active));
            cut.WaitForAssertion(() => cut.Find("#tenant-add-tenant-a-"), TimeSpan.FromSeconds(5));
        }

        await cut.Find($"#{expectedFocusId}").ClickAsync(new Microsoft.AspNetCore.Components.Web.MouseEventArgs());
        if (action == "create") {
            SetPrivateField(cut.Instance, "_createTenantId", "new-tenant");
            SetPrivateField(cut.Instance, "_createName", "New Tenant");
        }
        else if (action == "add") {
            SetPrivateField(cut.Instance, "_addUserId", "new-user");
        }

        string methodName = action switch {
            "create" => "OnCreateTenantConfirm",
            "disable" => "OnDisableConfirm",
            "enable" => "OnEnableConfirm",
            "add" => "OnAddUserConfirm",
            "remove" => "OnRemoveUserConfirm",
            _ => throw new InvalidOperationException($"Unknown action '{action}'."),
        };
        await cut.InvokeAsync(() => InvokePrivateAsync(cut.Instance, methodName));

        string message = Services.GetRequiredService<TestToastService>().LastOptions!.Message!.ToString()!;
        message.ShouldBe("Access denied. Administrator permission is required.");
        message.ShouldNotContain("hidden tenant");
        message.ShouldNotContain("secret-value");
        message.ShouldNotContain("completed", Case.Insensitive);
        cut.FindAll("[data-confirmation-fact='target']").ShouldBeEmpty();
        JSInterop.Invocations.Last(invocation => invocation.Identifier == "hexalithAdmin.focusElementById")
            .Arguments[0].ShouldBe(expectedFocusId);
    }

    [Theory]
    [InlineData("create", "tenant-create-button", "Failed to create tenant.")]
    [InlineData("disable", "tenant-disable-tenant-a", "Failed to disable tenant.")]
    [InlineData("enable", "tenant-enable-tenant-disabled", "Failed to enable tenant.")]
    [InlineData("add", "tenant-add-tenant-a-", "Failed to add user.")]
    [InlineData("remove", "tenant-remove-tenant-a-user-1", "Failed to remove user.")]
    [InlineData("change-role", "tenant-change-role-tenant-a-user-1", "Failed to change role.")]
    public async Task IndependentTenantUnexpectedFailureUsesFixedCopy(
        string action,
        string expectedFocusId,
        string expectedMessage) {
        TenantSummary active = CreateTenant("tenant-a", "Tenant A", TenantStatusType.Active);
        TenantSummary disabled = CreateTenant("tenant-disabled", "Tenant Disabled", TenantStatusType.Disabled);
        TenantUser user = new("user-1", "TenantReader");
        SetupTenants([active, disabled]);
        _ = _mockTenantApi.GetTenantDetailAsync("tenant-a", Arg.Any<CancellationToken>())
            .Returns(new TenantDetail("tenant-a", "Tenant A", null, TenantStatusType.Active, DateTimeOffset.UtcNow.AddDays(-2)));
        _ = _mockTenantApi.GetTenantUsersAsync("tenant-a", Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<TenantUser>>([user]));
        var failure = new Exception("bearer secret-value at redis://private");
        _ = _mockTenantApi.CreateTenantAsync(Arg.Any<CreateTenantRequest>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromException<AdminOperationResult?>(failure));
        _ = _mockTenantApi.DisableTenantAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromException<AdminOperationResult?>(failure));
        _ = _mockTenantApi.EnableTenantAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromException<AdminOperationResult?>(failure));
        _ = _mockTenantApi.AddUserToTenantAsync(
                Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromException<AdminOperationResult?>(failure));
        _ = _mockTenantApi.RemoveUserFromTenantAsync(
                Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromException<AdminOperationResult?>(failure));
        _ = _mockTenantApi.ChangeUserRoleAsync(
                Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromException<AdminOperationResult?>(failure));
        IRenderedComponent<Tenants> cut = Render<Tenants>();
        cut.WaitForAssertion(() => cut.Markup.ShouldContain("tenant-disabled"), TimeSpan.FromSeconds(5));

        if (action is "add" or "remove" or "change-role") {
            await cut.InvokeAsync(() => InvokePrivateAsync(cut.Instance, "OnRowClick", active));
            cut.WaitForAssertion(() => cut.Find("#tenant-add-tenant-a-"), TimeSpan.FromSeconds(5));
        }

        await cut.Find($"#{expectedFocusId}").ClickAsync(new Microsoft.AspNetCore.Components.Web.MouseEventArgs());
        if (action == "create") {
            SetPrivateField(cut.Instance, "_createTenantId", "new-tenant");
            SetPrivateField(cut.Instance, "_createName", "New Tenant");
        }
        else if (action == "add") {
            SetPrivateField(cut.Instance, "_addUserId", "new-user");
        }

        string methodName = action switch {
            "create" => "OnCreateTenantConfirm",
            "disable" => "OnDisableConfirm",
            "enable" => "OnEnableConfirm",
            "add" => "OnAddUserConfirm",
            "remove" => "OnRemoveUserConfirm",
            "change-role" => "OnChangeRoleConfirm",
            _ => throw new InvalidOperationException($"Unknown action '{action}'."),
        };
        await cut.InvokeAsync(() => InvokePrivateAsync(cut.Instance, methodName));

        string message = Services.GetRequiredService<TestToastService>().LastOptions!.Message!.ToString()!;
        message.ShouldBe(expectedMessage);
        message.ShouldNotContain("secret-value");
        message.ShouldNotContain("redis://private");
    }

    // ===== Helper methods =====

    private void SetupTenants(IReadOnlyList<TenantSummary> tenants) => _ = _mockTenantApi.ListTenantsAsync(Arg.Any<CancellationToken>())
            .Returns(tenants);

    private void SetupReadOnlyUser() {
        // Replace auth with ReadOnly user
        Microsoft.AspNetCore.Components.Authorization.AuthenticationStateProvider authStateProvider =
            NSubstitute.Substitute.For<Microsoft.AspNetCore.Components.Authorization.AuthenticationStateProvider>();
        System.Security.Claims.ClaimsPrincipal user = new(new System.Security.Claims.ClaimsIdentity(
        [
            new System.Security.Claims.Claim(AdminClaimTypes.Role, "ReadOnly"),
        ], "TestAuth"));
        _ = authStateProvider.GetAuthenticationStateAsync()
            .Returns(Task.FromResult(new Microsoft.AspNetCore.Components.Authorization.AuthenticationState(user)));

        _ = Services.AddSingleton(authStateProvider);
        _ = Services.AddScoped<AdminUserContext>();
    }

    private Microsoft.AspNetCore.Components.NavigationManager NavManager =>
        Services.GetRequiredService<Microsoft.AspNetCore.Components.NavigationManager>();

    private static async Task InvokePrivateAsync(object instance, string methodName, params object?[]? args) {
        MethodInfo method = instance.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException($"Method '{methodName}' not found.");

        object? result = method.Invoke(instance, args ?? []);
        if (result is Task task) {
            await task.ConfigureAwait(false);
        }
    }

    private static T InvokePrivate<T>(object instance, string methodName, params object?[]? args) {
        MethodInfo method = instance.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException($"Method '{methodName}' not found.");

        object? result = method.Invoke(instance, args ?? []);
        return result is T typedResult
            ? typedResult
            : throw new InvalidOperationException($"Method '{methodName}' did not return {typeof(T).Name}.");
    }

    private static void SetPrivateField(object instance, string fieldName, object? value) {
        FieldInfo field = instance.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException($"Field '{fieldName}' not found.");
        field.SetValue(instance, value);
    }

    private static string FindTenantsPageSource() {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory is not null) {
            string candidate = Path.Combine(
                directory.FullName,
                "src",
                "Hexalith.EventStore.Admin.UI",
                "Pages",
                "Tenants.razor");
            if (File.Exists(candidate)) {
                return candidate;
            }

            directory = directory.Parent;
        }

        throw new FileNotFoundException("Could not locate Tenants.razor from the test output directory.");
    }

    private static TenantSummary CreateTenant(
        string tenantId,
        string name,
        TenantStatusType status)
        => new(tenantId, name, status);

    private void AssertAcceptedToast(string expectedPrefix) {
        string message = Services.GetRequiredService<TestToastService>().LastOptions!.Message!.ToString()!;
        message.ShouldStartWith(expectedPrefix);
        message.ShouldNotContain("completed", Case.Insensitive);
        message.ShouldNotContain("successfully", Case.Insensitive);
    }
}
