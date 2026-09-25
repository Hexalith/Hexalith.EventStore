using System.Net.Http.Json;

using Hexalith.EventStore.Contracts.Effects;

namespace Hexalith.EventStore.Client.Effects;

/// <summary>HTTP implementation of the trusted effect submission contract.</summary>
public sealed class HttpTrustedEffectSubmitter(HttpClient client) : ITrustedEffectSubmitter
{
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
            "api/v1/trusted-effects",
            request,
            cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<TrustedEffectResult>(cancellationToken)
            .ConfigureAwait(false)
            ?? throw new InvalidOperationException("Trusted effect gateway returned no result.");
    }
}
