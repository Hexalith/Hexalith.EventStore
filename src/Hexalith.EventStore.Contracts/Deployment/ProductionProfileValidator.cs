using System.Text.Json;
using System.Text.RegularExpressions;

namespace Hexalith.EventStore.Contracts.Deployment;

/// <summary>Checks closed structural bindings against a separately composed inventory; never evaluates authority.</summary>
public static partial class ProductionProfileValidator
{
    private static readonly string[] AppendSemantics = [
        "revision-tied-to-read-state", "conditional-durable-revision-advance", "create-once-event-identity",
        "whole-actor-batch-commit-or-reject", "mandatory-raw-and-generic-path-enforcement",
        "exact-original-batch-reconciliation-after-unknown-outcome",
        "governed-erasure-advances-revision-and-rejects-stale-writers",
    ];

    private static readonly string[] RestoreCoverage = [
        "actor-events-revisions-erasure-completion", "admission-and-fence", "global-position-allocation",
        "read-models-checkpoints-command-status", "scheduler-reminders-and-jobs", "placement",
    ];

    /// <summary>Returns deterministic value-free issues and, on success, both profile digests.</summary>
    /// <param name="profile">The complete selected profile candidate.</param>
    /// <param name="context">Expected scoped app/component IDs supplied by the composing inventory.</param>
    /// <returns>A structural result with authorization and qualification unevaluated.</returns>
    public static ProductionProfileValidationResult Validate(ProductionProfile? profile, ProductionProfileValidationContext? context)
    {
        IReadOnlyList<ProductionProfileIssue> issues = Inspect(profile, context, compareInfrastructureDigest: true);
        if (issues.Count != 0 || profile is null)
        {
            return new(false, null, null, issues);
        }
        try
        {
            // Use the codec's exact canonical-byte limit, including default BCL escaping.
            string resolvedDigest = ProductionProfileCodec.ComputeResolvedDigest(profile, context!);
            return new(true, ProductionProfileCodec.InfrastructureDigest(profile), resolvedDigest, []);
        }
        catch (ProductionProfileValidationException exception)
        {
            return new(false, null, null, exception.Issues);
        }
    }

    /// <summary>Collects structural issues, optionally excluding the digest being constructed.</summary>
    internal static IReadOnlyList<ProductionProfileIssue> Inspect(ProductionProfile? profile, ProductionProfileValidationContext? context,
        bool compareInfrastructureDigest)
    {
        var issues = new List<ProductionProfileIssue>();
        void Add(string path, string code) => issues.Add(new(path, code));
        IReadOnlyList<ProductionProfileIssue> Result() => issues.Distinct().OrderBy(i => i.Path, StringComparer.Ordinal)
            .ThenBy(i => i.Code, StringComparer.Ordinal).ToArray();
        if (profile is null)
        {
            Add("$", "missing-document");
            return Result();
        }
        if (context is null)
        {
            Add("$.context", "missing-validation-context");
        }
        else
        {
            CheckExpected(context.ExpectedApplicationIds, "$.context.expectedApplicationIds");
            CheckExpected(context.ExpectedComponentIds, "$.context.expectedComponentIds");
        }
        try
        {
            CheckShape(JsonSerializer.SerializeToElement(profile, ProductionProfileCodec.Options), "$", "");
        }
        catch (JsonException)
        {
            Add("$", "invalid-structure");
        }
        // Shape issues must be resolved before dereferencing any selected field or collection.
        if (issues.Any(i => !i.Path.StartsWith("$.context", StringComparison.Ordinal)))
        {
            return Result();
        }

        if (profile.SchemaVersion != 1) Add("$.schemaVersion", "unsupported-version");
        if (profile.Purpose != "eventstore-resolved-production-profile") Add("$.purpose", "unsupported-purpose");
        var apps = Index(profile.Applications, a => a.Id, "$.applications");
        var components = Index(profile.Components, c => c.Id, "$.components");
        var providers = Index(profile.Infrastructure.Providers, p => p.Id, "$.infrastructure.providers");
        _ = Index(profile.Infrastructure.Tools, t => t.Id, "$.infrastructure.tools");
        var artifacts = Index(profile.Release.Artifacts, a => a.Id, "$.release.artifacts");
        var targets = Index(profile.Environment.PhysicalTargets, t => t.Id, "$.environment.physicalTargets");
        var credentials = Index(profile.Environment.Credentials, c => c.Id, "$.environment.credentials");
        _ = Index(profile.Environment.Endpoints, e => e.Id, "$.environment.endpoints");
        var evidence = Index(profile.Evidence, e => e.Id, "$.evidence");
        var units = Index(profile.Restore.Units, u => u.Id, "$.restore.units");
        _ = Index(profile.Protection.Writers, w => w.Id, "$.protection.writers");
        var tuple = Index(profile.Protection.ComponentVersions, c => c.Id, "$.protection.componentVersions");
        Require(profile.Applications.Count > 0, "$.applications", "missing-inventory");
        Require(profile.Components.Count > 0, "$.components", "missing-inventory");
        Require(providers.Count > 0, "$.infrastructure.providers", "missing-inventory");
        Require(profile.Infrastructure.Tools.Count > 0, "$.infrastructure.tools", "missing-tool-pins");
        Require(artifacts.Count > 0, "$.release.artifacts", "missing-inventory");
        Require(evidence.Count > 0, "$.evidence", "missing-evidence");
        Require(credentials.Count > 0, "$.environment.credentials", "missing-credential-references");
        Require(profile.Environment.KeyGenerations.Count > 0, "$.environment.keyGenerations", "missing-key-generations");
        if (context is not null)
        {
            Coverage(profile.Applications.Select(a => a.Id).ToArray(), context.ExpectedApplicationIds, "$.applications");
            Coverage(profile.Components.Select(c => c.Id).ToArray(), context.ExpectedComponentIds, "$.components");
        }
        UniqueScope(profile.Applications.Select(a => (a.ClusterId, a.Namespace, a.AppId)), "$.applications");
        UniqueScope(profile.Components.Select(c => (c.ClusterId, c.Namespace, c.Name)), "$.components");

        ProductionProfileTemplate template = profile.Template;
        Require(template.SidecarMode == "per-application", "$.template.sidecarMode", "unsupported-sidecar-mode");
        Require(template.ActorStateType == "state.postgresql", "$.template.actorStateType", "unsupported-actor-state-type");
        Require(template.ActorStateVersion == "v1", "$.template.actorStateVersion", "unsupported-component-version");
        Require(template.ActorStateName == "statestore", "$.template.actorStateName", "invalid-actor-component-name");
        Require(template.Oq8Profile == "oq8-postgresql-v1", "$.template.oq8Profile", "unsupported-oq8-profile");
        Coverage(template.AppendSemantics, AppendSemantics, "$.template.appendSemantics");
        Coverage(template.ExcludedProfiles, ["Development/test", "Redis including SignalR backplane", "Cosmos template alternatives"], "$.template.excludedProfiles");
        Require(profile.Release.TemplateId == template.Id, "$.release.templateId", "template-binding-mismatch");
        Require(profile.Release.Artifacts.Any(a => a.Kind == "composed-image" && a.Digest == profile.Release.ComposedHostDigest),
            "$.release.composedHostDigest", "composed-host-artifact-mismatch");
        Require(profile.Release.QualifiedInfrastructureSetIds.Count > 0, "$.release.qualifiedInfrastructureSetIds", "missing-qualified-set-references");
        bool runtimeValid = Version.TryParse(profile.Release.RuntimeVersion, out Version? runtime) && ThreePartVersion().IsMatch(profile.Release.RuntimeVersion);
        bool floorValid = Version.TryParse(template.MinimumRuntimeVersion, out Version? floor) && ThreePartVersion().IsMatch(template.MinimumRuntimeVersion);
        Require(runtimeValid && floorValid && floor! >= new Version(1, 18, 3) && runtime! >= floor,
            "$.release.runtimeVersion", "runtime-floor-mismatch");
        Require(runtimeValid && runtime!.ToString(2) == template.RuntimeMinor, "$.template.runtimeMinor", "runtime-minor-mismatch");
        Require(profile.Release.SidecarPatchVersion == profile.Release.RuntimeVersion, "$.release.sidecarPatchVersion", "runtime-binding-mismatch");
        Require(profile.Environment.ControlPlaneDropAllCapabilities, "$.environment.controlPlaneDropAllCapabilities", "required-capability-drop");
        Require(profile.Environment.SecretContractDigest == template.SecretContractDigest, "$.environment.secretContractDigest", "secret-contract-binding-mismatch");
        bool baoFloorValid = ThreePartVersion().IsMatch(template.OpenBaoServerFloor) &&
            Version.TryParse(template.OpenBaoServerFloor, out Version? declaredBaoFloor) && MeetsOpenBaoFloor(declaredBaoFloor);
        Require(baoFloorValid, "$.template.openBaoServerFloor", "server-floor-mismatch");
        Require(ThreePartVersion().IsMatch(profile.Environment.OpenBaoServerVersion) &&
            Version.TryParse(profile.Environment.OpenBaoServerVersion, out Version? bao) && MeetsOpenBaoFloor(bao) &&
            Version.TryParse(template.OpenBaoServerFloor, out Version? baoFloor) && baoFloorValid && bao >= baoFloor,
            "$.environment.openBaoServerVersion", "server-floor-mismatch");
        Require(profile.Catalogs.Generation > 0, "$.catalogs.generation", "invalid-generation");
        Require(profile.Catalogs.ContentDigest == profile.Release.CatalogContentDigest, "$.catalogs.contentDigest", "catalog-binding-mismatch");

        ProductionProfileTopology topology = profile.Infrastructure.Topology;
        Require(topology.DeploymentMode == "self-managed-kubernetes", "$.infrastructure.topology.deploymentMode", "unsupported-deployment-mode");
        Link(topology.ManagementProviderId, providers, "$.infrastructure.topology.managementProviderId");
        Link(topology.WorkloadProviderId, providers, "$.infrastructure.topology.workloadProviderId");
        Link(topology.RancherProviderId, providers, "$.infrastructure.topology.rancherProviderId");
        Require(providers.TryGetValue(topology.ManagementProviderId, out ProductionProfileProvider? managementProvider) &&
            managementProvider.Provider == "Kubernetes" && managementProvider.Roles.Contains("management", StringComparer.Ordinal),
            "$.infrastructure.topology.managementProviderId", "topology-provider-binding-mismatch");
        Require(providers.TryGetValue(topology.WorkloadProviderId, out ProductionProfileProvider? workloadProvider) &&
            workloadProvider.Provider == "Kubernetes" && workloadProvider.Roles.Contains("workload", StringComparer.Ordinal),
            "$.infrastructure.topology.workloadProviderId", "topology-provider-binding-mismatch");
        Require(providers.TryGetValue(topology.RancherProviderId, out ProductionProfileProvider? rancherProvider) &&
            rancherProvider.Provider == "Rancher" && rancherProvider.Roles.Contains("management", StringComparer.Ordinal),
            "$.infrastructure.topology.rancherProviderId", "topology-provider-binding-mismatch");
        Require(profile.Environment.ClusterIds.Contains(topology.ManagementClusterId, StringComparer.Ordinal), "$.infrastructure.topology.managementClusterId", "dangling-cluster-reference");
        Require(profile.Environment.ClusterIds.Contains(topology.WorkloadClusterId, StringComparer.Ordinal), "$.infrastructure.topology.workloadClusterId", "dangling-cluster-reference");
        Require(topology.BackupComponentIds.Count > 0, "$.infrastructure.topology.backupComponentIds", "missing-backup-components");
        Links(topology.BackupComponentIds, components, "$.infrastructure.topology.backupComponentIds");
        Link(profile.Environment.OpenBaoComponentId, components, "$.environment.openBaoComponentId");
        Link(profile.Environment.BrokerComponentId, components, "$.environment.brokerComponentId");
        Require(components.TryGetValue(profile.Environment.OpenBaoComponentId, out ProductionProfileComponent? secretComponent)
            && secretComponent.Name == "openbao" && secretComponent.Type == "secretstores.hashicorp.vault",
            "$.environment.openBaoComponentId", "secret-component-binding-mismatch");
        Require(secretComponent is not null && providers.TryGetValue(secretComponent.ProviderId, out ProductionProfileProvider? baoProvider)
            && baoProvider.Provider == "OpenBao" && baoProvider.Roles.Contains("secrets", StringComparer.Ordinal)
            && baoProvider.Version == profile.Environment.OpenBaoServerVersion,
            "$.environment.openBaoServerVersion", "secret-provider-binding-mismatch");
        Require(components.TryGetValue(profile.Environment.BrokerComponentId, out ProductionProfileComponent? brokerComponent)
            && brokerComponent.Type.StartsWith("pubsub.", StringComparison.Ordinal),
            "$.environment.brokerComponentId", "broker-component-binding-mismatch");
        Require(profile.Environment.SchedulerComponentIds.Count > 0, "$.environment.schedulerComponentIds", "missing-scheduler-components");
        Require(profile.Environment.PlacementComponentIds.Count > 0, "$.environment.placementComponentIds", "missing-placement-components");
        Links(profile.Environment.SchedulerComponentIds, components, "$.environment.schedulerComponentIds");
        Links(profile.Environment.PlacementComponentIds, components, "$.environment.placementComponentIds");
        foreach (string id in profile.Environment.SchedulerComponentIds)
            Require(components.TryGetValue(id, out ProductionProfileComponent? component) && component.Roles.Contains("scheduler", StringComparer.Ordinal),
                "$.environment.schedulerComponentIds", "scheduler-component-binding-mismatch");
        foreach (string id in profile.Environment.PlacementComponentIds)
            Require(components.TryGetValue(id, out ProductionProfileComponent? component) && component.Roles.Contains("placement", StringComparer.Ordinal),
                "$.environment.placementComponentIds", "placement-component-binding-mismatch");

        for (int i = 0; i < profile.Applications.Count; i++)
        {
            ProductionProfileApplication app = profile.Applications[i];
            string path = $"$.applications[{i}]";
            Require(profile.Environment.ClusterIds.Contains(app.ClusterId, StringComparer.Ordinal), path + ".clusterId", "dangling-cluster-reference");
            Link(app.ArtifactId, artifacts, path + ".artifactId");
            Require(artifacts.TryGetValue(app.ArtifactId, out ProductionProfileArtifact? artifact) &&
                artifact.Kind is "composed-image" or "module-image" or "extension-image" && artifact.Digest == app.ImageDigest,
                path + ".imageDigest", "application-artifact-binding-mismatch");
            Require(app.SidecarImageDigest == profile.Release.RuntimeImageDigest, path + ".sidecarImageDigest", "runtime-binding-mismatch");
            Require(app.DaprApiTokenEnabled && app.AppApiTokenEnabled, path + ".daprApiTokenEnabled", "required-api-tokens");
            CheckAppCredential(app.DaprApiTokenCredentialId, app.Id, path + ".daprApiTokenCredentialId");
            CheckAppCredential(app.AppApiTokenCredentialId, app.Id, path + ".appApiTokenCredentialId");
            Links(app.ComponentIds, components, path + ".componentIds");
            for (int j = 0; j < app.ComponentIds.Count; j++)
                if (components.TryGetValue(app.ComponentIds[j], out ProductionProfileComponent? component))
                    Require(component.AppScopeIds.Contains(app.Id, StringComparer.Ordinal), path + $".componentIds[{j}]", "app-component-scope-mismatch");
            ProductionProfileComponent[] actor = profile.Components.Where(c => c.ActorStateStore && c.AppScopeIds.Contains(app.Id, StringComparer.Ordinal)).ToArray();
            Require(app.HostsActors ? app.ActorTypes.Count > 0 && actor.Length == 1 : app.ActorTypes.Count == 0 && actor.Length == 0,
                path + ".hostsActors", "actor-component-coverage-mismatch");
            EvidenceLinks(app.EvidenceIds, path + ".evidenceIds",
                [app.ImageDigest, app.SidecarImageDigest, app.EffectiveConfigurationDigest, app.AclDigest, app.ResiliencyDigest],
                ["application"], appId: app.Id);
        }
        for (int i = 0; i < profile.Components.Count; i++)
        {
            ProductionProfileComponent component = profile.Components[i];
            string path = $"$.components[{i}]";
            Link(component.ProviderId, providers, path + ".providerId");
            Require(profile.Environment.ClusterIds.Contains(component.ClusterId, StringComparer.Ordinal), path + ".clusterId", "dangling-cluster-reference");
            Links(component.AppScopeIds, apps, path + ".appScopeIds");
            Links(component.PhysicalTargetIds, targets, path + ".physicalTargetIds");
            Links(component.CredentialReferenceIds, credentials, path + ".credentialReferenceIds");
            for (int j = 0; j < component.AppScopeIds.Count; j++)
                if (apps.TryGetValue(component.AppScopeIds[j], out ProductionProfileApplication? app))
                    Require(app.ComponentIds.Contains(component.Id, StringComparer.Ordinal) && app.ClusterId == component.ClusterId && app.Namespace == component.Namespace,
                        path + $".appScopeIds[{j}]", "app-component-scope-mismatch");
            foreach (string id in component.PhysicalTargetIds)
                if (targets.TryGetValue(id, out ProductionProfilePhysicalTarget? target))
                    Require(target.ProviderId == component.ProviderId, path + ".physicalTargetIds", "provider-target-mismatch");
            foreach (string id in component.CredentialReferenceIds)
                if (credentials.TryGetValue(id, out ProductionProfileCredentialReference? credential))
                    Require(credential.ComponentIds.Contains(component.Id, StringComparer.Ordinal), path + ".credentialReferenceIds", "credential-component-mismatch");
            Require(component.Roles.Count > 0, path + ".roles", "missing-role-coverage");
            Require(!component.Type.StartsWith("state.", StringComparison.Ordinal) || component.Stateful,
                path + ".stateful", "state-store-must-be-stateful");
            Require(component.SupportSources.Count > 0 && component.AdvisorySources.Count > 0 && component.RenewalOwners.Count > 0,
                path + ".supportSources", "missing-currency-inputs");
            if (component.ActorStateStore)
            {
                Require(component.Stateful && component.AppScopeIds.Count == 1 && component.PhysicalTargetIds.Count == 1,
                    path + ".actorStateStore", "actor-isolation-required");
                Require((!component.AppScopeIds.Contains(profile.Freshness.EventStoreAppId, StringComparer.Ordinal) || component.Name == template.ActorStateName)
                    && component.Type == template.ActorStateType && component.Version == template.ActorStateVersion,
                    path + ".type", "actor-contract-mismatch");
                if (component.AppScopeIds.Count == 1 && apps.TryGetValue(component.AppScopeIds[0], out ProductionProfileApplication? host))
                    Require(host.HostsActors, path + ".appScopeIds", "actor-host-required");
                foreach (string id in component.CredentialReferenceIds)
                    if (credentials.TryGetValue(id, out ProductionProfileCredentialReference? credential))
                        Require(credential.ConsumerAppIds.SequenceEqual(component.AppScopeIds) && credential.ComponentIds.SequenceEqual(new[] { component.Id }),
                            path + ".credentialReferenceIds", "actor-credential-isolation-required");
                Require(component.CredentialReferenceIds.Count > 0, path + ".credentialReferenceIds", "missing-credential-references");
                Require(tuple.TryGetValue(component.Id, out ProductionProfileComponentVersion? pin) && pin.Version == component.Version,
                    path + ".version", "protection-tuple-mismatch");
                Require(profile.Protection.Writers.Any(w => w.ComponentId == component.Id), path + ".id", "missing-writer-inventory");
                foreach (ProductionProfileComponent other in profile.Components.Where(c => c.Id != component.Id))
                    foreach (string own in component.PhysicalTargetIds)
                        foreach (string otherId in other.PhysicalTargetIds)
                            if (targets.TryGetValue(own, out ProductionProfilePhysicalTarget? left) && targets.TryGetValue(otherId, out ProductionProfilePhysicalTarget? right))
                                Require(!Overlaps(left, right), path + ".physicalTargetIds", "actor-physical-target-reused");
            }
            EvidenceLinks(component.EvidenceIds, path + ".evidenceIds",
                [component.LoadedIdentityDigest, component.AclDigest, component.ResiliencyDigest, component.DataMigrationDigest,
                    .. providers.TryGetValue(component.ProviderId, out ProductionProfileProvider? provider) ? new[] { provider.ImmutableDigest } : []],
                ["component"], componentId: component.Id);
        }
        for (int i = 0; i < profile.Environment.PhysicalTargets.Count; i++)
        {
            ProductionProfilePhysicalTarget target = profile.Environment.PhysicalTargets[i];
            string path = $"$.environment.physicalTargets[{i}]";
            Link(target.ProviderId, providers, path + ".providerId");
            Require(target.Port is > 0 and <= 65535, path + ".port", "invalid-port");
            Require(HostName().IsMatch(target.Host), path + ".host", "invalid-public-host");
            Require(profile.Components.Any(c => c.PhysicalTargetIds.Contains(target.Id, StringComparer.Ordinal)), path + ".id", "unbound-physical-target");
            Require(profile.Evidence.Any(e => e.PhysicalTargetIds.Contains(target.Id, StringComparer.Ordinal)
                && e.Coverage.Contains("physical-target", StringComparer.Ordinal)
                && providers.TryGetValue(target.ProviderId, out ProductionProfileProvider? provider)
                && e.SubjectDigests.Contains(provider.ImmutableDigest, StringComparer.Ordinal)), path + ".id", "missing-target-evidence-association");
        }
        for (int i = 0; i < profile.Environment.Endpoints.Count; i++)
        {
            ProductionProfileEndpoint endpoint = profile.Environment.Endpoints[i];
            string path = $"$.environment.endpoints[{i}]";
            Link(endpoint.ComponentId, components, path + ".componentId");
            Require(Uri.TryCreate(endpoint.Address, UriKind.Absolute, out Uri? uri) && uri.Host.Length > 0 &&
                uri.UserInfo.Length == 0 && uri.Query.Length == 0 && uri.Fragment.Length == 0 &&
                uri.Scheme is "https" or "grpcs" or "postgresql" or "amqps" or "rediss",
                path + ".address", "credential-free-endpoint-required");
        }
        for (int i = 0; i < profile.Environment.Credentials.Count; i++)
        {
            ProductionProfileCredentialReference credential = profile.Environment.Credentials[i];
            string path = $"$.environment.credentials[{i}]";
            Require(credential.Store == "openbao" && ReferenceName().IsMatch(credential.LogicalName) && ReferenceName().IsMatch(credential.MapKey)
                && Identifier().IsMatch(credential.Generation), path, "credential-reference-required");
            Links(credential.ConsumerAppIds, apps, path + ".consumerAppIds");
            Links(credential.ComponentIds, components, path + ".componentIds");
            for (int j = 0; j < credential.ComponentIds.Count; j++)
                if (components.TryGetValue(credential.ComponentIds[j], out ProductionProfileComponent? component))
                {
                    Require(component.CredentialReferenceIds.Contains(credential.Id, StringComparer.Ordinal),
                        path + $".componentIds[{j}]", "credential-component-mismatch");
                    if (component.ActorStateStore)
                        Require(credential.ComponentIds.Count == 1 && new HashSet<string>(credential.ConsumerAppIds, StringComparer.Ordinal).SetEquals(component.AppScopeIds),
                            path + $".componentIds[{j}]", "actor-credential-isolation-required");
                }
            bool actorMaterial = credential.ComponentIds.Any(id => components.TryGetValue(id, out ProductionProfileComponent? component) && component.ActorStateStore)
                || profile.Components.Any(c => c.ActorStateStore && c.CredentialReferenceIds.Contains(credential.Id, StringComparer.Ordinal));
            if (actorMaterial)
                Require(!profile.Environment.Credentials.Any(other => other.Id != credential.Id &&
                    other.Store == credential.Store && other.LogicalName == credential.LogicalName && other.MapKey == credential.MapKey && other.Generation == credential.Generation),
                    path, "actor-credential-alias");
            Require(credential.ConsumerAppIds.Count + credential.ComponentIds.Count > 0, path, "unbound-credential-reference");
        }

        ProductionProfileProtection protection = profile.Protection;
        Require(protection.Mode == "mandatory-actor-transaction" && protection.MandatoryGuard, "$.protection.mode", "mandatory-protection-required");
        Require(protection.ActorOwnership == "AggregateActor/IActorStateManager", "$.protection.actorOwnership", "actor-ownership-required");
        Require(protection.SdkVersion == profile.Release.SdkVersion && protection.RuntimeVersion == profile.Release.RuntimeVersion,
            "$.protection.runtimeVersion", "protection-tuple-mismatch");
        Coverage(protection.Semantics, AppendSemantics, "$.protection.semantics");
        for (int i = 0; i < protection.ComponentVersions.Count; i++)
        {
            ProductionProfileComponentVersion pin = protection.ComponentVersions[i];
            Require(components.TryGetValue(pin.Id, out ProductionProfileComponent? component) && component.ActorStateStore && component.Version == pin.Version,
                $"$.protection.componentVersions[{i}]", "protection-tuple-mismatch");
        }
        for (int i = 0; i < protection.Writers.Count; i++)
        {
            ProductionProfileWriter writer = protection.Writers[i];
            string path = $"$.protection.writers[{i}]";
            Link(writer.AppId, apps, path + ".appId");
            Require(components.TryGetValue(writer.ComponentId, out ProductionProfileComponent? component) && component.ActorStateStore
                && component.AppScopeIds.Contains(writer.AppId, StringComparer.Ordinal), path + ".componentId", "writer-binding-mismatch");
            Require(writer.ErasureContractDigest == protection.AuthorizedErasureContractDigest, path + ".erasureContractDigest", "erasure-binding-mismatch");
        }
        Link(profile.Issuer.ComponentId, components, "$.issuer.componentId");
        Require(profile.Issuer.ContractDigest == template.IssuerContractDigest, "$.issuer.contractDigest", "issuer-binding-mismatch");
        Link(profile.Issuer.BootstrapCredentialReferenceId, credentials, "$.issuer.bootstrapCredentialReferenceId");
        Require(credentials.TryGetValue(profile.Issuer.BootstrapCredentialReferenceId, out ProductionProfileCredentialReference? bootstrap)
            && bootstrap.ComponentIds.Contains(profile.Issuer.ComponentId, StringComparer.Ordinal),
            "$.issuer.bootstrapCredentialReferenceId", "issuer-bootstrap-binding-mismatch");
        Require(profile.Issuer.ClientCredentialReferenceIds.Count > 0, "$.issuer.clientCredentialReferenceIds", "missing-credential-references");
        Links(profile.Issuer.ClientCredentialReferenceIds, credentials, "$.issuer.clientCredentialReferenceIds");
        Link(profile.Freshness.EventStoreAppId, apps, "$.freshness.eventStoreAppId");
        Require(apps.TryGetValue(profile.Freshness.EventStoreAppId, out ProductionProfileApplication? eventStore) && eventStore.HostsActors,
            "$.freshness.eventStoreAppId", "eventstore-actor-host-required");
        Require(profile.Freshness.Replicas > 0 && (profile.Freshness.Replicas == 1 || !profile.Freshness.MultiReplicaFreshnessClaim),
            "$.freshness.multiReplicaFreshnessClaim", "unsupported-replica-freshness-claim");

        Require(units.Count > 0, "$.restore.units", "missing-restore-units");
        Coverage(profile.Restore.Order, units.Keys.ToArray(), "$.restore.order");
        Coverage(profile.Restore.ForbiddenIndependentRestores, units.Keys.ToArray(), "$.restore.forbiddenIndependentRestores");
        Coverage(profile.Restore.Units.SelectMany(u => u.Coverage).Distinct(StringComparer.Ordinal).ToArray(), RestoreCoverage, "$.restore.units");
        for (int i = 0; i < profile.Restore.Units.Count; i++)
        {
            ProductionProfileRestoreUnit unit = profile.Restore.Units[i];
            Links(unit.ComponentIds, components, $"$.restore.units[{i}].componentIds");
            Require(unit.ComponentIds.Count > 0 && unit.Coverage.Count > 0, $"$.restore.units[{i}]", "incomplete-restore-unit");
        }
        for (int i = 0; i < profile.Components.Count; i++)
            if (profile.Components[i].Stateful || profile.Components[i].Type.StartsWith("state.", StringComparison.Ordinal)
                || profile.Environment.SchedulerComponentIds.Contains(profile.Components[i].Id, StringComparer.Ordinal)
                || profile.Environment.PlacementComponentIds.Contains(profile.Components[i].Id, StringComparer.Ordinal))
                Require(profile.Restore.Units.Any(u => u.ComponentIds.Contains(profile.Components[i].Id, StringComparer.Ordinal)),
                    $"$.components[{i}].id", "missing-restore-component");
        EvidenceLinks([profile.Release.CliCompatibilityEvidenceId], "$.release.cliCompatibilityEvidenceId",
            [profile.Release.RuntimeImageDigest], ["runtime", "cli-compatibility"]);
        EvidenceLinks(profile.Release.EvidenceIds, "$.release.evidenceIds",
            [profile.Release.ComposedHostDigest, profile.Release.HelmPackageDigest, profile.Release.CatalogContentDigest,
                profile.Release.McpCliPackageDigest, profile.Release.RuntimeImageDigest, profile.Release.RealmContractDigest,
                profile.Release.RecoveryContractDigest, profile.Release.CheckSuiteContractDigest,
                .. profile.Release.ExtensionDigests, .. profile.Release.Artifacts.Select(a => a.Digest)], ["release"]);
        EvidenceLinks(profile.Environment.BrokerEvidenceIds, "$.environment.brokerEvidenceIds",
            [profile.Environment.BrokerContractDigest,
                .. brokerComponent is not null ? new[] { brokerComponent.LoadedIdentityDigest } : []],
            ["broker"], componentId: profile.Environment.BrokerComponentId);
        EvidenceLinks(profile.Environment.SchedulerPlacementEvidenceIds, "$.environment.schedulerPlacementEvidenceIds",
            [profile.Environment.SchedulerConfigurationDigest, profile.Environment.PlacementConfigurationDigest],
            ["scheduler-reminders-and-jobs", "placement"],
            componentIds: [.. profile.Environment.SchedulerComponentIds, .. profile.Environment.PlacementComponentIds]);
        EvidenceLinks(profile.Catalogs.EvidenceIds, "$.catalogs.evidenceIds",
            [profile.Catalogs.RootDigest, profile.Catalogs.RouteDigest, profile.Catalogs.IdempotencyDigest,
                profile.Catalogs.ContentDigest, profile.Catalogs.Ad34InventoryDigest, profile.Catalogs.Ad34RegisterDigest], ["catalogs", "ad34"]);
        EvidenceLinks(protection.EvidenceIds, "$.protection.evidenceIds",
            [protection.TransactionSemanticsDigest, protection.PlacementFailoverDigest, protection.AuthorizedErasureContractDigest,
                .. protection.Writers.Select(w => w.ImplementationDigest)], ["append-erasure", .. protection.Semantics]);
        EvidenceLinks(profile.Issuer.EvidenceIds, "$.issuer.evidenceIds",
            [profile.Issuer.ContractDigest, profile.Issuer.AssertionConfigurationDigest], ["issuer"], componentId: profile.Issuer.ComponentId);
        EvidenceLinks(profile.Freshness.EvidenceIds, "$.freshness.evidenceIds",
            [profile.Freshness.PostureDigest], ["freshness"], appId: profile.Freshness.EventStoreAppId);
        EvidenceLinks(profile.Restore.EvidenceIds, "$.restore.evidenceIds",
            [profile.Restore.AcceptedPostureDigest], ["restore", .. RestoreCoverage],
            componentIds: profile.Restore.Units.SelectMany(u => u.ComponentIds).Distinct(StringComparer.Ordinal).ToArray());
        for (int i = 0; i < profile.Evidence.Count; i++)
        {
            ProductionProfileEvidence reference = profile.Evidence[i];
            string path = $"$.evidence[{i}]";
            Links(reference.AppIds, apps, path + ".appIds");
            Links(reference.ComponentIds, components, path + ".componentIds");
            Links(reference.PhysicalTargetIds, targets, path + ".physicalTargetIds");
            Require(reference.SubjectDigests.Count > 0 && reference.Coverage.Count > 0, path, "incomplete-evidence-association");
        }
        if (compareInfrastructureDigest)
            Require(profile.InfrastructureDigest == ProductionProfileCodec.InfrastructureDigest(profile), "$.infrastructureDigest", "infrastructure-digest-mismatch");
        return Result();

        void Require(bool condition, string path, string code) { if (!condition) Add(path, code); }
        void CheckExpected(IReadOnlyList<string>? ids, string path)
        {
            if (ids is null || ids.Count == 0) { Add(path, "missing-expected-inventory"); return; }
            if (ids.Any(id => id is null || !Identifier().IsMatch(id))) Add(path, "invalid-expected-identity");
            if (ids.Distinct(StringComparer.Ordinal).Count() != ids.Count) Add(path, "duplicate-expected-identity");
        }
        void CheckShape(JsonElement element, string path, string field)
        {
            if (!compareInfrastructureDigest && path == "$.infrastructureDigest") return;
            if (element.ValueKind == JsonValueKind.Null) { Add(path, "missing-selection"); return; }
            if (element.ValueKind == JsonValueKind.Object)
            {
                foreach (JsonProperty property in element.EnumerateObject()) CheckShape(property.Value, path + "." + property.Name, property.Name);
            }
            else if (element.ValueKind == JsonValueKind.Array)
            {
                var members = new HashSet<string>(StringComparer.Ordinal);
                int index = 0;
                foreach (JsonElement item in element.EnumerateArray())
                {
                    if (item.ValueKind == JsonValueKind.String && !members.Add(item.GetString()!)) Add($"{path}[{index}]", "duplicate-set-member");
                    CheckShape(item, $"{path}[{index++}]", field);
                }
            }
            else if (element.ValueKind == JsonValueKind.String)
            {
                string value = element.GetString()!;
                if (string.IsNullOrWhiteSpace(value) || value.Length > 4096 || value.Any(char.IsControl)) Add(path, "missing-or-invalid-text");
                else if ((field.EndsWith("Digest", StringComparison.Ordinal) || field.EndsWith("Digests", StringComparison.Ordinal)) && !Digest().IsMatch(value)) Add(path, "invalid-digest");
                else if ((field is "id" || field.EndsWith("Id", StringComparison.Ordinal) || field.EndsWith("Ids", StringComparison.Ordinal)) && !Identifier().IsMatch(value)) Add(path, "invalid-identity");
            }
        }
        Dictionary<string, T> Index<T>(IReadOnlyList<T> entries, Func<T, string> id, string path)
        {
            var map = new Dictionary<string, T>(StringComparer.Ordinal);
            for (int i = 0; i < entries.Count; i++) if (!map.TryAdd(id(entries[i]), entries[i])) Add($"{path}[{i}].id", "duplicate-identity");
            return map;
        }
        void Link<T>(string id, Dictionary<string, T> map, string path) { if (!map.ContainsKey(id)) Add(path, "dangling-reference"); }
        void Links<T>(IReadOnlyList<string> ids, Dictionary<string, T> map, string path)
        {
            for (int i = 0; i < ids.Count; i++) Link(ids[i], map, $"{path}[{i}]");
        }
        void Coverage(IReadOnlyList<string> actual, IReadOnlyList<string>? expected, string path)
        {
            if (expected is null || !new HashSet<string>(actual, StringComparer.Ordinal).SetEquals(expected)) Add(path, "inventory-coverage-mismatch");
        }
        void UniqueScope(IEnumerable<(string, string, string)> scopes, string path)
        {
            var seen = new HashSet<(string, string, string)>();
            int i = 0;
            foreach (var scope in scopes) { if (!seen.Add(scope)) Add($"{path}[{i}]", "duplicate-scoped-identity"); i++; }
        }
        void CheckAppCredential(string id, string appId, string path)
        {
            Link(id, credentials, path);
            if (credentials.TryGetValue(id, out ProductionProfileCredentialReference? credential))
                Require(credential.ConsumerAppIds.Contains(appId, StringComparer.Ordinal), path, "credential-app-mismatch");
        }
        void EvidenceLinks(IReadOnlyList<string> ids, string path, IReadOnlyList<string> subjects, IReadOnlyList<string> coverage,
            string? appId = null, string? componentId = null, IReadOnlyList<string>? componentIds = null)
        {
            Require(ids.Count > 0, path, "missing-evidence-references");
            Links(ids, evidence, path);
            ProductionProfileEvidence[] references = ids.Where(evidence.ContainsKey).Select(id => evidence[id]).ToArray();
            Require(subjects.All(subject => references.Any(reference => reference.SubjectDigests.Contains(subject, StringComparer.Ordinal))),
                path, "evidence-subject-mismatch");
            Require(coverage.All(category => references.Any(reference => reference.Coverage.Contains(category, StringComparer.Ordinal))),
                path, "evidence-coverage-mismatch");
            if (componentIds is not null)
                Require(componentIds.All(id => references.Any(reference => reference.ComponentIds.Contains(id, StringComparer.Ordinal))),
                    path, "evidence-coverage-mismatch");
            for (int i = 0; i < ids.Count; i++)
                if (evidence.TryGetValue(ids[i], out ProductionProfileEvidence? reference))
                {
                    Require((appId is null || reference.AppIds.Contains(appId, StringComparer.Ordinal)) &&
                        (componentId is null || reference.ComponentIds.Contains(componentId, StringComparer.Ordinal)) &&
                        reference.Coverage.Any(category => coverage.Contains(category, StringComparer.Ordinal)),
                        $"{path}[{i}]", "evidence-coverage-mismatch");
                    Require(reference.SubjectDigests.Any(subject => subjects.Contains(subject, StringComparer.Ordinal)),
                        $"{path}[{i}]", "evidence-subject-mismatch");
                }
        }
    }

    private static bool Overlaps(ProductionProfilePhysicalTarget left, ProductionProfilePhysicalTarget right) =>
        StringComparer.OrdinalIgnoreCase.Equals(left.Host.TrimEnd('.'), right.Host.TrimEnd('.')) && left.Port == right.Port && left.Database == right.Database &&
        (left.TableName == right.TableName || left.TableName == right.MetadataTableName || left.MetadataTableName == right.TableName || left.MetadataTableName == right.MetadataTableName);

    private static bool MeetsOpenBaoFloor(Version version) => version >= new Version(2, 7, 1) ||
        (version.Major == 2 && version.Minor == 6 && version >= new Version(2, 6, 4));

    [GeneratedRegex("\\A[0-9a-f]{64}\\z", RegexOptions.CultureInvariant)]
    private static partial Regex Digest();

    [GeneratedRegex("\\A[A-Za-z0-9][A-Za-z0-9._:/-]{0,127}\\z", RegexOptions.CultureInvariant)]
    private static partial Regex Identifier();

    [GeneratedRegex("\\A[A-Za-z0-9][A-Za-z0-9._/-]{0,127}\\z", RegexOptions.CultureInvariant)]
    private static partial Regex ReferenceName();

    [GeneratedRegex("\\A[0-9]+\\.[0-9]+\\.[0-9]+\\z", RegexOptions.CultureInvariant)]
    private static partial Regex ThreePartVersion();

    [GeneratedRegex("\\A[A-Za-z0-9][A-Za-z0-9.:-]{0,252}\\z", RegexOptions.CultureInvariant)]
    private static partial Regex HostName();
}
