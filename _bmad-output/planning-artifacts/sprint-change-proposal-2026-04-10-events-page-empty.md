# Sprint Change Proposal: Events Page Permanently Empty (Missing Stream Activity Writer)

**Date:** 2026-04-10
**Triggered by:** Live investigation of Admin UI `/events` page — page shows zero events despite active command traffic
**Scope Classification:** Minor — Direct implementation by dev team
**Related prior proposals:** `sprint-change-proposal-2026-04-01-events-page.md` (Story 15.12), `sprint-change-proposal-2026-03-30.md` (Story 15.11)

---

## Section 1: Issue Summary

The Admin UI `/events` page (https://localhost:60034/events) is permanently empty in all environments — the grid never shows events, the stat cards always read `Recent Events = 0`, `Unique Event Types = 0`, `Active Streams = 0`, even after commands have been submitted and events persisted.

**Root cause:** `Pages/Events.razor` calls `AdminStreamApiClient.GetRecentlyActiveStreamsAsync()`, which on the Admin.Server side delegates to `DaprStreamQueryService.GetRecentlyActiveStreamsAsync` at `src/Hexalith.EventStore.Admin.Server/Services/DaprStreamQueryService.cs:99`. That method reads the DAPR state key `admin:stream-activity:{tenantId ?? "all"}`. **No code anywhere in `src/**/*.cs` writes to this key.** A grep for `admin:stream-activity` yields only the single reader plus tests — there is no corresponding writer.

**Why prior stories didn't catch this:**
- **Story 15.2** (`_bmad-output/implementation-artifacts/15-2-activity-feed-recent-active-streams.md`, status: done) built the reader and the UI but assumed the index would be populated by an "admin projection" — which was never implemented.
- **Story 15.11** (`sprint-change-proposal-2026-03-30.md`) fixed command-activity persistence and even referenced `admin:stream-activity:{tenantId}` as an existing pattern to mirror. That reference pointed at a reader-only file.
- **Story 15.12** (`sprint-change-proposal-2026-04-01-events-page.md`) implemented the Events page UI assuming `GetRecentlyActiveStreamsAsync` already worked.

Each story shipped correctly under its own assumptions, but the assumed writer never existed.

**Evidence gathered during investigation:**

1. **Grep:** `rg "admin:stream-activity" src/**/*.cs` returns exactly one match — `DaprStreamQueryService.cs:99` (the reader).
2. **Live state store probe against the running Aspire environment:**
   - `GET http://localhost:3501/v1.0/state/statestore/admin:stream-activity:all` → HTTP 204 (empty)
   - `GET http://localhost:3501/v1.0/state/statestore/admin:stream-activity:tenant-b` → HTTP 204 (empty)
   - `GET http://localhost:3501/v1.0/state/statestore/admin:command-activity:all` → 37 tracked commands across 10 unique `(tenant, domain, aggregate)` streams — confirming events are being produced
3. **Structured logs from `eventstore-admin` via Aspire MCP:** every Admin.Server request to `/api/v1/admin/streams` emits
   `Warning: Admin index 'admin:stream-activity:all' not found. Index population requires admin projection setup.`
4. **epics.md:559** confirms the misleading reference that misled Stories 15.11/15.12.

---

## Section 2: Impact Analysis

### Epic Impact

| Epic | Impact | Detail |
|------|--------|--------|
| **Epic 15 (Admin Web UI)** | Add one story | New Story 15.13 covers the missing writer. Existing stories 15.2/15.11/15.12 remain valid. |
| All other epics | No impact | Epics 1–14, 16–20 unaffected — no other consumer of `admin:stream-activity:*` |

### Story Impact

| Story | Action |
|-------|--------|
| Story 15.2 (Activity Feed & Recent Active Streams) | No change needed — the reader it built is correct; just needed the writer behind it |
| Story 15.11 (Persistent State Store and Command Activity) | Technical Notes updated: remove the incorrect "same pattern as `admin:stream-activity:{tenantId}`" reference and append a dated note pointing at Story 15.13 |
| Story 15.12 (Events Page Cross-Stream Browser) | No change needed — the UI is correct; just needed real data flowing into its data source |
| **New Story 15.13** | Added — covers the missing `DaprStreamActivityTracker` writer, hook-in to `SubmitCommandHandler`, DI registration, reader adaptation, and Tier 1 tests |

### Artifact Conflicts

| Artifact | Conflict | Action |
|----------|----------|--------|
| **PRD** | None | FR68–FR71 (admin UI browsing) are implicitly restored to conformance; no FR/NFR text change |
| **Architecture** | None | Uses established patterns — DAPR state store via `DaprClient`, optimistic concurrency via `GetStateAndETagAsync`/`TrySaveStateAsync`, Rule 12 (advisory tracking must not block command processing). No new components, technologies, or integration points |
| **UX Design** | None | Events.razor already matches the D3 Timeline-Centric layout. This is pure backend data plumbing |
| **epics.md** | Minor edits | Story 15.11 cross-reference cleanup + Story 15.13 insertion |
| **Tests** | Additive | New Tier 1 test file; existing `DaprStreamQueryServiceTests` and `DaprCommandActivityTrackerTests` remain valid |
| **CI/CD, IaC, deployment, docs** | None | No pipeline, infrastructure, or doc updates needed |

### Technical Impact

- **2 new files**
  - `src/Hexalith.EventStore.Server/Commands/IStreamActivityTracker.cs` — writer interface
  - `src/Hexalith.EventStore/Commands/DaprStreamActivityTracker.cs` — DAPR-backed writer
- **3 modified source files**
  - `src/Hexalith.EventStore.Server/Pipeline/SubmitCommandHandler.cs` — inject + invoke the tracker
  - `src/Hexalith.EventStore/Extensions/ServiceCollectionExtensions.cs` — register the tracker as singleton
  - `src/Hexalith.EventStore.Admin.Server/Services/DaprStreamQueryService.cs` — switch reader from per-tenant key to single global key with in-memory filtering (mirrors command tracker pattern)
- **1 new test file**
  - `tests/Hexalith.EventStore.Client.Tests/Commands/DaprStreamActivityTrackerTests.cs` — Tier 1 unit tests (7 tests)
- **1 modified planning artifact**
  - `_bmad-output/planning-artifacts/epics.md` — Story 15.11 note + Story 15.13 insertion
- **0 API contract changes** (no new DTOs, no new HTTP endpoints, no new DAPR components)
- **0 infrastructure changes** (reuses existing `statestore` DAPR component and `EventStore:CommandStatus` options binding)

---

## Section 3: Recommended Approach

**Selected path:** Direct Adjustment — Option 1.

**How it works:**
1. Introduce a `DaprStreamActivityTracker` that mirrors `DaprCommandActivityTracker` one-for-one (same lifetime, same ETag retry loop, same Rule 12 swallow-and-log behavior, same bounded list shape).
2. Hook it into `SubmitCommandHandler.Handle` immediately after `commandRouter.RouteCommandAsync` returns, right alongside the existing command-tracker block. The status read is hoisted and shared between both trackers to avoid a second round-trip.
3. Register the tracker in DI as a singleton alongside the command tracker.
4. Adapt the Admin.Server reader to the single-global-key model — a 30-line surgical change in `DaprStreamQueryService`.
5. Add a 7-case Tier 1 unit test file following the existing `DaprCommandActivityTrackerTests` conventions.
6. Clean up the misleading reference in epics.md and add a Story 15.13 entry.

**Why Option 1 over rollback or MVP reduction:**
- **Rollback (Option 2):** Would require reverting Stories 15.12 (Events page UI) and 15.2 (Streams page reader) — both shipping features already in use. Rollback gains nothing because the missing writer still has to be built regardless. Effort: High, Risk: High.
- **MVP Reduction (Option 3):** Not warranted — the gap is a single small component that takes <2 hours to implement and restores FR conformance. Effort: High (doc churn), Risk: Medium.
- **Direct Adjustment (Option 1):** Smallest blast radius, mirrors a proven pattern, zero API contract churn, zero UI churn, restores FR conformance immediately. **Effort: Low, Risk: Low.**

**Effort estimate:** Low — single story, ~2 hours implementation + testing.
**Risk level:** Low — no API changes, no schema changes, pattern is a literal clone of an already-working tracker.
**Timeline impact:** None — fits comfortably within current sprint.

---

## Section 4: Detailed Change Proposals

All seven edits were reviewed incrementally and approved individually during the course-correct workflow. Summaries below; see the workflow transcript for full before/after diffs on each.

### 4.1 New file — `IStreamActivityTracker` interface

**File:** `src/Hexalith.EventStore.Server/Commands/IStreamActivityTracker.cs`

Defines a single `TrackAsync(tenantId, domain, aggregateId, newEventsAppended, timestamp, ct)` method. Shape chosen to match what `SubmitCommandHandler.Handle` already has in scope. Rule 12 is documented explicitly in XML comments. No read method — the Admin.Server reader stays in `DaprStreamQueryService`.

### 4.2 New file — `DaprStreamActivityTracker` implementation

**File:** `src/Hexalith.EventStore/Commands/DaprStreamActivityTracker.cs`

Clone of `DaprCommandActivityTracker` adapted to `StreamSummary`:

- Single global key `admin:stream-activity:all`.
- Optimistic concurrency with 3 ETag retries.
- Bounded FIFO at 1000 entries ordered by `LastActivityUtc` descending.
- Cumulative counters: on update, reads existing `StreamSummary`, adds `newEventsAppended` to `EventCount` and sets `LastEventSequence = newEventCount` (valid because per FR10, per-aggregate sequences are gapless).
- Early return if `newEventsAppended <= 0` — rejected/idempotent commands don't pollute the index.
- `HasSnapshot: false` on first write; a future snapshot subsystem can upsert to flip it.
- `StreamStatus = Active` on every write; tombstoning is out of scope.
- Reuses `CommandStatusOptions.StateStoreName` — zero new configuration.
- All exceptions caught and logged (Rule 12).

### 4.3 Modified — `SubmitCommandHandler` pipeline hook

**File:** `src/Hexalith.EventStore.Server/Pipeline/SubmitCommandHandler.cs`

- Add optional `IStreamActivityTracker? streamActivityTracker` primary-constructor parameter.
- Preserve backwards compatibility by adding four overload constructors that forward to the new primary (existing tests/callers continue to compile unchanged).
- Hoist the `ReadStatusAsync` call out of the existing command-tracker block so both trackers share the result — avoids a second round-trip.
- New tracker invocation is gated on `processingResult.Accepted && (finalStatus?.EventCount ?? 0) > 0`, so rejected/no-op commands skip the DAPR write entirely.
- New log event `1105 StreamActivityTrackingFailed` mirrors the existing `1104 ActivityTrackingFailed`, with message text that mentions "Admin Streams/Events pages may be stale."

### 4.4 Modified — DI registration

**File:** `src/Hexalith.EventStore/Extensions/ServiceCollectionExtensions.cs`

Two lines added immediately after the command-tracker registration at line 113:

```csharp
_ = services.AddSingleton<DaprStreamActivityTracker>();
_ = services.AddSingleton<IStreamActivityTracker>(sp => sp.GetRequiredService<DaprStreamActivityTracker>());
```

Plus a comment explicitly flagging that this is writer-only — the reader lives in `DaprStreamQueryService` on the Admin.Server side.

### 4.5 Modified — Admin.Server reader adaptation

**File:** `src/Hexalith.EventStore.Admin.Server/Services/DaprStreamQueryService.cs`

- Switch from per-tenant key (`admin:stream-activity:{tenantId ?? "all"}`) to single constant key `admin:stream-activity:all`.
- Apply tenant and domain filters in memory as two independent `Where` clauses (symmetric with the command tracker's `GetRecentCommandsAsync`).
- Downgrade the "index not found" log from Warning to Debug, with new text: "Stream activity index '{IndexKey}' is empty. No commands have produced events yet, or the writer has not run." This prevents the current warning flood at every 30-second dashboard poll.
- Security unchanged — `AdminTenantAuthorizationFilter` in `AdminStreamsController` still force-scopes `tenantId` for non-admin users before this method is called.

### 4.6 New test file — Tier 1 unit tests

**File:** `tests/Hexalith.EventStore.Client.Tests/Commands/DaprStreamActivityTrackerTests.cs`

Seven tests following the existing `DaprCommandActivityTrackerTests` conventions:

| Test | Protects |
|---|---|
| `TrackAsync_NewStream_InsertsNewSummary` | First-write path (empty index → new entry with correct counters) |
| `TrackAsync_ExistingStream_AccumulatesEventCountAndSequence` | Accumulator math — cumulative `EventCount` and `LastEventSequence` |
| `TrackAsync_ZeroNewEvents_IsNoOp` | Rejected/idempotent commands pollute the index |
| `TrackAsync_DifferentAggregates_KeepsBothEntries` | Identity matching tuple `(tenant, domain, aggregate)` |
| `TrackAsync_SameAggregateDifferentTenants_KeepsBothEntries` | Multi-tenant isolation in a single global key |
| `TrackAsync_EtagMismatch_RetriesUntilSaveSucceeds` | Optimistic-concurrency retry loop |
| `TrackAsync_DaprThrows_SwallowsException` | Rule 12 — tracker failures cannot break command processing |

Uses NSubstitute for `DaprClient` mocking and Shouldly for assertions, matching project conventions.

### 4.7 Modified — `epics.md` cleanup + new Story 15.13

**File:** `_bmad-output/planning-artifacts/epics.md`

- **Story 15.11 Technical Notes (around line 559):** remove the incorrect reference to `admin:stream-activity:{tenantId}` as an existing pattern. Add a dated note (`**Note (2026-04-10):**`) pointing forward to Story 15.13 and this proposal.
- **Insert new Story 15.13 between 15.12 and Epic 16:** full Given/When/Then acceptance criteria (6 ACs), Technical Notes block listing all 5 source files and the test file, plus a "Root cause note" paragraph documenting the assumption chain through stories 15.2 → 15.11 → 15.12 → 15.13.

---

## Section 5: Implementation Handoff

**Scope:** Minor — direct implementation by dev team.

**Handoff:**

| Role | Responsibility |
|------|---------------|
| **Developer** | Implement all 7 edits from Section 4 in order (interface → impl → handler → DI → reader → tests → epics.md) |
| **Developer** | Build with zero warnings (`dotnet build Hexalith.EventStore.slnx --configuration Release`) |
| **Developer** | Run Tier 1 tests: `dotnet test tests/Hexalith.EventStore.Client.Tests/` — all existing tests must still pass, all 7 new tests must pass |
| **Developer** | Run Tier 2 tests: `dotnet test tests/Hexalith.EventStore.Server.Tests/` — verify no regression (requires `dapr init`) |
| **Developer** | Manual smoke test per success criteria below |

### Success Criteria

1. **Build** — `dotnet build Hexalith.EventStore.slnx --configuration Release` succeeds with zero warnings (`TreatWarningsAsErrors=true` is set repo-wide).
2. **Tier 1 tests** — All existing tests continue to pass, all 7 new `DaprStreamActivityTrackerTests` cases pass.
3. **Cold start smoke test**
   - Flush Redis + rebuild + `aspire run` (per project restart protocol in memory).
   - Confirm `admin:stream-activity:all` returns HTTP 204 before any commands.
   - Submit 3 `IncrementCounter` commands to the sample domain.
   - Confirm `admin:stream-activity:all` now contains a single `StreamSummary` entry with `EventCount = 3`, `LastEventSequence = 3`, `StreamStatus = Active`.
4. **Admin UI `/events` page**
   - Navigate to https://localhost:60034/events after submitting the commands.
   - Confirm stat cards show `Recent Events > 0`, `Unique Streams = 1`, `Unique Event Types ≥ 1`.
   - Confirm the grid shows the events with correct timestamps and event-type names.
   - Click a row → navigates to `/streams/{tenant}/{domain}/{aggregate}?detail={seq}`.
5. **Admin UI `/streams` page** — confirm no regression; streams list continues to render.
6. **Tenant filter** — select a tenant from the `/events` dropdown; confirm only that tenant's events show.
7. **Advisory failure test (optional)** — temporarily throw in `DaprStreamActivityTracker.TrackAsync`, submit a command, confirm the command still succeeds and only a warning is logged (Rule 12).
8. **No structured log flood** — confirm the 30-second dashboard poll no longer emits a Warning-level `Admin index 'admin:stream-activity:all' not found` line (it should be gone entirely or downgraded to Debug).

### New Story ID

`15-13-stream-activity-tracker-writer`

### Dependencies

None — all five source-file changes are self-contained. Optional but recommended: merge this story before the next `/events`-touching story to avoid rebase churn on `Events.razor`.

---

## Appendix: Investigation Transcript Highlights

- **Grep:** `rg "admin:stream-activity" src/**/*.cs` → 1 match (reader only).
- **Live state probe:** `curl http://localhost:3501/v1.0/state/statestore/admin:stream-activity:all` → HTTP 204.
- **Live state probe:** `curl http://localhost:3501/v1.0/state/statestore/admin:command-activity:all` → 37 entries across 10 streams.
- **Aspire structured log sample (eventstore-admin):** `"Admin index 'admin:stream-activity:all' not found. Index population requires admin projection setup."` — emitted by `DaprStreamQueryService` at every dashboard poll.
- **epics.md line 559 confirmation:** the Story 15.11 Technical Note that claims to "Follow same pattern as `admin:stream-activity:{tenantId}` in `DaprStreamQueryService`" — the pattern referenced does not exist in the writing direction.
