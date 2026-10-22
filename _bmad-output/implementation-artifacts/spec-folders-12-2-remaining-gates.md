---
title: 'Close EventStore qualification gates for Folders 12.2'
type: 'bugfix'
created: '2026-10-22'
status: 'in-progress'
route: 'oneshot'
baseline_commit: '995117f9b899a2994e99257091b0ea35cfbe5090'
review_loop_iteration: 0
context: []
---

<frozen-after-approval reason="explicit user authorization to investigate, fix and qualify while preserving existing changes">

## Intent

Investigate all 21 unchanged Server secret-material guard findings, distinguish usable secrets from verified false positives, and fix SDK-owned defects without weakening detection, broad exemptions or historical edits. If immutable evidence prevents closure, document the exact blocker. Repair bounded-worker observation tooling to capture actual Server mappings for every short-lived worker while retaining strict phase/process/module coverage and every previous failure. Execute fresh real-Dapr bounded acceptance and affected regressions with Debug/project references. Bind revisions, sources, binaries, runtime identities, commands, results and cleanup in a new sealed append-only handoff. Distinguish fresh execution from reused evidence. Preserve all existing changes and Folders source/selected outputs. Keep assertions, deadlines, retries, admission rules and storage limits unchanged. No publication, deployment, pins, staging, commits or pushes. Do not claim Folders adoption or whole-story completion.

</frozen-after-approval>

## Implementation Notes

- Single producer qualification goal. No human-only intent gaps, irreversible changes or public API additions. The user's explicit preservation instructions authorize working beside the existing dirty tree; no Git recording will occur.
- Prior handoff SHA256SUMS verifies: 6101 files, seal 0b4d5ade382ea889b78fdb5cba7783c7f0fbaa1bdc68d06646b4fc4ade732135. Current ordinary/PDB source matches; 1012 local binary/manifest inputs are absent and require fresh builds, not binary-equivalence claims.
- New packet: evidence/folders-12-2-gates-20261022T151209Z. Protect all prior evidence, current edits, Folders source and selected outputs; retain canonical revisions and index identities before mutations.
- Reuse current bounded production assertions and audit-runtime.py complete requirements (9 scenarios, 30 workers, 4 modules each). Repair sampling with deterministic externally verified capture at worker lifetime boundaries; fail closed on missing observations and retain errors. Do not alter production semantics or acceptance timeouts.
- Investigate secret guard lexical triggers and positive/tamper controls before changing SDK fixtures or parser. Historical findings remain failures if exact evidence changes would be required.
- Fresh solution and affected project builds in Debug with UseHexalithProjectReferences=true and UseNuGetDeps=false; direct individual xUnit executions. Bind fresh portable PDBs and mapped images to their actual source graph. Reuse unrelated regressions only when source identity supports a specifically limited historical claim.
- Run independent blind review under bmad-build before sealing; keep qualification negative if any requested gate remains failed. Existing Folders whole-story planning is context, not the story being implemented here; do not update its sprint status.
