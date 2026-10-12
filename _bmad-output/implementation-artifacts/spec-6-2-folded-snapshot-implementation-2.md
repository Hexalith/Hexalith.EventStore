---
title: 'Story 6.2: Folded Snapshot Actor Proofs'
type: 'feature'
created: '2026-10-12'
status: 'ready-for-dev'
route: 'dispatch'
review_loop_iteration: 0
context:
  - '{project-root}/_bmad-output/implementation-artifacts/spec-folded-snapshot.md'
  - '{project-root}/_bmad-output/implementation-artifacts/epic-6-context.md'
  - '{project-root}/_bmad-output/implementation-artifacts/spec-6-2-folded-snapshot-implementation.md'
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** Story 6.2's folded-snapshot runtime is on `main`, but Server actor tests stay green when a due check receives the wrong arguments or a retain path overwrites a protected key.

**Approach:** Keep approved HX-FS-V1 behavior. Make the actor decision proofs fail on those regressions. Change production code only when a new proof shows the current code violates the approved spec.

## Boundaries & Constraints

**Always:** Recompute the normative digest before edits and stop unless it is `a4ca9686628b284fb74da931e8cfb1466e80de45fd3d4e89a3c62358a4498ca5` with `MaxSnapshotEnvelopeOverheadBytes` 4096. Reuse the actor batch and `SnapshotEnvelopeInspector`. Every new assertion must be able to fail.

**Never:** Edit bytes inside the HX-FS-V1 normative markers. Add a second fold or replay path, projection work, upcasting, or Epic 8 protection. Delete protected, opaque, or ambiguous snapshots. Weaken advisory automatic skip or fail-closed manual retention. Touch P1R evidence or Projects pins. Do not add capture-limit, Admin, wire-delegation, byte-identity, `InvokeAsync` scan, compatibility-harness, or LiveSidecar work; those are deferred.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
| --- | --- | --- | --- |
| Qualified due check | Loaded snapshot sequence, aggregate type, and rejection events inside the interval | `ShouldCreateSnapshotAsync` receives those arguments; staging uses the 9-argument `CreateSnapshotAsync` | Unqualified or not-due commands do not stage |
| Manual retain | `UnreadableProtected`, `ProviderOpaque`, shape-colliding `Corrupt`, or equal sequence | Key retained; equal sequence is `AlreadyCurrent` | No overwrite and no `Created` |
| Pre-commit cancel | Caller cancels after staging and before commit | `SaveStateAsync` is not called | Cancellation still propagates |
| Redacted replacement | Retained key replaced at the committed head | `result.Message` and `snapshotLogger` carry the reason class and no exception object | Provider text and stacks stay out of the result |

</frozen-after-approval>

## Code Map

- `src/Hexalith.EventStore.Server/Actors/AggregateActor.cs` -- `ShouldCreateSnapshotAsync`, `CreateAutomaticReplacementAsync` allowlist, `CreateManualSnapshotAsync`. Do not redesign the decision block.
- `src/Hexalith.EventStore.Server/Events/SnapshotManager.cs` -- `LoadSnapshotAsync` never deletes; `LoadSnapshotForCommandAsync` is the command path. Drive retention tests through the command path with a verified head.

## Tasks & Acceptance

**Execution:**
- [ ] `_bmad-output/implementation-artifacts/spec-folded-snapshot.md` -- recompute the normative digest and stop on mismatch.
- [ ] `tests/Hexalith.EventStore.Server.Tests/Actors/AggregateActorDomainResultTests.cs` -- assert qualified due-check arguments, the 9-argument `CreateSnapshotAsync` for "does not stage", no `SaveStateAsync` on pre-commit cancellation, the replacement allowlist including `SnapshotUnprotectUnavailable` and an unlisted rejection, the `SnapshotPolicyUnavailable` log class, and remove the Story 3.9 / H2 / M2 comments.
- [ ] `tests/Hexalith.EventStore.Server.Tests/Actors/AggregateActorInfrastructureFailureTests.cs` -- restore the concrete `(tenant, domain, 2, 0)` due-check stub.
- [ ] `tests/Hexalith.EventStore.Server.Tests/Events/FoldedSnapshotReadTests.cs` -- drive protected and ambiguous retention through `LoadSnapshotForCommandAsync` with a verified head; add negative, fractional, string, and duplicate `snapshotEnvelopeVersion` cases.
- [ ] `tests/Hexalith.EventStore.Server.Tests/Actors/AggregateActorManualSnapshotTests.cs` -- cover `UnreadableProtected`, `ProviderOpaque`, shape-colliding `Corrupt`, and equality-only `AlreadyCurrent`; drop the unreachable setup in `CreateManualSnapshotAsync_RenamedEnvelopeSerializerIsUnsupported`.
- [ ] `tests/Hexalith.EventStore.Server.Tests/Actors/AggregateActorFoldedSnapshotMigrationTests.cs` -- assert `result.Message`, `snapshotLogger`, and no exception object on snapshot logs; assert bounded `SnapshotStreamUnavailable` for the orphan snapshot.

**Acceptance Criteria:**
- Given the approved artifact, when the normative digest is recomputed, then it is `a4ca9686628b284fb74da931e8cfb1466e80de45fd3d4e89a3c62358a4498ca5` and no marker byte changed.
- Given a qualified due command, a manual retain case, or pre-commit cancellation, when the Server actor tests run, then each approved outcome is asserted on the overload, logger, and result the production code actually uses.
- Given a protected, ambiguous, or non-positive envelope, when the command read path runs, then the key is retained and the typed outcome matches the approved policy.

## Implementation Notes

## Spec Change Log

## Review Triage Log

## Verification

**Commands:**
- `python3 -c "from pathlib import Path; import hashlib; p=Path('_bmad-output/implementation-artifacts/spec-folded-snapshot.md').read_bytes(); b=b'<!-- HX-FS-V1-NORMATIVE-BEGIN -->\n'; e=b'<!-- HX-FS-V1-NORMATIVE-END -->\n'; assert p.count(b)==p.count(e)==1 and b'\r' not in p and not p.startswith(b'\xef\xbb\xbf'); s=p.index(b)+len(b); t=p.index(e,s); print(hashlib.sha256(p[s:t]).hexdigest())"` -- expected: `a4ca9686628b284fb74da931e8cfb1466e80de45fd3d4e89a3c62358a4498ca5`
- `dotnet test tests/Hexalith.EventStore.Server.Tests/Hexalith.EventStore.Server.Tests.csproj --configuration Release` -- expected: pass, warnings as errors
- `git diff --check` -- expected: no whitespace errors
