# Sprint Change Proposal - Epic 4 Runtime Confidence Follow-Through

**Date:** 2026-04-28
**Author:** Bob (Scrum Master)
**Project Lead:** Jerome
**Project:** Hexalith.EventStore
**Trigger:** Epic 4 retrospective handoff and current-state verification of remaining cleanup items
**Mode:** Batch
**Scope Classification:** Moderate - backlog follow-through and verification gating. No PRD, epic, or architecture redefinition is required.

---

## 1. Issue Summary

Epic 4 delivered the event distribution backbone: CloudEvents publication, tenant/domain topic routing, resilient backlog draining, and per-aggregate backpressure. The implementation direction remains valid. The course correction is needed because runtime confidence and artifact evidence are still incomplete while later security work depends on Epic 4 behavior.

Current verified state:

| Finding | Evidence | Current Status |
|---|---|---|
| Story 4.2 status drift is corrected | `_bmad-output/implementation-artifacts/4-2-resilient-publication-and-backlog-draining.md` now says `Status: done` | Closed |
| Story 4.3 execution evidence remains thin | `_bmad-output/implementation-artifacts/4-3-per-aggregate-backpressure.md` still contains story and acceptance criteria only | Open: `post-epic-4-r4a2-story-4-3-execution-record` |
| Replay still GUID-validates replay correlation IDs | `src/Hexalith.EventStore/Controllers/ReplayController.cs` still uses `Guid.TryParse(correlationId)` and returns "not a valid GUID format" | Open: `post-epic-3-r3a1-replay-ulid-validation` |
| Live command-surface verification remains backlog | `sprint-status.yaml` keeps `post-epic-3-r3a7-live-command-surface-verification: backlog` | Open |
| Tier 3 pub/sub delivery coverage remains backlog | `sprint-status.yaml` keeps `post-epic-4-r4a5-tier3-pubsub-delivery: backlog` | Open |
| Drain integrity guard remains backlog | `sprint-status.yaml` keeps `post-epic-4-r4a6-drain-integrity-guard: backlog` | Open |
| Old story numbering comment cleanup remains backlog | `sprint-status.yaml` keeps `post-epic-4-r4a8-story-numbering-comments: backlog` | Open |

Bob (Scrum Master): "The course correction is not about reopening Epic 4. Epic 4 stands. The correction is to stop downstream runtime-security confidence from resting on incomplete recovery evidence."

---

## 2. Checklist Execution Summary

| Checklist Section | Status | Notes |
|---|---|---|
| 1. Trigger and context | Done | Trigger is the Epic 4 retrospective handoff plus current code/status evidence |
| 2. Epic impact | Done | Epic 4 remains complete; Epic 5 evidence is affected because it relies on topic isolation, DAPR runtime behavior, and replay/recovery paths |
| 3. Artifact conflict and impact | Done | PRD, epics, and architecture already express the needed requirements; backlog execution and verification evidence need correction |
| 4. Path forward | Done | Direct adjustment is viable; rollback and MVP review are not justified |
| 5. Proposal components | Done | Specific execution and routing changes are listed below |
| 6. Final review and handoff | Action-needed | This proposal needs approval before sprint-status promotion or story creation work |

---

## 3. Impact Analysis

### Epic Impact

- **Epic 4: Event Distribution & Pub/Sub** remains complete. No acceptance criteria need removal or redefinition.
- **Epic 5: Security & Multi-Tenant Isolation** remains directionally correct, but final runtime-security confidence should be treated as provisional until the live command surface, replay identifier rule, and pub/sub delivery path are verified.
- **Epic 6 and later operations/admin work** may consume drain, pub/sub, and backpressure signals. Those epics should not assume drain integrity and subscriber delivery are fully proven until the post-Epic-4 items are closed.

### Story Impact

Existing backlog entries remain authoritative:

| Story Key | Purpose | Recommendation |
|---|---|---|
| `post-epic-3-r3a1-replay-ulid-validation` | Fix replay identifier validation so ULIDs/non-GUID correlation IDs are not rejected | Execute first |
| `post-epic-3-r3a7-live-command-surface-verification` | Verify `/swagger`, command submission, status query, replay, and ProblemDetails against running AppHost | Execute immediately after replay fix |
| `post-epic-4-r4a2-story-4-3-execution-record` | Reconstruct Story 4.3 task checklist, dev record, file list, change log, and verification summary | Execute before future agents rely on Story 4.3 |
| `post-epic-4-r4a6-drain-integrity-guard` | Prevent incomplete persisted event ranges from being reported as successful drain delivery | Execute before zero-loss recovery claims |
| `post-epic-4-r4a5-tier3-pubsub-delivery` | Prove command to persist to publish to subscriber delivery and drain re-publish in a running topology | Execute after command surface is healthy |
| `post-epic-4-r4a8-story-numbering-comments` | Normalize stale Story 4.4 / 4.5 comments to current story numbers or neutral feature names | Execute as low-risk cleanup |

### Artifact Conflicts

- **PRD:** No change required. FR17-FR20, FR67, NFR22, NFR24, NFR25, and NFR28 already require the behavior this correction protects.
- **Epics:** No change required. Epic 4 and Epic 5 definitions are correct.
- **Architecture:** No change required. D6 topic naming, ADR-P2 persist-then-publish, D11 Keycloak runtime verification, and Rule 16 already support the correction.
- **UX:** No immediate change required. Future admin degraded-event-distribution views may depend on the runtime evidence produced here.
- **Sprint status:** No new keys are required. The existing backlog keys are sufficient. Optional status promotion should happen only when story files are created and approved for development.

### Technical Impact

- The replay controller needs a targeted validation change away from GUID-only parsing.
- Runtime verification depends on Aspire/DAPR being available. Aspire MCP currently detects no running AppHost, so no live resource evidence was captured during this proposal.
- Drain recovery needs an explicit integrity policy when loaded event count differs from `UnpublishedEventsRecord.EventCount`.
- Story 4.3 documentation must be reconstructed from implementation and test evidence already present in the codebase.

---

## 4. Recommended Approach

**Selected path: Direct Adjustment.**

Rollback is not useful because Epic 4 behavior is coherent and aligned with PRD/architecture. MVP review is not needed because requirements remain achievable. The issue is execution discipline: existing cleanup stories must be promoted through the normal story workflow and treated as gates before relying on Epic 5 runtime-security evidence.

Recommended sequence:

1. Execute `post-epic-3-r3a1-replay-ulid-validation`.
2. Execute `post-epic-3-r3a7-live-command-surface-verification`.
3. Execute `post-epic-4-r4a2-story-4-3-execution-record`.
4. Execute `post-epic-4-r4a6-drain-integrity-guard`.
5. Execute `post-epic-4-r4a5-tier3-pubsub-delivery`.
6. Execute `post-epic-4-r4a8-story-numbering-comments`.

Risk level remains **medium** until replay and live command-surface verification are complete. It becomes **low** only when pub/sub runtime delivery and drain integrity have explicit evidence.

---

## 5. Detailed Change Proposals

### Proposal 1 - Promote Replay ULID Validation Fix as First Gate

```text
Story: post-epic-3-r3a1-replay-ulid-validation
Section: Priority / sequencing

OLD:
Backlog cleanup item from Epic 3 / Epic 4 retrospectives.

NEW:
First execution gate for Epic 4/Epic 5 runtime confidence. Replay must no longer use
Guid.TryParse for inbound replay correlation IDs. Tests must prove valid ULIDs or
non-GUID correlation IDs accepted by the platform rule are not rejected as invalid GUIDs.

Rationale:
Replay is an operational recovery path. Pub/sub recovery evidence is weaker while replay
still violates the platform identifier rule.
```

### Proposal 2 - Make Live Command-Surface Verification a Runtime Evidence Gate

```text
Story: post-epic-3-r3a7-live-command-surface-verification
Section: Acceptance criteria / execution notes

OLD:
Verify command surface before using Epic 3 as the baseline for Epic 4.

NEW:
Verify command surface before using Epic 5 runtime-security results as final proof.
Coverage must include Swagger, command submission, status query, replay surface, and
representative ProblemDetails paths against a running AppHost.

Rationale:
Security E2E evidence is only meaningful if the command API, replay, and error paths are
known healthy in the running topology.
```

### Proposal 3 - Reconstruct Story 4.3 Evidence Before Further Dependency

```text
Story: post-epic-4-r4a2-story-4-3-execution-record
Section: Story 4.3 documentation

OLD:
Story 4.3 has story text, acceptance criteria, and Definition of Done only.

NEW:
Add task checklist, dev agent record, implementation notes, final verification summary,
file list, and change log mapped to AC #1-#12.

Rationale:
Backpressure is part of the runtime behavior that protects the command pipeline. Future
agents need evidence, not inference.
```

### Proposal 4 - Add Drain Integrity Guard Before Zero-Loss Claims

```text
Story: post-epic-4-r4a6-drain-integrity-guard
Section: Acceptance criteria

OLD:
Drain can load a shorter persisted event range than UnpublishedEventsRecord.EventCount
and still publish the loaded events.

NEW:
Drain must detect loaded-count mismatch, preserve the drain record, increment retry or
emit an explicit operational signal, and include tests for missing first, middle, and
last events in the range.

Rationale:
At-least-once delivery and zero-loss recovery require incomplete state to be visible,
not converted into apparent success.
```

### Proposal 5 - Add Tier 3 Pub/Sub Delivery Proof After Command Surface Is Healthy

```text
Story: post-epic-4-r4a5-tier3-pubsub-delivery
Section: Verification scope

OLD:
Mocked DAPR publication tests verify publisher behavior, but subscriber delivery and
drain re-publish remain deferred.

NEW:
Running topology verifies command -> persist -> CloudEvents publish -> subscriber receipt,
and publish failure -> drain record -> recovery -> re-publish.

Rationale:
FR18, FR20, NFR22, NFR24, and NFR28 require runtime evidence beyond mocked DaprClient calls.
```

### Proposal 6 - Keep Story Numbering Cleanup Low-Risk and Non-Behavioral

```text
Story: post-epic-4-r4a8-story-numbering-comments
Section: Scope constraint

OLD:
Source comments reference old Story 4.4 / 4.5 numbering.

NEW:
Normalize comments to current story numbers or neutral feature names. No behavior changes.

Rationale:
Traceability cleanup is useful, but it must not distract from runtime confidence gates.
```

---

## 6. Sprint Status Proposal

No immediate `sprint-status.yaml` edit is required because the relevant backlog keys already exist:

```yaml
post-epic-3-r3a1-replay-ulid-validation: backlog
post-epic-3-r3a7-live-command-surface-verification: backlog
post-epic-4-r4a2-story-4-3-execution-record: backlog
post-epic-4-r4a5-tier3-pubsub-delivery: backlog
post-epic-4-r4a6-drain-integrity-guard: backlog
post-epic-4-r4a8-story-numbering-comments: backlog
```

After approval, the Scrum Master should create or refresh story files for the selected items, then move only those ready for implementation from `backlog` to `ready-for-dev`.

---

## 7. Implementation Handoff

**Scope:** Moderate.

**Bob / Scrum Master**

- Preserve existing routing and avoid duplicate stories.
- Prepare ready-for-dev story files in the recommended sequence.
- Keep Epic 5 runtime-security evidence marked provisional until replay and live command-surface gates close.

**Dev**

- Implement replay identifier validation correction.
- Implement drain integrity guard.
- Apply non-behavioral story-numbering comment cleanup.

**QA**

- Execute live command-surface verification.
- Design and run Tier 3 pub/sub delivery and drain recovery evidence.
- Separate environment limitations from product failures in test artifacts.

**Architect**

- Reassess only if Tier 3 verification reveals a structural limitation in topic isolation, DAPR access control, or drain recovery.

---

## 8. Success Criteria

The course correction is successful when:

1. Replay no longer rejects valid platform correlation IDs through GUID-only validation.
2. The running command surface is verified through Aspire/AppHost.
3. Story 4.3 contains a complete execution record and final verification summary.
4. Drain recovery cannot report success for an incomplete persisted event range.
5. Tier 3 pub/sub delivery and drain re-publish have explicit runtime evidence.
6. Stale Epic 4 story-number comments are normalized without behavior changes.
7. Epic 5 security/runtime evidence is no longer provisional.

Bob (Scrum Master): "This is the cleanest correction: keep the plan, execute the gates, and make the evidence match the architecture."
