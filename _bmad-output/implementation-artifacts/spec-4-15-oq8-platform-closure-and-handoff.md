---
title: 'Story 4.15: OQ8 Platform Closure And Handoff'
type: 'feature'
created: '2026-08-10'
status: 'in-progress'
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
- [ ] `_bmad-output/implementation-artifacts/evidence/story-4-15/**` and `4-8-eventstore-oq8-platform-evidence.yaml` -- create a checksummed closure subject/crosswalk, exact landed-source and captured-artifact identity record, limitation set, named content-bound review receipts, and source-only handoff with external authorities false.
- [ ] `tools/validate-oq8-platform-evidence.py` -- validate the immutable v1 capture plus closure layer, replace baseline-to-current diff inference with a pinned commit/path identity proof, and fail closed on drift, missing approvals, unsafe content, or overstated authority.
- [ ] `tests/Hexalith.EventStore.Contracts.Tests/Packaging/Oq8PlatformClosureTests.cs` -- prove manifest/subject/receipt/source/status/document contracts and mutate each critical field to demonstrate rejection.
- [ ] `docs/**` OQ8 references and Story 4.11-4.14 metadata -- reconcile final behavior, source-only consumption limits, malformed frontmatter, and truthful child status without rewriting approved historical authority.
- [ ] `_bmad-output/implementation-artifacts/sprint-status.yaml` -- advance only evidence-approved children and Story 4.15; keep Epic 4 `in-progress` and all release/Folders authority external.

**Acceptance Criteria:**
- Given Stories 4.9-4.14 request closure, when the packet is validated, then every invariant, design-digest reference, command/count, limitation, named reviewer decision, and exact EventStore source/artifact identity has one reproducible crosswalk and any omission keeps Story 4.15 non-done.
- Given a reviewed packet, when it is handed off, then it records EventStore platform completion against `e5fef514e1fbbbc52c5b64dfe6e3de18410d49ec` and unchanged bound paths while release approval, Folders closure, package/pin authority, and consumer migration remain false.

## Spec Change Log

## Design Notes

The Story 4.14 directory is evidence input, not a mutable approval container. Story 4.15 should hash a separate review subject and keep acceptances outside the subject they approve, following the deployed-runtime parity closure pattern. The absent Folders design bytes are disclosed; EventStore preserves the approved version/digest reference without pretending to recompute it.

## Verification

**Commands:**
- `python3 tools/validate-oq8-platform-evidence.py` -- expected: immutable capture, exact source/artifact crosswalk, content-bound approvals, limitations, and authority boundaries pass.
- `dotnet build tests/Hexalith.EventStore.Contracts.Tests/Hexalith.EventStore.Contracts.Tests.csproj --configuration Release -m:1` then the built xUnit v3 assembly filtered to `Oq8PlatformClosureTests` -- expected: all closure and negative-mutation cases pass with no skips.
- `dotnet build Hexalith.EventStore.slnx --configuration Release -m:1` -- expected: zero warnings and errors.
- `git diff --check` -- expected: no whitespace errors; unrelated untracked `spec-gh-31400593510-fix-ci-cd.md` remains untouched.
