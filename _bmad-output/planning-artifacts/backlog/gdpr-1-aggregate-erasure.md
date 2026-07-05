---
id: GDPR-1
title: Aggregate Erasure And Tombstoning
classification: backlog
status: draft
source_story: 7.5
created: 2026-07-05
---

# GDPR-1 - Aggregate Erasure And Tombstoning

## Scope

Define a future capability for aggregate-level erasure or tombstoning across EventStore state, snapshots, read models, brokered events, backups, and operational evidence. The design must cover crypto-shred boundaries, tombstone semantics, retention obligations, replay behavior, auditability, and verification evidence.

## Non-Goals

- Do not implement erasure or tombstoning in Phase 4 MVP.
- Do not rewrite historical event streams as a cleanup mechanism.
- Do not hide this work inside security, backup, compaction, or admin UI stories.

## Dependencies

- Approved event identity, snapshot, and projection semantics.
- Secret-store and key-management posture from deployment hardening work.
- Legal/product decision on retention, crypto-shred, and audit requirements.

## Risks

- Event-sourcing immutability can conflict with deletion expectations if the product contract is not explicit.
- Snapshots, read models, backups, logs, and dead-letter records can preserve personal data after primary stream tombstoning.
- Cross-tenant erasure mistakes are tenant-isolation incidents.

## Validation Expectations

- Tests must prove tenant-scoped erasure/tombstone behavior without exposing hidden aggregate existence across tenants.
- Higher-tier tests must inspect persisted state-store, snapshot, read-model, and backup/end-state evidence.
- Documentation must state what is erased, tombstoned, retained, or crypto-shredded.
