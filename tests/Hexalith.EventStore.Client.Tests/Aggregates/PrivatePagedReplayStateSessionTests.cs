using System.Runtime.InteropServices;

using Hexalith.EventStore.Client.Aggregates;
using Hexalith.EventStore.Client.Events;
using Hexalith.EventStore.Contracts.Events;

using Shouldly;

namespace Hexalith.EventStore.Client.Tests.Aggregates;

/// <summary>Exercises the local private page boundary without claiming Dapr transition authority.</summary>
public sealed class PrivatePagedReplayStateSessionTests
{
    /// <summary>Checks synchronous private capture, detached prior reads and clearing after the borrowed call.</summary>
    [Fact]
    public async Task SuccessorIsCapturedSynchronouslyAndPriorReadCannotChangePrivateState()
    {
        byte[] initial = [1, 2];
        var budget = new EventBufferBudget();
        using var session = Session(initial, budget);
        byte[] handle = session.CreatePriorHandle();
        initial[0] = 9;
        ReadOnlyMemory<byte> priorCopy = await session.ReadPriorAsync(handle, CancellationToken.None);
        priorCopy.ToArray().ShouldBe([1, 2]);
        MemoryMarshal.TryGetArray(priorCopy, out ArraySegment<byte> borrowed).ShouldBeTrue();
        borrowed.Array![0] = 8;
        using (InvocationPayloadLease prior = session.BorrowRetainedPrior(CancellationToken.None))
        {
            Read(prior).ShouldBe([1, 2]);
        }

        byte[] source = [3, 4];
        ValueTask<byte[]> write = session.WriteSuccessorAsync(handle, source, "state.v1", CancellationToken.None);
        write.IsCompletedSuccessfully.ShouldBeTrue();
        source[0] = 9;
        byte[] successor = await write;
        byte[] successorForSeal = successor.ToArray();
        successor[0] ^= 1;
        Should.Throw<InvalidOperationException>(() => session.SealInvocation(successor));
        session.SealInvocation(successorForSeal);
        using InvocationPayloadLease sealedState = session.BorrowSealedSuccessor(CancellationToken.None);
        Read(sealedState).ShouldBe([3, 4]);
        borrowed.Array.ShouldAllBe(value => value == 0);
        handle.ShouldAllBe(value => value == 0);
        Should.Throw<InvalidOperationException>(() => session.ReadPriorAsync(handle, CancellationToken.None));
        budget.LiveBytes.ShouldBeGreaterThan(0);
        session.Dispose();
        budget.LiveBytes.ShouldBe(0);
        source.ShouldBe([9, 4]);
        Should.Throw<ObjectDisposedException>(() => sealedState.CopyTo(0, new byte[2]));
    }

    /// <summary>Checks another operation, serializer or second successor cannot replace prepared bytes.</summary>
    [Fact]
    public async Task ForeignScopeAndSecondSuccessorCannotChangePreparedBytes()
    {
        using var first = Session(new byte[] { 1 }, new EventBufferBudget());
        using var second = Session(new byte[] { 1 }, new EventBufferBudget(), operationId: "other-operation");
        byte[] handle = first.CreatePriorHandle();
        Should.Throw<InvalidOperationException>(() => second.ReadPriorAsync(handle, CancellationToken.None));
        Should.Throw<InvalidOperationException>(() => first.WriteSuccessorAsync(handle, new byte[] { 2 }, "wrong", CancellationToken.None));
        byte[] successor = await first.WriteSuccessorAsync(handle, new byte[] { 2 }, "state.v1", CancellationToken.None);
        Should.Throw<InvalidOperationException>(() => first.WriteSuccessorAsync(handle, new byte[] { 3 }, "state.v1", CancellationToken.None));
        first.SealInvocation(successor);
        using InvocationPayloadLease prepared = first.BorrowSealedSuccessor(CancellationToken.None);
        Read(prepared).ShouldBe([2]);
    }

    /// <summary>Checks concurrent attempts produce one local candidate.</summary>
    [Fact]
    public async Task ConcurrentCapturesCreateExactlyOneLocalSuccessor()
    {
        using var session = Session(new byte[] { 1 }, new EventBufferBudget());
        byte[] handle = session.CreatePriorHandle();
        byte[]?[] outcomes = await Task.WhenAll(Enumerable.Range(0, 16).Select(index => Task.Run(async () =>
        {
            try { return await session.WriteSuccessorAsync(handle, new byte[] { (byte)index }, "state.v1", CancellationToken.None); }
            catch (InvalidOperationException) { return null; }
        })));
        outcomes.Count(outcome => outcome is not null).ShouldBe(1);
        session.SealInvocation(outcomes.Single(outcome => outcome is not null)!);
    }

    /// <summary>Checks originating cancellation clears unsealed work and preserves the last-good prior.</summary>
    [Fact]
    public async Task OriginalCancellationClearsBorrowedAndUnsealedOwnersButKeepsLastGoodPrior()
    {
        using var cancellation = new CancellationTokenSource();
        var budget = new EventBufferBudget();
        using var session = Session(new byte[] { 1, 2 }, budget, operationCancellation: cancellation.Token);
        byte[] handle = session.CreatePriorHandle();
        ReadOnlyMemory<byte> borrowed = await session.ReadPriorAsync(handle, CancellationToken.None);
        _ = await session.WriteSuccessorAsync(handle, new byte[] { 3, 4 }, "state.v1", CancellationToken.None);
        int preparedCapacity = budget.LiveBytes;
        cancellation.Cancel();
        Should.Throw<OperationCanceledException>(() => session.ReadPriorAsync(handle, CancellationToken.None));
        borrowed.Span.ToArray().ShouldBe([0, 0]);
        budget.LiveBytes.ShouldBeLessThan(preparedCapacity);
        Should.Throw<InvalidOperationException>(() => session.BorrowSealedSuccessor(CancellationToken.None));
        using InvocationPayloadLease retained = session.BorrowRetainedPrior(CancellationToken.None);
        Read(retained).ShouldBe([1, 2]);
    }

    /// <summary>Checks a cancelled method token ends the uncommitted borrowed call.</summary>
    [Fact]
    public async Task MethodCancellationCannotLeaveAUsableUncommittedLease()
    {
        using var session = Session(new byte[] { 1 }, new EventBufferBudget());
        byte[] handle = session.CreatePriorHandle();
        ReadOnlyMemory<byte> borrowed = await session.ReadPriorAsync(handle, CancellationToken.None);
        Should.Throw<OperationCanceledException>(() => session.WriteSuccessorAsync(handle, new byte[] { 2 }, "state.v1", new CancellationToken(true)));
        borrowed.ToArray().ShouldBe([0]);
        Should.Throw<InvalidOperationException>(() => session.CreatePriorHandle());
        using InvocationPayloadLease prior = session.BorrowRetainedPrior(CancellationToken.None);
        Read(prior).ShouldBe([1]);
    }

    /// <summary>Checks independent owner recovery can still read locally sealed bytes.</summary>
    [Fact]
    public async Task SealedOwnerRemainsAvailableForIndependentRecoveryAfterOriginalCancellation()
    {
        using var cancellation = new CancellationTokenSource();
        using var session = Session(new byte[] { 1 }, new EventBufferBudget(), operationCancellation: cancellation.Token);
        byte[] successor = await session.WriteSuccessorAsync(session.CreatePriorHandle(), new byte[] { 2 }, "state.v1", CancellationToken.None);
        session.SealInvocation(successor);
        cancellation.Cancel();
        Should.Throw<OperationCanceledException>(() => session.ReadPriorAsync(new byte[32], CancellationToken.None));
        using InvocationPayloadLease recovery = session.BorrowSealedSuccessor(CancellationToken.None);
        Read(recovery).ShouldBe([2]);
        Should.Throw<OperationCanceledException>(() => session.BorrowSealedSuccessor(new CancellationToken(true)));
    }

    /// <summary>Checks validation receives the origin token and cannot retain a usable input facade.</summary>
    [Fact]
    public void ValidationFailureAndCancellationExpireInputFacadeAndKeepPrior()
    {
        int calls = 0;
        IReadOnlyPayload? retained = null;
        using var cancellation = new CancellationTokenSource();
        using var session = Session(new byte[] { 1 }, new EventBufferBudget(), operationCancellation: cancellation.Token,
            validate: (payload, token) =>
            {
                token.ShouldBe(cancellation.Token);
                retained = payload;
                if (++calls == 2) { cancellation.Cancel(); }
            });
        Should.Throw<ObjectDisposedException>(() => _ = retained!.Length);
        byte[] handle = session.CreatePriorHandle();
        Should.Throw<OperationCanceledException>(() => session.WriteSuccessorAsync(handle, new byte[] { 2 }, "state.v1", CancellationToken.None));
        Should.Throw<ObjectDisposedException>(() => retained!.CopyTo(0, new byte[1]));
        using InvocationPayloadLease prior = session.BorrowRetainedPrior(CancellationToken.None);
        Read(prior).ShouldBe([1]);
    }

    /// <summary>Checks source slices reserve their complete backing capacity before private capture.</summary>
    [Fact]
    public void CapacityAdmissionMeasuresWholeSourceBackingArrayBeforeCopyOrValidation()
    {
        int validations = 0;
        var budget = new EventBufferBudget(4096);
        using var session = Session(new byte[] { 1 }, budget, validate: (_, _) => validations++);
        int retainedCapacity = budget.LiveBytes;
        byte[] handle = session.CreatePriorHandle();
        byte[] largeBacking = new byte[4096];
        Should.Throw<InvalidOperationException>(() => session.WriteSuccessorAsync(handle, largeBacking.AsMemory(0, 1), "state.v1", CancellationToken.None)).Message.ShouldContain("ScratchLimit");
        validations.ShouldBe(1);
        budget.LiveBytes.ShouldBe(retainedCapacity);
        using InvocationPayloadLease prior = session.BorrowRetainedPrior(CancellationToken.None);
        Read(prior).ShouldBe([1]);
        session.Dispose();
        budget.LiveBytes.ShouldBe(0);
    }

    /// <summary>Checks unknown capacity and constructor validation failure refund private owners.</summary>
    [Fact]
    public void UnknownCapacityRefusesBeforeValidatorAndConstructorFailureRefundsOwners()
    {
        using var opaque = new OpaqueReplayStateMemory();
        int validations = 0;
        var budget = new EventBufferBudget();
        Should.Throw<InvalidOperationException>(() => Session(opaque.Memory, budget, validate: (_, _) => validations++))
            .Message.ShouldContain("ScratchLimit");
        validations.ShouldBe(0);
        budget.LiveBytes.ShouldBe(0);

        Should.Throw<InvalidOperationException>(() => Session(new byte[] { 1 }, budget,
            validate: (_, _) => throw new InvalidOperationException("Invalid canonical state")));
        budget.LiveBytes.ShouldBe(0);
    }

    /// <summary>Checks private timeline entries stay detached and bind the terminal successor.</summary>
    [Fact]
    public async Task TimelineIsContiguousDetachedAndMustEqualTheSealedSuccessor()
    {
        using var session = Session(new byte[] { 0 }, new EventBufferBudget(), timeline: true, pageCount: 2);
        byte[] first = [1];
        byte[] second = [2];
        session.AppendTimelineEntry(1, first);
        first[0] = 9;
        session.AppendTimelineEntry(2, second);
        second[0] = 9;
        byte[] successor = await session.WriteSuccessorAsync(session.CreatePriorHandle(), new byte[] { 2 }, "state.v1", CancellationToken.None);
        session.SealInvocation(successor);
        using InvocationPayloadLease prepared = session.BorrowSealedSuccessor(CancellationToken.None);
        Read(prepared).ShouldBe([2]);

        using var mismatch = Session(new byte[] { 0 }, new EventBufferBudget(), timeline: true);
        mismatch.AppendTimelineEntry(1, new byte[] { 1 });
        Should.Throw<InvalidOperationException>(() => mismatch.WriteSuccessorAsync(mismatch.CreatePriorHandle(), new byte[] { 2 }, "state.v1", CancellationToken.None));
        using InvocationPayloadLease prior = mismatch.BorrowRetainedPrior(CancellationToken.None);
        Read(prior).ShouldBe([0]);
    }

    /// <summary>Checks a swallowed recursive append cannot produce an accepted timeline.</summary>
    [Fact]
    public void ReentrantTimelineValidationRefusesEvenWhenValidatorSwallowsTheException()
    {
        PrivatePagedReplayStateSession? session = null;
        int calls = 0;
        var budget = new EventBufferBudget();
        session = Session(new byte[] { 0 }, budget, timeline: true, validate: (_, _) =>
        {
            if (++calls == 2)
            {
                Should.Throw<InvalidOperationException>(() => session!.AppendTimelineEntry(1, new byte[] { 9 }));
            }
        });
        using (session)
        {
            Should.Throw<InvalidOperationException>(() => session.AppendTimelineEntry(1, new byte[] { 1 }));
            calls.ShouldBe(2);
            Should.Throw<InvalidOperationException>(() => session.CreatePriorHandle());
            using InvocationPayloadLease prior = session.BorrowRetainedPrior(CancellationToken.None);
            Read(prior).ShouldBe([0]);
        }

        budget.LiveBytes.ShouldBe(0);
    }

    /// <summary>Checks timeline mode, count, sequence and cumulative bytes before successor acceptance.</summary>
    [Fact]
    public void TimelineCountModeAndCumulativeStateByteLimitsFailBeforeSuccessorAuthority()
    {
        using var gap = Session(new byte[] { 0 }, new EventBufferBudget(), timeline: true);
        Should.Throw<InvalidOperationException>(() => gap.AppendTimelineEntry(2, new byte[] { 1 }));
        using var stateOnly = Session(new byte[] { 0 }, new EventBufferBudget());
        Should.Throw<InvalidOperationException>(() => stateOnly.AppendTimelineEntry(1, new byte[] { 1 }));
        using var missing = Session(new byte[] { 0 }, new EventBufferBudget(), timeline: true);
        Should.Throw<InvalidOperationException>(() => missing.WriteSuccessorAsync(missing.CreatePriorHandle(), new byte[] { 1 }, "state.v1", CancellationToken.None));
        using var saturated = Session(new byte[] { 0 }, new EventBufferBudget(), timeline: true, committedTimelineStateBytes: 64 * 1024 * 1024);
        Should.Throw<InvalidOperationException>(() => saturated.AppendTimelineEntry(1, new byte[] { 1 })).Message.ShouldContain("TimelineLimit");
        Should.Throw<InvalidOperationException>(() => Scope(expectedHead: 1001, target: 1001, timeline: true)).Message.ShouldContain("TimelineLimit");
    }

    /// <summary>Checks the inclusive lease expiry clears borrowed state without changing the retained prior.</summary>
    [Fact]
    public async Task LeaseExpiryClearsBorrowedCopiesAndRetainsPriorUntilOwnerDisposal()
    {
        var clock = new ManualReplayTimeProvider();
        var budget = new EventBufferBudget();
        using var session = Session(new byte[] { 1 }, budget, timeProvider: clock);
        byte[] handle = session.CreatePriorHandle();
        ReadOnlyMemory<byte> borrowed = await session.ReadPriorAsync(handle, CancellationToken.None);
        clock.Advance(TimeSpan.FromMinutes(15));
        Should.Throw<InvalidOperationException>(() => session.CreatePriorHandle());
        borrowed.ToArray().ShouldBe([0]);
        using InvocationPayloadLease prior = session.BorrowRetainedPrior(CancellationToken.None);
        Read(prior).ShouldBe([1]);
        session.Dispose();
        budget.LiveBytes.ShouldBe(0);
    }

    /// <summary>Checks all approved empty and max-sequence local forms preserve exact prior bytes.</summary>
    [Theory]
    [InlineData(0, 0, 1, true)]
    [InlineData(7, 0, 1, true)]
    [InlineData(7, 7, 8, false)]
    [InlineData(long.MaxValue, long.MaxValue, long.MaxValue, false)]
    public async Task ZeroEventFormsRetainTheExactPriorWithoutSequenceOverflow(long head, long target, long start, bool timeline)
    {
        using var session = new PrivatePagedReplayStateSession(Scope(head, target, start, 0, timeline), "state.v1",
            new byte[] { 1 }, static (_, _) => { }, new EventBufferBudget(), CancellationToken.None);
        byte[] successor = await session.WriteSuccessorAsync(session.CreatePriorHandle(), new byte[] { 1 }, "state.v1", CancellationToken.None);
        session.SealInvocation(successor);
        using InvocationPayloadLease state = session.BorrowSealedSuccessor(CancellationToken.None);
        Read(state).ShouldBe([1]);
    }

    /// <summary>Checks malformed zero-event forms, changed empty successors and sequence overflow refuse.</summary>
    [Fact]
    public void InvalidZeroEventAndTerminalRangesCannotAllocatePrivateOwners()
    {
        Should.Throw<ArgumentException>(() => Scope(8, 7, 8, 0));
        Should.Throw<ArgumentException>(() => Scope(7, 7, 8, 0, true));
        Should.Throw<ArgumentException>(() => Scope(long.MaxValue, long.MaxValue, long.MaxValue, 0, true));
        Should.Throw<ArgumentException>(() => Scope(long.MaxValue, long.MaxValue, long.MaxValue, 2));
        _ = Scope(long.MaxValue, long.MaxValue, long.MaxValue, 1);
        using var empty = new PrivatePagedReplayStateSession(Scope(0, 0, 1, 0), "state.v1",
            new byte[] { 1 }, static (_, _) => { }, new EventBufferBudget(), CancellationToken.None);
        Should.Throw<InvalidOperationException>(() => empty.WriteSuccessorAsync(empty.CreatePriorHandle(), new byte[] { 2 }, "state.v1", CancellationToken.None));
    }

    private static PrivateReplayPageScope Scope(long expectedHead = 1, long target = 1,
        long pageStart = 1, int pageCount = 1, bool timeline = false, string operationId = "operation")
        => new("tenant", "domain", "aggregate-type", "aggregate", operationId, 1,
            expectedHead, target, pageStart, pageCount, timeline);

    private static PrivatePagedReplayStateSession Session(ReadOnlyMemory<byte> initial, EventBufferBudget budget,
        string operationId = "operation", bool timeline = false, int pageCount = 1,
        Action<IReadOnlyPayload, CancellationToken>? validate = null, CancellationToken operationCancellation = default,
        TimeProvider? timeProvider = null, long committedTimelineStateBytes = 0)
        => new(Scope(pageCount, pageCount, 1, pageCount, timeline, operationId), "state.v1", initial,
            validate ?? (static (_, _) => { }), budget, operationCancellation, committedTimelineStateBytes, timeProvider);

    private static byte[] Read(IReadOnlyPayload payload)
    {
        byte[] copy = new byte[payload.Length];
        payload.CopyTo(0, copy);
        return copy;
    }
}
