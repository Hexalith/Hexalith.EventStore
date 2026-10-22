using System.Net;
using System.Text.RegularExpressions;

namespace Hexalith.EventStore.Server.LiveSidecar.Tests.Fixtures;

/// <summary>Validates opt-in isolated qualification inputs before any process or container starts.</summary>
internal sealed class Oq8QualificationOverrides
{
    /// <summary>Gets the application build configuration.</summary>
    internal string Configuration { get; private init; } = "Release";

    /// <summary>Gets the explicitly selected sidecar executable, when supplied.</summary>
    internal string? DaprdPath { get; private init; }

    /// <summary>Gets the placement container selected by the caller.</summary>
    internal string PlacementContainer { get; private init; } = "dapr_placement";

    /// <summary>Gets the scheduler container selected by the caller.</summary>
    internal string SchedulerContainer { get; private init; } = "dapr_scheduler";

    /// <summary>Gets the loopback Redis endpoint.</summary>
    internal string RedisEndpoint { get; private init; } = "127.0.0.1:6379";

    /// <summary>Gets the private actor/scheduler namespace; the fixture-private SQLite registry isolates discovery.</summary>
    internal string? Namespace { get; private init; }

    /// <summary>Reads and validates the complete override group.</summary>
    /// <param name="read">The environment variable reader.</param>
    /// <returns>The validated execution inputs.</returns>
    internal static Oq8QualificationOverrides Read(Func<string, string?> read)
    {
        string[] names = ["DAPRD_PATH", "CONFIGURATION", "PLACEMENT_CONTAINER", "SCHEDULER_CONTAINER", "REDIS_ENDPOINT", "NAMESPACE"];
        string?[] values = names.Select(name => read("HEXALITH_OQ8_" + name)).ToArray();
        if (values.All(static value => value is null))
        {
            return new();
        }

        if (values.Any(static value => string.IsNullOrWhiteSpace(value)))
        {
            throw new InvalidOperationException("Isolated OQ8 overrides must supply the complete six-variable group.");
        }

        string binary = values[0]!;
        if (!Path.IsPathFullyQualified(binary) || !File.Exists(binary))
        {
            throw new InvalidOperationException("The OQ8 sidecar override must select an existing absolute executable path.");
        }

        string configuration = values[1]!;
        if (configuration is not ("Debug" or "Release"))
        {
            throw new InvalidOperationException("OQ8 configuration must be exactly Debug or Release.");
        }

        foreach (string name in values[2..4].OfType<string>())
        {
            if (!Regex.IsMatch(name, "^g6-oq8-[a-z0-9][a-z0-9-]{0,80}$", RegexOptions.CultureInvariant))
            {
                throw new InvalidOperationException("An isolated OQ8 control-plane container must use the g6-oq8- prefix.");
            }
        }

        if (values[2] == values[3])
        {
            throw new InvalidOperationException("OQ8 placement and scheduler containers must be distinct.");
        }

        if (!IPEndPoint.TryParse(values[4], out IPEndPoint? redis)
            || !IPAddress.IsLoopback(redis.Address)
            || redis.Port == 0)
        {
            throw new InvalidOperationException("The OQ8 Redis override must be one numeric loopback endpoint with a positive port.");
        }

        if (!Regex.IsMatch(values[5]!, "^g6-oq8-[a-z0-9][a-z0-9-]{0,48}$", RegexOptions.CultureInvariant))
        {
            throw new InvalidOperationException("An isolated OQ8 namespace must use a bounded g6-oq8- DNS label.");
        }

        return new()
        {
            Namespace = values[5],
            DaprdPath = binary,
            Configuration = configuration,
            PlacementContainer = values[2]!,
            SchedulerContainer = values[3]!,
            RedisEndpoint = redis.ToString(),
        };
    }
}
