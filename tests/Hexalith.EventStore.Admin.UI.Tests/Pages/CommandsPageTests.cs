using System.Reflection;

using Bunit;

using Hexalith.EventStore.Admin.Abstractions.Models.Commands;
using Hexalith.EventStore.Admin.Abstractions.Models.Tenants;
using Hexalith.EventStore.Admin.UI.Components.Shared;
using Hexalith.EventStore.Admin.UI.Pages;
using Hexalith.EventStore.Contracts.Commands;
using Hexalith.EventStore.SignalR;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

using NSubstitute;

namespace Hexalith.EventStore.Admin.UI.Tests.Pages;

/// <summary>
/// bUnit tests for the Commands page.
/// </summary>
public class CommandsPageTests : AdminUITestContext {
    private readonly AdminStreamApiClient _mockApiClient;

    public CommandsPageTests() {
        _mockApiClient = Substitute.For<AdminStreamApiClient>(
            Substitute.For<IHttpClientFactory>(),
            NullLogger<AdminStreamApiClient>.Instance);
        Services.AddScoped(_ => _mockApiClient);
        Services.AddScoped<DashboardRefreshService>();
        TestSignalRClient testClient = new();
        Services.AddSingleton(testClient);
        Services.AddSingleton(testClient.Inner);
    }

    [Fact]
    public void CommandsPage_RendersDataGridWithColumns() {
        // Arrange
        PagedResult<CommandSummary> commands = CreateCommandsResult(3);
        SetupMocks(commands);

        // Act
        IRenderedComponent<Commands> cut = Render<Commands>();
        cut.WaitForAssertion(() => cut.Markup.ShouldContain("tenant-0"), TimeSpan.FromSeconds(5));

        // Assert
        string markup = cut.Markup;
        markup.ShouldContain("Status");
        markup.ShouldContain("Command Type");
        markup.ShouldContain("Tenant");
        markup.ShouldContain("Domain");
        markup.ShouldContain("Aggregate ID");
        markup.ShouldContain("Correlation ID");
        markup.ShouldContain("Timestamp");
        markup.ShouldContain("tenant-0");
        markup.ShouldContain("counter");
        markup.ShouldContain("TestCommand");
    }

    [Fact]
    public void CommandsPage_ShowsEmptyState_WhenNoCommands() {
        // Arrange
        SetupMocks(new PagedResult<CommandSummary>([], 0, null));

        // Act
        IRenderedComponent<Commands> cut = Render<Commands>();
        cut.WaitForAssertion(() => cut.Markup.ShouldContain("No commands processed yet"), TimeSpan.FromSeconds(5));

        // Assert
        cut.Markup.ShouldContain("No commands processed yet");
    }

    [Fact]
    public void CommandsPage_StatusBadge_MapsCommandStatusCorrectly() {
        // Arrange
        List<CommandSummary> items =
        [
            new("t1", "d1", "agg-001", "corr-001", "Cmd1", CommandStatus.Completed, DateTimeOffset.UtcNow, 2, null),
            new("t2", "d2", "agg-002", "corr-002", "Cmd2", CommandStatus.Rejected, DateTimeOffset.UtcNow, null, "Invalid"),
            new("t3", "d3", "agg-003", "corr-003", "Cmd3", CommandStatus.Processing, DateTimeOffset.UtcNow, null, null),
        ];
        SetupMocks(new PagedResult<CommandSummary>(items, 3, null));

        // Act
        IRenderedComponent<Commands> cut = Render<Commands>();
        cut.WaitForAssertion(() => cut.Markup.ShouldContain("Completed"), TimeSpan.FromSeconds(5));

        // Assert
        string markup = cut.Markup;
        markup.ShouldContain("Completed");
        markup.ShouldContain("Rejected");
        markup.ShouldContain("Processing");
    }

    [Fact]
    public async Task CommandsPage_RowClick_NavigatesToStreamDetailWithCorrelation() {
        // Arrange
        CommandSummary command = new(
            "tenant a",
            "orders/sales",
            "agg?123/456",
            "corr id",
            "PlaceOrder",
            CommandStatus.Completed,
            DateTimeOffset.UtcNow,
            3,
            null);
        List<CommandSummary> items =
        [
            command,
        ];
        SetupMocks(new PagedResult<CommandSummary>(items, 1, null));

        // Act
        IRenderedComponent<Commands> cut = Render<Commands>();
        cut.WaitForAssertion(() => cut.Markup.ShouldContain("PlaceOrder"), TimeSpan.FromSeconds(5));

        await cut.InvokeAsync(() => InvokePrivateAsync(cut.Instance, "OnRowClick", command));

        NavManager.Uri.ShouldEndWith(
            "/streams/tenant%20a/orders%2Fsales/agg%3F123%2F456?correlation=corr%20id");
    }

    [Fact]
    public void CommandsPage_ShowsPagination_WhenMoreThan25Commands() {
        // Arrange
        PagedResult<CommandSummary> commands = CreateCommandsResult(30);
        SetupMocks(commands);

        // Act
        IRenderedComponent<Commands> cut = Render<Commands>();
        cut.WaitForAssertion(() => cut.Markup.ShouldContain("Page 1 of 2"), TimeSpan.FromSeconds(5));

        // Assert
        cut.Markup.ShouldContain("Page 1 of 2");
        cut.Markup.ShouldContain("30 total");
    }

    [Fact]
    public void CommandsPage_AggregateId_HasCssTruncation() {
        // Arrange
        List<CommandSummary> items =
        [
            new("t1", "d1", "abcdefghijklmnop", "corr-001", "Cmd1", CommandStatus.Completed, DateTimeOffset.UtcNow, 1, null),
        ];
        SetupMocks(new PagedResult<CommandSummary>(items, 1, null));

        // Act
        IRenderedComponent<Commands> cut = Render<Commands>();
        cut.WaitForAssertion(() => cut.Markup.ShouldContain("abcdefghijklmnop"), TimeSpan.FromSeconds(5));

        // Assert — full text rendered with CSS truncation class and title tooltip
        cut.Markup.ShouldContain("grid-cell-truncate");
        cut.Markup.ShouldContain("title=\"abcdefghijklmnop\"");
    }

    [Fact]
    public void CommandsPage_CorrelationId_HasCssTruncation() {
        // Arrange
        List<CommandSummary> items =
        [
            new("t1", "d1", "agg-001", "correlation1234567890", "Cmd1", CommandStatus.Completed, DateTimeOffset.UtcNow, 1, null),
        ];
        SetupMocks(new PagedResult<CommandSummary>(items, 1, null));

        // Act
        IRenderedComponent<Commands> cut = Render<Commands>();
        cut.WaitForAssertion(() => cut.Markup.ShouldContain("correlation1234567890"), TimeSpan.FromSeconds(5));

        // Assert — full text rendered with CSS truncation class and title tooltip
        cut.Markup.ShouldContain("grid-cell-truncate");
        cut.Markup.ShouldContain("title=\"correlation1234567890\"");
    }

    [Fact]
    public void CommandsPage_StatCards_ComputeCorrectValues() {
        // Arrange: 2 completed, 1 rejected, 1 processing, 1 timed out = 5 total
        List<CommandSummary> items =
        [
            new("t1", "d1", "a1", "c1", "Cmd1", CommandStatus.Completed, DateTimeOffset.UtcNow, 2, null),
            new("t2", "d2", "a2", "c2", "Cmd2", CommandStatus.Completed, DateTimeOffset.UtcNow, 1, null),
            new("t3", "d3", "a3", "c3", "Cmd3", CommandStatus.Rejected, DateTimeOffset.UtcNow, null, "Bad"),
            new("t4", "d4", "a4", "c4", "Cmd4", CommandStatus.Processing, DateTimeOffset.UtcNow, null, null),
            new("t5", "d5", "a5", "c5", "Cmd5", CommandStatus.TimedOut, DateTimeOffset.UtcNow, null, null),
        ];
        SetupMocks(new PagedResult<CommandSummary>(items, 5, null));

        // Act
        IRenderedComponent<Commands> cut = Render<Commands>();
        cut.WaitForAssertion(() => cut.Markup.ShouldContain("Total Commands"), TimeSpan.FromSeconds(5));

        // Assert
        string markup = cut.Markup;
        // Total: 5
        markup.ShouldContain(">5<");
        // Success Rate: 2/5 = 40%
        markup.ShouldContain("40%");
        // Failed: Rejected(1) + TimedOut(1) = 2
        markup.ShouldContain(">2<");
        // In-Flight: Processing(1) = 1
        markup.ShouldContain(">1<");
    }

    [Fact]
    public void CommandsPage_ForbiddenAccess_ShowsAccessDenied() {
        // Arrange
        _ = _mockApiClient.GetRecentCommandsAsync(
            Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns<PagedResult<CommandSummary>>(x => throw new Hexalith.EventStore.Admin.UI.Services.Exceptions.ForbiddenAccessException("Forbidden"));
        _ = _mockApiClient.GetTenantsAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<TenantSummary>>([]));

        // Act
        IRenderedComponent<Commands> cut = Render<Commands>();
        cut.WaitForAssertion(() => cut.Markup.ShouldContain("Access Denied"), TimeSpan.FromSeconds(5));

        // Assert
        cut.Markup.ShouldContain("Access Denied");
    }

    [Fact]
    public void CommandsPage_ServiceUnavailable_ShowsErrorState() {
        // Arrange
        _ = _mockApiClient.GetRecentCommandsAsync(
            Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns<PagedResult<CommandSummary>>(_ => throw new Hexalith.EventStore.Admin.UI.Services.Exceptions.ServiceUnavailableException());
        _ = _mockApiClient.GetTenantsAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<TenantSummary>>([]));

        // Act
        IRenderedComponent<Commands> cut = Render<Commands>();
        cut.WaitForAssertion(() => cut.Markup.ShouldContain("Unable to load commands"), TimeSpan.FromSeconds(5));

        // Assert
        cut.Markup.ShouldContain("admin backend may be unavailable");
    }

    [Fact]
    public void CommandsPage_ShowsFilteredEmptyState_WhenFiltersAreActive() {
        // Arrange
        SetupMocks(new PagedResult<CommandSummary>([], 0, null));

        // Act
        NavManager.NavigateTo("/commands?tenant=tenant-a");
        IRenderedComponent<Commands> cut = Render<Commands>();
        cut.WaitForAssertion(() => cut.Markup.ShouldContain("No commands found"), TimeSpan.FromSeconds(5));

        // Assert
        cut.Markup.ShouldContain("No commands found");
        cut.Markup.ShouldNotContain("No commands processed yet");
    }

    [Fact]
    public void CommandsPage_ShowsLoadingSkeleton() {
        // Arrange — setup a slow-responding mock
        TaskCompletionSource<PagedResult<CommandSummary>> tcs = new();
        _ = _mockApiClient.GetRecentCommandsAsync(
            Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(tcs.Task);
        _ = _mockApiClient.GetTenantsAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<TenantSummary>>([]));

        // Act
        IRenderedComponent<Commands> cut = Render<Commands>();

        // Assert — skeleton is shown while loading
        cut.Markup.ShouldContain("skeleton");

        // Complete the task to clean up
        tcs.SetResult(new PagedResult<CommandSummary>([], 0, null));
    }

    [Fact]
    public void CommandsPage_FromCommandStatus_CoversAllEnumValues() {
        // Assert every CommandStatus value maps to a valid config
        foreach (CommandStatus status in Enum.GetValues<CommandStatus>()) {
            StatusBadge.StatusDisplayConfig config = StatusBadge.StatusDisplayConfig.FromCommandStatus(status);
            config.ShouldNotBeNull();
            config.Label.ShouldNotBeNullOrWhiteSpace();
            config.Icon.ShouldNotBeNullOrWhiteSpace();
            config.CssColor.ShouldNotBeNullOrWhiteSpace();
        }
    }

    [Fact]
    public async Task CommandsPage_FilterUpdatesUrl() {
        // Arrange
        PagedResult<CommandSummary> commands = CreateCommandsResult(5);
        SetupMocks(commands);

        // Act — navigate to /commands before rendering so URL is set
        NavManager.NavigateTo("/commands");
        IRenderedComponent<Commands> cut = Render<Commands>();
        cut.WaitForAssertion(() => cut.Markup.ShouldContain("tenant-0"), TimeSpan.FromSeconds(5));

        SetPrivateField(cut.Instance, "_statusFilter", "Failed");
        SetPrivateField(cut.Instance, "_tenantFilter", "tenant-a");
        SetPrivateField(cut.Instance, "_commandTypeFilter", "Create Order");

        await cut.InvokeAsync(() => InvokePrivateAsync(cut.Instance, "OnCommandTypeFilterChanged"));

        // Assert — query string reflects the selected filters
        string uri = NavManager.Uri;
        uri.ShouldContain("/commands");
        uri.ShouldContain("status=failed");
        uri.ShouldContain("tenant=tenant-a");
        uri.ShouldContain("commandType=Create%20Order");

        _ = _mockApiClient.Received()
            .GetRecentCommandsAsync("tenant-a", "Failed", "Create Order", 1000, Arg.Any<CancellationToken>());
    }

    [Fact]
    public void CommandsPage_LoadsWithUrlFilters() {
        // Arrange
        PagedResult<CommandSummary> commands = CreateUniformCommandsResult(
            30,
            "tenant-a",
            "CreateCommand",
            CommandStatus.Processing);
        _ = _mockApiClient.GetRecentCommandsAsync(
            "tenant-a",
            "Processing",
            "Create",
            1000,
            Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(commands));
        _ = _mockApiClient.GetTenantsAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<TenantSummary>>(
            [
                new TenantSummary("tenant-a", "Tenant A", TenantStatusType.Active),
            ]));

        // Act — navigate with pre-set filters before rendering
        NavManager.NavigateTo("/commands?status=processing&page=2&tenant=tenant-a&commandType=Create");
        IRenderedComponent<Commands> cut = Render<Commands>();
        cut.WaitForAssertion(() => cut.Markup.ShouldContain("Page 2 of 2"), TimeSpan.FromSeconds(5));

        // Assert — URL parameters were applied to the initial API load
        cut.Markup.ShouldContain("Page 2 of 2");
        cut.Markup.ShouldContain("CreateCommand");
        _ = _mockApiClient.Received(1)
            .GetRecentCommandsAsync("tenant-a", "Processing", "Create", 1000, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CommandsPage_RefreshPreservesFiltersPageAndScrollState() {
        // Arrange
        JSInterop.Setup<double>("hexalithAdmin.getScrollTop", _ => true).SetResult(240d);
        PagedResult<CommandSummary> initial = CreateUniformCommandsResult(
            30,
            "tenant-a",
            "CreateCommand",
            CommandStatus.Processing);
        PagedResult<CommandSummary> refreshed = CreateUniformCommandsResult(
            30,
            "tenant-a",
            "CreateCommandRefreshed",
            CommandStatus.Processing);

        _ = _mockApiClient.GetRecentCommandsAsync(
            "tenant-a",
            "Processing",
            "Create",
            1000,
            Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(initial), Task.FromResult(refreshed));
        _ = _mockApiClient.GetTenantsAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<TenantSummary>>(
            [
                new TenantSummary("tenant-a", "Tenant A", TenantStatusType.Active),
            ]));

        NavManager.NavigateTo("/commands?status=processing&page=2&tenant=tenant-a&commandType=Create");
        IRenderedComponent<Commands> cut = Render<Commands>();
        cut.WaitForAssertion(() => cut.Markup.ShouldContain("Page 2 of 2"), TimeSpan.FromSeconds(5));

        DashboardRefreshService refreshService = Services.GetRequiredService<DashboardRefreshService>();

        // Act
        await cut.InvokeAsync(() => {
            RaiseRefresh(refreshService, new DashboardData(null, null));
            return Task.CompletedTask;
        });

        // Assert
        cut.WaitForAssertion(() => cut.Markup.ShouldContain("CreateCommandRefreshed"), TimeSpan.FromSeconds(5));
        cut.Markup.ShouldContain("Page 2 of 2");
        NavManager.Uri.ShouldContain("status=processing");
        NavManager.Uri.ShouldContain("tenant=tenant-a");
        NavManager.Uri.ShouldContain("commandType=Create");
        JSInterop.VerifyInvoke("hexalithAdmin.getScrollTop");
    }

    private void SetupMocks(PagedResult<CommandSummary> commands) {
        _ = _mockApiClient.GetRecentCommandsAsync(
            Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(commands));
        _ = _mockApiClient.GetTenantsAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<TenantSummary>>([]));
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

    private static void RaiseRefresh(DashboardRefreshService refreshService, DashboardData data) {
        FieldInfo? eventField = typeof(DashboardRefreshService)
            .GetField("OnDataChanged", BindingFlags.Instance | BindingFlags.NonPublic);
        Action<DashboardData>? handler = (Action<DashboardData>?)eventField?.GetValue(refreshService);
        handler?.Invoke(data);
    }

    private static void SetPrivateField(object instance, string fieldName, object? value) {
        FieldInfo field = instance.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException($"Field '{fieldName}' not found.");
        field.SetValue(instance, value);
    }

    private static PagedResult<CommandSummary> CreateCommandsResult(int count) {
        List<CommandSummary> items = [];
        for (int i = 0; i < count; i++) {
            items.Add(new CommandSummary(
                $"tenant-{i}",
                "counter",
                $"agg-{i:D8}",
                $"corr-{i:D8}",
                "TestCommand",
                CommandStatus.Completed,
                DateTimeOffset.UtcNow.AddMinutes(-i),
                (i + 1) * 2,
                null));
        }

        return new PagedResult<CommandSummary>(items, count, null);
    }

    private static PagedResult<CommandSummary> CreateUniformCommandsResult(
        int count,
        string tenantId,
        string commandType,
        CommandStatus status) {
        List<CommandSummary> items = [];
        for (int i = 0; i < count; i++) {
            items.Add(new CommandSummary(
                tenantId,
                "orders",
                $"agg-{i:D8}",
                $"corr-{i:D8}",
                commandType,
                status,
                DateTimeOffset.UtcNow.AddMinutes(-i),
                i + 1,
                null));
        }

        return new PagedResult<CommandSummary>(items, count, null);
    }
}
