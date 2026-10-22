namespace Hexalith.EventStore.Operations.Models;

/// <summary>
/// Requests durable capture of one raw subscriber dead letter.
/// </summary>
/// <param name="Identity">The safely extracted identity.</param>
/// <param name="Topic">The source dead-letter topic.</param>
/// <param name="Body">The exact structured CloudEvent bytes.</param>
/// <param name="BodySha256">The lowercase SHA-256 body hash.</param>
/// <param name="CapturedAtUtc">The capture time.</param>
public sealed record DeadLetterCaptureRequest(
    DeadLetterSafeIdentity Identity,
    string Topic,
    byte[] Body,
    string BodySha256,
    DateTimeOffset CapturedAtUtc);
