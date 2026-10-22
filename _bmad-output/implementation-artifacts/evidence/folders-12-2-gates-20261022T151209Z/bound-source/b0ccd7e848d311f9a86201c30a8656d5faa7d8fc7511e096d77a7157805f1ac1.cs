namespace Hexalith.EventStore.Contracts.Security;

/// <summary>
/// Couples an event protection result to its optional call-scoped persistence completion context.
/// Implements Story 8.1 sections 9 and 10.3 under normative digest
/// <c>de9ba8866fd98a480629890ee2b89a492fbad96d4d5a927388e6aaa0fdd72b4e</c>.
/// </summary>
/// <param name="ProtectionResult">The transformed event bytes, format, and metadata.</param>
/// <param name="CompletionContext">The required v2 reservation context, or <see langword="null"/> for non-v2 results.</param>
public sealed record PayloadProtectionWriteResult(
    PayloadProtectionResult ProtectionResult,
    PayloadProtectionCompletionContext? CompletionContext) {
    /// <summary>Gets the validated transformed event bytes, format, and metadata.</summary>
    public PayloadProtectionResult ProtectionResult { get; init; } = Validate(ProtectionResult, CompletionContext);

    private static PayloadProtectionResult Validate(
        PayloadProtectionResult ProtectionResult,
        PayloadProtectionCompletionContext? CompletionContext) {
        ArgumentNullException.ThrowIfNull(ProtectionResult);
        bool isV2 = string.Equals(ProtectionResult.SerializationFormat, "json+pdenc-v2", StringComparison.Ordinal)
            || string.Equals(ProtectionResult.Metadata?.Scheme, "hexalith-pdenc-v2", StringComparison.Ordinal);
        if (isV2 == (CompletionContext is null)) {
            throw new ArgumentException("The completion context must be present exactly for pdenc-v2 results.", nameof(CompletionContext));
        }

        return ProtectionResult;
    }

    /// <summary>Returns a bounded diagnostic name without payload or completion details.</summary>
    /// <returns>The contract type name.</returns>
    public override string ToString() => nameof(PayloadProtectionWriteResult);
}
