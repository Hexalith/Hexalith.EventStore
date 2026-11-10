---
title: 'Payload-protection capacity fixes (Story 8.7 gate 1)'
type: 'bugfix'
created: '2026-11-10'
status: 'done'
baseline_commit: '39ebd8b4f709a65213e0022275c29c110a82275e'
route: 'dispatch'
review_loop_iteration: 0
context: []
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** On every membership change, `PayloadKeyIndexes` rewrites the head and every page of both the per-aggregate scope index and the environment-wide KEK inventory. Every single-key use also reads the whole inventory. The effects are listed in deferred-work.md (D2, 8.5 review pass 3):
- reservations exceed the 2 MiB transaction profile at about 18k keys per KEK version;
- each rewrap re-verifies the whole inventory;
- invalidation re-reads it for each key.

Separately, `ErasureStateRegistry._observations` keeps one entry per aggregate for the life of the process (8.5 review pass 7). Both block Story 8.7 host wiring.

**Approach:** Keep every stored record shape and key byte-identical, and make three changes:
- Place index entries in a deterministic hash bucket computed from the entry identity and the head's `pageCount` (linear hashing).
- Let each page keep the generation of the head transaction that last wrote it.
- Add point views (the head plus the pages one operation needs).

Then a change writes the head plus the pages it touches, and a single-key check reads the head plus one page. Bound the erasure observations with least-recently-used (LRU) eviction. The durable `LifecycleV1` epoch/state check still catches regressions for evicted scopes.

## Boundaries & Constraints

**Always:**
- These rules stay as approved (base §10.2; effective authority `eb4565…` unchanged):
  - record shapes, member order and key grammar;
  - per-page sort order and uniqueness;
  - the 256-entry page cap;
  - conditional creation of new pages.
- Every index mutation still writes the head under its ETag, so comparing the head binds the whole index. Touched pages take the new head generation; untouched pages keep their own, which is never above the head's.
- Fail closed, using the existing typed failures:
  - a missing page, a misplaced or duplicate entry, a count or membership disagreement, or a page newer than an unchanged head → `ConsistencyMismatch`;
  - a full bucket page or an oversized plan → the existing typed admission limit;
  - more than three concurrent restarts → `ProviderUnavailable`.
- Whole-index reads remain only where whole-index evidence is the point:
  - final verification of scope-wide invalidation and deletion;
  - the restore admission count;
  - `VerifyInventoryAsync`, kept for rotation start and old-version retirement, which Story 8.7 or an operator drives.
- Evicting an observation never refuses a new aggregate.

**Never:**
- New record types, fields, keys, schema versions or authority/digest changes.
- A stored-data migration, or a reader for indexes written in the old layout. No host has enabled the engine, and 8.7 activation still refuses.
- Out of scope:
  - 8.7 host admission and Server wiring;
  - the Dapr Unavailable/Denied deferral;
  - 3.21;
  - the other 8.5 deferrals;
  - production qualification;
  - running live lanes;
  - member/work catalog paging;
  - the 4,096-read phase limit for scope-wide work on aggregates over about 4,000 keys.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|----------|--------------|---------------------------|----------------|
| Add, no growth | n+1 ≤ 64·P | Writes the head plus the entry's bucket page | Bucket page full → admission limit |
| Add with growth | P = 0, or n+1 > 64·P | Writes the head, the split page s, the new page P, and the target page if different | New page uses conditional create |
| Change or remove | Existing entry | Writes the head plus its bucket page | Entry absent → `ConsistencyMismatch` |
| Single-key check | Resolve, activate, rewrap, reconcile, quarantine, restore | Per index: reads the head, the bucket page, then the head again | Disagreement → `ConsistencyMismatch` |
| Concurrent change during a read | Page generation > head generation, or the head ETag moved | Restarts, up to 3 times | Then `ProviderUnavailable` |
| Misplaced entry | Entry on a page other than its bucket | Point or full read rejects it | `ConsistencyMismatch` |
| Observation limit reached | A new aggregate is observed | Admitted; the least-recently-observed scope is evicted | Never refused |
| Regression after eviction | Provider epoch < durable `LifecycleV1` epoch | Resolver and reservation refuse | `ConsistencyMismatch` |

## Decisions

- **D1 (owner, 2026-11-10): within the approved authority.** These three conventions are implementation rules under the unchanged authority `eb4565…`:
  - hash-bucket placement;
  - pages keeping the generation of the head that last wrote them;
  - per-key rewrap checking only its own membership, with `VerifyInventoryAsync` kept for rotation start and retirement.

  No amendment revision or digest change. Record the rules in this spec, in the `PayloadKeyIndexes` header comment and in the deferred-work resolution notes. A golden-vector test pins placement.
- **D2 (owner, 2026-11-10): one spec.** Keep the full spec despite its size (about 3,100 tokens). The index redesign and the observation limit ship together as 8.7 gate 1.

</frozen-after-approval>

## Code Map

- `src/Hexalith.EventStore.PayloadProtection/PayloadKeyIndexes.cs`
  - Full reads: `ReadScopeAsync` / `ReadKekAsync` at :12-100.
  - `VerifyMembership` at :103.
  - Builders at :114-177.
  - All of these get reworked.
- `ScopeKeyIndexSnapshot.cs`, `KekKeyIndexSnapshot.cs` — complete snapshots. Callers iterate `.Pages` as the full set; keep that meaning.
- Callers moving to point views:
  - `PayloadKeyResolver.cs:133-135`.
  - `KeyReservationCoordinator.cs:167-171`, `:192-197`, `:211-220`, `:337-343`.
  - `KeyLifecycleCoordinator.cs:162-178` (per key). At `:185-198`, the final check keeps the full scope read and batches inventory pages by address.
  - `PayloadProtectionReconciler.cs:187-189`, `:224-247`.
  - `PayloadKeyRotationCoordinator.cs:70`, `:89-105`, `:245-288`, `:307-325`, `:344-356`. Drop the per-rewrap `VerifyInventoryAsync` calls but keep the method.
  - `PayloadRestoreAdmission.cs:82-86`. The inventory moves to head plus bucket pages; the scope index stays a full read.
- `PayloadProtectionCatalogs.cs` — the existing precedent for "head + relevant page" plans. Unchanged.
- `PayloadProtectionStateCodec.cs:216-235` — already enforces per-page sort, uniqueness and the 256 cap. Reuse its entry identity strings as the hash input.
- `PayloadProtectionOperationalLimits.cs` — `RequireTransactionCapacity` and its constants. Add the observation bound here.
- `ErasureStateRegistry.cs:11`, `:80-91` — bound `_observations`.
- Tests that pin the old layout, all under `tests/Hexalith.EventStore.PayloadProtection.Tests/`:
  - `KeyIndexTests`: V057/V137 two-page test, generation-0 rewrite test, concurrent-commit test, wrong-generation test;
  - `LifecycleCapacityTests`: combined reservation, large KEK plan, sparse preflight;
  - `ReviewRestoreTests`: condition count;
  - `ReviewLifecycleCorrectionsTests`: 257-key test;
  - `ReviewScopeMetadataTests`, `PolicyLifecycleTests`: observations.
- `MemoryPayloadProtectionStateStore` has `Reading` and `Committing` hooks for read/write assertions.
- Live test sources that pin the layout: `PayloadProtectionOperationalLiveTests` (sequential placement of 257 keys) and `PayloadProtectionReviewLiveTests` (restore condition count).

## Tasks & Acceptance

**Execution:**
- [x] `src/Hexalith.EventStore.PayloadProtection/PayloadIndexPlacement.cs` (new)
  - **Hash:** `h` is the big-endian u64 of the first 8 bytes of SHA-256 over the codec identity string.
  - **Bucket:** with L = ⌊log₂ P⌋ and s = P − 2^L, `b = h mod 2^L`; if b < s, use `h mod 2^(L+1)` instead.
  - **Growth:** create page 0 when P = 0. Otherwise split bucket s into a new page P when n+1 > 64·P.
- [x] `src/Hexalith.EventStore.PayloadProtection/ScopeIndexView.cs`, `KekInventoryView.cs` (new) — the head plus the read pages, keyed by page number, with a factory from complete snapshots.
- [x] `src/Hexalith.EventStore.PayloadProtection/PayloadKeyIndexes.cs`
  - **Full reads:** accept page generation ≤ head generation, and check placement.
  - **Point reads:** a single scope entry; one or more inventory entries; and an insert mode that also reads the split page. Each confirms the head, restarts at most 3 times, and preflights read capacity.
  - **On views:** `VerifyMembership` and the builders, plus a `Conditions(view)` helper.
  - **Builders:** write only the head plus touched pages and keep `RequireTransactionCapacity`. A full bucket raises the admission limit.
- [x] Callers listed in the Code Map — use views. Replace "compare every page" with `Conditions(view)`.
- [x] `ErasureStateRegistry.cs`, `PayloadProtectionOperationalLimits.cs` — an LRU bound, `ErasureObservations = 65_536`, with an internal constructor override for tests.
- [x] `tests/Hexalith.EventStore.PayloadProtection.Tests/` — rewrite layout-pinned tests to expect placement. Add a test for each acceptance criterion, plus placement golden vectors.
- [x] `tests/Hexalith.EventStore.PayloadProtection.LiveSidecar.Tests/` — adapt layout-pinned assertions so they compile and match placement. These are not run.
- [x] Mutation-check each new guard in a scratch copy, never in the working tree.

**Acceptance Criteria:**
- **Touched pages only.** Given an index with at least 3 pages, when one entry is added without growth, changed or removed, then the plan is exactly the head plus that entry's bucket page. After the commit, every other page's bytes and ETag are unchanged.
- **Growth keeps every entry findable.** Given 5,000 sequential inserts across many growth steps, then every full read passes placement and count checks, and every entry is found by a point lookup.
- **Inventory reads stay per key.** Given a KEK inventory of 20,000 entries or more, when a key is reserved, resolved, activated, rewrapped, reconciled or invalidated, then the operation reads only the inventory head (twice) plus that key's bucket page, and the reservation plan passes `RequireTransactionCapacity`.
- **Eviction is safe.** Given observations at capacity, then a new aggregate is admitted and the least-recently-observed scope is evicted. Given a scope evicted at epoch 2 whose provider now reports epoch 1, then the resolver and the reservation fail with `ConsistencyMismatch`.
- **Golden placement.** Fixed identities and page counts map to the recorded buckets.

## Implementation Notes

- **Shared core.** `PayloadIndexLayout<THead, TPage, TEntry>` binds each closed schema's exact keys and fields, so the scope index and the KEK inventory share one read core (`ReadIndexAsync`) and one plan builder (`Plan`). Identity strings come from `PayloadProtectionStateCodec.ScopeEntryIdentity`/`KekEntryIdentity`, which the codec's per-page sort and uniqueness check now also uses.
- **Restarts.** Full and point reads both allow the initial attempt plus three restarts (full reads previously allowed two restarts); a fourth concurrent change returns `ProviderUnavailable`.
- **Head sanity.** A point read cannot count every page, so every read also rejects a head that declares more entries than `pageCount × 256`.
- **Views.** `ScopeIndexView`/`KekInventoryView` hold the confirmed head and the read pages by number. A complete snapshot converts implicitly (`From`), so existing callers and tests that pass full snapshots keep their meaning. A plan or membership check that needs a page its view did not read fails with `ConsistencyMismatch`.
- **Callers.** `PayloadKeyIndexes.ReadMembershipAsync(key)` reads and verifies one key's scope and inventory point views. Reservation reads both indexes in insert mode. Rewrap reads the new inventory in insert mode and no longer calls `VerifyInventoryAsync`; because that call used to bind the key to `backend.BackendId`, the completed-rewrap and commit paths now check the backend explicitly. Single-key lifecycle claims use point reads, including in the final check; scope-wide claims keep the full scope read, and their final check batches inventory bucket pages per address. Restore keeps the full scope read and compares each inventory's head plus the key's bucket page.
- **Known ceiling.** At 64 entries per page, a complete read reaches the 16 MiB phase preflight at about 255 pages (about 16,000 entries). Only whole-index evidence uses complete reads; `VerifyInventoryAsync` was already bounded earlier by its per-entry key and scope reads.
- **Mutation checks (scratch copy, 2026-11-10).** All 19 mutations were caught. Read guards: placement, page generation, full-read count and head capacity. Plan guards: touched pages only and touched-page generation. Point-read scope: every page read, and the split page omitted. Others: at most three restarts, the full-bucket limit, bucket-page-only membership, LRU eviction (removed, recency not refreshed, most recent evicted, a new aggregate refused), the durable resolver and reservation epoch checks, and both restored rewrap backend bindings. The scratch baseline failed only two `KeyRecordAuthorityTests`, which read repository documents that were not copied there and pass in the working tree.
- **Matrix audit (parent session).** The single-key row lists quarantine, but no test asserted the quarantine path's point reads. Added `IndexCapacityTests.QuarantineReadsOnlyItsBucketPage` (20,000-entry inventory, 24-hour orphan boundary). Scratch mutation M20, which reverts `QuarantineAsync` to whole scope and inventory reads, fails only that test. Unit suite: 1,187/1,187.
- **Verification note.** `Hexalith.EventStore.slnx` does not list `Hexalith.EventStore.PayloadProtection.Tests` or `Hexalith.EventStore.PayloadProtection.LiveSidecar.Tests`, so both were also built directly.
- **Review pass 1 patches (2026-11-10).** Applied five patches from the triage log (rows 1–5):
  - point reads preflight per chunk of at most 64 pages (`PointReadChunk`);
  - single-key final lifecycle checks use point reads;
  - new tests for a rewrap into a growing target inventory, and for the replay, recovery, barrier and Delete read patterns;
  - deferred-work `status: done` lines.
  Rows 6–8 were deferred to deferred-work.md. Nine further scratch mutations were caught (`mutation-results-2.txt`).
- **Final verification (2026-11-10).**
  - Debug builds of the PayloadProtection unit, live-sidecar and Azure Key Vault test projects: 0 warnings, 0 errors. Release `-warnaserror` builds with `UseHexalithProjectReferences=false` of the unit and live-sidecar test projects: 0 warnings, 0 errors.
  - Unit DLL 1,196/1,196 and Azure Key Vault DLL 192/192, run in place.
  - Live lanes were not run, per the spec; their sources compile.

## Spec Change Log

## Review Triage Log

Review pass 1 (2026-11-10): Blind Hunter, Edge Case Hunter and Verification Gap ran against `39ebd8b4f..` working tree (src, tests, deferred-work). No intent_gap or bad_spec entry, so no loopback.

| # | Source | Finding | Verdict | Evidence | Route |
|---|--------|---------|---------|----------|-------|
| 1 | EC1, BH1 | Scope-wide final check batches every key's inventory bucket pages into one point read whose preflight charges 64 KiB per page | high | `ReadIndexAsync` calls `RequireReadCapacity((planned+1)·65,536)` against the pass's cumulative 16 MiB budget. Roughly 250 distinct pages, or fewer once the per-key loop's reads are counted, fail every pass. With 20,000 inventory entries that is a few hundred keys in the aggregate; the old full read of 79 pages fit. | patch |
| 2 | EC2 | Single-key final check still reads the full scope index | medium | `KeyLifecycleCoordinator.cs:185` uses `ReadScopeAsync` for every claim. At 64 entries per page the preflight fails at about 255 pages (~16k keys; previously ~65k), so single-key invalidate/delete on such aggregates never completes. The frozen Always rule limits whole-index reads to scope-wide work. | patch |
| 3 | VG1 | No rewrap into a target inventory at a growth step with P ≥ 2 | medium | Pre-verified gap: every rewrap test uses an empty target, so dropping `inserting: true` at `PayloadKeyRotationCoordinator.cs:305` would pass. | patch |
| 4 | VG2, BH12 | No read-pattern test for rewrap recovery/replay, barrier reconcile, reservation replays or the Delete lifecycle path | medium | Pre-verified gap: reverting `:72`, `:245`, `:339`, `KeyReservationCoordinator.cs:168/189` or the Delete branch to whole-index reads would pass on one-entry inventories. | patch |
| 5 | BH10 | Resolved deferred-work entries lack `status: done` | low | Resolved entries elsewhere pair `resolution:` with `status: done <date>` (e.g. `deferred-work.md:5342-5348`). | patch |
| 6 | BH2, BH5 | `VerifyInventoryAsync` has no production caller, the 8.7 obligation is unrecorded, and an emptied inventory whose peak passed ~255 pages fails the full-read preflight | medium | No src caller remains. Empty inventories have no per-entry reads, so the whole-index preflight (owner-accepted 8.5 P4-B10 bound) now bounds retirement at a ~16k-entry peak (was ~65k). Whole-inventory verification never scaled (non-empty: ~1,300–2,000 entries) and is recorded as open. | defer |
| 7 | BH3 | The environment-wide KEK head is a contention point; read-only point proofs restart when it moves | medium | Pre-existing: every reservation always wrote the head, and the old full read had a far wider restart window. Dropping the confirming head read would change the frozen matrix row. | defer |
| 8 | EC5, BH6 | Restore re-reads each inventory per key and adds per-key compares | medium | Pre-existing: the old restore re-read and compared every page per key (worse). A head move between keys gives `OperatorDecisionRequired`; per-key key/page compares cap restore near 250 keys under 512 ops / 2 MiB. | defer |
| 9 | EC3 | Indexes written in the old sequential layout no longer read | low | Real but excluded by the frozen Never rule: no migration or legacy reader; no host has enabled the engine. | reject |
| 10 | EC4 | A stray page at address P blocks growth forever | low | Pre-existing: the old builder also conditionally created new pages with a null ETag. The state needs an out-of-band head rollback, and growth fails closed. | reject |
| 11 | BH4 | Accepting older page generations drops detection of a page rolled back out of band | low | True and inherent to owner decision D1 rule 2; the fix would edit this spec. A consistent rollback of head, page and key went undetected before too. | reject |
| 12 | BH7 | Commit-time refusal of the reservation transaction lost coverage | false | Planning-time and commit-time refusals both surface from the same `CommitAsync([...])` statement into the same failure handler. The replacement test asserts the full end state: Failed/provider-unavailable, claim Pending, no key, both indexes unchanged. | reject |
| 13 | BH8 | Evictions and full-bucket refusals emit no diagnostics | low | Diagnostics were deferred to Story 8.7/8.11 by owner decision D5 (8.5 pass 3). | reject |
| 14 | BH9 | Implicit snapshot-to-view conversions hide full-read call sites | low | Developer-only risk; removing them touches many test call sites. Complete snapshots are built from unique page keys, so the duplicate throw is unreachable from production reads. | reject |
| 15 | BH11 | The eviction test does not pin the reservation's reason | low | Scratch mutation M17 (durable reservation epoch check removed) made this test fail, so it discriminates. The core's exception type carries no reason. | reject |
| 16 | BH13 | Per-call layout closures and per-entry hashing on the resolver path | low | Microseconds against millisecond strong reads; caching adds complexity. | reject |

## Design Notes

**Average load of 64 per page.** Under linear hashing, an unsplit bucket's mean load stays below 128, half the 256 cap. Overflow therefore has negligible probability (about 1e-22 under Poisson). Hash input includes the engine-generated keyRef ULID, so no caller can steer entries into a chosen bucket.

**Why page writes stay local.** Moving from P to P+1 pages remaps only bucket s, so no other page is rewritten.

**Why comparing the head is enough.** Every transaction that changes the index also writes the head under its ETag. A page newer than the head being read therefore means a concurrent commit: restart, don't reject.

Example with P = 3 (L = 1, s = 1): an h ending in binary …10 maps to bucket 0 (h mod 2), which is below s, so it moves to bucket 2 (h mod 4). An h ending in …01 stays in bucket 1.

## Verification

**Commands:**
- `dotnet build tests/<project>/<project>.csproj -c Debug -m:1` for `Hexalith.EventStore.PayloadProtection.Tests`, `…LiveSidecar.Tests` and `…AzureKeyVault.Tests` — expected: 0 warnings, 0 errors. `Hexalith.EventStore.slnx` does not list the first two, so building the solution does not compile them.
- `dotnet tests/Hexalith.EventStore.PayloadProtection.Tests/bin/Debug/net10.0/Hexalith.EventStore.PayloadProtection.Tests.dll` — expected: all pass.
- `dotnet tests/Hexalith.EventStore.PayloadProtection.AzureKeyVault.Tests/bin/Debug/net10.0/Hexalith.EventStore.PayloadProtection.AzureKeyVault.Tests.dll` — expected: all pass.
- Scratch-copy mutation of each new guard (placement check, generation check, touched-pages-only, point-read scope, LRU eviction, durable regression) — expected: each mutation is caught.
