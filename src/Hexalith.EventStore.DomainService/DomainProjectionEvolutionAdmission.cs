using Hexalith.EventStore.Contracts.Projections;

namespace Hexalith.EventStore.DomainService;

/// <summary>Fences every legacy projection intake from unverified versioned evolution hints.</summary>
internal static class DomainProjectionEvolutionAdmission
{
    /// <summary>Refuses effective-view or proof hints before any legacy handler or store is selected.</summary>
    internal static void RequireLegacy(ProjectionRequest request)
    {
        if (request.VerifiedEffectiveEvents is not null || request.EventEvolutionProof is not null)
        {
            throw new ProjectionDispatchValidationException(ProjectionDispatchReasonCodes.UnsupportedCapability);
        }

        RequireLegacyEvents(request.Events);
    }

    /// <summary>Refuses canonical version metadata before named or shared rebuild callbacks.</summary>
    internal static void RequireLegacyEvents(IReadOnlyList<ProjectionEventDto>? events)
    {
        if (events?.Any(static item => item is not null
            && (item.MetadataVersion is not (null or 1)
                || item.StoredEventContractType is not null || item.StoredPayloadVersion is < 1 or > 1024
                || item.StoredSerializationFormat is not null || item.StoredEventTypeName is not null
                || item.StoredDigest is not null || item.RegistryFingerprint is not null || item.IsAdapted is not null
                || item.EffectiveEventContractType is not null || item.EffectivePayloadVersion is not null
                || item.EffectiveSerializationFormat is not null || item.EffectivePayload is not null)) == true)
        {
            throw new ProjectionDispatchValidationException(ProjectionDispatchReasonCodes.UnsupportedCapability);
        }
    }
}
