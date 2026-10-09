// Normative authority: de9ba8866fd98a480629890ee2b89a492fbad96d4d5a927388e6aaa0fdd72b4e; sections 6.1 and 12.3.
using System.Diagnostics.CodeAnalysis;

namespace Hexalith.EventStore.PayloadProtection;

/// <summary>
/// Resolves stored v2 snapshot type identifiers and aliases to exactly one registration (normative section 6.1).
/// </summary>
/// <remarks>
/// Construction fails when an identifier or alias is outside the closed grammar, when any identifier or alias
/// collides with another one (including an alias equal to its own or another current identifier), or when two
/// registrations share one CLR type. Aliases map directly to their registration, so alias cycles cannot form.
/// </remarks>
internal sealed class SnapshotTypeRegistry
{
    private readonly Dictionary<string, SnapshotTypeRegistration> _byIdentifier;

    /// <summary>
    /// Initializes a validated registry.
    /// </summary>
    /// <param name="registrations">The snapshot type registrations.</param>
    /// <exception cref="ArgumentException">A registration is null, invalid, or collides with another one.</exception>
    internal SnapshotTypeRegistry(IEnumerable<SnapshotTypeRegistration> registrations)
    {
        ArgumentNullException.ThrowIfNull(registrations);
        var byIdentifier = new Dictionary<string, SnapshotTypeRegistration>(StringComparer.Ordinal);
        var clrTypes = new HashSet<Type>();
        var ordered = new List<SnapshotTypeRegistration>();
        foreach (SnapshotTypeRegistration? registration in registrations)
        {
            if (registration?.TypeInfo is null || registration.Aliases is null)
            {
                throw new ArgumentException("A snapshot type registration is incomplete.", nameof(registrations));
            }

            if (!clrTypes.Add(registration.TypeInfo.Type))
            {
                throw new ArgumentException("Two snapshot type registrations share one CLR type.", nameof(registrations));
            }

            Add(byIdentifier, registration.SnapshotTypeId, registration);
            foreach (string alias in registration.Aliases)
            {
                Add(byIdentifier, alias, registration);
            }

            ordered.Add(registration);
        }

        _byIdentifier = byIdentifier;
        Registrations = ordered.AsReadOnly();
    }

    /// <summary>Gets the registrations in their configured order.</summary>
    internal IReadOnlyList<SnapshotTypeRegistration> Registrations { get; }

    /// <summary>
    /// Resolves one stored current identifier or historical alias by exact ordinal comparison.
    /// </summary>
    /// <param name="snapshotTypeId">The stored identifier.</param>
    /// <param name="registration">The resolved registration.</param>
    /// <returns><see langword="true"/> when the identifier is registered.</returns>
    internal bool TryResolve(string? snapshotTypeId, [NotNullWhen(true)] out SnapshotTypeRegistration? registration)
    {
        registration = null;
        return snapshotTypeId is not null && _byIdentifier.TryGetValue(snapshotTypeId, out registration);
    }

    private static void Add(
        Dictionary<string, SnapshotTypeRegistration> byIdentifier,
        string? identifier,
        SnapshotTypeRegistration registration)
    {
        try
        {
            AadCodec.ValidateSnapshotTypeId(identifier);
        }
        catch (PayloadProtectionFormatException)
        {
            throw new ArgumentException("A snapshot type identifier or alias is invalid.", "registrations");
        }

        if (!byIdentifier.TryAdd(identifier!, registration))
        {
            throw new ArgumentException("A snapshot type identifier or alias collides with another registration.", "registrations");
        }
    }
}
