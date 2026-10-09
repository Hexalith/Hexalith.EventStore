using Hexalith.EventStore.Contracts.Security;

namespace Hexalith.EventStore.Contracts.Tests.Security;

/// <summary>Executable exact final-durability/current-authority and incremental pending carrier bound checks over the production shared protocol.</summary>
public sealed class RecoverableAnchoredStateTests : IAnchoredStateTransitionAuthority
{
    private bool _verified = true;
    private int _records;
    private int _recoveries;
    private bool _admissionAllowed = true;
    private bool _failAdmission;
    private bool _recoveryAllowed = true;
    private bool _recordAllowed;
    private string? _anchor;
    private readonly Dictionary<string, AnchoredStateTransition> _admissions = new(StringComparer.Ordinal);
    /// <inheritdoc/>
    public Task<bool> AdmitTransitionAsync(AnchoredStateTransition transition, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(transition);
        if (_failAdmission) { return Task.FromException<bool>(new HttpRequestException("Controlled failure before independent admission.")); }
        if (!_admissionAllowed) { return Task.FromResult(false); }
        _admissions[transition.TargetDigest] = transition with { TargetBytes = transition.TargetBytes.ToArray() }; return Task.FromResult(true);
    }
    /// <inheritdoc/>
    public Task<bool> RecoverTransitionAsync(AnchoredStateTransition transition, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(transition);
        _recoveries++;
        return _recoveryAllowed && _admissions.TryGetValue(transition.TargetDigest, out var original)
            && System.Text.Json.JsonSerializer.Serialize(original) == System.Text.Json.JsonSerializer.Serialize(transition)
            ? RecordTransitionAsync(original, cancellationToken) : Task.FromResult(false);
    }
    /// <inheritdoc/>
    public Task<bool> RecordTransitionAsync(AnchoredStateTransition transition, CancellationToken cancellationToken = default)
    { ArgumentNullException.ThrowIfNull(transition); _records++; if (_recordAllowed) { _anchor = transition.TargetDigest; _verified = true; } return Task.FromResult(_recordAllowed); }
    /// <inheritdoc/>
    public Task<bool> VerifyTransitionAsync(AnchoredStateTransition transition, CancellationToken cancellationToken = default) => Task.FromResult(_verified);

    /// <summary>A target write suspended after prevalidation cannot certify stale durable bytes or changed final anchor/journal authority.</summary>
    [Theory]
    [InlineData("bytes")][InlineData("anchor")][InlineData("journal")][InlineData("valid")]
    public async Task ReconciliationReconfirmsFreshDurableTargetAndCurrentExactAuthorityAfterPersistence(string vector)
    {
        var transition = RecoverableAnchoredState.Prepare("scope", 0, 1, "before", "after");
        string anchor = transition.TargetDigest, durable = "after";
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously); var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var pending = RecoverableAnchoredState.ReconcileAsync("scope", "before", transition, value => value,
            value => Task.FromResult(RecoverableAnchoredState.Digest(value) == anchor), this, async value =>
            { value.ShouldBe("after"); entered.TrySetResult(); await release.Task; return durable; });
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        if (vector == "bytes") { durable = "before"; }
        if (vector == "anchor") { anchor = RecoverableAnchoredState.Digest("different-current-authority"); }
        if (vector == "journal") { _verified = false; }
        release.SetResult();
        if (vector == "valid") { (await pending).ShouldBe("after"); }
        else { await Should.ThrowAsync<InvalidOperationException>(() => pending); }
        _records.ShouldBe(0);
    }
    /// <summary>Only a separately authorized mutation can resolve an independently admitted exact original after its caller is absent; unknown/foreign stages remain restrictive.</summary>
    [Theory]
    [InlineData("valid")][InlineData("not-admitted")][InlineData("unknown")][InlineData("different-target")]
    public async Task AbsentOriginalCallerRecoveryRequiresRetainedExactAdmissionAndSeparateMutation(string vector)
    {
        _verified = false; _anchor = RecoverableAnchoredState.Digest("before"); _admissionAllowed = vector != "not-admitted";
        var original = RecoverableAnchoredState.Prepare("scope", 0, 1, "before", "after"); AnchoredStateTransition? pending = null;
        (await RecoverableAnchoredState.CommitAsync(original, this, () => Task.FromResult(pending), value => { pending = value; return Task.CompletedTask; })).ShouldBeFalse();
        pending ??= original; _recoveryAllowed = vector != "unknown";
        if (vector == "different-target") { pending = RecoverableAnchoredState.Prepare("scope", 0, 1, "before", "foreign"); }
        var retained = System.Text.Json.JsonSerializer.Serialize(pending); int saves = 0;
        Task<bool> Validate(string value) => Task.FromResult(RecoverableAnchoredState.Digest(value) == _anchor);
        Task<string> Persist(string value) { saves++; return Task.FromResult(value); }
        (await RecoverableAnchoredState.ReconcileAsync("scope", "before", pending, value => value, Validate, this, Persist)).ShouldBe("before");
        _recoveries.ShouldBe(0); saves.ShouldBe(0); _recordAllowed = true;
        var recovered = await RecoverableAnchoredState.ReconcileAsync("scope", "before", pending, value => value, Validate, this, Persist, true);
        recovered.ShouldBe(vector == "valid" ? "after" : "before"); saves.ShouldBe(vector == "valid" ? 1 : 0);
        System.Text.Json.JsonSerializer.Serialize(pending).ShouldBe(retained);
        if (vector == "valid")
        {
            var later = RecoverableAnchoredState.Prepare("scope", 1, 2, recovered, "later"); pending = null;
            (await RecoverableAnchoredState.CommitAsync(later, this, () => Task.FromResult(pending), value => { pending = value; return Task.CompletedTask; })).ShouldBeTrue();
            (await RecoverableAnchoredState.ReconcileAsync("scope", recovered, pending, value => value, Validate, this, Persist)).ShouldBe("later");
        }
    }
    /// <summary>No primary stage or journal advancement follows a denied admission or a failure after independent admission but before staging; later admitted work still progresses.</summary>
    [Theory]
    [InlineData("denied")][InlineData("before-admission")][InlineData("before-stage")]
    public async Task AdmissionAndPreStageFailureDoNotCreateOrAdvanceUnperformedOriginal(string failurePoint)
    {
        bool afterAdmission = failurePoint == "before-stage";
        _verified = false; _admissionAllowed = failurePoint != "denied"; _failAdmission = failurePoint == "before-admission"; AnchoredStateTransition? pending = null;
        var original = RecoverableAnchoredState.Prepare("scope", 0, 1, "before", "after");
        Task Stage(AnchoredStateTransition value) => Task.FromException(new HttpRequestException("Controlled failure before durable staging."));
        if (failurePoint != "denied") { await Should.ThrowAsync<HttpRequestException>(() => RecoverableAnchoredState.CommitAsync(original, this, () => Task.FromResult(pending), Stage)); }
        else { (await RecoverableAnchoredState.CommitAsync(original, this, () => Task.FromResult(pending), Stage)).ShouldBeFalse(); }
        pending.ShouldBeNull(); _records.ShouldBe(0); _recoveries.ShouldBe(0); _admissions.Count.ShouldBe(afterAdmission ? 1 : 0);
        _admissionAllowed = true; _failAdmission = false; _recordAllowed = true;
        var later = RecoverableAnchoredState.Prepare("scope", 0, 1, "before", "different-later-original");
        (await RecoverableAnchoredState.CommitAsync(later, this, () => Task.FromResult(pending), value => { pending = value; return Task.CompletedTask; })).ShouldBeTrue();
        pending!.TargetDigest.ShouldBe(later.TargetDigest); _records.ShouldBe(1);
    }

    /// <summary>Pending byte production stops at the bounded stream before completing the oversized carrier or making any independent journal call.</summary>
    [Fact]
    public void OversizedPendingSerializationStopsProductionBeforeJournalInvocation()
    {
        int produced = 0; string item = new('x', 4096);
        IEnumerable<string> original = Array.Empty<string>();
        IEnumerable<string> oversized = Enumerable.Range(0, 40000).Select(_ => { produced++; return item; });
        Should.Throw<InvalidOperationException>(() => RecoverableAnchoredState.Prepare("scope", 0, 1, original, oversized));
        produced.ShouldBeLessThan(10000); produced.ShouldBeGreaterThan(0); _records.ShouldBe(0);
    }
    /// <summary>Caller mutation after entry cannot change admitted or staged original transition bytes.</summary>
    [Fact]
    public async Task CommitOwnsExactBytesAcrossCallerMutation()
    {
        _recordAllowed = true;
        var original = RecoverableAnchoredState.Prepare("scope", 0, 1, "before", "after");
        byte[] expected = original.TargetBytes.ToArray();
        AnchoredStateTransition? staged = null;
        int reads = 0;
        bool committed = await RecoverableAnchoredState.CommitAsync(original, this,
            () => { if (reads++ == 0) { original.TargetBytes[0] ^= 0x01; } return Task.FromResult(staged); },
            value => { staged = value; return Task.CompletedTask; });
        committed.ShouldBeTrue();
        original.TargetBytes.ShouldNotBe(expected);
        _admissions[original.TargetDigest].TargetBytes.ShouldBe(expected);
        staged.ShouldNotBeNull();
        staged.TargetBytes.ShouldBe(expected);
        _records.ShouldBe(1);
    }

    /// <summary>A stage provider mutating its borrowed bytes cannot turn an invalid carrier into an exact journal transition.</summary>
    [Fact]
    public async Task CommitRejectsMutatedStageBeforeJournal()
    {
        _recordAllowed = true;
        var original = RecoverableAnchoredState.Prepare("scope", 0, 1, "before", "after");
        AnchoredStateTransition? staged = null;
        await Should.ThrowAsync<InvalidOperationException>(() => RecoverableAnchoredState.CommitAsync(original, this,
            () => Task.FromResult(staged), value => { staged = value; staged.TargetBytes[0] ^= 0x01; return Task.CompletedTask; }));
        _records.ShouldBe(0);
        original.TargetBytes.ShouldBe(RecoverableAnchoredState.Prepare("scope", 0, 1, "before", "after").TargetBytes);
    }

}
