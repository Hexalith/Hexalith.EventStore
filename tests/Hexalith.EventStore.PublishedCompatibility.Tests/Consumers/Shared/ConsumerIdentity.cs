using System.Reflection;
using System.Security.Cryptography;

namespace PublishedConsumer;

/// <summary>Reports physical identities from the running process rather than build metadata alone.</summary>
internal static class ConsumerIdentity
{
    internal static object[] Read() => new[] { "Client", "Contracts", "DomainService", "Server", "ServiceDefaults" }
        .Select(name => Assembly.Load("Hexalith.EventStore." + name))
        .Select(assembly => (object)new
        {
            name = assembly.GetName().Name,
            version = assembly.GetName().Version?.ToString(),
            informationalVersion = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion,
            configuration = assembly.GetCustomAttribute<AssemblyConfigurationAttribute>()?.Configuration,
            path = assembly.Location,
            sha256 = Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(assembly.Location))),
        }).ToArray();
}
