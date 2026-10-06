using System.Security.Cryptography;
using System.Text.Json;

using Hexalith.EventStore.Client.Events;

using Shouldly;

namespace Hexalith.EventStore.Client.Tests.Events;

/// <summary>Qualifies local supplied-graph checks and their limits under the trusted-code policy.</summary>
public sealed class EventDependencyClosureVerifierTests
{
    /// <summary>Checks each independent identity, pin and reachability refusal.</summary>
    /// <param name="mismatch">The supplied graph mismatch to exercise.</param>
    [Theory]
    [InlineData("managed-version")]
    [InlineData("native-abi")]
    [InlineData("managed-context")]
    [InlineData("native-context")]
    [InlineData("logical-identity")]
    [InlineData("dependency-kind")]
    [InlineData("missing-edge-target")]
    [InlineData("omitted-edge")]
    [InlineData("unknown-edge")]
    [InlineData("duplicate-edge")]
    [InlineData("duplicate-node")]
    [InlineData("unresolved-root")]
    [InlineData("empty-roots")]
    [InlineData("empty-graph")]
    [InlineData("too-many-roots")]
    [InlineData("too-many-nodes")]
    public void SuppliedGraphRefusesEachPinAndReachabilityMismatch(string mismatch)
    {
        WithGraphFixture((registry, rows, graph, root) =>
        {
            EventDependencyIdentity leaf = graph[1].Identity;
            (EventResolvedDependency[] Nodes, EventDependencyIdentity[] Roots) supplied = mismatch switch
            {
                "managed-version" => ([graph[0] with { ResolvedVersionOrAbiIdentity = "2.0" }, graph[1]], [root]),
                "native-abi" => ([graph[0], graph[1] with { ResolvedVersionOrAbiIdentity = "abi-2" }], [root]),
                "managed-context" => ([graph[0] with { LoaderContextId = "another-context" }, graph[1]], [root]),
                "native-context" => ([graph[0], graph[1] with { LoaderContextId = "another-context" }], [root]),
                "logical-identity" => ([graph[0] with { Identity = new("another-root", "managed") }, graph[1]], [root]),
                "dependency-kind" => ([graph[0] with { Identity = new("root", "native") }, graph[1]], [root]),
                "missing-edge-target" => (graph[..1], [root]),
                "omitted-edge" => ([graph[0] with { Dependencies = [] }, graph[1]], [root]),
                "unknown-edge" => ([graph[0] with { Dependencies = [new("unknown", "native")] }, graph[1]], [root]),
                "duplicate-edge" => ([graph[0] with { Dependencies = [leaf, leaf] }, graph[1]], [root]),
                "duplicate-node" => ([graph[0], graph[0], graph[1]], [root]),
                "unresolved-root" => (graph, [new("unknown", "managed")]),
                "empty-roots" => (graph, []),
                "empty-graph" => ([], [root]),
                "too-many-roots" => (graph, Enumerable.Repeat(root, 65_537).ToArray()),
                "too-many-nodes" => (Enumerable.Repeat(graph[0], 65_537).ToArray(), [root]),
                _ => throw new ArgumentOutOfRangeException(nameof(mismatch)),
            };

            InvalidOperationException failure = Should.Throw<InvalidOperationException>(() =>
                EventDependencyClosureVerifier.RequirePinnedResolvedGraph(
                    registry, rows, registry.Fingerprint, supplied.Nodes, supplied.Roots, CancellationToken.None));
            failure.Message.ShouldStartWith("CapabilityMismatch:");
        });
    }

    /// <summary>Checks that invalid graph scalars refuse before any artifact is opened.</summary>
    /// <param name="invalidScalar">The invalid node, edge or root scalar.</param>
    [Theory]
    [InlineData("node-kind")]
    [InlineData("edge-kind")]
    [InlineData("root-kind")]
    [InlineData("node-identity")]
    [InlineData("edge-identity")]
    [InlineData("root-identity")]
    [InlineData("version")]
    [InlineData("context")]
    [InlineData("file")]
    public void SuppliedGraphRefusesInvalidScalarsBeforeOpeningFiles(string invalidScalar)
    {
        WithGraphFixture((registry, rows, graph, root) =>
        {
            (EventResolvedDependency[] Nodes, EventDependencyIdentity[] Roots) supplied = invalidScalar switch
            {
                "node-kind" => ([graph[0] with { Identity = new("root", "Managed") }, graph[1]], [root]),
                "edge-kind" => ([graph[0] with { Dependencies = [new("leaf", "Native")] }, graph[1]], [root]),
                "root-kind" => (graph, [new("root", "Managed")]),
                "node-identity" => ([graph[0] with { Identity = new(string.Empty, "managed") }, graph[1]], [root]),
                "edge-identity" => ([graph[0] with { Dependencies = [new(string.Empty, "native")] }, graph[1]], [root]),
                "root-identity" => (graph, [new(string.Empty, "managed")]),
                "version" => ([graph[0] with { ResolvedVersionOrAbiIdentity = string.Empty }, graph[1]], [root]),
                "context" => ([graph[0] with { LoaderContextId = string.Empty }, graph[1]], [root]),
                "file" => ([graph[0] with { ResolvedFile = string.Empty }, graph[1]], [root]),
                _ => throw new ArgumentOutOfRangeException(nameof(invalidScalar)),
            };
            File.Delete(graph[0].ResolvedFile);
            File.Delete(graph[1].ResolvedFile);

            Should.Throw<ArgumentException>(() => EventDependencyClosureVerifier.RequirePinnedResolvedGraph(
                registry, rows, registry.Fingerprint, supplied.Nodes, supplied.Roots, CancellationToken.None));
        });
    }

    /// <summary>Records that a consistent partial supplied graph cannot establish catalog completeness.</summary>
    [Fact]
    public void MatchingPartialSupplyCannotEstablishCompleteCatalogOrEdges()
    {
        WithGraphFixture((registry, rows, graph, root) =>
        {
            EventDependencyClosureVerifier.RequirePinnedResolvedGraph(
                registry, rows, registry.Fingerprint, graph, [root], CancellationToken.None);

            // G rows have no edge inventory. Omitting both a node and its incoming edge
            // cannot be detected by this local checker; reviewed catalog authority is separate.
            EventResolvedDependency[] partialSupply = [graph[0] with { Dependencies = [] }];
            EventDependencyClosureVerifier.RequirePinnedResolvedGraph(
                registry, rows, registry.Fingerprint, partialSupply, [root], CancellationToken.None);
            registry.Rows.Count(static row => row.Tag == 0x47).ShouldBe(2);
        });
    }

    /// <summary>Records that checking a path does not preserve its bytes for subsequent execution.</summary>
    [Fact]
    public void ChangedFileIsRefusedAfterAPreviousSuccessfulLocalCheck()
    {
        WithGraphFixture((registry, rows, graph, root) =>
        {
            EventDependencyClosureVerifier.RequirePinnedResolvedGraph(
                registry, rows, registry.Fingerprint, graph, [root], CancellationToken.None);
            File.WriteAllBytes(graph[0].ResolvedFile, [3, 2, 1]);

            // A successful path hash does not make the path immutable or bind later execution.
            File.ReadAllBytes(graph[0].ResolvedFile).ShouldBe(new byte[] { 3, 2, 1 });
            Should.Throw<InvalidOperationException>(() => EventDependencyClosureVerifier.RequirePinnedResolvedGraph(
                registry, rows, registry.Fingerprint, graph, [root], CancellationToken.None));
        });
    }

    /// <summary>Checks the 64 MiB graph workspace refusal before filesystem access.</summary>
    [Fact]
    public void ResolvedGraphWorkspaceRefusesBeforeOpeningAnOverBudgetFilePath()
    {
        WithGraphFixture((registry, rows, graph, root) =>
        {
            EventResolvedDependency[] oversized = [
                graph[0] with { ResolvedFile = new string('x', 16 * 1024 * 1024) }, graph[1],
            ];

            InvalidOperationException failure = Should.Throw<InvalidOperationException>(() =>
                EventDependencyClosureVerifier.RequirePinnedResolvedGraph(
                    registry, rows, registry.Fingerprint, oversized, [root], CancellationToken.None));
            failure.Message.ShouldStartWith("RegistryLimit:");
        });
    }

    /// <summary>Checks exact pinned registry rows, supplied contexts and transitive file bytes.</summary>
    [Fact]
    public void PinnedResolvedGraphChecksEdgesFilesContextsAndCompleteRows()
    {
        string directory = Path.Combine(Path.GetTempPath(), "event-dependency-closure-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            string rootFile = Path.Combine(directory, "root.dll");
            string leafFile = Path.Combine(directory, "leaf.so");
            File.WriteAllBytes(rootFile, [1, 2, 3]);
            File.WriteAllBytes(leafFile, [4, 5, 6]);
            ReadOnlyMemory<byte>[] rows = CreateFixtureRows([
                CreateDependencyRow("root", "managed", "1.0", "locked", File.ReadAllBytes(rootFile)),
                CreateDependencyRow("leaf", "native", "abi-1", "locked", File.ReadAllBytes(leafFile)),
                CreateDependencyRow("nonlocal", "managed", "2.0", "remote", [7]),
            ]);
            using var registry = new EventDomainRegistry("d", rows);
            var root = new EventDependencyIdentity("root", "managed");
            var leaf = new EventDependencyIdentity("leaf", "native");
            EventResolvedDependency[] graph = [
                new(root, "1.0", "locked", rootFile, [leaf]),
                new(leaf, "abi-1", "locked", leafFile, []),
            ];

            EventDependencyClosureVerifier.RequirePinnedResolvedGraph(registry, rows, registry.Fingerprint, graph, [root], CancellationToken.None);
            Should.Throw<InvalidOperationException>(() => EventDependencyClosureVerifier.RequirePinnedResolvedGraph(
                registry, rows[..^1], registry.Fingerprint, graph, [root], CancellationToken.None));
            ReadOnlyMemory<byte>[] changedPin = rows.ToArray();
            changedPin[^1] = CreateDependencyRow("nonlocal", "managed", "2.0", "remote", [8]);
            Should.Throw<InvalidOperationException>(() => EventDependencyClosureVerifier.RequirePinnedResolvedGraph(
                registry, changedPin, registry.Fingerprint, graph, [root], CancellationToken.None));
            Should.Throw<InvalidOperationException>(() => EventDependencyClosureVerifier.RequirePinnedResolvedGraph(
                registry, rows, new string('0', 64), graph, [root], CancellationToken.None));
            Should.Throw<InvalidOperationException>(() => EventDependencyClosureVerifier.RequirePinnedResolvedGraph(
                registry, rows, registry.Fingerprint, graph[..1], [root], CancellationToken.None));
            Should.Throw<InvalidOperationException>(() => EventDependencyClosureVerifier.RequirePinnedResolvedGraph(
                registry, rows, registry.Fingerprint, [graph[0], graph[0], graph[1]], [root], CancellationToken.None));
            Should.Throw<InvalidOperationException>(() => EventDependencyClosureVerifier.RequirePinnedResolvedGraph(
                registry, rows, registry.Fingerprint, [graph[0] with { LoaderContextId = "unlocked" }, graph[1]], [root], CancellationToken.None));
            Should.Throw<OperationCanceledException>(() => EventDependencyClosureVerifier.RequirePinnedResolvedGraph(
                registry, rows, registry.Fingerprint, graph, [root], new CancellationToken(true)));

            File.WriteAllBytes(leafFile, [4, 5, 7]);
            Should.Throw<InvalidOperationException>(() => EventDependencyClosureVerifier.RequirePinnedResolvedGraph(
                registry, rows, registry.Fingerprint, graph, [root], CancellationToken.None));
        }
        finally { Directory.Delete(directory, recursive: true); }
    }

    private static ReadOnlyMemory<byte>[] CreateFixtureRows(ReadOnlyMemory<byte>[] dependencies)
    {
        Dictionary<string, string> fixture = JsonSerializer.Deserialize<Dictionary<string, string>>(
            File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Events", "Fixtures", "EventRegistryV17.json")))!;
        return [Convert.FromHexString(fixture["AliasRow"]), Convert.FromHexString(fixture["DescriptorRow"]),
            Convert.FromHexString(fixture["VersionRow"]), Convert.FromHexString(fixture["SharedRow"]), .. dependencies];
    }

    private static void WithGraphFixture(
        Action<EventDomainRegistry, ReadOnlyMemory<byte>[], EventResolvedDependency[], EventDependencyIdentity> verify)
    {
        string directory = Path.Combine(Path.GetTempPath(), "event-dependency-controls-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            string rootFile = Path.Combine(directory, "root.dll");
            string leafFile = Path.Combine(directory, "leaf.so");
            File.WriteAllBytes(rootFile, [1, 2, 3]);
            File.WriteAllBytes(leafFile, [4, 5, 6]);
            ReadOnlyMemory<byte>[] rows = CreateFixtureRows([
                CreateDependencyRow("root", "managed", "1.0", "declared-context", [1, 2, 3]),
                CreateDependencyRow("leaf", "native", "abi-1", "declared-context", [4, 5, 6]),
            ]);
            using var registry = new EventDomainRegistry("d", rows);
            var root = new EventDependencyIdentity("root", "managed");
            var leaf = new EventDependencyIdentity("leaf", "native");
            EventResolvedDependency[] graph = [
                new(root, "1.0", "declared-context", rootFile, [leaf]),
                new(leaf, "abi-1", "declared-context", leafFile, []),
            ];
            verify(registry, rows, graph, root);
        }
        finally { Directory.Delete(directory, recursive: true); }
    }

    private static byte[] CreateDependencyRow(string identity, string kind, string version, string context, byte[] content)
    {
        using var writer = new EventEvolutionBinaryWriter(4096);
        writer.WriteByte(0x47);
        writer.WriteString("d");
        writer.WriteString(identity);
        writer.WriteString(kind);
        writer.WriteUInt16(3);
        writer.WriteByte(1); writer.WriteString(version);
        writer.WriteByte(2); writer.WriteHash(SHA256.HashData(content));
        writer.WriteByte(3); writer.WriteString(context);
        return writer.CopyEncodedBytes();
    }
}
