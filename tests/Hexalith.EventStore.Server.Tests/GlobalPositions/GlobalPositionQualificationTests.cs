using System.Text;

using Hexalith.EventStore.Testing.Fakes;

using Shouldly;

namespace Hexalith.EventStore.Server.Tests.GlobalPositions;

/// <summary>Development-only checks for decoded position and reservation mechanics.</summary>
public sealed class GlobalPositionQualificationTests
{
    /// <summary>Tuple keys preserve boundaries, case, whitespace, and Unicode spelling.</summary>
    /// <param name="tenant">The first exact tenant.</param>
    /// <param name="domain">The first exact domain.</param>
    /// <param name="otherTenant">The second exact tenant.</param>
    /// <param name="otherDomain">The second exact domain.</param>
    [Theory]
    [InlineData("a:b", "c", "a", "b:c")]
    [InlineData("Tenant", "orders", "tenant", "orders")]
    [InlineData("tenant", "Orders", "tenant", "orders")]
    [InlineData("tenant ", "orders", "tenant", "orders")]
    [InlineData("\u00e9", "orders", "e\u0301", "orders")]
    [InlineData("tenant", "\u00e9", "tenant", "e\u0301")]
    public void ShardKey_PreservesExactTuple(string tenant, string domain, string otherTenant, string otherDomain)
    {
        var first = new QualificationShard(tenant, domain);
        var second = new QualificationShard(otherTenant, otherDomain);

        first.ShouldNotBe(second);
        first.StateKey().ShouldNotBe(second.StateKey());
        new QualificationShard(tenant, domain).StateKey().ShouldBe(first.StateKey());
    }

    /// <summary>Default and empty shards cannot create an allocation key.</summary>
    [Fact]
    public void ShardKey_RejectsMissingIdentity()
    {
        Should.Throw<InvalidOperationException>(() => default(QualificationShard).StateKey());
        Should.Throw<ArgumentException>(() => new QualificationShard(string.Empty, "orders"));
        Should.Throw<ArgumentException>(() => new QualificationShard("tenant", string.Empty));
    }

    /// <summary>Malformed Unicode never aliases a replacement character in a state key.</summary>
    [Fact]
    public void ShardKey_RejectsInvalidUtf8Source()
    {
        string invalid = new((char)0xD800, 1);
        Should.Throw<EncoderFallbackException>(() => new QualificationShard(invalid, "orders"));
        Should.Throw<EncoderFallbackException>(() => new QualificationShard("tenant", invalid));
    }

    /// <summary>Valid legacy comparisons preserve zero's unknown meaning.</summary>
    /// <param name="left">The left scalar.</param>
    /// <param name="right">The right scalar.</param>
    /// <param name="expected">The expected bounded outcome name.</param>
    [Theory]
    [InlineData(1L, 2L, "Less")]
    [InlineData(2L, 1L, "Greater")]
    [InlineData(2L, 2L, "Equal")]
    [InlineData(0L, 2L, "UnknownPosition")]
    [InlineData(2L, 0L, "UnknownPosition")]
    [InlineData(0L, 0L, "UnknownPosition")]
    [InlineData(-1L, 0L, "InvalidPosition")]
    public void Compare_LegacyValues(long left, long right, string expected)
    {
        QualificationPositionComparer.Compare(new(1, left), new(1, right)).ToString().ShouldBe(expected);
    }

    /// <summary>Invalid values beat unsupported values, which beat unknown zero.</summary>
    [Fact]
    public void Compare_EnforcesInvalidAndUnsupportedPrecedence()
    {
        var shard = new QualificationShard("tenant", "orders");
        var zero = new QualificationPosition(1, 0);
        var valid = new QualificationPosition(2, 0, shard, 1, 1);
        var unknownOuter = new QualificationPosition(3, 0);
        QualificationPosition unknownCanonicalization = valid with { CanonicalizationVersion = 2 };
        QualificationPosition unknownScheme = valid with { Scheme = "future-scheme" };

        AssertBoth(zero, valid, QualificationComparison.UnsupportedCrossScheme);
        AssertBoth(zero, unknownOuter, QualificationComparison.UnsupportedScheme);
        AssertBoth(zero, unknownCanonicalization, QualificationComparison.UnsupportedScheme);
        AssertBoth(zero, unknownScheme, QualificationComparison.UnsupportedScheme);
        AssertBoth(valid, unknownScheme, QualificationComparison.UnsupportedScheme);
        AssertBoth(valid with { Counter = 0 }, unknownScheme, QualificationComparison.InvalidPosition);
        AssertBoth(valid with { Scheme = string.Empty }, unknownOuter, QualificationComparison.InvalidPosition);
        AssertBoth(new QualificationPosition(1, -1), unknownOuter, QualificationComparison.InvalidPosition);
        AssertBoth(valid with { Counter = 0 }, unknownOuter, QualificationComparison.InvalidPosition);
        AssertBoth(valid with { Generation = 0 }, unknownCanonicalization, QualificationComparison.InvalidPosition);
        AssertBoth(valid with { Shard = default }, zero, QualificationComparison.InvalidPosition);
    }

    /// <summary>Only equal shard and generation pairs are comparable.</summary>
    [Fact]
    public void Compare_ShardAndGenerationBoundariesHaveNoScalarFallback()
    {
        var first = new QualificationPosition(2, 0, new("tenant", "orders"), 1, 42);
        AssertBoth(first, first with { Shard = new("other", "orders") }, QualificationComparison.UnsupportedCrossShard);
        AssertBoth(first, first with { Shard = new("tenant", "billing") }, QualificationComparison.UnsupportedCrossShard);
        AssertBoth(first, first with { Generation = 2 }, QualificationComparison.UnsupportedCrossGeneration);
        AssertBoth(first, first with { Shard = new("other", "orders"), Generation = 2 }, QualificationComparison.UnsupportedCrossShard);
        AssertBoth(first, first with { Shard = new("other", "orders"), Counter = 1 }, QualificationComparison.UnsupportedCrossShard);
        AssertBoth(first, first with { Shard = new("other", "orders"), Counter = 100 }, QualificationComparison.UnsupportedCrossShard);
        AssertBoth(first, first with { Generation = 2, Counter = 1 }, QualificationComparison.UnsupportedCrossGeneration);
        AssertBoth(first, first with { Generation = 2, Counter = 100 }, QualificationComparison.UnsupportedCrossGeneration);
        QualificationPositionComparer.Compare(first, first with { Counter = 43 }).ShouldBe(QualificationComparison.Less);
        QualificationPositionComparer.Compare(first, first with { Counter = 41 }).ShouldBe(QualificationComparison.Greater);
        QualificationPositionComparer.Compare(first, first with { }).ShouldBe(QualificationComparison.Equal);
    }

    /// <summary>Initial admission is required before any counter can be granted.</summary>
    [Fact]
    public async Task Reserve_UnadmittedShard_FailsWithoutStateMutation()
    {
        var state = new InMemoryStateManager();
        var candidate = new QualificationAllocator(state);

        await Should.ThrowAsync<InvalidOperationException>(() => candidate.ReserveAsync(new("tenant", "orders"), 1));
        await state.SaveStateAsync();

        state.CommittedState.ShouldBeEmpty();
    }

    /// <summary>Reservations persist their ceiling, keep gaps, and survive a new fixture instance.</summary>
    [Fact]
    public async Task Reserve_PersistsCeilingAndContinuesAfterRestart()
    {
        var state = new InMemoryStateManager();
        var shard = new QualificationShard("tenant", "orders");
        var candidate = new QualificationAllocator(state);
        await candidate.AdmitAsync(shard, 7);

        QualificationPosition first = await candidate.ReserveAsync(shard, 3);
        first.ShouldBe(new QualificationPosition(2, 0, shard, 7, 1));
        state.CommittedState[shard.StateKey()].ShouldBe(new QualificationAllocationState(shard, 7, 3));

        // No event commit is simulated: those reserved counters remain an allowed gap.
        await state.ClearCacheAsync();
        var restarted = new QualificationAllocator(state);
        await restarted.AdmitAsync(shard, 7);
        QualificationPosition next = await restarted.ReserveAsync(shard, 2);

        next.Counter.ShouldBe(4);
        next.ShouldBe(new QualificationPosition(2, 0, shard, 7, 4));
        state.CommittedState[shard.StateKey()].ShouldBe(new QualificationAllocationState(shard, 7, 5));
    }

    /// <summary>Each exact tenant/domain pair owns an independent fixture counter.</summary>
    [Fact]
    public async Task Reserve_IsolatesPairsAndDoesNotResetReadmission()
    {
        var state = new InMemoryStateManager();
        var candidate = new QualificationAllocator(state);
        var first = new QualificationShard("a:b", "c");
        var second = new QualificationShard("a", "b:c");
        await candidate.AdmitAsync(first, 1);
        await candidate.AdmitAsync(second, 1);

        (await candidate.ReserveAsync(first, 5)).ShouldBe(new QualificationPosition(2, 0, first, 1, 1));
        (await candidate.ReserveAsync(second, 1)).ShouldBe(new QualificationPosition(2, 0, second, 1, 1));
        await candidate.AdmitAsync(first, 1);
        (await candidate.ReserveAsync(first, 1)).ShouldBe(new QualificationPosition(2, 0, first, 1, 6));

        state.CommittedState[first.StateKey()].ShouldBe(new QualificationAllocationState(first, 1, 6));
        state.CommittedState[second.StateKey()].ShouldBe(new QualificationAllocationState(second, 1, 1));
    }

    /// <summary>A later admission cannot replace an existing generation.</summary>
    [Fact]
    public async Task Admit_RejectsGenerationReplacementWithoutReset()
    {
        var state = new InMemoryStateManager();
        var candidate = new QualificationAllocator(state);
        var shard = new QualificationShard("tenant", "orders");
        await candidate.AdmitAsync(shard, 1);
        _ = await candidate.ReserveAsync(shard, 2);

        await Should.ThrowAsync<InvalidOperationException>(() => candidate.AdmitAsync(shard, 2));
        await state.SaveStateAsync();

        state.CommittedState[shard.StateKey()].ShouldBe(new QualificationAllocationState(shard, 1, 2));
    }

    /// <summary>Overflow and invalid counts fail before staging or saving state.</summary>
    /// <param name="ceiling">The existing persisted ceiling.</param>
    /// <param name="count">The requested reservation count.</param>
    [Theory]
    [InlineData(long.MaxValue, 1)]
    [InlineData(long.MaxValue - 1, 2)]
    public async Task Reserve_OverflowPreservesPersistedCeiling(long ceiling, int count)
    {
        var state = new InMemoryStateManager();
        var shard = new QualificationShard("tenant", "orders");
        var previous = new QualificationAllocationState(shard, 1, ceiling);
        await state.SetStateAsync(shard.StateKey(), previous);
        await state.SaveStateAsync();
        var candidate = new QualificationAllocator(state);

        await Should.ThrowAsync<OverflowException>(() => candidate.ReserveAsync(shard, count));
        await state.SaveStateAsync();

        state.CommittedState[shard.StateKey()].ShouldBe(previous);
    }

    /// <summary>The final valid Int64 counter remains available before overflow.</summary>
    [Fact]
    public async Task Reserve_FinalCounterSucceedsBeforeOverflow()
    {
        var state = new InMemoryStateManager();
        var shard = new QualificationShard("tenant", "orders");
        await state.SetStateAsync(shard.StateKey(), new QualificationAllocationState(shard, 1, long.MaxValue - 1));
        await state.SaveStateAsync();
        var candidate = new QualificationAllocator(state);

        (await candidate.ReserveAsync(shard, 1)).ShouldBe(new QualificationPosition(2, 0, shard, 1, long.MaxValue));
        await Should.ThrowAsync<OverflowException>(() => candidate.ReserveAsync(shard, 1));
        await state.SaveStateAsync();

        state.CommittedState[shard.StateKey()].ShouldBe(new QualificationAllocationState(shard, 1, long.MaxValue));
    }

    /// <summary>Nonpositive admission and reservation sizes cannot create or alter state.</summary>
    /// <param name="value">The invalid count and generation.</param>
    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(int.MinValue)]
    public async Task InvalidCountsAndGenerationsDoNotMutateState(int value)
    {
        var state = new InMemoryStateManager();
        var candidate = new QualificationAllocator(state);
        var shard = new QualificationShard("tenant", "orders");
        await Should.ThrowAsync<ArgumentOutOfRangeException>(() => candidate.AdmitAsync(shard, value));
        await state.SaveStateAsync();
        state.CommittedState.ShouldBeEmpty();
        await candidate.AdmitAsync(shard, 1);
        await Should.ThrowAsync<ArgumentOutOfRangeException>(() => candidate.ReserveAsync(shard, value));
        await state.SaveStateAsync();
        state.CommittedState[shard.StateKey()].ShouldBe(new QualificationAllocationState(shard, 1, 0));
    }

    /// <summary>Corrupt state is never treated as a new shard or an allocation ceiling.</summary>
    /// <param name="generation">The persisted generation.</param>
    /// <param name="ceiling">The persisted ceiling.</param>
    [Theory]
    [InlineData(0L, 1L)]
    [InlineData(1L, -1L)]
    public async Task Reserve_RejectsCorruptState(long generation, long ceiling)
    {
        var state = new InMemoryStateManager();
        var shard = new QualificationShard("tenant", "orders");
        var corrupt = new QualificationAllocationState(shard, generation, ceiling);
        await state.SetStateAsync(shard.StateKey(), corrupt);
        await state.SaveStateAsync();
        var candidate = new QualificationAllocator(state);

        await Should.ThrowAsync<InvalidOperationException>(() => candidate.ReserveAsync(shard, 1));
        await state.SaveStateAsync();
        state.CommittedState[shard.StateKey()].ShouldBe(corrupt);
        await Should.ThrowAsync<InvalidOperationException>(() => candidate.AdmitAsync(shard, 1));
        await state.SaveStateAsync();

        state.CommittedState[shard.StateKey()].ShouldBe(corrupt);
    }

    /// <summary>A stored routing copy cannot redirect a fixture allocation.</summary>
    [Fact]
    public async Task Reserve_RejectsMismatchedStoredShard()
    {
        var state = new InMemoryStateManager();
        var shard = new QualificationShard("tenant", "orders");
        var corrupt = new QualificationAllocationState(new("other", "orders"), 1, 10);
        await state.SetStateAsync(shard.StateKey(), corrupt);
        await state.SaveStateAsync();
        var candidate = new QualificationAllocator(state);

        await Should.ThrowAsync<InvalidOperationException>(() => candidate.ReserveAsync(shard, 1));
        await state.SaveStateAsync();

        state.CommittedState[shard.StateKey()].ShouldBe(corrupt);
    }

    /// <summary>Synthetic serial traffic preserves every shard's complete identity and ceiling.</summary>
    [Fact]
    public async Task Reserve_SyntheticWorkloadPreservesEachPersistedPartition()
    {
        var state = new InMemoryStateManager();
        QualificationShard[] shards = Enumerable.Range(0, 32)
            .Select(index => new QualificationShard($"tenant:{index}", $"domain:{index % 4}"))
            .ToArray();
        var candidate = new QualificationAllocator(state);
        foreach (QualificationShard shard in shards)
        {
            await candidate.AdmitAsync(shard, 3);
        }

        long expectedCeiling = 0;
        for (int batch = 0; batch < 20; batch++)
        {
            int count = (batch % 7) + 1;
            if (batch % 5 == 0)
            {
                await state.ClearCacheAsync();
                candidate = new QualificationAllocator(state);
            }

            foreach (QualificationShard shard in shards)
            {
                (await candidate.ReserveAsync(shard, count)).ShouldBe(
                    new QualificationPosition(2, 0, shard, 3, expectedCeiling + 1));
            }

            expectedCeiling += count;
        }

        state.CommittedState.Count.ShouldBe(shards.Length);
        foreach (QualificationShard shard in shards)
        {
            state.CommittedState[shard.StateKey()].ShouldBe(new QualificationAllocationState(shard, 3, expectedCeiling));
        }
    }

    private static void AssertBoth(QualificationPosition first, QualificationPosition second, QualificationComparison expected)
    {
        QualificationPositionComparer.Compare(first, second).ShouldBe(expected);
        QualificationPositionComparer.Compare(second, first).ShouldBe(expected);
    }
}
