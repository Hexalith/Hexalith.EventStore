// Effective authority: eb45653553efe53ba83e6dd6c9796b47dfb6fb0bba0628ff95f6bc1b96b054c9; base section 8.2, V046-V048.
namespace Hexalith.EventStore.PayloadProtection;

/// <summary>Signals only a durably observed conflicting key reference, allowing wholly fresh material before encryption.</summary>
internal sealed class PayloadKeyCollisionException() : Exception("Payload key reservation collided.");
