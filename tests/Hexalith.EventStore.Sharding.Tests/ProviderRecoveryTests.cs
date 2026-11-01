using System.Globalization;
using System.Text.Json;

using Hexalith.EventStore.Sharding.TestHost;

using Shouldly;

namespace Hexalith.EventStore.Sharding.Tests;

/// <summary>Isolated same-storage PostgreSQL restart and conservative failure recovery.</summary>
[Collection("ShardingLive")]
public sealed class ProviderRecoveryTests
{
    /// <summary>Preserves exact durable authority through stop/start, interrupted save, response loss and replacement.</summary>
    [Fact]
    public async Task SameStorageProviderRecoveryPreservesAuthorityAcrossIndependentHostsAsync()
    {
        await using var fixture = new ShardingFixture();
        await fixture.InitializeAsync();
        RunBinding? binding = JsonSerializer.Deserialize<RunBinding>(await File.ReadAllTextAsync(Path.Combine(fixture.Artifacts, "binding.json")));
        binding.ShouldNotBeNull();
        binding.Configuration["authorization"].ShouldBe(ShardingFixture.AuthorizationFor(providerCommitProxy: false));
        int[] initialProcesses = fixture.ProcessIds;
        initialProcesses.Length.ShouldBe(4);
        initialProcesses.Distinct().Count().ShouldBe(4);
        var acknowledged = new HashSet<string>(StringComparer.Ordinal);
        var states = new List<ShardState>();
        (string Tenant, string Domain)[] pairs =
        [
            ("shared", "orders"), ("shared", "billing"), ("other", "orders"),
            ("a|b", "c"), ("a", "b|c"), ("A", "b|c"), ("é", "unicode"), ("e\u0301", "unicode"),
        ];
        for (int index = 0; index < pairs.Length; index++)
        {
            (string tenant, string domain) = pairs[index];
            long generation = long.MaxValue - index;
            ShardReply admission = await fixture.ApplyAsync(index % 2, new("Admit", tenant, domain, generation));
            admission.Accepted.ShouldBeTrue();
            admission.State.ShouldBe(new(tenant, domain, generation, 0));
            states.Add(await ReserveAsync(fixture, admission.State!, index + 1, acknowledged, index % 2));
        }

        // The full acknowledged range remains authoritative even though no events consume it.
        states[0] = await ReserveAsync(fixture, states[0], 13, acknowledged, 1);
        var zero = new ShardState("zero-admission", "orders", long.MaxValue, 0);
        (await fixture.ApplyAsync(0, new("Admit", zero.TenantId, zero.Domain, zero.Generation))).State.ShouldBe(zero);
        var bound = new ShardState("bounds", "int64", long.MaxValue, long.MaxValue - 2);
        (await fixture.ApplyAsync(0, new("Admit", bound.TenantId, bound.Domain, bound.Generation))).Accepted.ShouldBeTrue();
        (await fixture.ApplyAsync(1, new("Seed", bound.TenantId, bound.Domain, bound.Generation, SeedCeiling: bound.Ceiling))).State.ShouldBe(bound);
        await AssertStateAsync(fixture, bound);
        await fixture.StopProviderAsync(abrupt: false);
        fixture.ProcessIds.ShouldBe(initialProcesses);
        // Every failure here is conservatively uncertain, even for an attempted admission.
        await AssertUncertainActorAsync(fixture, 0, new("Reserve", states[0].TenantId, states[0].Domain, states[0].Generation, 4));
        await AssertUncertainActorAsync(fixture, 1, new("Reserve", "missing", "orders", 17, 1));
        await AssertUncertainActorAsync(fixture, 1, new("Admit", "during-outage", "orders", 19));
        await fixture.RecoverProviderAsync();
        fixture.ProcessIds.ShouldBe(initialProcesses);
        states[0] = await InspectUncertainReservationAsync(fixture, states[0], 4);
        await AssertAllAsync(fixture, states.Append(zero).Append(bound));
        (await fixture.StateAsync("missing", "orders")).ShouldBeNull();
        ShardState? outageAdmission = await fixture.StateAsync("during-outage", "orders");
        if (outageAdmission is not null)
        {
            outageAdmission.ShouldBe(new("during-outage", "orders", 19, 0));
            states.Add(outageAdmission);
        }
        else
        {
            await AssertDenialAsync(fixture, new("Reserve", "during-outage", "orders", 19, 1), "not-admitted");
        }
        await AssertDenialAsync(fixture, new("Reserve", "missing", "orders", 17, 1), "not-admitted");
        await AssertDenialAsync(fixture, new("Admit", states[0].TenantId, states[0].Domain, 1), "generation-conflict");
        (await fixture.ApplyAsync(1, new("Admit", states[0].TenantId, states[0].Domain, states[0].Generation))).State.ShouldBe(states[0]);
        states[0] = await ReserveAsync(fixture, states[0], 2, acknowledged, 1);

        // PostgreSQL itself witnesses a writer blocked before the provider is killed.
        Task<string> lockHolder = fixture.HoldStateLockAsync();
        await fixture.AwaitStateLockAsync(blockedSave: false);
        ShardState beforeInterrupted = states[0];
        int interruptedSends = 0;
        Task<ShardReply> interrupted = fixture.ApplyAsync(0, new("Reserve", beforeInterrupted.TenantId, beforeInterrupted.Domain, beforeInterrupted.Generation, 11), observeTransport: (sends, _) => interruptedSends = sends);
        await fixture.AwaitStateLockAsync(blockedSave: true);
        await fixture.StopProviderAsync(abrupt: true);
        await AssertUncertainAsync(() => interrupted);
        interruptedSends.ShouldBe(1);
        await AssertUncertainAsync(async () => { _ = await lockHolder; });
        await AssertUncertainActorAsync(fixture, 1, new("Reserve", "missing-after-kill", "orders", 23, 1));
        await fixture.RecoverProviderAsync();
        fixture.ProcessIds.ShouldBe(initialProcesses);
        states[0] = await InspectUncertainReservationAsync(fixture, beforeInterrupted, 11);
        await AssertAllAsync(fixture, states.Append(zero).Append(bound));
        (await fixture.StateAsync("missing-after-kill", "orders")).ShouldBeNull();
        await AssertDenialAsync(fixture, new("Reserve", "missing-after-kill", "orders", 23, 1), "not-admitted");
        states[0] = await ReserveAsync(fixture, states[0], 3, acknowledged, 1);

        ShardState beforeLost = states[0];
        await AssertUncertainActorAsync(fixture, 0, new("Reserve", beforeLost.TenantId, beforeLost.Domain, beforeLost.Generation, 17), loseResponse: true);
        ShardState lostOutcome = beforeLost with { Ceiling = beforeLost.Ceiling + 17 };
        await AssertStateAsync(fixture, lostOutcome); // Never replay that ambiguous reservation.
        states[0] = await ReserveAsync(fixture, lostOutcome, 2, acknowledged, 1);
        states.Add(await ReserveAsync(fixture, zero, 1, acknowledged, 0));
        bound = await ReserveAsync(fixture, bound, 2, acknowledged, 1);
        bound.Ceiling.ShouldBe(long.MaxValue);
        await AssertDenialAsync(fixture, new("Reserve", bound.TenantId, bound.Domain, bound.Generation, 1), "overflow");
        await AssertAllAsync(fixture, states.Append(bound));

        int[] replacements = await fixture.RestartAsync();
        replacements.Length.ShouldBe(4);
        replacements.Distinct().Count().ShouldBe(4);
        replacements.Intersect(initialProcesses).ShouldBeEmpty();
        await AssertAllAsync(fixture, states.Append(bound));
        for (int index = 0; index < states.Count; index++)
        {
            ShardState state = states[index];
            (await fixture.ApplyAsync(index % 2, new("Inspect", state.TenantId, state.Domain, state.Generation))).State.ShouldBe(state);
            states[index] = await ReserveAsync(fixture, state, 1, acknowledged, index % 2);
        }

        await AssertDenialAsync(fixture, new("Reserve", bound.TenantId, bound.Domain, bound.Generation, 1), "overflow");
        if (outageAdmission is null)
            await AssertDenialAsync(fixture, new("Reserve", "during-outage", "orders", 19, 1), "not-admitted");
        await AssertAllAsync(fixture, states.Append(bound));
        acknowledged.Count.ShouldBe(68 + (outageAdmission is null ? 0 : 1)); // 36 initial + 13 unused + 2 + 3 + 2 + 1 + 2 + 9 after replacement.
        await fixture.DisposeAsync();
        fixture.CleanupVerified.ShouldBeTrue();
        Console.WriteLine("Retained same-storage recovery artifacts: " + fixture.Artifacts);
    }

    private static async Task<ShardState> InspectUncertainReservationAsync(ShardingFixture fixture, ShardState previous, int count)
    {
        ShardState? durable = await fixture.StateAsync(previous.TenantId, previous.Domain);
        durable.ShouldNotBeNull();
        durable.TenantId.ShouldBe(previous.TenantId);
        durable.Domain.ShouldBe(previous.Domain);
        durable.Generation.ShouldBe(previous.Generation);
        // Permit the entire reserved range as a gap when a save may have committed.
        // Partial, reset, absent and repeated reservations are never valid outcomes.
        new[] { previous.Ceiling, previous.Ceiling + count }.ShouldContain(durable.Ceiling);
        return durable;
    }

    private static async Task<ShardState> ReserveAsync(ShardingFixture fixture, ShardState previous, int count, HashSet<string> acknowledged, int node)
    {
        ShardReply reply = await fixture.ApplyAsync(node, new("Reserve", previous.TenantId, previous.Domain, previous.Generation, count));
        reply.Accepted.ShouldBeTrue();
        reply.Code.ShouldBe("saved");
        reply.First.ShouldBe(checked(previous.Ceiling + 1));
        reply.Count.ShouldBe(count);
        ShardState expected = previous with { Ceiling = checked(previous.Ceiling + count) };
        reply.State.ShouldBe(expected);
        for (int offset = 0; offset < count; offset++)
            acknowledged.Add(ShardIdentity.Encode(previous.TenantId, previous.Domain) + ":" + previous.Generation.ToString(CultureInfo.InvariantCulture) + ":" + (reply.First + offset).ToString(CultureInfo.InvariantCulture)).ShouldBeTrue();
        await AssertStateAsync(fixture, expected);
        return expected;
    }

    private static async Task AssertAllAsync(ShardingFixture fixture, IEnumerable<ShardState> expected)
    {
        foreach (ShardState state in expected) await AssertStateAsync(fixture, state);
    }

    private static async Task AssertStateAsync(ShardingFixture fixture, ShardState expected)
    {
        (await fixture.StateAsync(expected.TenantId, expected.Domain)).ShouldBe(expected);
        using JsonDocument json = JsonDocument.Parse((await fixture.DurableAsync(expected.TenantId, expected.Domain))!);
        foreach ((string name, long value) in new[] { ("Generation", expected.Generation), ("Ceiling", expected.Ceiling) })
        {
            JsonElement field = json.RootElement.EnumerateObject().Single(member => member.Name.Equals(name, StringComparison.OrdinalIgnoreCase)).Value;
            field.ValueKind.ShouldBe(JsonValueKind.String);
            field.GetString().ShouldBe(value.ToString(CultureInfo.InvariantCulture));
        }
    }

    private static async Task AssertDenialAsync(ShardingFixture fixture, ShardRequest request, string code)
    {
        string? before = await fixture.DurableAsync(request.TenantId, request.Domain);
        ShardReply result = await fixture.ApplyAsync(1, request);
        result.Accepted.ShouldBeFalse();
        result.Code.ShouldBe(code);
        (await fixture.DurableAsync(request.TenantId, request.Domain)).ShouldBe(before);
    }

    private static async Task AssertUncertainActorAsync(ShardingFixture fixture, int node, ShardRequest request, bool loseResponse = false)
    {
        int sends = 0;
        bool discarded = false;
        Exception? error = await Record.ExceptionAsync(() => fixture.ApplyAsync(node, request, loseResponse: loseResponse,
            observeTransport: (count, dropped) => { sends = count; discarded = dropped; }));
        error.ShouldNotBeNull();
        sends.ShouldBe(1); // Checked outside the expected exception so a retry rejection cannot satisfy the test.
        discarded.ShouldBe(loseResponse);
    }

    private static async Task AssertUncertainAsync(Func<Task> operation)
        => (await Record.ExceptionAsync(operation)).ShouldNotBeNull();
}
