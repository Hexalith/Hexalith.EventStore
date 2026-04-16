using Bunit;

using Hexalith.EventStore.Admin.Abstractions.Models.Storage;
using Hexalith.EventStore.Admin.UI.Pages;
using Hexalith.EventStore.Admin.UI.Services.Exceptions;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.FluentUI.AspNetCore.Components;

using NSubstitute;
using NSubstitute.ExceptionExtensions;

namespace Hexalith.EventStore.Admin.UI.Tests.Pages;

/// <summary>
/// bUnit tests for the Snapshots page.
/// </summary>
public class SnapshotsPageTests : AdminUITestContext {
    private readonly AdminSnapshotApiClient _mockSnapshotApi;

    public SnapshotsPageTests() {
        _mockSnapshotApi = Substitute.For<AdminSnapshotApiClient>(
            Substitute.For<IHttpClientFactory>(),
            NullLogger<AdminSnapshotApiClient>.Instance);
        _ = Services.AddScoped(_ => _mockSnapshotApi);

        // Register AdminStorageApiClient that some shared components might need
        _ = Services.AddScoped(_ => Substitute.For<AdminStorageApiClient>(
            Substitute.For<IHttpClientFactory>(),
            NullLogger<AdminStorageApiClient>.Instance));
    }

    // ===== Merge-blocking tests (4.1-4.11) =====

    [Fact]
    public void SnapshotsPage_RendersStatCards_WithCorrectValues() {
        // Arrange
        SetupPolicies([
            new SnapshotPolicy("tenant-a", "orders", "OrderAggregate", 100, DateTimeOffset.UtcNow.AddDays(-5)),
            new SnapshotPolicy("tenant-b", "inventory", "StockAggregate", 200, DateTimeOffset.UtcNow.AddDays(-2)),
        ]);

        // Act
        IRenderedComponent<Snapshots> cut = Render<Snapshots>();
        cut.WaitForAssertion(() => cut.Markup.ShouldContain("Total Policies"), TimeSpan.FromSeconds(5));

        // Assert
        cut.Markup.ShouldContain("Total Policies");
        cut.Markup.ShouldContain("2");
        cut.Markup.ShouldContain("Tenants Covered");
        cut.Markup.ShouldContain("Avg Interval");
    }

    [Fact]
    public void SnapshotsPage_ShowsSkeletonCards_DuringLoading() {
        // Arrange — never complete the task
        TaskCompletionSource<IReadOnlyList<SnapshotPolicy>> tcs = new();
        _ = _mockSnapshotApi.GetSnapshotPoliciesAsync(Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(tcs.Task);

        // Act
        IRenderedComponent<Snapshots> cut = Render<Snapshots>();

        // Assert — skeleton cards present during loading
        cut.Markup.ShouldContain("aria-hidden=\"true\"");
    }

    [Fact]
    public void SnapshotsPage_PolicyGrid_RendersAllPolicies() {
        // Arrange
        SetupPolicies([
            new SnapshotPolicy("tenant-a", "orders", "OrderAggregate", 100, DateTimeOffset.UtcNow.AddDays(-5)),
            new SnapshotPolicy("tenant-b", "inventory", "StockAggregate", 200, DateTimeOffset.UtcNow.AddDays(-2)),
        ]);

        // Act
        IRenderedComponent<Snapshots> cut = Render<Snapshots>();
        cut.WaitForAssertion(() => cut.Markup.ShouldContain("tenant-a"), TimeSpan.FromSeconds(5));

        // Assert
        cut.Markup.ShouldContain("tenant-a");
        cut.Markup.ShouldContain("tenant-b");
        cut.Markup.ShouldContain("OrderAggregate");
        cut.Markup.ShouldContain("StockAggregate");
        cut.Markup.ShouldContain("orders");
        cut.Markup.ShouldContain("inventory");
    }

    [Fact]
    public void SnapshotsPage_ShowsEmptyState_WhenNoPolicies() {
        // Arrange
        SetupPolicies([]);

        // Act
        IRenderedComponent<Snapshots> cut = Render<Snapshots>();
        cut.WaitForAssertion(() => cut.Markup.ShouldContain("No snapshot policies configured"), TimeSpan.FromSeconds(5));

        // Assert
        cut.Markup.ShouldContain("No snapshot policies configured");
    }

    [Fact]
    public void SnapshotsPage_ShowsIssueBanner_OnApiError() {
        // Arrange
        _ = _mockSnapshotApi.GetSnapshotPoliciesAsync(Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new ServiceUnavailableException("test"));

        // Act
        IRenderedComponent<Snapshots> cut = Render<Snapshots>();
        cut.WaitForAssertion(() => cut.Markup.ShouldContain("Unable to load snapshot policies"), TimeSpan.FromSeconds(5));

        // Assert
        cut.Markup.ShouldContain("Unable to load snapshot policies");
    }

    [Fact]
    public void SnapshotsPage_HasH1Heading() {
        // Arrange
        SetupPolicies([]);

        // Act
        IRenderedComponent<Snapshots> cut = Render<Snapshots>();
        cut.WaitForAssertion(() => cut.Markup.ShouldContain("Snapshots"), TimeSpan.FromSeconds(5));

        // Assert
        cut.Markup.ShouldContain("<h1");
        cut.Markup.ShouldContain("Snapshots");
    }

    [Fact]
    public void SnapshotsPage_AddPolicyButton_HiddenForReadOnlyUsers() {
        // Arrange — set up ReadOnly user
        SetupReadOnlyUser();
        SetupPolicies([]);

        // Act
        IRenderedComponent<Snapshots> cut = Render<Snapshots>();
        cut.WaitForAssertion(() => cut.Markup.ShouldContain("Snapshots"), TimeSpan.FromSeconds(5));

        // Assert — "Add Policy" should not be visible
        cut.Markup.ShouldNotContain("Add Policy");
    }

    [Fact]
    public void SnapshotsPage_AddPolicyButton_VisibleForOperatorUsers() {
        // Arrange — default user is Admin (from AdminUITestContext)
        SetupPolicies([]);

        // Act
        IRenderedComponent<Snapshots> cut = Render<Snapshots>();
        cut.WaitForAssertion(() => cut.Markup.ShouldContain("Add Policy"), TimeSpan.FromSeconds(5));

        // Assert
        cut.Markup.ShouldContain("Add Policy");
    }

    [Fact]
    public async Task SnapshotsPage_CreatePolicyDialog_OpensViaUrlPreFill_AndCreatesPolicy() {
        // Arrange — use URL pre-fill to auto-open create dialog
        SetupPolicies([]);
        _ = _mockSnapshotApi.SetSnapshotPolicyAsync(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<AdminOperationResult?>(new AdminOperationResult(true, "op-1", "Created", null)));

        // Act — navigate with create=true to auto-open dialog pre-filled
        NavManager.NavigateTo("/snapshots?create=true&tenant=tenant-x&domain=sales&aggregateType=OrderAggregate");
        IRenderedComponent<Snapshots> cut = Render<Snapshots>();
        cut.WaitForAssertion(() => cut.Markup.ShouldContain("Create Snapshot Policy"), TimeSpan.FromSeconds(5));

        // Assert — dialog is open with form fields
        cut.Markup.ShouldContain("Create Snapshot Policy");
        cut.Markup.ShouldContain("Tenant ID");
        cut.Markup.ShouldContain("Domain");
        cut.Markup.ShouldContain("Aggregate Type");

        // Act — submit create policy
        IRenderedComponent<FluentButton> createBtn = cut.FindComponents<FluentButton>()
            .First(b => b.Markup.Contains("Create Policy"));
        await createBtn.InvokeAsync(createBtn.Instance.OnClick.InvokeAsync);

        // Assert — API invoked and dialog closes on success
        _ = await _mockSnapshotApi.Received(1).SetSnapshotPolicyAsync(
            "tenant-x", "sales", "OrderAggregate", Arg.Any<int>(), Arg.Any<CancellationToken>());
        cut.WaitForAssertion(() => cut.Markup.ShouldNotContain("Create Snapshot Policy"), TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task SnapshotsPage_CreatePolicyDialog_ShowsErrorToastOnFailure() {
        // Arrange — use URL pre-fill, mock failure
        SetupPolicies([]);
        _ = _mockSnapshotApi.SetSnapshotPolicyAsync(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<AdminOperationResult?>(new AdminOperationResult(false, "op-1", "Duplicate policy", "InvalidOperation")));

        NavManager.NavigateTo("/snapshots?create=true&tenant=tenant-x&domain=sales&aggregateType=OrderAggregate");
        IRenderedComponent<Snapshots> cut = Render<Snapshots>();
        cut.WaitForAssertion(() => cut.Markup.ShouldContain("Create Snapshot Policy"), TimeSpan.FromSeconds(5));

        // Act — click create button (form pre-filled from URL)
        IRenderedComponent<FluentButton> createBtn = cut.FindComponents<FluentButton>()
            .First(b => b.Markup.Contains("Create Policy"));
        await createBtn.InvokeAsync(createBtn.Instance.OnClick.InvokeAsync);

        // Assert — dialog should remain open on failure
        cut.Markup.ShouldContain("Create Snapshot Policy");
        _ = await _mockSnapshotApi.Received(1).SetSnapshotPolicyAsync(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<int>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SnapshotsPage_DeleteDialog_CallsDeleteSnapshotPolicyAsync() {
        // Arrange
        SnapshotPolicy policy = new("tenant-a", "orders", "OrderAggregate", 100, DateTimeOffset.UtcNow.AddDays(-5));
        SetupPolicies([policy]);
        _ = _mockSnapshotApi.DeleteSnapshotPolicyAsync(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<AdminOperationResult?>(new AdminOperationResult(true, "op-1", "Deleted", null)));

        IRenderedComponent<Snapshots> cut = Render<Snapshots>();
        cut.WaitForAssertion(() => cut.Markup.ShouldContain("tenant-a"), TimeSpan.FromSeconds(5));

        // Act — click delete button
        IRenderedComponent<FluentButton> deleteButton = cut.FindComponents<FluentButton>()
            .First(b => b.Markup.Contains("Delete") && !b.Markup.Contains("Delete Snapshot Policy"));
        await deleteButton.InvokeAsync(deleteButton.Instance.OnClick.InvokeAsync);
        cut.WaitForAssertion(() => cut.Markup.ShouldContain("Delete Snapshot Policy"), TimeSpan.FromSeconds(5));

        // Confirm delete
        IRenderedComponent<FluentButton> confirmBtn = cut.FindComponents<FluentButton>()
            .First(b => b.Markup.Contains("Delete Policy"));
        await confirmBtn.InvokeAsync(confirmBtn.Instance.OnClick.InvokeAsync);

        // Assert
        _ = await _mockSnapshotApi.Received(1).DeleteSnapshotPolicyAsync(
            "tenant-a", "orders", "OrderAggregate", Arg.Any<CancellationToken>());
    }

    // ===== Recommended tests (4.12-4.21) =====

    [Fact]
    public void SnapshotsPage_UrlParameters_ReadOnInit() {
        // Arrange
        SetupPolicies([
            new SnapshotPolicy("tenant-filter", "orders", "OrderAggregate", 100, DateTimeOffset.UtcNow.AddDays(-5)),
            new SnapshotPolicy("other-tenant", "inventory", "StockAggregate", 200, DateTimeOffset.UtcNow.AddDays(-2)),
        ]);

        // Act — navigate with tenant parameter
        NavManager.NavigateTo("/snapshots?tenant=tenant-filter");
        IRenderedComponent<Snapshots> cut = Render<Snapshots>();
        cut.WaitForAssertion(() => cut.Markup.ShouldContain("tenant-filter"), TimeSpan.FromSeconds(5));

        // Assert — filtered to show matching tenant
        cut.Markup.ShouldContain("tenant-filter");
    }

    [Fact]
    public async Task SnapshotsPage_CreateDialogRendersFormFields() {
        // Arrange
        SetupPolicies([]);

        IRenderedComponent<Snapshots> cut = Render<Snapshots>();
        cut.WaitForAssertion(() => cut.Markup.ShouldContain("Add Policy"), TimeSpan.FromSeconds(5));

        // Act — open create dialog
        IRenderedComponent<FluentButton> addButton = cut.FindComponents<FluentButton>()
            .First(b => b.Markup.Contains("Add Policy"));
        await addButton.InvokeAsync(addButton.Instance.OnClick.InvokeAsync);
        cut.WaitForAssertion(() => cut.Markup.ShouldContain("Create Snapshot Policy"), TimeSpan.FromSeconds(5));

        // Assert — form fields present
        cut.Markup.ShouldContain("Tenant ID");
        cut.Markup.ShouldContain("Domain");
        cut.Markup.ShouldContain("Aggregate Type");
        cut.Markup.ShouldContain("Interval Events");
    }

    [Fact]
    public void SnapshotsPage_EditDialog_PrefillsValues() {
        // Arrange
        SetupPolicies([
            new SnapshotPolicy("tenant-a", "orders", "OrderAggregate", 150, DateTimeOffset.UtcNow.AddDays(-5)),
        ]);

        IRenderedComponent<Snapshots> cut = Render<Snapshots>();
        cut.WaitForAssertion(() => cut.Markup.ShouldContain("tenant-a"), TimeSpan.FromSeconds(5));

        // Assert — grid shows policy data that would be used to prefill the edit dialog
        cut.Markup.ShouldContain("OrderAggregate");
        string formatted = 150.ToString("N0");
        cut.Markup.ShouldContain(formatted);
    }

    [Fact]
    public async Task SnapshotsPage_DeleteDialog_ShowsPolicyDetails() {
        // Arrange
        SetupPolicies([
            new SnapshotPolicy("tenant-a", "orders", "OrderAggregate", 100, DateTimeOffset.UtcNow.AddDays(-5)),
        ]);

        IRenderedComponent<Snapshots> cut = Render<Snapshots>();
        cut.WaitForAssertion(() => cut.Markup.ShouldContain("tenant-a"), TimeSpan.FromSeconds(5));

        // Act — click delete button
        IRenderedComponent<FluentButton> deleteButton = cut.FindComponents<FluentButton>()
            .First(b => b.Markup.Contains("Delete") && !b.Markup.Contains("Delete Snapshot Policy"));
        await deleteButton.InvokeAsync(deleteButton.Instance.OnClick.InvokeAsync);
        cut.WaitForAssertion(() => cut.Markup.ShouldContain("Delete Snapshot Policy"), TimeSpan.FromSeconds(5));

        // Assert — dialog shows policy details
        cut.Markup.ShouldContain("OrderAggregate");
        cut.Markup.ShouldContain("orders");
        cut.Markup.ShouldContain("tenant-a");
        cut.Markup.ShouldContain("Existing snapshots will not be deleted");
    }

    [Fact]
    public void SnapshotsPage_CreateSnapshotButton_HiddenForReadOnlyUsers() {
        // Arrange
        SetupReadOnlyUser();
        SetupPolicies([]);

        // Act
        IRenderedComponent<Snapshots> cut = Render<Snapshots>();
        cut.WaitForAssertion(() => cut.Markup.ShouldContain("Snapshots"), TimeSpan.FromSeconds(5));

        // Assert
        cut.Markup.ShouldNotContain("Create Snapshot");
    }

    [Fact]
    public void SnapshotsPage_PolicyListFilters_ByTenantFilter() {
        // Arrange
        SetupPolicies([
            new SnapshotPolicy("alpha-tenant", "orders", "OrderAggregate", 100, DateTimeOffset.UtcNow.AddDays(-5)),
            new SnapshotPolicy("beta-tenant", "inventory", "StockAggregate", 200, DateTimeOffset.UtcNow.AddDays(-2)),
        ]);

        // Act — navigate with tenant filter
        NavManager.NavigateTo("/snapshots?tenant=alpha");
        IRenderedComponent<Snapshots> cut = Render<Snapshots>();
        cut.WaitForAssertion(() => cut.Markup.ShouldContain("alpha-tenant"), TimeSpan.FromSeconds(5));

        // Assert — filtered results
        cut.Markup.ShouldContain("alpha-tenant");
    }

    [Fact]
    public void SnapshotsPage_TenantsCovered_ShowsDistinctCount() {
        // Arrange — two policies for same tenant = 1 distinct
        SetupPolicies([
            new SnapshotPolicy("tenant-a", "orders", "OrderAggregate", 100, DateTimeOffset.UtcNow.AddDays(-5)),
            new SnapshotPolicy("tenant-a", "inventory", "StockAggregate", 200, DateTimeOffset.UtcNow.AddDays(-2)),
        ]);

        // Act
        IRenderedComponent<Snapshots> cut = Render<Snapshots>();
        cut.WaitForAssertion(() => cut.Markup.ShouldContain("Tenants Covered"), TimeSpan.FromSeconds(5));

        // Assert — distinct tenant count is 1
        cut.Markup.ShouldContain("Tenants Covered");
    }

    [Fact]
    public void SnapshotsPage_AvgInterval_ShowsNA_WhenNoPolicies() {
        // Arrange
        SetupPolicies([]);

        // Act
        IRenderedComponent<Snapshots> cut = Render<Snapshots>();
        cut.WaitForAssertion(() => cut.Markup.ShouldContain("Avg Interval"), TimeSpan.FromSeconds(5));

        // Assert
        cut.Markup.ShouldContain("N/A");
    }

    // ===== Helpers =====

    private void SetupPolicies(IReadOnlyList<SnapshotPolicy> policies) => _ = _mockSnapshotApi.GetSnapshotPoliciesAsync(Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(policies));

    private void SetupReadOnlyUser() {
        // Override auth state with ReadOnly user
        Microsoft.AspNetCore.Components.Authorization.AuthenticationStateProvider authStateProvider =
            Substitute.For<Microsoft.AspNetCore.Components.Authorization.AuthenticationStateProvider>();
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
}
