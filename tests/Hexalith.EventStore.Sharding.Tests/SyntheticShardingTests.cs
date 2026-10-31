using System.Globalization;
using System.Text.Json;

using Hexalith.EventStore.Sharding.TestHost;

using Shouldly;

namespace Hexalith.EventStore.Sharding.Tests;

/// <summary>Independent-host synthetic reservations with complete provider end-state checks.</summary>
public sealed class SyntheticShardingTests
{
    /// <summary>Proves bounded concurrency, identity isolation, denials, unused ceilings and restart arithmetic.</summary>
    [Fact]
    public async Task TwoIndependentHostsPreserveDurableSyntheticReservationsAsync()
    {
        await using var fixture = new ShardingFixture();
        await fixture.InitializeAsync();
        int[] original = fixture.ProcessIds;
        original.Length.ShouldBe(4);
        original.Distinct().Count().ShouldBe(4);
        const string Tenant = "tenant|x";
        const string Domain = "domain:.| ";
        const long Generation = 7;

        (await fixture.ApplyAsync(0, new("Admit", Tenant, Domain, Generation))).State.ShouldBe(new(Tenant, Domain, Generation, 0));
        int[] requestedCounts = Enumerable.Range(0, 64).Select(index => (index % 9) + 1).ToArray();
        requestedCounts.Sum().ShouldBe(316);
        Task<ShardReply>[] operations = requestedCounts
            .Select((count, index) => fixture.ApplyAsync(index % 2, new("Reserve", Tenant, Domain, Generation, count)))
            .ToArray();
        ShardReply[] ranges = await Task.WhenAll(operations);
        for (int index = 0; index < ranges.Length; index++) ranges[index].Count.ShouldBe(requestedCounts[index]);
        long expectedFirst = 1;
        var counters = new HashSet<long>();
        foreach (ShardReply range in ranges.OrderBy(range => range.First))
        {
            range.Accepted.ShouldBeTrue();
            range.State.ShouldBe(new(Tenant, Domain, Generation, range.First + range.Count - 1));
            range.First.ShouldBe(expectedFirst);
            for (long counter = range.First; counter < range.First + range.Count; counter++) counters.Add(counter).ShouldBeTrue();
            expectedFirst += range.Count;
        }

        const long Total = 316;
        counters.Count.ShouldBe((int)Total);
        (await fixture.StateAsync(Tenant, Domain)).ShouldBe(new(Tenant, Domain, Generation, Total));
        await AssertUnchangedDenialAsync(fixture, new("Admit", Tenant, Domain, Generation + 1), "generation-conflict");
        await AssertUnchangedDenialAsync(fixture, new("Reserve", Tenant, Domain, Generation + 1, 1), "generation-conflict");
        foreach (int count in new[] { 0, -1, int.MinValue })
        {
            await AssertUnchangedDenialAsync(fixture, new("Reserve", Tenant, Domain, Generation, count), "invalid-count");
        }

        await AssertUnchangedDenialAsync(fixture, new("Admit", "missing", "pair", 0), "invalid-generation");
        await AssertUnchangedDenialAsync(fixture, new("Admit", "missing", "pair", -1), "invalid-generation");
        await AssertUnchangedDenialAsync(fixture, new("Reserve", "missing", "pair", 1, 1), "not-admitted");
        string? beforeRoutingDenial = await fixture.DurableAsync(Tenant, Domain);
        ShardReply mismatch = await fixture.ApplyAsync(1, new("Reserve", "wrong", Domain, Generation, 1), ShardIdentity.Encode(Tenant, Domain));
        mismatch.Code.ShouldBe("routing-mismatch");
        mismatch.Accepted.ShouldBeFalse();
        (await fixture.DurableAsync(Tenant, Domain)).ShouldBe(beforeRoutingDenial);
        (await fixture.DurableAsync("wrong", Domain)).ShouldBeNull();

        (string Tenant, string Domain)[] pairs =
        [
            ("shared", "one"), ("shared", "two"), ("one", "shared"), ("two", "shared"),
            ("a|b", "c"), ("a", "b|c"), ("A", "b|c"), ("é", "unicode"), ("e\u0301", "unicode"),
            ("unicode", "é"), ("unicode", "e\u0301"), (" ", "."),
        ];
        pairs.Select(pair => ShardIdentity.Encode(pair.Tenant, pair.Domain)).Distinct(StringComparer.Ordinal).Count().ShouldBe(pairs.Length);
        for (int index = 0; index < pairs.Length; index++)
        {
            (string tenant, string domain) = pairs[index];
            long generation = 100 + index;
            (await fixture.ApplyAsync(index % 2, new("Admit", tenant, domain, generation))).Accepted.ShouldBeTrue();
            ShardReply reserved = await fixture.ApplyAsync((index + 1) % 2, new("Reserve", tenant, domain, generation, index + 1));
            reserved.First.ShouldBe(1);
            reserved.State.ShouldBe(new(tenant, domain, generation, index + 1));
            (await fixture.StateAsync(tenant, domain)).ShouldBe(reserved.State);
        }

        // This range is deliberately never consumed. Restart must honor its full saved ceiling.
        ShardReply unused = await fixture.ApplyAsync(1, new("Reserve", Tenant, Domain, Generation, 13));
        unused.Accepted.ShouldBeTrue();
        unused.First.ShouldBe(Total + 1);
        unused.Count.ShouldBe(13);
        unused.State.ShouldBe(new(Tenant, Domain, Generation, Total + 13));
        (await fixture.StateAsync(Tenant, Domain)).ShouldBe(unused.State);
        (await fixture.ApplyAsync(0, new("Admit", Tenant, Domain, Generation))).State.ShouldBe(unused.State);

        const string BoundsTenant = "bounds";
        const string BoundsDomain = "int64";
        const long BoundsGeneration = long.MaxValue;
        (await fixture.ApplyAsync(0, new("Admit", BoundsTenant, BoundsDomain, BoundsGeneration))).Accepted.ShouldBeTrue();
        ShardReply seeded = await fixture.ApplyAsync(1, new("Seed", BoundsTenant, BoundsDomain, BoundsGeneration, SeedCeiling: long.MaxValue - 1));
        seeded.State.ShouldBe(new(BoundsTenant, BoundsDomain, BoundsGeneration, long.MaxValue - 1));
        await AssertPreciseBoundStateAsync(fixture, seeded.State!);
        await AssertUnchangedDenialAsync(fixture, new("Reserve", BoundsTenant, BoundsDomain, BoundsGeneration, 2), "overflow");

        const string AdmissionOnlyTenant = "admitted-without-reservation";
        const string AdmissionOnlyDomain = "zero-ceiling";
        var admissionOnly = new ShardState(AdmissionOnlyTenant, AdmissionOnlyDomain, 31, 0);
        (await fixture.ApplyAsync(0, new("Admit", AdmissionOnlyTenant, AdmissionOnlyDomain, 31))).State.ShouldBe(admissionOnly);
        (await fixture.StateAsync(AdmissionOnlyTenant, AdmissionOnlyDomain)).ShouldBe(admissionOnly);

        int[] replacements = await fixture.RestartAsync();
        replacements.Length.ShouldBe(4);
        replacements.Distinct().Count().ShouldBe(4);
        replacements.Intersect(original).ShouldBeEmpty();
        (await fixture.StateAsync(AdmissionOnlyTenant, AdmissionOnlyDomain)).ShouldBe(admissionOnly);
        (await fixture.ApplyAsync(1, new("Inspect", AdmissionOnlyTenant, AdmissionOnlyDomain, 31))).State.ShouldBe(admissionOnly);
        ShardReply firstAfterAdmission = await fixture.ApplyAsync(0, new("Reserve", AdmissionOnlyTenant, AdmissionOnlyDomain, 31, 1));
        firstAfterAdmission.First.ShouldBe(1);
        firstAfterAdmission.Count.ShouldBe(1);
        firstAfterAdmission.State.ShouldBe(admissionOnly with { Ceiling = 1 });
        (await fixture.StateAsync(AdmissionOnlyTenant, AdmissionOnlyDomain)).ShouldBe(firstAfterAdmission.State);
        (await fixture.StateAsync(Tenant, Domain)).ShouldBe(unused.State);
        (await fixture.ApplyAsync(1, new("Inspect", Tenant, Domain, Generation))).State.ShouldBe(unused.State);
        (await fixture.ApplyAsync(0, new("Admit", Tenant, Domain, Generation))).State.ShouldBe(unused.State);
        ShardReply afterRestart = await fixture.ApplyAsync(1, new("Reserve", Tenant, Domain, Generation, 2));
        afterRestart.First.ShouldBe(unused.State!.Ceiling + 1);
        afterRestart.State.ShouldBe(new(Tenant, Domain, Generation, unused.State.Ceiling + 2));
        (await fixture.StateAsync(Tenant, Domain)).ShouldBe(afterRestart.State);
        await AssertUnchangedDenialAsync(fixture, new("Admit", Tenant, Domain, 99), "generation-conflict");
        await AssertUnchangedDenialAsync(fixture, new("Reserve", "missing", "pair", 1, 1), "not-admitted");
        for (int index = 0; index < pairs.Length; index++)
        {
            (string tenant, string domain) = pairs[index];
            (await fixture.StateAsync(tenant, domain)).ShouldBe(new(tenant, domain, 100 + index, index + 1));
            ShardReply next = await fixture.ApplyAsync(index % 2, new("Reserve", tenant, domain, 100 + index, 1));
            next.First.ShouldBe(index + 2);
            next.State.ShouldBe(new(tenant, domain, 100 + index, index + 2));
            (await fixture.StateAsync(tenant, domain)).ShouldBe(next.State);
        }

        await AssertPreciseBoundStateAsync(fixture, seeded.State!);
        ShardReply last = await fixture.ApplyAsync(0, new("Reserve", BoundsTenant, BoundsDomain, BoundsGeneration, 1));
        last.First.ShouldBe(long.MaxValue);
        last.State.ShouldBe(new(BoundsTenant, BoundsDomain, BoundsGeneration, long.MaxValue));
        await AssertPreciseBoundStateAsync(fixture, last.State!);
        await AssertUnchangedDenialAsync(fixture, new("Reserve", BoundsTenant, BoundsDomain, BoundsGeneration, 1), "overflow");
        await fixture.RestartAsync();
        await AssertPreciseBoundStateAsync(fixture, last.State!);
        await AssertUnchangedDenialAsync(fixture, new("Reserve", BoundsTenant, BoundsDomain, BoundsGeneration, int.MaxValue), "overflow");
        (await fixture.StateAsync(Tenant, Domain)).ShouldBe(afterRestart.State);
        await fixture.DisposeAsync();
        fixture.CleanupVerified.ShouldBeTrue();
        Console.WriteLine("Retained synthetic development artifacts: " + fixture.Artifacts);
    }

    private static async Task AssertUnchangedDenialAsync(ShardingFixture fixture, ShardRequest request, string code)
    {
        string? before = await fixture.DurableAsync(request.TenantId, request.Domain);
        ShardReply denied = await fixture.ApplyAsync(0, request);
        denied.Accepted.ShouldBeFalse();
        denied.Code.ShouldBe(code);
        // Inspect after a completed actor operation, including the runtime's save boundary.
        (await fixture.DurableAsync(request.TenantId, request.Domain)).ShouldBe(before);
    }

    private static async Task AssertPreciseBoundStateAsync(ShardingFixture fixture, ShardState expected)
    {
        // Fail at the first typed provider boundary, before denial or process replacement.
        (await fixture.StateAsync(expected.TenantId, expected.Domain)).ShouldBe(expected);
        using JsonDocument document = JsonDocument.Parse((await fixture.DurableAsync(expected.TenantId, expected.Domain))!);
        foreach ((string property, long value) in new[] { ("Generation", expected.Generation), ("Ceiling", expected.Ceiling) })
        {
            JsonElement field = document.RootElement.EnumerateObject()
                .Single(member => string.Equals(member.Name, property, StringComparison.OrdinalIgnoreCase)).Value;
            field.ValueKind.ShouldBe(JsonValueKind.String);
            field.GetString().ShouldBe(value.ToString(CultureInfo.InvariantCulture));
        }
    }
}
