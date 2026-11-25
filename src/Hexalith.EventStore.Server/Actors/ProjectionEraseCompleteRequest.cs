namespace Hexalith.EventStore.Server.Actors;

/// <summary>Request to complete an erase operation.</summary>
/// <param name="OperationId">The in-flight erase operation identifier (must match the admitted operation).</param>
public sealed record ProjectionEraseCompleteRequest(string OperationId);
