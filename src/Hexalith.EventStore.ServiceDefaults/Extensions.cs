using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;

using OpenTelemetry;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;

namespace Microsoft.Extensions.Hosting;

// Adds common Aspire services: service discovery, resilience, health checks, and OpenTelemetry.
// This project should be referenced by each service project in your solution.
// To learn more about using this project, see https://aka.ms/dotnet/aspire/service-defaults
public static class Extensions {
    private const string HealthEndpointPath = "/health";
    private const string AlivenessEndpointPath = "/alive";
    private const string ReadinessEndpointPath = "/ready";

    public static TBuilder AddServiceDefaults<TBuilder>(this TBuilder builder) where TBuilder : IHostApplicationBuilder {
        builder.ConfigureOpenTelemetry();

        builder.AddDefaultHealthChecks();

        builder.Services.AddServiceDiscovery();

        builder.Services.ConfigureHttpClientDefaults(http => {
            // Turn on resilience by default
            http.AddStandardResilienceHandler();

            // Turn on service discovery by default
            http.AddServiceDiscovery();
        });

        // Uncomment the following to restrict the allowed schemes for service discovery.
        // builder.Services.Configure<ServiceDiscoveryOptions>(options =>
        // {
        //     options.AllowedSchemes = ["https"];
        // });

        return builder;
    }

    public static TBuilder ConfigureOpenTelemetry<TBuilder>(this TBuilder builder) where TBuilder : IHostApplicationBuilder {
        builder.Logging.AddOpenTelemetry(logging => {
            logging.IncludeFormattedMessage = true;
            logging.IncludeScopes = true;
        });

        // Structured JSON console logging (AC #5): ensures log output is machine-parseable
        // and structured fields from [LoggerMessage] methods appear as named JSON properties.
        builder.Logging.AddJsonConsole(options => {
            options.UseUtcTimestamp = true;
        });

        builder.Services.AddOpenTelemetry()
            .WithMetrics(metrics => {
                metrics.AddAspNetCoreInstrumentation()
                    .AddHttpClientInstrumentation()
                    .AddRuntimeInstrumentation();
            })
            .WithTracing(tracing => {
                tracing.AddSource(builder.Environment.ApplicationName)
                    .AddSource("Hexalith.EventStore.CommandApi")
                    .AddSource("Hexalith.EventStore")
                    .AddAspNetCoreInstrumentation(tracing =>
                        // Exclude health check requests from tracing
                        tracing.Filter = context =>
                            !context.Request.Path.StartsWithSegments(HealthEndpointPath)
                            && !context.Request.Path.StartsWithSegments(AlivenessEndpointPath)
                            && !context.Request.Path.StartsWithSegments(ReadinessEndpointPath)
                    )
                    // Uncomment the following line to enable gRPC instrumentation (requires the OpenTelemetry.Instrumentation.GrpcNetClient package)
                    //.AddGrpcClientInstrumentation()
                    .AddHttpClientInstrumentation();
            });

        builder.AddOpenTelemetryExporters();

        return builder;
    }

    private static TBuilder AddOpenTelemetryExporters<TBuilder>(this TBuilder builder) where TBuilder : IHostApplicationBuilder {
        var useOtlpExporter = !string.IsNullOrWhiteSpace(builder.Configuration["OTEL_EXPORTER_OTLP_ENDPOINT"]);

        if (useOtlpExporter) {
            builder.Services.AddOpenTelemetry().UseOtlpExporter();
        }

        // Uncomment the following lines to enable the Azure Monitor exporter (requires the Azure.Monitor.OpenTelemetry.AspNetCore package)
        //if (!string.IsNullOrEmpty(builder.Configuration["APPLICATIONINSIGHTS_CONNECTION_STRING"]))
        //{
        //    builder.Services.AddOpenTelemetry()
        //       .UseAzureMonitor();
        //}

        return builder;
    }

    public static TBuilder AddDefaultHealthChecks<TBuilder>(this TBuilder builder) where TBuilder : IHostApplicationBuilder {
        builder.Services.AddHealthChecks()
            // Add a default liveness check to ensure app is responsive
            .AddCheck("self", () => HealthCheckResult.Healthy(), ["live"]);

        return builder;
    }

    /// <summary>
    /// Writes a detailed JSON health check response for development environments.
    /// </summary>
    internal static Task WriteHealthCheckJsonResponse(HttpContext httpContext, HealthReport healthReport) {
        httpContext.Response.ContentType = "application/json; charset=utf-8";

        using var stream = new MemoryStream();
        using (var writer = new System.Text.Json.Utf8JsonWriter(stream, new System.Text.Json.JsonWriterOptions { Indented = true })) {
            writer.WriteStartObject();
            writer.WriteString("status", healthReport.Status.ToString());
            writer.WriteStartObject("results");

            foreach (var entry in healthReport.Entries) {
                writer.WriteStartObject(entry.Key);
                writer.WriteString("status", entry.Value.Status.ToString());
                writer.WriteString("description", entry.Value.Description);
                writer.WriteString("duration", entry.Value.Duration.ToString());
                writer.WriteEndObject();
            }

            writer.WriteEndObject();
            writer.WriteEndObject();
        }

        return httpContext.Response.WriteAsync(
            System.Text.Encoding.UTF8.GetString(stream.ToArray()));
    }

    public static WebApplication MapDefaultEndpoints(this WebApplication app) {
        ArgumentNullException.ThrowIfNull(app);

        // Health check status code mapping: Healthy=200, Degraded=200, Unhealthy=503
        var statusCodes = new Dictionary<HealthStatus, int> {
            [HealthStatus.Healthy] = StatusCodes.Status200OK,
            [HealthStatus.Degraded] = StatusCodes.Status200OK,
            [HealthStatus.Unhealthy] = StatusCodes.Status503ServiceUnavailable,
        };

        // All health checks must pass for app to be considered ready to accept traffic after starting
        var healthOptions = new HealthCheckOptions {
            ResultStatusCodes = statusCodes,
        };

        // Development: detailed JSON response; Production: default minimal plaintext
        if (app.Environment.IsDevelopment()) {
            healthOptions.ResponseWriter = WriteHealthCheckJsonResponse;
        }

        app.MapHealthChecks(HealthEndpointPath, healthOptions);

        // Only health checks tagged with the "live" tag must pass for app to be considered alive
        app.MapHealthChecks(AlivenessEndpointPath, new HealthCheckOptions {
            Predicate = r => r.Tags.Contains("live"),
            ResultStatusCodes = statusCodes,
        });

        // Only health checks tagged with the "ready" tag must pass for readiness (K8s readiness probe)
        var readinessOptions = new HealthCheckOptions {
            Predicate = r => r.Tags.Contains("ready"),
            ResultStatusCodes = statusCodes,
        };

        if (app.Environment.IsDevelopment()) {
            readinessOptions.ResponseWriter = WriteHealthCheckJsonResponse;
        }

        app.MapHealthChecks(ReadinessEndpointPath, readinessOptions);

        return app;
    }
}
