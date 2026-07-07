# Sprint Change Proposal — Raw SignalR Hub Leave Validation Gap

- **Date:** 2026-07-07
- **Author:** Amelia (Developer)
- **Trigger source:** Epic 2 retrospective (`epic-2-retro-2026-07-07.md`) Action #6; deferred-work ledger entry (Story 2.5)
- **Change scope classification:** **Minor** (direct developer implementation)
- **Chosen path:** Direct Adjustment — patch now (option confirmed by Administrator over "explicitly schedule")
- **Status:** Implemented and validated

---

## Section 1 — Issue Summary

**Problem statement.** `ProjectionChangedHub` (`src/Hexalith.EventStore/SignalRHub/ProjectionChangedHub.cs`) validated its group-join path more strictly than its group-leave path. `JoinGroupCoreAsync` rejected null/blank `projectionType`/`tenantId` and rejected colons in either (the `:` is the reserved group-name separator). `LeaveGroupCoreAsync` — after Story 2.5 added scoped-suffix validation — still applied only the scope colon/length guards and **omitted the projection/tenant null, blank, and colon guards**.

**How discovered.** Surfaced during the Story 2.5 review (`spec-2-5-scoped-metadata-rich-projection-notifications.md`), logged as a deferred-work entry, and carried into the Epic 2 retrospective as Action #6: *"Patch or explicitly schedule the raw SignalR hub leave validation gap."*

**Evidence / failure path.** A malformed raw `LeaveGroup(null, "a:b")` or `LeaveGroup("x:y", "acme")` call flowed unchecked into `BuildGroupName` → `Groups.RemoveFromGroupAsync` → the `ClientLeftGroup` debug log with an invalid group name. Impact is **low severity** — group removal is idempotent (removing a connection from a group it never joined is a harmless no-op) and the only leak was a malformed name in a `Debug`-level log — but it is a real join/leave asymmetry that the retrospective completion gate required closing.

**Scope boundary (what this change does *not* do).** The leave path is intentionally **authorization-free**: a client must always be able to leave a group, so no tenant authorization is added to `LeaveGroupCoreAsync`. The fix is input-validation symmetry only.

---

## Section 2 — Impact Analysis

- **Epic Impact:** Epic 2 only — closes the last open retrospective action owned by the Developer. No epic scope change.
- **Story Impact:** No active or future story is rewritten. This is post-Story-2.5 hardening carried as deferred work; the **Correct-Course Story Rewrite Gate does not trigger** (no architectural pivot, no active-story AC/task/Dev-Notes supersession). See gate assessment below.
- **Artifact Conflicts:** None. Story 2.5's "Always" invariants (backward-compatible `JoinGroup`/`LeaveGroup`, group `{projectionType}:{tenantId}`, run tenant authorization before membership changes) are preserved — the join path is untouched and the leave path gains only defensive input validation.
- **Technical Impact:** One production method hardened; five unit-test cases added. No API surface, wire-format, group-name, or public-signature change. Backward compatible.

### Story Rewrite Gate assessment (required by correct-course customization)

The gate demands explicit old→new story rewrites when a change is an architectural pivot or supersedes active story ACs/tasks/Dev-Notes/design assumptions. This change is a localized defensive patch to already-shipped Story 2.5 code that *adds* validation without altering any documented behavior or assumption. **No story rewrite is required; the gate is satisfied (N/A).**

---

## Section 3 — Recommended Approach

**Direct Adjustment — patch now.** Rationale: the fix is trivial (~10 lines), strictly additive, symmetric with the existing, already-reviewed `JoinGroupCoreAsync` guards, and low-risk. Scheduling it as a named backlog item would defer roughly fifteen minutes of work while adding tracking overhead. Administrator confirmed this path over the "explicitly schedule" alternative.

- **Effort:** ~15 minutes (1 method, 5 test cases).
- **Risk:** Very low — additive validation, no behavior change on valid input; full Server.Tests suite green.
- **Timeline impact:** None; closes an open action within the current session.

---

## Section 4 — Detailed Change Proposals

### 4.1 Production code — `src/Hexalith.EventStore/SignalRHub/ProjectionChangedHub.cs`

**Section:** `LeaveGroupCoreAsync` (method entry)

**OLD:**
```csharp
private async Task LeaveGroupCoreAsync(string projectionType, string tenantId, string? scope) {
    string? normalizedScope = string.IsNullOrWhiteSpace(scope) ? null : scope;
```

**NEW:**
```csharp
private async Task LeaveGroupCoreAsync(string projectionType, string tenantId, string? scope) {
    ArgumentException.ThrowIfNullOrWhiteSpace(projectionType);
    ArgumentException.ThrowIfNullOrWhiteSpace(tenantId);

    // Defense-in-depth: colons are reserved as group name separator. Applied on the leave
    // path too so a malformed raw LeaveGroup/LeaveGroupScoped call cannot build and remove
    // an invalid group name or emit one to the debug log. Leaving stays authorization-free
    // by design — removing a connection from a group it is not in is idempotent and harmless.
    if (projectionType.Contains(':') || tenantId.Contains(':')) {
        throw new HubException("projectionType and tenantId must not contain colons.");
    }

    string? normalizedScope = string.IsNullOrWhiteSpace(scope) ? null : scope;
```

**Rationale:** Mirrors the `JoinGroupCoreAsync` guards (lines 85–91) so join and leave apply the same safe group rules. The colon check reuses the exact `HubException` message used on the join path for wire consistency.

### 4.2 Tests — `tests/Hexalith.EventStore.Server.Tests/SignalR/ProjectionChangedHubTests.cs`

Added five test cases proving the guards fire **before** `RemoveFromGroupAsync`:

| Test | Asserts |
|---|---|
| `LeaveGroup_ProjectionTypeContainsColon_ThrowsHubExceptionBeforeRemovingGroup` | `HubException` "must not contain colons"; no `RemoveFromGroupAsync` |
| `LeaveGroup_TenantIdContainsColon_ThrowsHubExceptionBeforeRemovingGroup` | `HubException` "must not contain colons"; no `RemoveFromGroupAsync` |
| `LeaveGroup_NullOrWhitespaceProjectionType_ThrowsBeforeRemovingGroup` (`[Theory]` null/""/"   ") | `ArgumentException`; no `RemoveFromGroupAsync` |
| `LeaveGroup_NullOrWhitespaceTenantId_ThrowsBeforeRemovingGroup` (`[Theory]` null/""/"   ") | `ArgumentException`; no `RemoveFromGroupAsync` |
| `LeaveGroupScoped_ProjectionTypeContainsColon_ThrowsHubExceptionBeforeRemovingGroup` | `HubException` "must not contain colons"; no `RemoveFromGroupAsync` |

### 4.3 Tracking artifacts

- **`_bmad-output/implementation-artifacts/deferred-work.md`** — Story 2.5 leave-validation entry marked `status: **RESOLVED 2026-07-07** …`.
- **`_bmad-output/implementation-artifacts/sprint-status.yaml`** — Epic 2 Action #6 set `status: done` with implementation note.

---

## Section 5 — Implementation Handoff

- **Scope:** Minor — implemented directly by the Developer agent in this session.
- **Validation performed:**
  - `dotnet test tests/Hexalith.EventStore.Server.Tests` filtered to `ProjectionChangedHubTests` — **34/34 passed** (Debug).
  - `dotnet build src/Hexalith.EventStore --configuration Release` — **0 warnings, 0 errors** (confirms `TreatWarningsAsErrors`/CI packaging path).
- **Completion gate (Epic 2 retro Action #6):** *"raw leave calls validate projection type and tenant id using the same safe group rules as join calls, or the gap is assigned to a named backlog item."* → **Met via the code path** (same guards as join).
- **Success criteria:** join/leave input-validation symmetry; no malformed group name can reach `RemoveFromGroupAsync` or the debug log; backward compatibility preserved; suites green.
- **Follow-up:** None. Not committed by this workflow — commit/push per your normal cadence (note: a concurrent auto-dev loop may touch `main`; verify refs before committing).
