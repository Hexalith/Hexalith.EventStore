using System.Collections.ObjectModel;

namespace Hexalith.EventStore.Client.Projections;

/// <summary>
/// An immutable, ordered manifest of coordinated read-model operations within one store component.
/// </summary>
/// <remarks>
/// Structural invariants that do not depend on deployment limits are enforced at construction so an
/// invalid manifest fails before any state access: a non-empty operation list, no duplicate logical keys
/// (compared with <see cref="StringComparer.Ordinal"/>), and a validated <see cref="ReadModelBatchScope"/>.
/// Size limits (operation count, total canonical bytes, per-key bytes) are validated by the store against
/// its configured <see cref="ReadModelBatchOptions"/>, also before any state access. The ordinal position
/// of each operation is its index in <see cref="Operations"/> and participates in the fingerprint.
/// </remarks>
public sealed class ReadModelBatch {
    /// <summary>Retains this immutable manifest until completion so its pending envelopes can be recovered by scope.
    /// This opt-in supports one exact OwnerSelection through the same bounded recovery anchor. Coordinated OwnerSelections retain their separate guarded recovery.</summary>
    public bool RetainManifestForRecovery { get; init; }

    /// <summary>Registers preparation before private content is durable so aggregate erasure can discover it.</summary>
    public bool DiscoverableRecovery { get; init; }

    /// <summary>Optional content-free digest binding a discoverable private replay to its admitted request.</summary>
    public string? RecoveryRequestDigest { get; init; }

    /// <summary>Gets optional selection evidence explicitly carried by a fingerprint-bound model operation.</summary>
    public Hexalith.EventStore.Contracts.Projections.ProjectionOwnerSelection? OwnerSelection { get; init; }
    /// <summary>Gets optional route selections contained in a coordinated full-replay manifest.</summary>
    public IReadOnlyList<Hexalith.EventStore.Contracts.Projections.ProjectionOwnerSelection>? OwnerSelections { get; init; }
    /// <summary>
    /// Initializes a new <see cref="ReadModelBatch"/>.
    /// </summary>
    /// <param name="scope">The batch identity scope.</param>
    /// <param name="operations">The ordered operations (at least one, unique logical keys).</param>
    /// <exception cref="ArgumentNullException"><paramref name="scope"/> or <paramref name="operations"/> is null.</exception>
    /// <exception cref="ArgumentException">The manifest is empty, contains a null operation, or has duplicate keys.</exception>
    public ReadModelBatch(ReadModelBatchScope scope, IEnumerable<ReadModelBatchOperation> operations) {
        ArgumentNullException.ThrowIfNull(scope);
        ArgumentNullException.ThrowIfNull(operations);
        scope.Validate();

        var materialized = new List<ReadModelBatchOperation>();
        var seenKeys = new HashSet<string>(StringComparer.Ordinal);
        foreach (ReadModelBatchOperation operation in operations) {
            ArgumentNullException.ThrowIfNull(operation);
            if (!seenKeys.Add(operation.Key)) {
                throw new ArgumentException(
                    $"Duplicate logical key '{operation.Key}' in read-model batch; repeated keys are not normalized.",
                    nameof(operations));
            }

            materialized.Add(operation);
        }

        if (materialized.Count == 0) {
            throw new ArgumentException("A read-model batch must contain at least one operation.", nameof(operations));
        }

        Scope = scope;
        Operations = new ReadOnlyCollection<ReadModelBatchOperation>(materialized);
    }

    /// <summary>Gets the batch identity scope.</summary>
    public ReadModelBatchScope Scope { get; }

    /// <summary>Gets the ordered operations. The index of each operation is its ordinal position.</summary>
    public IReadOnlyList<ReadModelBatchOperation> Operations { get; }
}
