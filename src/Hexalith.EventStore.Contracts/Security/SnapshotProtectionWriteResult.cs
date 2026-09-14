namespace Hexalith.EventStore.Contracts.Security;

/// <summary>
/// Couples a snapshot protection result to its optional call-scoped persistence completion context.
/// Implements Story 8.1 sections 9 and 10.3 under normative digest
/// <c>de9ba8866fd98a480629890ee2b89a492fbad96d4d5a927388e6aaa0fdd72b4e</c>.
/// </summary>
/// <param name="ProtectionResult">The transformed snapshot state and metadata.</param>
/// <param name="CompletionContext">The required v2 reservation context, or <see langword="null"/> for non-v2 results.</param>
public sealed record SnapshotProtectionWriteResult(
    SnapshotProtectionResult ProtectionResult,
    PayloadProtectionCompletionContext? CompletionContext) {
    /// <summary>Gets the validated transformed snapshot state and metadata.</summary>
    public SnapshotProtectionResult ProtectionResult { get; init; } = Validate(ProtectionResult, CompletionContext);

    private static SnapshotProtectionResult Validate(
        SnapshotProtectionResult ProtectionResult,
        PayloadProtectionCompletionContext? CompletionContext) {
        ArgumentNullException.ThrowIfNull(ProtectionResult);
        bool isV2 = ProtectionResult.State is ProtectedSnapshotPayloadV2
            || string.Equals(ProtectionResult.Metadata?.Scheme, "hexalith-pdenc-v2", StringComparison.Ordinal);
        if (isV2 == (CompletionContext is null)) {
            throw new ArgumentException("The completion context must be present exactly for pdenc-v2 results.", nameof(CompletionContext));
        }

        return ProtectionResult;
    }

    /// <summary>Returns a bounded diagnostic name without snapshot or completion details.</summary>
    /// <returns>The contract type name.</returns>
    public override string ToString() => nameof(SnapshotProtectionWriteResult);
}
