using System.Text.RegularExpressions;
using System.Text;

using Hexalith.EventStore.Server.Control;

using Shouldly;

namespace Hexalith.EventStore.Server.Tests.Control;

public sealed class PostgreSqlControlContractTests
{
    [Fact]
    public void VersionOneSchemaMatchesExactReviewedDdlAndIndexes()
    {
        string root = FindRepositoryRoot();
        string contract = File.ReadAllText(Path.Combine(root, "_bmad-output", "implementation-artifacts", "6-5-integration", "metadata-adapter-contract.md"));
        MatchCollection blocks = Regex.Matches(contract, "```sql\\r?\\n(?<sql>.*?)\\r?\\n```", RegexOptions.Singleline);
        blocks.Count.ShouldBe(2);
        string expected = string.Join("\n", blocks.Select(match => match.Groups["sql"].Value.Replace("\r\n", "\n"))) + "\n";
        string actual = File.ReadAllText(Path.Combine(root, "src", "Hexalith.EventStore.Server", "Control", "Schema", "v1.sql"))
            .Replace("\r\n", "\n");
        actual.ShouldBe(expected);
    }

    [Fact]
    public void FamilyCapsMatchEveryNormativeByteCeilingAndRefuseOverrun()
    {
        string normative = File.ReadAllText(Path.Combine(FindRepositoryRoot(), "_bmad-output", "implementation-artifacts",
            "spec-event-versioning-upcasting.md"));
        string table = normative.Split("## Normative byte ceilings", StringSplitOptions.None)[1]
            .Split("## Recovery correction ownership", StringSplitOptions.None)[0];
        MatchCollection rows = Regex.Matches(table, @"^\| (?<family>(?:record|control|public)/[^| ]+) \| (?<bytes>\d+) \|$", RegexOptions.Multiline);
        rows.Count.ShouldBe(PostgreSqlControlCaps.MaximumBytes.Count);
        foreach (Match row in rows)
        {
            string family = row.Groups["family"].Value;
            int ceiling = int.Parse(row.Groups["bytes"].Value, System.Globalization.CultureInfo.InvariantCulture);
            PostgreSqlControlCaps.MaximumBytes[family].ShouldBe(ceiling);
            PostgreSqlControlCaps.RequireLength(family, ceiling);
            Should.Throw<InvalidOperationException>(() => PostgreSqlControlCaps.RequireLength(family, (long)ceiling + 1));
        }
        Should.Throw<InvalidOperationException>(() => PostgreSqlControlCaps.RequireLength("control/unknown", 1));
        Should.Throw<InvalidOperationException>(() => PostgreSqlControlCaps.RequireLength("control/queue", 0));
    }

    [Fact]
    public void AddressAndQueueShardGuardsRejectNoncanonicalInputs()
    {
        string address = "owner-registry-entry:" + new string('a', 64);
        PostgreSqlControlCaps.RequireFramedAddress(address, "owner-registry-entry:");
        Should.Throw<InvalidOperationException>(() => PostgreSqlControlCaps.RequireFramedAddress(address.ToUpperInvariant(), "owner-registry-entry:"));
        Should.Throw<InvalidOperationException>(() => PostgreSqlControlCaps.RequireFramedAddress(address + "0", "owner-registry-entry:"));
        PostgreSqlControlCaps.RequireQueueShard(7, 6400);
        Should.Throw<InvalidOperationException>(() => PostgreSqlControlCaps.RequireQueueShard(8, 0));
        Should.Throw<InvalidOperationException>(() => PostgreSqlControlCaps.RequireQueueShard(0, 6401));
    }

    [Fact]
    public void OwnerFenceUsesCanonicalUlidBitsAndRejectsInvalidTokens()
    {
        string zero = PostgreSqlOwnerFence.Create(DateTimeOffset.UnixEpoch, new byte[10]);
        zero.ShouldBe(new string('0', 26));
        string one = PostgreSqlOwnerFence.Create(DateTimeOffset.UnixEpoch.AddMilliseconds(1), new byte[10]);
        one.ShouldNotBe(zero);
        PostgreSqlOwnerFence.RequireCanonical(one);
        PostgreSqlOwnerFence.RequireCanonical(PostgreSqlOwnerFence.Create(DateTimeOffset.UtcNow));
        Should.Throw<InvalidOperationException>(() => PostgreSqlOwnerFence.RequireCanonical("8" + new string('0', 25)));
        Should.Throw<InvalidOperationException>(() => PostgreSqlOwnerFence.RequireCanonical("0" + new string('I', 25)));
        Should.Throw<ArgumentException>(() => PostgreSqlOwnerFence.Create(DateTimeOffset.UnixEpoch.ToOffset(TimeSpan.FromHours(1)), new byte[10]));
    }

    [Fact]
    public void RegistryIndexPreservesNulIdentifiersAndDerivesIndependentHashAnswers()
    {
        var index = new PostgreSqlRegistryIndex("dep\0loy"u8, "tenant"u8, "scope\0id"u8, "subject"u8, -123);
        index.Deployment.ShouldBe("dep\0loy"u8.ToArray());
        index.ScopeId.ShouldBe("scope\0id"u8.ToArray());
        index.FirstTicks.ShouldBe(-123);
        index.Shard.ShouldBe((byte)91);
        Convert.ToHexStringLower(index.DeploymentHash).ShouldBe("95438899381fa4abfc72ea2229a2eec10a32df79d9084e15173c885191ab8557");
        Convert.ToHexStringLower(index.ScopeHash).ShouldBe("c3f2abde7e6320bf81398af80efc0f98f01a491f179b912d42f0505dfc78de37");
        Should.Throw<InvalidOperationException>(() => new PostgreSqlRegistryIndex("d"u8, "wrong"u8, "s"u8, "x"u8, 0));
        Should.Throw<DecoderFallbackException>(() => new PostgreSqlRegistryIndex([0xff], "tenant"u8, "s"u8, "x"u8, 0));
    }

    [Fact]
    public async Task TransactionPlanRequiresOrderedExactGenerationFenceAndRepeatablePayload()
    {
        string firstFence = PostgreSqlOwnerFence.Create(DateTimeOffset.UnixEpoch, new byte[10]);
        string secondFence = PostgreSqlOwnerFence.Create(DateTimeOffset.UnixEpoch.AddMilliseconds(1), new byte[10]);
        var payload = new PostgreSqlControlPayloadSource(1, () => new MemoryStream([42], writable: false));
        var old = new PostgreSqlControlRowImage(3, firstFence, payload, null);
        var next = new PostgreSqlControlRowImage(4, firstFence, payload, null);
        string low = "held-delivery:" + new string('0', 64);
        string high = "operations-epoch:" + new string('f', 64);
        var plan = new PostgreSqlControlTransactionPlan([
            new("control/epoch", high, null, new PostgreSqlControlRowImage(0, secondFence, payload, null)),
            new("control/held", low, old, next),
        ]);
        plan.Mutations.Select(item => item.Address).ShouldBe([low, high]);
        await payload.RequireExactLengthAsync(CancellationToken.None);
        Should.Throw<InvalidOperationException>(() => new PostgreSqlControlTransactionPlan([
            new PostgreSqlControlMutation("control/held", low, old, next),
            new PostgreSqlControlMutation("control/held", low, old, next),
        ]));
        Should.Throw<InvalidOperationException>(() => new PostgreSqlControlTransactionPlan([
            new PostgreSqlControlMutation("control/held", low, old, next with { Generation = 3 }),
        ]));
        Should.Throw<InvalidOperationException>(() => new PostgreSqlControlTransactionPlan([
            new PostgreSqlControlMutation("control/held", low, old, next with { OwnerFence = secondFence }),
        ]));
        _ = new PostgreSqlControlTransactionPlan([
            new PostgreSqlControlMutation("control/held", low, old, next with { OwnerFence = secondFence }, OwnershipTransfer: true),
        ]);
        Should.Throw<InvalidOperationException>(() => new PostgreSqlControlTransactionPlan([
            new PostgreSqlControlMutation("control/held", high, old, next),
        ]));
        Should.Throw<InvalidOperationException>(() => new PostgreSqlControlTransactionPlan([
            new PostgreSqlControlMutation("control/registry-entry", "owner-registry-entry:" + new string('a', 64), old, next),
        ]));
        await Should.ThrowAsync<InvalidOperationException>(() => new PostgreSqlControlPayloadSource(2,
            () => new MemoryStream([1], writable: false)).RequireExactLengthAsync(CancellationToken.None).AsTask());
        await Should.ThrowAsync<InvalidOperationException>(() => new PostgreSqlControlPayloadSource(1,
            () => new MemoryStream([1, 2], writable: false)).RequireExactLengthAsync(CancellationToken.None).AsTask());
    }

    [Fact]
    public void FixedSqlComparesEveryPredecessorColumnBeforeMutation()
    {
        PostgreSqlControlStatements.Insert.ShouldContain("ON CONFLICT DO NOTHING");
        PostgreSqlControlStatements.SelectForUpdate.ShouldContain("FOR UPDATE");
        foreach (string column in new[] { "generation", "owner_fence", "payload", "registry_deployment", "registry_scope_kind",
            "registry_scope_id", "registry_subject", "registry_first_ticks", "registry_shard", "registry_deployment_hash", "registry_scope_hash" })
        {
            PostgreSqlControlStatements.Update.ShouldContain(column);
            PostgreSqlControlStatements.Delete.ShouldContain(column);
        }
        PostgreSqlControlStatements.Update.ShouldContain("payload = $16");
        PostgreSqlControlStatements.Delete.ShouldContain("payload = $5");
        PostgreSqlControlStatements.Update.ShouldContain("registry_scope_hash IS NOT DISTINCT FROM $24");
        PostgreSqlControlStatements.Delete.ShouldContain("registry_scope_hash IS NOT DISTINCT FROM $13");
        PostgreSqlControlStatements.Select.ShouldNotContain("dapr_");
    }

    private static string FindRepositoryRoot()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Hexalith.EventStore.slnx")))
        {
            directory = directory.Parent;
        }
        return directory?.FullName ?? throw new DirectoryNotFoundException("Story 6.6 repository root was not found.");
    }
}
