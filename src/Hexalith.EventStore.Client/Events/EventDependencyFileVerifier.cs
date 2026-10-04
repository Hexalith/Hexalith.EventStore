using System.Security.Cryptography;

namespace Hexalith.EventStore.Client.Events;

/// <summary>Verifies exact bytes for one locally resolved managed or native G dependency.</summary>
/// <remarks>This check does not prove graph completeness, a loader context, or manifest attestation.</remarks>
internal static class EventDependencyFileVerifier
{
    /// <summary>Checks a G row against the file selected by the locked deployment loader.</summary>
    internal static void RequireExactFile(EventRegistryRow dependency, string resolvedFile, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(dependency);
        ArgumentException.ThrowIfNullOrEmpty(resolvedFile);
        if (dependency.Tag != 0x47)
        {
            throw new ArgumentException("A dependency byte check requires an exact G row.", nameof(dependency));
        }

        cancellationToken.ThrowIfCancellationRequested();
        using FileStream stream = File.OpenRead(resolvedFile);
        using IncrementalHash hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        byte[] buffer = new byte[64 * 1024];
        byte[]? digest = null;
        try
        {
            int read;
            while ((read = stream.Read(buffer)) != 0)
            {
                cancellationToken.ThrowIfCancellationRequested();
                hash.AppendData(buffer.AsSpan(0, read));
            }

            cancellationToken.ThrowIfCancellationRequested();
            digest = hash.GetHashAndReset();
            if (!digest.AsSpan().SequenceEqual(dependency.GetEncodedField(2)))
            {
                throw new InvalidOperationException("CapabilityMismatch: dependency bytes disagree with the sealed G row.");
            }
        }
        finally
        {
            CryptographicOperations.ZeroMemory(buffer);
            if (digest is not null) { CryptographicOperations.ZeroMemory(digest); }
        }
    }
}
