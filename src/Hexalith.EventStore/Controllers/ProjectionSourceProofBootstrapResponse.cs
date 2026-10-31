namespace Hexalith.EventStore.Controllers;

/// <summary>The existing source bootstrap success wire shape.</summary>
/// <param name="Status">Existing status value.</param>
/// <param name="CurrentSequence">Existing currentSequence value.</param>
/// <param name="Erased">Existing erased value.</param>
/// <param name="SourceIncarnation">Existing sourceIncarnation value.</param>
public sealed record ProjectionSourceProofBootstrapResponse(
    Hexalith.EventStore.Server.Actors.SourceProofInspectionStatus Status,
    long CurrentSequence,
    bool Erased,
    string? SourceIncarnation);
