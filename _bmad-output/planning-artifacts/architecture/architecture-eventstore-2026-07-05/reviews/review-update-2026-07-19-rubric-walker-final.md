# Reviewer Gate — 2026-07-19 Rubric Walker Final Closure

## Verdict

**PASS — no critical or high findings remain.** The runtime-secret amendment closes the last cross-consumer divergence: one atomic `GetSecret` map carries a generation, process-local caching is bounded inside the rotation overlap, acknowledgement is readiness-bound, and old material cannot be revoked before every runtime and startup-only consumer has moved safely.

## Scope And Method

Reviewed the complete canonical `_bmad-output/planning-artifacts/architecture.md` against the good-spine checklist, with a focused attack on AD-24 lines 308-314. Constructed independently implemented runtime consumers, startup-only Dapr components, deployment validators, rotation controllers, failure/recovery paths, and development substitutes to test whether all could obey the Rule while disagreeing about retrieval, atomicity, cache validity, acknowledgement, rollback, or revocation.

The earlier runtime-version, Azure Container Apps, logical path/catalog ownership, bootstrap-cycle, scope/ACL alignment, and AD-23 boundary resolutions were also rechecked and remain intact.

## Runtime Retrieval And Rotation Closure

### Retrieval unit and version behavior

AD-24 now fixes the serving-path operation and atomicity boundary (`architecture.md:312`):

- runtime-required consumers call `GetSecret` for exactly one logical map;
- `BulkGetSecret` is forbidden, avoiding the incompatible behavior of the fixed `vaultKVUsePrefix: true` component posture;
- normal serving paths do not pin `metadata.version_id`, so consumers converge on the current published OpenBao version;
- values that must rotate atomically share one map and one required non-secret generation marker;
- the canonical contract owns embedded keys, value shapes, rotation-unit boundaries, consumers, and generation/cache/rotation bounds (`architecture.md:308`).

Two consumers can no longer split an atomic credential set across calls, merge values from different versions, or select incompatible Vault versions while remaining compliant.

### Cache and failure behavior

The cache rule is both bounded and fail closed:

- successful values may exist only in process memory;
- each cataloged `maxAge` is shorter than its rotation overlap;
- consumers may not persist values, merge generations, use an expired value, or continue using a value after failed refresh;
- lookup failure disables the dependent operation and keeps readiness false until a bounded successful recheck;
- plaintext and alternate-provider fallback remain forbidden.

This closes the prior stale-cache seam. A consumer cannot acknowledge a generation and then legally retain old material beyond the overlap, and a transient refresh failure cannot silently turn the previous value into an unbounded fallback.

### Rotation, acknowledgement, and rollback

The selected protocol is one enforceable sequence (`architecture.md:314`):

1. Publish the new OpenBao version/generation while old and new credentials are both accepted.
2. Wait for every cataloged runtime consumer to acknowledge the new generation while ready.
3. Wait for every startup-only component to acknowledge through successful sidecar rollout/component initialization.
4. Revoke old material only after complete acknowledgement.

Failure before complete acknowledgement preserves old validity and rolls back by publishing a restored generation; early revocation is explicitly forbidden. Because runtime values are generation-marked and retrieved without version pinning, restored generations become the convergent current value without requiring consumers to select historical Vault versions. The startup-only rule remains compatible with the Vault component's initialization-time token behavior and controlled sidecar rollout.

No two compliant rotation controllers can now disagree about when old material may be revoked, what constitutes runtime/startup acknowledgement, or whether an incomplete rollout can continue by invalidating the old credential.

## Cross-Decision Consistency

- **AD-9:** the singleton overlay, canonical contract, per-app Configurations, component scopes, and deployment validation remain one topology unit.
- **AD-10:** failed refresh, expired cache, missing bootstrap material, and incomplete rotation all fail closed; diagnostics remain support-safe.
- **AD-12:** manifest/contract validation, readiness evidence, rotation behavior, and the mandatory real-OpenBao integration lane provide production-path proof.
- **AD-18:** the Dapr API token remains an out-of-band bootstrap-class input and does not create a recursive secret lookup.
- **AD-23:** operational/application secret rotation does not approve or alter the draft payload-protection KEK backend; `pdenc-v2` key custody remains separate.
- **Azure Container Apps:** managed ACA Dapr remains explicitly non-conforming until a separately approved profile proves OpenBao support and equivalent scoping (`architecture.md:316`).

No diagram, convention, Stack row, capability mapping, or Deferred item weakens these rules. Environment-specific values remain safely deferred under the sole platform-overlay owner rather than becoming independent architectural choices.

## Good-Spine Checklist

| Check | Result | Notes |
| --- | --- | --- |
| Fixes the real divergence points one level down | **Pass** | Provider, owner, retrieval unit, generation, cache, acknowledgement, rotation, rollback, and environment support are fixed. |
| Misses no owned divergence point | **Pass** | Runtime and startup-only consumers share one catalog and one revocation gate. |
| Every AD Rule is enforceable and prevents its stated divergence | **Pass** | Contract parsing, manifest/scope/ACL validation, readiness, cache expiry, generation acknowledgements, rollout, and integration tests are observable gates. |
| Deferred contains no divergence-producing architecture decision | **Pass** | Exact values and windows are downstream data under one overlay-owned contract; their ownership and required inequalities are fixed. |
| Named technology is verified-current | **Pass** | OpenBao Stable v1/runtime 1.16 catalog status and Vault-compatible component type are verified; runtime and SDK pins remain separate. |
| Ratifies rather than contradicts brownfield reality | **Pass with handoff** | Runtime 1.18.0 is ratified; stale deployment guides remain implementation documentation to reconcile with the explicit target decision. |
| Covers driving requirements/spec capabilities | **Pass** | FR34, NFR4, NFR17, and Story 7.6 have enforceable provider, safety, and operational behavior. |
| Does not weaken inherited invariants | **Pass** | No parent spine is inherited; AD-9/10/12/18/23 remain intact or stronger. |
| Every feature-altitude dimension is decided, deferred, or open | **Pass** | Deployment, provider, security, ownership, lifecycle, rotation, failure, environments, and release evidence are covered. |

## Non-blocking Handoff

Story 7.6 still needs to create `deploy/dapr/openbao-secret-contract.yaml`, implement its validator/readiness/acknowledgement evidence, add the real-OpenBao lane, and reconcile the Kubernetes/ACA/deployment/security guides. Those are bounded implementation and documentation tasks, not remaining critical or high architecture findings.
