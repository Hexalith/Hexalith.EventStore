---
title: Sprint Change Proposal — Health Endpoint Anonymous-Access Contract (AD-16)
date: 2026-07-07
author: Administrator (Correct-Course workflow)
change_scope: Moderate
status: approved
owner: Winston (System Architect)
triggered_by:
  - _bmad-output/implementation-artifacts/sprint-status.yaml (Epic 1 open action)
  - _bmad-output/implementation-artifacts/deferred-work.md (spec-1-6 deferred entry)
  - _bmad-output/implementation-artifacts/epic-1-retro-2026-07-07.md (action #4)
  - _bmad-output/implementation-artifacts/epic-2-retro-2026-07-07.md
artifacts_modified:
  - _bmad-output/planning-artifacts/architecture.md (new invariant AD-16 + convention + capability map)
  - _bmad-output/planning-artifacts/epics.md (Stories 5.3, 5.5, 7.3 AC rewrites)
  - _bmad-output/planning-artifacts/prd.md (NFR1 clarification + coverage row)
  - _bmad-output/implementation-artifacts/deferred-work.md (entry resolved)
  - _bmad-output/implementation-artifacts/sprint-status.yaml (action done)
---

# Sprint Change Proposal — Health Endpoint Anonymous-Access Contract

## Section 1 — Issue Summary

**Problem statement.** The EventStore health/liveness/readiness probe endpoints (`/health`, `/alive`,
`/ready`, mapped by `ServiceDefaults.MapDefaultEndpoints`) are reachable anonymously **only by accident**:
no global fallback authorization policy exists yet, so unauthenticated probe calls currently succeed. The
Phase 4 security stories (5.3 production auth guards, 5.5 domain-service trust boundary, 7.3 deployment
hardening) all drive toward a fail-closed default (AD-10 / NFR1). The idiomatic ASP.NET Core implementation
of "fail closed by default" is `AuthorizationOptions.FallbackPolicy = RequireAuthenticatedUser()`, which
would **silently capture** every endpoint without explicit `AllowAnonymous`, including the probe endpoints.

**Two-directional hazard.**
1. **Availability break:** a fallback policy blocks Kubernetes liveness/readiness probes, DAPR app-health
   checks (`/alive` is the configured `DaprAppHealthCheckPath`), and Aspire orchestration — DAPR would mark
   modules unhealthy.
2. **Fail-open regression (worse):** the tempting "fix" for broken probes is to weaken or remove the
   fallback policy — which re-opens fail-open across **every** endpoint, the exact NFR1 violation AD-10
   exists to prevent.

A competing constraint is that anonymous probe responses must remain **support-safe** — no component names,
dependency detail, versions, tenant data, or exception text disclosed to anonymous callers outside
Development.

**Discovery / context.** This was surfaced by the Story 1.6 follow-up review (which switched the Aspire
domain-module app-health default from `/ready` to `/alive`) and recorded as a `[medium]` deferred item in
`spec-1-6-sample-and-tenants-domain-centric-adoption-2.md`. It is a **verbatim open Epic 1 action**
(owner Winston, `sprint-status.yaml`), a `deferred-work.md` ledger entry, and an item in both the Epic 1 and
Epic 2 retros. It is a **sequencing/contract-definition gap**, not a live production defect: no fallback
policy exists in the codebase today.

**Evidence.**
- `src/Hexalith.EventStore.ServiceDefaults/Extensions.cs:179-181` — `MapHealthChecks('/health' | '/alive' | '/ready')` with **no `.AllowAnonymous()`**; consumed by every host via `MapDefaultEndpoints`.
- No `FallbackPolicy` / `RequireAuthenticatedUser` anywhere in `src`/`samples` — auth is plain `AddAuthorization()`.
- `src/Hexalith.EventStore.Aspire/HexalithEventStoreDomainModuleExtensions.cs:14` — `DaprAppHealthCheckPath = "/alive"`.
- `_bmad-output/implementation-artifacts/D-5-proof-sample-blazorui-queries.md:329` — `Sample.Api` "no fail-closed FallbackPolicy … a fallback policy is optional defense-in-depth" (the seam where a future fallback would land).
- `_bmad-output/implementation-artifacts/deferred-work.md:90-92`; `sprint-status.yaml` Epic 1 action.

## Section 2 — Impact Analysis

### Epic impact
- **Epic 5 (Security & Tenant Isolation)** and **Epic 7 (Operator Trust)** remain completable as planned;
  they gain a precondition, not a redefinition. No epic added, removed, or resequenced.
- **Sequencing constraint added** (contract lands *before or with* any fallback policy) — this is a gate,
  not a re-order of epic delivery.

### Story impact
| Story | Change |
| --- | --- |
| 5.3 Production Authentication Guards | Owns the ordering gate; +2 AC blocks (probe-anonymity ordering + positive-probe/negative-protected-endpoint evidence). |
| 5.5 Internal & Domain-Service Trust Boundary | +1 AC scoping credential enforcement to canonical DAPR endpoints and exempting the probes (its host maps `MapDefaultEndpoints` beside `MapEventStoreDomainService`). |
| 7.3 Production Deployment Hardening | Health-check AC extended with an anonymity clause so DAPR app-health assumes the AD-16 exemption. |

### Artifact conflicts
- **Architecture:** AD-10 was silent on probe endpoints → new invariant **AD-16** + Consistency-Conventions
  row + Capability-map bindings.
- **PRD:** NFR1 asserted fail-closed with no probe carve-out → clarified; Story 7.3 added to NFR1 coverage.
- **UX:** N/A — no UI surface (the Admin UI `/health` *page* is unrelated to the transport probe endpoints).
- **Ledgers:** `deferred-work.md` entry resolved; `sprint-status.yaml` action → `done`.

### Technical impact
No production code change is required **now** because no fallback policy exists. The implementation seam is
`ServiceDefaults.MapDefaultEndpoints` (add explicit `AllowAnonymous()` to the three health-check endpoints)
plus a positive/negative authorization test — delivered inside Stories 5.3 / 5.5 / 7.3 when the fail-closed
default is actually introduced.

## Section 3 — Recommended Approach

**Selected path: Option 1 — Direct Adjustment (Hybrid).** Effort **Low**, Risk **Low**.

- *Option 2 (Rollback):* Not viable — nothing to revert.
- *Option 3 (MVP review):* N/A — no scope reduction; MVP unchanged.

**Rationale.** The cheapest, highest-leverage moment to pin this contract is *before* the fallback policy
exists. Authoring the invariant + story gates now prevents both the availability break and the fail-open
regression, and directly mirrors the accepted **AD-15 route-provenance** precedent (contract authored as an
architecture invariant + forward-story gate, owner Winston, resolved via a sprint-change-proposal). The
approach preserves AD-10 fail-closed intent while making the single support-safe anonymous exception
explicit and testable.

**Correct-Course Story Rewrite Gate — satisfied.** This change supersedes active/planned story acceptance
criteria (Stories 5.3, 5.5, 7.3). Explicit old→new AC rewrites for all three are included in Section 4.

## Section 4 — Detailed Change Proposals

### 4.1 Architecture — new invariant AD-16 (`architecture.md`)

New invariant inserted after AD-15:

> ### AD-16 - Health And Probe Endpoints Are Explicitly Anonymous And Fail-Closed-Compatible [ADOPTED]
> - **Binds:** FR26, FR28, FR34, NFR1, NFR3, NFR17
> - **Prevents:** (a) a global fallback authorization policy silently blocking liveness/readiness/DAPR app-health probes, and (b) the inverse regression where a broken probe is "fixed" by weakening or removing the fallback policy, re-opening fail-open across every endpoint.
> - **Rule:** `/health`, `/alive`, `/ready` (from `ServiceDefaults.MapDefaultEndpoints`) are the only explicit anonymous exception to AD-10 fail-closed. They are (1) pinned anonymous by explicit `AllowAnonymous`, (2) support-safe (status/outcome only outside Development), and (3) ordering-gated — any fallback/deny-by-default policy lands in the same-or-earlier slice and is never weakened to reach probes.
> - **Evidence:** A real-host-pipeline test asserts (i) probes reachable unauthenticated and (ii) a representative protected endpoint denies unauthenticated (AD-12 / NFR16).
>
> AD-16 refines AD-10.

Supporting edits: a "Health probes" row added to the Consistency Conventions table; `AD-16` appended to the
capability-map rows for `FR26, FR28, FR32` and `FR34-FR35`.

### 4.2 Story 5.3 rewrite (`epics.md`) — OLD → NEW

Added under `**Requirements covered:** FR26`:

> **Architecture gate:** AD-16 (health/probe anonymous-access contract). If this story or any host introduces
> a global fallback authorization policy or default-deny endpoint convention, the explicit probe-anonymity
> contract lands in the same or an earlier slice — never after.

Two AC blocks inserted before the existing `Given authentication guard tests run` block:

> **Given** a host introduces a global fallback authorization policy or a default-deny endpoint convention as part of fail-closed hardening
> **When** that policy is applied
> **Then** the health, liveness, and readiness endpoints `/health`, `/alive`, and `/ready` are explicitly pinned `AllowAnonymous` (or an equivalent auth-exempt convention) in the same or an earlier slice
> **And** the fallback policy or deny-by-default posture is not weakened, scoped down, or removed to make probes reachable (AD-16).
>
> **Given** the fail-closed default is active on a host
> **When** health-endpoint authorization is exercised on the real host pipeline
> **Then** `/health`, `/alive`, and `/ready` return their health status to an unauthenticated caller
> **And** a representative protected endpoint on the same host denies an unauthenticated caller
> **And** anonymous probe responses remain support-safe, disclosing no component names, dependency detail, versions, tenant data, or exception text outside Development.

*(Existing 5.3 ACs — Admin UI secret stripping, out-of-Development startup guard, JWT HTTPS/algorithm
pinning, dev-fallback/production-fail tests — are unchanged.)*

### 4.3 Story 5.5 rewrite (`epics.md`) — OLD → NEW

Added under `**Requirements covered:** FR28`:

> **Architecture gate:** AD-16 (health/probe anonymous-access contract). The domain-service credential
> enforcement covers only the canonical DAPR endpoints; the probe endpoints stay explicitly anonymous and
> support-safe.

AC inserted after the `...unauthenticated network-local callers cannot execute domain operations.` block:

> **Given** the domain-service SDK enforces app-layer credentials on its canonical endpoints
> **When** the credential requirement is applied, including any fallback or default-deny policy on the host
> **Then** the requirement covers `/process`, `/replay-state`, `/query`, `/project`, and `/admin/operational-index-metadata`
> **And** the health, liveness, and readiness endpoints `/health`, `/alive`, and `/ready` remain explicitly `AllowAnonymous` and support-safe so DAPR app-health and orchestration probes are not blocked (AD-16).

### 4.4 Story 7.3 rewrite (`epics.md`) — OLD → NEW

Added under `**Requirements covered:** FR34`:

> **Architecture gate:** AD-16 (health/probe anonymous-access contract). Enabling DAPR app-health/readiness
> probes assumes the probe endpoints are reachable anonymously; if a fail-closed policy is present, the AD-16
> exemption must already be in place.

Health-check AC extended (final clause added):

> **And** the probe endpoints `/health`, `/alive`, and `/ready` are reachable anonymously per the AD-16
> contract even when a fail-closed authorization policy is active.

### 4.5 PRD NFR1 (`prd.md`) — OLD → NEW

> **OLD:** … no endpoint may rely only on network posture or caller-supplied admin flags.
>
> **NEW:** … no endpoint may rely only on network posture or caller-supplied admin flags. The only anonymous
> exception is the health/liveness/readiness probe endpoints (`/health`, `/alive`, `/ready`), which are
> explicitly pinned `AllowAnonymous` and support-safe (AD-16); the fail-closed default is never weakened to
> reach probes.

NFR1 story-coverage row: `5.2, 5.3, 5.5, 7.2` → `5.2, 5.3, 5.5, 7.2, 7.3`.

### 4.6 Ledger closure

- `deferred-work.md` health entry: `resolution:` line added citing AD-16 + this proposal + enforcing stories;
  `status: RESOLVED 2026-07-07`.
- `sprint-status.yaml` Epic 1 action: `status: open` → `done` with note.

## Section 5 — Implementation Handoff

**Change scope classification: Moderate** (planning-artifact reorganization + forward-story gating; no
immediate code change).

| Recipient | Responsibility |
| --- | --- |
| **Winston (System Architect)** | Owns AD-16 and this proposal (authored). No further architecture action; AD-16 is `[ADOPTED]`. |
| **Amelia (Developer)** | When implementing Stories 5.3 / 5.5 / 7.3: add explicit `AllowAnonymous()` to the three `MapHealthChecks` endpoints in `ServiceDefaults.MapDefaultEndpoints` **in the same or an earlier slice** as any fallback/deny-by-default policy; add the positive-probe / negative-protected-endpoint authorization test on the real host pipeline; keep anonymous probe responses support-safe (Development-only rich writer). |
| **John (Product Manager) / PO** | Confirm ledger closure; no MVP or backlog scope change. |

**Success criteria.**
- `MapDefaultEndpoints` probe endpoints carry explicit `AllowAnonymous` before/with the first fallback policy.
- A test proves probes reachable unauthenticated **and** a protected endpoint denies unauthenticated on the
  same host.
- No fallback/deny-by-default policy is weakened to reach probes.
- Anonymous probe responses disclose no support-unsafe detail outside Development.

**Dependencies / sequencing.** AD-16 is a precondition of the fail-closed work in Stories 5.3, 5.5, 7.3.
It does not block any story from starting; it constrains the *order of edits within* those stories.
