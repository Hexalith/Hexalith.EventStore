// Effective authority: 08bfd5855998a276999db38447c409b2231f3d4e253d4e371d4d8bfe369462d6; base section 8.2, V046-V048.
namespace Hexalith.EventStore.PayloadProtection;

/// <summary>Signals only a durably observed conflicting key reference, allowing wholly fresh material before encryption.</summary>
internal sealed class PayloadKeyCollisionException() : Exception("Payload key reservation collided.");
