# Sprint Change Proposal: Story 6.6 Pragmatic Rescope

Date: 2026-10-09
Project: Hexalith.EventStore
Requested by: Administrator (sole owner)
Trigger: Story 6.6 "Event Versioning And Upcasting Implementation" — "seems over-engineered; many iterations taking hours without being able to complete. Find a more pragmatic solution that resolves the goal with less resources; avoid any task without real ROI."
Review mode: Batch
Status: **Approved by the owner on 2026-10-09 (batch, including D1–D5) and applied to the planning artifacts, uncommitted.** See the Application Record.
Scope: Moderate (one story rewritten, one task group retires dormant code; PRD/architecture/UX text aligned; no new stories)

## 1. Issue Summary

### Problem

Story 6.6 has run for five days (2026-10-04 → 2026-10-09) without meeting any acceptance criterion. All 8 tasks in the parent spec are unchecked, and all 20 verification obligations and 47 implementation follow-ups are still open. Meanwhile the work produced a large amount of scaffolding that never runs, while the feature itself stays switched off.

| Measure | Value (inventory 2026-10-09, HEAD `418e5632`) |
| --- | --- |
| New `src/` code for 6.6 | ~207 files, ~22.7k lines; **~20k lines never run in a live system** (estimate) |
| New test code | ~124 files, ~22.3k lines (estimate) |
| Guard/verification scripts | 30 files, 4,529 lines (`scripts/verify-dapr-logical-*.py`, `scripts/verify-event-evolution*.py`, …) |
| CI | New `.github/workflows/event-evolution-local-guards.yml` (19 matrix lanes plus 8 vector steps); `ci.yml` `event-evolution-compatibility` job |
| Evidence | `evidence/story-6-6/`: 4,534 tracked files (~31 MB), 859 MB on disk; 2 checkpoint packets alone are 530 MB |
| Planning docs | ~20 `story-6-6-*` model/amendment/checkpoint docs plus 85 KB and 37 KB specs; 13 "Dapr logical" models written in 5 days |
| Delivered capability | **None.** 15+ hard-coded guards refuse every versioned event on write and read. No test upcasts an event through the real write/read path. |

### Root cause

This is an approach that failed, not a gap in the requirements. Story 6.6 was bound to a 1.28 MB normative design (`spec-event-versioning-upcasting.md`; 2.8 MB counting the 6.5/6.5a–d specs). That design goes far beyond the PRD: provider attestation and logical-readback proofs, bounded scratch allocators and payload writers, artifact-hash loader admission, signed checkpoint/anchored-replay/snapshot-rewitness models, proof carriers, and mutation-kill sealed evidence packets. Each review loop added another model instead of closing scope. The build loop then treated every new model as authorized dormant work.

### What the PRD actually asks for

PRD FR33, Story 6.6 slice (FR33-C6), asks for three things:

1. Support event schema versioning/upcasting.
2. Reject an event whose metadata identity components are absent or not ULID-safe, rather than accepting it.
3. Add cancellation-token seams to the published processing/query/projection interfaces.

### What already exists and works (keep)

- The nullable `PayloadVersion`/`EventContractType` fields on `EventMetadata`, `EventEnvelope` and `DomainServiceWireEvent`, already copied end to end (always null today).
- `IAsyncDomainProcessor` and `IAsyncAggregateReplay`, registered and preferred by `DomainServiceRequestRouter`. `IDomainQueryHandler` and `IAsyncDomainProjectionHandler` already take a token.
- Live pieces of the first attempt: bounded V1 wire parsing in `DaprDomainServiceInvoker`, `BoundedV1DomainResultProducer` (samples), `EventLogicalDigest`, and legacy command replay admission (`LegacyCommandReplayInput`, `LegacyCommandReplayJsonAdmission`, `DetachedStateCapture`).

### Real gaps

- **Versioning:** nothing stamps a version on new events. `ApplyMethodResolver` resolves the CLR type from the stored CLR name. There is no upcaster that a domain author can practically implement: the shipped `IEventUpcaster` requires bounded writers and scratch allocators and has no registration path.
- **Identity:** there is no single event-metadata identity validator. A blank causation ID, a blank event type name, or a malformed message ID is accepted on append and on read.
- **Cancellation:** the token is still lost in three places: legacy `IDomainProjectionHandler` (`/project`), `AddEventStoreClient<TProcessor>` (it never registers the keyed async processor), and the aggregate `Handle` convention.

## 2. Impact Analysis

### Checklist status

| Item | Status | Finding |
| --- | --- | --- |
| 1.1 Trigger story | [x] | Story 6.6, `in-progress` since 2026-10-04 |
| 1.2 Problem category | [x] | Failed approach requiring a different solution (over-specification) |
| 1.3 Evidence | [x] | Inventory above (three code inventories, git history, artifact sizes) |
| 2.1 Current epic | [x] | Epic 6 is still completable; only 6.6 changes. 6.2/6.4 untouched. |
| 2.2 Epic-level changes | [x] | Rewrite Story 6.6 and its Epic 6 context lines; no new or removed stories |
| 2.3 Remaining epics | [x] | No dependency on the retired scaffolding. Story 3.17's "6.6's stricter … no direct-database product evidence" rule stays true. |
| 2.4 Obsolete or new epics | [N/A] | None |
| 2.5 Resequencing | [N/A] | None |
| 3.1 PRD | [!] | FR33 unchanged. FR33-C6 row reworded to bind the rewritten story ACs instead of "the approved specification". MVP unaffected (scope reduction inside the same requirement). |
| 3.2 Architecture | [!] | AD-13: replace the two Story 6.6 paragraphs (Dapr-only amendment, loader policy) with one pragmatic-scope paragraph. AD-1 storage rule unchanged. |
| 3.3 UX | [!] | `EXPERIENCE.md` "Evolution and ambiguous-save outcomes": first two paragraphs simplified (no Admin upcasting, no loader states) |
| 3.4 Other artifacts | [!] | CI workflow + job removed; 30 scripts removed; evidence archived; 47 deferred-work items withdrawn; review action items P1–P17 superseded; `epic-6-context.md` aligned; `docs/concepts/event-versioning.md` updated |
| 4.1 Direct adjustment | Viable | Rewrite 6.6; effort Medium (~3–4 dev days); risk Low–Medium |
| 4.2 Rollback | Viable (partial) | Retire dormant scaffolding; keep the live pieces listed above |
| 4.3 MVP review | Not needed | PRD requirement unchanged |

### Technical impact

- **Code:** Deletes ~20k lines of dormant `src` code and their tests. Small edits to `EventPersister`, `EventStreamReader`, `AggregateActor`, `DomainProcessorStateRehydrator`, `AggregateReplayer`, `EventStoreProjection`, `EventStoreDomainEventProcessor`, the `/project` dispatcher, `DomainServiceRequestRouter`, `EventStoreServiceCollectionExtensions`, and Contracts (new validator, attribute and upcaster interface).
- **Public API:** Additive only (minor release). About 12 public types shipped in v3.113–v3.116 with no consumers and no working registration path stay as `[Obsolete]` shells until the next planned major release (see D3).
- **CI:** Faster. Removes a 19-lane workflow and one `ci.yml` job.
- **Wire/storage compatibility:** Version-1 events are persisted byte-for-byte as today. Only event types that opt into version ≥ 2 write `PayloadVersion`. Older consumers already refuse versioned events (fail closed), so the rolling-upgrade rule is a deployment order, not a test matrix.

## 3. Recommended Approach

**Direct Adjustment plus a partial rollback of the dormant scaffolding.**

### Owner decisions (recommended options; approving this proposal approves them)

| # | Decision | Recommended | Alternative rejected |
| --- | --- | --- | --- |
| D1 | Governing specification for 6.6 | The rewritten story ACs **are** the approved specification (AD-13 satisfied by the story's design section + its test vectors). The 1.28 MB AD-13 spec and the 2026-10-05/06/08 6.6 amendments become historical. | Keep conforming to the 1.28 MB spec — the cause of the stall. |
| D2 | Event identity model | Keep the CLR type name as `EventTypeName`, add `PayloadVersion` (absent = 1). Handle renames with an upcaster step that changes the type name. **Drop** kebab-case contract identity and metadata V2 from 6.6. The V2 write fence stays and `EventContractType` stays reserved. | Canonical kebab contract type + alias registry + V2 metadata: higher cost, no PRD requirement. |
| D3 | Dormant scaffolding | Delete dormant internal code, its tests, scripts, the CI workflow/job, and 6.6 tests that pin planning docs. Keep the ~12 shipped public types as `[Obsolete]` shells (no behaviour) until the next major. Move the first-attempt planning docs to an archive folder; delete `evidence/story-6-6/` from HEAD (git history keeps it). | (a) Keep everything: guards and tests that refuse versioned events would have to be rewritten instead of deleted, and the 19-lane CI keeps running. (b) Delete the public types too: forces a SemVer-major release now. |
| D4 | Evidence bar | Unit tests + **one** end-to-end test through the real write/read path in an existing test project + Release build with warnings-as-errors + the affected test projects. Existing CI only. One review pass. | Mutation-kill guards, sealed packets, content digests, live-sidecar qualification lanes, rolling-upgrade matrices. |
| D5 | Upcaster shape | One small public interface that transforms a `JsonObject` from version *n* to *n + 1* (optionally renaming the type), discovered like aggregates and projections. | The shipped bounded-buffer `IEventUpcaster` (impractical for domain authors), or typed `TOld → TNew` upcasters (old classes must be kept). |

### Effort, risk, timeline

- **Effort:** ~3–4 focused dev days. Task 1 cleanup ~0.5–1 day, upcasting ~1.5–2, identity ~0.5, cancellation ~0.5.
- **Risk:** Low–Medium.
  - Main risk: read-side identity validation could reject legitimately stored legacy events. Mitigation: a preflight check, plus a stop-and-ask rule in AC6.
  - Second risk: the cleanup turns into a refactor. Mitigation: Task 1 is time-boxed and deletes only code with no production caller.
- **Timeline:** Replaces an open-ended loop with a bounded story. Epic 6 still needs 6.2 and 6.4 (unchanged).

## 4. Detailed Change Proposals

### 4.1 Story 6.6 — `_bmad-output/planning-artifacts/epics.md` (replace the whole story section)

OLD: `### Story 6.6: Event Versioning And Upcasting Implementation` through the end of its 12 acceptance criteria (current lines 5189–5270).

NEW:

```markdown
### Story 6.6: Event Versioning And Upcasting Implementation

As a domain author,
I want to declare an event payload version and register upcasters that convert older stored payloads when they are read,
So that I can evolve event schemas without rewriting history, while malformed event identity is rejected and processing honours cancellation.

**Requirements coverage:** Primary ownership of FR33-C6 (upcasting, metadata identity rejection, published cancellation seams). Supporting NFR7 (fail closed, never skip silently), NFR12 (additive; version-1 events unchanged), NFR16 (one persisted production-path test) and NFR18 (upcaster discovery is a reflection convention inventoried by Story 6.7).

**Architecture constraints:** AD-1, AD-6 and AD-13 (2026-10-09 pragmatic scope). Stored events are never rewritten; upcasting changes only the in-memory payload a domain service deserializes. Event state stays actor-owned through Dapr; no application SQL or direct-database evidence.

**UX coverage:** None required. Admin stream and event views keep showing stored data and may show the stored payload version where their DTO already carries it.

**Dependencies:** None blocking. The Story 6.5 specification and the 2026-10-05/06/08 Story 6.6 amendments are historical context only; this story's design and acceptance criteria are the governing specification (owner decision, sprint-change-proposal-2026-10-09).

**Current reconciliation (2026-10-09):** Rescoped by sprint-change-proposal-2026-10-09. The first attempt (2026-10-04 to 2026-10-09) built dormant scaffolding that Task 1 retires. Its live pieces stay: the async processor/replay interfaces, bounded V1 wire parsing, event logical digest, legacy command replay admission, and the nullable `PayloadVersion`/`EventContractType` metadata fields. Review action items P1–P17 of the first attempt are superseded by AC1 and AC2.

**Design (owner-approved 2026-10-09; type names are indicative):**

- A domain event type declares its current payload version with `[EventPayloadVersion(n)]`, where 1 ≤ n ≤ 1024. Without the attribute the version is 1.
- Version-1 events are persisted exactly as today, with no `PayloadVersion`. An event of version n ≥ 2 stores `PayloadVersion = n`, keeps `MetadataVersion` 1, and keeps the CLR type name as `EventTypeName`. `EventContractType` and metadata version 2 stay unused; the existing V2 write fence stays.
- An upcaster implements `IEventPayloadUpcaster`: the `EventTypeName` and `FromVersion` it accepts, an optional `TargetEventTypeName` for renames (null keeps the name), and `JsonObject Upcast(JsonObject payload)`, which produces version `FromVersion + 1`. Upcasters are discovered from the assemblies already scanned for aggregates and projections, and can also be registered explicitly with `AddEventPayloadUpcaster<T>()`.
- One shared client component runs the chain before CLR type resolution and deserialization. Every domain-service read path calls it: command-time state rehydration, replay/reconstruction, projections (including `/project` dispatch) and subscriptions.
- The EventStore server does not upcast; it stores and forwards `PayloadVersion` unchanged.

**Tasks:**

1. Retire the dormant first-attempt scaffolding (time-box: 1 day).
   - Delete internal code with no production caller and its tests: logical source/reconstruction/replay operations except `EventLogicalDigest`; checkpoint, anchored replay/continuation, snapshot issue/replace/rewitness; logical command state; logical query model; managed artifact/loader observation; proof framing/runtime-options codecs; the inert registry/upcast executor/evolution service with its client scratch/writer implementations.
   - Delete the 30 `scripts/verify-dapr-logical-*`/`verify-event-evolution*`/`prepare-event-evolution-candidates` scripts and their fixtures, `.github/workflows/event-evolution-local-guards.yml`, the `ci.yml` `event-evolution-compatibility` job, and tests that read 6.6 planning or evidence files.
   - Keep the shipped public types (`IEventUpcaster`, `EventUpcastResult`, `IV1Downserializer`, `V1DownserializeResult`, `AddEventStoreEventEvolutionManifestCandidate`, `AuthenticatedRawEvent`, `AuthenticatedRawEventPage`, both `IAuthenticatedRawEventSource`, `IBoundedPayloadWriter`, `IBoundedScratchAllocator`, `IReadOnlyPayload`, `ScratchSpanAction`) as `[Obsolete]` shells without behaviour, to be removed at the next major release.
   - Move the first-attempt 6.6 planning documents to `_bmad-output/implementation-artifacts/archive/story-6-6-first-attempt/` and delete `evidence/story-6-6/` from HEAD (history keeps it; record the last commit containing it in the archive README).
   - If removing a group would change live behaviour, leave that group in place and note it rather than refactoring.
2. Implement versioning and upcasting (AC1–AC5).
3. Implement the identity validator (AC6).
4. Close the cancellation gaps (AC7).
5. Add evidence and documentation (AC8).

**Acceptance Criteria:**

**AC1 — Write.** **Given** an event type without a version attribute **When** it is persisted **Then** its stored envelope is identical to pre-6.6 behaviour, with no `PayloadVersion`. **Given** an event type declaring `[EventPayloadVersion(3)]` **When** it is persisted **Then** `PayloadVersion` 3 travels unchanged through the wire result, persisted envelope, stream read, projection and subscription payloads, and the domain service, and is never dropped or relabelled.

**AC2 — Upcast on read.** **Given** stored history mixing older and current payload versions of the same event, with an upcaster registered for every step **When** a domain service rehydrates state for a command, replays/reconstructs, projects or handles a subscription **Then** each older payload passes through each step exactly once, in ascending version order, before deserialization **And** the resulting state equals the state produced by the same history written at the current version, while stored bytes, `MessageId`, sequence and correlation stay unchanged.

**AC3 — Rename.** **Given** a step with `TargetEventTypeName` **When** it runs **Then** the event deserializes as the renamed CLR type through the existing Apply/handler resolution.

**AC4 — Startup validation.** **Given** two upcasters for the same `(EventTypeName, FromVersion)`, a `FromVersion` below 1, a step whose output neither feeds another step nor equals the declared current version of a known event type, or an event type declaring version n > 1 that no step produces **When** the domain service starts **Then** startup fails with a message naming the event type and version.

**AC5 — Fail closed.** **Given** a stored payload with no path to a known current type, a version above the type's declared version, or an upcaster that throws or returns null **When** any read path meets it **Then** it fails with a typed error naming the type and version; nothing is skipped, no state or checkpoint advances, no handler effect occurs, and the payload content is not logged. Existing retry/poison handling applies to subscriptions and projections.

**AC6 — Identity validation.** **Given** an event about to be persisted **When** any identity component is absent or malformed **Then** the append is rejected before any state is staged, with a typed error naming the component. The components and rules are: tenant, domain and aggregate ID per the `AggregateIdentity` grammar; non-blank aggregate type and event type name; `MessageId` a valid ULID; `CorrelationId` per AD-32 (1–128 ASCII alphanumeric or hyphen); `CausationId` non-blank under the same character rule; sequence ≥ 1. **And** the same single `Contracts` validator also runs on stream read (`EventStreamReader`, beside the existing address checks) and in the subscription envelope check, so a stored event that fails it fails closed instead of being applied. **Preflight:** before enabling the read-side check, confirm that persisted history written by earlier versions satisfies every rule (for example, that event `MessageId`s have always been ULIDs). If a rule would reject legitimately stored events, stop and ask the owner instead of weakening the rule silently.

**AC7 — Cancellation seams.** **Given** a request whose token is cancelled **When** it reaches a legacy `IDomainProjectionHandler`, a processor registered through `AddEventStoreClient<TProcessor>`, or an aggregate `Handle` method **Then** the token arrives:
- `IDomainProjectionHandler` gains a token-aware default interface method that the `/project` endpoint and dispatcher call with the request token.
- `AddEventStoreClient<TProcessor>` also registers the keyed `IAsyncDomainProcessor` when the processor implements it.
- The `Handle` convention accepts an optional trailing `CancellationToken`.

**And** existing `IDomainProcessor`, `IAggregateReplay` and projection-handler implementations compile and behave unchanged, and no touched path replaces an available caller token with `CancellationToken.None`.

**AC8 — Evidence and completion.** **Given** completion is requested **When** checks run **Then** all of the following hold:
- Unit tests cover the pipeline: single step, multiple steps, rename, missing step, too-new version, throwing upcaster, and every startup-validation case.
- Unit tests cover the identity validator, one case per component, on append and on read.
- Tests cover the three cancellation seams.
- One end-to-end test runs through the real write and read path in an existing test project: persist version-1 events, declare version 2 with an upcaster, persist a version-2 event, rehydrate. It asserts the persisted envelopes (version 1 without `PayloadVersion`, version 2 with `PayloadVersion` 2, payload bytes unchanged) and the resulting aggregate state.
- `docs/concepts/event-versioning.md` explains how to version an event, write an upcaster, rename an event, and deploy in order: consumers with the upcaster before or together with the writer; older consumers refuse versioned events rather than misread them.
- The Release build with warnings-as-errors and every affected test project pass in the existing CI.

**Out of scope:**
- kebab-case contract identity and metadata V2
- application digest, provider attestation or logical-readback proofs
- checkpoint, anchored, snapshot-rewitness and command-state models
- bounded scratch/writer memory accounting and artifact-hash loader admission
- mutation-kill scripts, sealed evidence packets, content-digest approvals, new CI workflows and live-sidecar qualification lanes
- rolling-upgrade and downgrade matrices (replaced by the documented deployment order)
- V1 downserialization, publication pins and receipts, hold/resume lifecycle
- Admin UI or Type Catalog work, and offline rewrite of stored events

Findings outside these acceptance criteria go to `deferred-work.md` as optional follow-ups, not to this story.
```

**Rationale:** Binds the story to the three PRD outcomes, reuses what already runs, and adds explicit exclusions so a build loop cannot re-inflate scope.

### 4.2 PRD — `_bmad-output/planning-artifacts/prd.md` §7.1, row FR33-C6

OLD:

```
| FR33-C6 | 6.6 | Upcasting, metadata rejection, and published cancellation behavior conform to the approved specification. |
```

NEW:

```
| FR33-C6 | 6.6 | Upcasting, metadata rejection, and published cancellation behavior satisfy Story 6.6's acceptance criteria (owner-approved pragmatic scope, sprint-change-proposal-2026-10-09). |
```

**Rationale:** "The approved specification" pointed at the 1.28 MB design. FR33 itself is unchanged.

### 4.3 Architecture — `_bmad-output/planning-artifacts/architecture.md` AD-13

OLD: the two bullets `**Story 6.6 Dapr-only amendment (owner, 2026-10-05):** …` and `**Story 6.6 loader policy (owner, 2026-10-06):** …`.

NEW (one bullet replaces both):

```markdown
- **Story 6.6 pragmatic scope (owner, 2026-10-09):** Per [sprint-change-proposal-2026-10-09](sprint-change-proposal-2026-10-09.md), Story 6.6's design section and acceptance criteria are the approved upcaster specification, and its test vectors are the compatibility vectors this rule requires. Event types declare a payload version (absent = 1). Version-1 events persist unchanged, and newer versions store `PayloadVersion` with the CLR `EventTypeName`. One client-side pipeline applies registered contiguous `n → n + 1` JSON upcasters, with optional rename, before deserialization on every domain-service read path. Startup rejects duplicate or dangling chains, and read-time gaps or failures fail closed without skipping, partial state, or checkpoint advance. Stored events are never rewritten, and the server stores and forwards versions without upcasting. Application storage stays under AD-1: actor-owned through Dapr, no application SQL, and no direct-database product evidence. The [2026-10-04 metadata-adapter contract](../implementation-artifacts/6-5-integration/metadata-adapter-contract.md) stays withdrawn and grants no application SQL, schema, credential, or provider-proof authority to any story. The 2026-10-05 Dapr-only amendment, the 2026-10-06 loader policy, the 2026-10-08 logical-model amendment, and the approved `spec-event-versioning-upcasting.md` are historical for Story 6.6. Kebab-case contract identity, metadata V2, provider or readback proofs, loader admission, and bounded-buffer upcasters are out of scope.
```

**Rationale:** The two old paragraphs are what forced the provider-proof and loader-admission work. Keeping the storage boundary as a reference to AD-1 preserves the real constraint.

### 4.4 UX — `ux-designs/ux-eventstore-2026-07-05/EXPERIENCE.md`, section "Evolution and ambiguous-save outcomes"

OLD: the first two paragraphs ("Streams & Events, Type Catalog, and existing diagnostic/recovery details consume the shared allow-listed evolution service. …" and "Show only approved stable contract/version identifiers, … Story 6.6 admission remains fenced until its writer and serving consumers qualify.").

NEW:

```markdown
Streams & Events, Type Catalog, and diagnostic views show stored events as stored: the stored event type name and, when present, the stored payload version (absent means version 1). They never upcast and never rewrite retained payloads; upcasting happens only inside the domain service that deserializes the event. A read that fails because a version has no upcaster path or an upcaster failed surfaces as a typed failure with the event type, version, and sequence, never as current state, a skipped event, or an advanced checkpoint. Tenant input supplies no executable code, assembly path, or CLR type choice.
```

The third paragraph (ambiguous actor save) is unchanged.

**Rationale:** Removes UI requirements for loader-capability states and a server-side evolution service that will not exist.

### 4.5 Epic context — `_bmad-output/implementation-artifacts/epic-6-context.md`

`epic-6-context.md` is loaded as build context, so it must not keep the old design.

1. Line "New events carry canonical kebab-case type and positive payload version. …" → `New event types may declare a payload version (absent = 1); version-1 events persist unchanged. Event identity metadata (tenant/domain/aggregate grammar, aggregate and event type, ULID MessageId, AD-32 correlation, causation, sequence) is validated on append and read and fails closed.`
2. Line "All consumers share bounded contiguous deterministic upcasting …" → `Every domain-service read path shares one client-side pipeline of contiguous n → n+1 JSON upcasters (optional rename) before deserialization. Stored bytes, identity and sequence stay immutable; failures commit no partial state, checkpoint, or handler effect.`
3. Line "Cancellation propagates through processing/query/projection/read/upcast seams …" → `Published processing, query, and projection seams receive the caller's cancellation token; legacy implementations stay source-compatible.`
4. Delete the Technical Decisions bullets "Story 6.6 stays strictly within Dapr …", "Digests and Dapr readback prove logical returned values …", "Deterministic allow-listed registries …", "Catalogs/dependencies are trusted deployer-reviewed code …" and "Qualification checks persisted end state through Dapr, live-sidecar …". Replace them with: `Story 6.6 follows the AD-13 pragmatic scope (2026-10-09): story ACs govern; evidence is unit tests, one end-to-end persisted-path test, and the existing CI.`
5. In UX & Interaction Patterns, replace "event type/version/legacy/upcast/hop/outcome evidence" with "stored event type and payload version".
6. Cross-Story Dependencies: replace the two Story 6.6 bullets ("Stories 6.5a–6.5d feed …", "Story 6.6 remains in progress. …") with `Story 6.6 was rescoped on 2026-10-09 (sprint-change-proposal-2026-10-09); Story 6.5's design is historical context. Upcasting does not authorize snapshot or projection-cost redesign.`

### 4.6 Sprint status — `_bmad-output/implementation-artifacts/sprint-status.yaml`

Keep `6-6-event-versioning-and-upcasting-implementation: in-progress`. Add one comment above it, outside any guarded block:

```yaml
  # Rescoped 2026-10-09 (sprint-change-proposal-2026-10-09): pragmatic upcasting, identity validation, cancellation seams; first-attempt scaffolding retired by Task 1.
```

### 4.7 Implementation-artifact hygiene (applied with this proposal)

- `spec-6-6-event-versioning-and-upcasting-implementation.md` (status `in-progress`) and `spec-6-6-event-versioning-and-upcasting-implementation-2.md` (status `done`): set frontmatter `status: 'superseded'` and add, directly after the frontmatter, the banner `SUPERSEDED 2026-10-09 by sprint-change-proposal-2026-10-09 — do not resume; /bmad-build 6.6 starts a new spec from the rewritten story.` This stops the next build from resuming the old Step 3 workflow, as the 2026-10-08/09 checkpoints instruct.
- `story-6-6-session-checkpoint-2026-10-08.md` and `story-6-6-checkpoint-initial-session-checkpoint-2026-10-09.md`: add the same banner.
- `deferred-work.md`: the 47 entries whose status ends "implementation/evidence follow-up remains open until completed in Story 6.6." change to `status: resolved 2026-10-09 by sprint-change-proposal-2026-10-09 (Story 6.6 pragmatic rescope): withdrawn; the cited source (<original rule/obligation reference>) no longer governs Story 6.6.` (uses the ledger's existing `resolved` vocabulary; each original reference is preserved).
- Task 1 of the rewritten story performs the archive move and evidence deletion. They are not applied by this proposal.

### 4.8 Optional local cleanup (owner action; not tracked by git)

- About 828 MB of ignored files under `evidence/story-6-6/` and the `/tmp/story66-*` run directories named in the 2026-10-09 checkpoint can be deleted locally. The assistant does not delete them.

## 5. Implementation Handoff

- **Scope classification:** Moderate. The story is rewritten and the backlog keeps the same keys, so no PM/Architect replan is needed.
- **Route to:** Developer agent via `/bmad-build 6.6`, starting from the rewritten story. It must not resume the superseded spec or the 2026-10-08/09 checkpoints.
- **Order:** Task 1 (cleanup, own commit) → Tasks 2–4 (one commit each, independently green) → Task 5 → one `/bmad-code-review 6.6` pass limited to these ACs.
- **Success criteria:**
  1. AC1–AC8 pass.
  2. The Release build with warnings-as-errors and the affected test projects are green in the existing CI.
  3. The event-evolution guard workflow, the 30 scripts and the dormant groups are gone.
  4. No 6.6 work item remains outside these ACs.
- **Stop rule:** If an AC turns out to need a design decision not covered above (for example, AC6 preflight finds legacy data that a rule would reject), stop and ask the owner; do not add a new model.
- **Commit hygiene:** Each commit message is validated with the repository's commitlint before use. Cleanup commits use `refactor:`/`ci:`/`test:`, and the feature commit uses `feat:` (minor release; obsolete shells keep the public API additive). A concurrent Story 8.3 closure is in the working tree; preserve it.

## Application Record (2026-10-09)

Applied after owner approval ("Approve and apply"). Nothing was staged, committed, or pushed.

| Artifact | Change |
| --- | --- |
| `planning-artifacts/epics.md` | Story 6.6 section replaced with §4.1 NEW (lines 5189–5265) |
| `planning-artifacts/prd.md` | FR33-C6 row reworded (§4.2) |
| `planning-artifacts/architecture.md` | AD-13: two Story 6.6 bullets replaced by the pragmatic-scope bullet (§4.3, plus the retained metadata-adapter withdrawal sentence). AD-1: the last sentence now says "The 2026-10-04 PostgreSQL metadata-adapter permission stays withdrawn, as recorded under AD-13; Story 6.6 follows AD-13's 2026-10-09 pragmatic scope." so it no longer points at the superseded amendment as live. |
| `ux-designs/ux-eventstore-2026-07-05/EXPERIENCE.md` | First two "Evolution and ambiguous-save outcomes" paragraphs replaced (§4.4) |
| `implementation-artifacts/epic-6-context.md` | §4.5 items 1–6 |
| `implementation-artifacts/sprint-status.yaml` | One comment line above the 6.6 key (outside the guarded blocks); status stays `in-progress` |
| `implementation-artifacts/spec-6-6-…-implementation.md`, `…-implementation-2.md` | `status: 'superseded'` + banner after the frontmatter |
| `implementation-artifacts/story-6-6-session-checkpoint-2026-10-08.md`, `story-6-6-checkpoint-initial-session-checkpoint-2026-10-09.md` | Banner after the title |
| `implementation-artifacts/deferred-work.md` | 47 Story 6.6 follow-ups set to `resolved 2026-10-09 … withdrawn`, keeping each original reference. `scripts/check-deferred-work.py` exits 0 both before and after (427 legacy-advisory, unchanged). |

Notes:

- The `inputDocumentDigests` in `epics.md` frontmatter were already stale for `prd.md`, `architecture.md`, and `EXPERIENCE.md` before this change, and no tool enforces them. They were left untouched.
- No test or CI job reads the four 6.6 documents that were stamped. Tests that do read 6.6 planning or evidence files (the amendments, `story-6-6-dapr-logical-model.md`, `6-6-obligation-audit.json`, evidence vectors) were left unchanged. Task 1 deletes them together with the files they read.
- A concurrent session was changing Story 8.3 and Security files in the same working tree. None of those files were touched.

