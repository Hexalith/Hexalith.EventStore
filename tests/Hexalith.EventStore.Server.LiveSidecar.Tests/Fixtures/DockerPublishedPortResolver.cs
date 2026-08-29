using System.Globalization;
using System.Net;

namespace Hexalith.EventStore.Server.LiveSidecar.Tests.Fixtures;

/// <summary>Parses the host endpoint reported by <c>docker port</c> for a control-plane container.</summary>
internal static class DockerPublishedPortResolver
{
    /// <summary>Returns one unambiguous local host endpoint published for a container port.</summary>
    /// <param name="output">Standard output from <c>docker port &lt;container&gt; &lt;port&gt;/tcp</c>.</param>
    /// <param name="containerName">Container name used only in the support-safe failure message.</param>
    /// <param name="containerPort">Container port used only in the support-safe failure message.</param>
    /// <returns>The Docker-published local host endpoint, preserving its address family.</returns>
    internal static IPEndPoint ParseHostEndpoint(string output, string containerName, int containerPort)
    {
        ArgumentNullException.ThrowIfNull(output);
        ArgumentException.ThrowIfNullOrWhiteSpace(containerName);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(containerPort);

        string[] lines = output
            .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToArray();
        if (lines.Length == 0)
        {
            throw CreateAmbiguousEndpointException(containerName, containerPort);
        }

        var endpoints = new List<IPEndPoint>(lines.Length);
        foreach (string line in lines)
        {
            if (!IPEndPoint.TryParse(line, out IPEndPoint? endpoint)
                || endpoint.Port is <= 0 or > IPEndPoint.MaxPort
                || !IsLocalBinding(endpoint.Address))
            {
                throw CreateAmbiguousEndpointException(containerName, containerPort);
            }

            endpoints.Add(endpoint);
        }

        int[] ports = endpoints.Select(static endpoint => endpoint.Port).Distinct().ToArray();
        if (ports.Length != 1)
        {
            throw CreateAmbiguousEndpointException(containerName, containerPort);
        }

        return endpoints
            .OrderBy(static endpoint => endpoint.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork ? 0 : 1)
            .ThenBy(static endpoint => IPAddress.IsLoopback(endpoint.Address) ? 0 : 1)
            .First();
    }

    private static bool IsLocalBinding(IPAddress address)
        => IPAddress.IsLoopback(address)
            || address.Equals(IPAddress.Any)
            || address.Equals(IPAddress.IPv6Any);

    private static InvalidOperationException CreateAmbiguousEndpointException(string containerName, int containerPort)
        => new(
            $"Docker did not report one unambiguous local host endpoint for {containerName}:{containerPort.ToString(CultureInfo.InvariantCulture)}/tcp.");
}
