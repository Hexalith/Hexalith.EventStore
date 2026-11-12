using System.Diagnostics;
using System.Globalization;
using System.Net;
using System.Net.Http.Json;

using Hexalith.EventStore.Middleware;
using Hexalith.EventStore.ServiceDefaults;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using OpenTelemetry;
using OpenTelemetry.Logs;
using OpenTelemetry.Trace;

using Shouldly;

namespace Hexalith.EventStore.Server.Tests.Telemetry;

/// <summary>Exercises the real shared-host privacy registration and actual provider/exporter output.</summary>
[Collection("ReadDiagnosticsPrivacy")]
public sealed class ReadDiagnosticsPrivacyTests
{
    /// <summary>Hosting-level unhandled-exception output remains private after the execution scope unwinds on a rewritten path.</summary>
    [Fact]
    public async Task RealHostingFailureAfterPathRewriteOmitsProtectedException()
    {
        string secret = Guid.NewGuid().ToString("N");
        using StringWriter console = new(CultureInfo.InvariantCulture);
        TextWriter previous = Console.Out;
        using ReadPrivacyLogExporter logs = new();
        try
        {
            Console.SetOut(console);
            WebApplicationBuilder builder = WebApplication.CreateBuilder();
            builder.Configuration["urls"] = "http://127.0.0.1:0";
            builder.Configuration["OTEL_EXPORTER_OTLP_ENDPOINT"] = null;
            builder.Logging.ClearProviders();
            builder.ConfigureOpenTelemetry();
            builder.Logging.AddOpenTelemetry(options => options.AddProcessor(new SimpleLogRecordExportProcessor(logs)));
            WebApplication app = builder.Build();
            await using var lifetime = app.ConfigureAwait(true);
            app.UseMiddleware<CorrelationIdMiddleware>();
            app.MapPost("/api/v1/queries", (HttpContext context) =>
            {
                context.Request.Path = "/rewritten-unhandled";
                throw new InvalidOperationException(secret);
            });
            app.MapGet("/ordinary", (ILoggerFactory factory) =>
            {
                factory.CreateLogger("Privacy.Probe").LogInformation("ordinary-after-hosting-failure");
                return Results.Ok();
            });
            await app.StartAsync(TestContext.Current.CancellationToken).ConfigureAwait(true);
            using HttpClient client = new() { BaseAddress = new(app.Urls.Single()) };
            Task boundary = logs.ExpectBoundary("unhandled-control");
            using HttpRequestMessage request = new(HttpMethod.Post, "/api/v1/queries");
            request.Headers.Add(CorrelationIdMiddleware.HeaderName, "unhandled-control");
            using HttpResponseMessage response = await client.SendAsync(request, TestContext.Current.CancellationToken).ConfigureAwait(true);
            response.StatusCode.ShouldBe(HttpStatusCode.InternalServerError);
            await boundary.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken).ConfigureAwait(true);
            using HttpResponseMessage ordinary = await client.GetAsync("/ordinary", TestContext.Current.CancellationToken).ConfigureAwait(true);
            ordinary.EnsureSuccessStatusCode();
            await app.StopAsync(TestContext.Current.CancellationToken).ConfigureAwait(true);
            string.Join('\n', logs.Records).ShouldNotContain(secret);
            logs.Records.ShouldContain(record => record.StartsWith("Hexalith.EventStore.QueryBoundary|", StringComparison.Ordinal)
                && record.Contains("StatusCode=500", StringComparison.Ordinal));
            logs.Records.ShouldContain(record => record.Contains("ordinary-after-hosting-failure", StringComparison.Ordinal));
        }
        finally
        {
            Console.SetOut(previous);
        }
        console.ToString().ShouldNotContain(secret);
        console.ToString().ShouldContain("ordinary-after-hosting-failure");
    }

    /// <summary>Cancellation retains its exact token, omits rewritten-path details and restores the calling execution.</summary>
    [Fact]
    public async Task CanceledPipelineRestoresPrivacyAndInstrumentationScopes()
    {
        using ReadPrivacyLogExporter logs = new();
        WebApplicationBuilder builder = WebApplication.CreateBuilder();
        builder.Configuration["OTEL_EXPORTER_OTLP_ENDPOINT"] = null;
        builder.Logging.ClearProviders();
        builder.ConfigureOpenTelemetry();
        builder.Logging.AddOpenTelemetry(options => options.AddProcessor(new SimpleLogRecordExportProcessor(logs)));
        WebApplication app = builder.Build();
        await using var lifetime = app.ConfigureAwait(true);
        using CancellationTokenSource cancellation = new();
        await cancellation.CancelAsync().ConfigureAwait(true);
        ApplicationBuilder pipeline = new(app.Services);
        IStartupFilter filter = app.Services.GetServices<IStartupFilter>()
            .Single(value => value.GetType().Name == "ReadDiagnosticsPrivacyStartupFilter");
        ILogger logger = app.Services.GetRequiredService<ILoggerFactory>().CreateLogger("Privacy.Probe");
        filter.Configure(application => application.Run(context =>
        {
            context.Request.Path = "/rewritten-cancellation";
            logger.LogCritical("protected-canceled-cause");
            return Task.FromCanceled(cancellation.Token);
        }))(pipeline);
        DefaultHttpContext context = new() { RequestServices = app.Services };
        context.Request.Method = "POST";
        context.Request.Path = "/api/v1/queries";
        IHttpContextAccessor accessor = app.Services.GetRequiredService<IHttpContextAccessor>();
        accessor.HttpContext = context;
        try
        {
            OperationCanceledException canceled = await Should.ThrowAsync<OperationCanceledException>(() => pipeline.Build()(context)).ConfigureAwait(true);
            canceled.CancellationToken.ShouldBe(cancellation.Token);
            ReadDiagnosticsPrivacyScope.IsActive.ShouldBeFalse();
            Sdk.SuppressInstrumentation.ShouldBeFalse();
        }
        finally
        {
            accessor.HttpContext = null;
        }
        logger.LogInformation("ordinary-after-cancellation");
        string.Join('\n', logs.Records).ShouldNotContain("protected-canceled-cause");
        logs.Records.ShouldContain(record => record.Contains("ordinary-after-cancellation", StringComparison.Ordinal));
    }

    /// <summary>Routing-equivalent case and trailing-slash aliases retain privacy; unrelated methods/routes retain diagnostics.</summary>
    [Theory]
    [InlineData("POST", "/api/v1/queries", true)]
    [InlineData("POST", "/API/V1/QUERIES/", true)]
    [InlineData("POST", "/query/", true)]
    [InlineData("POST", "/api/v1/queries/owner-authority/workload/", true)]
    [InlineData("POST", "/api/v1/streams/read/workload", true)]
    [InlineData("PUT", "/actors/AggregateActor/opaque/method/InspectSourceMetadataPairAsync", true)]
    [InlineData("POST", "/ACTORS/AggregateActor/opaque/METHOD/GetStreamMetadataAsync/", true)]
    [InlineData("PUT", "/actors/AggregateActor/opaque/method/InspectSourceProofAsync", true)]
    [InlineData("GET", "/api/v1/queries", false)]
    [InlineData("POST", "/api/v1/commands", false)]
    [InlineData("PUT", "/actors/AggregateActor/opaque/method/SubmitCommandAsync", false)]
    [InlineData("POST", "/api/v1/queries/owner-authority/workload-extra", false)]
    public void FiniteReadSurfaceMatchesActualRoutingAliases(string method, string path, bool expected)
    {
        DefaultHttpContext context = new();
        context.Request.Method = method;
        context.Request.Path = path;
        ReadDiagnosticsPrivacy.IsProtectedRequest(context).ShouldBe(expected);
    }

    /// <summary>Distinct owner branches, early admission refusal, and exceptions emit only a common boundary record.</summary>
    /// <param name="publicPath">A routing-equivalent public query path.</param>
    /// <param name="incomingTraceFlags">Explicit incoming sampling, random, and unrecognized trace flags, or the real client-generated context.</param>
    [Theory]
    [InlineData("/api/v1/queries", null)]
    [InlineData("/API/V1/QUERIES/", null)]
    [InlineData("/api/v1/queries", ActivityTraceFlags.None)]
    [InlineData("/api/v1/queries", ActivityTraceFlags.Recorded)]
    [InlineData("/api/v1/queries", ActivityTraceFlags.RandomTraceId)]
    [InlineData("/api/v1/queries", ActivityTraceFlags.Recorded | ActivityTraceFlags.RandomTraceId)]
    [InlineData("/api/v1/queries", (ActivityTraceFlags)0x04)]
    [InlineData("/api/v1/queries", (ActivityTraceFlags)0x05)]
    public async Task RealProvidersOmitWholeReadSubtreesAndKeepUniformCompletion(string publicPath, ActivityTraceFlags? incomingTraceFlags)
    {
        string secret = Guid.NewGuid().ToString("N");
        using StringWriter console = new(CultureInfo.InvariantCulture);
        TextWriter previous = Console.Out;
        using ReadPrivacyLogExporter logs = new();
        using ReadPrivacyTraceExporter traces = new();
        using ActivitySource probe = new("Privacy.Probe");
        List<string> propagated = [];
        int nestedCalls = 0;
        TaskCompletionSource drainRelease = new(TaskCreationOptions.RunContinuationsAsynchronously);
        Task? drain = null;
        try
        {
            Console.SetOut(console);
            WebApplicationBuilder ownerBuilder = WebApplication.CreateBuilder();
            ownerBuilder.Configuration["urls"] = "http://127.0.0.1:0";
            ownerBuilder.Configuration["OTEL_EXPORTER_OTLP_ENDPOINT"] = null;
            ownerBuilder.Logging.ClearProviders();
            ownerBuilder.ConfigureOpenTelemetry();
            ownerBuilder.Logging.AddOpenTelemetry(options => options.AddProcessor(new SimpleLogRecordExportProcessor(logs)));
            WebApplication owner = ownerBuilder.Build();
            await using var ownerLifetime = owner.ConfigureAwait(true);
            owner.MapPost("/query", (HttpContext context, ILoggerFactory factory) =>
            {
                nestedCalls++;
                propagated.Add(context.Request.Headers["traceparent"].ToString());
                factory.CreateLogger("Privacy.Probe").LogError(new InvalidOperationException(secret), "Owner cause {Resource}", secret);
                using Activity? detail = probe.StartActivity("owner-branch-" + secret);
                return Results.NotFound();
            });
            await owner.StartAsync(TestContext.Current.CancellationToken).ConfigureAwait(true);
            using HttpClient ownerClient = new() { BaseAddress = new(owner.Urls.Single()) };

            WebApplicationBuilder builder = WebApplication.CreateBuilder();
            builder.Configuration["urls"] = "http://127.0.0.1:0";
            builder.Configuration["OTEL_EXPORTER_OTLP_ENDPOINT"] = null;
            builder.Logging.ClearProviders();
            builder.ConfigureOpenTelemetry();
            builder.Services.AddEventStoreReadDiagnosticsPrivacy();
            builder.Services.AddEventStoreReadDiagnosticsPrivacy();
            builder.Services.Count(descriptor => descriptor.ImplementationType?.Name == "ReadDiagnosticsPrivacyStartupFilter").ShouldBe(1);
            builder.Services.PostConfigure<LoggerFilterOptions>(options =>
            {
                foreach (string provider in new[] { "Console", "OpenTelemetry" })
                {
                    options.Rules.Add(new(provider, "Privacy.Probe", LogLevel.Trace, static (_, _, _) => true));
                    options.Rules.Add(new(provider, "Microsoft.AspNetCore.Server.Kestrel.Connections", LogLevel.Trace, static (_, _, _) => true));
                    options.Rules.Add(new(provider, "Microsoft.AspNetCore.Server.Kestrel.Transport", LogLevel.Trace, static (_, _, _) => true));
                }
            });
            builder.Logging.AddOpenTelemetry(options => options.AddProcessor(new SimpleLogRecordExportProcessor(logs)));
            builder.Services.AddOpenTelemetry().WithTracing(tracing => tracing.AddSource("Privacy.Probe")
                .SetSampler(new AlwaysOnSampler()).AddProcessor(new SimpleActivityExportProcessor(traces)));
            WebApplication app = builder.Build();
            await using var appLifetime = app.ConfigureAwait(true);
            app.UseMiddleware<CorrelationIdMiddleware>();
            app.UseExceptionHandler(new ExceptionHandlerOptions
            {
                ExceptionHandlingPath = "/error-reexecuted",
                AllowStatusCode404Response = true,
            });
            app.MapPost("/error-reexecuted", (HttpContext context, ILoggerFactory factory) =>
            {
                context.Request.Path.Value.ShouldBe("/error-reexecuted");
                factory.CreateLogger("Privacy.Probe").LogError("Re-executed cause {Identity}", secret);
                using Activity? rewritten = probe.StartActivity("rewritten-branch-" + secret);
                return Results.NotFound();
            });
            app.Use(async (context, next) =>
            {
                if (context.Request.Headers.ContainsKey("Early-Refusal"))
                {
                    context.RequestServices.GetRequiredService<ILoggerFactory>().CreateLogger("Privacy.Probe")
                        .LogCritical("Admission cause {Identity}", secret);
                    context.Response.StatusCode = StatusCodes.Status404NotFound;
                    return;
                }
                await next(context).ConfigureAwait(true);
            });
            app.MapPost("/api/v1/queries", async (HttpContext context, ILoggerFactory factory) =>
            {
                int branch = (await context.Request.ReadFromJsonAsync<int>(TestContext.Current.CancellationToken).ConfigureAwait(true));
                ILogger logger = factory.CreateLogger("Privacy.Probe");
                using IDisposable? scope = logger.BeginScope("Private scope {Resource}", secret);
                for (LogLevel level = LogLevel.Trace; level <= LogLevel.Critical; level++)
                    logger.Log(level, "Protected detail {Resource}", secret);
                using Activity? detail = probe.StartActivity("root-branch-" + secret);
                if (branch == 2)
                {
                    IHttpContextAccessor accessor = context.RequestServices.GetRequiredService<IHttpContextAccessor>();
                    drain = Task.Run(async () =>
                    {
                        await drainRelease.Task.ConfigureAwait(true);
                        accessor.HttpContext.ShouldBeNull();
                        logger.LogCritical("Async drained cause {Identity}", secret);
                        using Activity? late = probe.StartActivity("drained-branch-" + secret);
                    });
                }
                for (int call = 0; call < branch; call++)
                {
                    using HttpResponseMessage response = await ownerClient.PostAsJsonAsync("/query", 0, TestContext.Current.CancellationToken).ConfigureAwait(true);
                    response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
                }
                if (branch == 3) throw new InvalidOperationException(secret);
                return Results.NotFound();
            });
            app.MapGet("/ordinary", (ILoggerFactory factory) =>
            {
                factory.CreateLogger("Privacy.Probe").LogInformation("ordinary-log-visible");
                factory.CreateLogger("Microsoft.AspNetCore.Server.Kestrel.Connections").LogWarning("ordinary-transport-warning-visible");
                using Activity? activity = probe.StartActivity("ordinary-trace-visible");
                return Results.Ok();
            });
            await app.StartAsync(TestContext.Current.CancellationToken).ConfigureAwait(true);
            using HttpClient client = new() { BaseAddress = new(app.Urls.Single()) };
            logs.Records.Clear(); traces.Records.Clear();
            for (int branch = 0; branch <= 4; branch++)
            {
                string correlation = "privacy-" + branch.ToString(CultureInfo.InvariantCulture);
                Task completion = logs.ExpectBoundary(correlation);
                using HttpRequestMessage request = new(HttpMethod.Post, publicPath) { Content = JsonContent.Create(branch) };
                if (incomingTraceFlags is { } incomingFlags)
                {
                    request.Headers.Add("traceparent", "00-4bf92f3577b34da6a3ce929d0e0e4736-00f067aa0ba902b7-"
                        + ((byte)incomingFlags).ToString("x2", CultureInfo.InvariantCulture));
                }
                request.Headers.Add(CorrelationIdMiddleware.HeaderName, correlation);
                if (branch == 4) request.Headers.Add("Early-Refusal", "true");
                using HttpResponseMessage response = await client.SendAsync(request, TestContext.Current.CancellationToken).ConfigureAwait(true);
                response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
                await completion.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken).ConfigureAwait(true);
            }
            nestedCalls.ShouldBe(6);
            propagated.Count.ShouldBe(6);
            foreach (string value in propagated)
            {
                value.Length.ShouldBe(55);
                ActivityContext.TryParse(value, null, out ActivityContext context).ShouldBeTrue();
                // Sampling suppression depends on Recorded; other W3C flags do not enable collection.
                (context.TraceFlags & ActivityTraceFlags.Recorded).ShouldBe(ActivityTraceFlags.None);
            }
            logs.Records.Count.ShouldBe(5);
            logs.Records.ShouldAllBe(record => record.StartsWith("Hexalith.EventStore.QueryBoundary|", StringComparison.Ordinal)
                && record.Contains("StatusCode=404", StringComparison.Ordinal)
                && record.EndsWith("|TraceId=00000000000000000000000000000000|SpanId=0000000000000000", StringComparison.Ordinal));
            // The five initiating external-client spans are outside the service boundary; no service subtree is exported.
            traces.Records.Count.ShouldBe(5);
            traces.Records.ShouldAllBe(record => record.Contains("System.Net.Http", StringComparison.Ordinal));
            string readLogs = string.Join('\n', logs.Records), readTraces = string.Join('\n', traces.Records);
            readLogs.ShouldNotContain(secret); readTraces.ShouldNotContain(secret);
            using HttpResponseMessage ordinary = await client.GetAsync("/ordinary", TestContext.Current.CancellationToken).ConfigureAwait(true);
            ordinary.EnsureSuccessStatusCode();
            logs.Records.ShouldContain(record => record.Contains("ordinary-log-visible", StringComparison.Ordinal));
            logs.Records.ShouldContain(record => record.Contains("ordinary-transport-warning-visible", StringComparison.Ordinal));
            traces.Records.ShouldContain(record => record.Contains("ordinary-trace-visible", StringComparison.Ordinal));
            await app.StopAsync(TestContext.Current.CancellationToken).ConfigureAwait(true);
            drainRelease.TrySetResult();
            await drain!.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken).ConfigureAwait(true);
            string.Join('\n', logs.Records).ShouldNotContain(secret);
            string.Join('\n', traces.Records).ShouldNotContain(secret);
            await owner.StopAsync(TestContext.Current.CancellationToken).ConfigureAwait(true);
        }
        finally
        {
            drainRelease.TrySetResult();
            if (drain is not null) await drain.WaitAsync(TimeSpan.FromSeconds(10)).ConfigureAwait(true);
            Console.SetOut(previous);
        }
        console.ToString().ShouldNotContain(secret);
        console.ToString().ShouldContain("Query boundary completed");
        console.ToString().ShouldContain("ordinary-log-visible");
    }
}
