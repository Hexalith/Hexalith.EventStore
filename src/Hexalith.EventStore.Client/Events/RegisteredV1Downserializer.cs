using System.Security.Cryptography;

namespace Hexalith.EventStore.Client.Events;

/// <summary>Binds a V1 callable's direct file/options identity to the exact F descriptor.</summary>
/// <remarks>Expanded options, validator and sealed dependency attestation remain startup prerequisites.</remarks>
internal sealed class RegisteredV1Downserializer
{
    private readonly byte[] _assemblyHash;
    private readonly byte[] _optionsHash;

    /// <summary>Captures the actual implementation file digest and admitted immutable options digest.</summary>
    internal RegisteredV1Downserializer(string implementationId, IV1Downserializer downserializer, byte[] optionsHash)
    {
        ArgumentException.ThrowIfNullOrEmpty(implementationId);
        ArgumentNullException.ThrowIfNull(downserializer);
        ArgumentNullException.ThrowIfNull(optionsHash);
        if (optionsHash.Length != 32)
        {
            throw new ArgumentException("A downserializer options digest must contain exactly 32 bytes.", nameof(optionsHash));
        }

        ImplementationId = implementationId;
        Downserializer = downserializer;
        _optionsHash = optionsHash.ToArray();
        string location = downserializer.GetType().Assembly.Location;
        if (string.IsNullOrEmpty(location))
        {
            throw new ArgumentException("An unresolvable implementation assembly cannot be registered.", nameof(downserializer));
        }

        using FileStream stream = File.OpenRead(location);
        _assemblyHash = SHA256.HashData(stream);
    }

    /// <summary>Gets the exact implementation identity.</summary>
    internal string ImplementationId { get; }

    /// <summary>Gets the allow-listed callable.</summary>
    internal IV1Downserializer Downserializer { get; }

    /// <summary>Requires exact F direct-assembly and options agreement.</summary>
    internal void RequireDescriptor(EventRegistryRow descriptor)
    {
        if (!string.Equals(ImplementationId, descriptor.GetTextField(7), StringComparison.Ordinal)
            || !_assemblyHash.AsSpan().SequenceEqual(descriptor.GetEncodedField(8))
            || !_optionsHash.AsSpan().SequenceEqual(descriptor.GetEncodedField(9)))
        {
            throw new InvalidOperationException("DownserializeRejected: callable identity or options disagree with the F descriptor.");
        }
    }
}
