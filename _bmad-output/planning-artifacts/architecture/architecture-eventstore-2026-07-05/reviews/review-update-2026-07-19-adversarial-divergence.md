# Reviewer Gate - 2026-07-19 Adversarial Divergence Review Of AD-24

## Verdict

**FAIL — AD-24 fixes the provider label but does not yet make independently built deployment slices converge.** One critical ownership/composition gap and five high-severity contract gaps allow two slices to obey every relevant AD while producing mutually exclusive `openbao` components, secret scopes, bootstrap graphs, rotation behavior, and local/production overlays. The AD-23 payload-key custody boundary remains intact and has no direct conflict.

## Scope And Attack Method

Reviewed `_bmad-output/planning-artifacts/architecture.md` as a feature-altitude spine, concentrating on AD-24 and checking AD-9, AD-10, AD-12, AD-16, AD-18, AD-23, the topology/conventions, and Deferred for constraints that might close the gaps.

The adversarial pair is two one-level-down deployment slices that share the `eventstore` DAPR sidecar:

| Axis | Slice A - state-store credentials | Slice B - pub/sub and application credentials |
| --- | --- | --- |
| Component identity | Defines `Component/openbao`, `secretstores.hashicorp.vault`, `v1`, scoped to `eventstore` | Defines the same canonical component identity and scope |
| OpenBao layout | `enginePath: secret`, `vaultKVPrefix: dapr/eventstore`, `vaultValueType: map`; reads secret `infrastructure` key `redis-password` | `enginePath: kv`, `vaultKVUsePrefix: false`, `vaultValueType: text`; reads `rabbitmq-password` with identical secret name/key |
| DAPR secret scope | Attaches a default-deny Configuration allowing `infrastructure` | Attaches a default-deny Configuration allowing `rabbitmq-password` |
| Bootstrap | Kubernetes/platform secret supplies `vaultToken`; CA is injected as `caPem`; initialization errors are fatal | Host-mounted `vaultTokenMountPath`; platform trust plus `tlsServerName`; initialization errors are fatal |
| Rotation/failure | Patches the component to trigger DAPR reinitialization; gates traffic on successful re-init | Rotates the mounted token/secret by rollout; allows retry for a transient runtime fetch after initial readiness |
| Local overlay | Contract-compatible local-file component is also named `openbao` | Runs a local OpenBao container over HTTP and keeps the production component type |

Both slices use OpenBao in production, the canonical component name/type/version, `secretKeyRef`, DAPR APIs rather than a provider SDK, explicit component scopes, default-deny secret scopes, verified production TLS, out-of-band least-privilege bootstrap material, no committed inline token, and fatal initialization for the secrets each slice classifies as required. Yet the two `Component/openbao` resources and the one app-attached DAPR Configuration cannot coexist as authored. Applying one replaces the other; mechanically merging them still leaves one engine path/prefix/value shape/bootstrap mode and can drop one allowed-secret set. This is the divergence AD-24 says it prevents.

DAPR makes these choices behaviorally significant: the Vault/OpenBao component exposes separate `enginePath`, `vaultKVPrefix`, `vaultKVUsePrefix`, and `vaultValueType` fields, while component `secretKeyRef` has different name/key expectations for map-valued and string-valued secrets. DAPR secret scopes are store-name policies attached through an application Configuration and default to allowing all secrets unless constrained. See the official [OpenBao component](https://docs.dapr.io/reference/components-reference/supported-secret-stores/openbao/), [Vault component metadata](https://docs.dapr.io/reference/components-reference/supported-secret-stores/hashicorp-vault/), [component secret-reference contract](https://docs.dapr.io/operations/components/component-secrets/), and [secret-scoping contract](https://docs.dapr.io/developing-applications/building-blocks/secrets/secrets-scopes/).

## Critical Finding

### C1 - The canonical component and per-app Configuration have no single owner or composition rule

**Evidence:** AD-24 fixes `metadata.name: openbao`, component type/version, exact consuming app-id scoping, and a per-app default-deny allowed list (`architecture.md:301-307`). It does not name the authoritative manifest owner, namespace/deployment-scope uniqueness rule, DAPR Configuration identity/attachment owner, or how consuming slices contribute component scopes and `allowedSecrets`. AD-9 says AppHost and DAPR YAML change in the same slice (`architecture.md:106-110`) and the Runtime topology convention says they remain aligned by tests (`architecture.md:331`), but neither prevents two individually aligned slices from authoring the same singleton resources differently.

**Two compliant but incompatible units:** Slice A and Slice B above both define the required singleton `openbao` component and an `eventstore` secret-scope Configuration. In Kubernetes the same name/namespace is one resource, so last apply wins. With distinct Configuration names, only the Configuration actually attached to the sidecar supplies its secret-scope policy; with the same name, last apply again wins. In self-hosted resources, duplicate component names are likewise ambiguous or load-order-dependent. One slice therefore removes the other's backend metadata, component scope contribution, or allowed-secret contribution.

**Impact:** state store or pub/sub initialization failure, dropped secret access, accidental broadening during an emergency merge, and topology that passes each slice's tests but fails when assembled.

**Disposition: mandatory architecture fix.** Name one deployment-topology owner for the `openbao` Component and each app's DAPR Configuration. Require one resource per `(deployment scope/namespace, component name)` and one attached Configuration per app. Other slices contribute to a canonical/generated secret-consumer catalog; they do not define replacement singleton manifests. The composition step must union exact consuming app IDs and allowed secret names, reject duplicate logical keys or conflicting metadata, render AppHost plus every publish overlay, and test the assembled output rather than each fragment alone.

## High Findings

### H1 - Secret names, engine paths, prefixes, and value shapes are explicitly left open, contradicting AD-24's stated prevention

**Evidence:** AD-24's `Prevents` claims secret-path convergence (`architecture.md:304`), but Deferred leaves `secret paths/rotation policy` and deployment-overlay specifics to later work without a named spec gate (`architecture.md:447`). The Rule fixes none of `enginePath`, `vaultKVPrefix`, `vaultKVUsePrefix`, `vaultValueType`, logical secret name, embedded key, or required/optional classification. These are not merely provider-specific values: DAPR constructs the OpenBao path from the engine/prefix/name choices, and `map` versus `text` changes the `secretKeyRef.name`/`.key` contract.

**Two compliant but incompatible units:** Slice A expects `secret/dapr/eventstore/infrastructure` to return a map containing `redis-password`; Slice B configures the same singleton store for `kv/rabbitmq-password` as text. Selecting either component shape breaks the other. Even if two environments choose different physical paths intentionally, independently authored state-store, pub/sub, application, provisioning, OpenBao-policy, and allowlist units still need one stable logical-to-physical contract per environment.

**Impact:** unresolved component references, wrong embedded-key lookup, divergent OpenBao ACLs, unsafe hand-edited overlays, and rotations applied to a path consumers do not read.

**Disposition: mandatory architecture fix.** Bind a canonical secret contract/catalog containing, at minimum, logical configuration key, `enginePath`, prefix/use-prefix, DAPR secret `name`, embedded `key`, value type, consuming app IDs/components, required/optional class, and AD-23 custody classification. Environment-specific physical roots may remain parameters, but the logical name/key/value shape and consumer mapping must not be story-local. If the catalog is intentionally deferred, introduce a named approved-spec/template path and block every consuming deployment story until it exists, as AD-13 already does for other load-bearing formats.

### H2 - The three scoping layers can be individually least-privilege yet disagree

**Evidence:** AD-24 requires component `scopes` and per-app DAPR secret scopes (`architecture.md:307`) and calls the bootstrap credential least-privilege, but it does not bind the relationship among (1) DAPR Component app-id scopes, (2) the app Configuration's store-specific `allowedSecrets`, and (3) the OpenBao token/policy paths and capabilities. The official DAPR schema treats component `scopes` and application secret scopes as separate controls; OpenBao enforces a third provider policy outside DAPR.

**Two compliant but incompatible units:** Slice A scopes the component and DAPR policy exactly to `eventstore`/`infrastructure`, and grants its token only the state path. Slice B independently does the same for `eventstore`/`rabbitmq-password`. Each credential is least-privilege for its slice. Once there is one canonical component, however, it has one bootstrap token and one app-attached Configuration: using A's token/policy denies B; using B's denies A; broadening to an undocumented union defeats the independently reviewed least-privilege evidence.

**Impact:** false least-privilege claims, production-only authorization failures, or an unreviewed union token with more authority than either slice intended.

**Disposition: mandatory architecture fix.** Make the canonical catalog the source for all three generated/validated layers and define whether the singleton component uses one union policy, per-app/per-namespace instances, or another explicitly supported split. Require exact equality checks from app IDs and logical secret names through DAPR manifests to OpenBao policy paths/capabilities; “aligned by tests” needs this expected mapping to assert.

### H3 - Bootstrap excludes only one recursive lookup and leaves other circular dependencies compliant

**Evidence:** AD-24 says the OpenBao bootstrap credential is out-of-band and not recursively resolved from OpenBao (`architecture.md:307`). It does not classify the TLS trust root/client material or the DAPR API token governed by AD-18. AD-18 requires the client handler to source `dapr-api-token` from configuration (`architecture.md:226-240`) but does not establish that token's bootstrap source. An app cannot call the DAPR Secrets API to obtain the token it must already present to that API. Likewise the OpenBao component cannot obtain from itself the CA/trust material required to establish its verified TLS connection.

**Two compliant but incompatible units:** Slice A treats the OpenBao token, CA bundle, and DAPR API token as hosting-platform bootstrap inputs. Slice B treats only `vaultTokenMountPath` as bootstrap and models the DAPR API token or OpenBao CA as an application secret in the `openbao` catalog. Slice B follows the literal prohibition on recursively sourcing the *OpenBao bootstrap credential* but creates a different cycle. The Rule's broad `Prevents` statement is not enforced by its narrower bootstrap sentence.

**Impact:** sidecar/application deadlock at first start, TLS verification failure, or operators disabling DAPR API auth/TLS verification to recover.

**Disposition: mandatory architecture fix.** Add a bootstrap dependency DAG and classify the minimum out-of-band set: OpenBao authentication input, OpenBao TLS trust/client material where used, DAPR API token when enabled, and any hosting identity required to mount them. Prohibit every path from resolving an ancestor through `openbao`, not only `vaultToken`. Choose and bind the supported bootstrap mechanism per deployment profile (`vaultTokenMountPath`, a named platform secret store, workload-mediated token delivery, etc.) or place that choice behind a named pre-implementation spec gate.

### H4 - Startup, rotation, and runtime failure semantics are assertions without a shared executable policy

**Evidence:** AD-24 says a missing store/bootstrap/required secret fails deployment/startup before traffic and emits only a configuration key (`architecture.md:307`), but does not enumerate required secrets, name the validating owner, bind `spec.ignoreErrors`, define the readiness/traffic gate, or decide behavior after readiness. It also defers rotation policy (`architecture.md:447`). DAPR 1.18 hot-reloads a changed Component by closing and reinitializing it, temporarily making it unavailable; initialization failure behavior depends on `spec.ignoreErrors`. A change to secret data in OpenBao is not itself a change to the DAPR Component resource, so component metadata resolved through `secretKeyRef` needs an explicit refresh/rollout mechanism. See [DAPR resource updates](https://docs.dapr.io/operations/components/component-updates/).

**Two compliant but incompatible units:** Slice A validates its catalog at deployment, uses fatal component initialization, gates readiness, and rotates component credentials through a controlled component update. Slice B classifies only broker bootstrap as required, fetches an application secret lazily, retries/uses a cached value after readiness, and rotates a mounted token by pod rollout. Both fail the secrets they individually call required before their dependent resource serves, yet disagree on requiredness, cache acceptance, outage behavior, rollback, and when a rotation is complete.

**Impact:** mixed credentials across replicas, unbounded stale-secret use, avoidable downtime, partial rotation that appears successful, and `/ready` results that do not prove required-secret availability.

**Disposition: mandatory architecture fix.** For every catalog entry bind required/optional status, startup validator and dependency, `ignoreErrors` posture, readiness/traffic gating, retry/backoff and last-known-good policy, refresh mechanism by consumer class (DAPR component reference versus application Secrets API), overlap/revocation order, rollback, and completion evidence. Reconcile this explicitly with AD-16's support-safe health response and AD-12's production-path evidence requirement.

### H5 - Local and production overlays can preserve names while exercising incompatible contracts

**Evidence:** AD-24 binds production to OpenBao and verified TLS only for non-development deployments (`architecture.md:305-307`). The structural diagram labels a combined `LocalAndPublishTopology` with one `openbao` component (`architecture.md:384-412`), while Deferred leaves deployment overlays unspecified (`architecture.md:447`). AD-9 requires local/publish topology changes together, but does not require secret-name/key/value-shape, default-deny, bootstrap-failure, or rotation-equivalent behavior across profiles.

**Two compliant but incompatible units:** Slice A substitutes `secretstores.local.file` under the `openbao` name locally and preserves a map-valued catalog. Slice B runs real OpenBao locally over HTTP, uses text-valued secrets, and changes the production overlay to HTTPS. Both production outputs obey AD-24 and both update AppHost/YAML/tests together. Their local tests prove different lookup and failure contracts, so neither reliably validates the assembled production manifest.

**Impact:** production-only failures and false confidence from topology tests that validate resource names but not secret semantics.

**Disposition: architecture fix or explicit gated defer.** Decide whether Development runs OpenBao or a contract-compatible surrogate. Any surrogate must preserve component name, logical secret names, embedded-key/value shape, consumer scopes, default-deny behavior, required-secret failure, and diagnostic redaction; only endpoint/TLS/bootstrap transport may differ. Require render-and-validate tests for each supported deployment overlay and at least one production-profile OpenBao integration lane.

## AD-23 Key-Custody Separation And Full-Spine Conflict Check

### PASS - AD-23 and AD-24 establish distinct authorities

AD-23 assigns `pdenc-v2` engine mechanics to EventStore and production root-key/KEK custody to the approved provider backend (`architecture.md:291-299`). AD-24 explicitly states that the DAPR secret store is not production `pdenc-v2` key custody and preserves the Azure Key Vault Premium RSA-HSM wrap/unwrap backend (`architecture.md:309`). The topology reinforces separate edges: `EventStore -> DAPR Secrets -> OpenBao` for operational/application secrets and `PayloadProtection -> KeyBackend` for payload cryptography (`architecture.md:395-414`). A slice that stores root-key or KEK material in OpenBao is therefore not compliant; it is not a surviving adversarial interpretation.

The future payload-protection specification may validly choose an OpenBao-held *credential used to reach* the approved key backend or workload identity instead, because AD-23 assigns credential/environment policy to providers/operators. That choice must not expose root/KEK material through the DAPR Secrets API. The canonical secret catalog recommended above should carry an explicit classification that rejects payload key material while permitting separately approved backend bootstrap credentials.

### Other relevant ADs do not close the findings

- **AD-9** couples AppHost, YAML, overlays, and tests within a change; it does not establish singleton ownership, a merge rule, or the expected secret mapping across independent changes.
- **AD-10** reinforces no committed/plaintext secrets and application-layer security; it does not define OpenBao configuration semantics.
- **AD-12** demands strong evidence but cannot prove convergence until a canonical catalog, bootstrap graph, and rotation policy define what evidence must match.
- **AD-16** keeps health output support-safe, but does not make `/ready` an OpenBao/required-secret traffic gate or decide runtime degradation.
- **AD-18** safely owns outbound DAPR headers but leaves the DAPR API token's bootstrap source open, creating H3.
- **Deferred** is the direct conflict: it postpones paths, bootstrap-auth mechanism, rotation, and overlays even though AD-24 claims to prevent deployment slices from choosing incompatibly and supplies no blocking spec comparable to AD-13.

No other AD contradicts OpenBao as the production provider. The failure is under-specification at the deployment seams, not a conflict with the event-sourcing, gateway, projection, UI, or release invariants.

## Recommended Tightening Order

1. Fix C1 by naming the singleton manifest/Configuration owner and catalog-driven composition rule.
2. Fix H1/H2 with one logical secret and scope-policy catalog; make environment paths parameterized renderings, not independent story decisions.
3. Fix H3/H4 with a bootstrap DAG and required-secret/rotation/failure matrix.
4. Fix H5 with contract-equivalent local overlays plus one production OpenBao integration lane.
5. Keep the AD-23 custody sentence and add a catalog classification guard; do not route `pdenc-v2` root/KEK material through DAPR/OpenBao.

## Tail

No medium or low findings beyond the five high findings above. The provider choice, canonical component name/type/version, prohibition on direct provider SDKs and inline tokens, default-deny intent, verified non-development TLS, and explicit AD-23 separation are sound. They become convergent only after the singleton owner, secret contract, bootstrap graph, and operational semantics are bound or placed behind a named blocking specification.
