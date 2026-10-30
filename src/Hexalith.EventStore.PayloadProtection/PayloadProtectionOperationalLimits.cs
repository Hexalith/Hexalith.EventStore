// Effective authority: eb45653553efe53ba83e6dd6c9796b47dfb6fb0bba0628ff95f6bc1b96b054c9; qualified Dapr transport operating limits within structural schema caps.
using System.Text;

namespace Hexalith.EventStore.PayloadProtection;

/// <summary>Conservative supported limits for the qualified default four-MiB gRPC transport; larger structural schemas grant no transport admission.</summary>
internal static class PayloadProtectionOperationalLimits
{
    /// <summary>Maximum charged transaction bytes, including a conservative per-operation protobuf/metadata allowance.</summary>
    internal const int TransactionBytes = 2 * 1024 * 1024;
    /// <summary>Maximum operations in one qualified transaction.</summary>
    internal const int TransactionOperations = 512;
    /// <summary>Maximum cumulative strong-read bytes observed by one enclosing engine phase.</summary>
    internal const long ReadBytes = 16 * 1024 * 1024;
    /// <summary>Maximum strong-read calls observed by one enclosing engine phase.</summary>
    internal const int ReadOperations = 4096;
    /// <summary>Charges values, exact keys and ETags plus 1024 bytes of framing/metadata allowance per operation.</summary>
    internal static int Charge(PayloadProtectionStateMutation mutation)
    {
        if (mutation.ExpectedETag is { Length: > 128 }) { throw new PayloadProtectionFormatException(); }
        return checked(1024 + Encoding.UTF8.GetByteCount(mutation.Key) + (mutation.Bytes?.Length ?? 0)
            + (mutation.ExpectedETag is null ? 0 : Encoding.UTF8.GetByteCount(mutation.ExpectedETag)));
    }

    /// <summary>Rejects an oversized plan before transport submission with a retryable, bounded admission result.</summary>
    internal static void RequireTransactionCapacity(IReadOnlyList<PayloadProtectionStateMutation> mutations)
    {
        if (mutations.Count > TransactionOperations || mutations.Sum(m => (long)Charge(m)) + 1024 > TransactionBytes)
        { throw new PayloadProtectionStateException(Hexalith.EventStore.Contracts.Security.UnreadableProtectedDataReason.ProviderUnavailable, isAdmissionLimit: true); }
    }
    /// <summary>Checks the actual member-catalog clearing working set; Cleared tombstones need no acknowledgement read.</summary>
    internal static void RequireClearingCapacity(MemberCatalogSnapshot members, string newInstance, bool append)
    {
        long bytes = members.Head.Bytes.Length * 2L + members.Pages.Sum(p => (long)p.Bytes.Length);
        int reads = members.Pages.Count + 2;
        foreach (MemberEntryV1 entry in members.Pages.SelectMany(p => p.Value.Entries).Where(e => e.State != "Cleared"))
        {
            // These fixed-width maximum encodings safely bound a future immutable acknowledgement.
            var ack = new CacheClearAckV1(new('f', 64), "7ZZZZZZZZZZZZZZZZZZZZZZZZZ", ulong.MaxValue, entry.InstanceId, "9999-12-31T23:59:59.9999999Z");
            bytes += PayloadProtectionStateCodec.Encode(ack).Length; reads++;
        }
        if (append) { bytes += 1024; reads += 2; _ = PayloadProtectionStateKeys.Capability(members.Head.Value.TenantDomainDigest, 0, newInstance); }
        // Reserve room for the lifecycle, claim, drain, fence and one indexed-key work phase.
        if (bytes + 1024 * 1024 > ReadBytes || reads + 512 > ReadOperations)
        { throw new PayloadProtectionStateException(Hexalith.EventStore.Contracts.Security.UnreadableProtectedDataReason.ProviderUnavailable); }
    }
}
