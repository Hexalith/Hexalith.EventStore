# Brownfield Truth / Bounded-Scope Review — 2026-09-09 PRD Update

## Verdict

**Pass with one low audit caveat.** The PRD-only correction safely resolves every PRD-local Critical/High issue from the bound 2026-09-09 validation and converts every external dependency into a fail-closed blocker with an owner and trigger. Section 11.1 matches current `epics.md` primary ownership without promoting supporting or lifecycle work. Story 6.1/NFR8 and NFR7/SM11 now match current evidence. Frontmatter machine-readably rejects implementation readiness independently of document status. No architecture or epic-plan mutation is present in the current diff; however, the already-dirty worktree prevents Git alone from proving causal scope for every out-of-scope modified file.

## Review Basis

- Canonical PRD: `_bmad-output/planning-artifacts/prd.md`
- Bound prior result: `_bmad-output/planning-artifacts/prds/prd-eventstore-2026-07-05/validation-report.md`
- Current implementation slicing authority: `_bmad-output/planning-artifacts/epics.md`
- Current lifecycle authority/evidence inspected as needed: `_bmad-output/implementation-artifacts/sprint-status.yaml`, Story 4.15 and Story 6.1 wrappers, and `_bmad-output/implementation-artifacts/spec-folded-snapshot.md`
- Focused executable checks:
  - Folded-snapshot normative digest recomputation returned `0b456b5fcc49c6f7431e3476cefd073c184cf181d64bf84fb76414c1110d16b2`.
  - `python3 tools/validate-oq8-platform-evidence.py --pre-review` exited nonzero with `Lifecycle status drift: 4-15-oq8-platform-closure-and-handoff`, as the PRD says.
  - `git diff --name-only -- _bmad-output/planning-artifacts/architecture.md _bmad-output/planning-artifacts/epics.md` returned no paths.

## Prior Critical / High Disposition

| Prior finding | Current disposition | Evidence |
| --- | --- | --- |
| Critical — FR ownership map contradicted retired OR12 | **Safely fixed in PRD.** Section 11.1 maps FR1 to 1.11, assigns sole primary owners where appropriate, and uses named disjoint slices for genuinely multipart requirements. | PRD 437–479; `epics.md` primary-ownership rule 386–390 and story declarations. OR8/OR14 block renewed handoff until mechanical drift protection and digest reconciliation exist. |
| Critical — `epics.md` fails source-drift gate | **Explicit blocker.** No false closure is claimed. | PRD OR14, line 545: architecture, epics, and approval reconciliation; named architecture/epic/product owners; trigger before downstream handoff or readiness re-run. |
| Critical — OQ8 normative bytes unreproducible inside EventStore | **Explicit blocker plus fail-closed semantics.** | PRD 102–108 and OR11 at line 543: absent/mismatched authority invalidates OQ8 closure/readiness; architecture and Folders content owners; trigger before OQ8 closure, READY, release, or handoff. |
| Critical — MVP success could coexist with concurrent append loss | **Safely fixed in PRD and explicitly blocked.** | NFR7 line 323, out-of-scope qualification line 385, SM11 line 424, and OR4 line 536 all say class (c) is not delivered and cannot be satisfied by deferral/scope wording. OR4 names product + architecture owners and completion/readiness/release/deployment triggers. |
| High — NFR7 claimed duplicate-side-effect closure too early | **Safely fixed in PRD.** | NFR7 line 323 and SM11 line 424 mark class (e) closure-pending; OR15 line 546 owns lifecycle reconciliation and passing pre-review evidence. The focused validator currently fails exactly on Story 4.15 drift. |
| High — Story 6.1 path and authorization claims were stale | **Facts fixed; lifecycle explicitly blocked.** | NFR8 line 324 and §11.3 lines 518–520 match the canonical approved-authorized file. Its normative digest recomputes, section 19 records the 4096-byte bound and Story 6.2 authorization, while OR5 line 537 preserves wrapper/tracker/epic disagreement. |
| High — lifecycle contradictions absent from ledger | **Safely fixed in PRD.** | OR15 line 546 names Stories 4.15, 5.2, 5.4, and 6.1, owners, affected-claim/readiness triggers, and guarded status-transition validation. |
| High — final document state obscured reopened readiness | **Safely fixed in PRD.** | Frontmatter lines 3–9 separates `document_status: draft` from `implementation_readiness_status: blocked` and `implementation_readiness_result: reject`, bound to date, baseline, and report. Section 0 line 85 forbids READY, MVP completion, release, deployment, migration, and dependent handoff. A future `status: final` therefore cannot authorize READY while the independent readiness fields remain blocked/reject. |
| High — omnibus requirements lack clause closure | **Explicit blocker.** | OR7 line 539 requires stable subclauses or an all-clauses-required clause/story/evidence table; product and story owners; trigger before readiness re-run. |
| High — high-risk closure lacks non-authorship and correctness gates | **Explicit blockers.** | OR10 line 542 and OR13 line 544 name the product/EventStore/test owners and bind closure to independent approval and an exact enforced lane/command/evidence/status-transition rule before high-risk readiness or the next readiness re-run. |
| High — no coherent Phase 4/MVP exit decision rule | **Explicit blocker.** | OR17 line 548 requires one mandatory-versus-post-MVP exit table with evidence, evaluator, waiver policy, and current result; product owner; trigger before readiness re-run. |
| High — architecture cites obsolete PRD baseline | **Explicit blocker.** | OR14 line 545 requires architecture-first reconciliation, then epic reconciliation and renewed approval; owners and trigger are explicit. |
| High — NFR8 and NFR18 not acceptance-complete | **Snapshot facts fixed; remaining acceptance explicitly blocked.** | NFR8 line 324 distinguishes approved snapshot specification from undelivered runtime and missing projection bound; OR16 line 547 owns the projection specification/bound. NFR18 line 334 and OR6 line 538 preserve the missing posture document and owner-story gap. |

## Ownership Cross-Check

Section 11.1 matches the current story declarations for FR1–FR37. In particular:

- FR1 correctly names Story 1.11, not Story 4.11.
- FR11, FR13, FR19, FR21, FR22, and FR25 each expose the sole primary owner declared by `epics.md`; maintenance, corrective-release, adoption, and other supporting stories are not promoted.
- FR12 and FR15 reproduce the exact disjoint slices and all-slices-required rules in `epics.md` lines 388–390.
- FR26, FR27, FR32–FR34, FR36, and FR37 retain explicitly partitioned primary slices; closure stories and supporting NFR evidence do not silently become whole-requirement delivery claims.
- The section's explanatory text at PRD line 479 explicitly says the table is ownership-only, omits supporting stories, and does not report lifecycle state.

## Evidence-Truth Cross-Check

- Story 6.1: the canonical file exists with `status: approved-authorized`; its detached approval block names the approver, records the recomputed digest, fixes `MaxSnapshotEnvelopeOverheadBytes = 4096`, records no open decisions, and explicitly authorizes Story 6.2. The wrapper is `done`, the tracker is `review`, and `epics.md` still says backlog/absent. PRD §11.3 and OR5 state that exact split without converting specification authorization into runtime delivery.
- NFR8: the PRD correctly separates the authorized snapshot specification from Story 6.2's undelivered runtime outcome and from the missing projection-cost specification/number, which remains blocked by OR16.
- NFR7/SM11: the PRD reports only classes (a), (b), and (d) delivered; class (c) remains unprevented, and class (e) remains closure-pending. The current OQ8 pre-review validator failure corroborates the latter.

## Findings

### Critical (0)

None.

### High (0)

None.

### Medium (0)

None.

### Low (1)

**[Scope audit] — The current dirty worktree cannot independently prove that every out-of-scope modification predates this PRD update.**

Evidence: the update memlog explicitly records PRD-only authorization and sets architecture, epics, tracker, evidence, OQ8, fencing, acceptance, and CI changes outside the run. The canonical architecture and `epics.md` have no current diff. The wider worktree nevertheless contains pre-existing modified implementation/tracker/test files, so a post-hoc `git diff` cannot by itself attribute those changes to a particular run.

Concrete fix: preserve a start-of-run path snapshot or bounded patch manifest in future PRD update runs. No correction to this PRD is required; its text and run log remain consistent with the authorized PRD-only scope.

## Gate Decision

Accept this PRD-only update as a truthful blocked baseline. Do not interpret this pass as an implementation-readiness approval: `blocked` / `reject` remains authoritative until the owner-bound OR4–OR8, OR10–OR17 conditions are closed and readiness is re-run.
