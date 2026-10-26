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
            string AdapterId,
            string OperationId,
            int DescriptorVersion,
            IdempotencyReplayRetentionTier RetentionTier)> _adapters;
    private readonly CanonicalIdempotencyIntentEncoder _encoder;

    /// <summary>Initializes a new instance of the <see cref="IdempotencyIntentAdapterRegistry"/> class.</summary>
    /// <param name="adapters">The server-registered trusted adapters.</param>
    /// <param name="encoder">The canonical intent encoder.</param>
    public IdempotencyIntentAdapterRegistry(
        IEnumerable<IIdempotencyIntentAdapter> adapters,
        CanonicalIdempotencyIntentEncoder encoder)
    {
        ArgumentNullException.ThrowIfNull(adapters);
        ArgumentNullException.ThrowIfNull(encoder);

        var registered = new Dictionary<
            string,
            (
                IIdempotencyIntentAdapter Adapter,
                string AdapterId,
                string OperationId,
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
            if (!registered.TryAdd(
                adapter is IDeclaredIdempotencyIntentAdapter declared ? declared.Declaration.Route.Domain + "|" + commandType : commandType,
                (adapter, adapterId, operationId, descriptorVersion, retentionTier)))
            {
                throw new InvalidOperationException(
                    "Multiple trusted idempotency adapters are registered for one command type.");
            }
        }

        IIdempotencyIntentAdapter[] materialized = registered.Values.Select(value => value.Adapter).ToArray();
        foreach (IDeclaredIdempotencyIntentAdapter declared in materialized.OfType<IDeclaredIdempotencyIntentAdapter>())
        {
            RouteCatalogValidator.ValidateDeclaration(declared.Declaration.Route);
            foreach (IIdempotencyIntentAdapter other in materialized.Where(other => !ReferenceEquals(other, declared)))
            {
                bool overlaps = other is IDeclaredIdempotencyIntentAdapter scoped
                    ? scoped.Declaration.Route.Domain == declared.Declaration.Route.Domain
                        && scoped.Declaration.Route.Execution!.Aliases.Any(alias => CommandDiscriminatorCompatibility.Matches(declared.Declaration.Route, alias))
                    : CommandDiscriminatorCompatibility.Matches(declared.Declaration.Route, other.CommandType);
                if (overlaps)
                {
                    throw new InvalidOperationException("Ambiguous trusted adapter registrations overlap a declared handler.");
                }
            }
        }

        _adapters = registered;
        _encoder = encoder;
    }

    /// <inheritdoc/>
    public TrustedIdempotencyDescriptor Resolve(SubmitCommand command)
    {
        ArgumentNullException.ThrowIfNull(command);
        var registration = _adapters.Values.SingleOrDefault(value => value.Adapter is IDeclaredIdempotencyIntentAdapter declared
            && declared.Declaration.Route.Domain == command.Domain
            && CommandDiscriminatorCompatibility.Matches(declared.Declaration.Route, command.CommandType));
        if (registration.Adapter is null && !_adapters.TryGetValue(command.CommandType, out registration))
        {
            throw new InvalidOperationException(
                "No trusted idempotency adapter is registered for the command type.");
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
