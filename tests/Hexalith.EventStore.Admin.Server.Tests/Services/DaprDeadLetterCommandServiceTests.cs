#pragma warning disable CS8620 // Nullability mismatch in NSubstitute Returns() with nullable Dapr client methods

using Dapr.Client;

using Hexalith.EventStore.Admin.Abstractions.Models.Common;
using Hexalith.EventStore.Admin.Server.Configuration;
using Hexalith.EventStore.Admin.Server.Services;

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

using NSubstitute;
using NSubstitute.ExceptionExtensions;

namespace Hexalith.EventStore.Admin.Server.Tests.Services;

public class DaprDeadLetterCommandServiceTests
{
    private const string EventStoreAppId = "eventstore";

    private static DaprDeadLetterCommandService CreateService(
        DaprClient? daprClient = null,
        IAdminAuthContext? authContext = null)
    {
        daprClient ??= Substitute.For<DaprClient>();
        authContext ??= new NullAdminAuthContext();
        IOptions<AdminServerOptions> options = Options.Create(new AdminServerOptions
        {
            EventStoreAppId = EventStoreAppId,
            ServiceInvocationTimeoutSeconds = 30,
        });

        return new DaprDeadLetterCommandService(
            daprClient,
            options,
            authContext,
            NullLogger<DaprDeadLetterCommandService>.Instance);
    }

    // === RetryDeadLettersAsync ===

    [Fact]
    public async Task RetryDeadLettersAsync_ReturnsSuccess_WhenEventStoreResponds()
    {
        DaprClient daprClient = Substitute.For<DaprClient>();
        var expected = new AdminOperationResult(true, "op-1", "Retry started", null);

        daprClient.InvokeMethodAsync<AdminOperationResult>(
            Arg.Any<HttpRequestMessage>(),
            Arg.Any<CancellationToken>())
            .Returns(_ => expected);

        DaprDeadLetterCommandService service = CreateService(daprClient);

        AdminOperationResult result = await service.RetryDeadLettersAsync("tenant-a", ["msg-1", "msg-2"]);

        result.Success.ShouldBeTrue();
        result.OperationId.ShouldBe("op-1");
    }

    [Fact]
    public async Task RetryDeadLettersAsync_ForwardsJwtToken()
    {
        DaprClient daprClient = Substitute.For<DaprClient>();
        HttpRequestMessage? capturedRequest = null;
        IAdminAuthContext authContext = Substitute.For<IAdminAuthContext>();
        authContext.GetToken().Returns("dl-token");

        daprClient.InvokeMethodAsync<AdminOperationResult>(
            Arg.Do<HttpRequestMessage>(r => capturedRequest = r),
            Arg.Any<CancellationToken>())
            .Returns(_ => new AdminOperationResult(true, "op-1", null, null));

        DaprDeadLetterCommandService service = CreateService(daprClient, authContext);

        await service.RetryDeadLettersAsync("tenant-a", ["msg-1"]);

        capturedRequest.ShouldNotBeNull();
        capturedRequest!.Headers.Authorization!.Parameter.ShouldBe("dl-token");
    }

    [Fact]
    public async Task RetryDeadLettersAsync_ReturnsError_WhenServiceUnavailable()
    {
        DaprClient daprClient = Substitute.For<DaprClient>();
        daprClient.InvokeMethodAsync<AdminOperationResult>(
            Arg.Any<HttpRequestMessage>(),
            Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("EventStore down"));

        DaprDeadLetterCommandService service = CreateService(daprClient);

        AdminOperationResult result = await service.RetryDeadLettersAsync("tenant-a", ["msg-1"]);

        result.Success.ShouldBeFalse();
        result.ErrorCode.ShouldNotBeNull();
    }

    [Fact]
    public async Task RetryDeadLettersAsync_ReturnsNullResponseError()
    {
        DaprClient daprClient = Substitute.For<DaprClient>();
        daprClient.InvokeMethodAsync<AdminOperationResult>(
            Arg.Any<HttpRequestMessage>(),
            Arg.Any<CancellationToken>())
            .Returns(_ => (AdminOperationResult?)null);

        DaprDeadLetterCommandService service = CreateService(daprClient);

        AdminOperationResult result = await service.RetryDeadLettersAsync("tenant-a", ["msg-1"]);

        result.Success.ShouldBeFalse();
        result.ErrorCode.ShouldBe("NULL_RESPONSE");
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

        DaprDeadLetterCommandService service = CreateService(daprClient);

        await Should.ThrowAsync<OperationCanceledException>(
            () => service.RetryDeadLettersAsync("tenant-a", ["msg-1"], cts.Token));
    }

    [Fact]
    public async Task RetryDeadLettersAsync_ReturnsTimeoutError_WhenServiceTimesOut()
    {
        DaprClient daprClient = Substitute.For<DaprClient>();
        daprClient.InvokeMethodAsync<AdminOperationResult>(
            Arg.Any<HttpRequestMessage>(),
            Arg.Any<CancellationToken>())
            .Returns<AdminOperationResult?>(callInfo =>
            {
                CancellationToken token = callInfo.ArgAt<CancellationToken>(1);
                throw new OperationCanceledException(token);
            });

        IOptions<AdminServerOptions> options = Options.Create(new AdminServerOptions
        {
            EventStoreAppId = EventStoreAppId,
            ServiceInvocationTimeoutSeconds = 0,
        });

        DaprDeadLetterCommandService service = new(
            daprClient,
            options,
            new NullAdminAuthContext(),
            NullLogger<DaprDeadLetterCommandService>.Instance);

        AdminOperationResult result = await service.RetryDeadLettersAsync("tenant-a", ["msg-1"]);

        result.Success.ShouldBeFalse();
        result.ErrorCode.ShouldBe("TIMEOUT");
    }

    // === SkipDeadLettersAsync ===

    [Fact]
    public async Task SkipDeadLettersAsync_ReturnsSuccess_WhenEventStoreResponds()
    {
        DaprClient daprClient = Substitute.For<DaprClient>();
        var expected = new AdminOperationResult(true, "op-2", "Skip complete", null);

        daprClient.InvokeMethodAsync<AdminOperationResult>(
            Arg.Any<HttpRequestMessage>(),
            Arg.Any<CancellationToken>())
            .Returns(_ => expected);

        DaprDeadLetterCommandService service = CreateService(daprClient);

        AdminOperationResult result = await service.SkipDeadLettersAsync("tenant-a", ["msg-1"]);

        result.Success.ShouldBeTrue();
    }

    [Fact]
    public async Task SkipDeadLettersAsync_ReturnsError_WhenServiceUnavailable()
    {
        DaprClient daprClient = Substitute.For<DaprClient>();
        daprClient.InvokeMethodAsync<AdminOperationResult>(
            Arg.Any<HttpRequestMessage>(),
            Arg.Any<CancellationToken>())
            .ThrowsAsync(new HttpRequestException("Connection refused"));

        DaprDeadLetterCommandService service = CreateService(daprClient);

        AdminOperationResult result = await service.SkipDeadLettersAsync("tenant-a", ["msg-1"]);

        result.Success.ShouldBeFalse();
    }

    // === ArchiveDeadLettersAsync ===

    [Fact]
    public async Task ArchiveDeadLettersAsync_ReturnsSuccess_WhenEventStoreResponds()
    {
        DaprClient daprClient = Substitute.For<DaprClient>();
        var expected = new AdminOperationResult(true, "op-3", "Archive complete", null);

        daprClient.InvokeMethodAsync<AdminOperationResult>(
            Arg.Any<HttpRequestMessage>(),
            Arg.Any<CancellationToken>())
            .Returns(_ => expected);

        DaprDeadLetterCommandService service = CreateService(daprClient);

        AdminOperationResult result = await service.ArchiveDeadLettersAsync("tenant-a", ["msg-1"]);

        result.Success.ShouldBeTrue();
    }

    [Fact]
    public async Task ArchiveDeadLettersAsync_ReturnsError_WhenServiceUnavailable()
    {
        DaprClient daprClient = Substitute.For<DaprClient>();
        daprClient.InvokeMethodAsync<AdminOperationResult>(
            Arg.Any<HttpRequestMessage>(),
            Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("EventStore down"));

        DaprDeadLetterCommandService service = CreateService(daprClient);

        AdminOperationResult result = await service.ArchiveDeadLettersAsync("tenant-a", ["msg-1"]);

        result.Success.ShouldBeFalse();
    }

    // === Error code extraction ===

    [Fact]
    public async Task InvokePost_ExtractsHttpStatusCode_FromHttpRequestException()
    {
        DaprClient daprClient = Substitute.For<DaprClient>();
        daprClient.InvokeMethodAsync<AdminOperationResult>(
            Arg.Any<HttpRequestMessage>(),
            Arg.Any<CancellationToken>())
            .ThrowsAsync(new HttpRequestException("Not found", null, System.Net.HttpStatusCode.NotFound));

        DaprDeadLetterCommandService service = CreateService(daprClient);

        AdminOperationResult result = await service.RetryDeadLettersAsync("tenant-a", ["msg-1"]);

        result.Success.ShouldBeFalse();
        result.ErrorCode.ShouldBe("404");
    }
}
