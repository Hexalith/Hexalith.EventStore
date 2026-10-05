namespace Hexalith.EventStore.Server.Control;

/// <summary>Preflights and orders one declared participant set before the first SQL write.</summary>
/// <remarks>Family decoding, authority, quotas, counters and signed evidence remain mandatory admission inputs.</remarks>
internal sealed class PostgreSqlControlTransactionPlan
{
    private static readonly IReadOnlyDictionary<string, string> AddressPrefixes = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["control/execution"] = "publication-resume-state:",
        ["control/held"] = "held-delivery:",
        ["control/queue"] = "pin-capacity-queue:",
        ["control/queue-shard"] = "pin-capacity-queue-shard:",
        ["control/registry"] = "owner-registry-shard:",
        ["control/registry-entry"] = "owner-registry-entry:",
        ["control/registry-scope"] = "owner-registry-scope:",
        ["control/epoch"] = "operations-epoch:",
    };

    internal PostgreSqlControlTransactionPlan(IReadOnlyList<PostgreSqlControlMutation> mutations)
    {
        ArgumentNullException.ThrowIfNull(mutations);
        if (mutations.Count < 1 || checked(mutations.Count * 512L) > 128L * 1024 * 1024)
        {
            throw new InvalidOperationException("RegistryLimit: an empty or unbounded metadata participant set cannot be admitted.");
        }
        PostgreSqlControlMutation[] snapshot = mutations.ToArray();
        foreach (PostgreSqlControlMutation mutation in snapshot) { ArgumentNullException.ThrowIfNull(mutation); }
        Array.Sort(snapshot, static (left, right) => StringComparer.Ordinal.Compare(left.Address, right.Address));
        string? previous = null;
        foreach (PostgreSqlControlMutation mutation in snapshot)
        {
            ArgumentNullException.ThrowIfNull(mutation);
            if (!AddressPrefixes.TryGetValue(mutation.Family, out string? prefix))
            {
                throw new InvalidOperationException("CapabilityMismatch: no complete control family admission is available for this address.");
            }
            PostgreSqlControlCaps.RequireFramedAddress(mutation.Address, prefix);
            if (string.Equals(previous, mutation.Address, StringComparison.Ordinal))
            {
                throw new InvalidOperationException("CapabilityMismatch: a metadata address appears twice in one transaction.");
            }
            previous = mutation.Address;
            if (mutation.Expected is null && mutation.Next is null)
            {
                throw new InvalidOperationException("CapabilityMismatch: a metadata mutation has no predecessor or result.");
            }
            RequireImage(mutation.Family, mutation.Expected);
            RequireImage(mutation.Family, mutation.Next);
            if (mutation.Expected is null)
            {
                if (mutation.OwnershipTransfer || mutation.Next!.Generation != 0)
                {
                    throw new InvalidOperationException("CapabilityMismatch: a created metadata row must start at generation zero with a new fence.");
                }
            }
            else if (mutation.Next is null)
            {
                if (mutation.OwnershipTransfer)
                {
                    throw new InvalidOperationException("CapabilityMismatch: deletion cannot mint ownership authority.");
                }
            }
            else if (mutation.Expected.Generation == ulong.MaxValue
                || mutation.Next.Generation != mutation.Expected.Generation + 1
                || mutation.OwnershipTransfer == string.Equals(
                    mutation.Next.OwnerFence, mutation.Expected.OwnerFence, StringComparison.Ordinal))
            {
                throw new InvalidOperationException("CapabilityMismatch: metadata generation or ownership fence did not advance exactly.");
            }
        }
        Mutations = snapshot;
    }

    internal IReadOnlyList<PostgreSqlControlMutation> Mutations { get; }

    private static void RequireImage(string family, PostgreSqlControlRowImage? image)
    {
        if (image is null) { return; }
        ArgumentNullException.ThrowIfNull(image.Payload);
        PostgreSqlOwnerFence.RequireCanonical(image.OwnerFence);
        PostgreSqlControlCaps.RequireLength(family, image.Payload.Length);
        if ((family == "control/registry-entry") != (image.RegistryIndex is not null))
        {
            throw new InvalidOperationException("CapabilityMismatch: registry index columns must be present only on an entry row.");
        }
    }
}
