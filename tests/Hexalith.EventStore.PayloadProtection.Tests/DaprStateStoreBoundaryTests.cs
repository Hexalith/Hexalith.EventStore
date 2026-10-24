using System.Text;
using Dapr.Client;
using Hexalith.EventStore.Contracts.Security;
using NSubstitute;

namespace Hexalith.EventStore.PayloadProtection.Tests;

/// <summary>Checks local rejection at the state primitive; real component semantics are tested by the live project.</summary>
public sealed class DaprStateStoreBoundaryTests
{
    [Fact]
    public async Task UnqualifiedStoreAndRawMalformedMutationFailBeforeStateCalls()
    {
        DaprClient client = Substitute.For<DaprClient>();
        var store = new DaprPayloadProtectionStateStore(client);
        string root = new('a', 64);
        string key = PayloadProtectionStateKeys.WorkHead(root);
        (await Should.ThrowAsync<PayloadProtectionStateException>(async () => await store.ReadAsync<WorkHeadV1>(key))).Reason
            .ShouldBe(UnreadableProtectedDataReason.ProviderUnavailable);
        store.ConfirmQualification();
        await Should.ThrowAsync<PayloadProtectionFormatException>(async () => await store.CommitAsync([new(key, "{\"injected\":true}"u8.ToArray(), null)]));
        await client.DidNotReceiveWithAnyArgs().ExecuteStateTransactionAsync(default!, default!, default!, default);
    }

    [Fact]
    public async Task PersistentCatalogCannotAcquireTtlAndDeleteCannotUseArbitraryKey()
    {
        DaprClient client = Substitute.For<DaprClient>();
        var store = new DaprPayloadProtectionStateStore(client);
        store.ConfirmQualification();
        var head = new WorkHeadV1(new('a', 64), 0, 0, 0, "2026-10-23T00:00:00.0000000Z");
        PayloadProtectionStateMutation put = PayloadProtectionStateMutation.Put(head);
        await Should.ThrowAsync<PayloadProtectionFormatException>(async () => await store.CommitAsync([put with { TtlSeconds = 60 }]));
        await Should.ThrowAsync<PayloadProtectionFormatException>(async () => await store.CommitAsync([new("eventstore:pp:v2:key:arbitrary", null, "etag")]));
        await client.DidNotReceiveWithAnyArgs().ExecuteStateTransactionAsync(default!, default!, default!, default);
    }

    [Fact]
    public async Task WrongRootReadAndMalformedRecordReturnOnlyTypedFailure()
    {
        DaprClient client = Substitute.For<DaprClient>();
        var store = new DaprPayloadProtectionStateStore(client);
        store.ConfirmQualification();
        string key = PayloadProtectionStateKeys.WorkHead(new('a', 64));
        ReadOnlyMemory<byte> bytes = PayloadProtectionStateCodec.Encode(new WorkHeadV1(new('b', 64), 0, 0, 0, "2026-10-23T00:00:00.0000000Z"));
        client.GetByteStateAndETagAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<ConsistencyMode?>(), Arg.Any<IReadOnlyDictionary<string, string>?>(), Arg.Any<CancellationToken>())
            .Returns((bytes, "etag"));
        (await Should.ThrowAsync<PayloadProtectionStateException>(async () => await store.ReadAsync<WorkHeadV1>(key))).Reason
            .ShouldBe(UnreadableProtectedDataReason.ConsistencyMismatch);
        client.GetByteStateAndETagAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<ConsistencyMode?>(), Arg.Any<IReadOnlyDictionary<string, string>?>(), Arg.Any<CancellationToken>())
            .Returns((new ReadOnlyMemory<byte>(Encoding.UTF8.GetBytes("private-canary")), "etag"));
        PayloadProtectionStateException failure = await Should.ThrowAsync<PayloadProtectionStateException>(async () => await store.ReadAsync<WorkHeadV1>(key));
        failure.ToString().ShouldNotContain("private-canary"); failure.InnerException.ShouldBeNull();
    }

    [Fact]
    public void FailedBackendConstructionAndRejectedResultsClearOwnedOutput()
    {
        byte[] invalid = Enumerable.Repeat((byte)0x55, 513).ToArray();
        Should.Throw<PayloadProtectionFormatException>(() => new PayloadBackendResult(PayloadBackendFailure.None, invalid));
        invalid.ShouldAllBe(b => b == 0);
        byte[] partial = Enumerable.Repeat((byte)0x66, 32).ToArray();
        using var denied = new PayloadBackendResult(PayloadBackendFailure.Denied, partial);
        partial.ShouldAllBe(b => b == 0);
        Should.Throw<PayloadProtectionFormatException>(() => denied.TakeBytes());
    }

    [Fact]
    public async Task CumulativeRequestLimitsAndImmutableAcknowledgementsFailBeforeTransport()
    {
        var client = Substitute.For<DaprClient>(); var store = new DaprPayloadProtectionStateStore(client); store.ConfirmQualification();
        string now = PayloadProtectionStateCodec.Timestamp(DateTimeOffset.UtcNow);
        var many = Enumerable.Range(0, PayloadProtectionOperationalLimits.TransactionOperations + 1)
            .Select(i => PayloadProtectionStateMutation.Put(new WorkHeadV1(i.ToString("x64"), 0, 0, 0, now))).ToArray();
        await Should.ThrowAsync<PayloadProtectionFormatException>(() => store.CommitAsync(many).AsTask());
        var entropy = new CryptographicPayloadProtectionEntropy(); string scope = PayloadProtectionStateKeys.ScopeDigest(new("tenant", "domain", "aggregate"));
        var large = Enumerable.Range(0, 35).Select(_ => PayloadProtectionStateMutation.Put(PayloadProtectionStateCodec.Seal(new WrappedDekV1("tenant", "domain", "aggregate", scope,
            entropy.CreateKeyReference(), 1, "Reserved", "fixture", new('p', 60000), "RSA-OAEP-256", "AA", 1, 1, 1, entropy.CreateKeyReference(), now, now, new('0', 64), new('0', 64))))).ToArray();
        await Should.ThrowAsync<PayloadProtectionFormatException>(() => store.CommitAsync(large).AsTask());
        var ack = new CacheClearAckV1(scope, entropy.CreateKeyReference(), 1, entropy.CreateKeyReference(), now);
        await Should.ThrowAsync<PayloadProtectionFormatException>(() => store.CommitAsync([PayloadProtectionStateMutation.Put(ack, "etag")]).AsTask());
        await client.DidNotReceiveWithAnyArgs().ExecuteStateTransactionAsync(default!, default!, default!, default);
    }

    [Fact]
    public async Task NestedReadsShareTheCumulativeCallBudgetAndDoNotIssueOneMoreRequest()
    {
        var client = Substitute.For<DaprClient>(); var store = new DaprPayloadProtectionStateStore(client); store.ConfirmQualification();
        var value = new WorkHeadV1(new('a', 64), 0, 0, 0, PayloadProtectionStateCodec.Timestamp(DateTimeOffset.UtcNow));
        ReadOnlyMemory<byte> bytes = PayloadProtectionStateCodec.Encode(value); int calls = 0;
        client.GetByteStateAndETagAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<ConsistencyMode?>(), Arg.Any<IReadOnlyDictionary<string, string>?>(), Arg.Any<CancellationToken>())
            .Returns(_ => { calls++; return (bytes, "etag"); });
        using var phase = new PayloadOperationBudget(TimeProvider.System, default);
        for (int i = 0; i < PayloadProtectionOperationalLimits.ReadOperations; i++) { await store.ReadAsync<WorkHeadV1>(PayloadProtectionStateCodec.KeyFor(value)); }
        (await Should.ThrowAsync<PayloadProtectionStateException>(() => store.ReadAsync<WorkHeadV1>(PayloadProtectionStateCodec.KeyFor(value)).AsTask())).Reason.ShouldBe(UnreadableProtectedDataReason.ProviderUnavailable);
        calls.ShouldBe(PayloadProtectionOperationalLimits.ReadOperations);
    }

    [Fact]
    public async Task OversizedMalformedResponsesStillChargeTheEnclosingByteBudget()
    {
        var client = Substitute.For<DaprClient>(); var store = new DaprPayloadProtectionStateStore(client); store.ConfirmQualification();
        ReadOnlyMemory<byte> oversized = new byte[4 * 1024 * 1024]; int calls = 0;
        client.GetByteStateAndETagAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<ConsistencyMode?>(), Arg.Any<IReadOnlyDictionary<string, string>?>(), Arg.Any<CancellationToken>())
            .Returns(_ => { calls++; return (oversized, "etag"); });
        using var phase = new PayloadOperationBudget(TimeProvider.System, default);
        string key = PayloadProtectionStateKeys.WorkHead(new('a', 64));
        for (int i = 0; i < 4; i++)
        { (await Should.ThrowAsync<PayloadProtectionStateException>(() => store.ReadAsync<WorkHeadV1>(key).AsTask())).Reason.ShouldBe(UnreadableProtectedDataReason.ConsistencyMismatch); }
        (await Should.ThrowAsync<PayloadProtectionStateException>(() => store.ReadAsync<WorkHeadV1>(key).AsTask())).Reason.ShouldBe(UnreadableProtectedDataReason.ProviderUnavailable);
        calls.ShouldBe(4);
    }
}
