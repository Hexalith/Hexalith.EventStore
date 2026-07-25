using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace Hexalith.EventStore.IntegrationTests.Helpers;

/// <summary>
/// Polls a Dapr service-invocation boundary until it reaches the expected availability state.
/// </summary>
internal static class DaprInvocationReadinessProbe {
    /// <summary>
    /// The side-effect-free sample capability invoked through EventStore's Dapr sidecar.
    /// </summary>
    internal const string SampleInvocationPath
        = "/v1.0/invoke/sample/method/admin/operational-index-metadata";

    private const string _directInvokeErrorCode = "ERR_DIRECT_INVOKE";
    private const int _maxDiagnosticBodyLength = 512;
    private static readonly TimeSpan _probeRequestTimeout = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan _probeRetryDelay = TimeSpan.FromMilliseconds(250);

    /// <summary>
    /// Waits until the supplied probe observes the expected Dapr invocation state.
    /// </summary>
    /// <param name="expectedReady">Whether a successful response is expected.</param>
    /// <param name="probeAsync">Sends one side-effect-free invocation probe.</param>
    /// <param name="timeout">The complete polling timeout.</param>
    /// <param name="retryDelay">The delay between attempts.</param>
    /// <param name="cancellationToken">The caller cancellation token.</param>
    /// <param name="delayAsync">Optional delay implementation used by deterministic tests.</param>
    /// <returns>A task that completes when the expected state is observed.</returns>
    /// <exception cref="TimeoutException">The expected state was not observed before <paramref name="timeout"/>.</exception>
    internal static async Task WaitAsync(
        bool expectedReady,
        Func<CancellationToken, Task<HttpResponseMessage>> probeAsync,
        TimeSpan timeout,
        TimeSpan retryDelay,
        CancellationToken cancellationToken,
        Func<TimeSpan, CancellationToken, Task>? delayAsync = null) {
        ArgumentNullException.ThrowIfNull(probeAsync);
        if (timeout <= TimeSpan.Zero) {
            throw new ArgumentOutOfRangeException(nameof(timeout));
        }

        if (retryDelay < TimeSpan.Zero) {
            throw new ArgumentOutOfRangeException(nameof(retryDelay));
        }

        delayAsync ??= static (delay, token) => Task.Delay(delay, token);

        using var recoveryCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        recoveryCts.CancelAfter(timeout);

        Exception? lastError = null;
        HttpStatusCode? lastStatusCode = null;
        string? lastResponseBody = null;

        while (!recoveryCts.IsCancellationRequested) {
            lastError = null;
            lastStatusCode = null;
            lastResponseBody = null;

            try {
                using HttpResponseMessage response = await probeAsync(recoveryCts.Token).ConfigureAwait(false);
                lastStatusCode = response.StatusCode;
                if (expectedReady && response.IsSuccessStatusCode) {
                    return;
                }

                string responseBody = await response.Content
                    .ReadAsStringAsync(recoveryCts.Token)
                    .ConfigureAwait(false);
                lastResponseBody = TruncateForDiagnostic(responseBody);
                if (!expectedReady && IsDirectInvocationUnavailable(response.StatusCode, responseBody)) {
                    return;
                }
            }
            catch (HttpRequestException ex) {
                lastError = ex;
                lastStatusCode = null;
                lastResponseBody = null;
            }
            catch (OperationCanceledException ex) when (!cancellationToken.IsCancellationRequested) {
                lastError = ex;
            }

            try {
                await delayAsync(retryDelay, recoveryCts.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested) {
                break;
            }
        }

        cancellationToken.ThrowIfCancellationRequested();
        throw new TimeoutException(
            $"Sample Dapr invocation did not become {(expectedReady ? "ready" : "unavailable")} "
            + $"within {timeout}. "
            + $"Last status: {lastStatusCode?.ToString() ?? "no response"}. "
            + $"Last body: {(lastResponseBody is null ? "not available" : JsonSerializer.Serialize(lastResponseBody))}.",
            lastError);
    }

    /// <summary>
    /// Waits until EventStore's Dapr sidecar reports the expected availability of the sample
    /// domain service.
    /// </summary>
    /// <remarks>
    /// Aspire resource health is not sufficient on its own. The topology reuses the fixed
    /// <c>sample</c> app-id across fixtures, so placement can still route to a torn-down
    /// instance after the resource reports healthy, and the first command a fixture issues then
    /// fails with HTTP 500. This probes the exact boundary domain-service invocation depends on.
    /// </remarks>
    /// <param name="eventStoreDaprHttpEndpoint">EventStore's Dapr sidecar HTTP endpoint.</param>
    /// <param name="expectedReady">Whether a successful invocation is expected.</param>
    /// <param name="timeout">The complete polling timeout.</param>
    /// <param name="cancellationToken">The caller cancellation token.</param>
    /// <returns>A task that completes when the expected state is observed.</returns>
    internal static async Task WaitForSampleInvocationAsync(
        Uri eventStoreDaprHttpEndpoint,
        bool expectedReady,
        TimeSpan timeout,
        CancellationToken cancellationToken) {
        ArgumentNullException.ThrowIfNull(eventStoreDaprHttpEndpoint);

        using var eventStoreDaprClient = new HttpClient {
            BaseAddress = eventStoreDaprHttpEndpoint,
            Timeout = _probeRequestTimeout,
        };

        await WaitAsync(
                expectedReady,
                probeAsync: probeCancellationToken => eventStoreDaprClient.PostAsJsonAsync(
                        SampleInvocationPath,
                        new { Domains = Array.Empty<string>() },
                        probeCancellationToken),
                timeout,
                retryDelay: _probeRetryDelay,
                cancellationToken)
            .ConfigureAwait(false);
    }

    /// <summary>
    /// Determines whether a Dapr response is the exact direct-invocation unavailable error.
    /// </summary>
    /// <param name="statusCode">The response status code.</param>
    /// <param name="responseBody">The response body.</param>
    /// <returns><see langword="true"/> only for HTTP 500 with string error code <c>ERR_DIRECT_INVOKE</c>.</returns>
    internal static bool IsDirectInvocationUnavailable(HttpStatusCode statusCode, string responseBody) {
        if (statusCode != HttpStatusCode.InternalServerError) {
            return false;
        }

        try {
            using JsonDocument document = JsonDocument.Parse(responseBody);
            return document.RootElement.ValueKind == JsonValueKind.Object
                && document.RootElement.TryGetProperty("errorCode", out JsonElement errorCode)
                && errorCode.ValueKind == JsonValueKind.String
                && string.Equals(errorCode.GetString(), _directInvokeErrorCode, StringComparison.Ordinal);
        }
        catch (JsonException) {
            return false;
        }
    }

    private static string TruncateForDiagnostic(string responseBody)
        => responseBody.Length <= _maxDiagnosticBodyLength
            ? responseBody
            : responseBody[.._maxDiagnosticBodyLength] + "…";
}
