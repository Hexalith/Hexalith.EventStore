using System.Net.Http.Json;

using Hexalith.EventStore.Contracts.Effects;

namespace Hexalith.EventStore.Client.Effects;

/// <summary>HTTP implementation of the trusted effect submission contract.</summary>
/// <remarks>
/// The gateway admits a submission only from an authenticated internal workload: the request must cross the
/// gateway's Dapr app channel and carry the submitting domain service's own short-lived workload assertion granting
/// <c>eventstore:trusted-effect</c>. A domain service configures the client with
/// <c>AddEventStoreTrustedEffectWorkloadAssertion()</c> (Hexalith.EventStore.DomainService) before
/// <c>AddEventStoreDaprServiceInvocation("eventstore")</c>; without the assertion every submission is refused with
/// <c>401 Unauthorized</c> and nothing is admitted.
/// </remarks>
public sealed class HttpTrustedEffectSubmitter(HttpClient client) : ITrustedEffectSubmitter
{
    /// <summary>Gets the gateway route that receives trusted-effect submissions.</summary>
    public const string Route = "api/v1/trusted-effects";

    /// <inheritdoc/>
    public async Task<TrustedEffectResult> SubmitAsync(
        TrustedEffectSubmission submission,
        TrustedEffectContext context,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(submission);
        ArgumentNullException.ThrowIfNull(context);
        var request = new TrustedEffectSubmitRequest(
            submission,
            context.Purpose,
            context.CausationId,
            context.DelegationToken);
        using HttpResponseMessage response = await client.PostAsJsonAsync(
            Route,
            request,
            cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<TrustedEffectResult>(cancellationToken)
            .ConfigureAwait(false)
            ?? throw new InvalidOperationException("Trusted effect gateway returned no result.");
    }
}
