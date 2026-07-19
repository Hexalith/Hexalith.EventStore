# Reviewer Gate — 2026-07-19 Rubric Walker Re-review

## Verdict

**PASS — no critical or high findings remain.** The tightened AD-24 now fixes the OpenBao decision at feature altitude without conflating Dapr runtime and SDK versions, without claiming Azure Container Apps is conforming, and without leaving secret naming/path/scoping ownership available to competing deployment slices.

## Scope And Method

Re-reviewed `_bmad-output/planning-artifacts/architecture.md` against the complete good-spine checklist, concentrating on AD-24 and its interactions with AD-9, AD-10, AD-12, AD-18, AD-23, Consistency Conventions, Stack, Structural Seed, Capability Mapping, and Deferred. Each prior finding was retested against current official documentation and brownfield repository evidence; the remaining AD-24 rules were challenged by constructing independently implemented deployment, component, host, rotation, failure, and development-test slices.

## Prior Finding Resolution

### H1 — Runtime evidence: resolved

The spine now separates the Dapr runtime pin from Dapr .NET SDK packages (`architecture.md:306`, `:352-354`):

- repository CI/deployment runtime seed is explicitly `1.18.0`;
- production profiles must pin a compatible 1.18.x runtime;
- Dapr .NET SDK `1.18.4` is explicitly not runtime evidence.

The prior review understated the official catalog evidence. The current [Dapr supported secret-store catalog](https://docs.dapr.io/reference/components-reference/supported-secret-stores/) lists OpenBao as Stable, component v1, since runtime 1.16, while the [OpenBao component page](https://docs.dapr.io/reference/components-reference/supported-secret-stores/openbao/) correctly explains that it uses `secretstores.hashicorp.vault` rather than a dedicated component type. The tightened statement is therefore verified-current and the repository's explicit 1.18.0 runtime seed satisfies the catalog floor.

### H2 — Azure Container Apps incompatibility: resolved

AD-24 now states unambiguously that Azure Container Apps managed Dapr is not a conforming production profile because OpenBao is outside its supported component set and Dapr Configuration secret scopes are unsupported (`architecture.md:314`). A future ACA profile requires a separate approval proving both OpenBao component support and equivalent least-privilege scoping; it cannot silently substitute Azure Key Vault or claim compliance.

This is a convergent architecture choice. A Kubernetes/self-managed-Dapr slice and an ACA slice can no longer both claim AD-24 compliance while selecting incompatible providers or schemas. Existing ACA/Kubernetes deployment guides still require follow-up reconciliation, but that is source/documentation implementation work rather than an unresolved choice in the spine.

### M1 — Secret-path prevention versus deferral: resolved

AD-24 no longer claims that every environment has identical provider endpoint/engine/prefix values. It instead fixes one value-free logical contract, one owner, and one derivation path (`architecture.md:305`, `:308-310`):

- the platform deployment overlay solely owns the singleton `Component/openbao`, all per-app Dapr Configurations, and `deploy/dapr/openbao-secret-contract.yaml`;
- the contract fixes logical names, embedded keys/value shapes, consumers, dependent resources, retrieval lifecycle, and matching OpenBao policy paths;
- component scopes, secret scopes, and OpenBao ACL grants derive from the same contract and mismatches fail validation;
- exact endpoint, engine/prefix, bootstrap TTL, catalog entries, and operational values remain downstream overlay data under one named owner (`architecture.md:457`).

Two independently implemented consuming slices therefore cannot author competing path/name/scope authorities while remaining compliant. The deferred values are environment seed owned by the overlay, not unresolved cross-unit architecture.

## Whole-Spine AD-24 Review

### Enforceability and divergence prevention

AD-24 fixes all non-obvious integration choices needed one level down:

- provider, component identity/type/version, runtime evidence, and application abstraction;
- the sole metadata/catalog/configuration owner;
- canonical logical secret contract fields and value shape (`vaultValueType: map`);
- component scopes, per-app default-deny secret scopes, and OpenBao ACL derivation;
- bootstrap-class inputs and the prohibition on recursive Dapr/OpenBao bootstrap;
- initialization-time token semantics and controlled sidecar rollout on rotation;
- startup-only versus runtime-required behavior, readiness gating, bounded recheck, diagnostics, and fallback prohibition;
- development substitute parity and mandatory real-OpenBao release evidence;
- explicit production-environment support boundary.

Each Rule is observable through manifest validation, real-host readiness/failure behavior, scope/ACL comparison, forbidden-secret scans, rotation tests, and a real-OpenBao integration lane. These rules actually prevent every divergence named by AD-24's `Prevents` clause.

### Cross-AD consistency

- **AD-9:** AppHost/Dapr YAML parity is strengthened by the singleton overlay owner and scope/ACL validation gate.
- **AD-10:** default-deny secret access, verified TLS, out-of-band bootstrap, fail-closed runtime behavior, and support-safe diagnostics preserve the security posture.
- **AD-12:** real-OpenBao release evidence and parsed manifests supply production-path proof rather than mock-only confidence.
- **AD-18:** the Dapr API token is correctly classified as bootstrap input and remains compatible with the handler-owned outbound sidecar header rule.
- **AD-23:** operational/application secret retrieval remains explicitly separate from the draft-not-authorized payload-protection proposal and production `pdenc-v2` key custody (`architecture.md:316`). No AD-23 authority is approved, replaced, or weakened.

No other AD, convention, diagram, capability mapping, or Deferred item contradicts AD-24.

## Good-Spine Checklist

| Check | Result | Notes |
| --- | --- | --- |
| Fixes the real divergence points one level down | **Pass** | Provider, owner, contract, scope layers, bootstrap, lifecycle, rotation, failure, testing, and environment support are fixed. |
| Misses no owned divergence point | **Pass** | Every secret-management dimension at feature altitude is decided or assigned to one downstream owner. |
| Every AD Rule is enforceable and prevents its stated divergence | **Pass** | Rules have manifest, readiness, rotation, integration, and forbidden-fallback evidence points. |
| Deferred contains no divergence-producing architecture decision | **Pass** | Deferred values are environment-specific overlay data under a single owner and canonical contract. |
| Named technology is verified-current | **Pass** | Official Dapr catalog confirms OpenBao Stable v1 since runtime 1.16 through `secretstores.hashicorp.vault`; runtime and SDK pins are separate. |
| Ratifies rather than contradicts brownfield reality | **Pass with handoff** | CI/deploy runtime 1.18.0 is ratified; existing deployment guides are now stale against the explicit target decision and must be updated during implementation. |
| Covers driving requirements/spec capabilities | **Pass** | FR34, NFR4, NFR17, and Story 7.6 are bound, mapped, and made testable. |
| Does not weaken inherited invariants | **Pass** | No parent spine is inherited; AD-9/10/12/18/23 remain intact or stronger. |
| Every feature-altitude dimension is decided, deferred, or open | **Pass** | Deployment/environment, infra/provider, security, operations, ownership, failure, release evidence, and development parity are all represented. |

## Non-blocking Handoffs

- Reconcile `docs/guides/deployment-kubernetes.md`, `docs/guides/deployment-azure-container-apps.md`, `docs/guides/deployment-progression.md`, and `docs/guides/security-model.md` with ACA's non-conforming status, OpenBao ownership, and the 1.18.x runtime baseline.
- Create `deploy/dapr/openbao-secret-contract.yaml` and its validation/integration evidence in Story 7.6; its absence today is target-state implementation work, not an architecture ambiguity.

These handoffs do not leave a critical or high architecture finding.
