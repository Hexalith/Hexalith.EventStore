using System.Text.Json;
using System.Text.Json.Nodes;

using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.Eventing;
using Aspire.Hosting.Lifecycle;
using CommunityToolkit.Aspire.Hosting.Dapr;
using Hexalith.EventStore.Contracts.Routing;

namespace Hexalith.EventStore.Aspire;

/// <summary>Binds observations to the ordinary toolkit-rendered authority after all modeled backend overrides and before any child starts.</summary>
internal sealed class DevelopmentRouteCatalogComponentObserver(IDaprComponentResource authority, IReadOnlyList<ProjectResource> participants,
    IReadOnlyDictionary<ProjectResource, string> selectedAppIds, IReadOnlyDictionary<ProjectResource, string> accessControlPaths) : IDistributedApplicationEventingSubscriber
{
    internal string? ComponentPath { get; private set; }

    /// <inheritdoc/>
    public Task SubscribeAsync(IDistributedApplicationEventing eventing, DistributedApplicationExecutionContext executionContext, CancellationToken cancellationToken)
    {
        eventing.Subscribe<BeforeStartEvent>(ObserveAsync);
        return Task.CompletedTask;
    }

    private async Task ObserveAsync(BeforeStartEvent startup, CancellationToken token)
    {
        if (authority.Options?.LocalPath is not null) throw new InvalidOperationException("The dedicated authority must remain an ordinary modeled component.");
        List<string> loaded = [];
        foreach (var participant in participants)
        {
            var sidecar = participant.Annotations.OfType<DaprSidecarAnnotation>().Single().Sidecar;
            var options = sidecar.Annotations.OfType<DaprSidecarOptionsAnnotation>().Single().Options;
            if (options.AppId != selectedAppIds[participant] || Path.GetFullPath(options.Config!) != accessControlPaths[participant]
                || sidecar.Annotations.OfType<DaprComponentReferenceAnnotation>().Count(reference => ReferenceEquals(reference.Component, authority)) != 1)
                throw new InvalidOperationException("The selected receiver identity, ACL or authority reference changed after catalog composition.");
            var executable = startup.Model.Resources.OfType<ExecutableResource>().Single(resource => resource.Name == sidecar.Name + "-cli");
            List<object> arguments = [];
            foreach (var callback in executable.Annotations.OfType<CommandLineArgsCallbackAnnotation>())
                await callback.Callback(new CommandLineArgsCallbackContext(arguments, executable, token)).ConfigureAwait(false);
            var candidates = arguments.Select((value, index) => (value, index)).Where(item => item.value is "--resources-path")
                .Select(item => Path.Combine((string)arguments[item.index + 1], authority.Name + ".yaml")).Where(File.Exists)
                .Select(Path.GetFullPath).Distinct(StringComparer.Ordinal).ToArray();
            if (candidates.Length != 1) throw new InvalidOperationException("The actual sidecar must load exactly one toolkit-rendered authority component.");
            loaded.Add(candidates[0]);
        }
        if (loaded.Distinct(StringComparer.Ordinal).Count() != 1) throw new InvalidOperationException("The cohort selected different authority component bytes.");
        string path = loaded[0];
        var parent = new DirectoryInfo(Path.GetDirectoryName(path)!);
        if (parent.Name != authority.Name || parent.Parent?.Name.StartsWith("aspire-dapr.", StringComparison.Ordinal) != true
            || parent.LinkTarget is not null || parent.Parent.LinkTarget is not null || new FileInfo(path).LinkTarget is not null
            || new FileInfo(path).Length > 1024 * 1024)
            throw new InvalidOperationException("The authority must be the bounded regular file emitted by the toolkit into its owned temporary resource directory.");
        JsonObject component = DevelopmentRouteCatalogAspireExtensions.ReadYamlOrJson(path);
        if (component["kind"]?.GetValue<string>() != "Component" || component["metadata"]?["name"]?.GetValue<string>() != authority.Name
            || component["spec"]?["type"]?.GetValue<string>() != "state.redis")
            throw new InvalidOperationException("The rendered component changed its dedicated authority identity.");
        var metadata = component["spec"]!["metadata"]!.AsArray();
        foreach (var item in metadata.Where(item => item?["name"]?.GetValue<string>() is "actorStateStore" or "keyPrefix").ToArray()) metadata.Remove(item);
        metadata.Add(new JsonObject { ["name"] = "keyPrefix", ["value"] = "name" });
        component["metadata"]!["labels"] ??= new JsonObject();
        component["metadata"]!["labels"]![RouteCatalogAuthorityComponentPolicy.RoleLabel] = RouteCatalogAuthorityComponentPolicy.IsolatedRole;
        component["scopes"] = JsonSerializer.SerializeToNode(participants.Select(participant => selectedAppIds[participant]).Order(StringComparer.Ordinal).ToArray());
        // The toolkit has no public scopes builder. Update only the role/scopes of its actual
        // modeled output in place; preserve its late-resolved backend/auth metadata and loading path.
        await File.WriteAllBytesAsync(path, JsonSerializer.SerializeToUtf8Bytes(component), token).ConfigureAwait(false);
        if (!OperatingSystem.IsWindows()) File.SetUnixFileMode(path, UnixFileMode.UserRead | UnixFileMode.UserWrite);
        ComponentPath = path;
    }
}
