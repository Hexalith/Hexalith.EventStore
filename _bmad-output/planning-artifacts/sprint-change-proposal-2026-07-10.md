---
title: "Sprint Change Proposal: EventStore Projection/Query SDK Owner Proof For Parties Story 8.6 AC1"
status: "approved"
created: "2026-07-10"
approved: "2026-07-10"
approved_by: "Administrator"
project: "eventstore"
trigger_artifact: "../parties/_bmad-output/implementation-artifacts/8-6-ac1-llm-instructions.md"
mode: "batch"
scope_classification: "moderate"
---

# Sprint Change Proposal: EventStore Projection/Query SDK Owner Proof For Parties Story 8.6 AC1

## 1. Issue Summary

Hexalith.Parties Story 8.6 AC1 is blocked because the Story 8.3 matrix row
`EventStore projection/query SDK` remains `needs-additive-api`. The referenced
LLM instructions state that AC1 is not fixed by editing status text alone. It is
fixed only when the matrix row records owner-approved EventStore proof that the
projection/query SDK can safely replace Parties-local projection and query
mechanics.

The EventStore repository already contains core SDK surfaces:

- `IDomainProjectionHandler`
- `IDomainQueryHandler`
- `IReadModelStore`
- `ReadModelWritePolicy`
- `IQueryCursorCodec`
- `QueryCursorScope`
- domain-service endpoint mapping and handler discovery through
  `AddEventStoreDomainService()` / `UseEventStoreDomainService()`

However, the cross-repo Parties gate requires stronger owner proof than the
existence of those source files. It specifically requires evidence for:

- G3 read-model erasure hooks.
- G10 index batching or an approved SDK equivalent.
- G6 Parties freshness mapping for `Current`, `Stale`, `Rebuilding`,
  `Degraded`, `Unavailable`, and `LocalOnly`.
- Duplicate and out-of-order replay behavior.
- Full rebuild verification against aggregate replay.
- Cursor scope compatibility through `IQueryCursorCodec` and
  `QueryCursorScope`.
- Current EventStore commit SHA intended for Parties consumption.

Evidence found during this correct-course pass:

- EventStore HEAD inspected for this proposal:
  `ea5a120798ab82363f465890e77e123be3287707`.
- Parties Story 8.6 currently records an older submodule pin at creation:
  `0f428d0c914f2151aab15bb262f956a9630041dc`.
- `IReadModelStore` currently exposes `GetAsync`, `SaveAsync`, and
  `TrySaveAsync`, but no public delete/erase hook.
- `ReadModelWritePolicy` supports optimistic retry, `ApplyEventsAsync`, and
  `MergeAsync`, including multi-key aggregate/index write patterns in tests, but
  no explicit batch API was identified in the public read-model store contract.
- `ReadModelFreshnessState` currently covers `Unknown`, `Current`, `Aging`, and
  `Stale`; it does not directly model all Parties states listed above.
- Cursor scope and DataProtection-backed cursor compatibility are strongly
  covered by existing `QueryCursorCodecTests` and `QueryCursorScopeTests`.

## 2. Impact Analysis

### Epic Impact

Epic 1 is affected because it owns the domain author self-service SDK seams
covered by FR4, FR5, FR6, FR7, and FR9. Epic 1 is currently marked `done`, but
the Parties AC1 gate exposes a cross-domain owner-proof gap after Epic 1
completion. The recommended adjustment is to add a post-completion Epic 1
follow-up story rather than reopen completed implementation stories and pretend
their original scope already covered Parties AC1.

Epic 2 is indirectly affected because generated REST and UI consumers rely on
query metadata and provenance, but this change does not alter Story 2.8 scope.

Epic 4 and Epic 6 are indirectly related through duplicate/out-of-order replay,
full rebuild, projection cost, and sequence guard semantics. No Epic 4 or Epic 6
scope is moved into the proposed owner-proof story; if the owner proof discovers
missing platform behavior that belongs there, it must record that as blocked or
route a separate follow-up.

### Story Impact

Affected EventStore planning/story artifacts:

- `_bmad-output/planning-artifacts/epics.md`
  - Add a new Epic 1 follow-up story: `Story 1.8: Projection/Query SDK Owner
    Parity Proof`.
- `_bmad-output/implementation-artifacts/sprint-status.yaml`
  - Reopen Epic 1 as `in-progress` only for the new follow-up story.
  - Add `1-8-projection-query-sdk-owner-parity-proof: ready-for-dev`.
- New implementation story file after approval:
  `_bmad-output/implementation-artifacts/1-8-projection-query-sdk-owner-parity-proof.md`.
- New proof packet after implementation:
  `_bmad-output/implementation-artifacts/1-8-projection-query-sdk-owner-proof-packet.md`.

Affected Parties artifacts:

- `../parties/_bmad-output/implementation-artifacts/8-6-projection-and-query-sdk-migration.md`
  - No rewrite is required now. Its AC1, status, and block-if language already
    correctly halt source migration.
- `../parties/_bmad-output/implementation-artifacts/story-8-3-platform-api-prerequisite-matrix.md`
  - Must remain `needs-additive-api` until the EventStore owner proof packet is
    complete and approved.
  - May be changed to `available` only by the Parties matrix recorder flow after
    the EventStore proof packet exists and the checked-out
    `references/Hexalith.EventStore` pin matches the approved SHA.

### Artifact Conflicts

PRD:

- No PRD scope change is required. Existing FR4, FR5, FR6, FR7, FR9, NFR8, and
  NFR16 already cover the SDK and evidence-quality intent.

Architecture:

- AD-7 and AD-12 already govern read-model/cursor seams and persisted evidence.
  The proposal recommends an optional clarification that cross-repo deletion of
  local projection/query mechanics requires owner proof for erasure, batching or
  approved equivalent, freshness mapping, duplicate/out-of-order behavior,
  rebuild parity, and cursor compatibility.

UX:

- No direct UX artifact change is required. Parties freshness display is a
  consumer-side compatibility requirement, not an EventStore UI change.

### Technical Impact

Potential EventStore code impact depends on proof classification:

- If every proof item is already available, implementation is documentation,
  proof packet, validation, and owner approval only.
- If G3 is missing, `IReadModelStore`, `DaprReadModelStore`, and
  `InMemoryReadModelStore` likely need additive delete/erase APIs with tests.
- If G10 is not approved as equivalent through existing `MergeAsync`/multi-key
  patterns, a generic batch write or documented approved-equivalent pattern is
  needed.
- If G6 cannot be mapped from existing generic metadata/freshness primitives,
  add a generic compatibility primitive or document that the mapping is
  consumer-owned and prove it with approved adapter evidence.
- No Parties production source migration is authorized by this proposal.

## 3. Change Analysis Checklist

| Item | Status | Finding |
| --- | --- | --- |
| 1.1 Triggering story | [x] | Parties Story 8.6 AC1 revealed the issue. |
| 1.2 Core problem | [x] | Evidence/owner-approval gap, with possible additive EventStore API gaps. |
| 1.3 Supporting evidence | [x] | Parties matrix row, Story 8.6 AC1, and EventStore source inspection all support the block. |
| 2.1 Current epic viability | [x] | Epic 1 remains valid but needs a post-completion proof follow-up. |
| 2.2 Epic-level changes | [!] | Add Epic 1 Story 1.8 and reopen Epic 1 status until that follow-up is done. |
| 2.3 Remaining epics | [x] | No resequencing required; related Epic 2/4/6 work remains in place. |
| 2.4 New epic required | [N/A] | A new epic is not required. |
| 2.5 Priority/order | [x] | Story 1.8 should run before Parties Story 8.6 resumes source migration. |
| 3.1 PRD conflicts | [N/A] | No MVP or FR/NFR change required. |
| 3.2 Architecture conflicts | [!] | Optional AD-7/AD-12 clarification recommended for cross-repo deletion gates. |
| 3.3 UX conflicts | [N/A] | No EventStore UX change. |
| 3.4 Secondary artifacts | [!] | Sprint status, implementation story, proof packet, and Parties matrix recorder flow are affected. |
| 3.5 Affected active stories | [x] | Parties Story 8.6 remains correctly blocked; no stale active ACs found. |
| 4.1 Direct adjustment | [x] Viable | Best option. Add a proof story and possible additive API tasks. Effort medium, risk medium. |
| 4.2 Rollback | [x] Not viable | No EventStore work should be rolled back; missing proof is not simplified by rollback. |
| 4.3 MVP review | [x] Not viable | MVP scope does not change. |
| 4.4 Recommended path | [x] | Direct adjustment with a moderate backlog/story update. |
| 5.1 Issue summary | [x] | Included in this proposal. |
| 5.2 Impact summary | [x] | Included in this proposal. |
| 5.3 Path forward | [x] | Direct adjustment. |
| 5.4 MVP impact | [x] | No PRD/MVP impact. |
| 5.5 Handoff plan | [x] | Product Owner/Developer with Architect review. |
| 5.6 Story rewrite gate | [x] | New EventStore story required; Parties 8.6 remains blocked until matrix proof is available. |
| 6.1 Final review | [!] | Pending user approval. |
| 6.2 Proposal accuracy | [!] | Pending user review. |
| 6.3 Explicit approval | [!] | Pending user approval. |
| 6.4 Sprint status update | [!] | Apply only after approval. |
| 6.5 Handoff plan | [x] | Included below. |

## 4. Recommended Approach

Use Direct Adjustment.

Add one EventStore owner-proof follow-up story under Epic 1:

`Story 1.8: Projection/Query SDK Owner Parity Proof`

This story must classify every Parties AC1 proof item as one of:

- `already available`
- `additive API/test added`
- `blocked`

It must then produce an owner proof packet. The proof packet may return
`available` only when every AC1 item is backed by reviewed EventStore source and
validation evidence. If any item is missing or blocked, the proof packet returns
`still blocked`, and the Parties matrix row stays `needs-additive-api`.

Effort:

- Proof-only path: medium-low, about 1 to 2 focused engineering days.
- Additive API path: medium, about 3 to 5 focused engineering days depending on
  whether delete/erase and batch/equivalent APIs are required.

Risk:

- Medium. The public SDK surface may need additive APIs. The risk is controlled
  by keeping all changes generic in EventStore and preserving the Parties
  rollback path until proof is approved.

Timeline impact:

- EventStore can continue current ready-for-dev work, but Parties Story 8.6 must
  remain blocked until this proof is produced and recorded.

## 5. Detailed Change Proposals

### 5.1 Epics Document

Artifact:
`_bmad-output/planning-artifacts/epics.md`

Section:
After `### Story 1.7: DomainService Packaging And Guardrails`

OLD:

```markdown
### Story 1.7: DomainService Packaging And Guardrails
...
**And** package dependencies are reproducible
**And** the solution remains clean under warnings-as-errors.

## Epic 2: External Integration Surfaces
```

NEW:

```markdown
### Story 1.8: Projection/Query SDK Owner Parity Proof

**Requirements covered:** FR4, FR5, FR6, FR7, FR9, NFR8, NFR16
**Classification:** Post-completion owner-proof follow-up for cross-repo domain migration.

As an EventStore platform owner,
I want reviewed proof that the projection/query SDK can replace a non-trivial domain's local projection/query mechanics,
So that consuming modules can delete local rollback code only after EventStore owner evidence proves parity.

**Acceptance Criteria:**

**Given** a consuming domain requires projection/query SDK replacement proof
**When** the owner proof story starts
**Then** it records the current EventStore commit SHA and inspects `IDomainProjectionHandler`, `IDomainQueryHandler`, `IReadModelStore`, `ReadModelWritePolicy`, `IQueryCursorCodec`, `QueryCursorScope`, and domain-service registration APIs.

**Given** the proof is built for Parties Story 8.6 AC1
**When** each required item is evaluated
**Then** G3 read-model erasure hooks, G10 index batching or approved equivalent, G6 freshness mapping, duplicate/out-of-order replay behavior, full rebuild verification, cursor scope compatibility, and the intended EventStore pin are each classified as `already available`, `additive API/test added`, or `blocked`.

**Given** any required proof item is not satisfied
**When** the proof packet is produced
**Then** the final decision is `still blocked`, the missing API or behavior is named precisely, and no consuming story is authorized to mark the projection/query SDK row `available`.

**Given** additive code is needed
**When** implementation changes are made
**Then** they remain generic EventStore SDK capabilities and do not add Parties-specific domain logic to EventStore.

**Given** every required item is satisfied
**When** validation completes
**Then** the proof packet records source paths, test paths, validation commands/results, owner approval source, rollback note, known limitations, and final decision `available`.

**Given** the owner proof is available
**When** a consuming repo records it in its prerequisite matrix
**Then** the consuming repo must still verify its checked-out EventStore pin matches the approved SHA before source migration or local rollback deletion starts.
```

Rationale:
Existing Epic 1 stories implemented the generic SDK surfaces, but Parties AC1
requires owner-approved replacement proof for a specific cross-domain migration
gate. A focused follow-up is clearer and safer than reopening completed stories.

### 5.2 Sprint Status

Artifact:
`_bmad-output/implementation-artifacts/sprint-status.yaml`

Section:
`development_status`

OLD:

```yaml
  epic-1: done
  1-1-canonical-domain-service-sdk-host: done
  1-2-domain-query-handler-routing: done
  1-3-generic-read-models-and-query-cursors: done
  1-4-projection-and-domain-event-consumer-seams: done
  1-5-domain-module-hosting-observability: done
  1-6-sample-and-tenants-domain-centric-adoption: done
  1-7-domainservice-packaging-and-guardrails: done
  epic-1-retrospective: done
```

NEW:

```yaml
  epic-1: in-progress
  1-1-canonical-domain-service-sdk-host: done
  1-2-domain-query-handler-routing: done
  1-3-generic-read-models-and-query-cursors: done
  1-4-projection-and-domain-event-consumer-seams: done
  1-5-domain-module-hosting-observability: done
  1-6-sample-and-tenants-domain-centric-adoption: done
  1-7-domainservice-packaging-and-guardrails: done
  # Cross-repo owner-proof follow-up for Parties Story 8.6 AC1.
  1-8-projection-query-sdk-owner-parity-proof: ready-for-dev
  epic-1-retrospective: done
```

Rationale:
Adding a backlog story to an otherwise completed epic requires temporarily
reopening the epic status. The original Epic 1 delivered work stays done.

### 5.3 New Implementation Story

Artifact:
`_bmad-output/implementation-artifacts/1-8-projection-query-sdk-owner-parity-proof.md`

OLD:

```text
File does not exist.
```

NEW:

```markdown
---
title: "1.8 Projection/Query SDK Owner Parity Proof"
type: "proof"
status: "ready-for-dev"
---

# Story 1.8: Projection/Query SDK Owner Parity Proof

## Story

As an EventStore platform owner,
I want reviewed proof that the projection/query SDK can replace a non-trivial domain's local projection/query mechanics,
so that consuming modules can delete local rollback code only after EventStore owner evidence proves parity.

## Acceptance Criteria

1. Record the current EventStore commit SHA intended for consuming modules.
2. Inspect SDK surfaces and tests: `IDomainProjectionHandler`, `IDomainQueryHandler`, `IReadModelStore`, `ReadModelWritePolicy`, `IQueryCursorCodec`, `QueryCursorScope`, and domain-service projection/query registration APIs.
3. Classify every Parties Story 8.6 AC1 proof item as `already available`, `additive API/test added`, or `blocked`.
4. Keep any additive code generic in EventStore; do not add Parties-specific domain logic.
5. Add or identify tests proving every satisfied item.
6. Produce `_bmad-output/implementation-artifacts/1-8-projection-query-sdk-owner-proof-packet.md`.
7. Return final decision `available` only if every proof item is satisfied by reviewed source and validation evidence; otherwise return `still blocked`.

## Required Proof Items

- G3 read-model erasure hooks.
- G10 index batching or approved SDK equivalent.
- G6 freshness mapping for `Current`, `Stale`, `Rebuilding`, `Degraded`, `Unavailable`, and `LocalOnly`.
- Duplicate and out-of-order replay behavior.
- Full rebuild verification against aggregate replay.
- Cursor scope compatibility through `IQueryCursorCodec` and `QueryCursorScope`.
- Current EventStore commit SHA intended for consuming modules.

## Validation

Run focused validation first, then the broadest practical EventStore validation lane:

- `dotnet test tests/Hexalith.EventStore.Client.Tests/`
- `dotnet test tests/Hexalith.EventStore.Testing.Tests/`
- `dotnet test tests/Hexalith.EventStore.DomainService.Tests/`
- `dotnet test tests/Hexalith.EventStore.Server.Tests/`
- `dotnet build Hexalith.EventStore.slnx --configuration Release`
- `git diff --check`
```

Rationale:
This gives the Developer agent an explicit story contract rather than asking it
to infer Parties AC1 from a prompt file.

### 5.4 Architecture Clarification

Artifact:
`_bmad-output/planning-artifacts/architecture.md`

Section:
AD-7 - Read Models And Cursors Use Platform Seams

OLD:

```markdown
- **Rule:** Persisted read models use `IReadModelStore` plus `ReadModelWritePolicy`. Paging cursors use `IQueryCursorCodec` plus `QueryCursorScope`. Cursors are opaque, DataProtection-backed, scope-validated, bounded, and fail safe on tamper, malformed payloads, wrong scope, wrong query type, or key rotation.
```

NEW:

```markdown
- **Rule:** Persisted read models use `IReadModelStore` plus `ReadModelWritePolicy`. Paging cursors use `IQueryCursorCodec` plus `QueryCursorScope`. Cursors are opaque, DataProtection-backed, scope-validated, bounded, and fail safe on tamper, malformed payloads, wrong scope, wrong query type, or key rotation.
- **Cross-repo deletion gate:** Before a consuming domain deletes local projection/query/read-model/cursor rollback mechanics in favor of EventStore SDK seams, owner evidence must prove required erasure hooks, index batching or approved equivalent, freshness-state mapping, duplicate/out-of-order behavior, rebuild parity against aggregate replay, and cursor scope compatibility for that migration. Missing owner proof blocks deletion.
```

Rationale:
This aligns AD-7 with the already-enforced Parties gate and prevents future
consumers from treating checked-out source files as sufficient replacement
evidence.

### 5.5 Parties Matrix Row

Artifact:
`../parties/_bmad-output/implementation-artifacts/story-8-3-platform-api-prerequisite-matrix.md`

No immediate edit is proposed from the EventStore correct-course workflow. The
row must remain `needs-additive-api` until the EventStore proof packet is
available and the Parties matrix recorder verifies the checked-out
`references/Hexalith.EventStore` SHA.

Permitted later change, after proof only:

```text
Status:
available

Proof required before migration:
Story 8.6 AC1 proof recorded on YYYY-MM-DD against
references/Hexalith.EventStore pin <sha>. Owner-approved evidence covers G3
read-model erasure hooks, G10 index batching or approved equivalent, G6 Parties
freshness mapping, duplicate/out-of-order replay, full rebuild verification, and
cursor scope compatibility. Evidence: <paths/tests/PR>. Story 8.6 must still
build its Parties parity harness before deleting local rollback paths.
```

Rationale:
This proposal must not bypass the consuming repo gate.

## 6. Story Rewrite Gate

This change does not supersede active Parties Story 8.6 acceptance criteria. It
confirms them. No stale active Parties story section should be removed.

Required gate before implementation/review continues:

- EventStore must create or approve Story 1.8 before an EventStore Developer
  agent starts owner-proof work.
- Parties Story 8.6 source migration remains blocked while the Story 8.3
  `EventStore projection/query SDK` row is `needs-additive-api`.
- The Parties matrix row may not become `available` until the proof packet
  exists, every AC1 proof item is covered, and the checked-out
  `references/Hexalith.EventStore` pin matches the approved EventStore SHA.
- A superseded-scope banner is not sufficient. If any future Parties or
  EventStore story says the existing SDK source files alone unblock Story 8.6,
  that active story content must be rewritten before implementation or review
  proceeds.

## 7. Implementation Handoff

Scope classification: Moderate.

Route to:

- Product Owner / Developer for backlog/story update and proof story creation.
- Architect review if the proof story discovers that G3, G10, G6, duplicate/out-
  of-order, rebuild, or cursor compatibility requires an architecture decision.

Developer responsibilities:

- Create Story 1.8 from the proposal.
- Inspect EventStore source and tests.
- Add generic additive EventStore APIs/tests only when proof shows they are
  required.
- Produce the owner proof packet.
- Run focused and broad validation as practical.

Product Owner responsibilities:

- Approve the Epic 1 follow-up and sprint-status adjustment.
- Keep Parties Story 8.6 blocked until the matrix row is legitimately updated.

Success criteria:

- `_bmad-output/implementation-artifacts/1-8-projection-query-sdk-owner-proof-packet.md`
  exists.
- The proof packet final decision is either `available` or `still blocked`.
- If `available`, every proof item maps to source paths, test paths, validation
  commands/results, owner approval source, and EventStore commit SHA.
- If `still blocked`, the missing API/behavior is explicit enough to drive a
  follow-up EventStore story.
- No Parties production source migration starts before its Story 8.3 matrix row
  is updated by the recorder flow.

## 8. Approval Request

Approved by Administrator on 2026-07-10. Route to Product Owner / Developer for
Story 1.8 creation and implementation.

Approve this proposal to create the EventStore Story 1.8 owner-proof follow-up
and keep Parties Story 8.6 blocked until the proof is recorded.

Decision options:

- `yes` - approve and route to story creation / implementation.
- `revise` - adjust this proposal before approval.
- `no` - reject this approach and keep the existing block without a new
  EventStore story.
