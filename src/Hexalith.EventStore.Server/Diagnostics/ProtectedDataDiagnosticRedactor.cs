using System.Diagnostics;

using Hexalith.EventStore.Contracts.Commands;
using Hexalith.EventStore.Contracts.Problems;
using Hexalith.EventStore.Contracts.Security;
using Hexalith.EventStore.Server.Events;

namespace Hexalith.EventStore.Server.Diagnostics;

/// <summary>
/// Central runtime boundary for protected-data-capable diagnostic text.
/// </summary>
internal static class ProtectedDataDiagnosticRedactor {
    public const string DefaultReasonCode = "protected-data-diagnostic-redacted";
    public const string DefaultStage = "unspecified";

    public static string RedactException(Exception exception, string? stage)
        => BuildSafeText(GetReasonCode(exception), GetStage(exception, stage));

    public static string BuildSafeText(string? reasonCode, string? stage)
        => $"Protected data diagnostic details were redacted. ReasonCode={SafeReasonCode(reasonCode)}; Stage={SafeStage(stage)}.";

    public static string NormalizeDiagnosticText(string? diagnosticText, string? stage) {
        string safeStage = SafeStage(stage);
        if (TryParseSafeText(diagnosticText, out string? reasonCode, out string? parsedStage)) {
            return BuildSafeText(reasonCode, parsedStage);
        }

        return BuildSafeText(DefaultReasonCode, safeStage);
    }

    public static IReadOnlyDictionary<string, object?> BuildUnreadableProblemExtensions(
        ProtectedDataReadabilityDecision decision) {
        ArgumentNullException.ThrowIfNull(decision);

        UnreadableProtectedDataReason reason = decision.UnreadableReason!.Value;
        var extensions = new Dictionary<string, object?> {
            ["reasonCode"] = SafeReasonCode(decision.ReasonCode),
            [UnreadableProtectedDataProblem.ExtensionReasonCategory] = reason.ToString(),
            [UnreadableProtectedDataProblem.ExtensionStage] = ProtectedDataReadabilityDecisionStageCodes.From(decision.Stage),
            [GatewayProblemDetailsExtensions.TenantId] = decision.TenantId,
            [UnreadableProtectedDataProblem.ExtensionDomain] = decision.Domain,
            [UnreadableProtectedDataProblem.ExtensionMetadataVersion] = decision.MetadataVersion,
            [UnreadableProtectedDataProblem.ExtensionRetryable] = decision.IsRetryable,
            [UnreadableProtectedDataProblem.ExtensionPermanent] = decision.IsPermanent,
        };

        if (!string.IsNullOrWhiteSpace(decision.CorrelationId)) {
            extensions[GatewayProblemDetailsExtensions.CorrelationId] = decision.CorrelationId;
        }

        if (decision.SequenceNumber.HasValue) {
            extensions[UnreadableProtectedDataProblem.ExtensionSequenceNumber] = decision.SequenceNumber.Value;
        }

        if (!string.IsNullOrWhiteSpace(decision.AggregateId)) {
            extensions[UnreadableProtectedDataProblem.ExtensionAggregateId] = decision.AggregateId;
        }

        return extensions;
    }

    public static void RecordActivityException(Activity? activity, Exception exception, string? stage) {
        if (activity is null) {
            return;
        }

        string safeText = RedactException(exception, stage);
        _ = activity.SetStatus(ActivityStatusCode.Error, safeText);
        _ = activity.SetTag("eventstore.protected_data_diagnostic_redacted", true);
        _ = activity.SetTag("eventstore.failure_stage", SafeStage(stage));
        activity.AddEvent(new ActivityEvent(
            "exception",
            tags: new ActivityTagsCollection {
                ["exception.type"] = exception.GetType().FullName ?? exception.GetType().Name,
                ["exception.message"] = safeText,
                ["eventstore.protected_data_diagnostic_redacted"] = true,
            }));
    }

    private static string GetReasonCode(Exception exception)
        => exception is ProtectedDataUnreadableException protectedException
            ? protectedException.ReasonCode
            : DefaultReasonCode;

    private static string? GetStage(Exception exception, string? fallbackStage)
        => exception is ProtectedDataUnreadableException protectedException
                && !string.IsNullOrWhiteSpace(protectedException.Stage)
            ? protectedException.Stage
            : fallbackStage;

    private static string SafeReasonCode(string? value)
        => value switch {
            DefaultReasonCode => DefaultReasonCode,
            ProtectedDataReadabilityDecision.ReadableCode => ProtectedDataReadabilityDecision.ReadableCode,
            ProtectedDataReadabilityDecision.DeferredValidationCode => ProtectedDataReadabilityDecision.DeferredValidationCode,
            ProtectedDataReadabilityDecision.RestoreConflictCode => ProtectedDataReadabilityDecision.RestoreConflictCode,
            ProtectedDataReadabilityDecision.QuarantineRequiredCode => ProtectedDataReadabilityDecision.QuarantineRequiredCode,
            ProtectedDataReadabilityDecision.OperatorDecisionRequiredCode => ProtectedDataReadabilityDecision.OperatorDecisionRequiredCode,
            UnreadableProtectedDataReasonCodes.MissingKey => UnreadableProtectedDataReasonCodes.MissingKey,
            UnreadableProtectedDataReasonCodes.KeyInvalidatedOrDeleted => UnreadableProtectedDataReasonCodes.KeyInvalidatedOrDeleted,
            UnreadableProtectedDataReasonCodes.ProviderUnavailable => UnreadableProtectedDataReasonCodes.ProviderUnavailable,
            UnreadableProtectedDataReasonCodes.ProviderDenied => UnreadableProtectedDataReasonCodes.ProviderDenied,
            UnreadableProtectedDataReasonCodes.ConsistencyMismatch => UnreadableProtectedDataReasonCodes.ConsistencyMismatch,
            UnreadableProtectedDataReasonCodes.MalformedMetadata => UnreadableProtectedDataReasonCodes.MalformedMetadata,
            UnreadableProtectedDataReasonCodes.UnknownMetadataVersion => UnreadableProtectedDataReasonCodes.UnknownMetadataVersion,
            UnreadableProtectedDataReasonCodes.ProviderOpaqueUnsupportedOperation => UnreadableProtectedDataReasonCodes.ProviderOpaqueUnsupportedOperation,
            UnreadableProtectedDataReasonCodes.BytesMetadataMismatch => UnreadableProtectedDataReasonCodes.BytesMetadataMismatch,
            _ => DefaultReasonCode,
        };

    private static string SafeStage(string? value)
        => value switch {
            DefaultStage => DefaultStage,
            "pipeline" => "pipeline",
            "command-status" => "command-status",
            "dead-letter-publication" => "dead-letter-publication",
            "drain" => "drain",
            nameof(CommandStatus.Processing) => nameof(CommandStatus.Processing),
            nameof(CommandStatus.EventsStored) => nameof(CommandStatus.EventsStored),
            nameof(CommandStatus.EventsPublished) => nameof(CommandStatus.EventsPublished),
            nameof(CommandStatus.Rejected) => nameof(CommandStatus.Rejected),
            nameof(CommandStatus.PublishFailed) => nameof(CommandStatus.PublishFailed),
            nameof(CommandStatus.TimedOut) => nameof(CommandStatus.TimedOut),
            ProtectedDataReadabilityDecisionStageCodes.Rehydrate => ProtectedDataReadabilityDecisionStageCodes.Rehydrate,
            ProtectedDataReadabilityDecisionStageCodes.Publish => ProtectedDataReadabilityDecisionStageCodes.Publish,
            ProtectedDataReadabilityDecisionStageCodes.Replay => ProtectedDataReadabilityDecisionStageCodes.Replay,
            ProtectedDataReadabilityDecisionStageCodes.Rebuild => ProtectedDataReadabilityDecisionStageCodes.Rebuild,
            ProtectedDataReadabilityDecisionStageCodes.SnapshotLoad => ProtectedDataReadabilityDecisionStageCodes.SnapshotLoad,
            ProtectedDataReadabilityDecisionStageCodes.BackupAdmission => ProtectedDataReadabilityDecisionStageCodes.BackupAdmission,
            ProtectedDataReadabilityDecisionStageCodes.AdminInspection => ProtectedDataReadabilityDecisionStageCodes.AdminInspection,
            _ => DefaultStage,
        };

    private static bool TryParseSafeText(string? diagnosticText, out string? reasonCode, out string? stage) {
        reasonCode = null;
        stage = null;
        const string prefix = "Protected data diagnostic details were redacted. ReasonCode=";
        const string separator = "; Stage=";
        const string suffix = ".";
        if (string.IsNullOrWhiteSpace(diagnosticText)
            || !diagnosticText.StartsWith(prefix, StringComparison.Ordinal)
            || !diagnosticText.EndsWith(suffix, StringComparison.Ordinal)) {
            return false;
        }

        string body = diagnosticText[prefix.Length..^suffix.Length];
        int separatorIndex = body.IndexOf(separator, StringComparison.Ordinal);
        if (separatorIndex <= 0) {
            return false;
        }

        reasonCode = body[..separatorIndex];
        stage = body[(separatorIndex + separator.Length)..];
        return true;
    }
}
