# Sprint Change Proposal — Use Hexalith.Commons.UniqueIds for ULID Creation

**Date:** 2026-03-15
**Triggered by:** Jerome — organizational library reuse for ULID generation
**Author:** Jerome (facilitated by SM agent)
**Change scope:** Minor
**Mode:** Incremental (each proposal reviewed individually)

---

## Section 1: Issue Summary

Story 1.7 (MessageType Value Object & ULID Integration) plans a custom `UlidId` value object in the Contracts package with a direct ULID library dependency. However, `Hexalith.Commons.UniqueIds` (v2.13.0+, NuGet) — from the same Hexalith organization — already provides ULID generation (`UniqueIdHelper.GenerateSortableUniqueStringId()`), timestamp extraction, and Guid conversion.

Building a custom `UlidId` type duplicates functionality available in the organizational shared library and creates unnecessary maintenance divergence.

**Discovery context:** Story 1.7 is in backlog (not yet implemented), so this is a pre-implementation refinement with zero rework.

---

## Section 2: Impact Analysis

### Epic Impact

| Epic | Impact Level | Summary |
|------|-------------|---------|
| **Epic 1: Core Pipeline** | **Light** | Stories 1.1 and 1.7 ACs updated. No structural changes. |
| **Epics 2-18** | **None** | Reference "ULID" as format only, not implementation. |

### Artifact Conflicts

| Artifact | Sections Affected | Severity |
|----------|-------------------|----------|
| **Architecture** | D12 (ULID everywhere), Contracts file listing | Light |
| **Epics** | Story 1.1 ACs (UlidId references), Story 1.7 ACs + title | Moderate |
| **PRD** | UlidId type references (terminology sweep) | Light |
| **Infrastructure** | Directory.Packages.props, Contracts .csproj | Light |

### Technical Impact

- Contracts package gains first external dependency: `Hexalith.Commons.UniqueIds` (transitively brings `ByteAether.Ulid`)
- Equivalent dependency footprint to the originally planned custom `UlidId` approach (which would also need a ULID library)
- Alignment with Hexalith organizational package ecosystem

---

## Section 3: Recommended Approach

**Path:** Direct Adjustment (Option 1)

**Rationale:** Story 1.7 is in backlog — no implementation exists to rework. The change is purely an AC refinement. The epic structure, scope, and implementation sequence are unchanged.

- **Effort:** Low — AC text updates across 2 stories, 2 architecture sections, PRD sweep
- **Risk:** Low — no architectural rework, no scope change
- **Timeline impact:** None — may slightly accelerate Story 1.7 (less custom code to write)

---

## Section 4: Detailed Change Proposals

All 7 proposals were reviewed and approved incrementally.

### Architecture Changes

**Proposal 1: ADR D12 Update** — APPROVED
- D12 now specifies `Hexalith.Commons.UniqueIds.UniqueIdHelper.GenerateSortableUniqueStringId()` as the ULID generation mechanism
- Contracts does NOT take a direct dependency on a raw ULID library

**Proposal 2: Contracts File Listing** — APPROVED
- Removed `UlidId.cs` from planned Contracts file listing
- ULID generation provided by `Hexalith.Commons.UniqueIds` package dependency

### Epic Changes

**Proposal 3: Story 1.7 ACs** — APPROVED
- Replaced custom `UlidId.New()` / `UlidId.Parse()` with `UniqueIdHelper.GenerateSortableUniqueStringId()` / `UniqueIdHelper.ExtractTimestamp()`
- ULID fields are `string`-typed (no custom value object)
- Tests use `UniqueIdHelper` directly

**Proposal 4: Story 1.1 ACs** — APPROVED
- Replaced `UlidId` value object reference with string-typed ULID fields validated via `UniqueIdHelper`
- Test ACs updated to use `UniqueIdHelper.ExtractTimestamp()` for validation

**Proposal 5: Story 1.7 Title** — APPROVED
- Title: "MessageType Value Object & Hexalith.Commons ULID Integration"
- User story updated to reference `Hexalith.Commons.UniqueIds`

### Infrastructure Changes

**Proposal 6: Package Dependencies** — APPROVED
- `Directory.Packages.props`: Add `Hexalith.Commons.UniqueIds` v2.13.0
- `Hexalith.EventStore.Contracts.csproj`: Add PackageReference
- Actual file edits happen during Story 1.7 implementation

### PRD Changes

**Proposal 7: ULID Terminology Sweep** — APPROVED
- Replace `UlidId` type references with `UniqueIdHelper`-based approach
- ULID format decision (D12) unchanged

---

## Section 5: Implementation Handoff

### Change Scope Classification: **Minor**

Can be implemented directly by the development team as part of normal story execution. No backlog reorganization needed.

### Handoff Plan

| Role | Responsibility | Deliverables |
|------|---------------|-------------|
| **SM (this agent)** | Produced this Sprint Change Proposal | This document |
| **Dev** | Apply architecture proposals 1-2 to architecture.md | Updated architecture.md |
| **Dev** | Apply epic proposals 3-5 to epics.md | Updated epics.md |
| **Dev** | Apply PRD proposal 7 (UlidId sweep) to prd.md | Updated prd.md |
| **Dev** | Implement Story 1.7 using Hexalith.Commons.UniqueIds | Working code with tests |

### Success Criteria

- [ ] Architecture D12 references `Hexalith.Commons.UniqueIds` as ULID implementation
- [ ] Architecture file listing no longer includes `UlidId.cs`
- [ ] Story 1.7 ACs reference `UniqueIdHelper` methods, not custom `UlidId`
- [ ] Story 1.1 ACs reference string-typed ULID fields, not `UlidId` value object
- [ ] PRD contains no remaining `UlidId` type references
- [ ] No custom ULID library dependency in Contracts — only `Hexalith.Commons.UniqueIds`
- [ ] All documents internally consistent

---

**Generated:** 2026-03-15
**Approved:** 2026-03-15
**Status:** Approved — routed to Dev for artifact updates and Story 1.7 implementation
