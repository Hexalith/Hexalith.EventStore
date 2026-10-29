using Hexalith.EventStore.Contracts.Commands;
using Hexalith.EventStore.Contracts.Routing;
using Hexalith.EventStore.DomainService;
using Hexalith.EventStore.Server.Pipeline.Commands;

namespace Hexalith.EventStore.Server.Commands;

/// <summary>Resolves exactly one registered trusted adapter for each command type.</summary>
public sealed class IdempotencyIntentAdapterRegistry : IIdempotencyIntentAdapterRegistry
{
    private readonly IReadOnlyDictionary<
        string,
        (
            IIdempotencyIntentAdapter Adapter,
            RouteContractDeclaration? Route,
            string CommandType,
            string AdapterId,
            string OperationId,
            string? PolicyVersion,
            int DescriptorVersion,
            IdempotencyReplayRetentionTier RetentionTier)> _adapters;
    private readonly CanonicalIdempotencyIntentEncoder _encoder;
    private readonly Hexalith.EventStore.ServiceDefaults.Routing.IRouteCatalogAuthority? _catalogAuthority;

    /// <summary>Initializes a new instance of the <see cref="IdempotencyIntentAdapterRegistry"/> class.</summary>
    /// <param name="adapters">The server-registered trusted adapters.</param>
    /// <param name="encoder">The canonical intent encoder.</param>
    public IdempotencyIntentAdapterRegistry(
        Hexalith.EventStore.ServiceDefaults.Routing.IRouteCatalogAuthority authority,
        IEnumerable<IIdempotencyIntentAdapter> adapters,
        CanonicalIdempotencyIntentEncoder encoder)
        : this(adapters, encoder, authority)
    {
        ArgumentNullException.ThrowIfNull(authority);
    }

    /// <summary>Friend-only protocol composition without runtime authority.</summary>
    internal IdempotencyIntentAdapterRegistry(
        IEnumerable<IIdempotencyIntentAdapter> adapters,
        CanonicalIdempotencyIntentEncoder encoder,
        Hexalith.EventStore.ServiceDefaults.Routing.IRouteCatalogAuthority? catalogAuthority = null)
    {
        ArgumentNullException.ThrowIfNull(adapters);
        ArgumentNullException.ThrowIfNull(encoder);

        var registered = new Dictionary<
            string,
            (
                IIdempotencyIntentAdapter Adapter,
                RouteContractDeclaration? Route,
                string CommandType,
                string AdapterId,
                string OperationId,
                string? PolicyVersion,
                int DescriptorVersion,
                IdempotencyReplayRetentionTier RetentionTier)>(StringComparer.Ordinal);
        foreach (IIdempotencyIntentAdapter adapter in adapters)
        {
            ArgumentNullException.ThrowIfNull(adapter);
            string commandType = adapter.CommandType;
            string adapterId = adapter.AdapterId;
            string operationId = adapter.OperationId;
            int descriptorVersion = adapter.DescriptorVersion;
            IdempotencyReplayRetentionTier retentionTier = adapter.RetentionTier;
            ValidateAdapter(commandType, adapterId, operationId, descriptorVersion, retentionTier);
            RouteContractDeclaration? route = null;
            string? policyVersion = null;
            if (adapter is IDeclaredIdempotencyIntentAdapter declared)
            {
                CommandIntentDeclaration declaration = declared.Declaration;
                RouteCatalogValidator.ValidateDeclaration(declaration.Route);
                if (declaration.Route.Kind != "command" || declaration.Route.MessageType != commandType
                    || declaration.AdapterId != adapterId || declaration.OperationId != operationId
                    || string.IsNullOrWhiteSpace(declaration.PolicyVersion)
                    || declaration.DescriptorSchema.Version != descriptorVersion || declaration.RetentionTier != retentionTier)
                {
                    throw new InvalidOperationException("A trusted idempotency adapter disagrees with its command declaration.");
                }
                route = SnapshotRoute(declaration.Route);
                policyVersion = declaration.PolicyVersion;
            }
            if (!registered.TryAdd(
                route is not null ? route.Domain + "|" + commandType : commandType,
                (adapter, route, commandType, adapterId, operationId, policyVersion, descriptorVersion, retentionTier)))
            {
                throw new InvalidOperationException(
                    "Multiple trusted idempotency adapters are registered for one command type.");
            }
        }

        foreach (var registration in registered.Values.Where(value => value.Route is not null))
        {
            RouteContractDeclaration route = registration.Route!;
            RouteCatalogValidator.ValidateDeclaration(route);
            foreach (var other in registered.Values.Where(other => !ReferenceEquals(other.Adapter, registration.Adapter)))
            {
                bool overlaps = other.Route is { } scoped
                    ? scoped.Domain == route.Domain
                        && (scoped.Execution?.Aliases ?? [scoped.MessageType!]).Any(alias => CommandDiscriminatorCompatibility.Matches(route, alias))
                    : CommandDiscriminatorCompatibility.Matches(route, other.CommandType);
                if (overlaps)
                {
                    throw new InvalidOperationException("Ambiguous trusted adapter registrations overlap a declared handler.");
                }
            }
        }

        _adapters = registered;
        _encoder = encoder;
        _catalogAuthority = catalogAuthority;
    }

    /// <inheritdoc/>
    public TrustedIdempotencyDescriptor Resolve(SubmitCommand command)
        => ResolveAsync(command).GetAwaiter().GetResult();

    /// <inheritdoc/>
    public async Task<TrustedIdempotencyDescriptor> ResolveAsync(SubmitCommand command, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        var catalogLease = _catalogAuthority is null ? null : await _catalogAuthority.AcquireAsync(cancellationToken).ConfigureAwait(false);
        await using var catalogRelease = new Hexalith.EventStore.ServiceDefaults.Routing.RouteCatalogLeaseReleaser(catalogLease).ConfigureAwait(false);
        var registration = _adapters.Values.SingleOrDefault(value => value.Route is { } route
            && route.Domain == command.Domain
            && CommandDiscriminatorCompatibility.Matches(route, command.CommandType));
        if (registration.Adapter is null && (!_adapters.TryGetValue(command.CommandType, out registration) || registration.Route is not null))
        {
            throw new InvalidOperationException(
                "No trusted idempotency adapter is registered for the command type.");
        }

        if (catalogLease is not null)
        {
            var selected = catalogLease.Snapshot.Resolve("command", command.Tenant, command.Domain, command.CommandType);
            IdempotencyCatalogEntry admission = catalogLease.Snapshot.Admission(selected.Declaration);
            if (registration.Adapter is not IDeclaredIdempotencyIntentAdapter declared
                || RouteCatalogCodec.ComputeDeclarationDigest(declared.Declaration.Route) != selected.Target.DeclarationDigest
                || admission.AdapterId != registration.AdapterId || admission.OperationId != registration.OperationId
                || admission.PolicyVersion != registration.PolicyVersion || admission.DescriptorSchema.Version != registration.DescriptorVersion
                || admission.DescriptorDigest != RouteCatalogCodec.ComputeDescriptorDigest(declared.Declaration.DescriptorSchema)
                || admission.RetentionTier != registration.RetentionTier.ToString().ToLowerInvariant())
                throw new Hexalith.EventStore.ServiceDefaults.Routing.RouteCatalogUnavailableException("The trusted adapter differs from the committed admission facet.");
        }

        IdempotencyCanonicalIntent intent = registration.Adapter.CreateIntent(
            new IdempotencyIntentCommand(
                command.CommandType,
                command.Tenant,
                command.Domain,
                command.AggregateId,
                command.Payload,
                command.Extensions) { AuthenticatedSubject = command.AuthenticatedSubject })
            ?? throw new InvalidOperationException("The trusted idempotency adapter returned no canonical intent.");
        ValidateIntent(intent);
        if (registration.Route is not null && intent.PolicyVersion != registration.PolicyVersion)
            throw new InvalidOperationException("The trusted canonical intent disagrees with its declared policy version.");
        return new TrustedIdempotencyDescriptor(
            registration.AdapterId,
            registration.OperationId,
            registration.DescriptorVersion,
            _encoder.Encode(
                registration.AdapterId,
                registration.OperationId,
                registration.DescriptorVersion,
                registration.RetentionTier,
                intent),
            registration.RetentionTier);
    }

    private static RouteContractDeclaration SnapshotRoute(RouteContractDeclaration route) => route with
    {
        FallbackTypes = Array.AsReadOnly(route.FallbackTypes.ToArray()),
        Execution = route.Execution is { } execution ? execution with
        {
            Aliases = Array.AsReadOnly(execution.Aliases.ToArray()),
            ProjectionOutputs = Array.AsReadOnly(execution.ProjectionOutputs.ToArray()),
        } : null,
    };

    private static void ValidateAdapter(
        string commandType,
        string adapterId,
        string operationId,
        int descriptorVersion,
        IdempotencyReplayRetentionTier retentionTier)
    {
        if (string.IsNullOrWhiteSpace(commandType)
            || string.IsNullOrWhiteSpace(adapterId)
            || string.IsNullOrWhiteSpace(operationId)
            || descriptorVersion <= 0
            || !Enum.IsDefined(retentionTier))
        {
            throw new InvalidOperationException("A trusted idempotency adapter registration is invalid.");
        }
    }

    private static void ValidateIntent(IdempotencyCanonicalIntent intent)
    {
        if (string.IsNullOrWhiteSpace(intent.CanonicalTarget)
            || intent.SemanticPayload is not { Length: > 0 }
            || string.IsNullOrWhiteSpace(intent.PolicyVersion))
        {
            throw new InvalidOperationException("The trusted canonical intent is incomplete.");
        }
    }
}
