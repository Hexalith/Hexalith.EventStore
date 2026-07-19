# Reviewer Gate - 2026-07-19 AD-24 Adversarial Divergence Re-review

## Verdict

**FAIL — no critical findings remain; one high-severity rotation/retrieval contract gap remains.** The tightened AD-24 closes the prior singleton ownership, logical catalog/value-shape, three-layer scoping, bootstrap-cycle, and local/production parity holes. Independently built application consumers can still obey the canonical contract while selecting and retaining different OpenBao secret generations during a non-bootstrap secret rotation.

## Scope And Re-run Method

Re-reviewed the canonical `_bmad-output/planning-artifacts/architecture.md`, concentrating on AD-24 (`architecture.md:302-316`) and checking AD-9, AD-10, AD-12, AD-16, AD-18, AD-23, the Secrets/Runtime topology conventions, the structural topology, and Deferred.

The original state-store and pub/sub slices were replayed against every tightened clause. A second pair of independently built application-secret consumers was then constructed to test the remaining `runtime-required` lifecycle:

- Consumer A calls DAPR `GetSecret` for the canonical name on each dependent operation, requests no `metadata.version_id`, and therefore expects the latest OpenBao version to become authoritative immediately.
- Consumer B calls the same DAPR API for the same canonical name and embedded map key, but pins `metadata.version_id` during a rotation overlap and caches the successful result until its bounded recheck/host rollout. On lookup failure it disables the dependent operation, marks readiness false, and never falls back to plaintext, another provider, or an unbounded stale value.

Both consumers use the singleton `openbao` component, preserve the value-free catalog's name/key/map shape, stay within the generated component/app/OpenBao scopes, source bootstrap material out of band, and follow the stated fail-closed behavior. AD-24 does not choose between their supported retrieval/version/cache models or bind them to one rotation state machine.

## Prior Finding Reconciliation

### RESOLVED C1 - Singleton component and Configuration ownership

AD-24 now assigns sole ownership and composition of `Component/openbao`, every per-app DAPR `Configuration`, and `deploy/dapr/openbao-secret-contract.yaml` to the platform deployment overlay (`architecture.md:308`). State-store, pub/sub, application, and deployment slices are expressly prohibited from authoring competing copies. The original last-writer-wins resource collision is no longer compliant.

### RESOLVED H1 - Logical names, paths, embedded keys, and value shape

The canonical value-free contract records store-relative name, embedded map key, consumers, dependent component/host, retrieval lifecycle, and OpenBao policy path. AD-24 fixes `vaultValueType: map` and `vaultKVUsePrefix: true`, reserves `vaultAddr`, `enginePath`, `vaultKVPrefix`, and TLS metadata to the sole overlay owner, and requires logical names/keys/shapes/consumers/lifecycles to remain identical across environments (`architecture.md:308`). Two downstream slices can no longer independently choose `map` versus `text` or incompatible logical paths.

Exact catalog entries remain deferred (`architecture.md:457`), but that is no longer a cross-slice choice: one named contract and one owner bind them before consuming manifests are rendered. No critical/high divergence survives on catalog ownership or shape.

### RESOLVED H2 - DAPR component scopes, DAPR secret scopes, and OpenBao ACLs

All three layers now derive from the same contract, and a deployment gate rejects missing grants, extra grants, or mismatches (`architecture.md:310`). The former independently least-privilege but mutually denying state/pubsub policies are no longer compliant.

### RESOLVED H3 - Circular bootstrap inputs

The OpenBao token, DAPR API token, and TLS trust material are explicitly bootstrap-class, hosting-platform inputs that never depend on DAPR or OpenBao (`architecture.md:310`). This closes the DAPR-token-through-DAPR and OpenBao-CA-through-OpenBao cycles and reconciles AD-24 with AD-18.

### PARTIALLY RESOLVED H4 - Startup/failure and bootstrap-token rotation

AD-24 now binds per-secret `startup-only` versus `runtime-required` lifecycle, validation before readiness, fatal startup behavior, fail-closed runtime lookup behavior, bounded recheck, no alternate-provider/plaintext/stale fallback, redacted diagnostics, and an explicit sidecar rollout for the OpenBao bootstrap token (`architecture.md:308-312`). Those parts of the prior finding are resolved.

The remaining high finding below concerns rotation of the operational/application secret values retrieved *through* the initialized store, not rotation of the store's own bootstrap token.

### RESOLVED H5 - Development/production contract parity

Development substitutes must keep the `openbao` component name, canonical logical contract, and default-deny behavior, and release evidence must include a real-OpenBao integration lane (`architecture.md:312`). Environment-specific endpoint/TLS/bootstrap transport may differ without allowing secret-name/key/value/lifecycle drift. Azure Container Apps managed DAPR is expressly non-conforming until an approved equivalent profile exists (`architecture.md:314`).

### PASS - AD-23 key custody remains separate

AD-24 still forbids treating DAPR/OpenBao as production `pdenc-v2` root-key or KEK custody and now carefully describes the Azure Key Vault Premium RSA-HSM backend as draft/not authorized (`architecture.md:316`). The structural topology retains separate DAPR-secret and payload-key-backend edges (`architecture.md:405-424`). No compliant interpretation places payload root/KEK material in OpenBao.

## Remaining High Finding

### H4 - `runtime-required` does not bind version selection, cache lifetime, refresh, or non-bootstrap rotation completion

**Evidence:** The contract records only `startup-only` or `runtime-required` retrieval lifecycle (`architecture.md:308`). AD-24 defines the outcome after a runtime lookup fails and requires a bounded successful recheck, but it does not say when a consumer must perform the lookup, whether successful values may be cached and for how long, whether reads use latest or the DAPR Vault component's supported `metadata.version_id`, how old/new credentials overlap, when the provider may revoke the old version, or what proves every replica has adopted the new value. The controlled rollout language at `architecture.md:310` is specifically for the OpenBao **bootstrap token**. Deferred still leaves the rotation maintenance window to deployment hardening (`architecture.md:457`) without first fixing the consumer-visible rotation protocol.

The official DAPR Vault/OpenBao component exposes `metadata.version_id` as optional per-request metadata, so latest-versus-pinned is a real supported behavior rather than an invented implementation detail. The component also requires `vaultKVUsePrefix: false` for `BulkGetSecret`, while AD-24 fixes it to `true`; application slices are not told to use single-secret `GetSecret` only. See the official [Vault/OpenBao component metadata and request options](https://docs.dapr.io/reference/components-reference/supported-secret-stores/hashicorp-vault/) and [OpenBao compatibility page](https://docs.dapr.io/reference/components-reference/supported-secret-stores/openbao/).

**Two compliant but incompatible units:**

- Consumer A always reads latest per operation and switches as soon as the provider publishes the new version. Its deployment team revokes the old credential quickly because all A replicas report successful new-version reads.
- Consumer B pins the old `version_id` during overlap and caches it until a bounded refresh or rollout. It continues to satisfy all dependent operations until A's provider action revokes the old version, then fails closed and withdraws readiness. Alternatively, a B implementation can choose `BulkGetSecret` for several canonical entries and remain within the broad “DAPR Secrets API” rule, but it cannot operate with the fixed prefix posture.

Neither unit changes provider/component identity, catalog names or map keys, scopes, bootstrap inputs, TLS, diagnostics, or AD-23 custody. Their incompatible generation/refresh expectations appear only during rotation, exactly when the current contract must coordinate producer, consumers, and replicas.

**Impact:** mixed secret generations across replicas, avoidable readiness loss, failed state-store/broker/application authentication after early revocation, an indefinitely delayed cutover within an otherwise “bounded” but unspecified cache/recheck interval, or a permanently failing bulk-read implementation.

**Disposition: mandatory AD-24 tightening.** Extend `openbao-secret-contract.yaml` with the consumer-visible retrieval/rotation contract for every logical secret:

1. allowed DAPR operation (`GetSecret` only while `vaultKVUsePrefix: true`; explicitly forbid `BulkGetSecret` unless the architecture changes the prefix decision);
2. version policy (latest-only, or named pinned-version protocol with one owner);
3. maximum successful-value cache age and refresh trigger for `runtime-required` consumers;
4. component-reference refresh mode for `startup-only` credentials (controlled sidecar/component rollout);
5. publish -> overlap -> consumer acknowledgement -> revoke -> rollback ordering; and
6. assembled, per-replica completion evidence before old-version revocation.

Exact TTL/window values may remain environment parameters, but the state transitions, ownership, and bound that all consumers honor must be invariant. A simpler convergent rule is: runtime-required application consumers use single-secret latest-version reads, do not cache beyond a centrally configured maximum age, and provider revocation waits for every ready replica to prove the new generation.

## Whole-Spine Conflict Check

- **AD-9** now composes cleanly with AD-24 because the overlay owns the one contract and the assembled AppHost/YAML/configuration output.
- **AD-10** remains compatible with the no-plaintext/no-alternate-provider posture.
- **AD-12** can supply the required assembled-manifest, real-OpenBao, failure, and future rotation evidence once H4 defines the expected generation protocol.
- **AD-16** permits readiness to go false without leaking secret/provider details; AD-24's logical-key-only diagnostics are compatible.
- **AD-18** is no longer a bootstrap cycle because the DAPR API token is hosting-platform supplied.
- **AD-23** remains a separate payload-key authority and is neither weakened nor overridden.
- **Deferred** may safely retain concrete OpenBao topology, endpoint/path values, token TTL, and maintenance-window scheduling. It may not leave the consumer-visible secret-generation protocol open, because independently built producer and consumer units must agree before those exact values are selected.

No other critical or high-severity AD-24 divergence was found.
