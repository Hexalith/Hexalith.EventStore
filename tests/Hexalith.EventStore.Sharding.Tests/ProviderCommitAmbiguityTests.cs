using System.Globalization;
using System.Text.Json;

using Hexalith.EventStore.Sharding.TestHost;

using Shouldly;

namespace Hexalith.EventStore.Sharding.Tests;

/// <summary>Two repeated same-storage recovery cycles at the actual PostgreSQL COMMIT boundaries.</summary>
[Collection("ShardingLive")]
public sealed class ProviderCommitAmbiguityTests
{
    private static readonly JsonSerializerOptions ReadOptions = new() { PropertyNameCaseInsensitive = true };

    /// <summary>A startup failure after listener creation drains the owned proxy as well as storage.</summary>
    /// <param name="after">Listener-only or actual node/provider-session startup.</param>
    [Theory]
    [InlineData("proxy")]
    [InlineData("node")]
    public async Task ProxyStartupFailureVerifiesOwnedCleanupAsync(string after)
    {
        await using var fixture = new ShardingFixture();
        InvalidOperationException error = await Should.ThrowAsync<InvalidOperationException>(
            () => fixture.InitializeAsync(after, providerCommitProxy: true));
        error.Message.ShouldContain("Injected partial-startup failure after " + after);
        fixture.CleanupVerified.ShouldBeTrue();
        fixture.ProxyStopped.ShouldBeTrue();
        if (after == "node") fixture.ProxyAcceptedSessions.ShouldBeGreaterThan(0);
        fixture.ProxyDrainedSessions.ShouldBe(fixture.ProxyAcceptedSessions);
        Console.WriteLine("Retained proxy startup-failure artifacts: " + fixture.Artifacts);
    }

    /// <summary>Inspects complete authority before resuming after reservation and admission ambiguity.</summary>
    [Fact]
    public async Task RepeatedCommitFaultsPreserveExactAuthorityAndExposeEveryExecutionAsync()
    {
        await using var fixture = new ShardingFixture();
        await fixture.InitializeAsync(providerCommitProxy: true);
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
            foreach (PostgresCommitBoundary boundary in Enum.GetValues<PostgresCommitBoundary>())
            {
                ShardState previous = states[0];
                int count = boundary == PostgresCommitBoundary.BeforeCommit ? 3 : 5;
                ShardState expected = boundary == PostgresCommitBoundary.BeforeCommit ? previous : previous with { Ceiling = previous.Ceiling + count };
                await FaultAsync(fixture, new("Reserve", previous.TenantId, previous.Domain, previous.Generation, count,
                    OperationId: $"cycle-{cycle}-reserve-{boundary}"), boundary, expected);
                states[0] = await ReserveAsync(fixture, expected, boundary == PostgresCommitBoundary.BeforeCommit ? 2 : 1, acknowledged, 1);

                string tenant = $"admission-{cycle}-{boundary}";
                long generation = long.MaxValue - 100 - cycle;
                ShardState? admission = boundary == PostgresCommitBoundary.BeforeCommit ? null : new(tenant, "orders", generation, 0);
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

            // Every per-fault nontransactional execution count was inspected and retained
            // before this restart, so a PostgreSQL crash cannot erase retry evidence.
            await AssertAllAsync(fixture, states.Append(bound), absent);
            Dictionary<(string, string), string?> witnesses = await WitnessSnapshotAsync(fixture, states.Append(bound));
            await fixture.StopProviderAsync(abrupt: false);
            await fixture.RecoverProviderAsync();
            fixture.ProcessIds.ShouldBe(original);
            await AssertAllAsync(fixture, states.Append(bound), absent);
            (await WitnessSnapshotAsync(fixture, states.Append(bound))).ShouldBe(witnesses);
            int[] replacement = await fixture.RestartAsync();
            replacement.Length.ShouldBe(4);
            replacement.Distinct().Count().ShouldBe(4);
            replacement.Intersect(original).ShouldBeEmpty();
            await AssertAllAsync(fixture, states.Append(bound), absent);
            (await WitnessSnapshotAsync(fixture, states.Append(bound))).ShouldBe(witnesses);
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
        acknowledged.Count.ShouldBe(63); // 36 initial + (3 + 9) + (3 + 10) across cycles + final two.
        await fixture.AssertOperationAttemptsAsync();
        await fixture.DisposeAsync();
        fixture.CleanupVerified.ShouldBeTrue();
        fixture.ProxyStopped.ShouldBeTrue();
        Console.WriteLine("Retained provider commit-ambiguity artifacts: " + fixture.Artifacts);
    }

    private static async Task FaultAsync(ShardingFixture fixture, ShardRequest request, PostgresCommitBoundary boundary, ShardState? expected)
    {
        ShardState? previous = await fixture.StateAsync(request.TenantId, request.Domain);
        string? previousWitness = await fixture.DurableWitnessAsync(request.TenantId, request.Domain);
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
        ProviderFaultInstrumentation instrumentation = await fixture.ArmCommitFaultAsync(request, boundary);
        int sends = 0;
        Task<ShardReply> pending = fixture.ApplyAsync(0, request, observeTransport: (count, _) => sends = count);
        Exception? requestError = null;
        await FaultExperiment.RunAsync(async () =>
        {
            await fixture.AwaitCommitFaultAsync(instrumentation);
            pending.IsCompleted.ShouldBeFalse();
            // This direct read occurs while COMMIT or its acknowledgement is withheld.
            (await fixture.StateAsync(request.TenantId, request.Domain)).ShouldBe(expected);
            if (expected is not null) await AssertStateAsync(fixture, expected);
            await AssertFaultWitnessAsync(fixture, request, boundary, expected, previousWitness);
        }, () =>
        {
            if (instrumentation.Fault.Reached) instrumentation.Fault.BreakConnection();
            return Task.CompletedTask;
        }, async () =>
        {
            requestError = await Record.ExceptionAsync(() => pending.WaitAsync(TimeSpan.FromSeconds(25)));
            fixture.RecordFaultRequestOutcome(instrumentation.OperationId, pending.IsCompleted, requestError, sends);
        }, () =>
        {
            sends.ShouldBe(1); // Independent from request assertions and before instrumentation removal.
            return Task.CompletedTask;
        }, () =>
        {
            pending.IsCompleted.ShouldBeTrue();
            requestError.ShouldNotBeNull();
            return Task.CompletedTask;
        }, () => fixture.CompleteCommitFaultAsync(instrumentation), async () =>
        {
            (await fixture.StateAsync(request.TenantId, request.Domain)).ShouldBe(expected);
            if (expected is not null) await AssertStateAsync(fixture, expected);
        }, () => AssertFaultWitnessAsync(fixture, request, boundary, expected, previousWitness));
    }

    private static async Task AssertFaultWitnessAsync(ShardingFixture fixture, ShardRequest request, PostgresCommitBoundary boundary,
        ShardState? expected, string? previous)
    {
        string? json = await fixture.DurableWitnessAsync(request.TenantId, request.Domain);
        if (boundary == PostgresCommitBoundary.BeforeCommit) json.ShouldBe(previous);
        else JsonSerializer.Deserialize<ShardCommitWitness>(json!, ReadOptions)!.ShouldBe(new(request.OperationId!, expected!));
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
        foreach (ShardState state in states) await AssertStateAsync(fixture, state);
        foreach ((string tenant, string domain, _) in absent)
        {
            (await fixture.DurableAsync(tenant, domain)).ShouldBeNull();
            (await fixture.DurableWitnessAsync(tenant, domain)).ShouldBeNull();
        }
    }

    private static async Task AssertStateAsync(ShardingFixture fixture, ShardState state)
    {
        (await fixture.StateAsync(state.TenantId, state.Domain)).ShouldBe(state);
        ShardCommitWitness witness = JsonSerializer.Deserialize<ShardCommitWitness>((await fixture.DurableWitnessAsync(state.TenantId, state.Domain))!, ReadOptions)!;
        witness.State.ShouldBe(state);
        string.IsNullOrWhiteSpace(witness.OperationId).ShouldBeFalse();
        using JsonDocument json = JsonDocument.Parse((await fixture.DurableAsync(state.TenantId, state.Domain))!);
        foreach ((string name, long value) in new[] { ("Generation", state.Generation), ("Ceiling", state.Ceiling) })
        {
            JsonElement field = json.RootElement.EnumerateObject().Single(member => member.Name.Equals(name, StringComparison.OrdinalIgnoreCase)).Value;
            field.ValueKind.ShouldBe(JsonValueKind.String);
            field.GetString().ShouldBe(value.ToString(CultureInfo.InvariantCulture));
        }
    }

    private static async Task DenyAsync(ShardingFixture fixture, ShardRequest request, string code)
    {
        string? before = await fixture.DurableAsync(request.TenantId, request.Domain);
        string? witnessBefore = await fixture.DurableWitnessAsync(request.TenantId, request.Domain);
        ShardReply reply = await fixture.ApplyAsync(1, request);
        reply.Accepted.ShouldBeFalse();
        reply.Code.ShouldBe(code);
        (await fixture.DurableAsync(request.TenantId, request.Domain)).ShouldBe(before);
        (await fixture.DurableWitnessAsync(request.TenantId, request.Domain)).ShouldBe(witnessBefore);
    }

    private static async Task<Dictionary<(string, string), string?>> WitnessSnapshotAsync(ShardingFixture fixture, IEnumerable<ShardState> states)
    {
        var result = new Dictionary<(string, string), string?>();
        foreach (ShardState state in states)
            result.Add((state.TenantId, state.Domain), await fixture.DurableWitnessAsync(state.TenantId, state.Domain));
        return result;
    }
}
