using System.Text.Json;

namespace Hexalith.EventStore.Server.Projections;

public sealed partial class DaprProjectionActivationOutbox
{
    async Task<ProjectionActivationWorkItem?> IProjectionLegacyDeliveryOutbox.TryBindLegacyDeliveryAsync(
        ProjectionActivationWorkItem expected, ProjectionLegacyDeliveryIntent intent, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(expected);
        ArgumentNullException.ThrowIfNull(intent);
        ArgumentException.ThrowIfNullOrWhiteSpace(intent.OperationId);
        ArgumentException.ThrowIfNullOrWhiteSpace(intent.AppId);
        ArgumentException.ThrowIfNullOrWhiteSpace(intent.ServiceVersion);
        ArgumentException.ThrowIfNullOrWhiteSpace(intent.HeadMessageId);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(intent.HeadSequence);
        using (var size = new SizeLimitedWriteStream(4096))
        {
            try { JsonSerializer.Serialize(size, intent, daprClient.JsonSerializerOptions); }
            catch (ProjectionRebuildPrefixSafetyLimitExceededException)
            {
                throw new ArgumentException("Legacy replay references exceed the bounded intent profile.", nameof(intent));
            }
        }
        if (expected.LegacyDelivery is { } previous && !CanBind(previous, intent)) return null;
        if (expected.LegacyDelivery is { InvocationToken: not null } claimed
            && claimed.InvocationToken != intent.InvocationToken && claimed.LeaseExpiresAtUtc > timeProvider.GetUtcNow()) return null;
        if (intent.InvocationToken is not null && (intent.LeaseExpiresAtUtc is not { } expiry
            || expiry <= timeProvider.GetUtcNow() || expiry > timeProvider.GetUtcNow() + TimeSpan.FromMinutes(5))) return null;

        ProjectionActivationWorkItem? bound = null;
        await MutateAsync(expected.ActivationId, (items, _) =>
        {
            bound = null;
            int index = items.FindIndex(item => SameActivation(item, expected));
            if (index < 0 || items[index].Revision != expected.Revision
                || items[index].LegacyDelivery != expected.LegacyDelivery) return false;
            bound = items[index] with { LegacyDelivery = intent };
            items[index] = bound;
            return true;
        }, cancellationToken, maxLedgerBytes: 16_384).ConfigureAwait(false);
        return bound;
    }

    async Task<bool> IProjectionLegacyDeliveryOutbox.TryCompleteLegacyDeliveryAsync(
        ProjectionActivationWorkItem expected, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(expected);
        if (expected.LegacyDelivery is null) return false;
        bool completed = false;
        await MutateAsync(expected.ActivationId, (items, _) =>
        {
            completed = false;
            int index = items.FindIndex(item => SameActivation(item, expected));
            if (index < 0 || items[index].LegacyDelivery != expected.LegacyDelivery) return false;
            // EnsureAsync may have raised Revision for an append while this frozen work ran.
            // Retain that activation; its later delivery samples its own head.
            items[index] = items[index] with { LegacyDelivery = null };
            completed = true;
            return true;
        }, cancellationToken).ConfigureAwait(false);
        return completed;
    }

    private static bool SameActivation(ProjectionActivationWorkItem actual, ProjectionActivationWorkItem expected)
        => actual.ActivationId == expected.ActivationId && actual.TenantId == expected.TenantId
            && actual.Domain == expected.Domain && actual.AggregateId == expected.AggregateId;

    private static bool CanBind(ProjectionLegacyDeliveryIntent previous, ProjectionLegacyDeliveryIntent next)
        => previous.OperationId == next.OperationId && previous.AppId == next.AppId
            && previous.ServiceVersion == next.ServiceVersion && previous.HeadSequence == next.HeadSequence
            && previous.HeadMessageId == next.HeadMessageId
            && previous.FrozenActivationRevision == next.FrozenActivationRevision
            && previous.SourceProof == next.SourceProof
            && (previous.ProjectionType is null || previous.ProjectionType == next.ProjectionType)
            && (previous.SourceFingerprint is null || previous.SourceFingerprint == next.SourceFingerprint)
            && (previous.CandidateFingerprint is null || previous.CandidateFingerprint == next.CandidateFingerprint);
}
