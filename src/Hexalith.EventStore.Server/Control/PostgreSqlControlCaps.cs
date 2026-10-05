using System.Collections.Frozen;
using System.Text;

namespace Hexalith.EventStore.Server.Control;

/// <summary>Exact AD-13 normative byte ceilings; a row-family decoder must also validate each admitted payload.</summary>
internal static class PostgreSqlControlCaps
{
    private static readonly UTF8Encoding StrictUtf8 = new(false, true);

    internal static readonly FrozenDictionary<string, int> MaximumBytes = new Dictionary<string, int>(StringComparer.Ordinal)
    {
        ["record/D06-activation"] = 1048576,
        ["record/D12-legacy-claim"] = 8192,
        ["record/D12-cutover"] = 4096,
        ["record/D12-usage"] = 2048,
        ["record/D12-tombstone"] = 4096,
        ["record/D14-drain-limit"] = 4096,
        ["record/D14-drain-resolution"] = 4096,
        ["record/D16-membership-resolution"] = 16384,
        ["record/D29-capability"] = 65536,
        ["record/D29-charge"] = 4096,
        ["record/D29-counter"] = 4096,
        ["record/D29-pin-batch"] = 65536,
        ["record/D36-policy"] = 16384,
        ["record/D36-quarantine"] = 131072,
        ["record/D36-redrive"] = 3072,
        ["record/D45-carrier"] = 2048,
        ["record/D45-request"] = 4096,
        ["record/D45-attempt-set"] = 67108864,
        ["record/D45-closure"] = 65536,
        ["record/D45-window"] = 16384,
        ["record/D45-audit"] = 4096,
        ["record/D46-chunk"] = 65536,
        ["record/D46-capsule"] = 131072,
        ["record/D17-destination-config"] = 65536,
        ["control/execution"] = 786432,
        ["control/held"] = 131072,
        ["control/queue"] = 16384,
        ["control/queue-shard"] = 104857600,
        ["control/registry"] = 16384,
        ["control/registry-entry"] = 65536,
        ["control/registry-scope"] = 16384,
        ["control/epoch"] = 16384,
        ["control/cursor"] = 16384,
        ["public/cursor-envelope"] = 16384,
        ["public/held-redrive-request"] = 4096,
        ["public/held-redrive-202"] = 4096,
    }.ToFrozenDictionary(StringComparer.Ordinal);

    /// <summary>Rejects an unknown family or a zero/oversize complete encoded payload before persistence.</summary>
    internal static void RequireLength(string family, long encodedBytes)
    {
        ArgumentException.ThrowIfNullOrEmpty(family);
        if (!MaximumBytes.TryGetValue(family, out int ceiling))
        {
            throw new InvalidOperationException("CapabilityMismatch: unknown AD-13 metadata family.");
        }
        if (encodedBytes < 1 || encodedBytes > ceiling)
        {
            throw new InvalidOperationException("RegistryLimit: complete metadata family exceeds its exact byte ceiling.");
        }
    }

    /// <summary>Checks an already-derived addressed key's exact retained prefix and lowercase hash representation.</summary>
    /// <remarks>This format check does not derive or authenticate the key from its decoded family fields.</remarks>
    internal static void RequireFramedAddress(string address, string prefix)
    {
        ArgumentException.ThrowIfNullOrEmpty(address);
        ArgumentException.ThrowIfNullOrEmpty(prefix);
        if (StrictUtf8.GetByteCount(address) > 256 || !address.StartsWith(prefix, StringComparison.Ordinal)
            || address.Length != prefix.Length + 64)
        {
            throw new InvalidOperationException("CapabilityMismatch: metadata address is not the exact retained framed key.");
        }
        foreach (char digit in address.AsSpan(prefix.Length))
        {
            if (digit is not (>= '0' and <= '9' or >= 'a' and <= 'f'))
            {
                throw new InvalidOperationException("CapabilityMismatch: metadata address has a noncanonical digest.");
            }
        }
    }

    /// <summary>Checks the fixed eight-shard queue placement and per-shard row ceiling.</summary>
    internal static void RequireQueueShard(int shard, int rowCount)
    {
        if (shard is < 0 or > 7 || rowCount is < 0 or > 6400)
        {
            throw new InvalidOperationException("AppendPreparationLimit: queue shard or row count is outside its admitted range.");
        }
    }
}
