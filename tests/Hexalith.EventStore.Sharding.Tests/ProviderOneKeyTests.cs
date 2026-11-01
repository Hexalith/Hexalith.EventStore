using System.Globalization;
using System.Text.Json;

using Hexalith.EventStore.Sharding.TestHost;

using Shouldly;

namespace Hexalith.EventStore.Sharding.Tests;

/// <summary>Ordinary one-key implicit saves at actual execution and committed-idle boundaries across recovery.</summary>
[Collection("ShardingLive")]
public sealed class ProviderOneKeyTests
{
    /// <summary>Inspects complete authority before resuming after reservation and admission ambiguity.</summary>
    [Fact]
    public async Task OrdinaryOneKeyBoundariesPreserveFullAuthorityAcrossRecoveryAsync()
    {
        await using var fixture = new ShardingFixture();
        await fixture.InitializeAsync(providerOneKey: true);
        RunBinding? binding = JsonSerializer.Deserialize<RunBinding>(await File.ReadAllTextAsync(Path.Combine(fixture.Artifacts, "binding.json")));
        binding.ShouldNotBeNull();
        binding.Configuration["authorization"].ShouldContain("spec-4-6-one-key-provider-boundaries.md");
        binding.Configuration["authorization"].ShouldBe(ShardingFixture.AuthorizationFor(providerCommitProxy: true, providerOneKey: true));
        var acknowledged = new HashSet<string>(StringComparer.Ordinal);
        var states = new List<ShardState>();
        var absent = new List<(string Tenant, string Domain, long Generation)>();
        (string Tenant, string Domain)[] pairs =
        [
            ("shared", "orders"), ("shared", "billing"), ("other", "orders"),
            ("a|b", "c"), ("a", "b|c"), ("A", "b|c"), ("é", "unicode"), ("e\u0301", "unicode"),
        ];
        for (int index = 0; index < pairs.Length; index++)
        {
            (string tenant, string domain) = pairs[index];
            var initial = new ShardState(tenant, domain, long.MaxValue - index, 0);
            (await fixture.ApplyAsync(index % 2, new("Admit", tenant, domain, initial.Generation))).State.ShouldBe(initial);
            if (index < 2)
            {
                initial = initial with { Ceiling = 9_007_199_254_740_993L + index }; // Both faulted pairs exceed 2^53 before first allocation.
                (await fixture.ApplyAsync(index % 2, new("Seed", tenant, domain, initial.Generation, SeedCeiling: initial.Ceiling))).State.ShouldBe(initial);
                await AssertStateAsync(fixture, initial);
            }
            states.Add(await ReserveAsync(fixture, initial, index + 1, acknowledged, index % 2));
        }

        var bound = new ShardState("bound", "Int64", long.MaxValue, long.MaxValue - 2);
        (await fixture.ApplyAsync(0, new("Admit", bound.TenantId, bound.Domain, bound.Generation))).Accepted.ShouldBeTrue();
        (await fixture.ApplyAsync(1, new("Seed", bound.TenantId, bound.Domain, bound.Generation, SeedCeiling: bound.Ceiling))).State.ShouldBe(bound);
        await AssertAllAsync(fixture, states.Append(bound), absent);

        for (int cycle = 0; cycle < 2; cycle++)
        {
            int[] original = fixture.ProcessIds;
            original.Length.ShouldBe(4);
            original.Distinct().Count().ShouldBe(4);
            foreach (PostgresOneKeyBoundary boundary in Enum.GetValues<PostgresOneKeyBoundary>())
            {
                int faultedPair = boundary == PostgresOneKeyBoundary.BeforeExecution ? 0 : 1;
                ShardState previous = states[faultedPair];
                int count = boundary == PostgresOneKeyBoundary.BeforeExecution ? 3 : 5;
                ShardState expected = boundary == PostgresOneKeyBoundary.BeforeExecution ? previous : previous with { Ceiling = previous.Ceiling + count };
                await FaultAsync(fixture, new("Reserve", previous.TenantId, previous.Domain, previous.Generation, count,
                    OperationId: $"cycle-{cycle}-reserve-{boundary}"), boundary, expected);
                states[faultedPair] = expected; // Preserve this complete row through both recovery and replacement snapshots.

                string tenant = $"admission-{cycle}-{boundary}";
                long generation = long.MaxValue - 100 - cycle;
                ShardState? admission = boundary == PostgresOneKeyBoundary.BeforeExecution ? null : new(tenant, "orders", generation, 0);
                await FaultAsync(fixture, new("Admit", tenant, "orders", generation,
                    OperationId: $"cycle-{cycle}-admit-{boundary}"), boundary, admission);
                if (admission is null)
                {
                    absent.Add((tenant, "orders", generation));
                    await DenyAsync(fixture, new("Reserve", tenant, "orders", generation, 1), "not-admitted");
                }
                else
                {
                    states.Add(admission);
                    await DenyAsync(fixture, new("Admit", tenant, "orders", generation - 1), "generation-conflict");
                }
            }

            // Inspect nontransactional counters before recovery can alter sequence persistence.
            await AssertAllAsync(fixture, states.Append(bound), absent);
            Dictionary<string, string> snapshot = await fixture.AllProviderRowsAsync();
            await fixture.StopProviderAsync(abrupt: cycle == 1);
            await fixture.RecoverProviderAsync();
            fixture.ProcessIds.ShouldBe(original);
            await AssertAllAsync(fixture, states.Append(bound), absent);
            (await fixture.AllProviderRowsAsync()).ShouldBe(snapshot);
            foreach (ShardState state in states.Take(2))
                await DenyAsync(fixture, new("Reserve", state.TenantId, state.Domain, state.Generation - 1, 1), "generation-conflict");
            int[] replacement = await fixture.RestartAsync();
            replacement.Length.ShouldBe(4);
            replacement.Distinct().Count().ShouldBe(4);
            replacement.Intersect(original).ShouldBeEmpty();
            await AssertAllAsync(fixture, states.Append(bound), absent);
            (await fixture.AllProviderRowsAsync()).ShouldBe(snapshot);
            foreach (ShardState state in states.Take(2))
                await DenyAsync(fixture, new("Reserve", state.TenantId, state.Domain, state.Generation - 1, 1), "generation-conflict");
            for (int index = 0; index < states.Count; index++)
            {
                ShardState state = states[index];
                (await fixture.ApplyAsync(index % 2, new("Inspect", state.TenantId, state.Domain, state.Generation))).State.ShouldBe(state);
                states[index] = await ReserveAsync(fixture, state, 1, acknowledged, index % 2);
            }

            foreach ((string tenant, string domain, long generation) in absent)
                await DenyAsync(fixture, new("Reserve", tenant, domain, generation, 1), "not-admitted");
            await AssertAllAsync(fixture, states.Append(bound), absent);
        }

        bound = await ReserveAsync(fixture, bound, 2, acknowledged, 1);
        bound.Ceiling.ShouldBe(long.MaxValue);
        await DenyAsync(fixture, new("Reserve", bound.TenantId, bound.Domain, bound.Generation, 1), "overflow");
        await AssertAllAsync(fixture, states.Append(bound), absent);
        acknowledged.Count.ShouldBe(57); // 36 initial + 9 and 10 only after each recovery/replacement + final two; ten committed fault counters remain gaps.
        await fixture.AssertOneKeyAttemptsAsync();
        await fixture.DisposeAsync();
        fixture.CleanupVerified.ShouldBeTrue();
        fixture.ProxyStopped.ShouldBeTrue();
        Console.WriteLine("Retained ordinary one-key provider artifacts: " + fixture.Artifacts);
    }

    private static async Task FaultAsync(ShardingFixture fixture, ShardRequest request, PostgresOneKeyBoundary boundary, ShardState? expected)
    {
        ShardState? previous = await fixture.StateAsync(request.TenantId, request.Domain);
        Dictionary<string, string> previousRows = await fixture.AllProviderRowsAsync();
        string? previousWitness = await fixture.DurableWitnessAsync(request.TenantId, request.Domain);
        previousWitness.ShouldBeNull();
        if (request.Operation == "Admit")
        {
            previous.ShouldBeNull();
            previousWitness.ShouldBeNull();
        }
        else
        {
            previous.ShouldNotBeNull();
            await AssertStateAsync(fixture, previous);
        }
        ProviderOneKeyInstrumentation instrumentation = await fixture.ArmOneKeyFaultAsync(request, boundary);
        int sends = 0;
        Task<ShardReply> pending = fixture.ApplyAsync(0, request, observeTransport: (count, _) => sends = count);
        Exception? requestError = null;
        await FaultExperiment.RunAsync(async () =>
        {
            await fixture.AwaitOneKeyFaultAsync(instrumentation);
            pending.IsCompleted.ShouldBeFalse();
            // Read the full provider state while execution or successful acknowledgement is withheld.
            (await fixture.StateAsync(request.TenantId, request.Domain)).ShouldBe(expected);
            if (expected is not null) await AssertStateAsync(fixture, expected);
            await AssertFaultRowsAsync(fixture, request, expected, previousRows);
        }, () =>
        {
            if (instrumentation.Fault.Reached) instrumentation.Fault.BreakConnection();
            return Task.CompletedTask;
        }, async () =>
        {
            requestError = await Record.ExceptionAsync(() => pending.WaitAsync(TimeSpan.FromSeconds(25)));
            fixture.RecordFaultRequestOutcome(instrumentation.Request.OperationId!, pending.IsCompleted, requestError, sends);
        }, () =>
        {
            sends.ShouldBe(1); // Independent from request assertions and before instrumentation removal.
            return Task.CompletedTask;
        }, () =>
        {
            pending.IsCompleted.ShouldBeTrue();
            requestError.ShouldNotBeNull();
            return Task.CompletedTask;
        }, () => fixture.CompleteOneKeyFaultAsync(instrumentation), async () =>
        {
            (await fixture.StateAsync(request.TenantId, request.Domain)).ShouldBe(expected);
            if (expected is not null) await AssertStateAsync(fixture, expected);
        }, () => AssertFaultRowsAsync(fixture, request, expected, previousRows));
    }

    private static async Task AssertFaultRowsAsync(ShardingFixture fixture, ShardRequest request,
        ShardState? expected, Dictionary<string, string> previousRows)
    {
        Dictionary<string, string> rows = await fixture.AllProviderRowsAsync();
        string key = fixture.ProviderStateKey(request.TenantId, request.Domain);
        string[] expectedKeys = expected is not null ? previousRows.Keys.Append(key).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray()
            : previousRows.Keys.Order(StringComparer.Ordinal).ToArray();
        rows.Keys.Order(StringComparer.Ordinal).ShouldBe(expectedKeys);
        foreach ((string unchanged, string value) in previousRows.Where(row => row.Key != key)) rows[unchanged].ShouldBe(value);
        if (expected is null) rows.ContainsKey(key).ShouldBeFalse();
        else
        {
            using JsonDocument complete = JsonDocument.Parse(rows[key]);
            OneKeyAttemptValidator.ReadExactState(complete.RootElement.GetProperty("value").GetRawText()).ShouldBe(expected);
            if (request.Operation == "Reserve" && expected == ReadProviderState(previousRows[key])) rows[key].ShouldBe(previousRows[key]);
        }
        (await fixture.DurableWitnessAsync(request.TenantId, request.Domain)).ShouldBeNull();
    }

    private static ShardState ReadProviderState(string row)
    {
        using JsonDocument complete = JsonDocument.Parse(row);
        return OneKeyAttemptValidator.ReadExactState(complete.RootElement.GetProperty("value").GetRawText());
    }

    private static async Task<ShardState> ReserveAsync(ShardingFixture fixture, ShardState previous, int count, HashSet<string> acknowledged, int node)
    {
        int sends = 0;
        ShardReply reply = await fixture.ApplyAsync(node, new("Reserve", previous.TenantId, previous.Domain, previous.Generation, count),
            observeTransport: (actual, _) => sends = actual);
        sends.ShouldBe(1);
        reply.Accepted.ShouldBeTrue();
        reply.Code.ShouldBe("saved");
        reply.First.ShouldBe(checked(previous.Ceiling + 1));
        reply.Count.ShouldBe(count);
        ShardState next = previous with { Ceiling = checked(previous.Ceiling + count) };
        reply.State.ShouldBe(next);
        for (int index = 0; index < count; index++)
            acknowledged.Add(ShardIdentity.Encode(previous.TenantId, previous.Domain) + ":" + previous.Generation.ToString(CultureInfo.InvariantCulture)
                + ":" + (reply.First + index).ToString(CultureInfo.InvariantCulture)).ShouldBeTrue();
        await AssertStateAsync(fixture, next);
        return next;
    }

    private static async Task AssertAllAsync(ShardingFixture fixture, IEnumerable<ShardState> states, IEnumerable<(string Tenant, string Domain, long Generation)> absent)
    {
        ShardState[] complete = states.ToArray();
        Dictionary<string, string> rows = await fixture.AllStateRowsAsync();
        rows.Keys.Order(StringComparer.Ordinal).ShouldBe(complete.Select(state => fixture.ProviderStateKey(state.TenantId, state.Domain)).Order(StringComparer.Ordinal));
        foreach (ShardState state in complete)
        {
            await AssertStateAsync(fixture, state);
            OneKeyAttemptValidator.ReadExactState(rows[fixture.ProviderStateKey(state.TenantId, state.Domain)]).ShouldBe(state);
        }
        foreach ((string tenant, string domain, _) in absent)
        {
            (await fixture.DurableAsync(tenant, domain)).ShouldBeNull();
            (await fixture.DurableWitnessAsync(tenant, domain)).ShouldBeNull();
        }
    }

    private static async Task AssertStateAsync(ShardingFixture fixture, ShardState state)
    {
        (await fixture.StateAsync(state.TenantId, state.Domain)).ShouldBe(state);
        OneKeyAttemptValidator.ReadExactState((await fixture.DurableAsync(state.TenantId, state.Domain))!).ShouldBe(state);
        (await fixture.DurableWitnessAsync(state.TenantId, state.Domain)).ShouldBeNull();
    }

    private static async Task DenyAsync(ShardingFixture fixture, ShardRequest request, string code)
    {
        Dictionary<string, string> allBefore = await fixture.AllProviderRowsAsync();
        string? before = await fixture.DurableAsync(request.TenantId, request.Domain);
        string? witnessBefore = await fixture.DurableWitnessAsync(request.TenantId, request.Domain);
        ShardReply reply = await fixture.ApplyAsync(1, request);
        reply.Accepted.ShouldBeFalse();
        reply.Code.ShouldBe(code);
        (await fixture.DurableAsync(request.TenantId, request.Domain)).ShouldBe(before);
        (await fixture.DurableWitnessAsync(request.TenantId, request.Domain)).ShouldBe(witnessBefore);
        (await fixture.AllProviderRowsAsync()).ShouldBe(allBefore);
    }

}
