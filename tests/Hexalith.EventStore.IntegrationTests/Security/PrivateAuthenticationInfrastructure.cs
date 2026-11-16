using System.Security.Cryptography;
using System.Text.RegularExpressions;
using System.Text.Json.Nodes;
using System.Text.Json;

using Hexalith.EventStore.Aspire;
using Hexalith.EventStore.DomainService;
using Hexalith.EventStore.Contracts.Routing;

using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.Testing;

using CommunityToolkit.Aspire.Hosting.Dapr;

namespace Hexalith.EventStore.IntegrationTests.Security;

/// <summary>Owns the data and actor routing services for one authentication topology.</summary>
internal sealed class PrivateAuthenticationInfrastructure : IAsyncDisposable
{
    private readonly OwnedAuthenticationContainer _redis;
    private DevelopmentRouteCatalogTransport? _transport;
    private string? _transportCustody;
    private readonly PrivateAuthenticationCertificate _certificate;
    private bool _disposed;
    private DevelopmentRouteCatalogComposition? _catalog;
    private readonly DirectoryInfo _directory = Directory.CreateTempSubdirectory("eventstore-private-auth-");
    private readonly DirectoryInfo _discovery = Directory.CreateTempSubdirectory("eventstore-private-discovery-");

    /// <summary>Creates the private certificate for the owned application cohort.</summary>
    internal PrivateAuthenticationInfrastructure() : this(new OwnedAuthenticationContainer()) { }

    /// <summary>Allows focused cleanup retry controls to supply the owned Redis boundary.</summary>
    internal PrivateAuthenticationInfrastructure(OwnedAuthenticationContainer redis)
    {
        _redis = redis;
        _certificate = new PrivateAuthenticationCertificate(_directory.FullName);
    }

    /// <summary>Creates a client retaining TLS chain and hostname validation against this run's certificate.</summary>
    internal HttpClient CreateClient(Uri endpoint) => _certificate.CreateClient(endpoint);

    /// <summary>Gets the private Redis endpoint.</summary>
    internal string RedisEndpoint => _redis.Endpoint;

    /// <summary>Reads generation hints written by the real SDK after its live authority commits.</summary>
    internal RouteCatalogGenerationReadback ReadCommittedGateway()
    {
        DevelopmentRouteCatalogComposition catalog = _catalog ?? throw new InvalidOperationException("The private catalog is unavailable.");
        RouteCatalogGenerationReadback readback = JsonSerializer.Deserialize<RouteCatalogGenerationReadback>(
            File.ReadAllBytes(Path.Combine(catalog.Directory, "eventstore.committed.json")))
            ?? throw new InvalidOperationException("The committed gateway readback is unavailable.");
        if (!readback.Serving || readback.Epoch <= 0 || readback.RootDigest != catalog.RootDigest
            || readback.ActivationBindingDigest is null)
            throw new InvalidOperationException("The private gateway has not committed its complete catalog binding.");
        return readback;
    }

    /// <summary>starts disposable infrastructure before the AppHost is evaluated.</summary>
    internal async Task StartAsync(CancellationToken cancellationToken)
    {
        if (!OperatingSystem.IsLinux()) throw new PlatformNotSupportedException("Private authentication transport requires Linux.");
        _transportCustody = CreateTransportCustodyPath(Environment.GetEnvironmentVariable("HEXALITH_PRIVATE_AUTH_TEMP") ?? Path.GetTempPath());
        int redisPort = OwnedAuthenticationContainer.CandidatePort();
        await _redis.StartAsync("redis:7.4-alpine", null, redisPort, ["redis-server", "--port", redisPort.ToString(System.Globalization.CultureInfo.InvariantCulture)], cancellationToken).ConfigureAwait(false);
        string runtime = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".dapr/bin/daprd");
        _transport = await DevelopmentRouteCatalogTransport.StartAsync(
            new DevelopmentRouteCatalogTransportOptions(_transportCustody, runtime) { RuntimeProfile = DevelopmentRouteCatalogRuntimeProfile.Dapr1191 },
            "Development", cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Selects new custody beneath private temporary space, outside every enclosing checkout.</summary>
    internal static string CreateTransportCustodyPath(string temporaryRoot)
    {
        if (!Path.IsPathFullyQualified(temporaryRoot) || !Directory.Exists(temporaryRoot))
            throw new InvalidOperationException("Private authentication transport requires existing absolute temporary space.");
        for (DirectoryInfo? parent = new(temporaryRoot); parent is not null; parent = parent.Parent)
            if (parent.LinkTarget is not null || File.Exists(Path.Combine(parent.FullName, ".git")) || Directory.Exists(Path.Combine(parent.FullName, ".git")))
                throw new InvalidOperationException("Private authentication temporary space must be outside all checkouts and links.");
        return Path.Combine(temporaryRoot, "authentication-custody-" + Guid.NewGuid().ToString("N"));
    }

    /// <summary>Replaces only fixture-model infrastructure; retained component ACLs and authentication remain authoritative.</summary>
    internal void Configure(IDistributedApplicationTestingBuilder builder)
    {
        if (!OperatingSystem.IsLinux()) throw new PlatformNotSupportedException("Private authentication transport requires Linux.");
        DevelopmentRouteCatalogTransport transport = _transport ?? throw new InvalidOperationException("Private transport has not started.");
        // This dedicated authentication cohort exercises gateway + Counter with the real Keycloak IdP.
        // Other fixed adapter lanes continue to cover Admin, UI and the complete AppHost model.
        foreach (ProjectResource ancillary in builder.Resources.OfType<ProjectResource>().Where(project => project.Name is not ("eventstore" or "sample")).ToArray())
            builder.Resources.Remove(ancillary);
        builder.CreateResourceBuilder<ContainerResource>("security").WithContainerRuntimeArgs(
            "--label", "hexalith.eventstore.authentication-test-owner=" + _redis.Owner);
        ConfigurePrivateRealm(builder.Resources.OfType<ContainerResource>().Single(resource => resource.Name == "security"));
        var replacements = new Dictionary<IDaprComponentResource, IDaprComponentResource>();
        foreach (IDaprComponentResource component in builder.Resources.OfType<IDaprComponentResource>().ToArray())
        {
            string source = Path.Combine(builder.AppHostDirectory, "DaprComponents", component.Name + ".yaml");
            string content = File.ReadAllText(source);
            content = Regex.Replace(content, @"(?m)(^[ \t]*- name: redisHost\r?\n[ \t]*value: )[^\r\n]*", "$1\"" + RedisEndpoint + "\"");
            content = Regex.Replace(content, @"(?m)(^[ \t]*- name: redisPassword\r?\n[ \t]*value: )[^\r\n]*", "$1\"\"");
            string path = Path.Combine(_directory.FullName, component.Name + ".yaml");
            File.WriteAllText(path, content);
            builder.Resources.Remove(component);
            IDaprComponentResource replacement = builder.AddDaprComponent(component.Name, component.Type,
                new DaprComponentOptions { LocalPath = path }).Resource;
            replacements.Add(component, replacement);
        }
        var certificatePassword = builder.AddParameter("private-auth-https-" + Guid.NewGuid().ToString("N"), () => _certificate.Password, secret: true);
        int eventstorePort = OwnedAuthenticationContainer.CandidatePort();
        foreach (ProjectResource project in builder.Resources.OfType<ProjectResource>().ToArray())
        {
            builder.CreateResourceBuilder(project)
                .WithEnvironment("Logging__LogLevel__Default", "Warning")
                .WithEnvironment("Kestrel__Certificates__Default__Path", _certificate.Path)
                .WithEnvironment("Kestrel__Certificates__Default__Password", certificatePassword);
            foreach (WaitAnnotation wait in project.Annotations.OfType<WaitAnnotation>().ToArray())
            {
                if (wait.Resource is IDaprComponentResource original && replacements.TryGetValue(original, out IDaprComponentResource? replacement))
                    project.Annotations[project.Annotations.IndexOf(wait)] = new WaitAnnotation(replacement, wait.WaitType, wait.ExitCode) { WaitBehavior = wait.WaitBehavior };
            }
            foreach (EndpointAnnotation endpoint in project.Annotations.OfType<EndpointAnnotation>()) endpoint.Port = null;
            foreach (DaprSidecarAnnotation annotation in project.Annotations.OfType<DaprSidecarAnnotation>())
            {
                IDaprSidecarResource sidecar = annotation.Sidecar;
                foreach (DaprComponentReferenceAnnotation reference in sidecar.Annotations.OfType<DaprComponentReferenceAnnotation>().ToArray())
                {
                    if (replacements.TryGetValue(reference.Component, out IDaprComponentResource? replacement))
                        sidecar.Annotations[sidecar.Annotations.IndexOf(reference)] = new DaprComponentReferenceAnnotation(replacement);
                }
                foreach (WaitAnnotation wait in sidecar.Annotations.OfType<WaitAnnotation>().ToArray())
                {
                    if (wait.Resource is IDaprComponentResource original && replacements.TryGetValue(original, out IDaprComponentResource? replacement))
                        sidecar.Annotations[sidecar.Annotations.IndexOf(wait)] = new WaitAnnotation(replacement, wait.WaitType, wait.ExitCode) { WaitBehavior = wait.WaitBehavior };
                }
                DaprSidecarOptionsAnnotation options = sidecar.Annotations.OfType<DaprSidecarOptionsAnnotation>().Single();
                string configuration = File.ReadAllText(options.Options.Config
                    ?? throw new InvalidOperationException("Private authentication requires a receiver policy."));
                configuration = Regex.Replace(configuration, @"(?m)(^[ \t]*defaultAction:[ \t]*)allow\b", "${1}deny");
                // mDNS resolves fixed application IDs outside this topology. Keep discovery
                // in the same owned scratch boundary as state and control-plane trust.
                var specification = new Regex(@"(?m)^spec:[ \t]*\r?$");
                if (specification.Matches(configuration).Count != 1)
                    throw new InvalidOperationException("Private discovery requires one explicit Dapr specification.");
                configuration = specification.Replace(configuration,
                    "spec:\n  nameResolution:\n    component: sqlite\n    version: v1\n    configuration:\n      connectionString: "
                    + JsonSerializer.Serialize(Path.Combine(_discovery.FullName, "discovery.sqlite")));
                string closedConfiguration = Path.Combine(_directory.FullName, project.Name + ".closed.yaml");
                File.WriteAllText(closedConfiguration, configuration);
                sidecar.Annotations[sidecar.Annotations.IndexOf(options)] = new DaprSidecarOptionsAnnotation(options.Options with
                {
                    Config = closedConfiguration,
                    DaprHttpPort = project.Name == "eventstore" ? eventstorePort : null,
                    PlacementHostAddress = transport.PlacementAddress,
                    SchedulerHostAddress = transport.SchedulerAddress,
                    LogLevel = "warn",
                });
            }
            builder.CreateResourceBuilder(project).WithEventStoreDevelopmentRouteCatalogTransport(transport);
        }
        ComposeCatalog(builder);
    }

    private static void ConfigurePrivateRealm(ContainerResource keycloak)
    {
        ContainerFileSystemCallbackAnnotation import = keycloak.Annotations.OfType<ContainerFileSystemCallbackAnnotation>()
            .Single(annotation => annotation.DestinationPath == "/opt/keycloak/data/import");
        keycloak.Annotations[keycloak.Annotations.IndexOf(import)] = new ContainerFileSystemCallbackAnnotation
        {
            DestinationPath = import.DestinationPath, DefaultOwner = import.DefaultOwner,
            DefaultGroup = import.DefaultGroup, Umask = import.Umask,
            Callback = async (context, cancellationToken) =>
            {
                ContainerFileSystemItem[] files = (await import.Callback(context, cancellationToken).ConfigureAwait(false)).ToArray();
                ContainerFile file = files.OfType<ContainerFile>().Single(entry => entry.Name == "hexalith-realm.json");
                string contents = file.Contents ?? await File.ReadAllTextAsync(file.SourcePath!, cancellationToken).ConfigureAwait(false);
                file.SourcePath = null;
                file.Contents = InteractiveRealm(contents);
                return files;
            },
        };
    }

    private static string InteractiveRealm(string contents)
    {
        JsonNode realm = JsonNode.Parse(contents)!;
        JsonArray clients = realm["clients"]!.AsArray();
        JsonNode human = clients.Single(client => client!["clientId"]!.GetValue<string>() == "hexalith-eventstore")!;
        JsonNode unmarked = human.DeepClone();
        unmarked["clientId"] = "hexalith-eventstore-unmarked";
        clients.Add(unmarked);
        human["protocolMappers"]!.AsArray().Add(SurfaceMapper("ui"));
        JsonNode workload = clients.Single(client => client!["clientId"]!.GetValue<string>() == "eventstore")!;
        workload["protocolMappers"] = workload["protocolMappers"] ?? new JsonArray();
        workload["protocolMappers"]!.AsArray().Add(SurfaceMapper("service"));
        JsonNode reserved = human.DeepClone();
        reserved["clientId"] = "hexalith-eventstore-reserved-tenant";
        JsonArray mappings = reserved["protocolMappers"]!.AsArray();
        JsonNode tenantMapping = mappings.Single(mapper => mapper!["name"]!.GetValue<string>() == "tenants-mapper")!;
        tenantMapping["config"]!["user.attribute"] = "private-reserved-tenants";
        tenantMapping["config"]!["multivalued"] = "true";
        clients.Add(reserved);
        foreach (JsonNode? user in realm["users"]!.AsArray())
        {
            if (user?["attributes"]?["tenants"] is not JsonArray grants) continue;
            foreach (JsonNode? grant in grants.Where(grant => grant?.GetValue<string>() == "system").ToArray()) grants.Remove(grant);
            if (user["username"]?.GetValue<string>() == "tenant-a-user")
                user["attributes"]!["private-reserved-tenants"] = new JsonArray("tenant-a", "system");
        }
        return realm.ToJsonString();
    }

    private static JsonObject SurfaceMapper(string surface) => new()
        {
            ["name"] = "private-interactive-surface",
            ["protocol"] = "openid-connect",
            ["protocolMapper"] = "oidc-hardcoded-claim-mapper",
            ["consentRequired"] = false,
            ["config"] = new JsonObject
            {
                ["claim.name"] = "eventstore:surface", ["claim.value"] = surface, ["jsonType.label"] = "String",
                ["id.token.claim"] = "false", ["access.token.claim"] = "true", ["userinfo.token.claim"] = "false",
            },
        };

    private void ComposeCatalog(IDistributedApplicationTestingBuilder builder)
    {
        var gateway = builder.CreateResourceBuilder(builder.Resources.OfType<ProjectResource>().Single(project => project.Name == "eventstore"));
        var sample = builder.CreateResourceBuilder(builder.Resources.OfType<ProjectResource>().Single(project => project.Name == "sample"));
        var options = new DevelopmentRouteCatalogCompositionOptions(
            Path.Combine(_directory.FullName, "custody"), "authentication-routing", "authentication-routing", "run-key",
            new Dictionary<string, string> { ["run-key"] = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)) }, "run-secret-map");
        DevelopmentRouteCatalogInvocation[] calls = EventStoreDomainServiceRoutes.Operational
            .Select(route => new DevelopmentRouteCatalogInvocation(gateway, route.Route, ["POST"]))
            .Append(new DevelopmentRouteCatalogInvocation(gateway, "/health", ["GET"])).ToArray();
        _catalog = builder.AddEventStoreDevelopmentRouteCatalog(gateway,
            [new(sample, typeof(Hexalith.EventStore.Sample.Counter.SampleCatalog).Assembly,
                typeof(Hexalith.EventStore.Sample.Counter.CounterAggregate).Assembly)],
            [new(gateway, []) { TechnicalHttp = true }, new(sample, calls)],
            Path.Combine(_directory.FullName, "statestore.yaml"), options);
    }

    /// <inheritdoc/>
    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;
        var failures = new List<Exception>();
        if (OperatingSystem.IsLinux() && _transport is not null)
        {
            try
            {
                await _transport.DisposeAsync().ConfigureAwait(false);
                _transport = null;
            }
            catch (Exception ex) { failures.Add(ex); }
        }
        if (_transport is null && _transportCustody is not null)
        {
            try
            {
                if (Directory.Exists(_transportCustody) || File.Exists(_transportCustody)) Directory.Delete(_transportCustody, recursive: true);
                _transportCustody = null;
            }
            catch (Exception ex) { failures.Add(ex); }
        }
        foreach (OwnedAuthenticationContainer container in new[] { _redis })
        {
            try { await container.DisposeAsync().ConfigureAwait(false); }
            catch (Exception ex) { failures.Add(ex); }
        }
        if (_catalog is not null)
        {
            try
            {
                await _catalog.DisposeAsync().ConfigureAwait(false);
                _catalog = null;
            }
            catch (Exception ex) { failures.Add(ex); }
        }
        if (_transport is null && _catalog is null)
        {
            try { _certificate.Dispose(); }
            catch (Exception ex) { failures.Add(ex); }
            try { if (Directory.Exists(_directory.FullName)) _directory.Delete(recursive: true); }
            catch (Exception ex) { failures.Add(ex); }
            try { if (Directory.Exists(_discovery.FullName)) _discovery.Delete(recursive: true); }
            catch (Exception ex) { failures.Add(ex); }
        }
        if (failures.Count != 0) throw new AggregateException("Private authentication infrastructure cleanup failed.", failures);
        _disposed = true;
    }
}
