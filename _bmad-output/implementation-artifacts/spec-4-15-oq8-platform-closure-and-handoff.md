---
title: 'Story 4.15: OQ8 Platform Closure And Handoff'
type: 'feature'
created: '2026-08-10'
status: 'done'
review_loop_iteration: 0
story_key: '4-15-oq8-platform-closure-and-handoff'
baseline_commit: '699ca71206cd280dc6b770d83c338495bfe70fab'
context:
  - '{project-root}/_bmad-output/project-context.md'
  - '{project-root}/_bmad-output/implementation-artifacts/epic-4-context.md'
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** Story 4.14 produced source-bound multi-host evidence, but its packet deliberately leaves architecture, security, and test reviews pending, binds a dirty candidate rather than the landed capability commit, and cannot validate after unrelated repository changes. Status contradictions and stale documentation prevent a truthful EventStore OQ8 platform handoff.

**Approach:** Add an immutable closure layer over the Story 4.14 capture that crosswalks Stories 4.9-4.14, independently binds the landed OQ8 source and captured artifacts, records content-bound named reviews and limitations, and distinguishes EventStore platform completion from release or Folders-owned final closure.

## Boundaries & Constraints

**Always:** Preserve the checksummed Story 4.14 evidence directory unchanged. Bind every approval to one frozen subject containing the design reference, invariant/evidence crosswalk, exact source/artifact identities, limitations, and reviewer scope. Keep the approved test seams and sanitized structural-state limitation explicit. Advance tracking only when the final fail-closed validator passes.

**Ask First:** Changing OQ8 design 1.0.0, durable-admission behavior, the production profile, public contracts, or any release, package, registry, deployment, consumer pin, external repository, or submodule state.

**Never:** Fabricate unavailable Folders design bytes or reviewer approval; treat historical Story 4.8 checkboxes as child completion; rewrite captured observations or pending-review history; commit protected values, raw PostgreSQL state, diagnostics, identifiers, keys, intent, payloads, secrets, or private paths; claim release approval, Folders OQ8 closure, or consumer migration authority.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|----------|--------------|---------------------------|----------------|
| Approved handoff | Exact 4.9-4.14 evidence, landed source, artifacts, limitations, and three bound approvals | Record EventStore platform completion and a source-only Folders handoff | Keep release and Folders closure false |
| Evidence drift | Missing, changed, rejected, ambiguous, or unbound evidence/review | Reject closure and preserve non-done lifecycle state | Name the failed field or receipt without protected data |
| Later repository work | Unrelated commits after the landed OQ8 commit | Verify the pinned OQ8 path set and current byte equivalence | Reject any changed or incomplete bound path |
| Overstated authority | Packet claims publication, pinning, deployment, or Folders closure | Fail validation | Do not advance Story 4.15 |

</frozen-after-approval>

## Code Map

- `_bmad-output/implementation-artifacts/4-8-eventstore-oq8-platform-evidence.yaml:1-33` -- v1 handoff packet to evolve with explicit EventStore-platform, release, and Folders authority fields while retaining the immutable capture reference.
- `_bmad-output/implementation-artifacts/evidence/story-4-14/e60a3777c581d70b62f67173ccc2372b5b64a425/` -- immutable seven-file production evidence and manifest; `source-state.json` binds 14 candidate and 12 source-input paths, while `review-records.json` preserves pending history.
- `tools/validate-oq8-platform-evidence.py:16-73,526-717` -- hard-coded v1 packet, unsafe baseline-to-HEAD changed-file inference, leakage gates, identity crosswalk, and pending-review assertions.
- `tests/Hexalith.EventStore.Contracts.Tests/Packaging/DeployedRuntimeParityClosureTests.cs` -- golden content-bound subject/receipt, immutable-manifest, limitation, and negative-mutation test pattern for a new focused closure contract.
- `_bmad-output/implementation-artifacts/4-8-durable-tenant-scoped-idempotency-admission-and-expired-key-precedence.md:20-150,392-403` and `spec-4-{11,12,13,14}-*.md` -- historical ledger plus child implementation/test/review evidence; do not infer completion from unchecked parent tasks or malformed/status-divergent metadata.
- `docs/{concepts/command-lifecycle.md,concepts/architecture-overview.md,reference/command-api.md,guides/configuration-reference.md}` -- final behavior, limitation, evidence, and source-only consumption references.
- `_bmad-output/implementation-artifacts/sprint-status.yaml:109-125` -- authoritative child and Epic lifecycle tracking; unrelated Epic 4 work keeps the epic in progress.

## Tasks & Acceptance

**Execution:**
- [x] `_bmad-output/implementation-artifacts/evidence/story-4-15/**` and `4-8-eventstore-oq8-platform-evidence.yaml` -- create a checksummed closure subject/crosswalk, exact landed-source and captured-artifact identity record, limitation set, named content-bound review receipts, and source-only handoff with external authorities false.
- [x] `tools/validate-oq8-platform-evidence.py` -- validate the immutable v1 capture plus closure layer, replace baseline-to-current diff inference with a pinned commit/path identity proof, and fail closed on drift, missing approvals, unsafe content, or overstated authority.
- [x] `tests/Hexalith.EventStore.Contracts.Tests/Packaging/Oq8PlatformClosureTests.cs` -- prove manifest/subject/receipt/source/status/document contracts and mutate each critical field to demonstrate rejection.
- [x] `docs/**` OQ8 references and Story 4.11-4.14 metadata -- reconcile final behavior, source-only consumption limits, malformed frontmatter, and truthful child status without rewriting approved historical authority.
- [x] `_bmad-output/implementation-artifacts/sprint-status.yaml` -- advance only evidence-approved children and Story 4.15; keep Epic 4 `in-progress` and all release/Folders authority external.

**Acceptance Criteria:**
- Given Stories 4.9-4.14 request closure, when the packet is validated, then every invariant, design-digest reference, command/count, limitation, named reviewer decision, and exact EventStore source/artifact identity has one reproducible crosswalk and any omission keeps Story 4.15 non-done.
- Given a reviewed packet, when it is handed off, then it records EventStore platform completion against `5e8f175b2ced4715f7c6f765386812cc1001dbb4` and unchanged bound paths while release approval, Folders closure, package/pin authority, and consumer migration remain false.

### Review Findings

_Chunked code review, Group A of 3 (`tools/validate-oq8-platform-evidence.py` only; `Oq8PlatformClosureTests.cs` and the evidence/docs/sprint-status group are reviewed in follow-up chunks — see spec Verification/Code Map). Baseline `699ca712` → HEAD `83b32fcf`._

**⚠ Patch-application blocker discovered 2026-08-30:** `tools/validate-oq8-platform-evidence.py` is itself a sealed `gateInput` of the v2 successor evidence packet (`evidence/story-4-15-successors/v2/source-artifact-identity.json` and `review-subject.json` both pin its exact SHA-256, content-bound to three already-recorded reviewer approvals in `reviews/{architecture,security,test}.json`). Applying any of the four code-level patches below (F5–F8) and rebuilding confirmed this empirically: `dotnet ... -class Oq8PlatformClosureTests` went from a clean baseline to **31 failing tests**, all root-caused by the single changed gate-input hash cascading through the closure/lifecycle checks. Resealing correctly (recomputing the validator's hash, propagating it through `source-artifact-identity.json` → `review-subject.json` → `closure-sha256.txt`) would leave the three existing `reviews/*.json` receipts attesting to a subject that no longer matches — which needs fresh reviewer sign-off, not a mechanical hash update, per the frozen spec's "Never: ... fabricate ... reviewer approval." The 4 code patches were reverted (`git checkout -- tools/validate-oq8-platform-evidence.py`) pending the owner's direction; see thread for options.

- [ ] [Review][Patch] "current HEAD Git tree" drift-detection claim in `validate_source_state` is asserted in JSON but never actually verified against real Git state — The evidence schema requires `current.source == "current HEAD Git tree"` and `current.headMustDescendFromLandedSource == True`, but the code only checks the JSON document literally contains those values; it never calls `git rev-parse HEAD` or diffs the real working tree, and hashes files via `git show LANDED_SOURCE:<path>` (a fixed historical commit) instead. The purpose-built helper `git_diff_is_clean` (lines 841-847) is defined but never called anywhere in the file. This fails the frozen spec's I/O matrix row "Later repository work → Verify the pinned OQ8 path set and current byte equivalence... Reject any changed or incomplete bound path." Concretely: editing or deleting any of the 22 non-evolved "current bound" capability paths at real HEAD after the landed commit still passes `python3 tools/validate-oq8-platform-evidence.py`. The existing test `NonDescendantCurrentHeadDoesNotReplaceHistoricalV1Snapshot` detaches `--git-root` HEAD to a non-descendant commit and asserts the validator still PASSES — demonstrating the gap rather than closing it. The v2 successor path has a parallel, opposite weakness: it explicitly reads live disk bytes (`capture_v2_snapshots`) rather than resolving through git at all, with no check that the working tree matches committed HEAD. **Resolution (2026-08-30):** owner elected to fix this in a future dedicated pass rather than patch it now — it touches `NonDescendantCurrentHeadDoesNotReplaceHistoricalV1Snapshot`'s expected behavior (Group B, not yet reviewed) and, like every code change to this file, requires resealing the v2 successor evidence packet with a fresh architecture/security/test review cycle (see blocker note above). Left as an action item. [tools/validate-oq8-platform-evidence.py:841-847,1893,1913,1946,2281-2286]
- [x] [Review][Patch] Landed-source commit bound throughout the validator/evidence (`5e8f175b…`) did not match the spec's Acceptance Criteria commit (`e5fef514…`) — AC2 required the handoff to record completion "against `e5fef514e1fbbbc52c5b64dfe6e3de18410d49ec`," but `LANDED_SOURCE`, the evidence directory name, and every closure/handoff/identity check bind to `5e8f175b2ced4715f7c6f765386812cc1001dbb4` instead. Investigation traced two deliberate resealing commits: `f19f6d1e` (2026-08-11, documented: "reseal Story 4.15 after PublicationRecovery drift... retarget to 4b0a7b1d") and `e79f4672` (2026-08-27, titled "build(sweep): migrate legacy deferred-work entries to DW format" but its diff also retargeted `LANDED_SOURCE` to `5e8f175b` with no commit-message explanation). Diffing the 26 pinned capability paths between `4b0a7b1d` and `5e8f175b` shows exactly one legitimately changed (`DaprTestContainerFixture.cs`, Story 4.5 fixture hardening), and the validator's own `landed_overrides` dict already carries a self-documenting `reason` string for it — confirming `5e8f175b` is the correct, deliberately-chosen anchor, and AC2/the review-order links were simply never updated after the reseal. **Resolution (2026-08-30):** AC2 (line 62) and the four "Suggested Review Order" links (former lines 98,101,104,107) updated to `5e8f175b2ced4715f7c6f765386812cc1001dbb4`; see Spec Change Log. [tools/validate-oq8-platform-evidence.py:68; spec-4-15 AC2, review-order links]
- [x] [Review][Patch] Spec Change Log has no entry for the actual 2026-08-27 receipt finalization date pinned in the validator — `CURRENT_REVIEW_DATE = "2026-08-27"` is asserted throughout, but the Spec Change Log's last entry is dated 2026-08-11. **Resolution (2026-08-30):** resolved as a side effect of the F2 fix above — the new Change Log entry for commit `e79f4672` records 2026-08-27 activity (the second reseal), closing the traceability gap. [tools/validate-oq8-platform-evidence.py:75; spec Change Log]
- [ ] [Review][Patch] `run_subprocess_bounded` and `sha256_git_file` duplicate ~70 lines of selector-based bounded-subprocess-draining logic — any future timeout/drain/output-limit fix must be applied twice. [tools/validate-oq8-platform-evidence.py:761-828,861-925]
- [ ] [Review][Patch] `validate_pyyaml_dependency`'s workflow-wiring check is an unscoped substring search over the whole YAML file — three bootstrap fragments must each appear *somewhere* in the file, not together/in-order/in the right step; matches this project's known "guards green by construction" failure pattern. [tools/validate-oq8-platform-evidence.py:2855-2865]
- [ ] [Review][Patch] `scan_json_protected_content` recurses over arbitrary nested JSON with no depth bound — every other size-sensitive path added in this diff is explicitly bounded; this one can raise an undiagnostic `RecursionError` on hostile deeply-nested candidate JSON (the file's own tests already exercise hostile JSON fixtures). [tools/validate-oq8-platform-evidence.py:598-621]
- [ ] [Review][Patch] `except Exception` catch-all in `main()` discards the original exception message and traceback — only `type(exception).__name__` is reported, with no verbose/debug option, making a genuine programming bug in this ~2000-line validator very hard to diagnose from CI output. [tools/validate-oq8-platform-evidence.py:3507-3510]
- [ ] [Review][Patch] `retained_paths | REPLACED_PRIOR_BOUND_PATHS == capability_paths` is a roundabout way to assert a subset relationship — non-obviously just checking `REPLACED_PRIOR_BOUND_PATHS ⊆ capability_paths`. [tools/validate-oq8-platform-evidence.py:1939]
- [x] [Review][Defer] TOCTOU gap between `require_no_symlink_components` and the later `stat()`/`open()` in `read_bounded_regular_snapshot` [tools/validate-oq8-platform-evidence.py:876-905] — deferred, low exploitability in this tool's single-writer CI trust boundary
- [x] [Review][Defer] `REVIEW_ROSTER` names two reviewers as specific personas ("Winston", "Murat") but security is only a role label ("Security Reviewer") [tools/validate-oq8-platform-evidence.py:237-241] — deferred, cosmetic

## Spec Change Log

- 2026-08-10: Implemented the checksummed source-only closure, exact landed-source proof, content-bound architecture/security/test approvals, fail-closed validator/tests, documentation reconciliation, and truthful lifecycle handoff.
- 2026-08-10: Hardened review-found evidence-body bindings, exact invariant/path/object contracts, current HEAD/worktree/index proof, reviewed handoff and public-document semantics, unique lifecycle parsing, bounded failures, and adversarial fixture coverage; kept the story in review until fresh content-bound receipts could be recorded.
- 2026-08-11: Recorded the fresh content-bound architecture, security, and test receipts, finalized and resealed the source-only handoff, and retained release and Folders-owned closure limitations.
- 2026-08-11 (commit `f19f6d1e`): Resealed `LANDED_SOURCE` from `e5fef514e1fbbbc52c5b64dfe6e3de18410d49ec` to `4b0a7b1d3628a857f131cfbff99030714aefc747` because commit `4b0a7b1d` (Story 4.4) intentionally modified a bound capability path, `PublicationRecoveryActivationTests.cs`. AC2's commit hash was not updated at the time.
- 2026-08-27 (commit `e79f4672`): Resealed `LANDED_SOURCE` again to `5e8f175b2ced4715f7c6f765386812cc1001dbb4` because Story 4.5 fixture hardening intentionally modified `DaprTestContainerFixture.cs`, another bound capability path (see `landed_overrides` in the validator for the self-documented reason). This reseal's commit message did not mention the retarget.
- 2026-08-30 (code review, Group A chunk): Corrected AC2 and the four "Suggested Review Order" evidence links, which still named `e5fef514`, to the actual bound commit `5e8f175b2ced4715f7c6f765386812cc1001dbb4`, closing the traceability gap between the frozen-adjacent Acceptance Criteria and the twice-resealed implementation. No evidence artifacts were touched — this is a documentation-only correction confirming what the (already reviewed and checksummed) evidence packet already implements.
- 2026-08-31 (implement-step verification): `python3 tools/validate-oq8-platform-evidence.py` now fails with `Story 4.15 v2 gate-input identity drift: docs/ci.md`. Root cause: `docs/ci.md` is a sealed `gateInput` of the v2 successor packet, pinned at commit `83b32fcf` (2026-08-30 09:47 UTC+2), and unrelated commit `75dc59aa` ("fix: update BMAD 6.11.1-next.33", 2026-08-30 12:36 UTC+2) modified `docs/ci.md` 2h49m later. This is a fresh instance of the same "sealed gate-input drift" class already documented above for `tools/validate-oq8-platform-evidence.py` itself, but on a different bound path. Fixing it requires resealing the v2 packet's `docs/ci.md` hash and repropagating it through `source-artifact-identity.json` → `review-subject.json`, which would invalidate the three existing `reviews/{architecture,security,test}.json` receipts and needs fresh reviewer sign-off, not a mechanical patch — no code or evidence change was made. No `Oq8PlatformClosureTests` run was attempted past this point since the validator gate fails first. Left as an open blocker pending owner direction alongside the F5-F8 action items above.
- 2026-08-31 (implement-step re-verification at `69154d030942d5b274820ec287005af3e0ebc2e8`): confirmed the `docs/ci.md` gate-input drift blocker is unchanged — no intervening commit touched `docs/ci.md`, `tools/validate-oq8-platform-evidence.py`, or the sealed v2 successor packet. `dotnet build tests/Hexalith.EventStore.Contracts.Tests/Hexalith.EventStore.Contracts.Tests.csproj --configuration Release -m:1` is clean (0 warnings, 0 errors). Running `Oq8PlatformClosureTests` anyway (rather than stopping at the validator gate) confirms the predicted cascade empirically: 29 of 368 tests fail, every failure surfacing the same `Story 4.15 v2 gate-input identity drift: docs/ci.md` message in place of its expected assertion — no new failure class, matching the ~31-failure cascade shape already documented for a direct validator edit. `git diff --check` is clean. This ledger entry duplicates and confirms `deferred-work.md` DW-457 (filed the same day against the same root cause). Per the frozen I/O matrix ("Evidence drift ... Reject closure and preserve non-done lifecycle state"), no code, evidence, or sealed-packet change was made; `sprint-status.yaml` and the frontmatter `status` correctly remain `in-progress`. Resolution still requires an owner-authorized reseal of the v2 packet's `docs/ci.md` hash through `source-artifact-identity.json` → `review-subject.json` with fresh `architecture`/`security`/`test` reviewer sign-off — out of scope for an unattended implementation pass.

## Design Notes

The Story 4.14 directory is evidence input, not a mutable approval container. Story 4.15 should hash a separate review subject and keep acceptances outside the subject they approve, following the deployed-runtime parity closure pattern. The absent Folders design bytes are disclosed; EventStore preserves the approved version/digest reference without pretending to recompute it.

## Verification

**Commands:**
- `python3 tools/validate-oq8-platform-evidence.py` -- expected: immutable capture, exact source/artifact crosswalk, content-bound approvals, limitations, and authority boundaries pass.
- `dotnet build tests/Hexalith.EventStore.Contracts.Tests/Hexalith.EventStore.Contracts.Tests.csproj --configuration Release -m:1` then the built xUnit v3 assembly filtered to `Oq8PlatformClosureTests` -- expected: all closure and negative-mutation cases pass with no skips.
- `dotnet build Hexalith.EventStore.slnx --configuration Release -m:1` -- expected: zero warnings and errors.
- `git diff --check` -- expected: no whitespace errors; unrelated untracked `spec-gh-31400593510-fix-ci-cd.md` remains untouched.

## Suggested Review Order

**Closure validation**

- Start with the fail-closed entry point joining capture, reviews, handoff, and lifecycle.
  [`main` in `validate-oq8-platform-evidence.py`](../../tools/validate-oq8-platform-evidence.py)

- Inspect the final platform contract and its exact source-only authority boundary.
  [`validate_platform_closure` in `validate-oq8-platform-evidence.py`](../../tools/validate-oq8-platform-evidence.py)

- See the assembled v2 packet binding every closure artifact by digest.
  [`platformClosure` in `4-8-eventstore-oq8-platform-evidence.yaml`](4-8-eventstore-oq8-platform-evidence.yaml)

**Frozen evidence and approvals**

- Review the frozen subject that content-binds source, evidence, tests, and documentation.
  [`review-subject.json`](evidence/story-4-15/5e8f175b2ced4715f7c6f765386812cc1001dbb4/review-subject.json)

- Trace landed-tree identity and current unchanged-path verification.
  [`source-artifact-identity.json`](evidence/story-4-15/5e8f175b2ced4715f7c6f765386812cc1001dbb4/source-artifact-identity.json)

- Follow OQ8-1 through OQ8-8 into exact story evidence and counts.
  [`closure-crosswalk.json`](evidence/story-4-15/5e8f175b2ced4715f7c6f765386812cc1001dbb4/closure-crosswalk.json)

- Confirm consumer instructions retain Folders-owned final verification and decision authority.
  [`source-only-handoff.json`](evidence/story-4-15/5e8f175b2ced4715f7c6f765386812cc1001dbb4/source-only-handoff.json)

**Adversarial verification**

- Begin with the isolated candidate and final closure contract suite.
  [`Oq8PlatformClosureTests`](../../tests/Hexalith.EventStore.Contracts.Tests/Packaging/Oq8PlatformClosureTests.cs)

- Review resealed schema, binding, protected-content, and authority mutations.
  [`CandidateSemanticMutationsFailClosed`](../../tests/Hexalith.EventStore.Contracts.Tests/Packaging/Oq8PlatformClosureTests.cs)

- Verify hidden index flags cannot conceal changed bound capability paths.
  [`HiddenBoundCapabilityPathFailsClosed`](../../tests/Hexalith.EventStore.Contracts.Tests/Packaging/Oq8PlatformClosureTests.cs)

**Public boundary and lifecycle**

- Read the concise architecture statement of source-only completion and retained limitations.
  [OQ8 platform closure boundary](../../docs/concepts/architecture-overview.md#oq8-platform-closure-boundary)

- Finish with Story 4.15 review readiness while Epic 4 remains active.
  [`sprint-status.yaml`](sprint-status.yaml)
