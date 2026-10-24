using System.Globalization;
using System.Security.Cryptography;

using Hexalith.EventStore.ServiceDefaults;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Console;
using Microsoft.Extensions.Options;

using OpenTelemetry;
using OpenTelemetry.Logs;

using Shouldly;

namespace Hexalith.EventStore.Server.Tests.Telemetry;

/// <summary>Exercises real request logs through the shared Console and OpenTelemetry providers.</summary>
public sealed class RequestLogPrivacyTests
{
    /// <summary>The no-rule fallback retains the caller's global minimum level.</summary>
    /// <param name="minimum">The caller's stricter global logging threshold.</param>
    [Theory]
    [InlineData(LogLevel.Error)]
    [InlineData(LogLevel.None)]
    public async Task SharedDefaultsPreserveGlobalMinimumWithoutRules(LogLevel minimum)
    {
        WebApplicationBuilder builder = WebApplication.CreateBuilder();
        builder.Configuration.Sources.Clear();
        builder.Logging.ClearProviders();
        builder.Logging.SetMinimumLevel(minimum);
        builder.ConfigureOpenTelemetry();
        await using WebApplication app = builder.Build();
        ILoggerFactory loggers = app.Services.GetRequiredService<ILoggerFactory>();
        ILogger hosting = loggers.CreateLogger("Microsoft.AspNetCore.Hosting.Diagnostics");
        hosting.IsEnabled(LogLevel.Warning).ShouldBeFalse();
        hosting.IsEnabled(LogLevel.Error).ShouldBe(minimum != LogLevel.None);
        loggers.CreateLogger("Privacy.Unrelated").IsEnabled(LogLevel.Warning).ShouldBeFalse();
    }

    /// <summary>Hardening cannot re-enable warnings disabled by a stricter existing rule.</summary>
    /// <param name="providerSpecific">Whether the caller configures each provider separately.</param>
    /// <param name="minimum">The caller's stricter logging threshold.</param>
    [Theory]
    [InlineData(false, LogLevel.Error)]
    [InlineData(false, LogLevel.None)]
    [InlineData(true, LogLevel.Error)]
    [InlineData(true, LogLevel.None)]
    public async Task SharedDefaultsPreserveMoreRestrictiveHostingFilters(bool providerSpecific, LogLevel minimum)
    {
        WebApplicationBuilder builder = WebApplication.CreateBuilder();
        builder.Configuration.Sources.Clear();
        Dictionary<string, string?> values = new() { ["Logging:LogLevel:Default"] = "Information" };
        if (providerSpecific)
        {
            values["Logging:Console:LogLevel:Microsoft.AspNetCore.Hosting.Diagnostics"] = minimum.ToString();
            values["Logging:OpenTelemetry:LogLevel:Microsoft.AspNetCore.Hosting.Diagnostics"] = minimum.ToString();
        }
        else
        {
            values["Logging:LogLevel:Microsoft.AspNetCore.Hosting.Diagnostics"] = minimum.ToString();
        }
        builder.Configuration.AddInMemoryCollection(values);
        builder.Logging.ClearProviders();
        builder.ConfigureOpenTelemetry();
        await using WebApplication app = builder.Build();
        ILoggerFactory loggers = app.Services.GetRequiredService<ILoggerFactory>();
        ILogger hosting = loggers.CreateLogger("Microsoft.AspNetCore.Hosting.Diagnostics");
        hosting.IsEnabled(LogLevel.Information).ShouldBeFalse();
        hosting.IsEnabled(LogLevel.Warning).ShouldBeFalse();
        hosting.IsEnabled(LogLevel.Error).ShouldBe(minimum != LogLevel.None);
        loggers.CreateLogger("Privacy.Unrelated").IsEnabled(LogLevel.Information).ShouldBeTrue();
    }

    /// <summary>The fallback guard must not replace an existing equally specific global predicate.</summary>
    [Fact]
    public async Task SharedDefaultsPreserveExistingGlobalPredicate()
    {
        WebApplicationBuilder builder = WebApplication.CreateBuilder();
        builder.Configuration.Sources.Clear();
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?> { ["Logging:LogLevel:Default"] = "Information" });
        builder.Logging.ClearProviders();
        builder.Logging.AddFilter(static (_, level) => level >= LogLevel.Error);
        builder.ConfigureOpenTelemetry();
        await using WebApplication app = builder.Build();
        ILoggerFactory loggers = app.Services.GetRequiredService<ILoggerFactory>();
        ILogger hosting = loggers.CreateLogger("Microsoft.AspNetCore.Hosting.Diagnostics");
        hosting.IsEnabled(LogLevel.Warning).ShouldBeFalse();
        hosting.IsEnabled(LogLevel.Error).ShouldBeTrue();
        loggers.CreateLogger("Privacy.Unrelated").IsEnabled(LogLevel.Warning).ShouldBeFalse();
    }

    /// <summary>Query values cannot escape through provider-specific Information overrides.</summary>
    /// <param name="providerOverrides">Whether both providers explicitly enable hosting Information records.</param>
    /// <param name="lateRules">Whether a later caller adds provider rules during post-configuration.</param>
    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task SharedDefaultsSuppressRequestQueryValuesAndRetainSafeDiagnostics(bool providerOverrides, bool lateRules)
    {
        string secret = Convert.ToHexString(RandomNumberGenerator.GetBytes(24));
        using StringWriter console = new(CultureInfo.InvariantCulture);
        TextWriter original = Console.Out;
        using RequestPrivacyLogExporter exporter = new();
        try
        {
            // Server.Tests is assembly-serialized. Restore the process writer even on failure.
            Console.SetOut(console);
            WebApplicationBuilder builder = WebApplication.CreateBuilder();
            builder.WebHost.UseTestServer();
            builder.Configuration["OTEL_EXPORTER_OTLP_ENDPOINT"] = null;
            builder.Logging.ClearProviders();
            builder.Logging.SetMinimumLevel(LogLevel.Information);
            if (providerOverrides)
            {
                builder.Configuration["Logging:Console:LogLevel:Microsoft.AspNetCore.Hosting.Diagnostics"] = "Information";
                builder.Configuration["Logging:OpenTelemetry:LogLevel:Microsoft.AspNetCore.Hosting.Diagnostics"] = "Information";
            }
            builder.ConfigureOpenTelemetry();
            if (lateRules)
            {
                builder.Services.PostConfigure<LoggerFilterOptions>(options =>
                {
                    foreach (string provider in new[] { "Console", "OpenTelemetry" })
                        options.Rules.Add(new LoggerFilterRule(provider, "Microsoft.AspNetCore.Hosting.Diagnostics", LogLevel.Information,
                            static (_, _, level) => level != LogLevel.Error));
                });
                // Validation must see the final protected rules, including those added above.
                builder.Services.AddOptions<LoggerFilterOptions>().Validate(options =>
                    options.Rules.Where(rule => rule.ProviderName is "Console" or "OpenTelemetry")
                        .All(rule => rule.Filter?.Invoke(rule.ProviderName, "Microsoft.AspNetCore.Hosting.Diagnostics", LogLevel.Information) == false));
            }
            builder.Services.Configure<JsonConsoleFormatterOptions>(options => options.IncludeScopes = true);
            builder.Logging.AddOpenTelemetry(options => options.AddProcessor(new SimpleLogRecordExportProcessor(exporter)));
            await using (WebApplication app = builder.Build())
            {
                app.MapGet("/privacy", (HttpContext context, ILoggerFactory loggers) =>
                {
                    ILogger hosting = loggers.CreateLogger("Microsoft.AspNetCore.Hosting.Diagnostics");
                    using (hosting.BeginScope(new Dictionary<string, object?> { ["SensitiveScope"] = context.Request.Query["t"].ToString() }))
                        hosting.LogInformation("Unsafe request diagnostic {QueryValue}", context.Request.QueryString);
                    using (hosting.BeginScope("Safe scope {ScopeMarker}", "safe-scope-marker"))
                        hosting.LogWarning("Safe hosting warning {WarningCode}", "safe-warning-marker");
                    if (lateRules)
                        hosting.LogError("late-predicate-denied-error-marker");
                    ILogger unrelated = loggers.CreateLogger("Privacy.Unrelated");
                    using (unrelated.BeginScope("Unrelated scope {ScopeMarker}", "unrelated-scope-marker"))
                        unrelated.LogInformation("Safe information {InformationCode}", "safe-information-marker");
                    return Results.Ok();
                });
                await app.StartAsync(TestContext.Current.CancellationToken);
                using HttpClient client = app.GetTestClient();
                using HttpResponseMessage response = await client.GetAsync("/privacy?t=" + secret + "&api_key=" + secret,
                    TestContext.Current.CancellationToken);
                response.EnsureSuccessStatusCode();
                await app.StopAsync(TestContext.Current.CancellationToken);
            }
        }
        finally
        {
            Console.SetOut(original);
        }

        foreach (string capture in new[] { console.ToString(), string.Join('\n', exporter.Records) })
        {
            capture.ShouldNotContain(secret);
            capture.ShouldNotContain("Unsafe request diagnostic");
            capture.ShouldNotContain("Request starting");
            capture.ShouldNotContain("Request finished");
            capture.ShouldNotContain("late-predicate-denied-error-marker");
            capture.ShouldContain("safe-warning-marker");
            capture.ShouldContain("WarningCode");
            capture.ShouldContain("safe-scope-marker");
            capture.ShouldContain("ScopeMarker");
            capture.ShouldContain("safe-information-marker");
            capture.ShouldContain("InformationCode");
            capture.ShouldContain("unrelated-scope-marker");
        }
    }
}
