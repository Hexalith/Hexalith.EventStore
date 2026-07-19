# Reviewer Gate - 2026-07-19 AD-24 Final Adversarial Closure Review

## Verdict

**PASS — no critical or high-severity AD-24 divergence remains.** The runtime rotation amendment closes the final high finding, and the complete provider/component/catalog/scope/bootstrap/failure/overlay/key-custody attack no longer yields two independently built units that can obey every relevant AD while behaving incompatibly.

## Closure Of The Remaining High

The prior re-review found that two `runtime-required` consumers could both comply while disagreeing on `GetSecret` versus bulk retrieval, latest versus pinned versions, cache/refresh bounds, and old-version revocation.

AD-24 now fixes all load-bearing choices (`architecture.md:308-314`):

- the canonical contract records runtime generation/cache/rotation bounds and rotation-unit boundaries;
- serving-path consumers call `GetSecret` for one logical map;
- `BulkGetSecret` and serving-path `metadata.version_id` pinning are forbidden;
- atomically rotating values share one map and a required non-secret generation marker;
- successful values remain only in process memory for cataloged `maxAge`, which must be shorter than the overlap window;
- expired or failed-refresh values are not used, persisted, or merged across generations; and
- rotation is the single publish -> overlap -> acknowledge -> revoke protocol, with every runtime consumer ready on the new generation and every startup-only component reinitialized before revocation; early revocation is forbidden and pre-acknowledgement failure retains the old credential and publishes a restored generation.

Replaying the previous pair now produces no compliant incompatibility:

- A latest-per-operation consumer conforms.
- A pinned-version, bulk-read, over-age-cache, or early-revocation consumer violates an explicit Rule and is not a surviving interpretation.
- Consumers may choose different internal implementation techniques only if they honor the same cataloged `maxAge`, generation acknowledgement, readiness, and revocation fence; those differences are not observable contract divergence.

## Full AD-24 Re-attack

### Provider, component identity, and ownership - PASS

Production uses the singleton `openbao` component with fixed name/type/version. The platform overlay solely owns and composes that Component, every per-app DAPR Configuration, and `deploy/dapr/openbao-secret-contract.yaml`; downstream state-store, pub/sub, application, and deployment slices cannot author competing resources (`architecture.md:306-308`). The original last-writer-wins collision is no longer compliant.

### Secret names, engine paths, and value shape - PASS

One value-free contract binds store-relative name, embedded map keys, consumers, dependencies, lifecycle, policy path, rotation boundaries, and runtime bounds. `vaultValueType: map` and `vaultKVUsePrefix: true` are fixed. Only the platform overlay selects environment-specific endpoint/engine/prefix/TLS values, while logical names, keys, shapes, consumers, lifecycle, and rotation units remain identical (`architecture.md:308`). Deferred environment values therefore cannot be chosen incompatibly by consuming slices.

### Component, application, and provider scoping - PASS

Component app-id scopes, default-deny per-app `allowedSecrets`, and least-privilege OpenBao ACLs derive from the same contract. The assembled deployment gate rejects missing grants, extra grants, and cross-layer mismatches (`architecture.md:310`). Two independently “least privilege” policies can no longer deny one another while remaining compliant.

### Bootstrap and TLS - PASS

The OpenBao token, DAPR API token, and TLS trust material are hosting-platform bootstrap inputs that never depend on DAPR/OpenBao and are never committed. Verified HTTPS is mandatory outside Development, inline `vaultToken` is forbidden, and bootstrap-token rotation requires sidecar restart/rollout rather than assumed renewal or hot reload (`architecture.md:310`). The former DAPR-token and TLS-trust cycles are explicitly non-compliant.

### Startup, runtime failure, and readiness - PASS

Every host validates declared secrets through DAPR before readiness. Missing store/bootstrap/startup secrets fail startup; runtime lookup or refresh failure disables the dependent operation, withdraws readiness, and permits no plaintext, alternate-provider, expired, or failed-refresh fallback. Diagnostics expose only logical key and non-secret generation (`architecture.md:312-314`). This composes with AD-16's support-safe probes and AD-12's production-path evidence requirement.

### Local and production overlays - PASS

Development substitutes must retain the component name, logical contract, default-deny behavior, value shape, consumers, lifecycle, and rotation units, and release evidence includes real OpenBao. Azure Container Apps managed DAPR is expressly non-conforming until an approved equivalent profile proves component support and least-privilege scoping (`architecture.md:314-316`). Local tests can no longer silently validate a different contract.

### AD-23 key custody - PASS

OpenBao remains limited to operational/application secrets. AD-24 neither approves nor modifies the draft payload-protection backend and explicitly forbids treating the DAPR store as `pdenc-v2` root-key/KEK custody (`architecture.md:318`). The topology retains distinct OpenBao and payload-key-backend edges. A slice that stores payload root/KEK material in OpenBao violates AD-23/AD-24 rather than representing a compliant alternative.

## Whole-Spine Conflict Check

- AD-9's topology unit and assembled tests now have one owner and one canonical secret contract to assert.
- AD-10's fail-closed/no-committed-secret posture is preserved.
- AD-12 can verify assembled manifests, real OpenBao, generation cutover, readiness withdrawal, and denial evidence.
- AD-16 remains support-safe while AD-24 controls dependency readiness.
- AD-18's DAPR API token source is unambiguously bootstrap-class.
- AD-23 remains authoritative for payload-key custody.
- Deferred retains only environment values and maintenance scheduling; none lets lower-level units redefine the fixed retrieval or rotation protocol.

No remaining critical or high findings.
