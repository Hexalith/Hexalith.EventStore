---
title: Architecture Reviewer Gate - August 16 Update Rubric Walker
reviewed_artifact: _bmad-output/planning-artifacts/architecture/architecture-eventstore-2026-07-05/ARCHITECTURE-SPINE.md
review_type: good-spine-rubric-walker
date: 2026-08-16
verdict: hold
critical_findings: 1
high_findings: 3
medium_findings: 1
low_findings: 1
---

# Architecture Reviewer Gate - August 16 Update Rubric Walker

## Gate Verdict

**HOLD — the August 16 AD-11/AD-22/FR36 direction is substantively correct, but the spine is not yet a safe final build substrate because one listed downstream source still instructs the opposite Story 3.13 outcome, AD-22 does not bind the exact failed evidence subject, the Stack has drifted from the authoritative brownfield catalog, and the claimed write-once event invariant is contradicted by Deferred.**

The deterministic lint pass is clean (`0` findings), and the feature-altitude operational envelope is present. Critical and high findings below must be resolved before the Reviewer Gate can pass.

## Review Scope And Evidence

Reviewed the complete spine, with focused comparison of AD-11, AD-22, and the FR36 capability map against:

- `_bmad-output/planning-artifacts/prd.md` (updated 2026-08-16)
- `_bmad-output/planning-artifacts/sprint-change-proposal-2026-08-16.md`
- the spine-declared `_bmad-output/planning-artifacts/epics.md`
- `_bmad-output/implementation-artifacts/sprint-status.yaml`
- `references/Hexalith.Builds/Props/Directory.Packages.props`
- `global.json`, `.github/workflows/release.yml`, `Directory.Build.targets`, and current source/project paths
- the current official Dapr v1.18 OpenBao and secret-store component pages
- the official NuGet v3 package index for disputed package versions

Mechanical command:

```text
uv run .agents/skills/bmad-architecture/scripts/lint_spine.py --workspace _bmad-output/planning-artifacts/architecture/architecture-eventstore-2026-07-05
Result: ok=true, total_findings=0
```

## Critical

### C1 - The final spine and its listed epic source prescribe incompatible Story 3.13 outcomes

- **Evidence:** The spine's AD-22 (`ARCHITECTURE-SPINE.md:321-336`) and FR36 map (`:561`) correctly make Story 3.13 a rejected/non-authorizing v3.94.1 disposition, Story 3.14 the corrective release, and Story 3.15 the independent positive deployed-runtime closure. The updated PRD says the same (`prd.md:254`, `:363`, `:413`, `:442`). However, the spine lists `epics.md` as a source (`ARCHITECTURE-SPINE.md:16`), while `epics.md:1893-1927` still defines Story 3.13 as positive deployed-runtime parity for exact v3.94.1 and permits it to become `done` with a deployed identity. `sprint-status.yaml:215-219` likewise retains the pre-correction Story 3.13 identity and says it awaits the v3.94.1 positive packet; Stories 3.14 and 3.15 are absent.
- **Divergence:** One implementation unit following the final spine/PRD must reject v3.94.1 and defer positive closure to 3.15. Another following the spine-declared epic source is told to approve v3.94.1 positively in 3.13. The latter directly weakens AD-11/AD-22 and can misclassify non-authorizing evidence.
- **Proposal conflict:** The approved proposal requires the PRD, epic, architecture, story identities, and sprint tracker to change atomically (`sprint-change-proposal-2026-08-16.md:107-119`, `:363-369`, `:434-442`).
- **Action:** **Discuss/coordinate; block final handoff.** Apply the already-approved Story 3.13/3.14/3.15 replan to `epics.md`, story/spec identity, and sprint tracking atomically, or explicitly keep the spine non-final until that handoff is complete. Do not solve this by weakening AD-11/AD-22 or restoring v3.94.1 positive closure.

## High

### H1 - AD-22 does not bind Story 3.13 to the exact immutable failed-evidence subject and failure facts

- **Evidence:** The approved architecture replacement requires the disposition to preserve the exact v3.94.1 failure facts—malformed `source`/`url`/`documentation`, absent `revision`, `deployment_authorized: false`—and bind the disposition to evidence subject `6cee8dad34c1233c6184404b409fb65d1a4dd0bccdd0d0ee54e8869120970a97` (`sprint-change-proposal-2026-08-16.md:197-234`, `:339-347`). AD-22's amendment (`ARCHITECTURE-SPINE.md:325-336`) records the source/release/package lineage and negative outcome but omits both that subject identity and the concrete immutable failure facts.
- **Divergence:** Two Story 3.13 units can both claim a "content-bound negative disposition" while binding different content or omitting the label defects that made v3.94.1 non-authorizing. The Rule therefore does not fully prevent the evidence substitution it states it prevents.
- **Action:** **Autofix.** Amend the existing dated AD-22 paragraph in place (stable AD ID) to cite the exact subject digest and require preservation of the three malformed URI labels, missing revision, and false deployment authority. Keep AD-11's later-version-only correction rule unchanged.

### H2 - The Stack is not the current brownfield planning baseline

- **Evidence:** The Stack calls itself the current planning baseline (`ARCHITECTURE-SPINE.md:456-481`) but differs from the sole authoritative catalog mandated by AD-11. Current `references/Hexalith.Builds/Props/Directory.Packages.props` resolves:
  - `CommunityToolkit.Aspire.Hosting.Dapr` `13.4.1-beta.706`, not `.687`;
  - ASP.NET Core/SignalR `10.0.11`, not `10.0.10`;
  - `HexalithFrontComposerVersion` `4.1.1`, not `4.0.1`;
  - `OpenTelemetry.Instrumentation.StackExchangeRedis` `1.17.0-beta.1`, not `1.16.0-beta.1`;
  - `HexalithCommonsVersion`/UniqueIds `2.30.0`, not `2.28.2`;
  - `NSubstitute` `6.2.0`, not `6.0.0`.
  The official NuGet v3 index confirms that `.706`, `6.2.0`, and `10.0.11` exist. `global.json` still confirms SDK `10.0.302`.
- **Divergence:** A unit following the Stack selects versions different from a unit following AD-11's declared sole catalog authority. AD-21 is specifically affected because it calls the Stack's `4.0.1` FrontComposer value current while the governed family is `4.1.1`.
- **Action:** **Autofix.** Re-distill the Stack from the current catalog and make AD-11's ASP.NET security-baseline wording consistent with the accepted `10.0.11` family evidence. Prefer describing the catalog as the live authority so ordinary catalog refreshes do not make a finalized spine contradict itself.

### H3 - AD-5 claims adopted write-once event persistence while Deferred records that the brownfield provider permits overwrite

- **Evidence:** AD-5 is `[ADOPTED]` and says `AggregateActor` persists write-once events (`ARCHITECTURE-SPINE.md:109-113`). Deferred states that the tested Dapr/Redis profile durably wrote sequence 1 and then silently overwrote the same actor-state key, with no exception or retry, and that no portable fence has been selected (`:575-576`). FR31 requires a verify-first conflict spike, and NFR7 forbids silent data loss.
- **Divergence:** One event-mutation slice can rely on the adopted "write-once" claim; another can treat overwrite as current provider behavior and invent a local fence. Both readings are supported by the same spine, and the risk is silent event loss/corruption.
- **Action:** **Discuss, then tighten.** Until the provider-portable fence is approved and proven, mark write-once enforcement as an explicit open blocker for append-remediation work rather than a harmless Deferred detail. Amend AD-5 so its adopted portion names logical single-writer ownership without asserting unproved physical write-once enforcement, and gate any affected story from selecting its own fence.

## Medium

### M1 - Structural Seed presents future artifacts as present brownfield structure

- **Evidence:** The seed lists `src/Hexalith.EventStore.PayloadProtection/` and `deploy/dapr/openbao-secret-contract.yaml` (`ARCHITECTURE-SPINE.md:483-507`), but neither path exists. The first is gated post-MVP work under AD-23; the second remains Story 7.6 implementation work under AD-24.
- **Divergence:** Builders can treat those paths as existing extension seams while others correctly create them later under their gated stories. This violates the spine rule that structural seed is true at cold start and then code-owned.
- **Action:** **Autofix.** Remove nonexistent paths from Structural Seed or label them unambiguously as future/gated targets outside the current seed; retain their ownership and gates in AD-23/AD-24.

## Low

### L1 - The `source` provenance URI constraint is slightly less explicit than the approved proposal

- **Evidence:** AD-11 requires `org.opencontainers.image.source` to be the exact public EventStore repository URL, while explicitly saying only `.url` and `.documentation` are absolute public HTTPS URLs (`ARCHITECTURE-SPINE.md:153`). The proposal requires all three to be absolute public HTTPS URIs (`sprint-change-proposal-2026-08-16.md:329-337`). Current repository reality uses `https://github.com/Hexalith/Hexalith.EventStore`.
- **Action:** **Autofix during H1/H2 polish.** State that `.source` is also the exact absolute public HTTPS EventStore repository URI.

## Confirmed Passes

- **August 16 direction:** AD-11 correctly requires identical five-label provenance across both platform configs, raw registry/digest/config checks, both child smokes, immutable failed releases, and correction only by a later semantic version.
- **FR36 capability coverage:** The capability map correctly separates Story 1.20 source/package closure from Story 3.13 negative disposition, Story 3.14 corrective release, and Story 3.15 positive deployed-runtime closure, governed by AD-11, AD-12, and AD-22. No stale positive v3.94.1 claim survives inside the spine itself.
- **Release authority:** Story or tag completion alone grants no deployment or consumer-removal authority; AD-22 still requires owner-approved exact dependency/runtime identity.
- **Operational/environmental envelope:** Deployment and environment topology are covered by AD-9; OCI publication/deployment identity by AD-11/AD-22; fail-closed probes by AD-16; production secret-provider, scoping, rotation, readiness, development profile, and unsupported ACA posture by AD-24; release and persisted evidence by AD-12. Exact environment values have one overlay owner and a revisit location rather than multiple competing owners.
- **Current technology fit:** Official Dapr v1.18 documentation confirms OpenBao is Stable component v1 since runtime 1.16 and uses `secretstores.hashicorp.vault` v1; AD-24's technology choice remains current. The Dapr runtime and .NET SDK/package distinctions remain coherent.
- **Parent/inherited consistency:** No parent spine is declared or inherited, so there is no parent AD conflict to evaluate. The repository's DAPR-backed event-sourcing and package-catalog conventions are otherwise preserved.
- **Mechanics:** No placeholder, duplicate AD ID, missing Binds/Prevents/Rule, or unpinned Stack row was found by `lint_spine.py`.

## Checklist Summary

| Good-spine criterion | Result |
| --- | --- |
| Fixes all real divergence points one level down | **Fail** — C1 and H3 remain active divergence paths. |
| Every Rule is enforceable and matches Prevents | **Fail** — AD-22 lacks the exact evidence binding; AD-5 overstates current enforcement. |
| Deferred cannot cause incompatible builds | **Fail** — append fencing is a data-integrity decision presented as deferrable while AD-5 claims it already holds. |
| Named technology is verified-current | **Fail** — OpenBao is current, but six Stack entries conflict with the live catalog. |
| Ratifies brownfield reality | **Fail** — Stack and Structural Seed drift; core paradigm and most boundaries pass. |
| Covers the PRD delta | **Pass inside the spine; fail across its declared source set** — the FR36 split lands, but `epics.md`/tracker remain opposite. |
| Parent/inherited consistency | **N/A / pass** — no parent spine is declared. |
| Covers every feature-altitude dimension | **Pass** — deployment, environments, infrastructure/provider strategy, operations, security, state, integration, release, UX boundary, and evidence are decided or assigned. |

## Required Gate Closure

1. Atomically reconcile `epics.md`, Story 3.13/3.14/3.15 identity, and sprint tracking with the approved proposal.
2. Tighten AD-22 to the exact immutable failed-evidence subject and facts.
3. Refresh the Stack from the authoritative catalog.
4. Resolve the AD-5/write-once contradiction by making append fencing an explicit gated blocker until proven.
5. Correct Structural Seed future-path claims.

After those changes, rerun deterministic lint and this rubric; the August 16 release/parity decision itself does not need renegotiation.
