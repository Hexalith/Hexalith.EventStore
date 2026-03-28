#pragma warning disable CS8620 // Nullability mismatch in NSubstitute Returns() with nullable Dapr client methods

using System.Net;

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

public class DaprDeadLetterServiceTests {
    private const string StateStoreName = "statestore";
    private const string EventStoreAppId = "eventstore";

    private static DaprDeadLetterQueryService CreateQueryService(DaprClient? daprClient = null) {
        daprClient ??= Substitute.For<DaprClient>();
        IOptions<AdminServerOptions> options = Options.Create(new AdminServerOptions {
            StateStoreName = StateStoreName,
        });

        return new DaprDeadLetterQueryService(
            daprClient,
            options,
            NullLogger<DaprDeadLetterQueryService>.Instance);
    }

    private static DaprDeadLetterCommandService CreateCommandService(DaprClient? daprClient = null) {
        daprClient ??= Substitute.For<DaprClient>();
        IOptions<AdminServerOptions> options = Options.Create(new AdminServerOptions {
            EventStoreAppId = EventStoreAppId,
        });

        return new DaprDeadLetterCommandService(
            daprClient,
            options,
            new NullAdminAuthContext(),
            NullLogger<DaprDeadLetterCommandService>.Instance);
    }

    [Fact]
    public async Task ListDeadLettersAsync_ReturnsEntries_WhenIndexExists() {
        DaprClient daprClient = Substitute.For<DaprClient>();
        var entries = new List<DeadLetterEntry>
        {
            new("msg-1", "tenant1", "orders", "order-1", "corr-1", "Validation failed", DateTimeOffset.UtcNow, 0, "CreateOrder"),
            new("msg-2", "tenant1", "orders", "order-2", "corr-2", "Timeout", DateTimeOffset.UtcNow, 1, "UpdateOrder"),
        };

        daprClient.GetStateAsync<List<DeadLetterEntry>>(
            StateStoreName,
            "admin:dead-letters:tenant1",
            cancellationToken: Arg.Any<CancellationToken>())
            .Returns(_ => entries);

        DaprDeadLetterQueryService service = CreateQueryService(daprClient);

        PagedResult<DeadLetterEntry> result = await service.ListDeadLettersAsync("tenant1", 10, null);

        result.Items.Count.ShouldBe(2);
        result.TotalCount.ShouldBe(2);
        result.ContinuationToken.ShouldBeNull();
    }

    [Fact]
    public async Task ListDeadLettersAsync_ReturnsPaginated_WithContinuationToken() {
        DaprClient daprClient = Substitute.For<DaprClient>();
        var entries = new List<DeadLetterEntry>
        {
            new("msg-1", "t1", "d1", "a1", "c1", "err1", DateTimeOffset.UtcNow, 0, "Cmd1"),
            new("message-two", "tenant-one", "domain-one", "aggregate-two", "correlation-two", "error-two", DateTimeOffset.UtcNow, 0, "RetryDeadLetter"),
            new("msg-3", "t1", "d1", "a3", "c3", "err3", DateTimeOffset.UtcNow, 0, "Cmd3"),
        };

        daprClient.GetStateAsync<List<DeadLetterEntry>>(
            StateStoreName,
            "admin:dead-letters:t1",
            cancellationToken: Arg.Any<CancellationToken>())
            .Returns(_ => entries);

        DaprDeadLetterQueryService service = CreateQueryService(daprClient);

        // First page
        PagedResult<DeadLetterEntry> page1 = await service.ListDeadLettersAsync("t1", 2, null);
        page1.Items.Count.ShouldBe(2);
        page1.ContinuationToken.ShouldBe("2");

        // Second page
        PagedResult<DeadLetterEntry> page2 = await service.ListDeadLettersAsync("t1", 2, page1.ContinuationToken);
        page2.Items.Count.ShouldBe(1);
        page2.ContinuationToken.ShouldBeNull();
    }

    [Fact]
    public async Task ListDeadLettersAsync_IgnoresNegativeContinuationToken() {
        DaprClient daprClient = Substitute.For<DaprClient>();
        var entries = new List<DeadLetterEntry>
        {
            new("msg-1", "t1", "d1", "a1", "corr-1", "err1", DateTimeOffset.UtcNow, 0, "Cmd1"),
            new("message-two", "tenant-one", "domain-one", "aggregate-two", "correlation-two", "error-two", DateTimeOffset.UtcNow, 0, "RetryDeadLetter"),
        };

        daprClient.GetStateAsync<List<DeadLetterEntry>>(
            StateStoreName,
            "admin:dead-letters:t1",
            cancellationToken: Arg.Any<CancellationToken>())
            .Returns(_ => entries);

        DaprDeadLetterQueryService service = CreateQueryService(daprClient);

        PagedResult<DeadLetterEntry> result = await service.ListDeadLettersAsync("t1", 1, "-5");

        result.Items.Count.ShouldBe(1);
        result.Items[0].MessageId.ShouldBe("msg-1");
        result.ContinuationToken.ShouldBe("1");
    }

    [Fact]
    public async Task ListDeadLettersAsync_ReturnsEmpty_WhenIndexNotFound() {
        DaprClient daprClient = Substitute.For<DaprClient>();
        daprClient.GetStateAsync<List<DeadLetterEntry>>(
            StateStoreName,
            Arg.Any<string>(),
            cancellationToken: Arg.Any<CancellationToken>())
            .Returns(_ => (List<DeadLetterEntry>?)null);

        DaprDeadLetterQueryService service = CreateQueryService(daprClient);

        PagedResult<DeadLetterEntry> result = await service.ListDeadLettersAsync("tenant1", 10, null);

        result.Items.ShouldBeEmpty();
        result.TotalCount.ShouldBe(0);
    }

    [Fact]
    public async Task RetryDeadLettersAsync_DelegatesToEventStore() {
        DaprClient daprClient = Substitute.For<DaprClient>();
        var expected = new AdminOperationResult(true, "op-1", null, null);

        daprClient.InvokeMethodAsync<AdminOperationResult>(
            Arg.Any<HttpRequestMessage>(),
            Arg.Any<CancellationToken>())
            .Returns(_ => expected);

        DaprDeadLetterCommandService service = CreateCommandService(daprClient);

        AdminOperationResult result = await service.RetryDeadLettersAsync("tenant1", ["msg-1", "msg-2"]);

        result.Success.ShouldBeTrue();
    }

    [Fact]
    public async Task SkipDeadLettersAsync_DelegatesToEventStore() {
        DaprClient daprClient = Substitute.For<DaprClient>();
        var expected = new AdminOperationResult(true, "op-1", null, null);

        daprClient.InvokeMethodAsync<AdminOperationResult>(
            Arg.Any<HttpRequestMessage>(),
            Arg.Any<CancellationToken>())
            .Returns(_ => expected);

        DaprDeadLetterCommandService service = CreateCommandService(daprClient);

        AdminOperationResult result = await service.SkipDeadLettersAsync("tenant1", ["msg-1"]);

        result.Success.ShouldBeTrue();
    }

    [Fact]
    public async Task ArchiveDeadLettersAsync_DelegatesToEventStore() {
        DaprClient daprClient = Substitute.For<DaprClient>();
        var expected = new AdminOperationResult(true, "op-1", null, null);

        daprClient.InvokeMethodAsync<AdminOperationResult>(
            Arg.Any<HttpRequestMessage>(),
            Arg.Any<CancellationToken>())
            .Returns(_ => expected);

        DaprDeadLetterCommandService service = CreateCommandService(daprClient);

        AdminOperationResult result = await service.ArchiveDeadLettersAsync("tenant1", ["msg-1"]);

        result.Success.ShouldBeTrue();
    }

    [Fact]
    public async Task RetryDeadLettersAsync_ReturnsFailure_WhenExceptionThrown() {
        DaprClient daprClient = Substitute.For<DaprClient>();
        daprClient.InvokeMethodAsync<AdminOperationResult>(
            Arg.Any<HttpRequestMessage>(),
            Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("Service down"));

        DaprDeadLetterCommandService service = CreateCommandService(daprClient);

        AdminOperationResult result = await service.RetryDeadLettersAsync("tenant1", ["msg-1"]);

        result.Success.ShouldBeFalse();
        result.Message!.ShouldContain("Service down");
    }

    [Fact]
    public async Task ListDeadLettersAsync_PropagatesCancellation()
    {
        using CancellationTokenSource cts = new();
        await cts.CancelAsync();

        DaprClient daprClient = Substitute.For<DaprClient>();
        daprClient.GetStateAsync<List<DeadLetterEntry>>(
            StateStoreName,
            Arg.Any<string>(),
            cancellationToken: Arg.Any<CancellationToken>())
            .Returns<List<DeadLetterEntry>?>(_ => throw new OperationCanceledException());

        DaprDeadLetterQueryService service = CreateQueryService(daprClient);

        await Should.ThrowAsync<OperationCanceledException>(
            () => service.ListDeadLettersAsync("tenant1", 10, null, cts.Token));
    }

    [Fact]
    public async Task RetryDeadLettersAsync_PropagatesCancellation()
    {
        using CancellationTokenSource cts = new();
        await cts.CancelAsync();

        DaprClient daprClient = Substitute.For<DaprClient>();
        daprClient.InvokeMethodAsync<AdminOperationResult>(
            Arg.Any<HttpRequestMessage>(),
            Arg.Any<CancellationToken>())
            .Returns<AdminOperationResult?>(_ => throw new OperationCanceledException());

        DaprDeadLetterCommandService service = CreateCommandService(daprClient);

        await Should.ThrowAsync<OperationCanceledException>(
            () => service.RetryDeadLettersAsync("tenant1", ["msg-1"], cts.Token));
    }

    [Fact]
    public async Task RetryDeadLettersAsync_MapsHttpStatusCode_WhenRequestFails() {
        DaprClient daprClient = Substitute.For<DaprClient>();
        daprClient.InvokeMethodAsync<AdminOperationResult>(
            Arg.Any<HttpRequestMessage>(),
            Arg.Any<CancellationToken>())
            .ThrowsAsync(new HttpRequestException("Unauthorized", null, HttpStatusCode.Unauthorized));

        DaprDeadLetterCommandService service = CreateCommandService(daprClient);

        AdminOperationResult result = await service.RetryDeadLettersAsync("tenant1", ["msg-1"]);

        result.Success.ShouldBeFalse();
        result.ErrorCode.ShouldBe("401");
    }
}
