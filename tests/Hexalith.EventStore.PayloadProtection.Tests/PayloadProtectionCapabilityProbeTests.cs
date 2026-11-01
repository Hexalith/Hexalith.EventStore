using Hexalith.EventStore.Contracts.Security;

namespace Hexalith.EventStore.PayloadProtection.Tests;

/// <summary>Qualifying a component requires every metadata and persisted primitive assertion to pass.</summary>
public sealed class PayloadProtectionCapabilityProbeTests
{
    [Theory]
    [InlineData("missing-ETAG")]
    [InlineData("missing-TRANSACTIONAL")]
    [InlineData("missing-TTL")]
    [InlineData("duplicate-component")]
    [InlineData("partial-transaction")]
    [InlineData("ignored-etag")]
    [InlineData("ignored-delete-etag")]
    [InlineData("ignored-delete")]
    [InlineData("ttl-never-expires")]
    public async Task IncompatibleComponentsFailTheProbeAndRevokeExistingQualification(string fault)
    {
        ArgumentNullException.ThrowIfNull(fault);
        var transport = new DaprCapabilityProbeClient(fault);
        using var client = transport.Client;
        var store = new DaprPayloadProtectionStateStore(client);
        store.ConfirmQualification();
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        await Should.ThrowAsync<PayloadProtectionStateException>(() => new PayloadProtectionCapabilityProbe(store).ProbeAsync(deadline.Token).AsTask());
        (await Should.ThrowAsync<PayloadProtectionStateException>(() => store.ReadAsync<WorkHeadV1>(PayloadProtectionStateKeys.WorkHead(new('a', 64))).AsTask()))
            .Reason.ShouldBe(UnreadableProtectedDataReason.ProviderUnavailable);
    }

    [Fact]
    public async Task AllPrimitiveAssertionsGrantQualificationAndLeaveItUsable()
    {
        var transport = new DaprCapabilityProbeClient("");
        using var client = transport.Client;
        var store = new DaprPayloadProtectionStateStore(client);
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        (await new PayloadProtectionCapabilityProbe(store).ProbeAsync(deadline.Token)).ShouldBe("state.postgresql:v2");
        var value = new WorkHeadV1(new('a', 64), 0, 0, 0, "2026-10-23T00:00:00.0000000Z");
        (await store.CommitAsync([PayloadProtectionStateMutation.Put(value)], deadline.Token)).ShouldBe(PayloadStateCommitResult.Committed);
        (await store.ReadAsync<WorkHeadV1>(PayloadProtectionStateCodec.KeyFor(value), deadline.Token))!.Value.ShouldBe(value);
    }
}
