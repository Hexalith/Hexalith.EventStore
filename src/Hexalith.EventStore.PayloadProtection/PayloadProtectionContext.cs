// Normative authority: de9ba8866fd98a480629890ee2b89a492fbad96d4d5a927388e6aaa0fdd72b4e; sections 7, 8, 14, and 15.
using Hexalith.EventStore.Contracts.Identity;
using Hexalith.EventStore.Contracts.Security;

namespace Hexalith.EventStore.PayloadProtection;

/// <summary>
/// Carries the trusted occurrence identity used by the pdenc-v2 AAD codec (normative section 7.1).
/// </summary>
/// <param name="Identity">The authenticated aggregate identity.</param>
/// <param name="PayloadTypeId">The persisted event type or stable snapshot type identifier.</param>
/// <param name="PayloadKind">The payload kind.</param>
/// <param name="RecordSequence">The aggregate-local record sequence.</param>
internal sealed record PayloadProtectionContext(
    AggregateIdentity Identity,
    string PayloadTypeId,
    PayloadProtectionPayloadKind PayloadKind,
    ulong RecordSequence)
{
    /// <inheritdoc/>
    public override string ToString() => nameof(PayloadProtectionContext);
}
