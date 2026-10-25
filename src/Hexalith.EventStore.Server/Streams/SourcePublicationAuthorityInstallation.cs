namespace Hexalith.EventStore.Server.Streams;

/// <summary>Public independent authority-host binding. It installs neither enrollment nor credentials.</summary>
/// <param name="Audience">The private authority workload audience.</param><param name="PrimaryAudience">The private primary actor workload audience.</param>
/// <param name="WorkloadScheme">The configured workload verifier scheme.</param><param name="RegistrarWorkload">The sole initial enrollment principal.</param>
/// <param name="StorageIdentity">The independent actor-state storage identity.</param>
public sealed record SourcePublicationAuthorityInstallation(string Audience, string PrimaryAudience, string WorkloadScheme,
    string RegistrarWorkload, string StorageIdentity);
