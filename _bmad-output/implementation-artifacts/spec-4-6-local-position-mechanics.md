---
title: 'Story 4.6 Follow-up: Local Shard Position Mechanics'
type: 'chore'
created: '2026-10-31'
status: 'done'
route: 'oneshot'
review_loop_iteration: 1
parent_story_key: '4-6-global-position-sharding-spec-renegotiation'
completion_scope: 'development-candidate-only'
production_evidence_qualified: false
context:
  - '{project-root}/_bmad-output/implementation-artifacts/epic-4-context.md'
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** The approved sharding specification has no executable local candidate, and the owner does not have a representative production trace or production profile. General downstream authorization was supplied in this conversation; the missing empirical inputs must not prevent independent development checks or be replaced with invented production results.

**Approach:** Prepare a small test-only reference candidate for decoded logical positions and reservation mechanics in `tests/Hexalith.EventStore.Server.Tests/GlobalPositions/`. Exercise exact tenant/domain identity, injective local state keys, comparison precedence, explicit admission, persisted reservation ceilings, restart continuity, reservation gaps, corrupt state, and overflow using the existing in-memory actor state manager. These are deterministic development checks, not provider qualification.

**Boundaries:** Preserve every production source and contract, the approved predecessor/successor/wrapper, and the sprint tracker. Do not implement metadata wire formats, cursors, migration, distributed admission, physical fencing, or production allocator replacement in this bounded phase. Candidate types remain internal to the test assembly and are not registered with production DI. Do not claim thread/process concurrency, provider atomicity, production capacity, or any completed section 7 evidence row from in-memory results. Preserve concurrent user changes; do not stage, commit, push, or publish. The owner's production-data answer was “i dont know”; record it as an unresolved production evidence input, not a waiver or an invented profile.

</frozen-after-approval>

## Implementation Notes

- Reuse `Hexalith.EventStore.Testing.Fakes.InMemoryStateManager` for staged versus committed state and restart checks. A candidate instance assumes serialized calls; its shared dictionary key layout is a development fixture, not the selected production actor layout.
- The local shard key uses separate Base64 encodings of exact, strictly valid UTF-8 tenant and domain bytes. Neither alphabet contains the separator, so concatenation cannot conflate pairs. No trimming, case folding, normalization, or hashing occurs. Provider key limits remain unqualified.
- The candidate compares already-decoded descriptors. JSON structural validation, opaque unsupported wire round trips, and public/persisted metadata integration remain outside this phase; comparison tests must not be presented as those proofs.
- Before code changes, `aspire start --apphost src/Hexalith.EventStore.AppHost/Hexalith.EventStore.AppHost.csproj --isolated --non-interactive --format Json` succeeded. A filtered `aspire describe` showed healthy EventStore, Admin, sample, and security resources; `tenants` and `tenants-api` exited 1. These existing host failures are separate from deterministic candidate tests. The owned AppHost was stopped successfully with the exact-target Aspire stop command before building the test assembly.
- Added seven focused C# files: `QualificationShard`, `QualificationPosition`, `QualificationComparison`, `QualificationPositionComparer`, `QualificationAllocationState`, `QualificationAllocator`, and `GlobalPositionQualificationTests`. All are confined to the existing test project; candidate types are internal and require explicitly serialized calls.
- The synthetic development workload covers 32 exact tenant/domain pairs, 20 variable-sized reservation batches per pair, and four fixture-instance restarts. Every returned position and final committed ceiling is checked. There is no throughput or distributed-concurrency claim.

## Verification

Source baseline: `211fe67676a89c969eb8821798b52c7be124dc6b`, plus the seven uncommitted test-candidate files in this change. The approved semantic subject remains commit `411005b4ccb2352249c83330c54967c6a22beba1`, blob `160331d25451928ff3c3dea2300b65cab4f97c3b`, normative SHA-256 `995fcecd16b3421ec9ff666d0884bfb5e436932aa49529c152fb7c439172a1fd`.

- `dotnet build tests/Hexalith.EventStore.Server.Tests/Hexalith.EventStore.Server.Tests.csproj --configuration Debug -p:UseHexalithProjectReferences=true -m:1 --nologo --verbosity minimal` — exit 0, zero warnings, zero errors.
- `dotnet tests/Hexalith.EventStore.Server.Tests/bin/Debug/net10.0/Hexalith.EventStore.Server.Tests.dll -class '*GlobalPosition*'` — exit 0, **34 passed, 0 failed, 0 skipped**: 31 local candidate cases plus the 3 existing v1 allocator cases.
- `sha256sum tests/Hexalith.EventStore.Server.Tests/bin/Debug/net10.0/Hexalith.EventStore.Server.Tests.dll` — tested assembly SHA-256 `d6e1c30e9698760857b11f35f6324ed6cd348926b3a142ce572272a789af1a65`. This identifies the local tested binary, not a production qualification artifact.
- `aspire stop --apphost src/Hexalith.EventStore.AppHost/Hexalith.EventStore.AppHost.csproj --non-interactive` — exit 0. Subsequent exact-target `aspire describe --non-interactive --format Json` reported no running AppHost.
- `git diff --check -- tests/Hexalith.EventStore.Server.Tests/GlobalPositions _bmad-output/implementation-artifacts/epic-4-context.md` — exit 0; explicit file formatting checks also cover untracked candidate files.

The original predecessor, successor, wrapper, and sprint tracker remain byte-identical to `HEAD`. Concurrent user source/test/submodule changes are preserved. No dependency or project configuration was changed. No staging, commit, push, external publication, or production mutation was performed.

No section 7 empirical production evidence row is marked passed. Wire-format validation, opaque unsupported round trips, actor/provider integration, multi-process concurrency, migration/cursor mechanisms, production-profile ratification, and representative production capacity remain future work. Those are excluded from this bounded development phase rather than deferred defects introduced by it.

## Review Triage Log

- `medium`, patched — the decoded model lacked a scheme tag and could not exercise unknown-scheme precedence. Added the decoded tag and tested unsupported versus zero and invalid operands in both directions.
- `medium`, patched — equal counters alone could hide scalar sorting before partition checks. Added lower/higher cross-shard and cross-generation counter cases in both operand directions.
- `low`, patched — nonpositive guards lacked negative-input coverage. Added `-1` and `int.MinValue`, with committed state checked after flushing pending state.
- `low`, patched — the successful Int64 boundary adjacent to overflow was untested. Added the final valid counter and a subsequent rejected reservation, asserting the complete position and persisted ceiling.
- `medium`, patched — rejection assertions could overlook staged mutations. Flush pending state after rejection before verifying admission, routing, corrupt-state, and invalid-input outcomes.
- `medium`, patched — pair-isolation and restart checks could miss incorrect returned identities. Assert full returned descriptors, including exact shard and generation, and exercise the synthetic 32-shard workload.

All six findings were addressed; none were deferred. Only this local development phase is complete. Parent Story 4.6 retains its independent operator/evidence state.
