using Hexalith.EventStore.Contracts.Events;

namespace Hexalith.EventStore.Client.Tests.Events;

/// <summary>Provides exact fixture callbacks executed from a privately admitted test assembly image.</summary>
internal static class PrivateImageEventCallbacks
{
    /// <summary>Gets the number of actually executed schema callbacks.</summary>
    public static int SchemaCalls { get; private set; }

    /// <summary>Gets the number of actually executed identity callbacks.</summary>
    public static int IdentityCalls { get; private set; }

    /// <summary>Gets the number of actually executed serializer callbacks.</summary>
    public static int DeserializerCalls { get; private set; }

    /// <summary>Validates the exact local fixture scope and counts the actual schema call.</summary>
    public static void ValidateSchema(string domain, string canonicalType, int version, string format,
        IReadOnlyPayload payload, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (domain != "d" || canonicalType != "evt" || version is < 1 or > 2 || format != "json" || payload.Length != 2)
        {
            throw new InvalidOperationException("Unexpected private-image fixture scope.");
        }

        SchemaCalls++;
    }

    /// <summary>Counts the actual fixture identity callback after its schema call.</summary>
    public static void ValidateIdentity(string domain, string canonicalType, int version, string format,
        IReadOnlyPayload payload, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (payload.Length != 2) { throw new InvalidOperationException("Unexpected private-image fixture payload."); }
        IdentityCalls++;
    }

    /// <summary>Returns an object of this same private image's explicitly declared current type.</summary>
    public static object Deserialize(IReadOnlyPayload payload, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (payload.Length != 2) { throw new InvalidOperationException("Unexpected private-image fixture payload."); }
        DeserializerCalls++;
        return new AllowlistedCurrentEventTestValue();
    }
}
