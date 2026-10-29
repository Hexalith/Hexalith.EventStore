using System.Diagnostics;
using System.Text.Json;

using Hexalith.Commons.UniqueIds;

namespace Hexalith.EventStore.Server.LiveSidecar.Tests.Fixtures;

/// <summary>Owns one isolated Redis backend on a random loopback port; no shared machine state is read, written or cleared.</summary>
internal sealed class RouteCatalogDevelopmentRedis : IAsyncDisposable
{
    // Reuses the existing published-compatibility Redis pin; no new version/profile authority.
    public const string Image = "redis@sha256:c35b83ce044bb6d148c484d36e059ad28e02d5714ba6731fb55b6421e2ed0ccf";
    private const string OwnerLabel = "hexalith.route-catalog-fixture";
    private readonly string _owner = UniqueIdHelper.GenerateSortableUniqueStringId().ToLowerInvariant();
    private string? _containerId;
    private RouteCatalogDocker? _docker;
    internal RouteCatalogDocker Docker => _docker ?? throw new InvalidOperationException("The local Docker endpoint has not been captured.");
    public string Endpoint { get; private set; } = "";

    public async Task StartAsync()
    {
        _docker = await RouteCatalogDocker.CaptureAsync();
        _containerId = (await RunAsync(["run", "--detach", "--rm", "--pull", "never", "--name", "catalog-redis-" + _owner,
            "--label", OwnerLabel + "=" + _owner, "--publish", "127.0.0.1::6379", Image, "redis-server", "--save", "", "--appendonly", "no"])).Trim();
        using var inspection = JsonDocument.Parse(await RunAsync(["inspect", _containerId]));
        JsonElement owned = inspection.RootElement[0];
        RequireOwnership(owned);
        JsonElement binding = owned.GetProperty("NetworkSettings").GetProperty("Ports").GetProperty("6379/tcp")[0];
        if (binding.GetProperty("HostIp").GetString() != "127.0.0.1"
            || !int.TryParse(binding.GetProperty("HostPort").GetString(), out int port) || port <= 0)
            throw new InvalidOperationException("Catalog Redis must bind one owned random loopback port.");
        Endpoint = "127.0.0.1:" + port;
        for (int attempt = 0; attempt < 50; attempt++)
        {
            try { if ((await RunAsync(["exec", _containerId, "redis-cli", "ping"])).Trim() == "PONG") return; }
            catch (InvalidOperationException) { }
            await Task.Delay(100);
        }
        throw new InvalidOperationException("Owned catalog Redis did not become ready.");
    }

    public async ValueTask DisposeAsync()
    {
        if (_containerId is null) return;
        using var inspection = JsonDocument.Parse(await RunAsync(["inspect", _containerId]));
        RequireOwnership(inspection.RootElement[0]);
        string container = _containerId;
        await RunAsync(["rm", "--force", container]);
        _containerId = null;
    }

    private void RequireOwnership(JsonElement container)
    {
        if (container.GetProperty("Id").GetString() != _containerId
            || container.GetProperty("Config").GetProperty("Labels").GetProperty(OwnerLabel).GetString() != _owner)
            throw new InvalidOperationException("Refusing to operate on a Redis container without this fixture's exact ownership identity.");
    }

    private Task<string> RunAsync(string[] arguments) => Docker.RunAsync(arguments);
}
