using System.Text.Json;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using OpenTelemetry;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;

namespace Hexalith.EventStore.ServiceDefaults;

// Adds common Aspire services: service discovery, resilience, health checks, and OpenTelemetry.
// This project should be referenced by each service project in your solution.
// To learn more about using this project, see https://aka.ms/dotnet/aspire/service-defaults
public static class Extensions {
    private const string HealthEndpointPath = "/health";
    private const string AlivenessEndpointPath = "/alive";
    private const string ReadinessEndpointPath = "/ready";
    private const string LivenessTag = "live";
    private const string ReadinessTag = "ready";

    private static readonly string[] ActivitySourceNames = [
        "Hexalith.EventStore",
        "Microsoft.AspNetCore.SignalR.Server",
        "Microsoft.AspNetCore.SignalR.Client",
    ];

    public static TBuilder AddServiceDefaults<TBuilder>(this TBuilder builder) where TBuilder : IHostApplicationBuilder {
        ArgumentNullException.ThrowIfNull(builder);

        _ = builder.ConfigureOpenTelemetry();
        _ = builder.AddDefaultHealthChecks();
        _ = builder.Services.AddServiceDiscovery();
        _ = builder.Services.ConfigureHttpClientDefaults(static http => {
            _ = http.AddStandardResilienceHandler();
            _ = http.AddServiceDiscovery();
        });

        return builder;
    }

    public static TBuilder ConfigureOpenTelemetry<TBuilder>(this TBuilder builder) where TBuilder : IHostApplicationBuilder {
        ArgumentNullException.ThrowIfNull(builder);

        _ = builder.Logging.AddOpenTelemetry(static logging => {
            logging.IncludeFormattedMessage = true;
            logging.IncludeScopes = true;
        });
        _ = builder.Logging.AddJsonConsole(static console => console.UseUtcTimestamp = true);

        IOpenTelemetryBuilder telemetry = builder.Services.AddOpenTelemetry();
        _ = telemetry
            .WithMetrics(static metrics => {
                _ = metrics
                    .AddAspNetCoreInstrumentation()
                    .AddHttpClientInstrumentation()
                    .AddRuntimeInstrumentation();

                foreach (string sourceName in ActivitySourceNames) {
                    _ = metrics.AddMeter(sourceName);
                }
            })
            .WithTracing(static tracing => {
                foreach (string sourceName in ActivitySourceNames) {
                    _ = tracing.AddSource(sourceName);
                }

                _ = tracing
                    .AddAspNetCoreInstrumentation(static aspNetCore => aspNetCore.Filter = ShouldTraceHttpRequest)
                    .AddHttpClientInstrumentation();
            });

        if (IsOtlpConfigured(builder.Configuration)) {
            _ = telemetry.UseOtlpExporter();
        }

        return builder;
    }

    public static TBuilder AddDefaultHealthChecks<TBuilder>(this TBuilder builder) where TBuilder : IHostApplicationBuilder {
        ArgumentNullException.ThrowIfNull(builder);

        _ = builder.Services
            .AddHealthChecks()
            .AddCheck("self", static () => HealthCheckResult.Healthy(), [LivenessTag]);

        return builder;
    }

    /// <summary>
    /// Writes a detailed JSON health check response for development environments.
    /// </summary>
    public static async Task WriteHealthCheckJsonResponse(HttpContext httpContext, HealthReport healthReport) {
        ArgumentNullException.ThrowIfNull(httpContext);
        ArgumentNullException.ThrowIfNull(healthReport);

        httpContext.Response.ContentType = "application/json; charset=utf-8";

        using MemoryStream stream = new();
        using (Utf8JsonWriter writer = new(stream, new JsonWriterOptions { Indented = true })) {
            writer.WriteStartObject();
            writer.WriteString("status", healthReport.Status.ToString());
            writer.WriteStartObject("results");

            foreach (KeyValuePair<string, HealthReportEntry> entry in healthReport.Entries) {
                writer.WriteStartObject(entry.Key);
                writer.WriteString("status", entry.Value.Status.ToString());
                writer.WriteString("description", entry.Value.Description);
                writer.WriteString("duration", entry.Value.Duration.ToString());
                writer.WriteStartObject("data");
                foreach (KeyValuePair<string, object> dataEntry in entry.Value.Data) {
                    writer.WritePropertyName(dataEntry.Key);
                    try {
                        byte[] json = JsonSerializer.SerializeToUtf8Bytes(
                            dataEntry.Value,
                            dataEntry.Value?.GetType() ?? typeof(object));
                        writer.WriteRawValue(json);
                    }
                    catch (Exception ex) when (ex is NotSupportedException or JsonException or InvalidOperationException) {
                        writer.WriteStringValue($"[non-serializable: {dataEntry.Value?.GetType().Name ?? "null"}]");
                    }
                }

                writer.WriteEndObject();
                writer.WriteEndObject();
            }

            writer.WriteEndObject();
            writer.WriteEndObject();
        }

        stream.Position = 0;
        await stream.CopyToAsync(httpContext.Response.Body, httpContext.RequestAborted).ConfigureAwait(false);
    }

    public static bool ShouldTraceHttpRequest(HttpContext httpContext) {
        ArgumentNullException.ThrowIfNull(httpContext);

        return !httpContext.Request.Path.StartsWithSegments(HealthEndpointPath)
            && !httpContext.Request.Path.StartsWithSegments(AlivenessEndpointPath)
            && !httpContext.Request.Path.StartsWithSegments(ReadinessEndpointPath);
    }

    public static WebApplication MapDefaultEndpoints(this WebApplication app) {
        ArgumentNullException.ThrowIfNull(app);

        EventStoreServiceDefaultsOptions options = app.Services.GetService<IOptions<EventStoreServiceDefaultsOptions>>()?.Value
            ?? new EventStoreServiceDefaultsOptions();
        IDictionary<HealthStatus, int> statusCodes = new Dictionary<HealthStatus, int> {
            [HealthStatus.Healthy] = StatusCodes.Status200OK,
            [HealthStatus.Degraded] = StatusCodes.Status200OK,
            [HealthStatus.Unhealthy] = StatusCodes.Status503ServiceUnavailable,
        };

        HealthCheckOptions healthOptions = new() {
            ResultStatusCodes = statusCodes,
        };
        HealthCheckOptions livenessOptions = new() {
            Predicate = registration => registration.Tags.Contains(LivenessTag),
            ResultStatusCodes = statusCodes,
        };
        HealthCheckOptions readinessOptions = new() {
            Predicate = registration => registration.Tags.Contains(ReadinessTag),
            ResultStatusCodes = statusCodes,
        };

        if (app.Environment.IsDevelopment() && options.DevelopmentHealthResponseWriter is not null) {
            healthOptions.ResponseWriter = options.DevelopmentHealthResponseWriter;
            readinessOptions.ResponseWriter = options.DevelopmentHealthResponseWriter;
        }

        _ = app.MapHealthChecks(HealthEndpointPath, healthOptions);
        _ = app.MapHealthChecks(AlivenessEndpointPath, livenessOptions);
        _ = app.MapHealthChecks(ReadinessEndpointPath, readinessOptions);
        return app;
    }

    private static bool IsOtlpConfigured(IConfiguration configuration)
        => !string.IsNullOrWhiteSpace(configuration["OTEL_EXPORTER_OTLP_ENDPOINT"]);
}
