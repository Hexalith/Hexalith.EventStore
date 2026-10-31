using System.Text.RegularExpressions;

namespace Hexalith.EventStore.Contracts.Routing;

/// <summary>Validates independently observed generated human ingress without changing finite service dispatch.</summary>
public static class RouteCatalogGeneratedRestPolicy
{
    /// <summary>The explicit SDK generated-query fence implementation.</summary>
    public const string FenceVersion = "eventstore-generated-human-rest-v1";

    /// <summary>Validates a declared GET grant against real typed public queries and their complete owner schemas.</summary>
    public static bool AdmitsGrant(RouteCatalogEnvelope catalog, RouteCatalogReceiverSurface? receiver, string grant, IReadOnlyList<string> verbs)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentNullException.ThrowIfNull(verbs);
        if (receiver?.FenceVersion != FenceVersion || receiver.GeneratedRest is not { Count: > 0 } generated
            || catalog.SchemaVersion != 3 || verbs.Count != 1 || verbs[0] != "GET"
            || string.IsNullOrWhiteSpace(grant) || grant.Length > 512 || grant.Contains('?', StringComparison.Ordinal)
            || grant.Contains('#', StringComparison.Ordinal) || grant.Contains('\\', StringComparison.Ordinal)) return false;
        string[] segments = grant.Trim('/').Split('/');
        if (segments.Length is < 2 or > 32 || segments[^1] == "**" || segments.TakeWhile(segment => segment is not ("*" or "**")).Count() < 2
            || segments.Any(segment => segment.Length == 0 || segment is "." or ".."
                || segment.Contains('*', StringComparison.Ordinal) && segment is not ("*" or "**"))) return false;
        var admitted = new List<RouteCatalogGeneratedRestEndpoint>();
        foreach (var endpoint in generated)
        {
            RouteContractDeclaration? declaration = catalog.Declarations.SingleOrDefault(value => value.RouteId == endpoint.RouteId);
            RouteOperationContract? operation = catalog.Operations!.SingleOrDefault(value => value.RouteId == endpoint.RouteId);
            if (declaration is not { Kind: "query", Execution: not null } || operation is not { Ingress: "public" }
                || endpoint.ContractType != declaration.Execution.HandlerType
                || endpoint.DeclarationDigest != RouteCatalogCodec.ComputeDeclarationDigest(declaration)
                || operation.Digest != RouteCatalogCodec.ComputeOperationContractDigest(operation)
                || endpoint.OperationDigest != operation.Digest || !endpoint.Policies.Contains("EventStoreHuman", StringComparer.Ordinal)
                || !receiver.Endpoints.Any(actual => actual.Pattern == endpoint.Pattern && !actual.Anonymous && actual.Admitted
                    && actual.Methods.SequenceEqual(["GET"], StringComparer.Ordinal)
                    && actual.Policies.SequenceEqual(endpoint.Policies, StringComparer.Ordinal))) return false;
            admitted.Add(endpoint);
        }
        if (admitted.Select(value => value.Pattern).Distinct(StringComparer.OrdinalIgnoreCase).Count() != admitted.Count) return false;
        string expression = "^/" + string.Join('/', segments.Select(segment => segment switch
        {
            "*" => "[^/]+", "**" => ".*", _ => Regex.Escape(segment),
        })) + "(?:/.*)?$";
        // Match route structure, never caller data. Runtime admission uses the actual selected
        // endpoint object, human JWT and SDK action filter rather than this transport prefix.
        bool Reachable(string pattern) => Regex.IsMatch(
            Regex.Replace(pattern, @"\{[^{}]+\}", "owned-segment", RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(100)),
            expression, RegexOptions.CultureInvariant | RegexOptions.IgnoreCase | RegexOptions.NonBacktracking, TimeSpan.FromMilliseconds(100));
        return admitted.Any(endpoint => Reachable(endpoint.Pattern))
            && receiver.Endpoints.Where(endpoint => endpoint.Admitted && Reachable(endpoint.Pattern)).All(endpoint =>
                admitted.Any(typed => typed.Pattern == endpoint.Pattern) && endpoint.Operation == "generated-human-query");
    }

    /// <summary>Preserves exact SDK catalog callbacks under their existing participant and workload guards.</summary>
    public static bool AdmitsControlGrant(RouteCatalogReceiverSurface? receiver, string method, IReadOnlyList<string> verbs)
    {
        ArgumentNullException.ThrowIfNull(verbs);
        if (receiver?.FenceVersion != FenceVersion || verbs.Count != 1) return false;
        string? operation = method == "internal/routing/catalog-generation" && verbs[0] == "GET"
            ? Authorization.EventStoreOperationIdentifiers.RouteCatalogRead
            : verbs[0] == "POST" && method is "internal/routing/catalog/prepare" or "internal/routing/catalog/ready"
                or "internal/routing/catalog/commit" or "internal/routing/catalog/rollback" or "internal/routing/catalog/reconcile"
                ? Authorization.EventStoreOperationIdentifiers.RouteCatalogActivate : null;
        return operation is not null && receiver.Endpoints.Any(endpoint => endpoint.Pattern == "/" + method && endpoint.Admitted
            && !endpoint.Anonymous && !endpoint.IsPattern && endpoint.Operation == operation
            && endpoint.Methods.SequenceEqual(verbs, StringComparer.Ordinal)
            && endpoint.Policies.SequenceEqual(["EventStoreRouteCatalogWorkload:" + operation], StringComparer.Ordinal));
    }
}
