using System.Diagnostics;
using System.Net;

using Hexalith.EventStore.ServiceDefaults;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Time.Testing;

using Shouldly;

namespace Hexalith.EventStore.Server.Tests.Telemetry;

/// <summary>Verifies the approved bounded denial floor with monotonic fake time and actual response headers.</summary>
[Collection("ReadDiagnosticsPrivacy")]
public sealed class ReadQueryDenialTimingTests
{
    /// <summary>Work already performed counts toward the floor through the clock timestamp API.</summary>
    [Fact]
    public async Task OnlyRemainingAdmissionDurationIsWaited()
    {
        FakeTimeProvider clock = new();
        long admittedAt = clock.GetTimestamp();
        DefaultHttpContext context = new();
        context.Response.StatusCode = 404;
        clock.Advance(TimeSpan.FromMilliseconds(40));
        Task gate = ReadQueryDenialTiming.WaitAsync(context, clock, admittedAt);
        gate.IsCompleted.ShouldBeFalse();
        clock.Advance(TimeSpan.FromMilliseconds(9));
        gate.IsCompleted.ShouldBeFalse();
        clock.Advance(TimeSpan.FromMilliseconds(1));
        await gate.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken).ConfigureAwait(true);
        clock.GetElapsedTime(admittedAt).ShouldBe(TimeSpan.FromMilliseconds(50));
    }

    /// <summary>A reply already beyond the floor has no added wait.</summary>
    [Fact]
    public void AlreadyElapsedFloorDoesNotAddAnotherDelay()
    {
        FakeTimeProvider clock = new();
        long admittedAt = clock.GetTimestamp();
        DefaultHttpContext context = new();
        context.Response.StatusCode = 404;
        clock.Advance(TimeSpan.FromMilliseconds(75));
        ReadQueryDenialTiming.WaitAsync(context, clock, admittedAt).IsCompletedSuccessfully.ShouldBeTrue();
        clock.GetElapsedTime(admittedAt).ShouldBe(TimeSpan.FromMilliseconds(75));
    }

    /// <summary>Successful, structurally invalid and other non-404 outcomes retain immediate header behavior.</summary>
    /// <param name="status">A response outside the safe-denial timing policy.</param>
    [Theory]
    [InlineData(200)]
    [InlineData(204)]
    [InlineData(400)]
    [InlineData(401)]
    [InlineData(403)]
    [InlineData(499)]
    [InlineData(500)]
    [InlineData(503)]
    public void NonNotFoundResponsesDoNotScheduleADelay(int status)
    {
        FakeTimeProvider clock = new();
        DefaultHttpContext context = new();
        context.Response.StatusCode = status;
        ReadQueryDenialTiming.WaitAsync(context, clock, clock.GetTimestamp()).IsCompletedSuccessfully.ShouldBeTrue();
    }

    /// <summary>Cancellation aborts the remaining timer and preserves the exact request token.</summary>
    [Fact]
    public async Task CancellationInterruptsTheGateWithOriginalRequestToken()
    {
        FakeTimeProvider clock = new();
        using CancellationTokenSource cancellation = new();
        DefaultHttpContext context = new() { RequestAborted = cancellation.Token };
        context.Response.StatusCode = 404;
        Task gate = ReadQueryDenialTiming.WaitAsync(context, clock, clock.GetTimestamp());
        await cancellation.CancelAsync().ConfigureAwait(true);
        OperationCanceledException failure = await Should.ThrowAsync<OperationCanceledException>(() => gate).ConfigureAwait(true);
        failure.CancellationToken.ShouldBe(cancellation.Token);
    }

    /// <summary>Cancellation retains precedence even when processing has already used the floor.</summary>
    [Fact]
    public async Task AlreadyCanceledRequestNeverWaitsOrCommitsASafeDenial()
    {
        FakeTimeProvider clock = new();
        using CancellationTokenSource cancellation = new();
        await cancellation.CancelAsync().ConfigureAwait(true);
        DefaultHttpContext context = new() { RequestAborted = cancellation.Token };
        context.Response.StatusCode = 404;
        long admittedAt = clock.GetTimestamp();
        clock.Advance(TimeSpan.FromMilliseconds(75));
        OperationCanceledException failure = await Should.ThrowAsync<OperationCanceledException>(
            () => ReadQueryDenialTiming.WaitAsync(context, clock, admittedAt)).ConfigureAwait(true);
        failure.CancellationToken.ShouldBe(cancellation.Token);
        context.Response.HasStarted.ShouldBeFalse();
    }

    /// <summary>Real Kestrel headers and first body bytes wait even when the endpoint explicitly starts its response.</summary>
    /// <param name="path">An ASP.NET routing-equivalent public query alias.</param>
    /// <param name="earlyRefusal">Whether admission returns without invoking the endpoint.</param>
    [Theory]
    [InlineData("/api/v1/queries", false)]
    [InlineData("/API/V1/QUERIES/", false)]
    [InlineData("/api/v1/queries", true)]
    [InlineData("/API/V1/QUERIES/", true)]
    public async Task RealResponseHeadersAndFirstByteRespectAdmissionFloor(string path, bool earlyRefusal)
    {
        WebApplicationBuilder builder = WebApplication.CreateBuilder();
        builder.Configuration["urls"] = "http://127.0.0.1:0";
        builder.Configuration["OTEL_EXPORTER_OTLP_ENDPOINT"] = null;
        builder.Logging.ClearProviders();
        builder.ConfigureOpenTelemetry();
        WebApplication app = builder.Build();
        await using var lifetime = app.ConfigureAwait(true);
        app.Use(async (context, next) =>
        {
            if (context.Request.Headers.ContainsKey("Early-Refusal"))
            {
                context.Response.StatusCode = 404;
                return;
            }
            await next(context).ConfigureAwait(true);
        });
        app.MapGet("/ordinary", () => Results.Ok());
        app.MapPost("/api/v1/queries", async (HttpContext context) =>
        {
            context.Response.StatusCode = 404;
            context.Response.Headers["Denial-Control"] = "safe";
            await context.Response.StartAsync(context.RequestAborted).ConfigureAwait(true);
            await context.Response.WriteAsync("safe", context.RequestAborted).ConfigureAwait(true);
            await context.Response.Body.FlushAsync(context.RequestAborted).ConfigureAwait(true);
        });
        await app.StartAsync(TestContext.Current.CancellationToken).ConfigureAwait(true);
        using HttpClient client = new() { BaseAddress = new(app.Urls.Single()) };
        using HttpResponseMessage warmup = await client.GetAsync("/ordinary", TestContext.Current.CancellationToken).ConfigureAwait(true);
        warmup.EnsureSuccessStatusCode();
        using HttpRequestMessage request = new(HttpMethod.Post, path);
        if (earlyRefusal) request.Headers.Add("Early-Refusal", "true");
        long started = Stopwatch.GetTimestamp();
        using HttpResponseMessage response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead,
            TestContext.Current.CancellationToken).ConfigureAwait(true);
        Stopwatch.GetElapsedTime(started).ShouldBeGreaterThanOrEqualTo(TimeSpan.FromMilliseconds(50));
        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        string body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken).ConfigureAwait(true);
        body.ShouldBe(earlyRefusal ? string.Empty : "safe");
        Stopwatch.GetElapsedTime(started).ShouldBeGreaterThanOrEqualTo(TimeSpan.FromMilliseconds(50));
    }
}
