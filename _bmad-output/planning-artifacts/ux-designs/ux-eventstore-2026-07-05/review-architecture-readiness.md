# Architecture And Readiness Review - eventstore

## Overall verdict

The UX direction aligns with the architecture decisions that matter most: UI hosts consume client libraries, SignalR is only freshness evidence, success is projection-confirmed, security fails closed, and deferred operations are hidden/disabled or backed by `501`. The readiness problem is packaging and traceability: the required `ux.md` handoff path is not present, and Sample/Tenants/Admin source requirements need explicit mapping.

## Findings

- **high** The PRD defines the UX artifact as `_bmad-output/planning-artifacts/ux.md`, but the generated spines currently live only under the run folder (`prd.md:79`, `prd.md:352`; `_bmad-output/planning-artifacts/ux-designs/ux-eventstore-2026-07-05/DESIGN.md`; `_bmad-output/planning-artifacts/ux-designs/ux-eventstore-2026-07-05/EXPERIENCE.md`). A readiness scanner can still report "UX not found." *Fix:* Create `_bmad-output/planning-artifacts/ux.md` as the canonical handoff, either by composing the two spines or by linking to them with an explicit accepted artifact map.
- **high** Sample UI and Tenants UI are source-required surfaces, but the current UX spines are dominated by the EventStore Admin dashboard (`prd.md:48`, `prd.md:243-244`, `prd.md:352`; `epics.md:580`, `epics.md:610-633`). *Fix:* Add explicit sections or flows for Sample accepted-submission behavior and Tenants projection-confirmed/support-safe behavior so stories 2.3 and 2.4 can implement against the UX artifact.
- **medium** `Deferred & Backlog` is valid as honest capability discovery, but its visibility policy is underspecified against NFR15 and AD-10 (`architecture.md:103-107`, `prd.md:212-213`, `EXPERIENCE.md:45`, `EXPERIENCE.md:98`, `EXPERIENCE.md:195-203`). If the tab shows deferred operations broadly, it could still look like a feature catalog. *Fix:* Define when unavailable operations are hidden, when they are disabled, and which roles may see backlog-only entries.
- **medium** The UX spines do not include a source traceability table for NFR14, NFR15, FR34, AD-4, AD-8, AD-10, and AD-14 (`architecture.md:67-71`, `architecture.md:91-95`, `architecture.md:127-131`, `prd.md:212-213`, `epics.md:131-133`). *Fix:* Add a small traceability table mapping each decision/requirement to UX states, components, flows, and test evidence.
- **low** The implementation readiness report was created before the new UX spines and is now stale in its PRD/architecture discovery, but its UX warning still applies to the canonical path (`implementation-readiness-report-2026-07-05.md:52-64`, `implementation-readiness-report-2026-07-05.md:162-189`). *Fix:* Re-run readiness after publishing the canonical `ux.md`.

## Alignment notes

- AD-4 is honored by the UX rule that UI hosts consume EventStore Client libraries and do not host generated or hand-written per-message controllers.
- AD-8 is honored by the accepted/evidence-pending/projection-confirmed state model.
- AD-10 is honored by fail-closed access and support-safe admin mutations.
- AD-14 is honored by rendering freshness and confirmation from gateway metadata rather than ad hoc payload fields.
