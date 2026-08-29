using System.Globalization;
using System.Net;

namespace Hexalith.EventStore.Server.LiveSidecar.Tests.Fixtures;

/// <summary>Parses the host endpoint reported by <c>docker port</c> for a control-plane container.</summary>
internal static class DockerPublishedPortResolver
{
    /// <summary>Returns the single host port published for a container port.</summary>
    /// <param name="output">Standard output from <c>docker port &lt;container&gt; &lt;port&gt;/tcp</c>.</param>
    /// <param name="containerName">Container name used only in the support-safe failure message.</param>
    /// <param name="containerPort">Container port used only in the support-safe failure message.</param>
    /// <returns>The Docker-published host port.</returns>
    internal static int ParseHostPort(string output, string containerName, int containerPort)
    {
        ArgumentNullException.ThrowIfNull(output);
        ArgumentException.ThrowIfNullOrWhiteSpace(containerName);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(containerPort);

        int[] ports = output
            .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(ParseEndpointPort)
            .Where(static port => port is > 0 and <= IPEndPoint.MaxPort)
            .Distinct()
            .ToArray();

        return ports.Length == 1
            ? ports[0]
            : throw new InvalidOperationException(
                $"Docker did not report one unambiguous host port for {containerName}:{containerPort.ToString(CultureInfo.InvariantCulture)}/tcp.");
    }

    private static int ParseEndpointPort(string endpoint)
    {
        int separator = endpoint.LastIndexOf(':');
        return separator >= 0
            && int.TryParse(
                endpoint.AsSpan(separator + 1),
                NumberStyles.None,
                CultureInfo.InvariantCulture,
                out int port)
            ? port
            : 0;
    }
}
