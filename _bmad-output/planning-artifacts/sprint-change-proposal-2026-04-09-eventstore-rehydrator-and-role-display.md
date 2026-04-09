# Sprint Change Proposal — EventStore: State Rehydrator Tolerance & Role Display Fix

**Date:** 2026-04-09
**Triggered by:** Story 16-5 (Tenant Management — Admin Web UI)
**Scope Classification:** Minor
**Status:** Approved & Implemented
**Related:** sprint-change-proposal-2026-04-09-tenant-index-deserialization.md (umbrella)

---

## Section 1: Issue Summary

Two bugs in the Hexalith.EventStore repository prevent tenant management operations from working:

### Bug 1: DomainProcessorStateRehydrator crashes on unknown event types

When the EventStore sends all events (including rejection events) to a domain service's `/process` endpoint, the `DomainProcessorStateRehydrator.ResolveApplyMethod()` throws `InvalidOperationException` for event types that don't have a matching `Apply` method on the target state class. This causes a 500 Internal Server Error on the domain service, killing all commands after the first rejection.

**Affected scenarios:**
- Any command after a rejection event was persisted (e.g., `UserAlreadyInTenantRejection`)
- Any domain service with multiple aggregates (events from one aggregate crash the state rehydration of another)

### Bug 2: DaprTenantQueryService crashes parsing integer enum roles

`GetTenantUsersAsync()` calls `JsonElement.GetString()` on `TenantRole` values serialized as integers (0/1/2). `GetString()` throws on `JsonValueKind.Number`, the exception is caught, and an empty user list is returned — hiding all users from the UI.

---

## Section 2: Impact Analysis

| Area | Impact |
|------|--------|
| Epic 16, Story 16-5 | Directly blocked — tenant disable, enable, and user management fail |
| EventStore.Client (shared library) | `DomainProcessorStateRehydrator` is used by ALL domain services, not just Tenants |
| Other epics | No impact — fix is additive (skip instead of throw) |

---

## Section 3: Changes Applied

### Change 1: DomainProcessorStateRehydrator — skip unknown events

**File:** `src/Hexalith.EventStore.Client/Handlers/DomainProcessorStateRehydrator.cs`

**What changed:**
- Renamed `ResolveApplyMethod` → `TryResolveApplyMethod`, returns `MethodInfo?` (nullable) instead of throwing
- Updated 3 callers (`ApplyContractEventEnvelope`, `ApplyJsonEventByName`, `ReplayEventsFromEnumerable`) to check for null and skip the event
- Events without matching Apply methods (rejection events, cross-aggregate events) are silently skipped during state rehydration

**Rationale:** Event streams in this EventStore persist ALL outcomes — successful events AND rejection events. Rejection event types (e.g., `UserAlreadyInTenantRejection`, `TenantAlreadyExistsRejection`) are domain-specific and don't modify aggregate state. The rehydrator must tolerate them. Additionally, when a domain service registers multiple aggregates, the processor iteration sends the same event stream to each aggregate's processor — events from one aggregate type naturally won't have Apply methods on another's state class.

**Risk:** Low. Skipping unknown events during state rehydration is the correct semantic — only events with matching Apply methods should modify state. Throwing on unknowns was overly strict.

### Change 2: DaprTenantQueryService — handle integer enum roles

**File:** `src/Hexalith.EventStore.Admin.Server/Services/DaprTenantQueryService.cs`

**What changed:**
- `GetTenantUsersAsync()` now checks `prop.Value.ValueKind` before extracting the role value
- For `JsonValueKind.Number`, maps the integer to the role name via `MapTenantRole(int)`
- Added `MapTenantRole` helper: `0 → TenantOwner, 1 → TenantContributor, 2 → TenantReader`

**Rationale:** `System.Text.Json` serializes enums as integers by default. The original code assumed string values.

---

## Section 4: Validation

### Post-deployment steps
- Redis FLUSHALL (clears corrupted projection data and stale rejection events)
- Create 2+ tenants
- Disable and re-enable a tenant
- Add users with all 3 roles (TenantOwner, TenantContributor, TenantReader)
- Verify users appear in detail panel with correct role labels
- Attempt duplicate user add — verify rejection toast (not 500 crash)

### Success Criteria
- [x] Disable/Enable operations succeed
- [x] Add User succeeds for all 3 roles
- [x] Users display in detail panel with role names
- [x] Subsequent commands work after a rejection event
- [ ] All Tier 1 tests pass
