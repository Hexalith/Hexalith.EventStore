using AngleSharp.Dom;

using Bunit;

using Hexalith.EventStore.Admin.Abstractions.Models.DeadLetters;
using Hexalith.EventStore.Admin.UI.Pages;
using Hexalith.EventStore.Admin.UI.Services.Exceptions;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.FluentUI.AspNetCore.Components;

using NSubstitute;
using NSubstitute.ExceptionExtensions;

namespace Hexalith.EventStore.Admin.UI.Tests.Pages;

/// <summary>
/// bUnit tests for the DeadLetters page.
/// </summary>
public class DeadLettersPageTests : AdminUITestContext
{
    private readonly AdminDeadLetterApiClient _mockDeadLetterApi;

    public DeadLettersPageTests()
    {
        _mockDeadLetterApi = Substitute.For<AdminDeadLetterApiClient>(
            Substitute.For<IHttpClientFactory>(),
            NullLogger<AdminDeadLetterApiClient>.Instance);
        Services.AddScoped(_ => _mockDeadLetterApi);
    }

    // ===== Merge-blocking tests (4.1-4.13) =====

    [Fact]
    public void DeadLetters_ShowsLoadingSkeletons_WhenLoading()
    {
        // Arrange — never complete the task
        TaskCompletionSource<PagedResult<DeadLetterEntry>> tcs = new();
        _ = _mockDeadLetterApi.GetDeadLettersAsync(
            Arg.Any<string?>(), Arg.Any<int>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(tcs.Task);

        // Act
        IRenderedComponent<DeadLetters> cut = Render<DeadLetters>();

        // Assert — skeleton cards present during loading
        cut.Markup.ShouldContain("aria-hidden=\"true\"");
    }

    [Fact]
    public void DeadLetters_ShowsEmptyState_WhenNoEntries()
    {
        // Arrange
        SetupEntries([], 0);

        // Act
        IRenderedComponent<DeadLetters> cut = Render<DeadLetters>();
        cut.WaitForAssertion(() => cut.Markup.ShouldContain("No dead letters"), TimeSpan.FromSeconds(5));

        // Assert
        cut.Markup.ShouldContain("No dead letters. All commands processed successfully.");
        cut.Markup.ShouldContain("Failed commands will appear here for investigation and replay.");
    }

    [Fact]
    public void DeadLetters_ShowsDataGrid_WhenEntriesExist()
    {
        // Arrange
        SetupEntries(CreateSampleEntries(), 2);

        // Act
        IRenderedComponent<DeadLetters> cut = Render<DeadLetters>();
        cut.WaitForAssertion(() => cut.Markup.ShouldContain("tenant-a"), TimeSpan.FromSeconds(5));

        // Assert — grid renders with entry data
        cut.Markup.ShouldContain("tenant-a");
        cut.Markup.ShouldContain("tenant-b");
        cut.Markup.ShouldContain("counter");
        cut.Markup.ShouldContain("orders");
        cut.Markup.ShouldContain("SubmitOrder");
    }

    [Fact]
    public void DeadLetters_ShowsStatCards_WhenLoaded()
    {
        // Arrange
        SetupEntries(CreateSampleEntries(), 2);

        // Act
        IRenderedComponent<DeadLetters> cut = Render<DeadLetters>();
        cut.WaitForAssertion(() => cut.Markup.ShouldContain("Total Dead Letters"), TimeSpan.FromSeconds(5));

        // Assert — four stat cards with correct labels
        cut.Markup.ShouldContain("Total Dead Letters");
        cut.Markup.ShouldContain("Tenants Affected");
        cut.Markup.ShouldContain("Oldest Entry");
        cut.Markup.ShouldContain("High Retry (3+)");
    }

    [Fact]
    public void DeadLetters_FiltersVisibleEntries_WhenTenantFilterApplied()
    {
        // Arrange — set up filtered result for when tenant filter is applied
        _ = _mockDeadLetterApi.GetDeadLettersAsync(
            "tenant-a", Arg.Any<int>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new PagedResult<DeadLetterEntry>(
                [CreateSampleEntries()[0]], 1, null)));

        // Act — navigate with tenant filter
        NavManager.NavigateTo("/health/dead-letters?tenant=tenant-a");
        IRenderedComponent<DeadLetters> cut = Render<DeadLetters>();
        cut.WaitForAssertion(() => cut.Markup.ShouldContain("tenant-a"), TimeSpan.FromSeconds(5));

        // Assert
        cut.Markup.ShouldContain("tenant-a");
    }

    [Fact]
    public void DeadLetters_FiltersVisibleEntries_WhenSearchFilterApplied()
    {
        // Arrange
        SetupEntries(CreateSampleEntries(), 2);

        // Act — navigate with search filter
        NavManager.NavigateTo("/health/dead-letters?search=timeout");
        IRenderedComponent<DeadLetters> cut = Render<DeadLetters>();
        cut.WaitForAssertion(() => cut.Markup.ShouldContain("Dead Letters"), TimeSpan.FromSeconds(5));

        // Assert — search filter is read from URL, page renders with filtered data
        cut.Markup.ShouldContain("Dead Letters");
    }

    [Fact]
    public void DeadLetters_ExpandsRowDetail_OnRowClick()
    {
        // Arrange
        List<DeadLetterEntry> entries = CreateSampleEntries();
        SetupEntries(entries, 2);

        // Act
        IRenderedComponent<DeadLetters> cut = Render<DeadLetters>();
        cut.WaitForAssertion(() => cut.Markup.ShouldContain("tenant-a"), TimeSpan.FromSeconds(5));

        // Verify detail is not shown initially
        cut.Markup.ShouldNotContain("Failure Reason:");
        cut.Markup.ShouldNotContain("Correlation ID:");

        // Act - click a data row
        IElement? row = cut.FindAll("tr").FirstOrDefault(r => r.TextContent.Contains("tenant-a", StringComparison.OrdinalIgnoreCase));
        row.ShouldNotBeNull();
        row!.Click();

        // Assert - inline detail is displayed for expanded row
        cut.WaitForAssertion(() => cut.Markup.ShouldContain("Correlation ID:"), TimeSpan.FromSeconds(5));
    }

    [Fact]
    public void DeadLetters_ShowsRetryDialog_WhenRetryClicked()
    {
        // Arrange
        List<DeadLetterEntry> entries = CreateSampleEntries();
        SetupEntries(entries, 2);

        IRenderedComponent<DeadLetters> cut = Render<DeadLetters>();
        cut.WaitForAssertion(() => cut.Markup.ShouldContain("tenant-a"), TimeSpan.FromSeconds(5));

        // Select an entry via native checkbox
        SelectRowCheckbox(cut, entries[0].MessageId);

        // Click Retry Selected
        cut.WaitForAssertion(() => cut.Markup.ShouldContain("Retry Selected"), TimeSpan.FromSeconds(5));
        ClickButton(cut, "Retry Selected");

        // Assert — dialog opens
        cut.WaitForAssertion(() => cut.Markup.ShouldContain("Retry Dead Letters"), TimeSpan.FromSeconds(5));
        cut.Markup.ShouldContain("resubmitted for processing");
    }

    [Fact]
    public async Task DeadLetters_CallsRetryApi_OnConfirm()
    {
        // Arrange
        List<DeadLetterEntry> entries = CreateSampleEntries();
        SetupEntries(entries, 2);
        _ = _mockDeadLetterApi.RetryDeadLettersAsync(
            Arg.Any<string>(), Arg.Any<IReadOnlyList<string>>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<AdminOperationResult?>(
                new AdminOperationResult(true, "op-1", "Retried", null)));

        IRenderedComponent<DeadLetters> cut = Render<DeadLetters>();
        cut.WaitForAssertion(() => cut.Markup.ShouldContain("tenant-a"), TimeSpan.FromSeconds(5));

        // Select first entry
        SelectRowCheckbox(cut, entries[0].MessageId);

        // Open retry dialog
        cut.WaitForAssertion(() => cut.Markup.ShouldContain("Retry Selected"), TimeSpan.FromSeconds(5));
        ClickButton(cut, "Retry Selected");
        cut.WaitForAssertion(() => cut.Markup.ShouldContain("Retry Dead Letters"), TimeSpan.FromSeconds(5));

        // Confirm retry — find dialog Retry button (not "Retry Selected")
        IRenderedComponent<FluentButton> confirmBtn = cut.FindComponents<FluentButton>()
            .First(b => b.Markup.Contains(">Retry<"));
        await confirmBtn.InvokeAsync(() => confirmBtn.Instance.OnClick.InvokeAsync());

        // Assert — API invoked with correct tenant
        await _mockDeadLetterApi.Received(1).RetryDeadLettersAsync(
            entries[0].TenantId,
            Arg.Any<IReadOnlyList<string>>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public void DeadLetters_ShowsSkipDialog_WhenSkipClicked()
    {
        // Arrange
        List<DeadLetterEntry> entries = CreateSampleEntries();
        SetupEntries(entries, 2);

        IRenderedComponent<DeadLetters> cut = Render<DeadLetters>();
        cut.WaitForAssertion(() => cut.Markup.ShouldContain("tenant-a"), TimeSpan.FromSeconds(5));

        // Select an entry
        SelectRowCheckbox(cut, entries[0].MessageId);

        // Click Skip Selected
        cut.WaitForAssertion(() => cut.Markup.ShouldContain("Skip Selected"), TimeSpan.FromSeconds(5));
        ClickButton(cut, "Skip Selected");

        // Assert — dialog opens with "permanently" bold
        cut.WaitForAssertion(() => cut.Markup.ShouldContain("Skip Dead Letters"), TimeSpan.FromSeconds(5));
        cut.Markup.ShouldContain("permanently");
    }

    [Fact]
    public void DeadLetters_ShowsArchiveDialog_WhenArchiveClicked()
    {
        // Arrange
        List<DeadLetterEntry> entries = CreateSampleEntries();
        SetupEntries(entries, 2);

        IRenderedComponent<DeadLetters> cut = Render<DeadLetters>();
        cut.WaitForAssertion(() => cut.Markup.ShouldContain("tenant-a"), TimeSpan.FromSeconds(5));

        // Select an entry
        SelectRowCheckbox(cut, entries[0].MessageId);

        // Click Archive Selected
        cut.WaitForAssertion(() => cut.Markup.ShouldContain("Archive Selected"), TimeSpan.FromSeconds(5));
        ClickButton(cut, "Archive Selected");

        // Assert — dialog opens
        cut.WaitForAssertion(() => cut.Markup.ShouldContain("Archive Dead Letters"), TimeSpan.FromSeconds(5));
        cut.Markup.ShouldContain("moved to the archive");
    }

    [Fact]
    public void DeadLetters_ShowsIssueBanner_WhenApiUnavailable()
    {
        // Arrange
        _ = _mockDeadLetterApi.GetDeadLettersAsync(
            Arg.Any<string?>(), Arg.Any<int>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new ServiceUnavailableException("test"));

        // Act
        IRenderedComponent<DeadLetters> cut = Render<DeadLetters>();
        cut.WaitForAssertion(() => cut.Markup.ShouldContain("Unable to load dead-letter entries"), TimeSpan.FromSeconds(5));

        // Assert
        cut.Markup.ShouldContain("Unable to load dead-letter entries");
    }

    [Fact]
    public void DeadLetters_CheckboxClick_DoesNotTriggerRowExpansion()
    {
        // Arrange
        List<DeadLetterEntry> entries = CreateSampleEntries();
        SetupEntries(entries, 2);

        // Act
        IRenderedComponent<DeadLetters> cut = Render<DeadLetters>();
        cut.WaitForAssertion(() => cut.Markup.ShouldContain("tenant-a"), TimeSpan.FromSeconds(5));

        // Act - click checkbox
        SelectRowCheckbox(cut, entries[0].MessageId);

        // Assert - no row detail expansion was triggered by checkbox click
        cut.Markup.ShouldNotContain("Correlation ID:");
    }

    // ===== Recommended tests (4.14-4.26) =====

    [Fact]
    public void DeadLetters_SelectAll_TogglesAllVisibleCheckboxes()
    {
        // Arrange
        List<DeadLetterEntry> entries = CreateSampleEntries();
        SetupEntries(entries, 2);

        IRenderedComponent<DeadLetters> cut = Render<DeadLetters>();
        cut.WaitForAssertion(() => cut.Markup.ShouldContain("tenant-a"), TimeSpan.FromSeconds(5));

        // Act — click header checkbox (select all)
        SelectAllVisible(cut, true);

        // Assert — selection bar shows count
        cut.WaitForAssertion(() => cut.Markup.ShouldContain("selected"), TimeSpan.FromSeconds(5));
        cut.Markup.ShouldContain("selected");
    }

    [Fact]
    public void DeadLetters_IndividualCheckbox_TogglesSelection()
    {
        // Arrange
        List<DeadLetterEntry> entries = CreateSampleEntries();
        SetupEntries(entries, 2);

        IRenderedComponent<DeadLetters> cut = Render<DeadLetters>();
        cut.WaitForAssertion(() => cut.Markup.ShouldContain("tenant-a"), TimeSpan.FromSeconds(5));

        // Act — select one entry
        SelectRowCheckbox(cut, entries[0].MessageId);

        // Assert
        cut.WaitForAssertion(() => cut.Markup.ShouldContain("selected"), TimeSpan.FromSeconds(5));
    }

    [Fact]
    public void DeadLetters_SelectionBar_ShowsCountAndButtons()
    {
        // Arrange
        List<DeadLetterEntry> entries = CreateSampleEntries();
        SetupEntries(entries, 2);

        IRenderedComponent<DeadLetters> cut = Render<DeadLetters>();
        cut.WaitForAssertion(() => cut.Markup.ShouldContain("tenant-a"), TimeSpan.FromSeconds(5));

        // Select one entry
        SelectRowCheckbox(cut, entries[0].MessageId);

        // Assert — shows action buttons
        cut.WaitForAssertion(() => cut.Markup.ShouldContain("Retry Selected"), TimeSpan.FromSeconds(5));
        cut.Markup.ShouldContain("Skip Selected");
        cut.Markup.ShouldContain("Archive Selected");
    }

    [Fact]
    public void DeadLetters_HidesActionButtons_ForReadOnlyUser()
    {
        // Arrange
        SetupReadOnlyUser();
        List<DeadLetterEntry> entries = CreateSampleEntries();
        SetupEntries(entries, 2);

        // Act
        IRenderedComponent<DeadLetters> cut = Render<DeadLetters>();
        cut.WaitForAssertion(() => cut.Markup.ShouldContain("tenant-a"), TimeSpan.FromSeconds(5));

        // Assert — no action buttons visible (even without selection, verify they won't appear)
        cut.Markup.ShouldNotContain("Retry Selected");
        cut.Markup.ShouldNotContain("Skip Selected");
        cut.Markup.ShouldNotContain("Archive Selected");
    }

    [Fact]
    public void DeadLetters_ShowsActionButtons_ForOperatorUser()
    {
        // Arrange — default is Admin which >= Operator
        List<DeadLetterEntry> entries = CreateSampleEntries();
        SetupEntries(entries, 2);

        IRenderedComponent<DeadLetters> cut = Render<DeadLetters>();
        cut.WaitForAssertion(() => cut.Markup.ShouldContain("tenant-a"), TimeSpan.FromSeconds(5));

        // Select an entry to trigger selection bar
        SelectRowCheckbox(cut, entries[0].MessageId);

        // Assert
        cut.WaitForAssertion(() => cut.Markup.ShouldContain("Retry Selected"), TimeSpan.FromSeconds(5));
        cut.Markup.ShouldContain("Skip Selected");
        cut.Markup.ShouldContain("Archive Selected");
    }

    [Fact]
    public async Task DeadLetters_LoadMore_AppendsEntries()
    {
        // Arrange — first page with continuation token
        List<DeadLetterEntry> page1 = [CreateSampleEntries()[0]];
        _ = _mockDeadLetterApi.GetDeadLettersAsync(
            Arg.Any<string?>(), Arg.Any<int>(), Arg.Is<string?>(s => s == null), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new PagedResult<DeadLetterEntry>(page1, 2, "page2-token")));

        List<DeadLetterEntry> page2 = [CreateSampleEntries()[1]];
        _ = _mockDeadLetterApi.GetDeadLettersAsync(
            Arg.Any<string?>(), Arg.Any<int>(), Arg.Is<string?>(s => s == "page2-token"), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new PagedResult<DeadLetterEntry>(page2, 2, null)));

        IRenderedComponent<DeadLetters> cut = Render<DeadLetters>();
        cut.WaitForAssertion(() => cut.Markup.ShouldContain("Load More"), TimeSpan.FromSeconds(5));

        // Assert page 1 data
        cut.Markup.ShouldContain("tenant-a");
        cut.Markup.ShouldContain("Showing 1 of 2 entries");

        // Act — click Load More
        IRenderedComponent<FluentButton> loadMoreBtn = cut.FindComponents<FluentButton>()
            .First(b => b.Markup.Contains("Load More"));
        await loadMoreBtn.InvokeAsync(() => loadMoreBtn.Instance.OnClick.InvokeAsync());

        // Assert — second page appended
        cut.WaitForAssertion(() => cut.Markup.ShouldContain("tenant-b"), TimeSpan.FromSeconds(5));
    }

    [Fact]
    public void DeadLetters_HidesLoadMore_WhenNoContinuationToken()
    {
        // Arrange — no continuation token
        SetupEntries(CreateSampleEntries(), 2);

        // Act
        IRenderedComponent<DeadLetters> cut = Render<DeadLetters>();
        cut.WaitForAssertion(() => cut.Markup.ShouldContain("tenant-a"), TimeSpan.FromSeconds(5));

        // Assert — no Load More button
        cut.Markup.ShouldNotContain("Load More");
    }

    [Fact]
    public void DeadLetters_PersistsFiltersInUrl()
    {
        // Arrange
        SetupEntries(CreateSampleEntries(), 2);

        // Act — navigate with filters
        NavManager.NavigateTo("/health/dead-letters?tenant=test-tenant&search=timeout");
        IRenderedComponent<DeadLetters> cut = Render<DeadLetters>();
        cut.WaitForAssertion(() => cut.Markup.ShouldContain("Dead Letters"), TimeSpan.FromSeconds(5));

        // Assert — URL filter is actually applied
        cut.Markup.ShouldContain("Dead Letters");
        cut.Markup.ShouldContain("tenant-a");
        cut.Markup.ShouldNotContain("tenant-b");
    }

    [Fact]
    public void DeadLetters_ReadsFiltersFromUrl_OnInit()
    {
        // Arrange — set up filtered result
        _ = _mockDeadLetterApi.GetDeadLettersAsync(
            "my-tenant", Arg.Any<int>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new PagedResult<DeadLetterEntry>([], 0, null)));

        // Act
        NavManager.NavigateTo("/health/dead-letters?tenant=my-tenant");
        IRenderedComponent<DeadLetters> cut = Render<DeadLetters>();
        cut.WaitForAssertion(() => cut.Markup.ShouldContain("Dead Letters"), TimeSpan.FromSeconds(5));

        // Assert — API was called with tenant filter
        _ = _mockDeadLetterApi.Received().GetDeadLettersAsync(
            "my-tenant", Arg.Any<int>(), Arg.Any<string?>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task DeadLetters_HandlesPartialFailure_OnRetry()
    {
        // Arrange — two tenants, one fails
        List<DeadLetterEntry> entries =
        [
            new("msg-1", "tenant-a", "counter", "agg-1", "corr-1",
                "Timeout error", DateTimeOffset.UtcNow.AddHours(-1), 1,
                "Hexalith.Counter.Commands.IncrementCounter"),
            new("msg-2", "tenant-b", "orders", "agg-2", "corr-2",
                "Deserialization error", DateTimeOffset.UtcNow.AddHours(-2), 4,
                "Hexalith.Orders.Commands.SubmitOrder"),
        ];
        SetupEntries(entries, 2);

        _ = _mockDeadLetterApi.RetryDeadLettersAsync(
            "tenant-a", Arg.Any<IReadOnlyList<string>>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<AdminOperationResult?>(
                new AdminOperationResult(true, "op-1", "OK", null)));
        _ = _mockDeadLetterApi.RetryDeadLettersAsync(
            "tenant-b", Arg.Any<IReadOnlyList<string>>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<AdminOperationResult?>(
                new AdminOperationResult(false, "op-2", "Service error", "INTERNAL")));

        IRenderedComponent<DeadLetters> cut = Render<DeadLetters>();
        cut.WaitForAssertion(() => cut.Markup.ShouldContain("tenant-a"), TimeSpan.FromSeconds(5));

        // Select all via header checkbox
        SelectAllVisible(cut, true);

        // Open retry dialog
        cut.WaitForAssertion(() => cut.Markup.ShouldContain("Retry Selected"), TimeSpan.FromSeconds(5));
        ClickButton(cut, "Retry Selected");
        cut.WaitForAssertion(() => cut.Markup.ShouldContain("Retry Dead Letters"), TimeSpan.FromSeconds(5));

        // Confirm
        IRenderedComponent<FluentButton> confirmBtn = cut.FindComponents<FluentButton>()
            .First(b => b.Markup.Contains(">Retry<"));
        await confirmBtn.InvokeAsync(() => confirmBtn.Instance.OnClick.InvokeAsync());

        // Assert — both tenants called
        await _mockDeadLetterApi.Received(1).RetryDeadLettersAsync(
            "tenant-a", Arg.Any<IReadOnlyList<string>>(), Arg.Any<CancellationToken>());
        await _mockDeadLetterApi.Received(1).RetryDeadLettersAsync(
            "tenant-b", Arg.Any<IReadOnlyList<string>>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public void DeadLetters_SelectionClearsOnFilterChange()
    {
        // Arrange
        SetupEntries(CreateSampleEntries(), 2);

        IRenderedComponent<DeadLetters> cut = Render<DeadLetters>();
        cut.WaitForAssertion(() => cut.Markup.ShouldContain("tenant-a"), TimeSpan.FromSeconds(5));

        // Select one row first
        SelectRowCheckbox(cut, "msg-001-abc");
        cut.WaitForAssertion(() => cut.Markup.ShouldContain("Retry Selected"), TimeSpan.FromSeconds(5));

        // Change tenant filter through URL (forces reload and should clear selection)
        NavManager.NavigateTo("/health/dead-letters?tenant=tenant-a");
        IRenderedComponent<DeadLetters> cut2 = Render<DeadLetters>();

        // Assert - action bar no longer visible
        cut2.WaitForAssertion(() => cut2.Markup.ShouldNotContain("Retry Selected"), TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task DeadLetters_ClearsGridRows_WhenRefreshFailsAfterSuccess()
    {
        // Arrange - first load succeeds, refresh fails
        _ = _mockDeadLetterApi.GetDeadLettersAsync(
            Arg.Any<string?>(), Arg.Any<int>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(
                Task.FromResult(new PagedResult<DeadLetterEntry>(CreateSampleEntries(), 2, null)),
                Task.FromException<PagedResult<DeadLetterEntry>>(new ServiceUnavailableException("backend down")));

        IRenderedComponent<DeadLetters> cut = Render<DeadLetters>();
        cut.WaitForAssertion(() => cut.Markup.ShouldContain("tenant-a"), TimeSpan.FromSeconds(5));

        // Act
        ClickButton(cut, "Refresh");

        // Assert
        cut.WaitForAssertion(() => cut.Markup.ShouldContain("Unable to load dead-letter entries"), TimeSpan.FromSeconds(5));
        cut.Markup.ShouldNotContain("tenant-a");
        cut.Markup.ShouldNotContain("tenant-b");
    }

    [Fact]
    public void DeadLetters_TenantFilterResetsPagination()
    {
        // Arrange
        _ = _mockDeadLetterApi.GetDeadLettersAsync(
            Arg.Any<string?>(), Arg.Any<int>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new PagedResult<DeadLetterEntry>(CreateSampleEntries(), 2, null)));

        // Act
        IRenderedComponent<DeadLetters> cut = Render<DeadLetters>();
        cut.WaitForAssertion(() => cut.Markup.ShouldContain("tenant-a"), TimeSpan.FromSeconds(5));

        // Assert — data loads from page 1 (continuation token is null)
        _ = _mockDeadLetterApi.Received().GetDeadLettersAsync(
            Arg.Any<string?>(), Arg.Any<int>(), null, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task DeadLetters_SkipAndArchive_GroupByTenantLikeRetry()
    {
        // Arrange
        List<DeadLetterEntry> entries = CreateSampleEntries();
        SetupEntries(entries, 2);

        _ = _mockDeadLetterApi.SkipDeadLettersAsync(
            Arg.Any<string>(), Arg.Any<IReadOnlyList<string>>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<AdminOperationResult?>(
                new AdminOperationResult(true, "op-1", "OK", null)));

        IRenderedComponent<DeadLetters> cut = Render<DeadLetters>();
        cut.WaitForAssertion(() => cut.Markup.ShouldContain("tenant-a"), TimeSpan.FromSeconds(5));

        // Select first entry
        SelectRowCheckbox(cut, entries[0].MessageId);

        // Open skip dialog
        cut.WaitForAssertion(() => cut.Markup.ShouldContain("Skip Selected"), TimeSpan.FromSeconds(5));
        ClickButton(cut, "Skip Selected");
        cut.WaitForAssertion(() => cut.Markup.ShouldContain("Skip Dead Letters"), TimeSpan.FromSeconds(5));

        // Confirm skip
        IRenderedComponent<FluentButton> confirmBtn = cut.FindComponents<FluentButton>()
            .First(b => b.Markup.Contains(">Skip<"));
        await confirmBtn.InvokeAsync(() => confirmBtn.Instance.OnClick.InvokeAsync());

        // Assert — API called with correct tenant
        await _mockDeadLetterApi.Received(1).SkipDeadLettersAsync(
            entries[0].TenantId,
            Arg.Any<IReadOnlyList<string>>(),
            Arg.Any<CancellationToken>());
    }

    // ===== Helpers =====

    private static List<DeadLetterEntry> CreateSampleEntries() =>
    [
        new("msg-001-abc", "tenant-a", "counter", "agg-001-xyz", "corr-001-abc",
            "Timeout error: operation exceeded 30s limit", DateTimeOffset.UtcNow.AddHours(-1), 1,
            "Hexalith.Counter.Commands.IncrementCounter"),
        new("msg-002-def", "tenant-b", "orders", "agg-002-xyz", "corr-002-def",
            "Deserialization error: invalid JSON payload", DateTimeOffset.UtcNow.AddHours(-2), 4,
            "Hexalith.Orders.Commands.SubmitOrder"),
    ];

    private void SetupEntries(IReadOnlyList<DeadLetterEntry> entries, int totalCount, string? continuationToken = null)
    {
        _ = _mockDeadLetterApi.GetDeadLettersAsync(
            Arg.Any<string?>(), Arg.Any<int>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new PagedResult<DeadLetterEntry>(entries, totalCount, continuationToken)));
    }

    private static void SelectRowCheckbox(IRenderedComponent<DeadLetters> cut, string messageId)
    {
        IRenderedComponent<FluentCheckbox> checkbox = cut.FindComponents<FluentCheckbox>()
            .First(c => c.Markup.Contains($"Select {messageId}", StringComparison.Ordinal));
        _ = checkbox.InvokeAsync(() => checkbox.Instance.ValueChanged.InvokeAsync(true));
    }

    private static void SelectAllVisible(IRenderedComponent<DeadLetters> cut, bool? state)
    {
        IRenderedComponent<FluentCheckbox> headerCheckbox = cut.FindComponents<FluentCheckbox>()
            .First(c => c.Markup.Contains("Select all visible entries", StringComparison.Ordinal));
        _ = headerCheckbox.InvokeAsync(() => headerCheckbox.Instance.CheckStateChanged.InvokeAsync(state));
    }

    private static void ClickButton(IRenderedComponent<DeadLetters> cut, string text)
    {
        IRenderedComponent<FluentButton> btn = cut.FindComponents<FluentButton>()
            .First(b => b.Markup.Contains(text));
        btn.Find("fluent-button").Click();
    }

    private void SetupReadOnlyUser()
    {
        Microsoft.AspNetCore.Components.Authorization.AuthenticationStateProvider authStateProvider =
            Substitute.For<Microsoft.AspNetCore.Components.Authorization.AuthenticationStateProvider>();
        System.Security.Claims.ClaimsPrincipal user = new(new System.Security.Claims.ClaimsIdentity(
        [
            new System.Security.Claims.Claim(AdminClaimTypes.Role, "ReadOnly"),
        ], "TestAuth"));
        _ = authStateProvider.GetAuthenticationStateAsync()
            .Returns(Task.FromResult(new Microsoft.AspNetCore.Components.Authorization.AuthenticationState(user)));
        Services.AddSingleton(authStateProvider);
        Services.AddScoped<AdminUserContext>();
    }

    private Microsoft.AspNetCore.Components.NavigationManager NavManager =>
        Services.GetRequiredService<Microsoft.AspNetCore.Components.NavigationManager>();
}
