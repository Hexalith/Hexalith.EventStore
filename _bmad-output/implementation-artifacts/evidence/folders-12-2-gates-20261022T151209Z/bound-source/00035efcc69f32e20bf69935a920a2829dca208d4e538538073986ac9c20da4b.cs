using System.Security.Cryptography;
using System.Text.Json;

using Hexalith.EventStore.Client.Events;
using Hexalith.EventStore.Client.Registration;

using Microsoft.Extensions.DependencyInjection;

using Shouldly;

namespace Hexalith.EventStore.Client.Tests.Registration;

/// <summary>Checks bounded manifest candidate registration without readiness or activation authority.</summary>
public sealed class EventEvolutionManifestRegistrationTests
{
    /// <summary>Checks malformed manifests refuse before a candidate enters the service collection.</summary>
    /// <param name="invalidManifest">The malformed manifest shape to exercise.</param>
    [Theory]
    [InlineData("foreign-domain")]
    [InlineData("duplicate-dependency")]
    [InlineData("invalid-kind")]
    [InlineData("row-count")]
    [InlineData("row-size")]
    public void InvalidManifestRefusesWithoutRegisteringACandidate(string invalidManifest)
    {
        ReadOnlyMemory<byte>[] rows = invalidManifest switch
        {
            "foreign-domain" => FixtureRows(),
            "duplicate-dependency" => [.. FixtureRows(), DependencyRow([1]), DependencyRow([2])],
            "invalid-kind" => [.. FixtureRows(), DependencyRow([1], "Managed")],
            "row-count" => Enumerable.Repeat(FixtureRows()[0], 65_537).ToArray(),
            "row-size" => [new byte[64 * 1024 + 1]],
            _ => throw new ArgumentOutOfRangeException(nameof(invalidManifest)),
        };
        var services = new ServiceCollection();

        Should.Throw<ArgumentException>(() => services.AddEventStoreEventEvolutionManifestCandidate(
            invalidManifest == "foreign-domain" ? "another-domain" : "d", rows, new string('0', 64)));
        services.Count.ShouldBe(0);
    }

    /// <summary>Checks candidate pin ownership, compatibility and duplicate registration refusal.</summary>
    [Fact]
    public void RegistrationPinsAnOwnedManifestAndRejectsInvalidOrDuplicateCandidates()
    {
        ReadOnlyMemory<byte>[] rows = FixtureRows();
        byte[] mutableRow = rows[0].ToArray();
        rows[0] = mutableRow;
        string fingerprint = Convert.ToHexStringLower(EventRegistryFingerprintCodec.Compute("d", rows));
        var services = new ServiceCollection();

        Should.Throw<InvalidOperationException>(() => services.AddEventStoreEventEvolutionManifestCandidate(
            "d", rows, new string('0', 64)));
        services.Count.ShouldBe(0);
        Should.Throw<ArgumentException>(() => services.AddEventStoreEventEvolutionManifestCandidate(
            "d", rows, fingerprint.ToUpperInvariant()));
        services.Count.ShouldBe(0);
        Should.Throw<ArgumentException>(() => services.AddEventStoreEventEvolutionManifestCandidate(
            "d", rows, fingerprint, 64L * 1024 * 1024));
        services.Count.ShouldBe(0);

        _ = services.AddEventStoreEventEvolutionManifestCandidate("d", rows, fingerprint);
        Should.Throw<InvalidOperationException>(() => services.AddEventStoreEventEvolutionManifestCandidate("d", rows, fingerprint));
        services.Count.ShouldBe(1);

        mutableRow[0] = 0; // The registry must own a copy of every admitted row.
        using ServiceProvider provider = services.BuildServiceProvider();
        EventEvolutionManifestCandidate candidate = provider.GetRequiredKeyedService<EventEvolutionManifestCandidate>("d");
        candidate.Registry.Fingerprint.ShouldBe(fingerprint);
        provider.GetKeyedService<EventEvolutionManifestCandidate>("another-domain").ShouldBeNull();
        using ServiceProvider secondProvider = services.BuildServiceProvider();
        provider.Dispose();
        secondProvider.GetRequiredKeyedService<EventEvolutionManifestCandidate>("d")
            .Registry.GetCurrentVersion("evt").ShouldBe(1);
    }

    private static ReadOnlyMemory<byte>[] FixtureRows()
    {
        Dictionary<string, string> fixture = JsonSerializer.Deserialize<Dictionary<string, string>>(
            File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Events", "Fixtures", "EventRegistryV17.json")))!;
        return [Convert.FromHexString(fixture["AliasRow"]), Convert.FromHexString(fixture["DescriptorRow"]),
            Convert.FromHexString(fixture["VersionRow"]), Convert.FromHexString(fixture["SharedRow"])];
    }

    private static byte[] DependencyRow(byte[] content, string kind = "managed")
    {
        using var writer = new EventEvolutionBinaryWriter(4096);
        writer.WriteByte(0x47);
        writer.WriteString("d");
        writer.WriteString("domain");
        writer.WriteString(kind);
        writer.WriteUInt16(3);
        writer.WriteByte(1); writer.WriteString("1.0");
        writer.WriteByte(2); writer.WriteHash(SHA256.HashData(content));
        writer.WriteByte(3); writer.WriteString("locked");
        return writer.CopyEncodedBytes();
    }
}
