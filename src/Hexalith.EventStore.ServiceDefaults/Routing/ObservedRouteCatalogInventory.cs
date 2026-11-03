using Hexalith.EventStore.Contracts.Routing;

namespace Hexalith.EventStore.ServiceDefaults.Routing;

/// <summary>Owns document snapshots and pure digest results for exactly one fresh host observation.</summary>
internal sealed class ObservedRouteCatalogInventory
{
    private readonly Dictionary<RouteContractDeclaration, string> _declarations = new(ReferenceEqualityComparer.Instance);
    private readonly Dictionary<RouteOperationContract, string> _operations = new(ReferenceEqualityComparer.Instance);
    private readonly Dictionary<IdempotencyCatalogEntry, string> _admission = new(ReferenceEqualityComparer.Instance);

    /// <summary>Snapshots documents in the original identity calculation's validation order.</summary>
    internal ObservedRouteCatalogInventory(RouteCatalogHostObservation observation)
    {
        var admission = Snapshot(observation.AdmissionPolicies!, entry => entry.RouteId, entry => entry with
        {
            DescriptorSchema = entry.DescriptorSchema is { } schema ? schema with
            { Fields = schema.Fields is null ? null! : Array.AsReadOnly(schema.Fields.ToArray()) } : null!,
            ReaderDigestKeyGenerations = entry.ReaderDigestKeyGenerations is null ? null! : Array.AsReadOnly(entry.ReaderDigestKeyGenerations.ToArray()),
            KeyMap = entry.KeyMap is null ? null! : Array.AsReadOnly(entry.KeyMap.ToArray()),
        }, RouteCatalogCodec.ComputeIdempotencyPolicyDigest, _admission);
        var declarations = Snapshot(observation.Declarations, entry => entry.RouteId, entry => entry with
        {
            FallbackTypes = entry.FallbackTypes is null ? null! : Array.AsReadOnly(entry.FallbackTypes.ToArray()),
            Execution = entry.Execution is { } execution ? execution with
            {
                Aliases = execution.Aliases is null ? null! : Array.AsReadOnly(execution.Aliases.ToArray()),
                ProjectionOutputs = execution.ProjectionOutputs is null ? null! : Array.AsReadOnly(execution.ProjectionOutputs.ToArray()),
            } : null,
        }, RouteCatalogCodec.ComputeDeclarationDigest, _declarations);
        var operations = observation.Operations is null ? null : Snapshot(observation.Operations, entry => entry.RouteId,
            entry => entry with { RequestSchema = entry.RequestSchema.Clone(), ResultSchema = entry.ResultSchema.Clone() },
            RouteCatalogCodec.ComputeOperationContractDigest, _operations);
        Observation = observation with { AdmissionPolicies = admission, Declarations = declarations, Operations = operations };
    }

    /// <summary>Gets the fresh observation with privately owned document collections.</summary>
    internal RouteCatalogHostObservation Observation { get; }

    /// <summary>Computes each owned declaration digest once; decoded candidate documents remain local to this observation.</summary>
    internal string Declaration(RouteContractDeclaration declaration) => Get(declaration, _declarations, RouteCatalogCodec.ComputeDeclarationDigest);

    /// <summary>Gets the digest of an owned operation schema.</summary>
    internal string Operation(RouteOperationContract operation) => Get(operation, _operations, RouteCatalogCodec.ComputeOperationContractDigest);

    /// <summary>Computes each installed or decoded candidate admission policy digest once.</summary>
    internal string Admission(IdempotencyCatalogEntry entry) => Get(entry, _admission, RouteCatalogCodec.ComputeIdempotencyPolicyDigest);

    private static string Get<T>(T value, Dictionary<T, string> digests, Func<T, string> compute) where T : class
    {
        if (digests.TryGetValue(value, out string? digest)) return digest;
        digest = compute(value);
        digests.Add(value, digest);
        return digest;
    }

    private static IReadOnlyList<T> Snapshot<T>(IReadOnlyList<T> documents, Func<T, string> routeId, Func<T, T> copy,
        Func<T, string> compute, Dictionary<T, string> digests) where T : class
    {
        T[] source = documents.ToArray();
        var copies = new Dictionary<T, T>(ReferenceEqualityComparer.Instance);
        foreach (T document in source.OrderBy(routeId, StringComparer.Ordinal))
        {
            // Preserve duplicates for the existing inventory refusal; never join by RouteId here.
            if (copies.ContainsKey(document)) continue;
            T snapshot = copy(document);
            string digest = compute(snapshot);
            copies.Add(document, snapshot);
            digests.Add(snapshot, digest);
        }
        return Array.AsReadOnly(source.Select(document => copies[document]).ToArray());
    }
}
