namespace Hexalith.EventStore.Client.Projections;

/// <summary>
/// Derives the platform-owned, opaque marker key namespace for coordinated read-model batches. The
/// namespace embeds only a hash of the full scope plus batch identity, never raw tenant data or payload.
/// </summary>
internal static class ReadModelBatchKeys {
    /// <summary>The versioned marker key prefix.</summary>
    public const string MarkerPrefix = "hxrmb:v1:marker:";

    /// <summary>Builds the marker/receipt key for a scope hash.</summary>
    /// <param name="scopeHash">The opaque scope hash.</param>
    /// <returns>The marker key.</returns>
    public static string MarkerKey(string scopeHash) => MarkerPrefix + scopeHash;

    /// <summary>Builds the private immutable legacy compensation evidence key in the owning batch namespace.</summary>
    internal static string ScratchCompensationKey(string scopeHash) => MarkerKey(scopeHash) + ":scratch-compensation";

    /// <summary>Builds the private immutable manifest key within this same coordinated batch namespace.</summary>
    internal static string ManifestKey(string scopeHash) => MarkerKey(scopeHash) + ":manifest";
}
