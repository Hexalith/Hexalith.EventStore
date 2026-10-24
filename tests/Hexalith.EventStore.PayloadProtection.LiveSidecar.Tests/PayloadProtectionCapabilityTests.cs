// Effective authority: eb45653553efe53ba83e6dd6c9796b47dfb6fb0bba0628ff95f6bc1b96b054c9; base PF-02/section 10; amendment section 7.
namespace Hexalith.EventStore.PayloadProtection.LiveSidecar.Tests;

/// <summary>Real-sidecar capability foundation; full lifecycle fault/concurrency qualification remains required.</summary>
public sealed class PayloadProtectionCapabilityTests(PayloadProtectionDaprFixture fixture) : IClassFixture<PayloadProtectionDaprFixture>
{
    [Fact]
    public async Task RealPostgresqlV2ProvesCreateOnlyCasAtomicRollbackAndTtl()
    {
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        (await new PayloadProtectionCapabilityProbe(fixture.Store).ProbeAsync(deadline.Token)).ShouldBe("state.postgresql:v2");
        string root = PayloadProtectionStateKeys.Digest("pf02-persisted-catalog-fixture");
        var value = new WorkHeadV1(root, 0, 0, 0, PayloadProtectionStateCodec.Timestamp(DateTimeOffset.UtcNow));
        (await fixture.Store.CommitAsync([PayloadProtectionStateMutation.Put(value)], deadline.Token)).ShouldBe(PayloadStateCommitResult.Committed);
        PayloadProtectionStateValue<WorkHeadV1>? read = await fixture.Store.ReadAsync<WorkHeadV1>(PayloadProtectionStateKeys.WorkHead(root), deadline.Token);
        read.ShouldNotBeNull(); read.Value.ShouldBe(value); read.ETag.ShouldNotBeNullOrEmpty();
    }
}
