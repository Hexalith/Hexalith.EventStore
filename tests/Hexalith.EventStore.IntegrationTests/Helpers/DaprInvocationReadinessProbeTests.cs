using System.Net;
using System.Net.Sockets;
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

    /// <summary>
    /// Verifies the shared fixture readiness wait polls the exact sample invocation boundary
    /// through EventStore's Dapr sidecar until it stops reporting direct-invocation failure.
    /// </summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Fact]
    public async Task WaitForSampleInvocationAsync_UnavailableThenReady_PollsExactInvokeBoundary() {
        List<string> requestedPaths = [];
        var unavailableResponsesRemaining = 2;

        using var stub = new HttpListener();
        int port = GetFreeTcpPort();
        stub.Prefixes.Add($"http://127.0.0.1:{port}/");
        stub.Start();

        using var cancellationSource = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        // The loop owns its shutdown: stopping the listener faults the pending GetContextAsync,
        // which is the expected way this stub ends rather than a test failure.
        Task stubLoop = Task.Run(
            async () => {
                try {
                    while (true) {
                        HttpListenerContext context = await stub.GetContextAsync().ConfigureAwait(false);
                        requestedPaths.Add(context.Request.Url!.AbsolutePath);
                        byte[] payload;
                        if (unavailableResponsesRemaining > 0) {
                            unavailableResponsesRemaining--;
                            context.Response.StatusCode = (int)HttpStatusCode.InternalServerError;
                            payload = Encoding.UTF8.GetBytes("{\"errorCode\":\"ERR_DIRECT_INVOKE\"}");
                        }
                        else {
                            context.Response.StatusCode = (int)HttpStatusCode.OK;
                            payload = Encoding.UTF8.GetBytes("{}");
                        }

                        context.Response.ContentType = "application/json";
                        await context.Response.OutputStream.WriteAsync(payload).ConfigureAwait(false);
                        context.Response.Close();
                    }
                }
                catch (ObjectDisposedException) {
                }
                catch (HttpListenerException) {
                }
            },
            CancellationToken.None);

        await DaprInvocationReadinessProbe.WaitForSampleInvocationAsync(
                new Uri($"http://127.0.0.1:{port}"),
                expectedReady: true,
                timeout: TimeSpan.FromSeconds(20),
                cancellationSource.Token)
            .ConfigureAwait(true);

        stub.Stop();
        await stubLoop.ConfigureAwait(true);

        requestedPaths.Count.ShouldBe(3);
        requestedPaths.ShouldAllBe(path
            => path == "/v1.0/invoke/sample/method/admin/operational-index-metadata");
    }

    private static int GetFreeTcpPort() {
        using var probe = new TcpListener(IPAddress.Loopback, 0);
        probe.Start();
        int port = ((IPEndPoint)probe.LocalEndpoint).Port;
        probe.Stop();
        return port;
    }

    private static HttpResponseMessage CreateResponse(HttpStatusCode statusCode, string body)
        => new(statusCode) {
            Content = new StringContent(body, Encoding.UTF8, "application/json"),
        };

    private static Task ThrowInternalCancellationAsync(TimeSpan _, CancellationToken cancellationToken)
        => Task.FromException(new OperationCanceledException("readiness timeout", cancellationToken));
}
