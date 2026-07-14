namespace Hexalith.EventStore.Server.Projections;

/// <summary>Indicates that authoritative history proves a persisted checkpoint cannot be hydrated safely.</summary>
internal sealed class ProjectionDeliveryHistoryValidationException : InvalidOperationException {
    public ProjectionDeliveryHistoryValidationException(string message)
        : base(message) {
    }
}
