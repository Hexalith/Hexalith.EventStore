using Hexalith.EventStore.Contracts.Commands;
using Hexalith.EventStore.Server.Pipeline.Commands;

namespace Hexalith.EventStore.Server.Commands;

/// <summary>Resolves exactly one registered trusted adapter for each command type.</summary>
public sealed class IdempotencyIntentAdapterRegistry : IIdempotencyIntentAdapterRegistry
{
    private readonly IReadOnlyDictionary<string, IIdempotencyIntentAdapter> _adapters;
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

        var registered = new Dictionary<string, IIdempotencyIntentAdapter>(StringComparer.Ordinal);
        foreach (IIdempotencyIntentAdapter adapter in adapters)
        {
            ArgumentNullException.ThrowIfNull(adapter);
            ValidateAdapter(adapter);
            if (!registered.TryAdd(adapter.CommandType, adapter))
            {
                throw new InvalidOperationException(
                    "Multiple trusted idempotency adapters are registered for one command type.");
            }
        }

        _adapters = registered;
        _encoder = encoder;
    }

    /// <inheritdoc/>
    public TrustedIdempotencyDescriptor Resolve(SubmitCommand command)
    {
        ArgumentNullException.ThrowIfNull(command);
        if (!_adapters.TryGetValue(command.CommandType, out IIdempotencyIntentAdapter? adapter))
        {
            throw new InvalidOperationException(
                "No trusted idempotency adapter is registered for the command type.");
        }

        IdempotencyCanonicalIntent intent = adapter.CreateIntent(command)
            ?? throw new InvalidOperationException("The trusted idempotency adapter returned no canonical intent.");
        ValidateIntent(intent);
        return new TrustedIdempotencyDescriptor(
            adapter.AdapterId,
            adapter.OperationId,
            adapter.DescriptorVersion,
            _encoder.Encode(adapter, intent),
            adapter.RetentionTier);
    }

    private static void ValidateAdapter(IIdempotencyIntentAdapter adapter)
    {
        if (string.IsNullOrWhiteSpace(adapter.CommandType)
            || string.IsNullOrWhiteSpace(adapter.AdapterId)
            || string.IsNullOrWhiteSpace(adapter.OperationId)
            || adapter.DescriptorVersion <= 0
            || !Enum.IsDefined(adapter.RetentionTier))
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
