using System.Net;
using System.Text;

using Shouldly;

namespace Hexalith.EventStore.IntegrationTests.Helpers;

/// <summary>
/// Tests the deterministic branches of <see cref="DaprInvocationReadinessProbe"/>.
/// </summary>
public class DaprInvocationReadinessProbeTests {
    /// <summary>
    /// Verifies that only the exact Dapr unavailable response is accepted.
    /// </summary>
    /// <param name="statusCode">The response status.</param>
    /// <param name="responseBody">The response body.</param>
    [Theory]
    [InlineData(HttpStatusCode.BadGateway, "{\"errorCode\":\"ERR_DIRECT_INVOKE\"}")]
    [InlineData(HttpStatusCode.InternalServerError, "{")]
    [InlineData(HttpStatusCode.InternalServerError, "null")]
    [InlineData(HttpStatusCode.InternalServerError, "[]")]
    [InlineData(HttpStatusCode.InternalServerError, "\"ERR_DIRECT_INVOKE\"")]
    [InlineData(HttpStatusCode.InternalServerError, "{\"errorCode\":42}")]
    [InlineData(HttpStatusCode.InternalServerError, "{\"errorCode\":\"ERR_OTHER\"}")]
    public void IsDirectInvocationUnavailable_UnexpectedResponse_ReturnsFalse(
        HttpStatusCode statusCode,
        string responseBody) {
        DaprInvocationReadinessProbe.IsDirectInvocationUnavailable(statusCode, responseBody).ShouldBeFalse();
    }

    /// <summary>
    /// Verifies the exact Dapr direct-invocation unavailable response is accepted.
    /// </summary>
    [Fact]
    public void IsDirectInvocationUnavailable_ExactDaprError_ReturnsTrue() {
        DaprInvocationReadinessProbe.IsDirectInvocationUnavailable(
                HttpStatusCode.InternalServerError,
                "{\"errorCode\":\"ERR_DIRECT_INVOKE\"}")
            .ShouldBeTrue();
    }

    /// <summary>
    /// Verifies timeout diagnostics retain the last unexpected status and response body.
    /// </summary>
    [Fact]
    public async Task WaitAsync_UnexpectedResponse_ReportsStatusAndBodyAsync() {
        TimeoutException exception = await Should.ThrowAsync<TimeoutException>(
                () => DaprInvocationReadinessProbe.WaitAsync(
                    expectedReady: false,
                    probeAsync: static _ => Task.FromResult(CreateResponse(
                        HttpStatusCode.InternalServerError,
                        "{\"errorCode\":\"ERR_OTHER\"}")),
                    timeout: TimeSpan.FromMinutes(1),
                    retryDelay: TimeSpan.Zero,
                    cancellationToken: CancellationToken.None,
                    delayAsync: ThrowInternalCancellationAsync))
            .ConfigureAwait(true);

        exception.Message.ShouldContain(nameof(HttpStatusCode.InternalServerError));
        exception.Message.ShouldContain("ERR_OTHER");
    }

    /// <summary>
    /// Verifies an internal operation cancellation is normalized to the bounded timeout diagnostic.
    /// </summary>
    [Fact]
    public async Task WaitAsync_InternalOperationCancellation_ThrowsTimeoutAsync() {
        var probeCancellation = new OperationCanceledException("probe timed out");

        TimeoutException exception = await Should.ThrowAsync<TimeoutException>(
                () => DaprInvocationReadinessProbe.WaitAsync(
                    expectedReady: true,
                    probeAsync: _ => Task.FromException<HttpResponseMessage>(probeCancellation),
                    timeout: TimeSpan.FromMinutes(1),
                    retryDelay: TimeSpan.Zero,
                    cancellationToken: CancellationToken.None,
                    delayAsync: ThrowInternalCancellationAsync))
            .ConfigureAwait(true);

        exception.InnerException.ShouldBeSameAs(probeCancellation);
        exception.Message.ShouldContain("no response");
    }

    /// <summary>
    /// Verifies caller cancellation remains caller cancellation rather than being rewritten as a timeout.
    /// </summary>
    [Fact]
    public async Task WaitAsync_ParentCancellation_PropagatesAsync() {
        using var cancellationSource = new CancellationTokenSource();
        cancellationSource.Cancel();
        int attemptCount = 0;

        _ = await Should.ThrowAsync<OperationCanceledException>(
                () => DaprInvocationReadinessProbe.WaitAsync(
                    expectedReady: true,
                    probeAsync: _ => {
                        attemptCount++;
                        return Task.FromResult(CreateResponse(HttpStatusCode.OK, string.Empty));
                    },
                    timeout: TimeSpan.FromMinutes(1),
                    retryDelay: TimeSpan.Zero,
                    cancellationSource.Token))
            .ConfigureAwait(true);

        attemptCount.ShouldBe(0);
    }

    private static HttpResponseMessage CreateResponse(HttpStatusCode statusCode, string body)
        => new(statusCode) {
            Content = new StringContent(body, Encoding.UTF8, "application/json"),
        };

    private static Task ThrowInternalCancellationAsync(TimeSpan _, CancellationToken cancellationToken)
        => Task.FromException(new OperationCanceledException("readiness timeout", cancellationToken));
}
