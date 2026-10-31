using System.Text.Json;

using Microsoft.Extensions.Configuration;

namespace Hexalith.EventStore.ServiceDefaults.Routing;

/// <summary>Privately retains the owner credential handoff; only nonsecret coordinates enter host observations.</summary>
internal sealed class DevelopmentActorStateOwnerBoundary
{
    private readonly IConfiguration _configuration;
    private readonly DevelopmentActorStateOwner[] _owners;
    private readonly DevelopmentActorStateOwnerObservation[] _observed;

    internal DevelopmentActorStateOwnerBoundary(IConfiguration configuration)
    {
        _configuration = configuration;
        _owners = Configured(configuration);
        _observed = ReadObserved(configuration);
    }

    internal void RequireUnchanged(IReadOnlyList<DevelopmentActorStateOwner> effective)
    {
        var configured = Configured(_configuration);
        if (!Equivalent(_owners, configured) || !Equivalent(_owners, effective)
            || !_observed.SequenceEqual(ReadObserved(_configuration)))
            throw Refusal();
    }

    internal static DevelopmentActorStateOwnerObservation[] ReadObserved(IConfiguration configuration)
    {
        var owners = Configured(configuration);
        var section = configuration.GetSection("EventStore:RouteCatalog:DevelopmentBootstrap");
        if (owners.Length == 0)
        {
            if (section["ActorStateOwnersPath"] is not null) throw Refusal();
            return [];
        }
        string directory = Path.GetFullPath(section["Directory"] ?? throw Refusal());
        string host = section["HostId"] ?? throw Refusal();
        string path = section["ActorStateOwnersPath"] ?? throw Refusal();
        if (host.Length is < 1 or > 128 || host.Any(character => !char.IsAsciiLetterOrDigit(character) && character is not ('-' or '_'))
            || !Path.IsPathFullyQualified(path) || path != Path.Combine(directory, host + ".actor-state-owners.json")) throw Refusal();
        var file = new FileInfo(path);
        if (!file.Exists || file.LinkTarget is not null || file.Length is < 2 or > 32768) throw Refusal();
        DevelopmentActorStateOwnerObservation[] observed;
        try
        {
            using var stream = file.OpenRead();
            byte[] bytes = new byte[32769];
            int length = stream.ReadAtLeast(bytes, bytes.Length, throwOnEndOfStream: false);
            if (length > 32768) throw Refusal();
            observed = JsonSerializer.Deserialize<DevelopmentActorStateOwnerObservation[]>(bytes.AsSpan(0, length)) ?? throw Refusal();
        }
        catch (Exception exception) when (exception is JsonException or IOException or UnauthorizedAccessException) { throw Refusal(); }
        if (observed.Length != owners.Length || observed.Any(owner => owner is null || !owner.ApiTokenConfigured)
            || observed.Select(owner => owner.AppId).Distinct(StringComparer.Ordinal).Count() != observed.Length
            || !observed.SequenceEqual(owners.Select(owner => new DevelopmentActorStateOwnerObservation(owner.AppId, CanonicalEndpoint(owner.HttpEndpoint), true))))
            throw Refusal();
        string[] selected = section.GetSection("SelectedAppIds").Get<string[]>() ?? [];
        if (observed.Any(owner => !selected.Contains(owner.AppId, StringComparer.Ordinal))) throw Refusal();
        string? nativeEndpoint = configuration["AdminServer:EventStoreDaprHttpEndpoint"];
        string? nativeOwner = configuration["AdminServer:EventStoreAppId"];
        if (nativeEndpoint is null || nativeOwner is null
            || !observed.Any(owner => owner.AppId == nativeOwner && owner.HttpEndpoint == CanonicalEndpoint(nativeEndpoint))) throw Refusal();
        return observed;
    }

    internal static string CanonicalEndpoint(string value)
    {
        if (!Uri.TryCreate(value, UriKind.Absolute, out var uri) || uri.Scheme is not ("http" or "https")
            || uri.UserInfo.Length != 0 || uri.Query.Length != 0 || uri.Fragment.Length != 0
            || uri.AbsolutePath != "/") throw Refusal();
        return uri.AbsoluteUri;
    }

    private static DevelopmentActorStateOwner[] Configured(IConfiguration configuration)
    {
        var owners = configuration.GetSection("EventStore:RouteCatalog:DevelopmentActorStateOwners").Get<DevelopmentActorStateOwner[]>() ?? [];
        if (owners.Length > 32 || owners.Any(owner => owner is null || string.IsNullOrWhiteSpace(owner.AppId) || string.IsNullOrWhiteSpace(owner.ApiToken))
            || owners.Select(owner => owner.AppId).Distinct(StringComparer.Ordinal).Count() != owners.Length) throw Refusal();
        foreach (var owner in owners) _ = CanonicalEndpoint(owner.HttpEndpoint);
        return owners;
    }

    private static bool Equivalent(IReadOnlyList<DevelopmentActorStateOwner> left, IReadOnlyList<DevelopmentActorStateOwner> right)
        => left.Count == right.Count && left.Zip(right).All(pair => pair.Second is not null && pair.First.AppId == pair.Second.AppId
            && pair.First.HttpEndpoint == pair.Second.HttpEndpoint && pair.First.ApiToken == pair.Second.ApiToken);

    private static RouteCatalogUnavailableException Refusal() => new("The selected owner sidecar coordinates or private credential handoff changed.", RouteCatalogRefusalReason.Configuration);
}
