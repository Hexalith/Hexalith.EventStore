using System.Text.Json;

using Hexalith.EventStore.Client.Registration;

namespace P1R.Counter;

/// <summary>Registers a pinned qualification-only alias manifest without gateway authority.</summary>
internal static class FixtureEvolutionManifest
{
    /// <summary>The distinct historical event name used by the replay probe.</summary>
    internal const string LegacyAlias = "P1R.Legacy.CounterIncremented";

    /// <summary>The independently reviewed fingerprint for the tracked five-row fixture.</summary>
    internal const string PinnedFingerprint = "5a5748913258b5832b333fe507bc689f1ac9e53a9201b4bb5cb983b535da933b";

    /// <summary>Loads and registers the exact tracked test fixture manifest.</summary>
    internal static string Register(IServiceCollection services)
    {
        using JsonDocument document = JsonDocument.Parse(File.ReadAllBytes(
            Path.Combine(AppContext.BaseDirectory, "fixture-evolution-manifest.json")));
        JsonElement root = document.RootElement;
        if (root.GetProperty("test_only").GetBoolean() is not true
            || root.GetProperty("gateway_authority").GetBoolean() is not false
            || root.GetProperty("domain").GetString() != "counter"
            || root.GetProperty("legacy_alias").GetString() != LegacyAlias)
        {
            throw new InvalidOperationException("The qualification manifest lacks its test-only boundary.");
        }

        ReadOnlyMemory<byte>[] rows = root.GetProperty("rows").EnumerateArray()
            .Select(static row => (ReadOnlyMemory<byte>)Convert.FromHexString(row.GetString()!)).ToArray();
        services.AddEventStoreEventEvolutionManifestCandidate("counter", rows, PinnedFingerprint);
        return PinnedFingerprint;
    }
}
