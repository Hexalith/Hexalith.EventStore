using System.Globalization;

namespace Hexalith.EventStore.Server.LiveSidecar.Tests.Fixtures;

/// <summary>Allows an isolated native Development control plane on explicitly configured loopback ports.</summary>
internal static class DaprTestInfrastructurePorts
{
    /// <summary>Reads one explicit loopback port, preserving the existing fixture default when absent.</summary>
    internal static int Read(string variableName, int defaultPort)
        => Parse(Environment.GetEnvironmentVariable(variableName), defaultPort, variableName);

    /// <summary>Uses only the configured port when supplied; no fallback to a shared control plane is permitted.</summary>
    internal static int[] ReadCandidates(string variableName, params int[] defaults)
    {
        string? value = Environment.GetEnvironmentVariable(variableName);
        return value is null ? defaults : [Parse(value, defaults[0], variableName)];
    }

    /// <summary>Rejects empty, malformed, zero and out-of-range infrastructure overrides before startup.</summary>
    internal static int Parse(string? value, int defaultPort, string variableName)
    {
        if (value is null)
        {
            return defaultPort;
        }

        if (!int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out int port) || port is < 1 or > 65535)
        {
            throw new InvalidOperationException($"{variableName} must name a loopback port between 1 and 65535.");
        }

        return port;
    }
}
