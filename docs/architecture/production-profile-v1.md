# Production profile v1 construction contract

This is the EventStore-owned structural construction contract for Story 3.19.
It neither completes that story nor selects or qualifies a production target.
No profile is published by this library. The eventual sole publication slot is
`deploy/dapr/production-profile.yaml`; its bytes use strict JSON, a YAML subset.
The Platform preparation package remains an unchanged design input.

## Closed grammar

The normative typed grammar is `Contracts/Deployment/ProductionProfile*.cs`.
Every constructor parameter is a required, case-sensitive JSON property using
camelCase. All sections, selections, strings and collections are non-null. Empty
strings, control characters, unknown properties and duplicate properties refuse.
No arbitrary metadata maps or inline credential-value fields exist. An empty
collection is allowed only where the deployment genuinely has no such members;
the validator separately requires the inventories and coverage listed below.

The root has integer `schemaVersion: 1`, fixed
`purpose: "eventstore-resolved-production-profile"`, and exactly `template`,
`infrastructure`, `release`, `environment`, `applications`, `components`,
`catalogs`, `protection`, `issuer`, `freshness`, `restore`, `evidence`, and
`infrastructureDigest`. The infrastructure digest is recomputed; the resolved
digest is returned outside the document. Digests are 64 lowercase hex characters.
All numeric fields are decimal integers. The records describe explicit selected
inputs; a version, digest, evidence identifier or `qualified*` name is a claim
being bound, never a finding that qualification actually occurred.

Identities use 1–128 ASCII letters, digits, dots, underscores, colons, slashes and
hyphens. App/component IDs are opaque scoped IDs, supplied by the composing
inventory as well as the document; they are distinct from Dapr `appId` and
component `name`. Within the environment each app `(clusterId, namespace,
appId)` and component `(clusterId, namespace, name)` is unique. Instance sets
must equal the caller's nonempty expected app and component ID sets. Missing,
duplicate or empty context refuses. A document cannot certify its own coverage.

Every component names a pinned provider, its loaded identity, declared roles,
app scopes, ACL and resiliency digests, source/currency/renewal owners and
evidence. Every app binds its retained artifact, image, sidecar, effective
configuration, token references, hosted actor types and evidence. App scopes
and all declared app/component/provider/target/artifact/evidence references must
resolve. An endpoint is a public, credential-free absolute address associated
with a component. Database physical targets explicitly include provider, host,
port, database, table and metadata table; public values are never inferred from
an endpoint or a credential. Actor components require exactly one such target.

Actor-hosting apps have exactly one actor-state component, scoped solely to
their scoped app ID, in their cluster and namespace. It uses the selected
template actor type/version and component name `statestore`. No other component
may bind the same database/host/port and intersecting table or metadata table;
distinct target IDs cannot evade isolation. Actor-hosting apps cannot scope into
another app's actor component. Other state and shared reads are separately
enumerated components. Credential references are closed `openbao` logical
name/map-key/generation records with explicit consumer and dependent component
IDs; no credential value, provider connection string, URI userinfo, query or
fragment is accepted in an endpoint. Reference presence says nothing about
OpenBao grants or the referenced secret's contents.

## AD-26 and approved design mapping

| Requirement | Required typed fields |
| --- | --- |
| Template identity and invariant contract | `template.id`, `runtimeMinor`, `minimumRuntimeVersion`, `sidecarMode`, actor component type/version/name, OQ8 profile, scope/ACL/resiliency shape digests, append semantics, secret contract/floor, issuer/scheduler/placement/restore contracts and excluded profiles; `release.templateId` |
| Exact Dapr runtime image, SDK, CLI and compatibility | `release.runtimeVersion`, `runtimeImageDigest`, `sdkVersion`, `cliVersion`, `cliCompatibilityEvidenceId`, `sidecarPatchVersion`; each app's `sidecarImageDigest`; minimum Dapr 1.18.3 and declared minor agree |
| Kubernetes/per-app sidecars and control-plane capability drop | `infrastructure.topology.deploymentMode`, `template.sidecarMode`, `environment.controlPlaneDropAllCapabilities`; per-app sidecar identity |
| Provider, topology and deploy-tool pins | `infrastructure.providers` (`id`, provider/version/immutable digest, topology, roles), `tools` (ID/version/digest), `topology` |
| Complete shared/environment instance inventory, loaded identities, currency | externally expected component IDs; each `components` row's provider, version, loaded identity, role coverage, support/advisory sources, currency/renewal owners and evidence; providers can serve multiple roles without fictitious deployments |
| Management/workload pair and Rancher | `infrastructure.topology`: management/workload cluster IDs, management/workload provider IDs, Rancher provider/license, capacity, private access/authentication/agent-permission contract digests, backup component IDs, native recovery access and deployment-writer boundary digests; referenced components include actual agents/CRDs/operators |
| App/component scopes, ACLs and separate state | complete `applications` and `components`; per-app component IDs, per-component scope IDs, namespace/cluster identity, actor flag, data-migration digest, ACL/resiliency digests; reciprocal links and unique physical ownership |
| Actor-state physical target and no provider credentials | `environment.physicalTargets` host/port/database/table/metadata table/provider; component target IDs; `environment.credentials` references only |
| Broker durability, retention, dead letter and recovery | `environment.brokerComponentId`, `brokerContractDigest`, `brokerEvidenceIds`; corresponding pinned/loaded component and evidence |
| Scheduler and placement | `environment.schedulerComponentIds`, `placementComponentIds`, exact scheduler/placement configuration digests and evidence |
| API tokens and effective applied configuration | each app's `daprApiTokenEnabled`, `appApiTokenEnabled`, Dapr/app token credential reference IDs, `effectiveConfigurationDigest`, scope/ACL/resiliency digests; environment applied/rendered configuration digests |
| OpenBao AD-24 | `environment.openBaoComponentId`, `secretContractDigest`, `openBaoServerVersion`, `credentials`; `template.secretContractDigest`, `openBaoServerFloor`; issuer bootstrap/client references |
| Release composition | `release.artifacts` exact packages/images, composed host digest, extension digests, retained application Helm digest, McpCli package digest, qualified infrastructure set IDs, realm/recovery/check-suite contract digests and evidence |
| Environment-current bindings | `environment.id`, cluster IDs, endpoints/targets, key generations, applied realm and secret-contract identities, credential generations, effective/rendered configuration digests |
| AD-33/AD-25 catalogs and AD-34 | `catalogs.generation`, root/route/idempotency/content digests, AD-34 profile inventory/register digests and evidence; release catalog content digest must agree |
| Mandatory AD-5 append tuple and erasure | `protection.mode`, implementation ID, transaction semantics digest, qualified SDK/runtime/component versions, actor ownership, mandatory guard, required semantics, writer inventory, placement/failover digest, authorized erasure contract and evidence; exact runtime/SDK/provider versions agree |
| Workload assertion issuer and bootstrap | `issuer.id`, component ID, contract digest, assertion configuration digest, bootstrap and client credential reference IDs, evidence |
| Replica/freshness posture | `freshness.eventStoreAppId`, replica count, multi-replica claim, posture digest and evidence; one replica or no multi-replica claim |
| Complete restore posture | `restore.acceptedPostureDigest`, consistency point, backup method, ordered units covering every state component plus scheduler/placement; each unit's coverage, component IDs and method; complete order and forbidden independent restores; readback evidence |
| Required evidence with ownership and associations | `evidence` ID, owner, qualification ID, source identity, subject digests, app/component/target and coverage associations; section evidence IDs resolve and app/component/target associations are checked |

The seven required append semantics are `revision-tied-to-read-state`,
`conditional-durable-revision-advance`, `create-once-event-identity`,
`whole-actor-batch-commit-or-reject`, `mandatory-raw-and-generic-path-enforcement`,
`exact-original-batch-reconciliation-after-unknown-outcome`, and
`governed-erasure-advances-revision-and-rejects-stale-writers`.
Restore coverage contains `actor-events-revisions-erasure-completion`,
`admission-and-fence`, `global-position-allocation`,
`read-models-checkpoints-command-status`, `scheduler-reminders-and-jobs`, and
`placement`. The ordered restore units cover every component flagged stateful,
including scheduler and placement. Each unit appears once in `restore.order`,
and every such unit is explicitly forbidden from independent restoration.

## Canonical bytes and digest domains

Encoding is UTF-8 without BOM, whitespace, trailing bytes or final LF. Object
keys sort ordinally, strings use default BCL JSON escaping and integers use
decimal notation. Identity sets sort ordinally by `id`; all string sets sort
ordinally. `restore.order` alone retains declared order. Duplicate set members
refuse. Floats, comments, unsupported versions, documents over 4 MiB, depth over
32 and any input bytes differing from the canonical re-encoding refuse on
decode. The decoder captures caller-owned buffers before parsing and comparison.

The infrastructure preimage is the canonical object
`{"infrastructure":I,"purpose":"platform-infrastructure-profile","schemaVersion":1,"template":T}`.
Its lowercase SHA-256 is `infrastructureDigest`. It includes template plus
provider/topology/deploy-tool pins only. It excludes release data, endpoints,
credentials/generations, loaded observations, owners, support/advisory sources
and other environment-current bindings. The resolved digest is SHA-256 of the
complete canonical document, including the recomputed infrastructure digest;
the resolved digest itself is never serialized. Neither digest includes itself.
These new encodings do not reuse or change route catalog preimages or Builds'
`ReleaseEvidenceCodec`. Changing only release/environment bindings changes only
the resolved digest; changing a provider pin changes both.

## Result boundary

`ProductionProfileValidator.Validate` returns structural validity, deterministic
path/code issues and, on success, both digests. Paths use schema names and numeric
array indices, never attacker-controlled IDs or values. Codec failures use the
same safe issue exception without retaining parser exceptions or input values.
`Authorization` and `Qualification` always report `unevaluated`, including for a
complete synthetic profile. There is no release availability, promotion,
authenticated evidence, authority receipt, signature verification, support
currency evaluation or production readiness result. Those remain separate owner
work and Story 3.19 issue-step gates.
