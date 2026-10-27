using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

using Hexalith.EventStore.Client.Tests.Projections;
using Hexalith.EventStore.DomainService.Content;

using Microsoft.Extensions.Options;

using Shouldly;

namespace Hexalith.EventStore.DomainService.Tests.Content;

public sealed class AuthoritativeContentStoreTests
{
    private static readonly ContentScope _scope = new("tenant-a", "folders", "folder-a", "workspace-a");
    private static readonly ClaimsPrincipal _principal = new(new ClaimsIdentity([new Claim("tenant", "tenant-a")], "test"));
    private static readonly byte[] _bytes = [0, 255, 1, 2, 128, 10];

    [Theory]
    [InlineData(false, "read-only")]
    [InlineData(true, "read-only")]
    [InlineData(false, "task")]
    [InlineData(true, "task")]
    [InlineData(false, "future-fence")]
    [InlineData(true, "future-fence")]
    public async Task TaskSealRequiresTaskAndFenceBoundMutationAuthority(bool completion, string denial)
    {
        using var dapr = new RecordingDaprClient { PersistTypedState = true };
        var authority = new ContentTestAuthority { ReadOnly = denial == "read-only", SealTask = "task-a", SealFence = 1 };
        var store = Store(dapr, authority);
        string task = denial == "task" ? "task-other" : "task-a";
        long fence = denial == "future-fence" ? 2 : 1;
        int writes = dapr.TrySaveByteOperations.Count;
        await Should.ThrowAsync<UnauthorizedAccessException>(() => completion
            ? store.TrySealTaskForCompletionAsync(_principal, _scope, task, fence)
            : store.TryQuiesceTaskLeaseAsync(_principal, _scope, task, fence));
        dapr.TrySaveByteOperations.Count.ShouldBe(writes);
    }

    [Fact]
    public async Task CleanLeaseReleaseAllowsOnlyAHigherFenceAndCompletionRemainsTerminal()
    {
        using var dapr = new RecordingDaprClient { PersistTypedState = true };
        var authority = new ContentTestAuthority(); var store = Store(dapr, authority);
        (await store.TryQuiesceTaskLeaseAsync(_principal, _scope, "task-a", 1)).ShouldBeTrue();
        await Should.ThrowAsync<ContentConflictException>(() => store.UploadAsync(_principal,
            UploadRequest(_bytes) with { LeaseFence = 1 }, new MemoryStream(_bytes)));
        ContentReceipt receipt = await store.UploadAsync(_principal, UploadRequest(_bytes) with { LeaseFence = 2 }, new MemoryStream(_bytes));
        (await store.TryQuiesceTaskLeaseAsync(_principal, _scope, "task-a", 2)).ShouldBeFalse();
        await store.PrepareAsync(_principal, Publication("new-lease", receipt) with { LockFence = 2 });
        authority.Outcome = ContentPublicationOutcome.Accepted;
        (await store.TrySealTaskForCompletionAsync(_principal, _scope, "task-a", 2)).ShouldBeTrue();
        await Should.ThrowAsync<ContentConflictException>(() => store.UploadAsync(_principal,
            UploadRequest(_bytes) with { LeaseFence = 3 }, new MemoryStream(_bytes)));
    }

    [Fact]
    public async Task CompletionSealRejectsAPreparationAlreadyInFlight()
    {
        using var dapr = new RecordingDaprClient { PersistTypedState = true };
        var authority = new ContentTestAuthority(); var store = Store(dapr, authority);
        var receipt = await UploadAsync(store); await store.PrepareAsync(_principal, Publication("accepted", receipt));
        authority.Outcome = ContentPublicationOutcome.Accepted; await store.RecoverAsync(_principal, _scope, "accepted");
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var resume = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        authority.BeforePreparation = async () => { entered.SetResult(); await resume.Task; };
        Task racing = store.PrepareAsync(_principal, Publication("racing", receipt) with { Changes = [Change(receipt) with { Version = 2 }] });
        await entered.Task;
        (await store.TrySealTaskForCompletionAsync(_principal, _scope, "task-a", 1)).ShouldBeTrue();
        resume.SetResult(); await Should.ThrowAsync<ContentConflictException>(() => racing);
    }

    [Fact]
    public async Task CompletionSealRejectsPendingStagedAndRacingAdmissionsButPreservesPublishedBytes()
    {
        using var dapr = new RecordingDaprClient { PersistTypedState = true };
        var authority = new ContentTestAuthority();
        var store = Store(dapr, authority);
        ContentReceipt receipt = await UploadAsync(store);
        (await store.TrySealTaskForCompletionAsync(_principal, _scope, "task-a", 1)).ShouldBeFalse();
        await store.PrepareAsync(_principal, Publication("native", receipt));
        (await store.TrySealTaskForCompletionAsync(_principal, _scope, "task-a", 1)).ShouldBeFalse();
        authority.Outcome = ContentPublicationOutcome.Accepted;
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var resume = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        authority.BeforeUpload = async () => { entered.SetResult(); await resume.Task; };
        Task<ContentReceipt> racing = UploadAsync(store, [9, 8, 7]);
        await entered.Task;
        (await store.TrySealTaskForCompletionAsync(_principal, _scope, "task-a", 1)).ShouldBeTrue();
        (await Store(dapr, authority).TrySealTaskForCompletionAsync(_principal, _scope, "task-a", 1)).ShouldBeTrue();
        authority.BeforeUpload = null; resume.SetResult();
        await Should.ThrowAsync<ContentConflictException>(() => racing);
        await Should.ThrowAsync<ContentConflictException>(() => UploadAsync(store, [9, 8, 7]));
        await Should.ThrowAsync<ContentConflictException>(() => store.PrepareAsync(_principal,
            Publication("late", receipt) with { Changes = [Change(receipt) with { Version = 2 }] }));
        (await store.ReadAsync(_principal, Read())).Bytes.ShouldBe(_bytes);
    }

    [Fact]
    public async Task ActualBytesAreVerifiedAndRecoveredAtomicallyAfterStoreReplacement()
    {
        using var dapr = new RecordingDaprClient { PersistTypedState = true };
        var authority = new ContentTestAuthority();
        var store = Store(dapr, authority);
        ContentReceipt first = await UploadAsync(store);
        ContentReceipt second = await UploadAsync(store, [9, 8, 7]);
        ContentPublicationRequest request = Publication("command-a", first) with
        {
            Changes = [Change(first), new("file-b", "incarnation-b", 1, second.Id, new Dictionary<string, string> { ["path"] = "src/b.bin" }, second)],
        };
        await store.PrepareAsync(_principal, request);
        (await store.ListAsync(_principal, _scope)).ShouldBeEmpty();
        await Should.ThrowAsync<ContentUnavailableException>(() => store.ReadAsync(_principal, Read()));
        authority.Outcome = ContentPublicationOutcome.Accepted;
        var replacement = Store(dapr, authority);
        (await replacement.ReadAsync(_principal, Read())).Bytes.ShouldBe(_bytes);
        (await replacement.ListAsync(_principal, _scope)).Count.ShouldBe(2);
        (await replacement.VerifyUploadAsync(_principal, new(_scope, "task-a", first.Id))).ShouldBe(first);
        // Only the dedicated physical body namespace may contain encoded bytes.
        var bodyWrites = dapr.TrySaveByteOperations.Where(static write => write.Key.StartsWith("authoritative-content:body:", StringComparison.Ordinal)).ToArray();
        bodyWrites.Length.ShouldBe(2);
        foreach (var metadata in dapr.TrySaveByteOperations.Where(static write => write.Key.StartsWith("authoritative-content:metadata:", StringComparison.Ordinal)))
            Encoding.UTF8.GetString(metadata.Value).ShouldNotContain(Convert.ToBase64String(_bytes));
        dapr.TrySaveByteOperations.All(static write => write.Concurrency == Dapr.Client.ConcurrencyMode.FirstWrite).ShouldBeTrue();
        dapr.TrySaveByteMetadata.Values.All(static metadata => metadata is null || !metadata.ContainsKey("ttlInSeconds")).ShouldBeTrue();
    }

    [Fact]
    public async Task HashLengthScopeTaskAndConfidentialityCannotBeAssertedByTheCaller()
    {
        using var dapr = new RecordingDaprClient { PersistTypedState = true };
        var authority = new ContentTestAuthority();
        var store = Store(dapr, authority);
        ContentUploadRequest invalid = UploadRequest(_bytes) with { ExpectedSha256 = new string('0', 64) };
        await Should.ThrowAsync<ArgumentException>(() => store.UploadAsync(_principal, invalid, new MemoryStream(_bytes)));
        await Should.ThrowAsync<ArgumentException>(() => store.UploadAsync(_principal, UploadRequest(_bytes) with { ExpectedLength = 1 }, new MemoryStream(_bytes)));
        dapr.TrySaveByteOperations.ShouldBeEmpty();
        authority.RejectBytes = true;
        await Should.ThrowAsync<UnauthorizedAccessException>(() => UploadAsync(store));
        dapr.TrySaveByteOperations.ShouldBeEmpty();
        authority.RejectBytes = false;
        ContentReceipt receipt = await UploadAsync(store);
        await Should.ThrowAsync<ContentUnavailableException>(() => store.VerifyUploadAsync(_principal, new(_scope, "other-task", receipt.Id)));
        await Should.ThrowAsync<UnauthorizedAccessException>(() => store.VerifyUploadAsync(_principal, new(_scope with { TenantId = "tenant-b" }, "task-a", receipt.Id)));
        ContentPublicationRequest forged = Publication("forged", receipt) with { Changes = [Change(receipt) with { ExpectedReceipt = receipt with { ByteLength = 999 } }] };
        await Should.ThrowAsync<ContentConflictException>(() => store.PrepareAsync(_principal, forged));
        authority.Deny = true;
        int writes = dapr.TrySaveByteOperations.Count;
        await Should.ThrowAsync<UnauthorizedAccessException>(() => store.ReadAsync(_principal, Read()));
        dapr.TrySaveByteOperations.Count.ShouldBe(writes);
    }

    [Fact]
    public async Task EquivalentReplayConflictsFencesRejectionAndTombstonesPreserveVisibility()
    {
        using var dapr = new RecordingDaprClient { PersistTypedState = true };
        var authority = new ContentTestAuthority();
        var store = Store(dapr, authority);
        ContentReceipt receipt = await UploadAsync(store);
        ContentPublicationRequest initial = Publication("add", receipt);
        authority.RejectPreparation = true;
        await Should.ThrowAsync<ContentConflictException>(() => store.PrepareAsync(_principal, initial));
        authority.RejectPreparation = false;
        await store.PrepareAsync(_principal, initial);
        await store.PrepareAsync(_principal, initial);
        await Should.ThrowAsync<ContentConflictException>(() => store.PrepareAsync(_principal, initial with { LockFence = 2 }));
        await Should.ThrowAsync<ContentConflictException>(() => Store(dapr, authority).PrepareAsync(_principal, Publication("racing-command", receipt)));
        authority.Outcomes["add"] = ContentPublicationOutcome.Rejected;
        (await store.RecoverAsync(_principal, _scope, "add")).ShouldBe(ContentPublicationOutcome.Rejected);
        (await store.ListAsync(_principal, _scope)).ShouldBeEmpty();
        await store.PrepareAsync(_principal, Publication("accepted", receipt));
        authority.Outcomes["accepted"] = ContentPublicationOutcome.Accepted;
        await store.RecoverAsync(_principal, _scope, "accepted");
        ContentPublicationRequest deletion = Publication("delete", receipt) with { ExpectedVersion = 1, Changes = [new("file-a", "incarnation-a", 2, null)] };
        await store.PrepareAsync(_principal, deletion);
        authority.Outcomes["delete"] = ContentPublicationOutcome.Accepted;
        await store.RecoverAsync(_principal, _scope, "delete");
        await store.PrepareAsync(_principal, initial);
        (await store.RecoverAsync(_principal, _scope, "accepted")).ShouldBe(ContentPublicationOutcome.Accepted);
        await Should.ThrowAsync<ContentUnavailableException>(() => store.ReadAsync(_principal, Read()));
        await Should.ThrowAsync<ContentConflictException>(() => store.PrepareAsync(_principal, Publication("resurrect", receipt) with { Changes = [Change(receipt) with { Version = 3 }] }));
        (await store.ListAsync(_principal, _scope)).ShouldBeEmpty();
    }

    [Fact]
    public async Task MissingOrCorruptBodiesAndCancelledOperationsNeverReturnEmptySuccess()
    {
        using var dapr = new RecordingDaprClient { PersistTypedState = true };
        var authority = new ContentTestAuthority { Outcome = ContentPublicationOutcome.Accepted };
        var store = Store(dapr, authority);
        ContentReceipt receipt = await UploadAsync(store);
        await store.PrepareAsync(_principal, Publication("accepted", receipt));
        await store.RecoverAsync(_principal, _scope, "accepted");
        string bodyKey = dapr.TrySaveByteOperations.Single(static write => write.Key.StartsWith("authoritative-content:body:", StringComparison.Ordinal)).Key;
        dapr.SeedByteStore(bodyKey, JsonSerializer.SerializeToUtf8Bytes(new ContentStoredBody(receipt, [1]), dapr.JsonSerializerOptions));
        await Should.ThrowAsync<ContentUnavailableException>(() => store.ReadAsync(_principal, Read()));
        using var cancelled = new CancellationTokenSource();
        cancelled.Cancel();
        await Should.ThrowAsync<OperationCanceledException>(() => store.ReadAsync(_principal, Read(), cancelled.Token));
        await dapr.DeleteStateAsync("statestore", bodyKey, null!, null!, CancellationToken.None);
        await Should.ThrowAsync<ContentUnavailableException>(() => store.ReadAsync(_principal, Read()));
    }

    [Fact]
    public async Task RetentionRequiresFreshP7DEpochHoldReleaseAndSealedGeneration()
    {
        using var dapr = new RecordingDaprClient { PersistTypedState = true };
        var authority = new ContentTestAuthority();
        var clock = new ContentTestTime();
        var store = Store(dapr, authority, clock);
        ContentReceipt receipt = await UploadAsync(store);
        (await store.ApplyDispositionAsync(_principal, _scope, receipt.Id)).ShouldBeFalse();
        authority.Disposition = new(2, true, false, false, false);
        (await store.ApplyDispositionAsync(_principal, _scope, receipt.Id)).ShouldBeFalse();
        clock.Now = clock.Now.AddDays(6).AddHours(23);
        (await store.ApplyDispositionAsync(_principal, _scope, receipt.Id)).ShouldBeFalse();
        authority.Disposition = new(3, true, false, false, true);
        clock.Now = clock.Now.AddDays(10);
        (await store.ApplyDispositionAsync(_principal, _scope, receipt.Id)).ShouldBeFalse();
        authority.Disposition = new(4, true, false, false, false);
        (await store.ApplyDispositionAsync(_principal, _scope, receipt.Id)).ShouldBeFalse();
        clock.Now = clock.Now.AddDays(7);
        authority.SealErasure = false;
        (await store.ApplyDispositionAsync(_principal, _scope, receipt.Id)).ShouldBeFalse();
        dapr.TryDeleteByteOperations.ShouldBeEmpty();
        authority.SealErasure = true;
        (await store.ApplyDispositionAsync(_principal, _scope, receipt.Id)).ShouldBeFalse();
        clock.Now = clock.Now.AddDays(7);
        (await store.ApplyDispositionAsync(_principal, _scope, receipt.Id)).ShouldBeTrue();
        dapr.TrySaveByteOperations.Count(write => write.Key.StartsWith("authoritative-content:body:", StringComparison.Ordinal)
            && JsonSerializer.Deserialize<ContentStoredBody>(write.Value, dapr.JsonSerializerOptions)!.Erased).ShouldBe(1);
        (await Store(dapr, authority, clock).ApplyDispositionAsync(_principal, _scope, receipt.Id)).ShouldBeTrue();
        await Should.ThrowAsync<ContentUnavailableException>(() => store.VerifyUploadAsync(_principal, new(_scope, "task-a", receipt.Id)));
    }

    [Fact]
    public async Task PendingPublicationAndUnobservedResumeCycleProtectBodies()
    {
        using var dapr = new RecordingDaprClient { PersistTypedState = true };
        var authority = new ContentTestAuthority { Disposition = new(1, true, false, false, false) };
        var clock = new ContentTestTime();
        var store = Store(dapr, authority, clock);
        ContentReceipt receipt = await UploadAsync(store);
        await store.ApplyDispositionAsync(_principal, _scope, receipt.Id);
        clock.Now = clock.Now.AddDays(8);
        authority.Disposition = authority.Disposition with { Generation = 3 };
        (await store.ApplyDispositionAsync(_principal, _scope, receipt.Id)).ShouldBeFalse();
        await store.PrepareAsync(_principal, Publication("pending", receipt));
        clock.Now = clock.Now.AddDays(10);
        (await store.ApplyDispositionAsync(_principal, _scope, receipt.Id)).ShouldBeFalse();
        dapr.TryDeleteByteOperations.ShouldBeEmpty();
    }

    [Fact]
    public async Task StableUploadIntentRepairsInterruptedBytesAndRejectsConflictingReplay()
    {
        using var dapr = new RecordingDaprClient { PersistTypedState = true };
        var authority = new ContentTestAuthority();
        var store = Store(dapr, authority);
        ContentUploadRequest request = UploadRequest(_bytes) with { UploadId = "stable-operation" };
        bool interrupted = false;
        dapr.AfterTrySaveByteState = (key, _) =>
        {
            if (!interrupted && key.StartsWith("authoritative-content:body:", StringComparison.Ordinal))
            {
                interrupted = true;
                throw new OperationCanceledException();
            }
        };
        await Should.ThrowAsync<OperationCanceledException>(() => store.UploadAsync(_principal, request, new MemoryStream(_bytes)));
        var reserved = (await store.ListUploadsAsync(_principal, _scope)).Single();
        reserved.Complete.ShouldBeFalse();
        ContentReceipt repaired = await Store(dapr, authority).UploadAsync(_principal, request, new MemoryStream(_bytes));
        repaired.ShouldBe(reserved.Receipt);
        (await store.UploadAsync(_principal, request, new MemoryStream(_bytes))).ShouldBe(repaired);
        byte[] different = [1, 2, 3];
        await Should.ThrowAsync<ContentConflictException>(() => store.UploadAsync(_principal, UploadRequest(different) with { UploadId = request.UploadId }, new MemoryStream(different)));
        (await store.ListUploadsAsync(_principal, _scope)).Count.ShouldBe(1);
        dapr.TrySaveByteOperations.Count(static write => write.Key.StartsWith("authoritative-content:body:", StringComparison.Ordinal)).ShouldBe(1);
    }

    [Fact]
    public async Task ContentOwnedHoldAndDurableAbandonmentRequireFreshEpoch()
    {
        using var dapr = new RecordingDaprClient { PersistTypedState = true };
        var authority = new ContentTestAuthority { Disposition = new(1, true, false, false, false) };
        var clock = new ContentTestTime();
        var store = Store(dapr, authority, clock);
        ContentReceipt receipt = await store.UploadAsync(_principal, UploadRequest(_bytes) with { TaskId = null }, new MemoryStream(_bytes));
        (await store.ApplyDispositionAsync(_principal, _scope, receipt.Id)).ShouldBeFalse();
        (await store.ListUploadsAsync(_principal, _scope)).Single().CleanupEpoch.ShouldBeNull();
        await store.AbandonUploadAsync(_principal, _scope, receipt.Id);
        await store.ApplyDispositionAsync(_principal, _scope, receipt.Id);
        clock.Now = clock.Now.AddDays(8);
        await store.SetHoldAsync(_principal, _scope, receipt.Id, true);
        (await store.ApplyDispositionAsync(_principal, _scope, receipt.Id)).ShouldBeFalse();
        await store.SetHoldAsync(_principal, _scope, receipt.Id, false);
        (await store.ApplyDispositionAsync(_principal, _scope, receipt.Id)).ShouldBeFalse();
        clock.Now = clock.Now.AddDays(7);
        (await store.ApplyDispositionAsync(_principal, _scope, receipt.Id)).ShouldBeTrue();
        await Should.ThrowAsync<ContentConflictException>(() => store.SetHoldAsync(_principal, _scope, receipt.Id, true));
    }

    [Fact]
    public async Task NonCooperativeInputHasBoundedCancellationBeforePersistence()
    {
        using var dapr = new RecordingDaprClient { PersistTypedState = true };
        var authority = new ContentTestAuthority();
        var store = new DaprAuthoritativeContentStore(dapr, authority, Options.Create(new AuthoritativeContentOptions { OperationTimeout = TimeSpan.FromMilliseconds(100) }), TimeProvider.System);
        using var input = new ContentNonCooperativeStream();
        await Should.ThrowAsync<OperationCanceledException>(() => store.UploadAsync(_principal, UploadRequest(_bytes), input));
        dapr.TrySaveByteOperations.ShouldBeEmpty();
    }

    [Fact]
    public async Task ExactConfiguredByteBoundaryPreservesBinaryAndInvalidUtf8()
    {
        using var dapr = new RecordingDaprClient { PersistTypedState = true };
        var authority = new ContentTestAuthority { Outcome = ContentPublicationOutcome.Accepted };
        var store = new DaprAuthoritativeContentStore(dapr, authority, Options.Create(new AuthoritativeContentOptions { MaximumBodyBytes = 6 }), TimeProvider.System);
        ContentReceipt receipt = await UploadAsync(store);
        await store.PrepareAsync(_principal, Publication("boundary", receipt));
        (await store.ReadAsync(_principal, Read() with { MaximumBytes = 6 })).Bytes.ShouldBe(_bytes);
        await Should.ThrowAsync<ArgumentException>(() => store.UploadAsync(_principal, UploadRequest([0, 1, 2, 3, 4, 5, 6]), new MemoryStream([0, 1, 2, 3, 4, 5, 6])));
        await Should.ThrowAsync<ArgumentOutOfRangeException>(() => store.ReadAsync(_principal, Read() with { MaximumBytes = 5 }));
    }

    [Fact]
    public async Task TrustedUploadInspectionEnforcesSeparateAuthorityTaskTenantAndActualIntegrity()
    {
        using var dapr = new RecordingDaprClient { PersistTypedState = true };
        var authority = new ContentTestAuthority { DenyOrdinaryRead = true };
        var store = Store(dapr, authority);
        ContentReceipt receipt = await UploadAsync(store);
        var request = new ContentUploadVerification(_scope, "task-a", receipt.Id);
        ContentReadResult inspected = await store.InspectUploadAsync(_principal, request);
        inspected.Receipt.ShouldBe(receipt);
        inspected.Bytes.ShouldBe(_bytes);
        await Should.ThrowAsync<UnauthorizedAccessException>(() => store.ReadAsync(_principal, Read()));
        await Should.ThrowAsync<ContentUnavailableException>(() => store.InspectUploadAsync(_principal, request with { TaskId = "other-task" }));
        await Should.ThrowAsync<UnauthorizedAccessException>(() => store.InspectUploadAsync(_principal, request with { Scope = _scope with { TenantId = "tenant-b" } }));
        authority.DenyInspection = true;
        await Should.ThrowAsync<UnauthorizedAccessException>(() => store.InspectUploadAsync(_principal, request));
        // Verification's descriptor-only result and operation remain unchanged.
        (await store.VerifyUploadAsync(_principal, request)).ShouldBe(receipt);
        authority.DenyInspection = false;
        string bodyKey = dapr.TrySaveByteOperations.Single(static write => write.Key.StartsWith("authoritative-content:body:", StringComparison.Ordinal)).Key;
        dapr.SeedByteStore(bodyKey, JsonSerializer.SerializeToUtf8Bytes(new ContentStoredBody(receipt, [1]), dapr.JsonSerializerOptions));
        await Should.ThrowAsync<ContentUnavailableException>(() => store.InspectUploadAsync(_principal, request));
        await dapr.DeleteStateAsync("statestore", bodyKey, null!, null!, CancellationToken.None);
        await Should.ThrowAsync<ContentUnavailableException>(() => store.InspectUploadAsync(_principal, request));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task RevokedClaimCannotDeleteAfterConcurrentResumeOrFailedGrant(bool resumed)
    {
        using var dapr = new RecordingDaprClient { PersistTypedState = true };
        var authority = new ContentTestAuthority { Disposition = new(1, true, false, false, false) };
        var clock = new ContentTestTime();
        var store = Store(dapr, authority, clock);
        ContentReceipt receipt = await UploadAsync(store);
        await store.ApplyDispositionAsync(_principal, _scope, receipt.Id);
        clock.Now = clock.Now.AddDays(8);
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        int calls = 0;
        authority.SealAsync = async (_, token) =>
        {
            if (Interlocked.Increment(ref calls) != 1) return false;
            entered.SetResult();
            await release.Task.WaitAsync(token);
            return true;
        };
        Task<bool> first = store.ApplyDispositionAsync(_principal, _scope, receipt.Id);
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        if (resumed) authority.Disposition = new(2, false, true, false, false);
        (await Store(dapr, authority, clock).ApplyDispositionAsync(_principal, _scope, receipt.Id)).ShouldBeFalse();
        await store.SetHoldAsync(_principal, _scope, receipt.Id, true);
        await store.PrepareAsync(_principal, Publication("new-reference", receipt));
        release.SetResult();
        (await first).ShouldBeFalse();
        dapr.TryDeleteByteOperations.ShouldBeEmpty();
        (await store.VerifyUploadAsync(_principal, new(_scope, "task-a", receipt.Id))).ShouldBe(receipt);
    }

    [Fact]
    public async Task StaleFailedClaimCannotReopenCommittedDeletionWhileBodyDeletionIsPaused()
    {
        using var dapr = new RecordingDaprClient { PersistTypedState = true };
        var authority = new ContentTestAuthority { Disposition = new(1, true, false, false, false) };
        var clock = new ContentTestTime();
        var store = Store(dapr, authority, clock);
        ContentReceipt receipt = await UploadAsync(store);
        await store.ApplyDispositionAsync(_principal, _scope, receipt.Id);
        clock.Now = clock.Now.AddDays(8);
        var firstEntered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseFirst = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var deletionEntered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseDeletion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        int calls = 0;
        authority.SealAsync = async (_, token) =>
        {
            if (Interlocked.Increment(ref calls) != 1) return true;
            firstEntered.SetResult();
            await releaseFirst.Task.WaitAsync(token);
            return false;
        };
        Task<bool> stale = store.ApplyDispositionAsync(_principal, _scope, receipt.Id);
        await firstEntered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        string bodyKey = dapr.TrySaveByteOperations.Single(static write => write.Key.StartsWith("authoritative-content:body:", StringComparison.Ordinal)).Key;
        int bodyReads = 0;
        dapr.BeforeReadByteStateAsync = async (key, token) =>
        {
            if (key != bodyKey || Interlocked.Increment(ref bodyReads) != 1) return;
            deletionEntered.SetResult();
            await releaseDeletion.Task.WaitAsync(token);
        };
        Task<bool> committed = Store(dapr, authority, clock).ApplyDispositionAsync(_principal, _scope, receipt.Id);
        await deletionEntered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        releaseFirst.SetResult();
        // The stale failure sees the irreversible decision and may safely finish its deletion.
        (await stale).ShouldBeTrue();
        await Should.ThrowAsync<ContentConflictException>(() => store.SetHoldAsync(_principal, _scope, receipt.Id, true));
        await Should.ThrowAsync<ContentConflictException>(() => store.PrepareAsync(_principal, Publication("late-reference", receipt)));
        authority.Disposition = new(2, false, true, true, true);
        (await Store(dapr, authority, clock).ApplyDispositionAsync(_principal, _scope, receipt.Id)).ShouldBeTrue();
        releaseDeletion.SetResult();
        (await committed).ShouldBeTrue();
        dapr.TrySaveByteOperations.Count(write => write.Key.StartsWith("authoritative-content:body:", StringComparison.Ordinal)
            && JsonSerializer.Deserialize<ContentStoredBody>(write.Value, dapr.JsonSerializerOptions)!.Erased).ShouldBe(1);
    }

    [Fact]
    public async Task ReplacementCompletesIrreversibleDeletionAfterCrashWithoutReopeningAuthority()
    {
        using var dapr = new RecordingDaprClient { PersistTypedState = true };
        var authority = new ContentTestAuthority { Disposition = new(1, true, false, false, false) };
        var clock = new ContentTestTime();
        var store = Store(dapr, authority, clock);
        ContentReceipt receipt = await UploadAsync(store);
        await store.ApplyDispositionAsync(_principal, _scope, receipt.Id);
        clock.Now = clock.Now.AddDays(8);
        dapr.BeforeReadByteState = key =>
        {
            if (key.StartsWith("authoritative-content:body:", StringComparison.Ordinal)) throw new OperationCanceledException();
        };
        await Should.ThrowAsync<OperationCanceledException>(() => store.ApplyDispositionAsync(_principal, _scope, receipt.Id));
        await Should.ThrowAsync<ContentConflictException>(() => store.SetHoldAsync(_principal, _scope, receipt.Id, true));
        await Should.ThrowAsync<ContentConflictException>(() => store.PrepareAsync(_principal, Publication("late-reference", receipt)));
        dapr.BeforeReadByteState = null!;
        authority.RejectDispositionRead = true;
        authority.SealAsync = (_, _) => throw new InvalidOperationException("A committed decision must not request another source grant.");
        (await Store(dapr, authority, clock).ApplyDispositionAsync(_principal, _scope, receipt.Id)).ShouldBeTrue();
        dapr.TrySaveByteOperations.Count(write => write.Key.StartsWith("authoritative-content:body:", StringComparison.Ordinal)
            && JsonSerializer.Deserialize<ContentStoredBody>(write.Value, dapr.JsonSerializerOptions)!.Erased).ShouldBe(1);
    }

    [Fact]
    public async Task InterruptedSourceGrantRequiresFreshSourceProofAndEpochBeforeDeletionCommit()
    {
        using var dapr = new RecordingDaprClient { PersistTypedState = true };
        var authority = new ContentTestAuthority { Disposition = new(1, true, false, false, false) };
        var clock = new ContentTestTime();
        var store = Store(dapr, authority, clock);
        ContentReceipt receipt = await UploadAsync(store);
        await store.ApplyDispositionAsync(_principal, _scope, receipt.Id);
        clock.Now = clock.Now.AddDays(8);
        authority.SealAsync = (_, _) => throw new OperationCanceledException();
        await Should.ThrowAsync<OperationCanceledException>(() => store.ApplyDispositionAsync(_principal, _scope, receipt.Id));
        authority.Disposition = new(2, false, true, false, false);
        (await Store(dapr, authority, clock).ApplyDispositionAsync(_principal, _scope, receipt.Id)).ShouldBeFalse();
        await store.SetHoldAsync(_principal, _scope, receipt.Id, true);
        await store.SetHoldAsync(_principal, _scope, receipt.Id, false);
        authority.Disposition = new(3, true, false, false, false);
        (await store.ApplyDispositionAsync(_principal, _scope, receipt.Id)).ShouldBeFalse();
        authority.SealAsync = (_, _) => Task.FromResult(true);
        clock.Now = clock.Now.AddDays(6);
        (await store.ApplyDispositionAsync(_principal, _scope, receipt.Id)).ShouldBeFalse();
        dapr.TryDeleteByteOperations.ShouldBeEmpty();
        clock.Now = clock.Now.AddDays(1);
        (await Store(dapr, authority, clock).ApplyDispositionAsync(_principal, _scope, receipt.Id)).ShouldBeTrue();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task DelayedCreateCannotRestoreBytesAfterErasureOrCompletionCrash(bool empty)
    {
        using var dapr = new RecordingDaprClient { PersistTypedState = true };
        var authority = new ContentTestAuthority { Disposition = new(1, true, false, false, false) };
        var clock = new ContentTestTime(); var store = Store(dapr, authority, clock);
        bool delayed = false; string? receiptId = null;
        dapr.BeforeTrySaveByteState = (key, _) =>
        {
            if (delayed || !key.StartsWith("authoritative-content:body:", StringComparison.Ordinal)) return;
            delayed = true; receiptId = key[(key.LastIndexOf(':') + 1)..];
            store.ApplyDispositionAsync(_principal, _scope, receiptId).GetAwaiter().GetResult().ShouldBeFalse();
            clock.Now = clock.Now.AddDays(8); bool crashed = false;
            dapr.AfterTrySaveByteState = (writtenKey, value) =>
            {
                if (!crashed && writtenKey == key && JsonSerializer.Deserialize<ContentStoredBody>(value.Span, dapr.JsonSerializerOptions) is { Erased: true })
                { crashed = true; throw new OperationCanceledException(); }
            };
            Should.Throw<OperationCanceledException>(() => store.ApplyDispositionAsync(_principal, _scope, receiptId).GetAwaiter().GetResult());
            dapr.AfterTrySaveByteState = null!;
            Store(dapr, authority, clock).ApplyDispositionAsync(_principal, _scope, receiptId).GetAwaiter().GetResult().ShouldBeTrue();
        };
        await Should.ThrowAsync<ContentUnavailableException>(() => UploadAsync(store, empty ? [] : _bytes));
        var replacement = Store(dapr, authority, clock);
        (await replacement.ListUploadsAsync(_principal, _scope)).Single().Erased.ShouldBeTrue();
        await Should.ThrowAsync<ContentUnavailableException>(() => replacement.VerifyUploadAsync(_principal, new(_scope, "task-a", receiptId!)));
        var write = dapr.TrySaveByteOperations.Last(write => write.Key.StartsWith("authoritative-content:body:", StringComparison.Ordinal));
        var tombstone = await dapr.GetStateAsync<ContentStoredBody>("statestore", write.Key, null, null!, CancellationToken.None);
        tombstone.Erased.ShouldBeTrue(); tombstone.Bytes.ShouldBeEmpty();
    }

    [Fact]
    public async Task ApplyReconcilesRejectedPreparationButProtectsIndeterminatePreparation()
    {
        using var dapr = new RecordingDaprClient { PersistTypedState = true };
        var authority = new ContentTestAuthority { Disposition = new(1, true, false, false, false) };
        var clock = new ContentTestTime(); var store = Store(dapr, authority, clock);
        ContentReceipt receipt = await UploadAsync(store);
        await store.PrepareAsync(_principal, Publication("rejected", receipt));
        (await store.ApplyDispositionAsync(_principal, _scope, receipt.Id)).ShouldBeFalse();
        (await store.ListUploadsAsync(_principal, _scope)).Single().CleanupEpoch.ShouldBeNull();
        authority.Outcomes["rejected"] = ContentPublicationOutcome.Rejected;
        (await store.ApplyDispositionAsync(_principal, _scope, receipt.Id)).ShouldBeFalse();
        (await store.ListUploadsAsync(_principal, _scope)).Single().CleanupEpoch.ShouldNotBeNull();
        clock.Now = clock.Now.AddDays(7);
        (await Store(dapr, authority, clock).ApplyDispositionAsync(_principal, _scope, receipt.Id)).ShouldBeTrue();
    }

    private static DaprAuthoritativeContentStore Store(RecordingDaprClient dapr, ContentTestAuthority authority, TimeProvider? clock = null)
        => new(dapr, authority, Options.Create(new AuthoritativeContentOptions()), clock ?? TimeProvider.System);
    private static ContentUploadRequest UploadRequest(byte[] bytes)
        => new(_scope, "task-a", Convert.ToHexStringLower(SHA256.HashData(bytes)), bytes.Length, "application/octet-stream");
    private static Task<ContentReceipt> UploadAsync(IAuthoritativeContentStore store, byte[]? bytes = null)
        => store.UploadAsync(_principal, UploadRequest(bytes ?? _bytes), new MemoryStream(bytes ?? _bytes));
    private static ContentChange Change(ContentReceipt receipt)
        => new("file-a", "incarnation-a", 1, receipt.Id, new Dictionary<string, string> { ["path"] = "src/a.bin" }, receipt);
    private static ContentPublicationRequest Publication(string command, ContentReceipt receipt)
        => new(_scope, command, "task-a", 1, 0, [Change(receipt)]);
    private static ContentReadRequest Read() => new(_scope, "file-a", "incarnation-a", 1, 1024);
}
