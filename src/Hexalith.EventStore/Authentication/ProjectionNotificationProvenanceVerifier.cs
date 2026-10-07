using Hexalith.EventStore.Client.Conventions;
using Hexalith.EventStore.Contracts.Projections;
using Hexalith.EventStore.Server.Configuration;
using Hexalith.EventStore.ServiceDefaults.Authentication;

using Microsoft.Extensions.Options;

namespace Hexalith.EventStore.Authentication;

/// <summary>
/// Verifies the signed publisher provenance of a projection-changed callback before any freshness or SignalR effect.
/// </summary>
/// <remarks>
/// The provenance must be a valid workload assertion for this host's <c>DaprInternal</c> audience, issued to an
/// allowed publisher, granting <see cref="EventStoreWorkloadOperations.ProjectionNotify"/>, and short-lived. It must
/// also carry tenant, projection-type, and topic bindings that match the notification exactly. An unbound provenance,
/// such as an authority's client-credentials token, is denied because it could be replayed for any tenant or topic.
/// The topic is derived from the notification's own tenant and projection type, so a body cannot claim a different
/// topic's provenance.
/// </remarks>
public sealed class ProjectionNotificationProvenanceVerifier(
    WorkloadAssertionValidator validator,
    IOptions<ProjectionChangeNotifierOptions> options)
{
    /// <summary>
    /// Returns the bounded denial reason, or <see langword="null"/> when the provenance is valid and bound.
    /// </summary>
    /// <param name="notification">The received notification.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The reason code, or <see langword="null"/>.</returns>
    public async Task<string?> VerifyAsync(ProjectionChangedNotification notification, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(notification);
        WorkloadAssertionEvaluation evaluation = await validator.ValidateAsync(
            DaprInternalAuthenticationOptions.SchemeName,
            notification.Provenance,
            EventStoreWorkloadOperations.ProjectionNotify,
            options.Value.GetEffectiveAllowedPublishers(),
            cancellationToken).ConfigureAwait(false);
        if (!evaluation.Succeeded)
        {
            return evaluation.ReasonCode;
        }

        string topic;
        try
        {
            topic = NamingConventionEngine.GetProjectionChangedTopic(notification.ProjectionType, notification.TenantId);
        }
        catch (ArgumentException)
        {
            // A publisher derives the topic the same way, so an identity that cannot form a topic was never published.
            return WorkloadAuthenticationReasons.BindingMismatch;
        }

        return CheckBinding(evaluation, EventStoreWorkloadAuthenticationDefaults.TenantBindingClaimType, notification.TenantId)
            ?? CheckBinding(evaluation, EventStoreWorkloadAuthenticationDefaults.ProjectionTypeBindingClaimType, notification.ProjectionType)
            ?? CheckBinding(evaluation, EventStoreWorkloadAuthenticationDefaults.TopicBindingClaimType, topic);
    }

    private static string? CheckBinding(WorkloadAssertionEvaluation evaluation, string claimType, string expected)
    {
        if (!evaluation.Bindings.TryGetValue(claimType, out string? bound))
        {
            return WorkloadAuthenticationReasons.BindingMissing;
        }

        return string.Equals(bound, expected, StringComparison.Ordinal) ? null : WorkloadAuthenticationReasons.BindingMismatch;
    }
}
