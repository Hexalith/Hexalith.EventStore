using ByteAether.Ulid;
using System.Globalization;
using System.Net.Http.Headers;
using System.Text.Json;
using Hexalith.EventStore.Contracts.Identity;
using Hexalith.EventStore.Contracts.Streams;
using Microsoft.Extensions.Configuration;

namespace Hexalith.EventStore.Client.Streams;

/// <summary>Platform-owned bounded adapter for the existing Dapr InspectSourceProofAsync actor method.</summary>
/// <param name="httpClient">The platform HTTP client.</param>
/// <param name="configuration">Trusted sidecar endpoint and existing Dapr API credential configuration.</param>
/// <param name="clock">The observation clock.</param>
public sealed class DaprNativeSourceMetadataReader(HttpClient httpClient, IConfiguration configuration, TimeProvider clock)
    : INativeSourceMetadataReader
{
    /// <inheritdoc/>
    public async Task<NativeSourceObservation?> ReadAsync(AggregateIdentity identity, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(identity); cancellationToken.ThrowIfCancellationRequested();
        try
        {
            string? endpoint = configuration["DAPR_HTTP_ENDPOINT"] ?? Environment.GetEnvironmentVariable("DAPR_HTTP_ENDPOINT");
            if (string.IsNullOrWhiteSpace(endpoint))
            {
                string? portValue = configuration["DAPR_HTTP_PORT"] ?? Environment.GetEnvironmentVariable("DAPR_HTTP_PORT");
                if (!int.TryParse(portValue, NumberStyles.None, CultureInfo.InvariantCulture, out int port) || port is < 1 or > 65535)
                    return null;
                endpoint = "http://127.0.0.1:" + port.ToString(CultureInfo.InvariantCulture);
            }
            if (!Uri.TryCreate(endpoint, UriKind.Absolute, out Uri? sidecar)
                || sidecar.Scheme is not ("http" or "https") || !string.IsNullOrEmpty(sidecar.UserInfo)) return null;
            var uri = new Uri(sidecar, "/v1.0/actors/AggregateActor/"
                + Uri.EscapeDataString(identity.ActorId) + "/method/InspectSourceProofAsync");
            using var request = new HttpRequestMessage(HttpMethod.Post, uri) { Content = new StringContent("{}") };
            request.Content.Headers.ContentType = new MediaTypeHeaderValue("application/json");
            string? token = configuration["DAPR_API_TOKEN"] ?? Environment.GetEnvironmentVariable("DAPR_API_TOKEN");
            if (!string.IsNullOrWhiteSpace(token)) request.Headers.Add("dapr-api-token", token);
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(TimeSpan.FromSeconds(2));
            using HttpResponseMessage response = await httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, timeout.Token).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode || response.Content.Headers.ContentLength is > 8192) return null;
            using Stream content = await response.Content.ReadAsStreamAsync(timeout.Token).ConfigureAwait(false);
            byte[] buffer = new byte[8193]; int used = 0;
            while (used < buffer.Length)
            {
                int count = await content.ReadAsync(buffer.AsMemory(used), timeout.Token).ConfigureAwait(false);
                if (count == 0) break;
                used += count;
            }
            if (used is 0 or > 8192) return null;
            NativeSourceInspection? inspection = JsonSerializer.Deserialize<NativeSourceInspection>(buffer.AsSpan(0, used), JsonSerializerOptions.Web);
            if (inspection is not { Status: 0, Exists: true, Erased: false, Proof: { CodecVersion: 1, Erased: false } proof }
                || !NativeSourceProofCodec.IsValid(proof, identity, inspection.CurrentSequence, proof.RetainedFloor)
                || inspection.CurrentSequence < 0 || proof.HeadSequence != inspection.CurrentSequence
                || proof.TenantId != identity.TenantId || proof.Domain != identity.Domain || proof.AggregateId != identity.AggregateId
                || proof.RetainedFloor < 1 || proof.RetainedFloor - 1 > proof.HeadSequence
                || !Ulid.TryParse(proof.SourceIncarnation, null, out _)
                || proof.CumulativeDigest is not { Length: 64 } || !proof.CumulativeDigest.All(character => character is >= '0' and <= '9' or >= 'a' and <= 'f')
                || (proof.HeadSequence == 0 ? proof.HeadMessageId is not null : string.IsNullOrWhiteSpace(proof.HeadMessageId)
                    || proof.HeadMessageId.Length > 256 || proof.HeadMessageId != proof.HeadMessageId.Trim()
                    || proof.HeadMessageId.Any(char.IsControl))) return null;
            cancellationToken.ThrowIfCancellationRequested();
            return new(proof, clock.GetUtcNow());
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
        catch (Exception exception) when (exception is HttpRequestException or IOException or JsonException or ArgumentException or InvalidOperationException or OperationCanceledException)
        { return null; }
    }
}
