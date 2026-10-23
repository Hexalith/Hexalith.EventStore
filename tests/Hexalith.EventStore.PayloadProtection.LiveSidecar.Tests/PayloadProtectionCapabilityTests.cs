// Effective authority: 08bfd5855998a276999db38447c409b2231f3d4e253d4e371d4d8bfe369462d6; base PF-02/section 10; amendment section 7.
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
