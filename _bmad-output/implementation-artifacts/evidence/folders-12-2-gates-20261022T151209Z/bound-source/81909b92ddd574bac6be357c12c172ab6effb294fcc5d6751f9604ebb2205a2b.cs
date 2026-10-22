namespace Hexalith.EventStore.DomainService;

/// <summary>
/// Declares one full-replay owner alongside one response-only legacy handler. The owner must verify
/// that the legacy handler never writes its persisted selection or generation. This local source
/// declaration does not certify installation-wide writer coverage or enroll a source guard.
/// </summary>
/// <param name="OwnerHandlerType">The exact, sole named writer implementation for the domain.</param>
/// <param name="ResponseOnlyLegacyHandlerType">The exact, sole legacy response handler implementation.</param>
public sealed record DomainProjectionLegacyCoexistence(Type OwnerHandlerType, Type ResponseOnlyLegacyHandlerType);
