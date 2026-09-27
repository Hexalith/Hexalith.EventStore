---
title: 'Story 6.5c: Publication, Subscription, and Rollout Spec'
type: 'feature'
created: '2026-09-27'
status: 'backlog'
context:
  - '{project-root}/_bmad-output/implementation-artifacts/epic-6-context.md'
---

## Intent

Specify stable publication and effect bytes, subscriber admission, delivery disposition, and mixed-version rollout as an input to the single Story 6.5 AD-13 artifact. This story changes no runtime behavior.

## Boundaries & Constraints

One committed MessageId retains one pinned publication identity through retry. Every addressed logical route has a durable completed or authenticated filtered result before physical acknowledgement. Poison, unavailable broker/provider evidence, or a failed route never becomes silent success. Existing signed fixture literals and public compatibility remain intact; no independent approval of Story 6.6 is granted.

## Tasks & Acceptance

- [ ] Inventory outbox, broker, transport, subscription, marker, effect, dead-letter, and operator-evidence paths in this slice.
- [ ] Propose exact publication/delivery codecs and pins, membership and route authority, per-route effect receipts, duplicate and poison handling, legacy JSON-to-binary handoff, and bounded capture/readback.
- [ ] Propose reader-first and writer-cutover readiness, provider probes, key retention, mixed fleet rollback, package compatibility, and support-safe diagnostics.
- [ ] Resolve `BH37-9`'s historical UTC-ticks versus original-offset wording with one normative delivery-digest rule and a byte-level vector; record its explicit disposition.

**Acceptance Criteria:**

- Given initial publication, redelivery, multi-route subscription, or legacy handoff, when bytes and effects are verified, then the same MessageId preserves exact authenticated evidence, each route has at most one effect, and acknowledgement waits for the complete addressed route set.
- Given changed bytes, membership, keys, broker claims, or a malformed/oversized carrier, when rollout or delivery proceeds, then readiness or admission holds with a typed outcome and no silent acknowledgement or payload disclosure.
- Given Story 6.5 integration, when this work is reviewed, then its section candidate and finding disposition are ready for reconciliation with Stories 6.5a and 6.5b; this child story alone authorizes no runtime work.

## Verification

Review the candidate against the current source inventory and the exact `BH37-9` triage row. Check signed fixture preservation, digest offset bytes, duplicate/redelivery, multi-route crash, legacy handoff, poison capture, lease/key rotation, and rollout vectors; keep the AD-13 receipt `UNAPPROVED`.
