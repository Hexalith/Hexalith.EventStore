namespace Hexalith.EventStore.Server.Actors;

/// <summary>Retains an already committed EventsStored checkpoint when exact original protection completion is unavailable.</summary>
internal sealed class PayloadProtectionRecoveryException(Exception innerException)
    : Exception("Original payload-protection completion remains pending.", innerException);
