# Story 15.3: Event Versioning & Schema Evolution Guide

Status: done

<!-- Note: Validation is optional. Run validate-create-story for quality check before dev-story. -->

## Story

As a developer evolving their domain model,
I want to understand how event versioning and schema evolution are handled,
so that I can safely change event structures without breaking existing data.

## Acceptance Criteria

1. **Given** a developer navigates to `docs/concepts/event-versioning.md` **When** they read the content **Then** the content explains how Hexalith handles event schema changes, including the three versioning metadata fields (`domainServiceVersion`, `eventTypeName`, `serializationFormat`) and their role in evolution
2. **And** the guide covers strategies for upcasting events (transforming old event format to new) with concrete code examples using the Counter domain
3. **And** the guide covers downcasting considerations (handling newer events in older consumers) and explains Hexalith's error-first philosophy (`UnknownEventException`)
4. **And** the guide documents backward compatibility guarantees: domain services MUST maintain backward-compatible deserialization for all event types they have ever produced
5. **And** examples use the Counter domain to show a concrete versioning scenario (e.g., adding a field to `CounterIncremented`, renaming an event type, splitting an event into two)
6. **And** the page follows the standard page template: back-link, H1, summary paragraph, prerequisites callout, content sections, Next Steps footer
7. **And** the guide is linked from `README.md` Concepts section
8. **And** `docs/concepts/event-envelope.md` forward-reference (line 293) is replaced with an actual link to `event-versioning.md`
9. **And** `docs/fr-traceability.md` is updated: FR51 from `GAP` to `COVERED` referencing `docs/concepts/event-versioning.md`
10. **And** markdownlint-cli2 passes with project config (`.markdownlint-cli2.jsonc`)

## Tasks / Subtasks

- [x] Task 1: Create `docs/concepts/event-versioning.md` (AC: #1, #2, #3, #4, #5, #6)
    - [x] 1.1 Write page following standard template: back-link `[← Back to Hexalith.EventStore](../../README.md)`, H1, summary paragraph, prerequisites callout linking to `event-envelope.md` and `command-lifecycle.md`. The summary paragraph must define "event versioning" and "schema evolution" in plain language before using these terms. Include a brief glossary callout early in the page defining key terms for beginners: **upcasting** (transforming old event format to new during replay), **downcasting** (handling newer events in older consumers), **rehydration** (rebuilding current state by replaying stored events), **aggregate state** (the current state of a domain object derived from its event history). These definitions let beginners follow along without slowing down experienced readers.
    - [x] 1.2 Section: "Why Error-First?" — lead with the _why_: "Skipping unknown events produces incorrect aggregate state" (Architecture.md D3). Explain the error-first philosophy (`UnknownEventException`) and the backward compatibility contract BEFORE showing any how-to strategies. This section sets the foundation for everything that follows.
    - [x] 1.3 Section: "The Versioning Metadata Foundation" — introduce the three metadata fields (`domainServiceVersion`, `eventTypeName`, `serializationFormat`) and how they enable safe evolution. Link to `event-envelope.md` for full field details — do NOT re-explain what that page already covers. However, include enough substance that a reader who skipped prerequisites can still follow the guide (brief 1-2 sentence summary per field + link for depth). Explicitly state that `eventTypeName` is **immutable once persisted** — renaming a .NET class does NOT change the stored type name. Also note: event type names are **scoped per-domain** (two different domains can have `OrderCreated` without conflict), but within the same domain, type name uniqueness is the developer's responsibility. Include the serialization format migration content here as a brief subsection (2-3 paragraphs) rather than a separate section — explain _why_ the `serializationFormat` field exists (future incremental migration, e.g. JSON → Protobuf) and that it's currently always `"json"`. Frame safe/unsafe classifications as "with the default JSON serializer" and note that `ISerializedEventPayload` allows custom serializers with potentially different behavior.
    - [x] 1.4 Section: "Common Schema Evolution Scenarios" — table of common changes with safe/unsafe classification and recommended approach. CRITICAL distinctions to make explicit:
        - "Add optional/nullable field" = SAFE (`System.Text.Json` behavior: missing JSON property → `default(T)` for value types, `null` for reference types)
        - "Add required field without default" = UNSAFE (deserialization produces unexpected defaults, breaks domain invariants)
        - "Rename .NET event class" = UNSAFE (persisted `eventTypeName` never changes — old events become `UnknownEventException`). Recommend: keep old class as alias or use `ISerializedEventPayload.EventTypeName` to decouple type name from class name
        - "Move event to different namespace" = UNSAFE (`eventTypeName` includes fully qualified name with namespace — same breakage as class rename)
        - "Delete event class" = UNSAFE (NEVER do this — mark `[Obsolete]` instead. The class must exist for as long as events of that type exist in any stream. Suggest organizing legacy events in a dedicated `Events/Legacy/` folder or similar)
        - "Remove field" = SAFE (System.Text.Json ignores extra JSON properties by default)
        - "Rename field" = UNSAFE (old JSON key won't map — use `[JsonPropertyName("oldName")]` attribute)
        - "Change field type" = depends: widening (int → long) is SAFE, narrowing (long → int) is UNSAFE (overflow), cross-type (int → string) BREAKS (`JsonException`)
        - "Add new enum member" = SAFE for old events, but CAUTION: new events with unknown enum value may confuse older consumers depending on `System.Text.Json` enum handling config
        - "Split event into two" / "Merge events" = UNSAFE (requires upcasting logic)
        - Note on C# records: "optional field" safety depends on `System.Text.Json` constructor resolution. Records with constructor parameters: if the new field has no default value and no `[JsonConstructor]`-annotated parameterless path, deserialization may throw. Recommend: always use default parameter values for new record fields (e.g., `int IncrementedBy = 1`)
    - [x] 1.5 Section: "Upcasting Strategies" — explain transforming old event payloads to new formats during deserialization, with Counter domain code example (e.g., `CounterIncremented` v1 → v2 with new `IncrementedBy` field). Use actual patterns from `CounterProcessor.cs` (e.g., `eventTypeName.EndsWith()` for type matching, multi-representation state handling) — show the real code, not idealized versions. Include a Mermaid sequence diagram showing the upcasting flow: old event in store → deserialize → transform → new state. Start with a simple before/after for beginners: "Here's event v1, here's event v2, here's what happens when old v1 events get replayed." Explicitly state: there is NO formal upcaster pipeline today (like EventStoreDB's `IEventUpcaster`) — the extension point is the domain service's `Apply` method and state rehydration logic. Document the `EndsWith()` pattern: explain both the WHY (resilience to namespace changes in type matching) and the RISK (suffix collision if two events share the same suffix — e.g., `OrderItemIncremented` and `CounterIncremented` both end with `Incremented`). Note: the backward compatibility contract applies to ALL event consumers — not just domain service rehydration, but also projections, read models, and integration subscribers.
    - [x] 1.6 Section: "Domain Service Version Routing" — explain how `domainServiceVersion` enables multiple versions running simultaneously via DAPR config store, with rollback capability. Include a deployment checklist: 1) Deploy new service version, 2) Update DAPR config store mapping, 3) Verify routing with test command, 4) Monitor for `UnknownEventException` errors. Warn about the common mistake: deploying v2 but forgetting to update config store mapping (commands still route to v1). Note about config store eventual consistency: during propagation window, some instances may route to v1 and others to v2 — design for this. Mention static fallback registration as safety net for critical domain services when config store is temporarily unavailable.
    - [x] 1.7 Section: "Counter Domain Versioning Example" — end-to-end walkthrough of evolving the Counter domain: adding a field, handling old events, deploying new version, verifying backward compatibility. IMPORTANT: before writing examples, verify current Counter domain code in `samples/Hexalith.EventStore.Sample/Counter/CounterProcessor.cs` is unchanged — use actual current code, not stale snapshots. Must cover **snapshot state evolution**: snapshots contain serialized state, so state shape changes require multi-format `Rehydrate*` handling (already demonstrated in `RehydrateCount` which handles null, typed, JSON, and enumerable representations). Old snapshots must remain deserializable. Include a verification/testing strategy: recommend testing backward compat by replaying synthetic old-format events and asserting correct state reconstruction. Add callout: "Counter is intentionally simple. For complex domain models with nested objects, collections, or polymorphic types, the same patterns apply but pay extra attention to nested deserialization and `System.Text.Json` converter behavior."
    - [x] 1.8 Section: "What's Coming (v3 Roadmap)" — brief mention of planned upcasting framework, schema registry, migration tooling (deferred to v3 per architecture roadmap). Draw a razor-sharp boundary: clearly separate what EXISTS today (metadata fields, version routing, manual strategies) from what is PLANNED (automated tooling). No vaporware. Honestly acknowledge the scaling limitation: manual upcasting works well for small-to-medium domains but becomes harder to maintain past ~10 evolving event types — v3 tooling addresses this real pain point, not a theoretical one. Also note: envelope schema changes between Hexalith versions are treated as MAJOR version bumps (per Architecture.md line 196); EventStore guarantees backward-compatible reading of all previously persisted envelopes.
    - [x] 1.9 Next Steps footer: link to `event-envelope.md`, `identity-scheme.md`, `first-domain-service.md`
- [x] Task 2: Update cross-references (AC: #7, #8)
    - [x] 2.1 Update `docs/concepts/event-envelope.md` line 293 — replace forward-reference with actual link to `event-versioning.md`
    - [x] 2.2 Add `event-versioning.md` link to `README.md` Concepts section
    - [x] 2.3 Add cross-link from `docs/guides/configuration-reference.md` if it mentions domain service versioning
- [x] Task 3: Update FR traceability (AC: #9)
    - [x] 3.1 Update `docs/fr-traceability.md` — set FR51 from `GAP` to `COVERED` referencing `docs/concepts/event-versioning.md`
    - [x] 3.2 Update gap summary counts (currently 16 gaps → 15)
    - [x] 3.3 Update coverage percentage
- [x] Task 4: Validate with markdownlint-cli2 (AC: #10)
    - [x] 4.1 Run `npx markdownlint-cli2 docs/concepts/event-versioning.md` and fix any issues
    - [x] 4.2 Verify all internal links resolve to existing files

## Dev Notes

### Architecture Context: Event Versioning Metadata Foundation

Hexalith.EventStore has three metadata fields in the event envelope that form the versioning foundation:

| Field                  | Source                                                              | Purpose                                                                                           |
| ---------------------- | ------------------------------------------------------------------- | ------------------------------------------------------------------------------------------------- |
| `domainServiceVersion` | Command envelope `domain-service-version` extension (default: `v1`) | Records which version of domain service produced the event; enables version-aware deserialization |
| `eventTypeName`        | .NET type name or `ISerializedEventPayload.EventTypeName`           | Type identifier for deserialization routing; CloudEvents `type` attribute                         |
| `serializationFormat`  | Default `"json"` or `ISerializedEventPayload.SerializationFormat`   | Per-event encoding format; enables incremental format migration                                   |

**EventStore owns all 11 metadata fields** — domain services return only the payload. This immutability prevents event stream poisoning.

### Error-First Deserialization Philosophy

When `EventStreamReader` encounters an event whose `eventTypeName` cannot be deserialized:

- `UnknownEventException` is thrown (NOT silently skipped)
- Rationale: "Skipping unknown events produces incorrect aggregate state" (Architecture.md D3)
- Recovery: redeploy previous domain service version or add backward-compatible deserializer

Key source files:

- `src/Hexalith.EventStore.Server/Events/UnknownEventException.cs`
- `src/Hexalith.EventStore.Server/Events/EventDeserializationException.cs`
- `src/Hexalith.EventStore.Server/Events/EventStreamReader.cs`

### Domain Service Version Routing (Already Implemented)

Version-based service resolution via DAPR config store:

- Resolution key: `{tenantId}:{domain}:{version}` (or `{tenantId}|{domain}|{version}` for config-friendly format)
- Version format: `v{number}` (regex: `^v[0-9]+$`, default: `v1`)
- Supports multiple versions of same domain service running simultaneously
- Instant rollback via config store update (no redeployment)

Source: `src/Hexalith.EventStore.Server/DomainServices/DaprDomainServiceInvoker.cs`, `DomainServiceResolver.cs`

### Counter Sample Backward-Compatibility Pattern

The Counter domain (`samples/Hexalith.EventStore.Sample/Counter/CounterProcessor.cs`) demonstrates the manual backward-compatibility pattern:

```csharp
// Handles multiple state representations (null, typed, JSON, enumerable)
private static int RehydrateCount(object? currentState) { ... }
// Applies events by type name substring matching
private static void ApplyEventToCount(string eventTypeName, ref int countValue) { ... }
```

**This is the pattern to document** — developers must follow this approach for backward-compatible state rehydration.

### Content Strategy: Complement, Don't Duplicate

The `event-envelope.md` page (11,400 words) already covers metadata field details extensively. This guide MUST:

- **Link** to `event-envelope.md` for field definitions and anatomy — do NOT re-explain
- **Add new value**: versioning _strategies_, _patterns_, and _examples_ that don't exist elsewhere
- **Lead with why**: the error-first philosophy ("Skipping unknown events produces incorrect aggregate state") must come BEFORE any how-to content — developers must understand the contract before learning techniques
- **Use real code**: show actual patterns from `CounterProcessor.cs` (e.g., `eventTypeName.EndsWith()`, multi-representation state handling) — not idealized abstractions
- **Self-contained enough**: readers may land on this page directly via search. The metadata field summary must be substantive enough to follow without reading `event-envelope.md` first (brief 1-2 sentence per field + link for depth)

### Critical: `eventTypeName` Immutability

The `eventTypeName` field is **persisted forever** with each event. Renaming a .NET class does NOT retroactively update stored type names. This is the #1 mistake developers will make. The scenarios table MUST prominently warn about this and recommend keeping old class as alias or using `ISerializedEventPayload.EventTypeName` to decouple.

### Critical: `System.Text.Json` Deserialization Behavior

The guide must document `System.Text.Json` behavior because it's the mechanism behind safe/unsafe classifications:

- Missing JSON property → `default(T)` for value types, `null` for reference types (makes "add optional field" safe)
- Extra JSON property → ignored by default (makes "remove field" safe)
- Wrong type for property → `JsonException` thrown
- Type widening (int → long) → silent, safe
- Type narrowing (long → int) → overflow risk
- Cross-type (int → string) → `JsonException`

**C# Records caveat:** Records with constructor parameters require special attention. If a new field has no default value and no `[JsonConstructor]`-annotated parameterless path, deserialization of old events (missing that JSON property) may throw. Best practice: always use default parameter values for new record fields (e.g., `int IncrementedBy = 1`).

### Critical: Never Delete Event Classes

Event classes must exist for as long as events of that type exist in ANY stream. Deleting a class causes `UnknownEventException` on rehydration. Instead:

- Mark with `[Obsolete("Superseded by EventV2, kept for backward-compatible deserialization")]`
- Move to `Events/Legacy/` or a dedicated legacy folder to keep the active codebase clean
- Ensure they remain compilable and deserializable

### Critical: `eventTypeName` Includes Full Namespace

The `eventTypeName` stores the fully qualified .NET type name including namespace. Moving `MyApp.Events.CounterIncremented` to `MyApp.V2.Events.CounterIncremented` breaks deserialization identically to a class rename. This must be in the scenarios table alongside class rename.

### Critical: Backward Compat Contract Applies to ALL Consumers

The backward compatibility requirement is NOT limited to domain service rehydration. It applies equally to:

- Projections and read models
- Integration event subscribers
- Any consumer that deserializes events from the store or pub/sub

The guide must frame this as a system-wide contract, not just a domain service concern.

### `EndsWith()` Pattern: Trade-offs

The Counter sample uses `eventTypeName.EndsWith("CounterIncremented")` for type matching. Document both sides:

- **WHY:** Resilience to namespace changes — if the event moves namespaces, the suffix still matches
- **RISK:** Suffix collision — `OrderItemIncremented` and `CounterIncremented` both end with `Incremented`. In single-domain scenarios this is unlikely, but multi-domain assemblies could hit this
- **Recommendation:** Use the most specific suffix possible, or exact match for production services with many event types

### Critical: Snapshot State Evolution

Snapshots contain serialized aggregate state. State shape changes break snapshot deserialization unless handled. The Counter sample already demonstrates this: `RehydrateCount()` handles null, typed object, `JsonElement`, and enumerable representations. This multi-format pattern is MANDATORY for any state that evolves. The guide must cover state evolution alongside event evolution — they are inseparable in practice.

### Competitive Context

EventStoreDB and Marten both provide formal `IEventUpcaster` interfaces. Hexalith's manual approach is valid for v1 but the guide should:

1. Show developers the exact code extension point (domain service `Apply` method / state rehydration)
2. Acknowledge competing tools offer more automation
3. Position v3 tooling as closing this gap
4. Honestly note manual approach doesn't scale well past ~10 evolving event types
5. Suggest organizational strategy for large domains: group events by domain, version folders, centralized backward-compat test suite

### What Is NOT Implemented (Deferred to v3)

Per architecture roadmap and product brief:

- Upcasting framework (transforming old → new event format)
- Downcasting framework
- Event schema registry
- Migration/evolution DSL
- Auto-versioning based on payload schema changes

**The guide should document what EXISTS (metadata fields, version routing, error-first contract) and the MANUAL strategies developers should use TODAY, while noting planned tooling for v3. The boundary between "today" and "v3 planned" must be razor-sharp — no vaporware.**

### Existing Forward-Reference to Update

`docs/concepts/event-envelope.md` line 293: _"A future guide on event versioning will cover the full strategy."_
→ Replace with actual link to `docs/concepts/event-versioning.md`

### Standard Page Template

```text
[← Back to Hexalith.EventStore](../../README.md)
# Page Title
One-paragraph summary.
> **Prerequisites:** [link1](path), [link2](path)
## Content Sections
## Next Steps
- **Next:** [page](link) — description
- **Related:** [page1](link), [page2](link)
```

### FR Traceability Current State

From `docs/fr-traceability.md`:

- FR51: `GAP` → needs update to `COVERED`
- Current gap count: 16 → will become 15
- Current coverage: ~75% → will increase slightly

### Project Structure Notes

- Target file: `docs/concepts/event-versioning.md` (new file)
- Alignment: `docs/concepts/` folder already contains related pages (`event-envelope.md`, `identity-scheme.md`, `command-lifecycle.md`)
- Follows architecture D1 folder structure
- Mermaid diagrams encouraged for flows (used extensively in `event-envelope.md` and `architecture-overview.md`)

### References

- [Source: _bmad-output/planning-artifacts/epics.md#Story 8.3]
- [Source: _bmad-output/planning-artifacts/architecture.md#D3 Domain Service Contract]
- [Source: _bmad-output/planning-artifacts/prd-documentation.md#FR51]
- [Source: docs/concepts/event-envelope.md#Versioning Fields]
- [Source: docs/fr-traceability.md#FR51 — currently GAP]
- [Source: src/Hexalith.EventStore.Contracts/Events/EventMetadata.cs]
- [Source: src/Hexalith.EventStore.Server/Events/UnknownEventException.cs]
- [Source: src/Hexalith.EventStore.Server/Events/EventStreamReader.cs]
- [Source: src/Hexalith.EventStore.Server/DomainServices/DaprDomainServiceInvoker.cs]
- [Source: samples/Hexalith.EventStore.Sample/Counter/CounterProcessor.cs]
- [Source: _bmad-output/implementation-artifacts/15-2-auto-generated-api-reference-and-ci-workflow.md — previous story learnings]

### Previous Story Intelligence (from Story 15-2)

- **Page template:** back-link `[← Back to Hexalith.EventStore](../../README.md)`, H1, intro paragraph, prerequisites blockquote, content sections, Next Steps footer
- **markdownlint-cli2** must pass with project config (`.markdownlint-cli2.jsonc`)
- **Branch pattern:** `docs/story-15-3-event-versioning-guide`
- **Commit pattern:** `feat(docs): Add event versioning and schema evolution guide (Story 15-3)`
- **Internal links:** All internal links must resolve to existing files
- **Cross-reference updates** are part of the story (update related docs' Next Steps)
- **Code blocks** need language hints for syntax highlighting
- **FR traceability** update is required (FR51: GAP → COVERED)
- All doc stories: feature branch per story, single commit with `feat(docs):` prefix, merge via PR

### Git Intelligence

Recent commits show consistent documentation pattern:

```text
a201d73 Merge pull request #93 from Hexalith/fix/sln-to-slnx-references
f825bf9 fix: replace .sln references with .slnx across docs
cf5d0bf Merge pull request #92 from Hexalith/docs/story-15-2-ready-for-dev
b666ce3 docs: add Story 15-2 spec and mark ready-for-dev
a73bcd3 feat: Add IEventPayloadProtectionService for GDPR payload encryption (#91)
```

All doc stories follow: feature branch → single commit with `feat(docs):` prefix → merge via PR.

## Dev Agent Record

### Agent Model Used

Claude Opus 4.6

### Debug Log References

- markdownlint-cli2 initially reported 2 errors (MD028 blank line inside blockquote, MD040 missing language on code block); both fixed and re-validated to 0 errors

### Completion Notes List

- Created comprehensive event versioning guide (~350 lines) covering all 9 subtasks
- Guide follows complement-not-duplicate strategy: links to event-envelope.md for field details, adds new versioning strategies/patterns/examples
- Used real code from CounterProcessor.cs (EndsWith pattern, RehydrateCount multi-format handling)
- Includes Mermaid sequence diagram for upcasting flow
- Safe/unsafe schema change table with System.Text.Json behavior explanations and C# records caveat
- Domain service version routing section with deployment checklist and common mistakes
- End-to-end Counter domain evolution walkthrough with testing strategy
- v3 roadmap section with razor-sharp boundary between what exists today and what is planned
- Updated 4 cross-reference files: event-envelope.md, README.md, configuration-reference.md, fr-traceability.md
- FR51 moved from GAP to COVERED (44 covered, 16 gaps, 75% coverage)
- ✅ Resolved review finding [HIGH]: Fixed `domainServiceVersion` source description in event-versioning.md and event-envelope.md — corrected from "assembly version or EventStoreDomainOptions override" to "command envelope's domain-service-version extension key (default: v1)". Also fixed example values from "1.0.0" to "v1" in event-envelope.md.
- ✅ Resolved review finding [MEDIUM]: Reverted non-15-3 scope changes from `README.md` (removed Upgrade Path and Type Documentation links from 15-4 and 15-2) and `docs/fr-traceability.md` (reverted FR19 and FR52 back to GAP, recalculated summary to 44/16/75%).
- ✅ Resolved review finding [MEDIUM]: `README.md` and `docs/fr-traceability.md` now only contain story 15-3 scope changes. Adjacent story claims (15-2 FR19, 15-4 FR52) were removed from those files so this story no longer depends on sibling documentation outputs.
- ✅ Resolved review finding [LOW]: Reconciled Completion Notes statistics with actual fr-traceability.md state (44 covered, 16 gaps, 75% coverage).
- ✅ Resolved review finding [MEDIUM]: Git isolation resolved — all changes merged to main via PR #95 (commit `1d4eb5f`), working tree clean, all story deliverables verified in place.
- ✅ Resolved review finding [HIGH]: Added an explicit downcasting considerations section to `docs/concepts/event-versioning.md`, including Counter-domain guidance on when older consumers must fail fast versus when a compatibility shim is required.
- ✅ Resolved review finding [MEDIUM]: Corrected `docs/concepts/event-envelope.md` to match the implementation — `eventTypeName` examples now use the fully qualified type name and the sample payload now matches the current marker-event `CounterIncremented` shape.
- ✅ Resolved review finding [MEDIUM]: Aligned `README.md` architecture wording with the documented product scope by removing premature `gRPC` claims and describing the v1 gateway as REST-based.
- ✅ Resolved review finding [MEDIUM]: Corrected `docs/fr-traceability.md` so FR54 is no longer marked `COVERED` without evidence; summary counts and Epic 15 gap tracking now reflect the current repository state.

### Change Log

- 2026-03-10: Created docs/concepts/event-versioning.md — event versioning and schema evolution guide (Story 15-3)
- 2026-03-10: Updated docs/concepts/event-envelope.md — replaced forward-reference with actual link to event-versioning.md
- 2026-03-10: Updated README.md — added event-versioning.md to Concepts section
- 2026-03-10: Updated docs/guides/configuration-reference.md — added cross-link to event-versioning.md in Domain Services section
- 2026-03-10: Updated docs/fr-traceability.md — FR51 GAP→COVERED, gap count 17→16, coverage 73%→75%
- 2026-03-10: Addressed code review findings — 4 items resolved (Date: 2026-03-10)
- 2026-03-10: Fixed domainServiceVersion source description in event-versioning.md and event-envelope.md (command envelope extension, not assembly version)
- 2026-03-10: Reverted non-15-3 scope changes from README.md and fr-traceability.md (15-2 FR19, 15-4 FR52) for clean story isolation
- 2026-03-10: Final verification — all deliverables confirmed in main (PR #95 merged), all review findings resolved, markdownlint 0 errors, all internal links valid
- 2026-03-10: Added downcasting guidance, corrected event envelope examples to match implementation, aligned README transport wording to REST-only v1, and fixed FR54 traceability accuracy

### File List

- docs/concepts/event-versioning.md (new — event versioning and schema evolution guide, now with explicit downcasting considerations)
- docs/concepts/event-envelope.md (modified — forward-reference replaced with link; version-source description corrected; aggregate/event examples aligned to the current implementation)
- README.md (modified — event-versioning.md link added to Concepts section; architecture wording aligned to REST-only v1 scope)
- docs/guides/configuration-reference.md (modified — Domain Services cross-link added)
- docs/fr-traceability.md (modified — FR51 GAP→COVERED retained; FR54 corrected to GAP; summary and gap analysis updated)
- \_bmad-output/implementation-artifacts/sprint-status.yaml (modified — story status)
- \_bmad-output/implementation-artifacts/15-3-event-versioning-and-schema-evolution-guide.md (modified — task checkboxes, Dev Agent Record, review follow-ups, status)

## Senior Developer Review (AI)

### Reviewer

GitHub Copilot (GPT-5.4)

### Outcome

All findings resolved — documentation accuracy fixed, downcasting guidance added, transport wording aligned to the shipped REST API, and traceability corrected to match the current repository state. Story complete.

### Review Notes

- [x] (AI-Review/HIGH) `docs/concepts/event-versioning.md` documented `domainServiceVersion` as being set from the assembly version or an `EventStoreDomainOptions` override, but the implementation persists the version extracted from the command envelope's `domain-service-version` extension (defaulting to `v1`). This is visible in `src/Hexalith.EventStore.Server/Actors/AggregateActor.cs` (`DaprDomainServiceInvoker.ExtractVersion(...)`) and `src/Hexalith.EventStore.Server/Events/EventPersister.cs` (`DomainServiceVersion: domainServiceVersion`). The same incorrect source description also remained in `docs/concepts/event-envelope.md`. **Resolved:** both docs now describe the implemented command-extension-based version source and example values use `v1`.
- [x] (AI-Review/MEDIUM) The story's `File List` does not match git reality. The working tree still contains unrelated changes to story artifacts (`15-2`, `15-4`), untracked generated reference docs under `docs/reference/api/`, untracked `docs/guides/upgrade-path.md`, and unrelated source/package changes under `src/` and project files. **Resolved:** All changes merged to main via PR #95 (`1d4eb5f`). Working tree is clean. Story deliverables verified in place.
- [x] (AI-Review/MEDIUM) `README.md` and `docs/fr-traceability.md` claimed outputs from adjacent stories (`Type Documentation` / FR19 from story 15-2 and `Upgrade Path` / FR52 from story 15-4). Those artifacts were outside the declared scope of story 15-3. **Resolved:** those sibling-story claims were removed from `README.md` and `docs/fr-traceability.md`.
- [x] (AI-Review/LOW) The story's `Completion Notes List` said FR traceability ended at `45 covered, 15 gaps, 76% coverage`, but the current `docs/fr-traceability.md` summary differed. **Resolved:** the story record now matches the current `44 covered, 16 gaps, 75%` summary used by the scoped 15-3 traceability state.
- [x] (AI-Review/HIGH) The original guide defined downcasting but did not actually explain how older consumers should handle newer events. **Resolved:** `docs/concepts/event-versioning.md` now includes an explicit downcasting section with Counter-based semantic-compatibility guidance, fail-fast recommendations for correctness-critical consumers, and compatibility-shim guidance for older downstream systems.
- [x] (AI-Review/MEDIUM) `docs/concepts/event-envelope.md` used simplified examples that did not match the implementation: it showed `eventTypeName` as `"CounterIncremented"`, used a payload that decoded to `{"amount": 1}`, and implied `aggregateId` persisted the full canonical identity. **Resolved:** the page now reflects the real persisted shapes — local `aggregateId`, fully qualified `eventTypeName`, and the current marker-event payload `{}`.
- [x] (AI-Review/MEDIUM) `README.md` described the Command API as `REST/gRPC`, but the documented architecture is REST-only in v1 and defers gRPC to a later version. **Resolved:** the README diagram and architecture description now describe the v1 gateway as REST-based.
- [x] (AI-Review/MEDIUM) `docs/fr-traceability.md` marked FR54 as covered even though the README did not contain a release-tag version reference. **Resolved:** FR54 is now tracked as a gap, and the summary/gap analysis counts were recalculated to match the repository state.
