// Effective authority: 08bfd5855998a276999db38447c409b2231f3d4e253d4e371d4d8bfe369462d6; base sections 10.2 and 13; amendment sections 1-2.
using System.Buffers.Binary;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Hexalith.EventStore.Contracts.Identity;

namespace Hexalith.EventStore.PayloadProtection;

/// <summary>Builds the exact framed digests and state keys without prefix scans or display fingerprints.</summary>
internal static class PayloadProtectionStateKeys
{
    /// <summary>Gets the approved effective authority digest.</summary>
    internal const string AuthorityDigest = "08bfd5855998a276999db38447c409b2231f3d4e253d4e371d4d8bfe369462d6";
    private const string Prefix = "eventstore:pp:v2:";

    /// <summary>Computes the injectively framed full scope digest.</summary>
    internal static string ScopeDigest(AggregateIdentity identity) => IdentityDigest("HXSC", identity.TenantId, identity.Domain, identity.AggregateId);
    /// <summary>Computes the injectively framed tenant/domain digest.</summary>
    internal static string TenantDomainDigest(string tenant, string domain) => IdentityDigest("HXTD", tenant, domain);
    /// <summary>Computes a backend/provider address digest.</summary>
    internal static string Digest(string value) => Convert.ToHexStringLower(SHA256.HashData(CanonicalText.Encode(value, 1, 4096)));
    /// <summary>Builds a wrapped-key address.</summary>
    internal static string Key(string scope, string keyRef, uint version) => Prefix + "key:" + Sha(scope) + ":" + Ulid(keyRef) + ":" + Version(version);
    /// <summary>Builds a scope index head address.</summary>
    internal static string IndexHead(string scope) => Prefix + "index-head:" + Sha(scope);
    /// <summary>Builds a scope index page address.</summary>
    internal static string IndexPage(string scope, uint page) => Prefix + "index:" + Sha(scope) + ":" + Page(page);
    /// <summary>Builds a KEK inventory head address.</summary>
    internal static string KekHead(string backend, string provider) => Prefix + "kek-index-head:" + Sha(backend) + ":" + Sha(provider);
    /// <summary>Builds a KEK inventory page address.</summary>
    internal static string KekPage(string backend, string provider, uint page) => Prefix + "kek-index:" + Sha(backend) + ":" + Sha(provider) + ":" + Page(page);
    /// <summary>Builds a lifecycle address.</summary>
    internal static string Lifecycle(string scope) => Prefix + "lifecycle:" + Sha(scope);
    /// <summary>Builds a fence address.</summary>
    internal static string Fence(string root) => Prefix + "fence:" + Sha(root);
    /// <summary>Builds a deployment address.</summary>
    internal static string Deployment(string root) => Prefix + "deployment:" + Sha(root);
    /// <summary>Builds an exact capability lease address.</summary>
    internal static string Capability(string root, ulong generation, string instance) => Prefix + "capability:" + Sha(root) + ":" + generation.ToString("D20", CultureInfo.InvariantCulture) + ":" + Ulid(instance);
    /// <summary>Builds an operation address.</summary>
    internal static string Operation(string operation) => Prefix + "operation:" + Ulid(operation);
    /// <summary>Builds a read lease address.</summary>
    internal static string Lease(string scope, string operation) => Prefix + "lease:" + Sha(scope) + ":" + Ulid(operation);
    /// <summary>Builds a completion lease address.</summary>
    internal static string Completion(string scope, string keyRef, uint version, string operation) => Prefix + "completion:" + Sha(scope) + ":" + Ulid(keyRef) + ":" + Version(version) + ":" + Ulid(operation);
    /// <summary>Builds a reconciliation address.</summary>
    internal static string Reconciliation(string operation) => Prefix + "reconcile:" + Ulid(operation);
    /// <summary>Builds an audit address, using the operation ID when a workflow is absent.</summary>
    internal static string Audit(string workflowOrOperation, string audit) => Prefix + "audit:" + Ulid(workflowOrOperation) + ":" + Ulid(audit);
    /// <summary>Builds a member head address.</summary>
    internal static string MemberHead(string root) => Prefix + "member-head:" + Sha(root);
    /// <summary>Builds a bounded member page address.</summary>
    internal static string MemberPage(string root, uint page) => Prefix + "members:" + Sha(root) + ":" + CatalogPage(page);
    /// <summary>Builds a work head address.</summary>
    internal static string WorkHead(string root) => Prefix + "work-head:" + Sha(root);
    /// <summary>Builds a bounded work page address.</summary>
    internal static string WorkPage(string root, uint page) => Prefix + "work:" + Sha(root) + ":" + CatalogPage(page);
    /// <summary>Builds an immutable drain address.</summary>
    internal static string Drain(string scope, string operation) => Prefix + "drain:" + Sha(scope) + ":" + Ulid(operation);
    /// <summary>Builds an explicit persistent clearing acknowledgement address.</summary>
    internal static string ClearAck(string scope, ulong epoch, string instance) => Prefix + "clear-ack:" + Sha(scope) + ":" + epoch.ToString(CultureInfo.InvariantCulture) + ":" + Ulid(instance);
    /// <summary>Validates a full lowercase SHA-256 address.</summary>
    internal static string Sha(string value) => IsHex(value, 64) ? value : throw new PayloadProtectionFormatException();
    /// <summary>Checks canonical lowercase hexadecimal.</summary>
    internal static bool IsHex(string? value, int length) => value is not null && value.Length == length && value.All(c => c is >= '0' and <= '9' or >= 'a' and <= 'f');

    private static string Ulid(string value) => CanonicalUlid.IsValid(value) ? value : throw new PayloadProtectionFormatException();
    private static string Version(uint value) => value > 0 ? value.ToString(CultureInfo.InvariantCulture) : throw new PayloadProtectionFormatException();
    private static string Page(uint value) => value.ToString("D10", CultureInfo.InvariantCulture);
    private static string CatalogPage(uint value) => value < 256 ? Page(value) : throw new PayloadProtectionFormatException();

    private static string IdentityDigest(string magic, params string[] values)
    {
        using var bytes = new MemoryStream();
        bytes.Write(Encoding.ASCII.GetBytes(magic));
        bytes.WriteByte(1);
        Span<byte> length = stackalloc byte[4];
        for (int i = 0; i < values.Length; i++)
        {
            byte[] value = CanonicalText.Encode(values[i], 1, i == 1 ? 128 : 256);
            bytes.WriteByte(checked((byte)(i + 1)));
            bytes.WriteByte(1);
            BinaryPrimitives.WriteUInt32BigEndian(length, checked((uint)value.Length));
            bytes.Write(length);
            bytes.Write(value);
        }

        return Convert.ToHexStringLower(SHA256.HashData(bytes.ToArray()));
    }
}
