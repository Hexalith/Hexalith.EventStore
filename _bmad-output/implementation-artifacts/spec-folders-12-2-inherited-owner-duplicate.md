---
title: 'Folders 12.2 Task 36: inherited owner duplicate verification'
type: 'bugfix'
created: '2026-10-22'
status: 'done'
route: 'oneshot'
review_loop_iteration: 0
context: []
---

<frozen-after-approval reason="User requested this bounded EventStore correction and qualification">

## Intent

Fix ordinary replay of an unchanged original event after another owner's rebuild
inherits its generation while omitting the original owner from the new capture.
Use the existing retained owner checkpoint and exact captured identity evidence;
an unchanged replay must remain AlreadyCaptured and named dispatch must return
AlreadyCompleted. Establish a failing regression from the retained tiny Folders
reproduction, then qualify focused regressions and real Dapr process recovery.

Preserve altered-identity refusal, missing/corrupt proof refusal, original source
continuity, CAS/epoch fences, and 100-operation/one-MiB limits. Record exact source
and runtime binary identities, commands, results, cleanup, and a Folders consumer
requalification handoff. Keep internal actor HTTP429/admission separate. Do not
modify Folders, dependency pins or historical evidence; do not publish, commit,
push or stage changes.

</frozen-after-approval>

## Implementation Notes

- Route decision: no unresolved intent gaps, no irreversible effects, one small
  internal admission correction with focused Client/DomainService/live regression
  extensions; no public API or persistence-format changes.
- Investigation: `SharedProjectionEpochCoordinator.JournalAsync` gates exact
  captured proof on the current capture map. `ReadOwnerCheckpointAsync` already
  preserves and validates the inherited owner binding; `CapturedIdentities.ReadDigestAsync`
  authenticates bounded tree paths and legacy inline prefixes. Reuse those seams.
- Additive evidence directory:
  `_bmad-output/implementation-artifacts/evidence/folders-12-2-inherited-owner-20261022`.
- Reuse `SharedProjectionBoundedIdentityTests`, partitioned dispatcher test helpers,
  and `ProjectionBoundedIdentityProcessWorkerTests` with real Dapr/Redis. Extend
  coverage for omitted owners, multiple inheritance, growth, altered identities,
  unavailable/corrupt proof, stale leases and epoch changes during proof reads.
- Startup baseline: `aspire start --apphost src/Hexalith.EventStore.AppHost/Hexalith.EventStore.AppHost.csproj --non-interactive`
  succeeded (AppHost PID 40941); inspect and stop before focused builds.
- Implemented a retained-proof admission branch for omitted copy-on-write owners.
  Paged proof is bounded by the inherited anchor and selected generation; legacy
  inline proof requires anchor zero. Exact canonical identity is authenticated and
  the epoch ETag reread before acknowledgement. No storage/API changes.
- Final verification: 359 focused Client checks; 2,073 full Client passes with two
  existing endpoint skips; 1,164 DomainService passes; five real Dapr/process cases
  with zero skips. The 10,001-event omitted-owner case uses four distinct workers.
  Maximum observed batch: 68 operations and 109,638 canonical bytes.
- Final Client image SHA-256:
  `2BDF48488D2B4B707D4D3A5D7E016B95774F0A44648D2E0F92385D00F67F8D91`.
  Full source/binary/PDB identities, exact commands, attempts, results and cleanup
  are retained in the new evidence packet and its `handoff.md`.
- No Folders, dependency pin or historical evidence edits; no staging, commits,
  pushes or publication. The workflow commit instruction is overridden by the
  user's explicit prohibition. Task 36 consumer requalification and actor HTTP429
  remain separately open. An unrelated `.mcp.json` change was preserved.

## Review Triage Log

- High / patch: age-only proof admitted a discarded owner after COW migration;
  reproduced paged/inline red, added anchor guard and positive anchor coverage.
- Medium / defer: preexisting buffered StageAsync COW materialization lacks
  inheritance pointers; separate staging fix, recorded in deferred-work.md.
- False / reject: abort allegedly retained A capture; actual AbortAsync clears
  both forms, and omitted/included A abort variants pass. Reviewer withdrew it.
- Medium / defer: arbitrary deletion of an entire owner checkpoint lacks another
  authoritative source inventory; separate recovery design, recorded in ledger.
- Low / reject: empty-registry tests do not run actual upcasters; only registry
  presence is claimed. Retained Folders reproduction registers FolderCreated;
  broad versioned-dispatch tests pass. New upcaster work is outside this fix.
- Medium / patch: restore original replacement-mode process lane alongside the
  inherited lane by parameterizing the handlers and keeping three/four phases.
- Medium / patch: prevent evidence overwrite using tenant-specific directories;
  large inherited case now differs from the original replacement-mode theory.
- Final independent verification found no remaining introduced defects. Detailed
  finding evidence and dispositions are in the packet's `review.md`.
