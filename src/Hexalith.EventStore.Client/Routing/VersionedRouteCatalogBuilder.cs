using Hexalith.EventStore.Contracts.Routing;

namespace Hexalith.EventStore.Client.Routing;

/// <summary>Builds deterministic complete schema-three generations, retaining immutable committed history outside dispatch.</summary>
public static class VersionedRouteCatalogBuilder
{
    /// <summary>Validates every supplied digest before returning a complete canonical generation.</summary>
    public static RouteCatalogEnvelope Build(RouteCatalogGenerationInput input)
    {
        ArgumentNullException.ThrowIfNull(input);
        if ((input.CommittedPredecessor is null) != (input.CommittedPredecessorRootDigest is null))
            throw new RouteCatalogValidationException([new("$.committedPredecessor", "missing-committed-root-or-bytes")]);
        var predecessor = input.CommittedPredecessor is null ? null : RouteCatalogCodec.Decode(input.CommittedPredecessor, input.CommittedPredecessorRootDigest);
        if (predecessor is not null && predecessor.Generation >= input.Generation)
            throw new RouteCatalogValidationException([new("$.generation", "non-successor-generation")]);
        foreach (var declaration in input.Declarations) RouteCatalogValidator.ValidateDeclaration(declaration);
        if (input.Declarations.Select(d => d.RouteId).Distinct(StringComparer.Ordinal).Count() != input.Declarations.Count
            || input.Operations.Select(o => o.RouteId).Distinct(StringComparer.Ordinal).Count() != input.Operations.Count)
            throw new RouteCatalogValidationException([new("$.declarations", "duplicate-owner-operation")]);
        var declarations = input.Declarations.ToDictionary(d => d.RouteId, StringComparer.Ordinal);
        var operations = input.Operations.ToDictionary(o => o.RouteId, StringComparer.Ordinal);
        var routes = input.Bindings.Select(binding =>
        {
            if (!declarations.TryGetValue(binding.RouteId, out var declaration) || !operations.TryGetValue(binding.RouteId, out var operation))
                throw new RouteCatalogValidationException([new(binding.RouteId, "missing-owner-declaration-or-operation")]);
            return new RouteCatalogEntry(declaration.RouteId, input.Generation, RouteCatalogCodec.ComputeDeclarationDigest(declaration),
                binding.AppId, binding.Method, binding.ContractVersion, declaration.CredentialKind, declaration.Operation,
                binding.IsRuntimeOverride, binding.ResolverTenantPattern, operation.Digest);
        }).ToArray();
        var retained = (predecessor?.RetainedIdempotency ?? []).Concat((predecessor?.Idempotency ?? [])
            .Select(entry => new RetainedIdempotencyCatalogEntry(entry)))
            .DistinctBy(entry => entry.Entry.ContentDigest).ToArray();
        var result = RouteCatalogCodec.Seal(new(3, input.Generation, input.Profile, false, input.Declarations, routes,
            input.Idempotency, string.Empty, string.Empty, input.Operations, retained));
        _ = RouteCatalogCodec.Encode(result);
        return result;
    }
}
