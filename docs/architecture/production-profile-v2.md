# Production profile v2 infrastructure scope

This EventStore-owned structural contract extends the
[v1 construction contract](production-profile-v1.md) for Story 3.19. It selects
no production target or pin, accepts no production inventory, publishes nothing,
and grants no qualification or authorization. The Platform preparation package
and parent story remain unchanged. There is no migration service or decoder
fallback. V1 remains a distinct, strict contract with unchanged golden bytes and
digest preimages.

## Closed grammar and scope

`Contracts/Deployment/ProductionProfileV2*.cs` is the normative typed grammar.
The complete root has `schemaVersion: 2`, fixed
`purpose: "eventstore-resolved-production-profile"`, and exactly `template`,
`infrastructure`, `release`, `environment`, `applications`, `components`,
`catalogs`, `protection`, `issuer`, `freshness`, `restore`, `evidence` and
`infrastructureDigest`. All sections except `components` reuse the corresponding
v1 owner types and mandatory checks. All properties are required, case sensitive
camelCase. Unknown, duplicate or missing fields refuse, including missing fields
whose explicit value may be null. Empty or invalid strings refuse. No arbitrary
metadata or inline credential values are admitted.

`components` is the complete actual instance inventory, including shared and
non-Dapr infrastructure. The existing identifier retains its meaning as an
opaque scoped instance ID, distinct from Dapr component name or application ID.
Every row has exactly:

- `id`, `name`, `kind`, `scope`, `providerId`, `softwareVersion`,
  `loadedIdentityDigest`, `bundleParentId`, `dapr`, `stateful`;
- `roles`, `physicalTargetIds`, `credentialReferenceIds`, `supportSources`,
  `advisorySources`, `currencyOwner`, `renewalOwners`, `evidenceIds`.

Sources, loaded identity, provider pin, role coverage, ownership and evidence
remain mandatory for every kind. `softwareVersion` is the exact constituent
software version and must equal the independently selected provider's `version`.
It is never inferred from a distribution label or from Dapr API version `v1`.
Provider `immutableDigest` and loaded-instance identity remain separate inputs.

Every `scope` has exactly `kind`, `environmentId`, `clusterId`, `nodeId`,
`namespace`. `environmentId` always equals `environment.id`. Applicable cluster
identifiers belong to `environment.clusterIds`. Identifiers use the unchanged
v1 identity syntax. V2 component scope namespaces and application namespaces
must be Kubernetes DNS labels: 1–63 ASCII lowercase letters, digits or hyphens,
beginning and ending with a letter or digit. Spaces, slashes, dots, underscores,
uppercase letters and longer names refuse. This v2 rule does not change v1
namespace acceptance. Inapplicable identifiers must be explicit nulls:

| Scope kind | Cluster ID | Node ID | Namespace |
| --- | --- | --- | --- |
| `environment` | null | null | null |
| `cluster` | required | null | null |
| `node` | required | required | null |
| `namespace` | required | null | required |

The closed instance kinds and applicable scopes are:

| Instance kind | Applicable scope kinds | Dapr details |
| --- | --- | --- |
| `dapr-component` | `namespace` | required |
| `container-runtime`, `node-os` | `node` | null |
| `crd`, `kubernetes` | `cluster` | null |
| `rancher`, `operator` | `cluster`, `namespace` | null |
| `service` | `environment`, `cluster`, `namespace` | null |
| `distribution` | `environment`, `cluster`, `node` | null |

Two instances of the same kind cannot share the same `(kind, scope.kind,
environmentId, clusterId, nodeId, namespace, name)` tuple under different IDs.
Distinct kinds may truthfully share a scoped name, as Kubernetes resource kinds
do. Dapr name uniqueness is retained because all such rows have kind
`dapr-component`. The kind and
scope lists are closed; adding a category requires an owner contract change.
The only nullable fields are the three conditional scope identifiers,
`bundleParentId`, and the whole `dapr` selection. This does not make nested Dapr
fields or other common sections nullable.

## Dapr applicability and retained bindings

A `dapr` selection has exactly `type`, `apiVersion`, `actorStateStore`,
`appScopeIds`, `aclDigest`, `resiliencyDigest`, `dataMigrationDigest`. It is
required exactly when `kind` is `dapr-component`. Its API version remains
separate from `softwareVersion`: for example an independently pinned PostgreSQL
software version can accompany Dapr component API version `v1`. No non-Dapr
instance needs a fabricated namespace, ACL, resiliency or migration digest.

V1's template, runtime floor, sidecar identity, effective configuration and both
application API-token checks remain mandatory. Dapr application scopes remain
reciprocal and share the application's actual cluster and namespace. Actor
components still require the template type/API/name, a sole app scope, exactly
one isolated physical target, exclusive credential material, protected
transactions, component-version tuples and writer inventory. The reused
`protection.componentVersions[].version` binds Dapr `apiVersion`; actual software
versions bind provider pins separately. Physical-target
isolation compares every instance, including non-Dapr infrastructure, with DNS
case and trailing-dot normalization. An infrastructure row cannot hide a
conflicting physical target.

Every stateful instance must appear in restore units. Dapr `state.*` instances
remain stateful and restore-covered independently of a falsified flag.
Scheduler and placement coverage remains mandatory independently of flags.
Token references, credential reciprocity and alias isolation, credential-free
endpoints, OpenBao patched floors, catalogs, issuer bootstrap, append/erasure
semantics, freshness, complete restore order and evidence checks are unchanged.
For each non-Dapr instance, evidence binds its loaded identity and provider pin
with `component` coverage and its explicit component ID. Dapr evidence also
binds all three configuration digests. These are declaration associations only.

## Bundles, topology and complete coverage

`bundleParentId` is null for standalone instances or names another complete
instance of kind `distribution`. Every constituent retains its own provider,
software version, immutable pin, loaded identity, scope, owners and sources.
The parent must contain the constituent's scope: an environment contains scopes
in that environment, a cluster contains scopes in that cluster, and a node
contains only that same node. Self-parent, dangling, cyclic and inconsistent
scope relationships refuse. Both parent and constituent must appear in the
resolved profile and the independent expected inventory.

The declared management/workload/Rancher provider tuple retains v1's provider
kind and role checks. V2 also requires exactly one `kubernetes` instance in each
declared cluster bound to the selected management/workload provider, and exactly
one `rancher` instance in the management cluster bound to the Rancher provider.
Cardinality counts all actual instances of that kind in the declared cluster
before checking the selected provider; an additional differently pinned instance
is a contradictory binding and refuses.
These actual instance identities bind the exact declared tuple; structural
success does not establish upstream compatibility or qualification.

The caller supplies `ProductionProfileValidationContext` with nonempty, unique
`expectedApplicationIds` and `expectedComponentIds`. The latter now covers every
actual v2 instance, including nodes, CRDs, services and distributions. Both actual
sets must equal these independent sets. A profile cannot certify its own
coverage. Missing, duplicate or unexpected instances refuse using safe schema
paths and codes with no supplied values. Results always leave `authorization`
and `qualification` equal to `unevaluated`, including after structural success.

## Exact canonical bytes and two digest domains

The owner canonical writer is shared with v1. Canonical bytes are UTF-8 without
BOM, whitespace, trailing bytes or final LF; object keys sort ordinally, strings
use default BCL JSON escaping, and integers use decimal notation. Identity sets
sort ordinally by `id`; string sets sort ordinally. `restore.order` alone
preserves order. Duplicate set members refuse. Comments, floats, depth over 32,
unsupported versions, noncanonical encoding and complete documents over four
MiB refuse. Direct validation enforces the same canonical-byte size bound.
Decoders snapshot caller buffers before parsing, comparison and hashing.

Let `T` be the complete reused template, `I` the complete reused infrastructure
selection, and `P2` the complete canonical v2 document including its recomputed
`infrastructureDigest`. The infrastructure preimage is exactly the canonical
object:

```json
{"infrastructure":I,"purpose":"platform-infrastructure-profile","schemaVersion":2,"template":T}
```

`infrastructureDigest` is lowercase SHA-256 of those bytes. This changes the
explicit preimage version without expanding its field domain: only the template
and declared invariant provider/topology/tool pins belong here. The unchanged
infrastructure topology fields remain declared invariant requirements. Actual
component scope, node and namespace identifiers, bundle bindings, software and
loaded observations, sources, owners, evidence and all other environment-current
bindings occur only in the complete resolved profile. Declared topology IDs and
actual instance scope are separately represented and structurally cross-checked.

The resolved preimage is exactly `Canonical(P2)`, with no wrapper and no embedded
resolved digest. Its `schemaVersion: 2` explicitly versions this preimage. The
resolved digest is lowercase SHA-256 of those bytes, returned outside them.
Changing only actual scope, a bundle binding, loaded identity, owner, credential
generation or other current binding changes only the resolved digest. Changing
an invariant provider/topology/tool pin or template changes both. No v1 digest is
reinterpreted as v2. `SyntheticProfile.v2.json`,
`SyntheticInfrastructurePreimage.v2.json` and `SyntheticDigests.v2.json` are
independent stdlib-generated controls, never qualified inventory.

## Structural consumption

The sole profile slot remains `deploy/dapr/production-profile.yaml` containing
canonical JSON. The independent composing inventory explicitly chooses
`hexalith.eventstore.production-composing-inventory.v1` or
`hexalith.eventstore.production-composing-inventory.v2`; each has exactly `schema`,
`source` (`path`, `sha256`), `expectedApplicationIds`, `expectedComponentIds`.
The source must be separately content-bound and cannot be either profile or
inventory slot. A v2 inventory selects only `ProductionProfileV2Codec` and
`ProductionProfileV2Validator`; a v1 inventory selects only their v1 counterparts.
A schema/profile mismatch refuses without retrying another decoder.

The [publication authority construction](publication-authority-v1.md) binds both
version contracts and every owner profile source, including the shared canonical
writer and validator. It retains the complete build-proof, qualification,
release-codec and ratification refusals before authenticated execution. Genuine
bridge success establishes only structural consistency. Actual Platform adoption,
authenticated complete inventory, production qualification and publication
remain separate unfinished work.
