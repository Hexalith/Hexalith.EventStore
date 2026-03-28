#pragma warning disable CS8620 // Nullability mismatch in NSubstitute Returns() with nullable Dapr client methods

using System.Net;

using Dapr.Client;

using Hexalith.EventStore.Admin.Abstractions.Models.Common;
using Hexalith.EventStore.Admin.Server.Configuration;
using Hexalith.EventStore.Admin.Server.Services;

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

using NSubstitute;
using NSubstitute.ExceptionExtensions;

namespace Hexalith.EventStore.Admin.Server.Tests.Services;

public class DaprProjectionCommandServiceTests {
    private const string EventStoreAppId = "eventstore";

    private static DaprProjectionCommandService CreateService(
        DaprClient? daprClient = null,
        IAdminAuthContext? authContext = null) {
        daprClient ??= Substitute.For<DaprClient>();
        authContext ??= new NullAdminAuthContext();

        IOptions<AdminServerOptions> options = Options.Create(new AdminServerOptions {
            EventStoreAppId = EventStoreAppId,
        });

        return new DaprProjectionCommandService(
            daprClient,
            options,
            authContext,
            NullLogger<DaprProjectionCommandService>.Instance);
    }

    [Fact]
    public async Task PauseProjectionAsync_DelegatesToEventStore() {
        DaprClient daprClient = Substitute.For<DaprClient>();
        var expected = new AdminOperationResult(true, "op-1", null, null);

        daprClient.InvokeMethodAsync<AdminOperationResult>(
            Arg.Any<HttpRequestMessage>(),
            Arg.Any<CancellationToken>())
            .Returns(_ => expected);

        DaprProjectionCommandService service = CreateService(daprClient);

        AdminOperationResult result = await service.PauseProjectionAsync("tenant1", "OrderSummary");

        result.Success.ShouldBeTrue();
    }

    [Fact]
    public async Task ResumeProjectionAsync_DelegatesToEventStore() {
        DaprClient daprClient = Substitute.For<DaprClient>();
        var expected = new AdminOperationResult(true, "op-1", null, null);

        daprClient.InvokeMethodAsync<AdminOperationResult>(
            Arg.Any<HttpRequestMessage>(),
            Arg.Any<CancellationToken>())
            .Returns(_ => expected);

        DaprProjectionCommandService service = CreateService(daprClient);

        AdminOperationResult result = await service.ResumeProjectionAsync("tenant1", "OrderSummary");

        result.Success.ShouldBeTrue();
    }

    [Fact]
    public async Task PauseProjectionAsync_ReturnsFailure_WhenExceptionThrown() {
        DaprClient daprClient = Substitute.For<DaprClient>();
        daprClient.InvokeMethodAsync<AdminOperationResult>(
            Arg.Any<HttpRequestMessage>(),
            Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("Service unavailable"));

        DaprProjectionCommandService service = CreateService(daprClient);

        AdminOperationResult result = await service.PauseProjectionAsync("tenant1", "OrderSummary");

        result.Success.ShouldBeFalse();
        result.Message!.ShouldContain("Service unavailable");
    }

    [Fact]
    public async Task ResetProjectionAsync_DelegatesToEventStore() {
        DaprClient daprClient = Substitute.For<DaprClient>();
        var expected = new AdminOperationResult(true, "op-1", null, null);

        daprClient.InvokeMethodAsync<AdminOperationResult>(
            Arg.Any<HttpRequestMessage>(),
            Arg.Any<CancellationToken>())
            .Returns(_ => expected);

        DaprProjectionCommandService service = CreateService(daprClient);

        AdminOperationResult result = await service.ResetProjectionAsync("tenant1", "OrderSummary", 0);

        result.Success.ShouldBeTrue();
    }

    [Fact]
    public async Task ReplayProjectionAsync_DelegatesToEventStore() {
        DaprClient daprClient = Substitute.For<DaprClient>();
        var expected = new AdminOperationResult(true, "op-1", null, null);

        daprClient.InvokeMethodAsync<AdminOperationResult>(
            Arg.Any<HttpRequestMessage>(),
            Arg.Any<CancellationToken>())
            .Returns(_ => expected);

        DaprProjectionCommandService service = CreateService(daprClient);

        AdminOperationResult result = await service.ReplayProjectionAsync("tenant1", "OrderSummary", 0, 100);

        result.Success.ShouldBeTrue();
    }

    [Fact]
    public async Task PauseProjectionAsync_PropagatesCancellation()
    {
        using CancellationTokenSource cts = new();
        await cts.CancelAsync();

        DaprClient daprClient = Substitute.For<DaprClient>();
        daprClient.InvokeMethodAsync<AdminOperationResult>(
            Arg.Any<HttpRequestMessage>(),
            Arg.Any<CancellationToken>())
            .Returns<AdminOperationResult?>(_ => throw new OperationCanceledException());

        DaprProjectionCommandService service = CreateService(daprClient);

        await Should.ThrowAsync<OperationCanceledException>(
            () => service.PauseProjectionAsync("tenant1", "OrderSummary", cts.Token));
    }

    [Fact]
    public async Task PauseProjectionAsync_MapsHttpStatusCode_WhenRequestFails() {
        DaprClient daprClient = Substitute.For<DaprClient>();
        daprClient.InvokeMethodAsync<AdminOperationResult>(
            Arg.Any<HttpRequestMessage>(),
            Arg.Any<CancellationToken>())
            .ThrowsAsync(new HttpRequestException("Conflict", null, HttpStatusCode.Conflict));

        DaprProjectionCommandService service = CreateService(daprClient);

        AdminOperationResult result = await service.PauseProjectionAsync("tenant1", "OrderSummary");

        result.Success.ShouldBeFalse();
        result.ErrorCode.ShouldBe("409");
    }
}
