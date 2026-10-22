namespace Hexalith.EventStore.Contracts.Security;

/// <summary>Candidate exact reservation; retained tenant DigestKey HMAC binds content intent, never unkeyed plaintext SHA.</summary>
/// <param name="Identity">Exact occurrence/root binding.</param>
/// <param name="DigestKeyVersion">Retained tenant fingerprint key version.</param>
/// <param name="ContentIntentHmac">Exact keyed full plaintext intent fingerprint.</param>
/// <param name="ReservationAttemptOrdinal">One-based exact occurrence attempt; only authoritative aborted prior attempts permit a successor.</param>
/// <param name="ProposedKeyReference">Fresh canonical reference; uniqueness must commit before encryption.</param>
public sealed record InteractionOccurrenceRequest(InteractionOccurrenceIdentity Identity, string DigestKeyVersion, string ContentIntentHmac, long ReservationAttemptOrdinal, string ProposedKeyReference)
{
    /// <inheritdoc/>
    public override string ToString() => nameof(InteractionOccurrenceRequest);
}
