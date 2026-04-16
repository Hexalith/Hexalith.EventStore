#pragma warning disable CS8620 // Nullability mismatch in NSubstitute Returns() with nullable Dapr client methods

using Dapr.Client;

using Hexalith.EventStore.Admin.Abstractions.Models.Common;
using Hexalith.EventStore.Admin.Abstractions.Models.DeadLetters;
using Hexalith.EventStore.Admin.Server.Configuration;
using Hexalith.EventStore.Admin.Server.Services;

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

using NSubstitute;
using NSubstitute.ExceptionExtensions;

namespace Hexalith.EventStore.Admin.Server.Tests.Services;

public class DaprDeadLetterQueryServiceTests {
    private const string StateStoreName = "statestore";

    private static DaprDeadLetterQueryService CreateService(DaprClient? daprClient = null) {
        daprClient ??= Substitute.For<DaprClient>();
        IOptions<AdminServerOptions> options = Options.Create(new AdminServerOptions {
            StateStoreName = StateStoreName,
        });

        return new DaprDeadLetterQueryService(
            daprClient,
            options,
            NullLogger<DaprDeadLetterQueryService>.Instance);
    }

    private static DeadLetterEntry CreateDeadLetter(string messageId, string tenantId)
        => new(
            messageId,
            tenantId,
            "Counter",
            "counter-1",
            "corr-1",
            "Timeout",
            DateTimeOffset.UtcNow,
            1,
            "IncrementCounter");

    // === GetDeadLetterCountAsync ===

    [Fact]
    public async Task GetDeadLetterCountAsync_ReturnsCount_WhenIndexExists() {
        DaprClient daprClient = Substitute.For<DaprClient>();
        var entries = new List<DeadLetterEntry>
        {
            CreateDeadLetter("msg-1", "tenant-a"),
            CreateDeadLetter("msg-2", "tenant-a"),
            CreateDeadLetter("msg-3", "tenant-b"),
        };

        _ = daprClient.GetStateAsync<List<DeadLetterEntry>>(
            StateStoreName,
            "admin:dead-letters:all",
            cancellationToken: Arg.Any<CancellationToken>())
            .Returns(_ => entries);

        DaprDeadLetterQueryService service = CreateService(daprClient);

        int count = await service.GetDeadLetterCountAsync();

        count.ShouldBe(3);
    }

    [Fact]
    public async Task GetDeadLetterCountAsync_ReturnsZero_WhenIndexNotFound() {
        DaprClient daprClient = Substitute.For<DaprClient>();
        _ = daprClient.GetStateAsync<List<DeadLetterEntry>>(
            StateStoreName,
            "admin:dead-letters:all",
            cancellationToken: Arg.Any<CancellationToken>())
            .Returns(_ => (List<DeadLetterEntry>?)null);

        DaprDeadLetterQueryService service = CreateService(daprClient);

        int count = await service.GetDeadLetterCountAsync();

        count.ShouldBe(0);
    }

    // === ListDeadLettersAsync ===

    [Fact]
    public async Task ListDeadLettersAsync_ReturnsTenantEntries_WhenTenantIdProvided() {
        DaprClient daprClient = Substitute.For<DaprClient>();
        var entries = new List<DeadLetterEntry>
        {
            CreateDeadLetter("msg-1", "tenant-a"),
            CreateDeadLetter("msg-2", "tenant-a"),
        };

        _ = daprClient.GetStateAsync<List<DeadLetterEntry>>(
            StateStoreName,
            "admin:dead-letters:tenant-a",
            cancellationToken: Arg.Any<CancellationToken>())
            .Returns(_ => entries);

        DaprDeadLetterQueryService service = CreateService(daprClient);

        PagedResult<DeadLetterEntry> result = await service.ListDeadLettersAsync("tenant-a", 10, null);

        result.Items.Count.ShouldBe(2);
        result.TotalCount.ShouldBe(2);
    }

    [Fact]
    public async Task ListDeadLettersAsync_ReturnsAllEntries_WhenTenantIdIsNull() {
        DaprClient daprClient = Substitute.For<DaprClient>();
        var entries = new List<DeadLetterEntry>
        {
            CreateDeadLetter("msg-1", "tenant-a"),
            CreateDeadLetter("msg-2", "tenant-b"),
        };

        _ = daprClient.GetStateAsync<List<DeadLetterEntry>>(
            StateStoreName,
            "admin:dead-letters:all",
            cancellationToken: Arg.Any<CancellationToken>())
            .Returns(_ => entries);

        DaprDeadLetterQueryService service = CreateService(daprClient);

        PagedResult<DeadLetterEntry> result = await service.ListDeadLettersAsync(null, 10, null);

        result.Items.Count.ShouldBe(2);
    }

    [Fact]
    public async Task ListDeadLettersAsync_ReturnsEmpty_WhenIndexNotFound() {
        DaprClient daprClient = Substitute.For<DaprClient>();
        _ = daprClient.GetStateAsync<List<DeadLetterEntry>>(
            StateStoreName,
            Arg.Any<string>(),
            cancellationToken: Arg.Any<CancellationToken>())
            .Returns(_ => (List<DeadLetterEntry>?)null);

        DaprDeadLetterQueryService service = CreateService(daprClient);

        PagedResult<DeadLetterEntry> result = await service.ListDeadLettersAsync("tenant-a", 10, null);

        result.Items.ShouldBeEmpty();
        result.TotalCount.ShouldBe(0);
    }

    [Fact]
    public async Task ListDeadLettersAsync_Throws_WhenExceptionThrown() {
        DaprClient daprClient = Substitute.For<DaprClient>();
        _ = daprClient.GetStateAsync<List<DeadLetterEntry>>(
            StateStoreName,
            Arg.Any<string>(),
            cancellationToken: Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("State store down"));

        DaprDeadLetterQueryService service = CreateService(daprClient);

        _ = await Should.ThrowAsync<InvalidOperationException>(
            () => service.ListDeadLettersAsync("tenant-a", 10, null));
    }

    [Fact]
    public async Task ListDeadLettersAsync_PaginatesCorrectly_WithContinuationToken() {
        DaprClient daprClient = Substitute.For<DaprClient>();
        var entries = new List<DeadLetterEntry>
        {
            CreateDeadLetter("msg-1", "tenant-a"),
            CreateDeadLetter("msg-2", "tenant-a"),
            CreateDeadLetter("msg-3", "tenant-a"),
            CreateDeadLetter("msg-4", "tenant-a"),
            CreateDeadLetter("msg-5", "tenant-a"),
        };

        _ = daprClient.GetStateAsync<List<DeadLetterEntry>>(
            StateStoreName,
            "admin:dead-letters:tenant-a",
            cancellationToken: Arg.Any<CancellationToken>())
            .Returns(_ => entries);

        DaprDeadLetterQueryService service = CreateService(daprClient);

        // First page
        PagedResult<DeadLetterEntry> page1 = await service.ListDeadLettersAsync("tenant-a", 2, null);
        page1.Items.Count.ShouldBe(2);
        page1.TotalCount.ShouldBe(5);
        _ = page1.ContinuationToken.ShouldNotBeNull();

        // Second page
        PagedResult<DeadLetterEntry> page2 = await service.ListDeadLettersAsync("tenant-a", 2, page1.ContinuationToken);
        page2.Items.Count.ShouldBe(2);
        _ = page2.ContinuationToken.ShouldNotBeNull();

        // Last page
        PagedResult<DeadLetterEntry> page3 = await service.ListDeadLettersAsync("tenant-a", 2, page2.ContinuationToken);
        page3.Items.Count.ShouldBe(1);
        page3.ContinuationToken.ShouldBeNull();
    }

    [Fact]
    public async Task ListDeadLettersAsync_PropagatesCancellation() {
        using CancellationTokenSource cts = new();
        await cts.CancelAsync();

        DaprClient daprClient = Substitute.For<DaprClient>();
        _ = daprClient.GetStateAsync<List<DeadLetterEntry>>(
            StateStoreName,
            Arg.Any<string>(),
            cancellationToken: Arg.Any<CancellationToken>())
            .Returns<List<DeadLetterEntry>?>(_ => throw new OperationCanceledException());

        DaprDeadLetterQueryService service = CreateService(daprClient);

        _ = await Should.ThrowAsync<OperationCanceledException>(
            () => service.ListDeadLettersAsync("tenant-a", 10, null, cts.Token));
    }
}
