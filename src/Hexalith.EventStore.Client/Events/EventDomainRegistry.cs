using System.Collections.Frozen;

namespace Hexalith.EventStore.Client.Events;

/// <summary>Owns a domain's immutable decoded descriptors and verifies all retained adjacent chains.</summary>
/// <remarks>Dependency/options attestation and corpus readiness must precede production advertisement.</remarks>
internal sealed class EventDomainRegistry : IDisposable
{
    private readonly EventRegistryRow[] _rows;
    private readonly FrozenDictionary<string, EventRegistryRow> _current;
    private readonly FrozenDictionary<(string Type, int Version), EventRegistryRow> _versions;
    private readonly FrozenDictionary<(string Type, int Version), EventRegistryRow> _edges;
    private readonly FrozenDictionary<string, EventRegistryRow> _aliases;

    /// <summary>Admits one bounded domain inventory and verifies its chain and alias topology.</summary>
    internal EventDomainRegistry(string domain, IReadOnlyList<ReadOnlyMemory<byte>> encodedRows, long referencedManifestBytes = 0)
    {
        Domain = domain;
        _rows = EventRegistryFingerprintCodec.DecodeRows(domain, encodedRows, referencedManifestBytes);
        try
        {
            Fingerprint = Convert.ToHexStringLower(EventRegistryFingerprintCodec.ComputeDecoded(_rows));
            _current = _rows.Where(static row => row.Tag == 0x44)
                .ToFrozenDictionary(static row => row.GetTextKey(1), StringComparer.Ordinal);
            _versions = _rows.Where(static row => row.Tag == 0x56)
                .ToFrozenDictionary(static row => (row.GetTextKey(1), row.GetVersionKey(2)));
            _edges = _rows.Where(static row => row.Tag == 0x45)
                .ToFrozenDictionary(static row => (row.GetTextKey(1), row.GetVersionKey(2)));
            _aliases = _rows.Where(static row => row.Tag == 0x41)
                .ToFrozenDictionary(static row => row.GetTextKey(1), StringComparer.Ordinal);
            ValidateTopology();
        }
        catch
        {
            foreach (EventRegistryRow row in _rows)
            {
                row.Dispose();
            }

            throw;
        }
    }

    /// <summary>Gets the ordinal owning domain.</summary>
    internal string Domain { get; }

    /// <summary>Gets the exact manifest fingerprint, which is not an attestation.</summary>
    internal string Fingerprint { get; }

    /// <summary>Enumerates exclusively owned immutable rows for the local semantic codec.</summary>
    internal IEnumerable<EventRegistryRow> Rows => _rows;

    /// <summary>Resolves an exact domain-scoped legacy alias to its declared source version and format.</summary>
    internal (string Type, int Version, string Format) ResolveAlias(string alias)
    {
        if (!_aliases.TryGetValue(alias, out EventRegistryRow? row))
        {
            throw new InvalidOperationException("UnknownEventContract: the legacy alias is not allow-listed.");
        }

        return (row.GetTextField(1), row.GetIntField(2), row.GetTextField(3));
    }

    /// <summary>Gets the current payload version for an allow-listed canonical type.</summary>
    internal int GetCurrentVersion(string canonicalType)
    {
        if (!_current.TryGetValue(canonicalType, out EventRegistryRow? row))
        {
            throw new InvalidOperationException("UnknownEventContract: the canonical event type is not allow-listed.");
        }

        return row.GetIntField(2);
    }

    /// <summary>Gets the aggregate route explicitly declared for an allow-listed event contract.</summary>
    internal string GetAggregateRoute(string canonicalType)
        => _current.TryGetValue(canonicalType, out EventRegistryRow? row)
            ? row.GetTextField(1)
            : throw new InvalidOperationException("UnknownEventContract: the canonical event type is not allow-listed.");

    /// <summary>Gets the exact registered version descriptor.</summary>
    internal EventRegistryRow GetVersion(string canonicalType, int version)
        => _versions.TryGetValue((canonicalType, version), out EventRegistryRow? row)
            ? row : throw new InvalidOperationException("UnknownEventContract: the payload version is not allow-listed.");

    /// <summary>Gets the exact adjacent edge descriptor for an admitted chain.</summary>
    internal EventRegistryRow GetEdge(string canonicalType, int sourceVersion)
        => _edges.TryGetValue((canonicalType, sourceVersion), out EventRegistryRow? row)
            ? row : throw new InvalidOperationException("UnknownEventContract: the adjacent upcast edge is not allow-listed.");

    /// <summary>Gets the exact explicitly registered F descriptor for the selected V1 write alias.</summary>
    internal EventRegistryRow GetDownserializer(string canonicalType, string writeAlias)
        => _rows.SingleOrDefault(row => row.Tag == 0x46
            && string.Equals(row.GetTextKey(1), canonicalType, StringComparison.Ordinal)
            && string.Equals(row.GetTextKey(2), writeAlias, StringComparison.Ordinal))
            ?? throw new InvalidOperationException("DownserializeRejected: no registered V1 downserializer for that alias.");

    /// <inheritdoc/>
    public void Dispose()
    {
        foreach (EventRegistryRow row in _rows)
        {
            row.Dispose();
        }
    }

    private void ValidateTopology()
    {
        if (_current.Count == 0 || _rows.Count(static row => row.Tag == 0x53) != 1)
        {
            throw new ArgumentException("A domain registry requires current descriptors and exactly one shared implementation row.");
        }

        foreach ((string type, EventRegistryRow descriptor) in _current)
        {
            _ = GetVersion(type, descriptor.GetIntField(2));
        }

        foreach (((string type, int version), EventRegistryRow _) in _versions)
        {
            int current = GetCurrentVersion(type);
            if (version > current || current - version > 16)
            {
                throw new ArgumentException("Every retained version must reach current in at most 16 adjacent hops.");
            }

            for (int source = version; source < current; source++)
            {
                EventRegistryRow edge = GetEdge(type, source);
                EventRegistryRow input = GetVersion(type, source);
                EventRegistryRow output = GetVersion(type, source + 1);
                if (edge.GetIntField(1) != source + 1
                    || !string.Equals(edge.GetTextField(2), input.GetTextField(7), StringComparison.Ordinal)
                    || !string.Equals(edge.GetTextField(3), output.GetTextField(7), StringComparison.Ordinal))
                {
                    throw new ArgumentException("An upcast edge must match its consecutive version schemas and exact formats.");
                }
            }
        }

        foreach (((string type, int source), EventRegistryRow _) in _edges)
        {
            if (source >= GetCurrentVersion(type))
            {
                throw new ArgumentException("An upcast edge cannot extend past current or downgrade.");
            }

            _ = GetVersion(type, source);
            _ = GetVersion(type, source + 1);
        }

        foreach (EventRegistryRow alias in _aliases.Values)
        {
            string type = alias.GetTextField(1);
            EventRegistryRow version = GetVersion(type, alias.GetIntField(2));
            if (!string.Equals(alias.GetTextField(3), version.GetTextField(7), StringComparison.Ordinal))
            {
                throw new ArgumentException("A legacy alias must match its declared source version format.");
            }
        }

        foreach (EventRegistryRow downserializer in _rows.Where(static row => row.Tag == 0x46))
        {
            string type = downserializer.GetTextKey(1);
            string writeAlias = downserializer.GetTextKey(2);
            if (!_aliases.TryGetValue(writeAlias, out EventRegistryRow? alias)
                || !string.Equals(alias.GetTextField(1), type, StringComparison.Ordinal)
                || downserializer.GetIntField(1) != GetCurrentVersion(type)
                || downserializer.GetIntField(2) != alias.GetIntField(2)
                || !string.Equals(downserializer.GetTextField(5), GetVersion(type, GetCurrentVersion(type)).GetTextField(7), StringComparison.Ordinal)
                || !string.Equals(downserializer.GetTextField(6), alias.GetTextField(3), StringComparison.Ordinal))
            {
                throw new ArgumentException("A V1 downserializer must bind the exact current type and registered write alias source.");
            }
        }

        foreach (IGrouping<string, EventRegistryRow> route in _current.Values.GroupBy(static row => row.GetTextField(1), StringComparer.Ordinal))
        {
            EventRegistryRow first = route.First();
            foreach (EventRegistryRow row in route.Skip(1))
            {
                foreach (int field in new[] { 5, 6, 7, 8, 9, 10, 11, 12 })
                {
                    if (!first.GetEncodedField(field).SequenceEqual(row.GetEncodedField(field)))
                    {
                        throw new ArgumentException("All descriptors on an aggregate route must agree on state and Apply behavior.");
                    }
                }
            }
        }
    }
}
