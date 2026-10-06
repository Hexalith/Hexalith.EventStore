using System.Security.Cryptography;

namespace Hexalith.EventStore.Client.Events;

/// <summary>Verifies exact bytes for one locally resolved managed or native G dependency.</summary>
/// <remarks>
/// This local check compares bytes at a caller-selected path. It does not authenticate resolved
/// identity or context claims, prove graph completeness, or bind later execution to those bytes.
/// The reviewed deployment must separately preserve immutable artifacts and their execution binding.
/// </remarks>
internal static class EventDependencyFileVerifier
{
    /// <summary>Checks an exact G-row hash against the currently readable caller-selected file.</summary>
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
