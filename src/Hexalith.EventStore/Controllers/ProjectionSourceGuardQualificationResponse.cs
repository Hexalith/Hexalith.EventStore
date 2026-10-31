namespace Hexalith.EventStore.Controllers;

/// <summary>The existing source guard maintenance success wire shape.</summary>
/// <param name="Qualified">Existing qualified value.</param>
/// <param name="Activated">Existing activated value.</param>
/// <param name="ReasonCode">Existing reasonCode value.</param>
/// <param name="PreservedSequence">Existing preservedSequence value.</param>
public sealed record ProjectionSourceGuardQualificationResponse(
    bool Qualified,
    bool Activated,
    string ReasonCode,
    long PreservedSequence);
