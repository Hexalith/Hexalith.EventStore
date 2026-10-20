using Hexalith.EventStore.Contracts.Projections;
using Hexalith.EventStore.ServiceDefaults.Authentication;

using Microsoft.Extensions.Options;

namespace Hexalith.EventStore.Server.Configuration;

/// <summary>
/// Validates projection change notifier configuration.
/// </summary>
/// <param name="provenanceIssuer">
/// The issuer of notification provenance. The pub/sub transport is valid only when it can bind provenance to the
/// notification's tenant, projection type, and topic.
/// </param>
public sealed class ValidateProjectionChangeNotifierOptions(IWorkloadAssertionIssuer? provenanceIssuer = null)
    : IValidateOptions<ProjectionChangeNotifierOptions>
    {
    /// <inheritdoc/>
    public ValidateOptionsResult Validate(string? name, ProjectionChangeNotifierOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        if (string.IsNullOrWhiteSpace(options.PubSubName))
        {
            return ValidateOptionsResult.Fail("Projection change pub/sub component name must not be empty.");
        }

        if (!Enum.IsDefined(typeof(ProjectionChangeTransport), options.Transport))
        {
            return ValidateOptionsResult.Fail("Projection change transport must be a defined value.");
        }

        // Story 5.5: the receiver denies unbound provenance, so pub/sub without a binding issuer would silently drop
        // every notification. Refuse it before the host starts.
        if (options.Transport == ProjectionChangeTransport.PubSub && provenanceIssuer?.CanBindResources != true)
        {
            return ValidateOptionsResult.Fail(
                "Projection change pub/sub transport requires a provenance issuer that binds each notification to its tenant, projection type, and topic. An OIDC authority cannot bind them; use Transport=Direct.");
        }

        if (options.MaxDetailMetadataEntries is < 1 or > ProjectionChangedMetadataLimits.MaximumEntries)
        {
            return ValidateOptionsResult.Fail("Projection change MaxDetailMetadataEntries must be between 1 and 16.");
        }

        if (options.MaxDetailMetadataBytes is < ProjectionChangedMetadataLimits.MinimumSerializedUtf8Bytes
            or > ProjectionChangedMetadataLimits.MaximumSerializedUtf8Bytes)
            {
            return ValidateOptionsResult.Fail("Projection change MaxDetailMetadataBytes must be between 2 and 2048 serialized UTF-8 bytes.");
        }

        if (string.IsNullOrWhiteSpace(options.ProvenanceAudience))
        {
            return ValidateOptionsResult.Fail("Projection change ProvenanceAudience must not be empty.");
        }

        return ValidateOptionsResult.Success;
    }
}
