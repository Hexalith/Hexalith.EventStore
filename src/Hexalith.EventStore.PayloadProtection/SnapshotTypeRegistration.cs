// Normative authority: de9ba8866fd98a480629890ee2b89a492fbad96d4d5a927388e6aaa0fdd72b4e; sections 6.1 and 12.3.
using System.Text.Json.Serialization.Metadata;

namespace Hexalith.EventStore.PayloadProtection;

/// <summary>
/// Maps one current stable snapshot type identifier and its historical aliases to one exact
/// <see cref="JsonTypeInfo"/> (normative section 6.1).
/// </summary>
/// <param name="SnapshotTypeId">The current identifier that writers emit.</param>
/// <param name="TypeInfo">The exact source-generated metadata used to deserialize authenticated snapshot state.</param>
/// <param name="Aliases">Historical identifiers that readers accept without rewriting them.</param>
internal sealed record SnapshotTypeRegistration(
    string SnapshotTypeId,
    JsonTypeInfo TypeInfo,
    IReadOnlyList<string> Aliases)
{
    /// <summary>
    /// Initializes a registration without historical aliases.
    /// </summary>
    /// <param name="snapshotTypeId">The current identifier that writers emit.</param>
    /// <param name="typeInfo">The exact source-generated metadata used after authentication.</param>
    internal SnapshotTypeRegistration(string snapshotTypeId, JsonTypeInfo typeInfo)
        : this(snapshotTypeId, typeInfo, [])
    {
    }

    /// <inheritdoc/>
    public override string ToString() => nameof(SnapshotTypeRegistration);
}
