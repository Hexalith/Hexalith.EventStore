using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using Hexalith.EventStore.Client.Events;
using Hexalith.EventStore.Server.Events;
using Shouldly;

namespace Hexalith.EventStore.Server.Tests.Events;
/// <summary>Proves private unsigned checkpoint adoption and range preparation without signing or replay dispatch.</summary>
public sealed class DaprLogicalCheckpointInitialTests
{
    /// <summary>Retains exact independent pins and charges while preparing current zero without any event or state effects.</summary>
    [Fact]
    public async Task ActualCurrentZeroRetainsPrivatePriorAndClearsEveryOwnedImage()
    {
        await using var fixture = new DaprLogicalCheckpointInitialFixture();
        await fixture.PrepareAsync();
        int before = fixture.Budget.LiveBytes;
        int events = fixture.Candidate.Checkpoint.Replay.Source.EventReads;
        int applies = fixture.Candidate.Checkpoint.Replay.Applies;
        var durable = fixture.Candidate.Checkpoint.Durable.ToDictionary(row => row.Key, row => row.Value.ToArray());
        var initial = await fixture.AdoptAsync();
        DaprLogicalCheckpointInitialSelection selection = DaprLogicalCheckpointInitialCodec.Decode(initial.SelectionImage);
        selection.CoveredSequence.ShouldBe(2L);
        selection.ActorHead.ShouldBe(2L);
        selection.TargetSequence.ShouldBe(2L);
        selection.CheckpointSourceHash.ToArray().ShouldBe(DaprLogicalClaimCodec.ComputeSourceBindingHash(fixture.Checkpoint, fixture.Budget));
        selection.RequestedSourceHash.ToArray().ShouldBe(selection.CheckpointSourceHash.ToArray());
        selection.FoldHash.ToArray().ShouldBe(fixture.Fold.Fingerprint.ToArray());
        selection.ReconstructionHash.ToArray().ShouldBe(fixture.Fold.Reconstruction.Fingerprint.ToArray());
        selection.RegistryFingerprint.ToArray().ShouldBe(Convert.FromHexString(fixture.Fold.Reconstruction.Evolution.RegistryFingerprint));
        var state = (ImmutablePayload)initial.CanonicalState;
        byte[] canonical = (byte[])Field(state, "_owner")!;
        canonical.ShouldBe("{\"value\":2}"u8.ToArray());
        selection.StateHash.ToArray().ShouldBe(SHA256.HashData(canonical));
        byte[] image = Bytes(initial.SelectionImage);
        byte[] pin = (byte[])Field(initial, "_selectionHash")!;
        var candidate = (DaprLogicalCheckpointCandidate)Field(initial, "_candidate")!;
        byte[][] candidateImages = [Bytes(candidate.State), Bytes(candidate.Root), Bytes(candidate.Witness)];
        selection.RootHash.ToArray().ShouldBe(SHA256.HashData(candidateImages[1]));
        selection.WitnessHash.ToArray().ShouldBe(SHA256.HashData(candidateImages[2]));
        int retained = fixture.Budget.LiveBytes;
        retained.ShouldBe(before + 8192 + candidateImages.Sum(bytes => bytes.Length * 2 + 256) + 12288 + canonical.Length + image.Length * 2 + 256);
        using (DaprLogicalResponseOwner range = await initial.PrepareRangeAsync(DaprLogicalCheckpointRangeKind.CurrentZero, 256, CancellationToken.None))
        {
            DaprLogicalCheckpointRangePreparation decoded = DaprLogicalCheckpointRangeCodec.Decode(range.Bytes);
            decoded.SelectionHash.ToArray().ShouldBe(SHA256.HashData(image));
            decoded.StartSequence.ShouldBe(3L);
            decoded.EndSequence.ShouldBe(2L);
            decoded.PlannedCount.ShouldBe(0);
            decoded.Kind.ShouldBe(DaprLogicalCheckpointRangeKind.CurrentZero);
            fixture.Budget.LiveBytes.ShouldBe(retained + range.Bytes.Length * 2 + 256);
        }

        fixture.Budget.LiveBytes.ShouldBe(retained);
        fixture.Candidate.Checkpoint.Replay.Source.EventReads.ShouldBe(events);
        fixture.Candidate.Checkpoint.Replay.Applies.ShouldBe(applies);
        fixture.Candidate.Checkpoint.Stages.ShouldBe(3);
        fixture.Candidate.Checkpoint.Saves.ShouldBe(1);
        foreach ((string key, byte[] value)in durable)
            fixture.Candidate.Checkpoint.Durable[key].ShouldBe(value);
        initial.Dispose();
        fixture.Budget.LiveBytes.ShouldBe(before);
        foreach (byte[] bytes in candidateImages.Append(canonical).Append(image).Append(pin))
            bytes.ShouldAllBe(value => value == 0);
        foreach (string name in new[]
        {
            "_candidate",
            "_canonical",
            "_selection",
            "_selectionHash",
            "_metadata",
            "_fold",
            "_checkpoint",
            "_requested"
        }

        )
            Field(initial, name).ShouldBeNull();
        Should.Throw<ObjectDisposedException>(() => _ = initial.CanonicalState);
    }

    /// <summary>Separates actual head from covered and requested targets and only plans bounded checkpoint-plus-one ranges.</summary>
    [Theory]
    [InlineData(3L, 3L, 256, 3L, 1)]
    [InlineData(1000L, 1000L, 256, 258L, 256)]
    [InlineData(1000L, 999L, 1, 3L, 1)]
    public async Task ActualBelowHeadCandidatePlansTailWithoutReadingOrApplyingIt(long head, long target, int count, long end, int planned)
    {
        await using var fixture = new DaprLogicalCheckpointInitialFixture();
        await fixture.PrepareAsync(head);
        int events = fixture.Candidate.Checkpoint.Replay.Source.EventReads;
        int applies = fixture.Candidate.Checkpoint.Replay.Applies;
        using var initial = await fixture.AdoptAsync(target);
        DaprLogicalCheckpointInitialSelection selection = DaprLogicalCheckpointInitialCodec.Decode(initial.SelectionImage);
        selection.ActorHead.ShouldBe(head);
        selection.CoveredSequence.ShouldBe(2L);
        selection.TargetSequence.ShouldBe(target);
        selection.CheckpointSourceHash.ToArray().ShouldNotBe(selection.RequestedSourceHash.ToArray());
        using var range = await initial.PrepareRangeAsync(DaprLogicalCheckpointRangeKind.Tail, count, CancellationToken.None);
        DaprLogicalCheckpointRangePreparation decoded = DaprLogicalCheckpointRangeCodec.Decode(range.Bytes);
        decoded.StartSequence.ShouldBe(3L);
        decoded.EndSequence.ShouldBe(end);
        decoded.PlannedCount.ShouldBe(planned);
        decoded.ActorHead.ShouldBe(head);
        decoded.TargetSequence.ShouldBe(target);
        fixture.Candidate.Checkpoint.Replay.Source.EventReads.ShouldBe(events);
        fixture.Candidate.Checkpoint.Replay.Applies.ShouldBe(applies);
        fixture.Candidate.Checkpoint.Stages.ShouldBe(3);
        fixture.Candidate.Checkpoint.Saves.ShouldBe(1);
    }

    /// <summary>Refuses current below head, empty tail and unsupported range/count before actual decisions.</summary>
    [Theory]
    [InlineData(3L, 2L, 0, 256)]
    [InlineData(2L, 2L, 1, 256)]
    [InlineData(3L, 3L, 2, 256)]
    [InlineData(3L, 3L, 1, 0)]
    [InlineData(3L, 3L, 1, 257)]
    public async Task UnsupportedRangeRefusesBeforeActualDecision(long head, long target, int kind, int count)
    {
        await using var fixture = new DaprLogicalCheckpointInitialFixture();
        await fixture.PrepareAsync(head);
        using var initial = await fixture.AdoptAsync(target);
        int decisions = fixture.Candidate.Decisions;
        int reads = fixture.Candidate.Checkpoint.Replay.Reads;
        int before = fixture.Budget.LiveBytes;
        await Should.ThrowAsync<ArgumentException>(() => initial.PrepareRangeAsync((DaprLogicalCheckpointRangeKind)kind, count, CancellationToken.None));
        fixture.Candidate.Decisions.ShouldBe(decisions);
        fixture.Candidate.Checkpoint.Replay.Reads.ShouldBe(reads);
        fixture.Budget.LiveBytes.ShouldBe(before);
    }

    /// <summary>Rejects unsupported admission before options/canonical callbacks or owner reads and disposes the taken candidate.</summary>
    [Theory]
    [InlineData("model")]
    [InlineData("parent")]
    [InlineData("clone")]
    [InlineData("checkpoint")]
    [InlineData("head")]
    [InlineData("etag")]
    [InlineData("timestamp")]
    [InlineData("floor")]
    [InlineData("before-covered")]
    [InlineData("after-head")]
    public async Task InvalidAdmissionRefusesBeforeCallbacksAndClearsTakenCandidate(string point)
    {
        ArgumentNullException.ThrowIfNull(point);
        await using var fixture = new DaprLogicalCheckpointInitialFixture();
        await fixture.PrepareAsync();
        int baseline = fixture.Budget.LiveBytes;
        var candidate = await fixture.Candidate.AcquireAsync();
        byte[] image = Bytes(candidate.State);
        using var clone = new DaprLogicalProjectionFold(fixture.Checkpoint, "projection", "projection:tenant:id", "projection-store", new byte[32], fixture.Fold.Reconstruction, fixture.Budget);
        using var otherBudget = new EventBufferBudget();
        int afterClone = fixture.Budget.LiveBytes - (candidate.State.Length * 2 + candidate.Root.Length * 2 + candidate.Witness.Length * 2 + 3 * 256 + 8192);
        int calls = fixture.Candidate.Checkpoint.Replay.Source.Callbacks.Count;
        int reads = fixture.Candidate.Checkpoint.Replay.Reads;
        int decisions = fixture.Candidate.Decisions;
        DaprLogicalSourceBinding checkpoint = point == "checkpoint" ? fixture.Checkpoint with
        {
            MetadataETag = "wrong"
        }

        : fixture.Checkpoint;
        DaprLogicalSourceBinding requested = point switch
        {
            "checkpoint" => checkpoint,
            "head" => fixture.Checkpoint with
            {
                ActorHead = 3
            },
            "etag" => fixture.Checkpoint with
            {
                MetadataETag = "wrong"
            },
            "timestamp" => fixture.Checkpoint with
            {
                MetadataLastModified = fixture.Checkpoint.MetadataLastModified.ToOffset(TimeSpan.Zero)
            },
            "floor" => fixture.Checkpoint with
            {
                RetainedFloor = 2
            },
            "before-covered" => fixture.Checkpoint with
            {
                TargetSequence = 1
            },
            "after-head" => fixture.Checkpoint with
            {
                TargetSequence = 3
            },
            _ => fixture.Checkpoint
        };
        await Should.ThrowAsync<InvalidOperationException>(() => DaprLogicalCheckpointInitial.AdoptAsync(point == "model" ? "wrong" : DaprLogicalCheckpointInitialCodec.ModelId, candidate, point == "clone" ? clone : fixture.Fold, checkpoint, requested, point == "parent" ? otherBudget : fixture.Budget, CancellationToken.None));
        fixture.Candidate.Checkpoint.Replay.Source.Callbacks.Count.ShouldBe(calls);
        fixture.Candidate.Checkpoint.Replay.Reads.ShouldBe(reads);
        fixture.Candidate.Decisions.ShouldBe(decisions);
        fixture.Budget.LiveBytes.ShouldBe(afterClone);
        otherBudget.LiveBytes.ShouldBe(0);
        image.ShouldAllBe(value => value == 0);
        clone.Dispose();
        fixture.Budget.LiveBytes.ShouldBe(baseline);
    }

    /// <summary>Checks every locally retained pin before the first later callback or actual decision.</summary>
    [Theory]
    [InlineData("selection")]
    [InlineData("canonical")]
    [InlineData("pin")]
    [InlineData("requested")]
    public async Task RetainedPrivateSubstitutionRefusesBeforeLaterCallback(string point)
    {
        ArgumentNullException.ThrowIfNull(point);
        await using var fixture = new DaprLogicalCheckpointInitialFixture();
        await fixture.PrepareAsync();
        using var initial = await fixture.AdoptAsync();
        byte[] selection = Bytes(initial.SelectionImage);
        byte[] canonical = (byte[])Field(initial.CanonicalState, "_owner")!;
        if (point == "selection")
            selection[0] ^= 1;
        else if (point == "canonical")
            canonical[0] ^= 1;
        else if (point == "pin")
            ((byte[])Field(initial, "_selectionHash")!)[0] ^= 1;
        else
            typeof(DaprLogicalCheckpointInitial).GetField("_requested", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(initial, fixture.Checkpoint with { MetadataETag = "changed" });
        int calls = fixture.Candidate.Checkpoint.Replay.Source.Callbacks.Count;
        int reads = fixture.Candidate.Checkpoint.Replay.Reads;
        int decisions = fixture.Candidate.Decisions;
        await Should.ThrowAsync<InvalidOperationException>(() => initial.PrepareRangeAsync(DaprLogicalCheckpointRangeKind.CurrentZero, 1, CancellationToken.None));
        fixture.Candidate.Checkpoint.Replay.Source.Callbacks.Count.ShouldBe(calls);
        fixture.Candidate.Checkpoint.Replay.Reads.ShouldBe(reads);
        fixture.Candidate.Decisions.ShouldBe(decisions);
    }

    /// <summary>Checks actual source, durable completed history and checkpoint bytes during the first yielding owner decision.</summary>
    [Theory]
    [InlineData("source")]
    [InlineData("history")]
    [InlineData("root")]
    [InlineData("private")]
    public async Task YieldingActualLossPreventsRangeReleaseAndRestoresCharges(string point)
    {
        ArgumentNullException.ThrowIfNull(point);
        await using var fixture = new DaprLogicalCheckpointInitialFixture();
        await fixture.PrepareAsync();
        using var initial = await fixture.AdoptAsync();
        byte[] selection = Bytes(initial.SelectionImage);
        int before = fixture.Budget.LiveBytes;
        int fired = 0;
        fixture.Candidate.Before = async (_, _) =>
        {
            await Task.Yield();
            fired++;
            if (point == "source")
                fixture.Candidate.Checkpoint.Replay.Source.Metadata = fixture.Candidate.Checkpoint.Replay.Source.Metadata!with
                {
                    ETag = "changed"
                };
            else if (point == "history")
                fixture.Candidate.Checkpoint.Replay.Store.Put("logical-replay:state:0", new byte[] { 91 });
            else if (point == "root")
                fixture.Candidate.Checkpoint.Durable[fixture.Candidate.Checkpoint.StageKeys[1]][0] ^= 1;
            else
                selection[0] ^= 1;
        };
        await Should.ThrowAsync<InvalidOperationException>(() => initial.PrepareRangeAsync(DaprLogicalCheckpointRangeKind.CurrentZero, 1, CancellationToken.None));
        fired.ShouldBe(1);
        fixture.Budget.LiveBytes.ShouldBe(before);
        fixture.Candidate.Checkpoint.Stages.ShouldBe(3);
        fixture.Candidate.Checkpoint.Saves.ShouldBe(1);
    }

    /// <summary>Observes reserved range bytes through an actually yielding final fence, then cancellation disposes them.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task YieldingFinalRangeCancellationReleasesProvisionalImage(bool foreignException)
    {
        await using var fixture = new DaprLogicalCheckpointInitialFixture();
        await fixture.PrepareAsync();
        using var original = new CancellationTokenSource();
        using var initial = await fixture.AdoptAsync(token: original.Token);
        int before = fixture.Budget.LiveBytes;
        int decisions = fixture.Candidate.Decisions;
        int fired = 0;
        fixture.Candidate.Before = async (ordinal, _) =>
        {
            if (ordinal != decisions + 2)
                return;
            await Task.Yield();
            fixture.Budget.LiveBytes.ShouldBeGreaterThan(before + DaprLogicalCheckpointRangeCodec.Measure(new DaprLogicalCheckpointRangePreparation(new byte[32], new byte[32], 2, 2, 2, 3, 2, 0, DaprLogicalCheckpointRangeKind.CurrentZero, DaprLogicalCheckpointRangeCodec.ModelId)) * 2 + 256);
            fired++;
            original.Cancel();
            if (foreignException)
                throw new OperationCanceledException(new CancellationToken(true));
        };
        OperationCanceledException error = await Should.ThrowAsync<OperationCanceledException>(() => initial.PrepareRangeAsync(DaprLogicalCheckpointRangeKind.CurrentZero, 1, original.Token));
        error.CancellationToken.ShouldBe(original.Token);
        fired.ShouldBe(1);
        fixture.Budget.LiveBytes.ShouldBe(before);
    }

    /// <summary>Original cancellation and exact token identity win before argument validation, actual reads or callbacks.</summary>
    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task ExactOriginatingTokenRefusesForeignWorkBeforeAnyCallback(bool originalCanceled, bool foreignCanceled)
    {
        await using var fixture = new DaprLogicalCheckpointInitialFixture();
        await fixture.PrepareAsync();
        using var original = new CancellationTokenSource();
        using var foreign = new CancellationTokenSource();
        using var initial = await fixture.AdoptAsync(token: original.Token);
        if (originalCanceled)
            original.Cancel();
        if (foreignCanceled)
            foreign.Cancel();
        int decisions = fixture.Candidate.Decisions;
        int calls = fixture.Candidate.Checkpoint.Replay.Source.Callbacks.Count;
        if (originalCanceled)
        {
            OperationCanceledException error = await Should.ThrowAsync<OperationCanceledException>(() => initial.PrepareRangeAsync(DaprLogicalCheckpointRangeKind.CurrentZero, 0, foreign.Token));
            error.CancellationToken.ShouldBe(original.Token);
        }
        else
            await Should.ThrowAsync<InvalidOperationException>(() => initial.PrepareRangeAsync(DaprLogicalCheckpointRangeKind.CurrentZero, 0, foreign.Token));
        fixture.Candidate.Decisions.ShouldBe(decisions);
        fixture.Candidate.Checkpoint.Replay.Source.Callbacks.Count.ShouldBe(calls);
    }

    /// <summary>Rejects cancelling or noncanonical adoption callbacks and clears the taken actual candidate without later writes.</summary>
    [Theory]
    [InlineData("cancel")]
    [InlineData("foreign")]
    [InlineData("noncanonical")]
    public async Task CanonicalAdoptionCallbackFailureClearsTakenOwnerAndPreventsLaterRelease(string mode)
    {
        ArgumentNullException.ThrowIfNull(mode);
        await using var fixture = new DaprLogicalCheckpointInitialFixture();
        await fixture.PrepareAsync();
        using var original = new CancellationTokenSource();
        int baseline = fixture.Budget.LiveBytes;
        var candidate = await fixture.Candidate.AcquireAsync(original.Token);
        byte[] bytes = Bytes(candidate.State);
        int writes = fixture.Candidate.Checkpoint.Replay.Writes;
        int stopped = 0;
        fixture.Candidate.Checkpoint.Replay.Hook = (point, _) =>
        {
            if (point != "read")
                return;
            stopped++;
            if (mode == "noncanonical")
            {
                fixture.Candidate.Checkpoint.Replay.AlwaysNoncanonical = true;
                return;
            }

            original.Cancel();
            if (mode == "foreign")
                throw new OperationCanceledException(new CancellationToken(true));
            throw new IOException("cancelled application callback");
        };
        if (mode == "noncanonical")
            await Should.ThrowAsync<InvalidOperationException>(() => DaprLogicalCheckpointInitial.AdoptAsync(DaprLogicalCheckpointInitialCodec.ModelId, candidate, fixture.Fold, fixture.Checkpoint, fixture.Checkpoint, fixture.Budget, original.Token));
        else
        {
            OperationCanceledException error = await Should.ThrowAsync<OperationCanceledException>(() => DaprLogicalCheckpointInitial.AdoptAsync(DaprLogicalCheckpointInitialCodec.ModelId, candidate, fixture.Fold, fixture.Checkpoint, fixture.Checkpoint, fixture.Budget, original.Token));
            error.CancellationToken.ShouldBe(original.Token);
            fixture.Candidate.Checkpoint.Replay.Writes.ShouldBe(writes);
        }

        stopped.ShouldBeGreaterThan(0);
        if (mode == "noncanonical")
            fixture.Candidate.Checkpoint.Replay.Writes.ShouldBeLessThanOrEqualTo(writes + 2);
        fixture.Budget.LiveBytes.ShouldBe(baseline);
        bytes.ShouldAllBe(value => value == 0);
        Should.Throw<ObjectDisposedException>(() => fixture.Candidate.Checkpoint.Replay.Borrowed[^1].CopyTo(0, new byte[1]));
        fixture.Candidate.Checkpoint.Stages.ShouldBe(3);
        fixture.Candidate.Checkpoint.Saves.ShouldBe(1);
    }

    /// <summary>Requires the initial fence itself to reject private substitution during its actual owner await.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task YieldingPrivateSubstitutionIsRefusedByTheInitialFence(bool canonical)
    {
        await using var fixture = new DaprLogicalCheckpointInitialFixture();
        await fixture.PrepareAsync();
        using var initial = await fixture.AdoptAsync();
        byte[] bytes = canonical ? (byte[])Field(initial.CanonicalState, "_owner")! : Bytes(initial.SelectionImage);
        int fired = 0;
        int before = fixture.Budget.LiveBytes;
        fixture.Candidate.Before = async (_, _) =>
        {
            await Task.Yield();
            fired++;
            bytes[0] ^= 1;
        };
        await Should.ThrowAsync<InvalidOperationException>(() => initial.RequireCurrentAsync(CancellationToken.None));
        fired.ShouldBe(1);
        fixture.Budget.LiveBytes.ShouldBe(before);
    }

    /// <summary>Rejects loss after range allocation and releases the provisional charge before returning refusal.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task FinalRangeLossDisposesProvisionalOwner(bool privateSelection)
    {
        await using var fixture = new DaprLogicalCheckpointInitialFixture();
        await fixture.PrepareAsync();
        using var initial = await fixture.AdoptAsync();
        byte[] selection = Bytes(initial.SelectionImage);
        int decisions = fixture.Candidate.Decisions;
        int before = fixture.Budget.LiveBytes;
        int fired = 0;
        fixture.Candidate.Before = async (ordinal, _) =>
        {
            if (ordinal != decisions + 2)
                return;
            await Task.Yield();
            fired++;
            fixture.Budget.LiveBytes.ShouldBeGreaterThan(before);
            if (privateSelection)
                selection[0] ^= 1;
            else
                fixture.Candidate.Checkpoint.Durable[fixture.Candidate.Checkpoint.StageKeys[1]][0] ^= 1;
        };
        await Should.ThrowAsync<InvalidOperationException>(() => initial.PrepareRangeAsync(DaprLogicalCheckpointRangeKind.CurrentZero, 1, CancellationToken.None));
        fired.ShouldBe(1);
        fixture.Budget.LiveBytes.ShouldBe(before);
        fixture.Candidate.Checkpoint.Stages.ShouldBe(3);
        fixture.Candidate.Checkpoint.Saves.ShouldBe(1);
    }

    private static object? Field(object value, string name) => value.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(value);
    /// <summary>Cancels the later private adoption read after initial copies exist, clearing their full capacity and stopping writes.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task LatePrivateAdoptionCancellationClearsAllocatedInitialCopies(bool foreignException)
    {
        await using var fixture = new DaprLogicalCheckpointInitialFixture();
        await fixture.PrepareAsync();
        using var original = new CancellationTokenSource();
        int baseline = fixture.Budget.LiveBytes;
        var candidate = await fixture.Candidate.AcquireAsync(original.Token);
        byte[] candidateBytes = Bytes(candidate.State);
        int reads = 0;
        int stoppedWrites = -1;
        ImmutablePayload? retainedCanonical = null;
        byte[]? observed = null;
        fixture.Candidate.Checkpoint.Replay.Hook = (point, _) =>
        {
            // Two current candidate decisions each roundtrip two reads, before the private initial read.
            if (point != "read" || ++reads != 5)
                return;
            object lease = fixture.Candidate.Checkpoint.Replay.Borrowed[^1];
            FieldInfo payloadField = lease.GetType().GetFields(BindingFlags.Instance | BindingFlags.NonPublic).Single(field => typeof(Hexalith.EventStore.Contracts.Events.IReadOnlyPayload).IsAssignableFrom(field.FieldType));
            retainedCanonical = (ImmutablePayload)payloadField.GetValue(lease)!;
            observed = (byte[])Field(retainedCanonical, "_owner")!;
            observed.ShouldBe("{\"value\":2}"u8.ToArray());
            fixture.Budget.LiveBytes.ShouldBeGreaterThan(baseline + 12288);
            stoppedWrites = fixture.Candidate.Checkpoint.Replay.Writes;
            original.Cancel();
            if (foreignException)
                throw new OperationCanceledException(new CancellationToken(true));
            throw new IOException("late private adoption cancellation");
        };
        OperationCanceledException error = await Should.ThrowAsync<OperationCanceledException>(() => DaprLogicalCheckpointInitial.AdoptAsync(DaprLogicalCheckpointInitialCodec.ModelId, candidate, fixture.Fold, fixture.Checkpoint, fixture.Checkpoint, fixture.Budget, original.Token));
        error.CancellationToken.ShouldBe(original.Token);
        reads.ShouldBe(5);
        stoppedWrites.ShouldBeGreaterThan(-1);
        fixture.Candidate.Checkpoint.Replay.Writes.ShouldBe(stoppedWrites);
        observed.ShouldNotBeNull();
        observed.ShouldAllBe(value => value == 0);
        candidateBytes.ShouldAllBe(value => value == 0);
        Field(retainedCanonical!, "_owner").ShouldBeNull();
        fixture.Budget.LiveBytes.ShouldBe(baseline);
    }

    /// <summary>Refuses a locally resealed candidate root because the retained initial selection binds all three exact images.</summary>
    [Fact]
    public async Task ResealedCandidateRootCannotChangeInitialSelectionBeforeActualDecision()
    {
        await using var fixture = new DaprLogicalCheckpointInitialFixture();
        await fixture.PrepareAsync();
        using var initial = await fixture.AdoptAsync();
        var candidate = (DaprLogicalCheckpointCandidate)Field(initial, "_candidate")!;
        byte[] root = Bytes(candidate.Root);
        byte[][] pins = (byte[][])Field(candidate, "_imagePins")!;
        root[0] ^= 1;
        pins[1] = SHA256.HashData(root);
        int decisions = fixture.Candidate.Decisions;
        int reads = fixture.Candidate.Checkpoint.Replay.Reads;
        await Should.ThrowAsync<InvalidOperationException>(() => initial.PrepareRangeAsync(DaprLogicalCheckpointRangeKind.CurrentZero, 1, CancellationToken.None));
        fixture.Candidate.Decisions.ShouldBe(decisions);
        fixture.Candidate.Checkpoint.Replay.Reads.ShouldBe(reads);
    }

    /// <summary>Uses admission pressure to refuse metadata/copy workspace before actual owner work and releases the taken candidate.</summary>
    [Fact]
    public async Task TightSharedCapacityRefusesWithoutUnchargedCopiesOrOwnerReads()
    {
        await using var fixture = new DaprLogicalCheckpointInitialFixture();
        await fixture.PrepareAsync();
        int baseline = fixture.Budget.LiveBytes;
        var candidate = await fixture.Candidate.AcquireAsync();
        byte[] bytes = Bytes(candidate.State);
        int candidateBytes = fixture.Budget.LiveBytes;
        using EventBufferReservation pressure = fixture.Budget.Reserve(128 * 1024 * 1024 - candidateBytes - 4096);
        int decisions = fixture.Candidate.Decisions;
        await Should.ThrowAsync<InvalidOperationException>(() => DaprLogicalCheckpointInitial.AdoptAsync(DaprLogicalCheckpointInitialCodec.ModelId, candidate, fixture.Fold, fixture.Checkpoint, fixture.Checkpoint, fixture.Budget, CancellationToken.None));
        // The addressed refresh may begin but cannot admit its existing canonical workspace; no private initial escapes.
        fixture.Candidate.Decisions.ShouldBeLessThanOrEqualTo(decisions + 1);
        fixture.Budget.LiveBytes.ShouldBe(baseline + 128 * 1024 * 1024 - candidateBytes - 4096);
        bytes.ShouldAllBe(value => value == 0);
        pressure.Dispose();
        fixture.Budget.LiveBytes.ShouldBe(baseline);
        fixture.Candidate.Checkpoint.Stages.ShouldBe(3);
        fixture.Candidate.Checkpoint.Saves.ShouldBe(1);
    }

    private static byte[] Bytes(ReadOnlyMemory<byte> memory)
    {
        MemoryMarshal.TryGetArray(memory, out ArraySegment<byte> bytes).ShouldBeTrue();
        return bytes.Array!;
    }
}
