using System.Text;

using Hexalith.EventStore.Client.Gateway;
using Hexalith.EventStore.Client.Projections.Checkpointed;
using Hexalith.EventStore.Contracts.Streams;

using NSubstitute;

using Shouldly;

namespace Hexalith.EventStore.Client.Tests.Projections.Checkpointed;

/// <summary>
/// The gateway never admits the reserved <c>system</c> tenant as a request scope, so retained platform source
/// history is read through its explicit platform scope and answered with the storage identity.
/// </summary>
public sealed class CheckpointedProjectionHistoryPlatformScopeTests {
    private static readonly CheckpointedProjectionStream _tenantsStream = new("system", "tenants", "tenant-alpha");

    [Fact]
    public async Task RetainedTenantsHistoryIsRequestedThroughThePlatformScopeAndAcceptsTheStorageIdentity() {
        List<StreamReadRequest> requests = [];
        IEventStoreGatewayClient gateway = Gateway(requests, pageTenant: "system");

        IReadOnlyList<CheckpointedProjectionEvent> events = await new CheckpointedProjectionHistory(gateway)
            .ReadRangeAsync(_tenantsStream, 1, 2, TestContext.Current.CancellationToken);

        events.Select(e => e.Sequence).ShouldBe([1, 2]);
        events.ShouldAllBe(e => e.Stream == _tenantsStream);
        StreamReadRequest request = requests.ShouldHaveSingleItem();
        request.Tenant.ShouldBe("platform:tenants");
        request.Domain.ShouldBe("tenants");
        request.AggregateId.ShouldBe("tenant-alpha");
    }

    [Fact]
    public async Task PlatformScopePageThatDoesNotCarryTheStorageIdentityIsUnavailable() {
        IEventStoreGatewayClient gateway = Gateway([], pageTenant: "platform:tenants");

        CheckpointedProjectionUnavailableException failure = await Should.ThrowAsync<CheckpointedProjectionUnavailableException>(
            () => new CheckpointedProjectionHistory(gateway).ReadRangeAsync(_tenantsStream, 1, 2, TestContext.Current.CancellationToken));

        failure.Reason.ShouldBe(CheckpointedProjectionUnavailableReason.HistoryUnavailable);
    }

    [Theory]
    [InlineData("tenant-a", "tenants")]
    [InlineData("system", "orders")]
    public async Task OtherStreamsKeepTheirOwnTenantAsTheRequestScope(string tenant, string domain) {
        List<StreamReadRequest> requests = [];
        IEventStoreGatewayClient gateway = Gateway(requests, pageTenant: tenant, pageDomain: domain);

        _ = await new CheckpointedProjectionHistory(gateway)
            .ReadRangeAsync(new CheckpointedProjectionStream(tenant, domain, "tenant-alpha"), 1, 2, TestContext.Current.CancellationToken);

        requests.ShouldHaveSingleItem().Tenant.ShouldBe(tenant);
    }

    private static IEventStoreGatewayClient Gateway(List<StreamReadRequest> requests, string pageTenant, string pageDomain = "tenants") {
        IEventStoreGatewayClient gateway = Substitute.For<IEventStoreGatewayClient>();
        _ = gateway.ReadStreamAsync(Arg.Any<StreamReadRequest>(), Arg.Any<CancellationToken>()).Returns(call => {
            StreamReadRequest request = call.Arg<StreamReadRequest>();
            requests.Add(request);
            StreamReadEvent[] events = [Event(1), Event(2)];
            return Task.FromResult(new StreamReadPage(
                pageTenant,
                pageDomain,
                request.AggregateId,
                events,
                new StreamReadMetadata(request.FromSequence, request.ToSequence, 2, 2, events.Length, false, null)));
        });
        return gateway;
    }

    private static StreamReadEvent Event(long sequence)
        => new(
            sequence,
            "Tenants.TenantCreated",
            Encoding.UTF8.GetBytes($"payload-{sequence}"),
            "json",
            1,
            $"message-{sequence}",
            $"correlation-{sequence}",
            null,
            DateTimeOffset.UnixEpoch.AddMinutes(sequence),
            null);
}
