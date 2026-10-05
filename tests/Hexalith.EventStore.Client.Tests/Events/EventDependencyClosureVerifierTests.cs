using System.Security.Cryptography;
using System.Text.Json;

using Hexalith.EventStore.Client.Events;

using Shouldly;

namespace Hexalith.EventStore.Client.Tests.Events;

public sealed class EventDependencyClosureVerifierTests
{
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
