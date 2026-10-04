using System.Security.Cryptography;

namespace Hexalith.EventStore.Client.Events;

/// <summary>Binds an explicit callable to exact implementation identity, file bytes and canonical declared options.</summary>
/// <remarks>Actual runtime setting equality and sealed dependency graph attestation remain separate requirements.</remarks>
internal sealed class EventImplementationBinding
{
    private readonly string _implementationId;
    private readonly byte[] _assemblyHash;
    private readonly byte[] _optionsHash;

    /// <summary>Computes direct file and expanded schema hashes before callable use.</summary>
    internal EventImplementationBinding(string implementationId, Delegate implementation, ReadOnlyMemory<byte> canonicalOptions,
        IReadOnlyList<EventOptionRule> optionSchema)
    {
        ArgumentException.ThrowIfNullOrEmpty(implementationId);
        ArgumentNullException.ThrowIfNull(implementation);
        if (implementation.GetInvocationList().Length != 1)
        {
            throw new ArgumentException("A registered implementation must be one explicit callable.", nameof(implementation));
        }
        _implementationId = implementationId;
        _optionsHash = EventOptionsManifestCodec.ComputeHash(canonicalOptions, optionSchema);
        string location = implementation.Method.Module.Assembly.Location;
        if (string.IsNullOrEmpty(location))
        {
            throw new ArgumentException("An implementation file must be resolvable.", nameof(implementation));
        }

        using FileStream stream = File.OpenRead(location);
        _assemblyHash = SHA256.HashData(stream);
    }

    /// <summary>Requires the exact adjacent ID, assembly and options fields from a decoded descriptor.</summary>
    internal void RequireFields(EventRegistryRow descriptor, int implementationField)
    {
        ArgumentNullException.ThrowIfNull(descriptor);
        if (!string.Equals(_implementationId, descriptor.GetTextField(implementationField), StringComparison.Ordinal)
            || !_assemblyHash.AsSpan().SequenceEqual(descriptor.GetEncodedField(implementationField + 1))
            || !_optionsHash.AsSpan().SequenceEqual(descriptor.GetEncodedField(implementationField + 2)))
        {
            throw new InvalidOperationException("CapabilityMismatch: implementation bytes or options disagree with the descriptor.");
        }
    }
}
