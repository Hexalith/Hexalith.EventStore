---
id: SPEC-correct-paged-rebuild-and-replay-equivalence
companions:
  - rebuild-semantics.md
  - ../../planning-artifacts/architecture.md
  - ../../project-context.md
  - ../../implementation-artifacts/1-10-coordinated-read-model-batch-writes.md
  - ../../implementation-artifacts/1-19-correct-paged-rebuild-and-replay-equivalence.md
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
  - **intent:** Replay equivalence is demonstrated through persisted configured-path evidence with bounded temporary full-prefix cost and explicit environment authority.
  - **success:** A stream larger than two pages and the required edge corpus prove semantic equality of actor/detail/index outputs, persisted freshness versions, and rebuild checkpoints; safety-limit exhaustion fails without changing live state; production-authorizing evidence additionally requires owner ratification and complete retained proof for the AD-26 profile.

## Constraints

- The equivalence target is every live surface owned by the rebuild operation: projection-actor state, required detail/index read models, persisted freshness projection versions, and rebuild checkpoints. ETags are not version evidence.
- Under the current stateless, domain-keyed handler contract, page reads accumulate the complete required prefix to the bounded target before projection; output remains non-live until promotion.
- Promotion uses Story 1.15's same-store batch or marker-gated resumable protocol. It must not invent another marker, claim cross-store atomicity, or persist live output before best-effort checkpointing.
- Only `IProjectionRebuildCheckpointStore` advances after proven promotion. `IProjectionCheckpointTracker` delivery checkpoints remain unchanged for Story 1.18.
- The rebuild page size is configurable, validates greater than zero, and defaults to 256. Exact page boundaries remain correct.
- `ProjectionOptions.RebuildMaxPrefixEventCount` defaults to 10,000 and `ProjectionOptions.RebuildMaxPrefixBytes` defaults to 67,108,864 (64 MiB); both are configurable server-wide and validation rejects non-positive values.
- Exceeding either full-prefix safety bound fails closed with `rebuild_prefix_safety_limit_exceeded`, preserves every live surface, advances no completion checkpoint, and clears terminal `Rebuilding` lifecycle state. Stories 6.3/6.4 may optimize cost without weakening equivalence.
- Story 1.15 coordinated batching and Story 1.17 named dispatch are completed prerequisites; Story 1.19 consumes those seams and must not create a local substitute.
- Persisted `IReadModelFreshness` is the projection-version authority. Lifecycle is never inferred from ETag, HTTP outcome, payload fields, or SignalR.
- Aggregate sequence remains gapless per aggregate; `SequenceNumber` is never global ordering; bounded `toPosition` uses the canonical replay boundary; shared platform JSON options remain authoritative.
- Existing immediate/poller full-replay behavior and admin rebuild control flow remain compatible. Public capability is additive.
- The governing draft architecture at SHA-256 `7e3dbc7bd335034bd9b98cadfed8b14650b7d811321b326b8f32e1b280960d51` governs this SPEC; its decision memlog is `7fc7acfbc3f5ae838a9922eab9553f15a95a4f3b788ef044f82c1e09119a98df`. AD-20 owns replay equivalence, AD-26 remains an assumption, and later wording within AD-1 through AD-33 supersedes earlier implementation guidance without changing this SPEC or its capability IDs.
- `ProjectionDispatchResponse` v2 with `ProjectionDispatchOutcome` is the cross-service carrier. The server-owned persisted route/checkpoint matrix is authoritative; this SPEC does not introduce a separate public `ProjectionDispatchResult` family.
- Every boundary canonicalizes exactly one explicit tenant before route resolution, lifecycle fencing, staging, checkpoint, read-model, or state access. Missing, duplicate, conflicting, invalid, inferred, or public `system` scope fails closed.
- Non-Development `/project` and `/project/v2` calls validate `dapr-api-token` against startup `APP_API_TOKEN` through shared constant-time middleware. Caller app ID, mTLS, and ACL success do not substitute for app-channel authentication.
- The sidecar-routed rebuild client explicitly chains `.AddEventStoreDaprServiceInvocation(appId, apiToken)` last so the platform handler is innermost and replaces `dapr-app-id` and `dapr-api-token`; current fail-open omission blocks non-Development readiness.
- Admin start, cancel, resume, and promotion actions preserve authenticated operator identity or a bounded validated delegation through one resumable mutation/audit unit. Audit failure cannot silently permit the action.
- The rebuild operation and its candidates bind stable projection route-entry IDs plus one activated AD-33 root/facet catalog generation. Non-Development runtime overrides, partial generations, and fingerprint drift fail readiness.
- The first public boundary accepts or mints the bounded `X-Correlation-ID`; every rebuild and projection hop propagates it unchanged or rejects an invalid replacement. Correlation is neither a GUID nor rebuild/status identity.
- Story 1.14 and AD-7 own projection read-model/checkpoint removal. Rebuild behavior neither authorizes nor proves the separate AD-30 full-erasure workflow.
- Required correctness evidence traverses the real orchestrator → `/project` → persistence path and asserts persisted state. Mock calls, HTTP status, and isolated replay tests are insufficient. Existing DAPR/Redis evidence proves Development/test replay semantics only; a production claim also requires owner ratification and complete retained proof for the proposed AD-26 PostgreSQL v1, durable-broker, OpenBao, resiliency, and independent-sidecar profile.

## Non-goals

- Reimplementing Story 1.17 named asynchronous multi-projection dispatch or Story 1.18 delivery deduplication/checkpoint advancement.
- Optimizing long-stream replay cost beyond the explicit temporary safety bound.
- Changing query-route provenance, rebuild erasure, released read-model/lifecycle/checkpoint ABI, AppHost topology, or domain-module code.
- Authorizing an AD-26 production profile, AD-30 full erasure, AD-31 dead-letter service, deployment, traffic, or consumer migration.

## Success signal

A configured-path rebuild of a fixture larger than two pages promotes persisted state, freshness
versions, and rebuild checkpoints semantically equal to canonical replay, while cancellation and
injected failures leave the previous complete live model intact and retry converges. The same
result becomes production-authorizing only after owner ratification and reproduction through the
complete AD-26 profile with the activated AD-33 catalog and required tenant, app-channel,
attribution, and correlation gates.

## Assumptions

- Internal staging key names and private helper types are implementation details when they are operation-scoped, collision-safe, support-safe, and preserve released ABI.
