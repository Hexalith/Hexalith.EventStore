using Hexalith.EventStore.Contracts.Routing;

namespace Hexalith.EventStore.ServiceDefaults.Routing;

/// <summary>Immutable canonical-byte snapshot used for one leased dispatch.</summary>
public sealed class CommittedRouteCatalogSnapshot
{
    private readonly byte[] _bytes;
    private readonly RouteCatalogEnvelope _envelope;

    /// <summary>Validates retained bytes and freezes every collection before exposing any route.</summary>
    public CommittedRouteCatalogSnapshot(ReadOnlyMemory<byte> bytes, string rootDigest, long epoch,
        IReadOnlyList<RouteCatalogAuxiliaryOperation>? auxiliaryOperations = null,
        IReadOnlyList<RouteCatalogProjectionCapability>? projectionCapabilities = null,
        string? localAppId = null)
    {
        _bytes = bytes.ToArray();
        RouteCatalogEnvelope decoded = RouteCatalogCodec.Decode(_bytes, rootDigest);
        _envelope = decoded with
        {
            Operations = decoded.Operations is null ? null : Array.AsReadOnly(decoded.Operations.Select(o => o with
            { RequestSchema = o.RequestSchema.Clone(), ResultSchema = o.ResultSchema.Clone() }).ToArray()),
            RetainedIdempotency = decoded.RetainedIdempotency is null ? null : Array.AsReadOnly(decoded.RetainedIdempotency.Select(r => new RetainedIdempotencyCatalogEntry(r.Entry with
            {
                DescriptorSchema = r.Entry.DescriptorSchema with { Fields = Array.AsReadOnly(r.Entry.DescriptorSchema.Fields.ToArray()) },
                ReaderDigestKeyGenerations = Array.AsReadOnly(r.Entry.ReaderDigestKeyGenerations.ToArray()), KeyMap = Array.AsReadOnly(r.Entry.KeyMap.ToArray()),
            })).ToArray()),
            Declarations = Array.AsReadOnly(decoded.Declarations.Select(d => d with
            {
                FallbackTypes = Array.AsReadOnly(d.FallbackTypes.ToArray()),
                Execution = d.Execution is { } e ? e with
                {
                    Aliases = Array.AsReadOnly(e.Aliases.ToArray()),
                    ProjectionOutputs = Array.AsReadOnly(e.ProjectionOutputs.ToArray()),
                } : null,
            }).ToArray()),
            Routes = Array.AsReadOnly(decoded.Routes.ToArray()),
            Idempotency = Array.AsReadOnly(decoded.Idempotency.Select(i => i with
            {
                DescriptorSchema = i.DescriptorSchema with { Fields = Array.AsReadOnly(i.DescriptorSchema.Fields.ToArray()) },
                ReaderDigestKeyGenerations = Array.AsReadOnly(i.ReaderDigestKeyGenerations.ToArray()),
                KeyMap = Array.AsReadOnly(i.KeyMap.ToArray()),
            }).ToArray()),
        };
        RootDigest = rootDigest;
        Epoch = epoch;
        LocalAppId = localAppId;
        AuxiliaryOperations = Array.AsReadOnly((auxiliaryOperations ?? []).Select(operation => operation with { Http = operation.Http is { } http ? http with { Grants = http.Grants is null ? null : Array.AsReadOnly(http.Grants.ToArray()) } : null }).ToArray());
        ProjectionCapabilities = Array.AsReadOnly((projectionCapabilities ?? []).Select(c => c with
        { ProjectionTypes = Array.AsReadOnly(c.ProjectionTypes.ToArray()) }).ToArray());
    }

    /// <summary>Gets the monotonic commit/rollback fence.</summary>
    public long Epoch { get; }
    /// <summary>Gets the independently verified Dapr identity of the acquiring host.</summary>
    public string? LocalAppId { get; }
    /// <summary>Gets the canonical-byte root.</summary>
    public string RootDigest { get; }
    /// <summary>Gets the immutable validated envelope.</summary>
    public RouteCatalogEnvelope Envelope => _envelope;
    /// <summary>Gets existing auxiliary calls explicitly covered by the activation owner binding.</summary>
    public IReadOnlyList<RouteCatalogAuxiliaryOperation> AuxiliaryOperations { get; }
    /// <summary>Gets named capability inventories covered by this generation.</summary>
    public IReadOnlyList<RouteCatalogProjectionCapability> ProjectionCapabilities { get; }
    /// <summary>Returns a copy of the retained bytes.</summary>
    public byte[] GetCanonicalBytes() => _bytes.ToArray();

    /// <summary>Selects exactly one declared key, preferring exact keys and then finite declared fallbacks.</summary>
    public (RouteContractDeclaration Declaration, RouteCatalogEntry Target) Resolve(string kind, string tenant, string domain, string discriminator)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(discriminator);
        RouteContractDeclaration[] declarations = [.. _envelope.Declarations.Where(d => d.Kind == kind && d.Domain == domain)];
        RouteContractDeclaration[] exact = [.. declarations.Where(d =>
            (kind == "projection" ? d.ProjectionType == discriminator : d.MessageType == discriminator)
            || (d.Execution is not null && CommandDiscriminatorCompatibility.Matches(d, discriminator)))];
        RouteContractDeclaration[] selected = exact.Length > 0 ? exact : [.. declarations.Where(d =>
            (kind == "projection" ? d.ProjectionType : d.MessageType) == "*" && d.FallbackTypes.Contains(discriminator, StringComparer.Ordinal))];
        if (selected.Length != 1)
            throw new RouteCatalogUnavailableException("The requested route is undeclared or ambiguous.");
        RouteContractDeclaration declaration = selected[0];
        RouteCatalogEntry target = _envelope.Routes.Single(r => r.RouteId == declaration.RouteId);
        if (target.ResolverTenantPattern is { } pattern && pattern != "*" && pattern != tenant)
            throw new RouteCatalogUnavailableException("The requested tenant is outside the committed resolver scope.");
        return (declaration, target);
    }

    /// <summary>Returns the joined admission facet of the same captured route generation.</summary>
    public IdempotencyCatalogEntry Admission(RouteContractDeclaration declaration)
        => _envelope.Idempotency.SingleOrDefault(i => i.RouteId == declaration.RouteId)
            ?? throw new RouteCatalogUnavailableException("The command has no committed admission facet.");
}
