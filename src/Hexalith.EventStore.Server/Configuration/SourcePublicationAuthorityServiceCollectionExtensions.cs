using System.Text.Json;
using Dapr.Actors.Client;
using Hexalith.EventStore.Client.Streams;
using Hexalith.EventStore.Contracts.Streams;
using Hexalith.EventStore.Server.Streams;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Hexalith.EventStore.Server.Configuration;

/// <summary>Explicit authority composition. Registration validates supplied evidence and credentials; it never installs an enrollment.</summary>
public static class SourcePublicationAuthorityServiceCollectionExtensions
{
    /// <summary>Registers the independent journal in its separately governed actor-state host. Configure local public workload verification separately.</summary>
    public static IServiceCollection AddEventStoreSourcePublicationAuthorityJournal(this IServiceCollection services,
        SourcePublicationAuthorityInstallation installation, ISourcePublicationAuthorityInstallationEvidence evidence)
    {
        ArgumentNullException.ThrowIfNull(services); ArgumentNullException.ThrowIfNull(installation); ArgumentNullException.ThrowIfNull(evidence);
        ValidateInstallation(installation);
        services.TryAddSingleton(TimeProvider.System);
        services.AddSingleton(installation); services.AddSingleton(evidence);
        services.AddActors(options => options.Actors.RegisterActor<SourcePublicationAuthorityActor>(SourcePublicationAuthorityActor.ActorTypeName));
        return services;
    }

    /// <summary>Registers producer, observer, registrar, reader and recovery transports with mandatory independent current evidence and observation ports.</summary>
    public static IServiceCollection AddEventStoreSourcePublicationAuthority(this IServiceCollection services,
        SourcePublicationAuthorityInstallation installation, IReadOnlyList<SourcePublicationAuthorityEnrollment> enrollments,
        ISourcePublicationAuthorityInstallationEvidence evidence, SourcePublicationAuthorityCredentials credentials,
        IAuthoritativeEventStreamReader sourceReader, ISourcePublicationProjector projector, ISourcePublicationAcknowledgementObserver acknowledgements)
    {
        ArgumentNullException.ThrowIfNull(services); ArgumentNullException.ThrowIfNull(installation); ArgumentNullException.ThrowIfNull(enrollments);
        ArgumentNullException.ThrowIfNull(evidence); ArgumentNullException.ThrowIfNull(credentials); ArgumentNullException.ThrowIfNull(sourceReader);
        ArgumentNullException.ThrowIfNull(projector); ArgumentNullException.ThrowIfNull(acknowledgements);
        ValidateInstallation(installation);
        if (services.Any(item => item.ServiceType == typeof(ISourcePublicationWriterRegistration)) || enrollments.Count is < 1 or > 1000
            || enrollments.Any(item => item is null || item.Scope is null || !evidence.IsCurrent(item) || item.StorageIdentity != installation.StorageIdentity
                || item.RegistrarWorkload != installation.RegistrarWorkload || item.ValidUntil <= DateTimeOffset.UtcNow)
            || enrollments.Select(item => item.Scope).Distinct().Count() != enrollments.Count
            || enrollments.SelectMany(item => item.CommandSources).Distinct().Count() != enrollments.Sum(item => item.CommandSources.Count)
            || new[] { credentials.Producer, credentials.Observer, credentials.Registrar, credentials.Reader, credentials.Recovery }
                .Any(issuer => issuer?.CanBindResources != true))
            throw new ArgumentException("Explicit current independently qualified scopes and resource-capable role issuers are required.", nameof(enrollments));
        IReadOnlyList<SourcePublicationAuthorityEnrollment> captured = Array.AsReadOnly(
            JsonSerializer.Deserialize<SourcePublicationAuthorityEnrollment[]>(JsonSerializer.SerializeToUtf8Bytes(enrollments))!);
        services.TryAddSingleton(TimeProvider.System);
        services.AddSingleton(installation); services.AddSingleton(captured); services.AddSingleton(evidence); services.AddSingleton(credentials);
        services.AddSingleton(sourceReader); services.AddSingleton(projector); services.AddSingleton(acknowledgements);
        services.AddSingleton<SourcePublicationAuthorityClient>(); services.AddSingleton<SourcePublicationPrimaryClient>();
        services.AddSingleton<SourcePublicationAuthority>();
        services.AddSingleton<ISourcePublicationNamespaceAuthority>(provider => provider.GetRequiredService<SourcePublicationAuthority>());
        services.AddSingleton<ISourcePublicationOperationAuthority>(provider => provider.GetRequiredService<SourcePublicationAuthority>());
        services.AddSingleton<ICompleteCommandSourceAuthority>(provider => provider.GetRequiredService<SourcePublicationAuthority>());
        services.AddSingleton<ISourcePublicationWriterRegistration>(provider => new DaprSourcePublicationWriterRegistration(
            captured.Select(item => item.Scope).ToArray(), provider.GetRequiredService<IActorProxyFactory>(),
            provider.GetRequiredService<ISourcePublicationNamespaceAuthority>(), provider.GetRequiredService<TimeProvider>(),
            provider.GetRequiredService<SourcePublicationPrimaryClient>()));
        services.AddSingleton<ISourcePublicationNamespaceSource, DaprSourcePublicationNamespaceSource>();
        services.AddSingleton<ISourcePublicationIndexStore, DaprSourcePublicationIndexStore>();
        services.AddActors(options =>
        {
            options.Actors.RegisterActor<SourcePublicationNamespaceActor>(SourcePublicationNamespaceActor.ActorTypeName);
            options.Actors.RegisterActor<SourcePublicationIndexActor>(SourcePublicationIndexActor.ActorTypeName);
        });
        return services;
    }
    private static void ValidateInstallation(SourcePublicationAuthorityInstallation installation)
    {
        if (new[] { installation.Audience, installation.PrimaryAudience, installation.WorkloadScheme, installation.RegistrarWorkload, installation.StorageIdentity }
            .Any(item => string.IsNullOrWhiteSpace(item) || item.Length > 200) || installation.Audience == installation.PrimaryAudience)
            throw new ArgumentException("Distinct authority and primary audiences with exact public installation inputs are required.", nameof(installation));
    }
}
