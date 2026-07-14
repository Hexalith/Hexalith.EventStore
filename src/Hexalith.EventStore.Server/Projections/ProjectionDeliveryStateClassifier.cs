namespace Hexalith.EventStore.Server.Projections;

/// <summary>Applies the fail-closed delivery-row version classification rules.</summary>
internal static class ProjectionDeliveryStateClassifier {
    /// <summary>Classifies a persisted row relative to the global writer cutover.</summary>
    public static ProjectionDeliveryStateClassification Classify(
        ProjectionDeliveryState? state,
        bool protocolV2Active) {
        if (state is null) {
            return ProjectionDeliveryStateClassification.Absent;
        }

        if (state.SchemaVersion == ProjectionDeliveryState.CurrentSchemaVersion
            && state.WriterProtocolVersion == ProjectionDeliveryState.CurrentWriterProtocolVersion) {
            return ProjectionDeliveryStateClassification.Current;
        }

        bool fiveFieldRow = state.SchemaVersion == 0 && state.WriterProtocolVersion == 0;
        if (fiveFieldRow) {
            if (protocolV2Active) {
                return ProjectionDeliveryStateClassification.SchemaRegression;
            }

            return state.LastDeliveredSequence == 0
                ? ProjectionDeliveryStateClassification.LegacyZero
                : ProjectionDeliveryStateClassification.LegacyNonZero;
        }

        if (protocolV2Active && state.WriterProtocolVersion < ProjectionDeliveryState.CurrentWriterProtocolVersion) {
            return ProjectionDeliveryStateClassification.SchemaRegression;
        }

        return ProjectionDeliveryStateClassification.Unsupported;
    }
}
