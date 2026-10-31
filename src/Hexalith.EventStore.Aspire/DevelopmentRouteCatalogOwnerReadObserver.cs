using System.Globalization;
using System.Security.Cryptography;
using System.Text.Json;

using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.Eventing;
using Aspire.Hosting.Lifecycle;

using CommunityToolkit.Aspire.Hosting.Dapr;

using Hexalith.EventStore.ServiceDefaults.Routing;

namespace Hexalith.EventStore.Aspire;

/// <summary>Retains actual selected owner coordinates without publishing their private API credentials.</summary>
internal sealed class DevelopmentRouteCatalogOwnerReadObserver : IDistributedApplicationEventingSubscriber
{
    private readonly DistributedApplicationExecutionContext _execution;
    private readonly Dictionary<ProjectResource, (string AppId, int Port)> _owners = [];
    private readonly Dictionary<ProjectResource, string> _credentials = [];
    private readonly Dictionary<ProjectResource, ExecutableResource> _executables = [];
    private readonly Dictionary<ProjectResource, Task> _publications = [];
    private readonly List<(ProjectResource Receiver, ProjectResource[] Owners, string Path)> _receivers = [];

    internal DevelopmentRouteCatalogOwnerReadObserver(IDistributedApplicationBuilder builder, IReadOnlyList<DevelopmentRouteCatalogReceiver> receivers,
        IReadOnlyDictionary<ProjectResource, string> apps, string directory)
    {
        _execution = builder.ExecutionContext;
        foreach (var receiver in receivers.Where(receiver => receiver.ActorStateOwners.Count > 0))
        {
            if (!receiver.TechnicalHttp || receiver.ActorStateOwners.Select(owner => owner.Resource).Distinct().Count() != receiver.ActorStateOwners.Count
                || receiver.ActorStateOwners.Any(owner => !receivers.Any(peer => ReferenceEquals(peer.Project.Resource, owner.Resource))))
                throw new InvalidOperationException("Native owner reads require actual selected peers and the explicit technical receiver profile.");
            var selected = receiver.ActorStateOwners.Select(owner => owner.Resource).ToArray();
            string path = Path.Combine(directory, receiver.Project.Resource.Name + ".actor-state-owners.json");
            _receivers.Add((receiver.Project.Resource, selected, path));
            foreach (var owner in receiver.ActorStateOwners)
            {
                if (_owners.ContainsKey(owner.Resource)) continue;
                var sidecar = Sidecar(owner.Resource);
                int port = sidecar.Annotations.OfType<DaprSidecarOptionsAnnotation>().Single().Options.DaprHttpPort
                    ?? throw new InvalidOperationException("The existing native owner read transport requires its actual fixed Dapr HTTP port.");
                _owners.Add(owner.Resource, (apps[owner.Resource], port));
                string generated = Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(32));
                var parameter = builder.AddParameter(owner.Resource.Name + "-catalog-owner-api-token", () => generated, secret: true);
                // Preserve an owner's existing private API credential; otherwise give only this
                // owner's application and sidecar a distinct run credential.
                sidecar.Annotations.Add(new EnvironmentCallbackAnnotation(context =>
                {
                    if (!context.EnvironmentVariables.ContainsKey("DAPR_API_TOKEN")) context.EnvironmentVariables["DAPR_API_TOKEN"] = parameter.Resource;
                }));
                owner.WithEnvironment(async context =>
                {
                    object credential = await CredentialAsync(sidecar, context.CancellationToken).ConfigureAwait(false);
                    if (context.EnvironmentVariables.TryGetValue("DAPR_API_TOKEN", out var existing)
                        && await ValueAsync(existing, context.CancellationToken).ConfigureAwait(false) != await ValueAsync(credential, context.CancellationToken).ConfigureAwait(false))
                        throw new InvalidOperationException("The selected owner's application and sidecar API credentials disagree.");
                    context.EnvironmentVariables["DAPR_API_TOKEN"] = credential;
                });
            }
            receiver.Project.WithEnvironment(async context =>
            {
                context.EnvironmentVariables["EventStore__RouteCatalog__DevelopmentBootstrap__ActorStateOwnersPath"] = path;
                for (int index = 0; index < selected.Length; index++)
                {
                    var owner = selected[index]; var expected = _owners[owner];
                    string prefix = "EventStore__RouteCatalog__DevelopmentActorStateOwners__" + index + "__";
                    context.EnvironmentVariables[prefix + "AppId"] = expected.AppId;
                    context.EnvironmentVariables[prefix + "HttpEndpoint"] = Endpoint(expected.Port);
                    object credential = await CredentialAsync(Sidecar(owner), context.CancellationToken).ConfigureAwait(false);
                    if (!_credentials.TryGetValue(owner, out var retained)
                        || retained != await ValueAsync(credential, context.CancellationToken).ConfigureAwait(false))
                        throw new InvalidOperationException("The selected owner's private API credential changed after rendering.");
                    context.EnvironmentVariables[prefix + "ApiToken"] = credential;
                }
                // The existing Admin service's native transport is a single configured owner.
                if (context.EnvironmentVariables.TryGetValue("AdminServer__EventStoreDaprHttpEndpoint", out var configured))
                {
                    string? endpoint = await ValueAsync(configured, context.CancellationToken).ConfigureAwait(false);
                    if (!selected.Any(owner => endpoint?.TrimEnd('/') == Endpoint(_owners[owner].Port).TrimEnd('/')))
                        throw new InvalidOperationException("The Admin native read endpoint differs from its actual selected owner sidecar.");
                }
                if (context.EnvironmentVariables.TryGetValue("AdminServer__EventStoreAppId", out var configuredApp)
                    && !selected.Any(owner => configuredApp.ToString() == _owners[owner].AppId))
                    throw new InvalidOperationException("The Admin native read owner identity differs from its selected resource.");
                if (!context.EnvironmentVariables.ContainsKey("AdminServer__EventStoreAppId") && selected.Length == 1)
                    context.EnvironmentVariables["AdminServer__EventStoreAppId"] = _owners[selected[0]].AppId;
            });
        }
    }

    public Task SubscribeAsync(IDistributedApplicationEventing eventing, DistributedApplicationExecutionContext executionContext, CancellationToken cancellationToken)
    {
        eventing.Subscribe<BeforeStartEvent>(ObserveAsync);
        eventing.Subscribe<BeforeResourceStartedEvent>(ObserveTargetAsync);
        return Task.CompletedTask;
    }

    private async Task ObserveAsync(BeforeStartEvent startup, CancellationToken token)
    {
        foreach (var owner in _owners)
        {
            var sidecar = Sidecar(owner.Key);
            var options = sidecar.Annotations.OfType<DaprSidecarOptionsAnnotation>().Single().Options;
            var executable = startup.Model.Resources.OfType<ExecutableResource>().Single(resource => resource.Name == sidecar.Name + "-cli");
            _executables[owner.Key] = executable;
            if (options.AppId != owner.Value.AppId || options.DaprHttpPort != owner.Value.Port
                || !executable.Annotations.OfType<EndpointAnnotation>().Any(endpoint => endpoint.Port == owner.Value.Port))
                throw new InvalidOperationException("The actual rendered owner sidecar identity or HTTP listener changed.");
            string? expected = await ValueAsync(await CredentialAsync(sidecar, token).ConfigureAwait(false), token).ConfigureAwait(false);
            var rendered = await EnvironmentAsync(executable, token).ConfigureAwait(false);
            if (string.IsNullOrWhiteSpace(expected) || !rendered.TryGetValue("DAPR_API_TOKEN", out var credential)
                || await ValueAsync(credential, token).ConfigureAwait(false) != expected)
                throw new InvalidOperationException("The actual rendered owner sidecar lost its private API credential.");
            if (_credentials.TryGetValue(owner.Key, out var prior) && prior != expected)
                throw new InvalidOperationException("The selected owner's private API credential changed during rendering.");
            _credentials[owner.Key] = expected;
        }
        foreach (var receiver in _receivers)
        {
            receiver.Receiver.Annotations.Add(new EnvironmentCallbackAnnotation(async context =>
            {
                // This is installed after ordinary model overrides and validates their final
                // handoff against the actual rendered owner's private credential.
                for (int index = 0; index < receiver.Owners.Length; index++)
                {
                    var owner = receiver.Owners[index]; var expected = _owners[owner];
                    string prefix = "EventStore__RouteCatalog__DevelopmentActorStateOwners__" + index + "__";
                    if (!context.EnvironmentVariables.TryGetValue(prefix + "AppId", out var app) || app.ToString() != expected.AppId
                        || !context.EnvironmentVariables.TryGetValue(prefix + "HttpEndpoint", out var endpoint) || endpoint.ToString() != Endpoint(expected.Port)
                        || !context.EnvironmentVariables.TryGetValue(prefix + "ApiToken", out var credential)
                        || await ValueAsync(credential, context.CancellationToken).ConfigureAwait(false) != _credentials[owner])
                        throw new InvalidOperationException("The final Admin owner-read handoff differs from the actual rendered owner.");
                }
                string? endpointValue = context.EnvironmentVariables.TryGetValue("AdminServer__EventStoreDaprHttpEndpoint", out var nativeEndpoint)
                    ? await ValueAsync(nativeEndpoint, context.CancellationToken).ConfigureAwait(false) : null;
                string? appValue = context.EnvironmentVariables.TryGetValue("AdminServer__EventStoreAppId", out var nativeApp)
                    ? await ValueAsync(nativeApp, context.CancellationToken).ConfigureAwait(false) : null;
                if (!receiver.Owners.Any(owner => appValue == _owners[owner].AppId
                    && endpointValue?.TrimEnd('/') == Endpoint(_owners[owner].Port).TrimEnd('/')))
                    throw new InvalidOperationException("The final Admin native transport differs from its actual selected owner.");
            }));
        }
    }

    private async Task ObserveTargetAsync(BeforeResourceStartedEvent startup, CancellationToken token)
    {
        foreach (var owner in _owners.Keys.Where(owner => ReferenceEquals(owner, startup.Resource)
            || _executables.TryGetValue(owner, out var executable) && ReferenceEquals(executable, startup.Resource)))
        {
            await RequireActualAsync(owner, token).ConfigureAwait(false);
            if (ReferenceEquals(owner, startup.Resource))
            {
                var environment = await EnvironmentAsync(owner, token).ConfigureAwait(false);
                if (!environment.TryGetValue("DAPR_API_TOKEN", out var credential)
                    || await ValueAsync(credential, token).ConfigureAwait(false) != _credentials[owner])
                    throw new InvalidOperationException("The actual owner application API credential changed before execution.");
            }
        }
        foreach (var receiver in _receivers.Where(receiver => ReferenceEquals(receiver.Receiver, startup.Resource)))
        {
            foreach (var owner in receiver.Owners) await RequireActualAsync(owner, token).ConfigureAwait(false);
            _ = await EnvironmentAsync(receiver.Receiver, token).ConfigureAwait(false);
            Task publication;
            lock (_publications)
            {
                if (!_publications.TryGetValue(receiver.Receiver, out publication!))
                    _publications.Add(receiver.Receiver, publication = PublishAsync(receiver.Owners, receiver.Path, token));
            }
            await publication.ConfigureAwait(false);
        }
    }

    private async Task RequireActualAsync(ProjectResource owner, CancellationToken token)
    {
        if (!_executables.TryGetValue(owner, out var executable)) throw new InvalidOperationException("The actual owner CLI has not been rendered.");
        var expected = _owners[owner];
        var listener = executable.Annotations.OfType<EndpointAnnotation>().SingleOrDefault(endpoint => endpoint.Name == "http");
        if (listener?.AllocatedEndpoint is not { } allocated || allocated.Port != expected.Port || allocated.UriScheme != "http"
            || allocated.Address is not ("localhost" or "127.0.0.1" or "::1"))
            throw new InvalidOperationException("The actual allocated owner HTTP listener differs from the selected native transport.");
        List<object> arguments = [];
        foreach (var callback in executable.Annotations.OfType<CommandLineArgsCallbackAnnotation>())
            await callback.Callback(new CommandLineArgsCallbackContext(arguments, executable, token)).ConfigureAwait(false);
        async Task<string?> Argument(string name, string alias)
        {
            var positions = arguments.Select((value, index) => (value, index)).Where(item => item.value is string value && value == name).ToArray();
            if (positions.Length != 1 || positions[0].index + 1 >= arguments.Count
                || arguments.OfType<string>().Any(value => value == alias || value.StartsWith(alias + "=", StringComparison.Ordinal) || value.StartsWith(name + "=", StringComparison.Ordinal)))
                throw new InvalidOperationException("The actual evaluated owner CLI identity or HTTP-port arguments are ambiguous.");
            return await ValueAsync(arguments[positions[0].index + 1], token).ConfigureAwait(false);
        }
        string? target = listener.TargetPort?.ToString(CultureInfo.InvariantCulture) ?? allocated.TargetPortExpression;
        if (string.IsNullOrWhiteSpace(target) || await Argument("--app-id", "-a").ConfigureAwait(false) != expected.AppId
            || await Argument("--dapr-http-port", "-H").ConfigureAwait(false) != target)
            throw new InvalidOperationException("The actual evaluated owner CLI identity or HTTP-port arguments changed.");
        var environment = await EnvironmentAsync(executable, token).ConfigureAwait(false);
        if (!environment.TryGetValue("DAPR_API_TOKEN", out var credential)
            || await ValueAsync(credential, token).ConfigureAwait(false) != _credentials[owner])
            throw new InvalidOperationException("The actual owner CLI API credential changed before execution.");
    }

    private async Task PublishAsync(ProjectResource[] owners, string path, CancellationToken token)
    {
        byte[] bytes = JsonSerializer.SerializeToUtf8Bytes(owners.Select(owner => new DevelopmentActorStateOwnerObservation(_owners[owner].AppId, Endpoint(_owners[owner].Port), true)).ToArray());
        var options = new FileStreamOptions { Mode = FileMode.CreateNew, Access = FileAccess.Write, Share = FileShare.None, Options = FileOptions.Asynchronous };
        if (!OperatingSystem.IsWindows()) options.UnixCreateMode = UnixFileMode.UserRead | UnixFileMode.UserWrite;
        try
        {
            var output = new FileStream(path, options);
            await using var dispose = output.ConfigureAwait(false);
            await output.WriteAsync(bytes, token).ConfigureAwait(false);
            await output.FlushAsync(token).ConfigureAwait(false);
        }
        catch (IOException exception) { throw new InvalidOperationException("The owned owner-read receipt already exists or is not a new regular file.", exception); }
    }

    private async Task<object> CredentialAsync(IResource resource, CancellationToken token)
    {
        var environment = await EnvironmentAsync(resource, token).ConfigureAwait(false);
        return environment.TryGetValue("DAPR_API_TOKEN", out var value) ? value
            : throw new InvalidOperationException("The actual selected owner has no API credential.");
    }

    private async Task<Dictionary<string, object>> EnvironmentAsync(IResource resource, CancellationToken token)
    {
        Dictionary<string, object> environment = [];
        foreach (var callback in resource.Annotations.OfType<EnvironmentCallbackAnnotation>())
            await callback.Callback(new EnvironmentCallbackContext(_execution, resource, environment, token)).ConfigureAwait(false);
        return environment;
    }

    private static async ValueTask<string?> ValueAsync(object value, CancellationToken token) => value is IValueProvider provider
        ? await provider.GetValueAsync(token).ConfigureAwait(false) : value.ToString();
    private static string Endpoint(int port) => "http://localhost:" + port.ToString(CultureInfo.InvariantCulture) + "/";
    private static IDaprSidecarResource Sidecar(ProjectResource project) => project.Annotations.OfType<DaprSidecarAnnotation>().Single().Sidecar;
}
