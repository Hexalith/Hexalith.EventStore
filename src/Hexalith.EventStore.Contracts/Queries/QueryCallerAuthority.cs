using System.Runtime.Serialization;
using System.Text.Json.Serialization;

namespace Hexalith.EventStore.Contracts.Queries;

/// <summary>Gateway-observed authority, immutable after capture and untrusted until its exact query binding is verified.</summary>
[DataContract]
[KnownType(typeof(string[]))]
public sealed record QueryCallerAuthority
{
    private readonly string[] _roles = [];
    private readonly string[] _permissions = [];

    /// <summary>Captures originating authority without retaining caller-owned collections.</summary>
    /// <param name="origin">Authenticated-caller, delegated-owner-query, or legacy bound-owner-query.</param>
    /// <param name="roles">Authenticated roles, never query fields.</param>
    /// <param name="permissions">Distinct permission claims, never OAuth scopes.</param>
    [JsonConstructor]
    public QueryCallerAuthority(string origin, IReadOnlyList<string> roles, IReadOnlyList<string> permissions)
    {
        Origin = origin;
        Roles = roles;
        Permissions = permissions;
    }

    /// <summary>Gets the authenticated provenance kind.</summary>
    [DataMember]
    public string Origin { get; init; }
    /// <summary>Gets a defensive copy of the captured roles.</summary>
    [DataMember]
    public IReadOnlyList<string> Roles { get => _roles.ToArray(); init => _roles = value?.ToArray() ?? []; }
    /// <summary>Gets a defensive copy of the captured distinct permission claims.</summary>
    [DataMember]
    public IReadOnlyList<string> Permissions { get => _permissions.ToArray(); init => _permissions = value?.ToArray() ?? []; }

    /// <summary>Compares captured authority by ordinal sequence content.</summary>
    public bool Equals(QueryCallerAuthority? other) => other is not null && Origin == other.Origin
        && _roles.SequenceEqual(other._roles, StringComparer.Ordinal)
        && _permissions.SequenceEqual(other._permissions, StringComparer.Ordinal);
    /// <inheritdoc/>
    public override int GetHashCode()
    {
        HashCode hash = new();
        hash.Add(Origin, StringComparer.Ordinal);
        hash.Add(_roles.Length);
        foreach (string role in _roles) hash.Add(role, StringComparer.Ordinal);
        hash.Add(_permissions.Length);
        foreach (string permission in _permissions) hash.Add(permission, StringComparer.Ordinal);
        return hash.ToHashCode();
    }
}
