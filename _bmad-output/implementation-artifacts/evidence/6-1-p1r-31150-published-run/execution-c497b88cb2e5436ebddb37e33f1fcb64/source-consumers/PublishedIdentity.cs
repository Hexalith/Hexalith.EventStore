using System.Reflection;
using System.Security.Cryptography;

/// <summary>Observes the actual assembly load context and physical file bytes.</summary>
internal static class PublishedIdentity
{
    /// <summary>Loads each selected assembly and returns its physical identity.</summary>
    internal static object Read()
    {
        string[] names = ["Hexalith.EventStore.Client", "Hexalith.EventStore.Contracts",
            "Hexalith.EventStore.Server", "Hexalith.EventStore.DomainService", "Hexalith.EventStore.ServiceDefaults"];
        return new
        {
            assemblies = names.Select(Assembly.Load).Select(assembly => new
            {
                name = assembly.GetName().Name,
                version = assembly.GetName().Version?.ToString(),
                path = assembly.Location,
                sha256 = Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(assembly.Location))),
            }).ToArray(),
        };
    }
}
