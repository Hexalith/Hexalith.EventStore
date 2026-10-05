using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;

namespace Hexalith.EventStore.Server.Control;

/// <summary>Derived byte-preserving index columns for one addressed owner-registry entry.</summary>
/// <remarks>The caller must independently decode and authenticate the canonical entry and its retained address.</remarks>
internal sealed class PostgreSqlRegistryIndex
{
    private static readonly UTF8Encoding StrictUtf8 = new(false, true);

    internal PostgreSqlRegistryIndex(ReadOnlySpan<byte> deployment, ReadOnlySpan<byte> scopeKind,
        ReadOnlySpan<byte> scopeId, ReadOnlySpan<byte> subject, long firstTicks)
    {
        RequireIdentifier(deployment);
        RequireIdentifier(scopeId);
        RequireIdentifier(subject);
        if (!scopeKind.SequenceEqual("tenant"u8) && !scopeKind.SequenceEqual("deployment"u8))
        {
            throw new InvalidOperationException("CapabilityMismatch: unknown owner-registry scope kind.");
        }
        Deployment = deployment.ToArray();
        ScopeKind = scopeKind.ToArray();
        ScopeId = scopeId.ToArray();
        Subject = subject.ToArray();
        FirstTicks = firstTicks;
        DeploymentHash = SHA256.HashData(Frame(Deployment));
        ScopeHash = SHA256.HashData(Frame(Deployment, ScopeKind, ScopeId));
        Shard = SHA256.HashData(Frame(ScopeKind, ScopeId))[0];
    }

    internal byte[] Deployment { get; }
    internal byte[] ScopeKind { get; }
    internal byte[] ScopeId { get; }
    internal byte[] Subject { get; }
    internal long FirstTicks { get; }
    internal byte Shard { get; }
    internal byte[] DeploymentHash { get; }
    internal byte[] ScopeHash { get; }

    private static void RequireIdentifier(ReadOnlySpan<byte> value)
    {
        if (value.Length is < 1 or > 1024)
        {
            throw new InvalidOperationException("RegistryLimit: a registry index identifier exceeds its exact byte range.");
        }
        _ = StrictUtf8.GetString(value);
    }

    private static byte[] Frame(params byte[][] values)
    {
        int length = 0;
        foreach (byte[] value in values) { length = checked(length + 4 + value.Length); }
        byte[] output = new byte[length];
        int offset = 0;
        foreach (byte[] value in values)
        {
            BinaryPrimitives.WriteUInt32BigEndian(output.AsSpan(offset, 4), checked((uint)value.Length));
            offset += 4;
            value.CopyTo(output.AsSpan(offset));
            offset += value.Length;
        }
        return output;
    }
}
