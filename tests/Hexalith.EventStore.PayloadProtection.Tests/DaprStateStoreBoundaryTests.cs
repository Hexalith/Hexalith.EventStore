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
}
