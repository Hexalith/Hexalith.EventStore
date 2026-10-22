using System.Globalization;
using System.Text.RegularExpressions;

using Microsoft.Extensions.DependencyInjection;

using Shouldly;

namespace Hexalith.EventStore.Server.LiveSidecar.Tests.Fixtures;

public sealed partial class DaprTestContainerFixture
{
    /// <summary>Scrapes actual native completed-RPC/app-callback counters separately from logical SDK observations.</summary>
    internal async Task<IReadOnlyDictionary<string, long>> CaptureNativeAttemptsAsync()
    {
        var result = new Dictionary<string, long>(StringComparer.Ordinal);
        using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(5) };
        foreach (var host in new[] { (Name: "primary", Port: _daprMetricsPort, App: _testHost),
            (Name: "replica", Port: _replicaDaprMetricsPort, App: _replicaTestHost) })
        {
            if (host.App is null) continue;
            foreach (var callback in host.App.Services.GetRequiredService<LiveNativeCallbackCounter>().Snapshot())
                result[host.Name + "/app_callbacks/" + callback.Key] = callback.Value;
            foreach (var send in host.App.Services.GetRequiredService<LiveActorSendCounter>().Snapshot())
                result[host.Name + "/caller_actor_http/" + send.Key] = send.Value;
            foreach (var send in host.App.Services.GetRequiredService<LiveSdkHttpAttemptObserver>().Snapshot())
                result[host.Name + "/sdk_http/" + send.Key] = send.Value;
            string metrics = await client.GetStringAsync(new Uri($"http://127.0.0.1:{host.Port}/metrics")).ConfigureAwait(false);
            foreach (string line in metrics.Split('\n'))
            {
                if (line.Length == 0 || line[0] == '#') continue;
                Match sample = Regex.Match(line, "^([a-zA-Z_][a-zA-Z0-9_]*)(?:\\{([^}]*)\\})?\\s+([0-9.eE+\\-]+)(?:\\s|$)", RegexOptions.CultureInvariant);
                if (!sample.Success) continue;
                string name = sample.Groups[1].Value;
                if (!long.TryParse(sample.Groups[3].Value, NumberStyles.AllowExponent | NumberStyles.AllowDecimalPoint,
                    CultureInfo.InvariantCulture, out long count) || count < 0) continue;
                string? method = null;
                if (name.Contains("grpc", StringComparison.Ordinal) && name.Contains("completed_rpcs", StringComparison.Ordinal))
                {
                    Match label = Regex.Match(sample.Groups[2].Value, "grpc_(?:client|server)_method=\"(/?dapr\\.proto\\.(?:runtime|internals)\\.v1\\.[A-Za-z0-9_.]+/[A-Za-z0-9_]+)\"", RegexOptions.CultureInvariant);
                    if (label.Success) method = label.Groups[1].Value;
                }
                else if (name.Contains("http_client", StringComparison.Ordinal) && name.Contains("completed_count", StringComparison.Ordinal))
                    method = "all_app_callbacks";
                else if (name.Contains("http_server", StringComparison.Ordinal) && name.Contains("request_count", StringComparison.Ordinal))
                    method = "all_sidecar_requests";
                if (method is null) continue;
                string key = host.Name + "/native/" + name + "/" + method;
                result[key] = result.GetValueOrDefault(key) + count;
            }
        }
        return result;
    }

    internal static IReadOnlyDictionary<string, long> NativeAttemptDelta(IReadOnlyDictionary<string, long> before,
        IReadOnlyDictionary<string, long> after)
        => after.ToDictionary(static pair => pair.Key, pair => pair.Value - before.GetValueOrDefault(pair.Key), StringComparer.Ordinal);

    /// <summary>Checks independent observations of the same actor calls, without adding transport layers together.</summary>
    internal static void AssertActorAttemptCoverage(IReadOnlyDictionary<string, long> attempts)
    {
        long callers = attempts.Where(static pair => pair.Key.Contains("/caller_actor_http/", StringComparison.Ordinal))
            .Sum(static pair => pair.Value);
        long diagnosticSends = attempts.Where(static pair => pair.Key.EndsWith("/sdk_http/actor_method", StringComparison.Ordinal))
            .Sum(static pair => pair.Value);
        long callbacks = attempts.Where(static pair => pair.Key.EndsWith("/app_callbacks/actor_methods", StringComparison.Ordinal))
            .Sum(static pair => pair.Value);
        diagnosticSends.ShouldBe(callers);
        callbacks.ShouldBe(callers);
    }
}
