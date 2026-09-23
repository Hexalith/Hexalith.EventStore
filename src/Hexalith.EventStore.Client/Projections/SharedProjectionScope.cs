using System.Security.Cryptography;
using System.Text;

namespace Hexalith.EventStore.Client.Projections;

/// <summary>Names one tenant/domain projection family and its admitted ordinary writers.</summary>
public sealed record SharedProjectionScope(
    string StoreName,
    string TenantId,
    string Domain,
    string Family,
    IReadOnlyList<string> RequiredWriters)
{
    /// <summary>Validates the durable fence boundary.</summary>
    public void Validate()
    {
        new ReadModelBatchScope(StoreName, TenantId, Domain, Family, Family, "shared-epoch").Validate();
        ArgumentNullException.ThrowIfNull(RequiredWriters);
        if (RequiredWriters.Count is 0 or > 64
            || RequiredWriters.Any(string.IsNullOrWhiteSpace)
            || RequiredWriters.Any(writer => Encoding.UTF8.GetByteCount(writer) > ReadModelBatchScope.MaxComponentByteLength)
            || RequiredWriters.Distinct(StringComparer.Ordinal).Count() != RequiredWriters.Count)
        {
            throw new ArgumentException("The shared projection requires distinct admitted writers.", nameof(RequiredWriters));
        }
    }

    /// <summary>Computes the opaque durable namespace for the tenant/family.</summary>
    public string ComputeHash()
    {
        Validate();
        var scope = new ReadModelBatchScope(StoreName, TenantId, Domain, Family, Family, "shared-epoch");
        return scope.ComputeScopeHash();
    }

    internal string StateKey => "shared-epoch:" + ComputeHash();

    internal string PhysicalKey(long generation, string logicalKey)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(logicalKey);
        byte[] digest = SHA256.HashData(Encoding.UTF8.GetBytes(logicalKey));
        return "shared-generation:" + ComputeHash() + ":" + generation.ToString(System.Globalization.CultureInfo.InvariantCulture)
            + ":" + Convert.ToHexString(digest);
    }

    internal string ReceiptKey(long epoch, string sourceStream, long position)
    {
        byte[] digest = SHA256.HashData(Encoding.UTF8.GetBytes(sourceStream + "\0" + position.ToString(System.Globalization.CultureInfo.InvariantCulture)));
        return "shared-receipt:" + ComputeHash() + ":" + epoch.ToString(System.Globalization.CultureInfo.InvariantCulture)
            + ":" + Convert.ToHexString(digest);
    }

    internal string CheckpointKey(string sourceStream)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceStream);
        byte[] digest = SHA256.HashData(Encoding.UTF8.GetBytes(sourceStream));
        return "shared-checkpoint:" + ComputeHash() + ":" + Convert.ToHexString(digest);
    }
}
