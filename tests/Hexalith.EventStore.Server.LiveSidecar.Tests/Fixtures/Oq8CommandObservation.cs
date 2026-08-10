using System.Net;

namespace Hexalith.EventStore.Server.LiveSidecar.Tests.Fixtures;

/// <summary>Contains a support-safe projection of one public command response.</summary>
/// <param name="StatusCode">The HTTP status.</param>
/// <param name="MessageIdentitySha256">The canonical message-identity digest, when returned.</param>
/// <param name="ResultSha256">The response result digest, when returned.</param>
/// <param name="ReasonCode">The stable problem reason code, when returned.</param>
internal sealed record Oq8CommandObservation(
    HttpStatusCode StatusCode,
    string? MessageIdentitySha256,
    string? ResultSha256,
    string? ReasonCode);
