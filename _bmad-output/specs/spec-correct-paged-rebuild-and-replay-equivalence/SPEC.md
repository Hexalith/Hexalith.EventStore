---
id: SPEC-correct-paged-rebuild-and-replay-equivalence
companions:
  - rebuild-semantics.md
  - ../../project-context.md
  - ../../implementation-artifacts/1-10-coordinated-read-model-batch-writes.md
sources:
  - ../../implementation-artifacts/1-14-correct-paged-rebuild-and-replay-equivalence.md
---

> **Canonical contract.** This SPEC and the files in `companions:` are the complete, preservation-validated contract for what to build, test, and validate. Source documents listed in frontmatter are for traceability only.

# Correct Paged Rebuild and Replay Equivalence

## Why

Long-stream rebuilds currently pass one bounded page to a stateless full-replay handler and overwrite the complete live projection with page-only state. Operators need rebuild paging to remain a read optimization: incomplete work must stay non-live, and promoted output must equal canonical replay through the same position.

## Capabilities

- **CAP-1**
  - **intent:** Rebuild participants expose explicit full-replay or incremental semantics.
  - **success:** A full-replay handler receives the complete required prefix; an incremental handler receives prior staged state plus one contiguous page; a lone page is never represented as a complete stream.

- **CAP-2**
  - **intent:** Rebuild work remains operation-scoped and non-live until every required projection is durably complete.
  - **success:** Incomplete, failed, or canceled work never replaces the last complete live actor, detail, or index state.

- **CAP-3**
  - **intent:** Paged and position-bounded rebuilds remain equivalent to canonical aggregate replay.
  - **success:** Page boundaries duplicate, skip, and reorder no events, and promoted output equals canonical replay through the same boundary.

- **CAP-4**
  - **intent:** Operators can cancel, fail, and resume rebuilds without corrupting live state or overstating progress.
  - **success:** Retry resumes from a durable safe boundary, while page-read progress is never reported as projection completion.

- **CAP-5**
  - **intent:** Required projections promote as one logical operation while retaining distinguishable per-projection outcomes.
  - **success:** The operation cannot succeed while any required projection is incomplete, and rebuild checkpoints advance only after durable promotion.

- **CAP-6**
  - **intent:** Projection lifecycle truthfully exposes rebuild progress.
  - **success:** Authoritative `ProjectionBacked` evidence is `Rebuilding` in flight and becomes `Current` or `Stale` only after durable promotion, with no terminal stale `Rebuilding` state.

- **CAP-7**
  - **intent:** Replay equivalence is demonstrated through persisted production-path evidence with bounded temporary full-prefix cost.
  - **success:** A stream larger than two pages and the required edge corpus prove semantic equality of actor/detail/index outputs, persisted freshness versions, and rebuild checkpoints; safety-limit exhaustion fails without changing live state.

## Constraints

- The equivalence target is every live surface owned by the rebuild operation: projection-actor state, required detail/index read models, persisted freshness projection versions, and rebuild checkpoints. ETags are not version evidence.
- Under the current stateless, domain-keyed handler contract, page reads accumulate the complete required prefix to the bounded target before projection; output remains non-live until promotion.
- Promotion uses Story 1.10's same-store batch or marker-gated resumable protocol. It must not invent another marker, claim cross-store atomicity, or persist live output before best-effort checkpointing.
- Only `IProjectionRebuildCheckpointStore` advances after proven promotion. `IProjectionCheckpointTracker` delivery checkpoints remain unchanged for Story 1.13.
- The rebuild page size is configurable, validates greater than zero, and defaults to 256. Exact page boundaries remain correct.
- Full-prefix accumulation has a configured safety ceiling and structured fail-closed outcome. Stories 6.3/6.4 may optimize cost without weakening equivalence.
- Persisted `IReadModelFreshness` is the projection-version authority. Lifecycle is never inferred from ETag, HTTP outcome, payload fields, or SignalR.
- Aggregate sequence remains gapless per aggregate; `SequenceNumber` is never global ordering; bounded `toPosition` uses the canonical replay boundary; shared platform JSON options remain authoritative.
- Existing immediate/poller full-replay behavior and admin rebuild control flow remain compatible. Public capability is additive.
- Required evidence traverses the real orchestrator → `/project` → persistence path and asserts persisted state. Mock calls, HTTP status, and isolated replay tests are insufficient.

## Non-goals

- Implementing Story 1.12 named asynchronous multi-projection dispatch or Story 1.13 delivery deduplication/checkpoint advancement.
- Optimizing long-stream replay cost beyond the explicit temporary safety bound.
- Changing query-route provenance, rebuild erasure, released read-model/lifecycle/checkpoint ABI, AppHost topology, or domain-module code.

## Success signal

A production-path rebuild of a fixture larger than two configured pages promotes persisted state, freshness versions, and rebuild checkpoints that are semantically equal to canonical replay, while cancellation and injected failures leave the previous complete live model intact and retry converges.

## Assumptions

- Internal staging key names and private helper types are implementation details when they are operation-scoped, collision-safe, support-safe, and preserve released ABI.

## Open Questions

- Must Story 1.10 coordinated batching and Story 1.12 named multi-projection dispatch land before Story 1.14, or is an additive Story 1.14 compatibility adapter authorized to expose the required detail/index projection set without implementing those stories?
- What maximum complete-prefix event count and/or serialized-byte ceiling, default value, configurability scope, and structured failure reason code are approved for the temporary full-sequence strategy?
