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

public class DaprStorageCommandServiceTests
{
    private const string EventStoreAppId = "eventstore";

    private static DaprStorageCommandService CreateService(
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

        return new DaprStorageCommandService(
            daprClient,
            options,
            authContext,
            NullLogger<DaprStorageCommandService>.Instance);
    }

    // === TriggerCompactionAsync ===

    [Fact]
    public async Task TriggerCompactionAsync_ReturnsSuccess_WhenEventStoreResponds()
    {
        DaprClient daprClient = Substitute.For<DaprClient>();
        var expected = new AdminOperationResult(true, "op-1", "Compaction started", null);

        daprClient.InvokeMethodAsync<AdminOperationResult>(
            Arg.Any<HttpRequestMessage>(),
            Arg.Any<CancellationToken>())
            .Returns(_ => expected);

        DaprStorageCommandService service = CreateService(daprClient);

        AdminOperationResult result = await service.TriggerCompactionAsync("tenant-a", "Counter");

        result.Success.ShouldBeTrue();
        result.OperationId.ShouldBe("op-1");
    }

    [Fact]
    public async Task TriggerCompactionAsync_ForwardsJwtToken()
    {
        DaprClient daprClient = Substitute.For<DaprClient>();
        HttpRequestMessage? capturedRequest = null;
        IAdminAuthContext authContext = Substitute.For<IAdminAuthContext>();
        authContext.GetToken().Returns("storage-token");

        daprClient.InvokeMethodAsync<AdminOperationResult>(
            Arg.Do<HttpRequestMessage>(r => capturedRequest = r),
            Arg.Any<CancellationToken>())
            .Returns(_ => new AdminOperationResult(true, "op-1", null, null));

        DaprStorageCommandService service = CreateService(daprClient, authContext);

        await service.TriggerCompactionAsync("tenant-a", null);

        capturedRequest.ShouldNotBeNull();
        capturedRequest!.Headers.Authorization!.Parameter.ShouldBe("storage-token");
    }

    [Fact]
    public async Task TriggerCompactionAsync_ReturnsError_WhenServiceUnavailable()
    {
        DaprClient daprClient = Substitute.For<DaprClient>();
        daprClient.InvokeMethodAsync<AdminOperationResult>(
            Arg.Any<HttpRequestMessage>(),
            Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("EventStore down"));

        DaprStorageCommandService service = CreateService(daprClient);

        AdminOperationResult result = await service.TriggerCompactionAsync("tenant-a", null);

        result.Success.ShouldBeFalse();
        result.ErrorCode.ShouldNotBeNull();
    }

    [Fact]
    public async Task TriggerCompactionAsync_ReturnsNullResponseError()
    {
        DaprClient daprClient = Substitute.For<DaprClient>();
        daprClient.InvokeMethodAsync<AdminOperationResult>(
            Arg.Any<HttpRequestMessage>(),
            Arg.Any<CancellationToken>())
            .Returns(_ => (AdminOperationResult?)null);

        DaprStorageCommandService service = CreateService(daprClient);

        AdminOperationResult result = await service.TriggerCompactionAsync("tenant-a", null);

        result.Success.ShouldBeFalse();
        result.ErrorCode.ShouldBe("NULL_RESPONSE");
    }

    [Fact]
    public async Task TriggerCompactionAsync_PropagatesCancellation()
    {
        using CancellationTokenSource cts = new();
        await cts.CancelAsync();

        DaprClient daprClient = Substitute.For<DaprClient>();
        daprClient.InvokeMethodAsync<AdminOperationResult>(
            Arg.Any<HttpRequestMessage>(),
            Arg.Any<CancellationToken>())
            .Returns<AdminOperationResult?>(_ => throw new OperationCanceledException());

        DaprStorageCommandService service = CreateService(daprClient);

        await Should.ThrowAsync<OperationCanceledException>(
            () => service.TriggerCompactionAsync("tenant-a", null, cts.Token));
    }

    // === CreateSnapshotAsync ===

    [Fact]
    public async Task CreateSnapshotAsync_ReturnsSuccess_WhenEventStoreResponds()
    {
        DaprClient daprClient = Substitute.For<DaprClient>();
        var expected = new AdminOperationResult(true, "op-2", "Snapshot created", null);

        daprClient.InvokeMethodAsync<AdminOperationResult>(
            Arg.Any<HttpRequestMessage>(),
            Arg.Any<CancellationToken>())
            .Returns(_ => expected);

        DaprStorageCommandService service = CreateService(daprClient);

        AdminOperationResult result = await service.CreateSnapshotAsync("tenant-a", "Counter", "counter-1");

        result.Success.ShouldBeTrue();
    }

    [Fact]
    public async Task CreateSnapshotAsync_ReturnsError_WhenServiceUnavailable()
    {
        DaprClient daprClient = Substitute.For<DaprClient>();
        daprClient.InvokeMethodAsync<AdminOperationResult>(
            Arg.Any<HttpRequestMessage>(),
            Arg.Any<CancellationToken>())
            .ThrowsAsync(new HttpRequestException("Connection refused"));

        DaprStorageCommandService service = CreateService(daprClient);

        AdminOperationResult result = await service.CreateSnapshotAsync("tenant-a", "Counter", "counter-1");

        result.Success.ShouldBeFalse();
    }

    // === SetSnapshotPolicyAsync ===

    [Fact]
    public async Task SetSnapshotPolicyAsync_ReturnsSuccess_WhenEventStoreResponds()
    {
        DaprClient daprClient = Substitute.For<DaprClient>();
        var expected = new AdminOperationResult(true, "op-3", "Policy set", null);

        daprClient.InvokeMethodAsync<AdminOperationResult>(
            Arg.Any<HttpRequestMessage>(),
            Arg.Any<CancellationToken>())
            .Returns(_ => expected);

        DaprStorageCommandService service = CreateService(daprClient);

        AdminOperationResult result = await service.SetSnapshotPolicyAsync("tenant-a", "Counter", "CounterAggregate", 100);

        result.Success.ShouldBeTrue();
    }

    [Fact]
    public async Task SetSnapshotPolicyAsync_ReturnsError_WhenServiceUnavailable()
    {
        DaprClient daprClient = Substitute.For<DaprClient>();
        daprClient.InvokeMethodAsync<AdminOperationResult>(
            Arg.Any<HttpRequestMessage>(),
            Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("EventStore down"));

        DaprStorageCommandService service = CreateService(daprClient);

        AdminOperationResult result = await service.SetSnapshotPolicyAsync("tenant-a", "Counter", "CounterAggregate", 100);

        result.Success.ShouldBeFalse();
    }

    // === DeleteSnapshotPolicyAsync ===

    [Fact]
    public async Task DeleteSnapshotPolicyAsync_ReturnsSuccess_WhenEventStoreResponds()
    {
        DaprClient daprClient = Substitute.For<DaprClient>();
        var expected = new AdminOperationResult(true, "op-4", "Policy deleted", null);

        daprClient.InvokeMethodAsync<AdminOperationResult>(
            Arg.Any<HttpRequestMessage>(),
            Arg.Any<CancellationToken>())
            .Returns(_ => expected);

        DaprStorageCommandService service = CreateService(daprClient);

        AdminOperationResult result = await service.DeleteSnapshotPolicyAsync("tenant-a", "Counter", "CounterAggregate");

        result.Success.ShouldBeTrue();
    }

    [Fact]
    public async Task DeleteSnapshotPolicyAsync_ReturnsError_WhenServiceUnavailable()
    {
        DaprClient daprClient = Substitute.For<DaprClient>();
        daprClient.InvokeMethodAsync<AdminOperationResult>(
            Arg.Any<HttpRequestMessage>(),
            Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("EventStore down"));

        DaprStorageCommandService service = CreateService(daprClient);

        AdminOperationResult result = await service.DeleteSnapshotPolicyAsync("tenant-a", "Counter", "CounterAggregate");

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

        DaprStorageCommandService service = CreateService(daprClient);

        AdminOperationResult result = await service.TriggerCompactionAsync("tenant-a", null);

        result.Success.ShouldBeFalse();
        result.ErrorCode.ShouldBe("404");
    }
}
