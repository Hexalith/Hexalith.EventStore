---
title: 'Story 4.6: Global Position Sharding Spec Renegotiation'
type: 'feature'
created: '2026-08-30'
status: 'awaiting-operator'
review_loop_iteration: 5
followup_review_recommended: true
baseline_commit: '1194dfe59bcbc9b235390d1e46a7dfe4ee115d94'
baseline_revision: '1194dfe59bcbc9b235390d1e46a7dfe4ee115d94'
predecessor_path: '_bmad-output/implementation-artifacts/spec-dapr-global-event-ordering.md'
predecessor_blob: '4c9edb37a8616aa373bd0054057c9e8eace6e0fa'
predecessor_sha256: '4bcff794a3c926f45e790ce462d562799db998feeab24aab1527ac6cf9ef1893'
predecessor_frozen_inner_sha256: '90be324c35d1545fd7c4dd53393ef27b08d2e6a3891d1bc9c6f38c9145740c10'
predecessor_frozen_element_sha256: 'c827761ba1f58aa6fde85ca8acedfdfdcc5097cbcbd470d2887a1e4d073d5d2c'
successor_path: '_bmad-output/implementation-artifacts/spec-dapr-global-event-ordering-v2.md'
successor_blob: '160331d25451928ff3c3dea2300b65cab4f97c3b'
successor_sha256: 'bbec7a16661995849891fae2617cf74c281d3da155086d0e22a39d5a2488f59a'
normative_sha256: '995fcecd16b3421ec9ff666d0884bfb5e436932aa49529c152fb7c439172a1fd'
superseded_normative_sha256: '2310f121dc48f59713a9c6bbc6ffe2e63be374d4d8ecc6e8d710a0d9cf3674de'
approval_state: 'absent'
implementation_authorized: false
context:
  - '{project-root}/_bmad-output/implementation-artifacts/epic-4-context.md'
warnings:
  - oversized
deferred:
  - summary: >-
      Repair the concurrent deferred-work ledger migration and its governance
      parser as one separately owned ledger-governance change.
    evidence: >-
      Review pass 5 reproduced 455 structured records that the bullet-only
      checker reports as an all-zero success, conflicting ledger and
      decision-journal identifiers, reopened accepted/resolved/closed work,
      lost structured provenance and severity, missing owner/review/grouping
      fields, malformed locations, and machine-local paths. These defects
      belong to the concurrent deferred-work migration and must not be edited
      or hidden by Story 4.6.
    location: >-
      _bmad-output/implementation-artifacts/deferred-work.md
    severity: high
operator_actions:
  - 'Approve the exact committed successor as every architecture_owner resolved from the candidate commit immutable allowlist, binding each approval to the candidate commit, successor blob, normative SHA-256, and reviewed content.'
  - 'Commission and preserve every production-provider and topology evidence category required by successor section 7 against the approved successor identity.'
  - 'Authorize a separately reviewed implementation story only after exact-content approval and every blocking evidence category are satisfied.'
---

<intent-contract>

## Intent

**Problem:** The simplified v2 global-position successor is not reviewable as an exact-content candidate: its normative digest is `PENDING`, its scope names an obsolete baseline and add-only diff, its required Story 4.6 wrapper is missing, and it does not explicitly supersede the rejected historical candidate. The frozen v1 allocator therefore remains the only authority.

**Approach:** Repair and content-bind the current simplified semantic successor, recreate this wrapper as the auditable story record, and complete every repository-local verification without changing runtime behavior. Finish `awaiting-operator` because architecture-owner approval, production-path evidence, and implementation authorization are human-owned follow-ups.

## Boundaries & Constraints

**Always:** Preserve the complete frozen v1 file byte-for-byte and reproduce its blob, file, frozen-inner, frozen-element, and 19 clause identities. Keep v1 authoritative until exact-content approval and all downstream evidence gates pass. Bind the final successor normative bytes to one SHA-256 in both artifacts after every normative edit, identify the actual pre-work baseline and `A`/`M` scope, explicitly supersede historical digest `2310f121dc48f59713a9c6bbc6ffe2e63be374d4d8ecc6e8d710a0d9cf3674de`, and distinguish verification from approval. Preserve unrelated work and orchestrator bookkeeping.

**Never:** Do not modify `sprint-status.yaml`, the stale blocked-result history, `deferred-work.md`, the v1 predecessor, planning authority, source, tests, public contracts, persisted state, migration data/code, DAPR/Aspire topology, deployment templates, or generated APIs. Do not claim that an agent, status value, digest, editable Markdown, commit, or review constitutes human approval or production evidence. Do not authorize implementation, deployment, migration, rollback, or cutover in this story.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|----------|---------------|----------------------------|----------------|
| Predecessor binding | Frozen v1 bytes and 19 mapped clauses | Every declared identity reproduces; each clause is retained, amended, or superseded exactly once | Any drift or missing/extra clause invalidates the candidate |
| Candidate identity | Unique normative markers and final successor bytes | One digest matches successor frontmatter, content table, and wrapper | `PENDING`, duplicate markers, CRLF/BOM, mismatch, or stale digest fails validation |
| Mixed position comparison | Valid/invalid v1 and v2 positions across schemes, canonicalizations, shards, and generations | Full tagged identity is preserved; only positive v1 peers or same-partition v2 peers are ordered | Unknown, invalid, or unsupported comparisons fail closed with the specified outcome |
| Scope and authority | Final committed diff with no exact-content human approval | Only this wrapper is added and v2 is modified; status is `awaiting-operator` and v1 remains authoritative | Any forbidden path change or approval inference invalidates completion |

</intent-contract>

## Candidate Identity

| Identity | Final value |
|---|---|
| Candidate-scope baseline | `1194dfe59bcbc9b235390d1e46a7dfe4ee115d94` |
| Frozen v1 Git blob | `4c9edb37a8616aa373bd0054057c9e8eace6e0fa` |
| Frozen v1 file SHA-256 | `4bcff794a3c926f45e790ce462d562799db998feeab24aab1527ac6cf9ef1893` |
| Frozen v1 inner SHA-256 | `90be324c35d1545fd7c4dd53393ef27b08d2e6a3891d1bc9c6f38c9145740c10` |
| Frozen v1 element SHA-256 | `c827761ba1f58aa6fde85ca8acedfdfdcc5097cbcbd470d2887a1e4d073d5d2c` |
| Successor Git blob | `160331d25451928ff3c3dea2300b65cab4f97c3b` |
| Successor file SHA-256 | `bbec7a16661995849891fae2617cf74c281d3da155086d0e22a39d5a2488f59a` |
| Successor normative SHA-256 | `995fcecd16b3421ec9ff666d0884bfb5e436932aa49529c152fb7c439172a1fd` |
| Explicitly superseded historical digest | `2310f121dc48f59713a9c6bbc6ffe2e63be374d4d8ecc6e8d710a0d9cf3674de` (non-authoritative) |
| Required committed scope | `A` this wrapper; `M` the successor; no other path |

These identities are verification results, not approval. Exact-content approval
must bind the eventual candidate commit, successor blob, normative digest, and
reviewed content to every authenticated `architecture_owner` resolved from the
candidate commit's immutable allowlist. Until that human action and all section
7 evidence are complete, frozen v1 remains authoritative and implementation is
unauthorized.

## Code Map

- `_bmad-output/implementation-artifacts/spec-dapr-global-event-ordering-v2.md:28-516` -- writable normative successor; retain the simplified semantic direction, correct its content/scope and approval mechanics, then hash the final normative range.
- `_bmad-output/implementation-artifacts/spec-dapr-global-event-ordering-v2.md:519-533` -- writable detached identity/status area; publish the final digest and explicit supersession without placing approval evidence inside approved bytes.
- `_bmad-output/implementation-artifacts/spec-dapr-global-event-ordering.md:11-36,67-71` -- immutable v1 authority; verify complete and frozen byte identities without editing it.
- `_bmad-output/implementation-artifacts/1-20-github-approval-role-allowlist.json` -- read-only immutable role source; `architecture_owner` currently resolves to `jpiquot`, but approval must use membership at the candidate commit.
- `src/Hexalith.EventStore.Server/Events/DaprGlobalPositionAllocator.cs:8-20`, `Actors/GlobalPositionActor.cs:15-45`, and `Events/EventPersister.cs:51-142` -- read-only v1 single-actor, scalar counter, reservation-before-commit behavior; explains gaps and the absence of strict commit order.
- `src/Hexalith.EventStore.Contracts/Events/EventMetadata.cs:22-61`, `Server/Events/EventEnvelope.cs:29-46`, and `Contracts/Projections/ProjectionEventDto.cs:39-103` -- read-only scalar persisted/public surfaces that require a later versioned-contract story.
- `src/Hexalith.EventStore.Client/Queries/QueryCursorScope.cs:62-80`, `src/Hexalith.EventStore.Server/Projections/IProjectionCheckpointTracker.cs:5-30`, and `src/Hexalith.EventStore.DomainService/DomainSharedProjectionRebuildFingerprint.cs:18-36` -- read-only cursor/checkpoint/fingerprint seams; allocation labels are not committed-event cursors and cross-shard `Max(long)` is forbidden.
- `tests/Hexalith.EventStore.IntegrationTests/ContractTests/ProjectionWatermarkRebuildIntegrationTests.cs:28-98,185-195` -- read-only evidence of the current scalar watermark assumption; no test edit belongs to this spec-only story.
- `_bmad-output/implementation-artifacts/bmad-build-auto-result-4-6-global-position-sharding-spec-renegotiation.md` -- stale historical dirty-tree result; preserve as history and never treat it as current verification.
- `_bmad-output/implementation-artifacts/sprint-status.yaml` and `_bmad-output/implementation-artifacts/deferred-work.md` -- orchestrator/unrelated state; never write, stage, or revert.

## Tasks & Acceptance

**Execution:**

- [x] `_bmad-output/implementation-artifacts/spec-dapr-global-event-ordering-v2.md` -- preserve section 1's predecessor provenance baseline `5ddda34f2ff0ffb0f72a60c44b265f2e4838a332`; correct section 10 to the pre-work baseline and exact `A wrapper`/`M successor` commit scope; explicitly supersede the historical candidate; require a non-empty unique authenticated candidate-commit owner set; distinguish malformed recognized data from losslessly preserved unsupported identities; make invalid, unsupported, and unknown precedence explicit; bind equivalent strategy artifacts/configuration/resource budgets; require unanimous expiring evidence-only authority and teardown; require an exact unanimously approved Story 4.5 seam review for no-change applicability; fail closed when admitting a new post-cutover shard; require canonical second-precision UTC expiry; then replace both placeholder digest values with the SHA-256 of the final normative byte range.
- [x] `_bmad-output/implementation-artifacts/spec-4-6-global-position-sharding-spec-renegotiation.md` -- preserve this intent contract, record matching final identities and verification evidence, validate staged Git blobs with optimized execution rejected, canonical frontmatter openers, duplicate-key rejection, complete-line unique normative markers, exactly five predecessor identity rows, worktree-to-index predecessor equality, exact canonical 19 ID/range/digest tuples, provenance-baseline and normative predecessor-table reproduction, complete frontmatter cross-binding, candidate-tree allowlist loading, and normative-range-only semantic checks; require both index and worktree equality with committed story paths, prove the eventual commit has exactly two paths, complete review records, correct code-map ranges, and finalize frontmatter as `awaiting-operator` with the non-empty operator action list.

**Acceptance Criteria:**

- Given the frozen v1 authority, when the successor is validated, then the predecessor blob/file/frozen-range identities and all 19 clause dispositions reproduce while the predecessor remains byte-identical.
- Given tenant, domain, and composite options, when the successor is reviewed, then ownership, contention, uniqueness, monotonicity, gaps, commit-order limits, hot shards, recovery, scaling, provider dependencies, and measurable selection gates support the selected composite tenant+domain boundary.
- Given v1, v2, mixed-history, invalid, partial-fleet, restart, migration, overflow, rollback, cursor, checkpoint, projection, and diagnostic cases, when the normative contract is applied, then complete identities remain lossless, unsupported comparisons fail closed, immutable event/aggregate guarantees remain intact, and no allocation label is misrepresented as a committed global cursor.
- Given the final successor bytes, when content verification runs, then unique LF/no-BOM normative markers delimit one range whose SHA-256 exactly matches both successor declarations and this wrapper, with historical digest `2310f121dc48f59713a9c6bbc6ffe2e63be374d4d8ecc6e8d710a0d9cf3674de` explicitly non-authoritative.
- Given baseline `1194dfe59bcbc9b235390d1e46a7dfe4ee115d94`, when final scope verification inspects the committed story change, then it reports only `A` for this wrapper and `M` for the v2 successor, and every forbidden surface is unchanged.
- Given every agent-capable task is verified and committed but exact-content approval and production evidence are absent, when the story completes, then both artifacts say `awaiting-operator`, the operator actions are imperative and non-empty, and runtime implementation/deployment/migration/cutover remain unauthorized.

## Spec Change Log

- **Review loop 1 (2026-08-30, `bad_spec`):** Review found that the verification plan named `python3` without an executable command, did not state the exact staged pre-commit scope command, and told operators to use a time-varying "current" owner roster while the normative design bound ownership to the candidate commit. The plan now embeds a reproducible static contract/matrix test, separates exact pre-commit and post-commit scope commands, preserves the predecessor provenance baseline while correcting only the candidate-scope baseline, and resolves every approver from the candidate commit's immutable allowlist. This avoids unrepeatable content claims and conflicting approval sets. **KEEP:** the simplified sections 2-9 semantic direction; all frozen-v1 byte and 19-clause bindings; composite tenant+domain selection; fail-closed comparisons; immutable mixed history; planning-only approval effect; explicit supersession of the rejected candidate; exact `A` wrapper/`M` successor scope; detached imperative operator actions; final digest cross-binding; and zero runtime, state, topology, test, deployment, v1, `deferred-work.md`, or `sprint-status.yaml` changes.
- **Review loop 2 (2026-08-30, `bad_spec`):** Review proved that the first executable verifier read mutable working-tree bytes instead of staged blobs, allowed duplicate YAML keys and under-specified clause rows, and that path-restricted diff checks could not prove the complete commit. It also exposed a vacuous empty owner-set approval and capacity evidence with no drift invalidation. The plan now verifies staged blobs, rejects duplicate keys, requires the exact 19 IDs and unique ranges with retained/amended/superseded vocabulary, asserts a non-empty unique authenticated candidate-commit owner set, binds capacity evidence to trace/provider/topology/limits/validity identity with mandatory revalidation, and requires exact whole-commit scope plus post-commit byte equality and revalidation. This avoids approving or committing bytes different from those reviewed, silently accepting malformed clause/frontmatter state, vacuous approval, and stale capacity evidence. **KEEP:** every loop-1 KEEP item; section 1's predecessor provenance baseline; the re-derived candidate-bound owner semantics; the semantic contract in sections 2-9 except the narrowly added evidence-validity rule; and the explicit preservation/exclusion of unrelated staged work.
- **Review loop 3 (2026-08-30, `bad_spec`):** Review showed that Python optimization could disable every assertion while retaining green output, several wrapper identities were declared but not cross-checked, clause rows were not pinned to the canonical ID/range/digest mapping, and semantic checks searched detached prose as well as normative bytes. It also exposed unspecified negative-v1 handling and capacity evidence that did not bind its measurement method, implementation artifact, validity authority, or exact expiry boundary. The plan now rejects optimized execution, reproduces the predecessor from its provenance commit, pins every canonical clause tuple, cross-checks every staged identity and authorization field, searches normative bytes only, classifies negative v1 positions, and binds capacity evidence to measurement, implementation, validity-profile, and exclusive-expiry identity with drift revalidation. This avoids vacuous verification, mutable self-declared predecessor mappings, detached-prose substitutions, comparison divergence, and stale or indefinitely valid capacity evidence. **KEEP:** every loop-1 and loop-2 KEEP item; the third derivation's exact two-path candidate scope, candidate-commit owner semantics, explicit historical supersession, detached operator handoff, and final identity approach; all existing sections 2-9 semantics except the narrow negative-position and evidence-validity clarifications.
- **Review loop 4 (2026-08-30, `bad_spec`):** Review found a circular evidence gate—production-path evidence required an implementation artifact before any implementation authority existed—and found that evidence outside capacity could survive artifact or provider drift. It also identified incomparable option benchmarks, an unspecified unknown outer metadata version, and verifier gaps for the frontmatter opener, normative predecessor-table declarations, candidate-tree allowlist, and post-commit index equality. The plan now permits a separately authorized isolated non-production evidence candidate without production/migration/cutover authority, binds every evidence row to artifact/provider/configuration/topology identity with drift invalidation, requires equivalent option artifacts and resource budgets, classifies unknown outer versions, and hardens every cited verifier seam. This avoids an impossible authorization cycle, stale cross-category evidence, biased strategy selection, divergent version handling, malformed frontmatter, drifted normative provenance claims, wrong approvers, and revalidation of uncommitted staged bytes. **KEEP:** every earlier KEEP item; negative-v1 handling; capacity method/artifact/validity/expiry binding; optimization rejection; canonical 19-clause tuples; complete wrapper identity cross-binding; normative-range-only checks; and the exact third-derivation successor identity approach, while treating its hashes as superseded diagnostics after normative amendment.
- **Review loop 5 (2026-08-30, `bad_spec`):** Review found that unknown outer metadata had no lossless public variant, evidence-only authority still excluded the persisted/public formats its mandatory proof must exercise, one non-empirical dependency declaration was swept into universal production execution, expiry encoding was not canonical, and marker validation counted only already-perfect marker-plus-LF bytes. The plan now defines an opaque raw unsupported-position variant, makes invalid-over-unknown precedence explicit, authorizes v2 format/runtime/topology work only inside an isolated non-production evidence candidate, distinguishes empirical evidence rows from content-bound applicability declarations, freezes expiry as canonical UTC second precision, and validates unique bare marker tokens plus exact LF termination. This avoids lossy future-version handling, a residual evidence deadlock, impossible provider proof for a no-change declaration, validator time disagreement, and hidden near-duplicate markers. **KEEP:** every earlier KEEP item; the fifth derivation's non-production/no-deploy boundary, equivalent strategy budgets, all-category evidence drift binding, unknown-version `UnsupportedScheme` outcome, hardened staged verifier, exact two-path commit, validated commit message, and final operator-only handoff; no runtime, state, topology, tests, v1, ledger, or sprint-status changes.
- **Review loop 6 (2026-08-31, `bad_spec`):** Review found ambiguous zero-v1 precedence, conflated malformed and unsupported identities, under-authorized evidence and no-change applicability paths, missing teardown, no fail-closed admission for post-cutover shards, and three verifier gaps around duplicate authority rows, predecessor worktree drift, and marker-line boundaries. The contract now orders invalid before unsupported before unknown, preserves only structurally valid unsupported identities, requires unanimous authenticated candidate-owner authority with expiry and teardown, binds no-change declarations to an exact unanimously approved seam review, and gates every new shard before reservation. The verifier now requires five authority rows before mapping, staged/worktree predecessor equality, and complete-line markers. **KEEP:** every earlier KEEP item; exact frozen-v1 and candidate identities; composite selection; immutable history; planning-only approval; exact two-path scope; and zero runtime, state, topology, test, v1, ledger, or sprint-status changes.

## Review Triage Log

### 2026-08-30 — Review pass
- verdicts: 33 findings — high 3, medium 17, low 1, false 12, maybe-false 0
- findings:
  - `[false]` `[reject]` Blind hunter: the unrelated `deferred-work.md` rewrite makes the Story 4.6 candidate exceed two paths — the initial clean gate passed before that concurrent staged change appeared, section 10 permits pre-existing work outside the candidate, and the story commit is constrained to the wrapper and successor pathspec.
  - `[false]` `[reject]` Blind hunter: `status: in-review` contradicts the eventual operator handoff — `in-review` is the workflow-mandated transient status during this review and will be replaced only after review and commit complete.
  - `[false]` `[reject]` Blind hunter: checked tasks conflict with empty review logs and iteration zero — this was the first active review pass, the log is populated by this entry, and `review_loop_iteration` counts bad-spec loopbacks rather than ordinary review execution.
  - `[medium]` `[bad_spec]` Blind hunter: the `python3` verification item was prose rather than an executable command — the plan now embeds the exact static byte/frontmatter/clause/matrix test that the implementation must run.
  - `[medium]` `[bad_spec]` Blind hunter: documented post-commit scope commands did not reproduce the claimed staged pre-commit check — the plan now specifies separate exact `git diff --cached` pre-commit and baseline-to-`HEAD` post-commit commands.
  - `[false]` `[reject]` Blind hunter: complete-worktree validation has no required fail-closed tool — the contract requires inspection rather than a publication format, and exact candidate-commit name-status plus complete `git status` inspection detects whether unrelated work entered the candidate while allowing it to remain outside.
  - `[medium]` `[bad_spec]` Blind hunter: wrapper approval asks for "current" owners while the successor binds owners at the candidate commit — the frontmatter action now resolves the complete role membership from the candidate commit's immutable allowlist.
  - `[false]` `[reject]` Blind hunter: "reviewed content" lacks identity and an omitted record format makes approval impossible — candidate commit, successor blob, and normative digest jointly identify the reviewed bytes, while section 8 deliberately delegates publication encoding without weakening the required authenticated fields.
  - `[high]` `[defer]` Blind hunter: the concurrent ledger migration is invisible to `check-deferred-work.py` — verified 455 `### DW-*` records but checker JSON returned every count as zero with exit 0; this is external to Story 4.6 and must be fixed by the ledger/governance owner.
  - `[medium]` `[defer]` Blind hunter: the concurrent migration marks resolved or closed legacy records open — verified examples DW-62, DW-71, DW-73, DW-75, DW-296, DW-297, and DW-450; the story must preserve rather than edit that external change.
  - `[medium]` `[defer]` Blind hunter: DW-6 loses its deliberate accepted disposition under an outer open status — verified the accepted text is flattened into `reason` while the record status is `open`; external ledger-owner work.
  - `[medium]` `[defer]` Blind hunter: reconciliation prose became actionable open records — verified resolved Story 2.8 reconciliation is represented as open DW records; external ledger-owner work.
  - `[medium]` `[defer]` Blind hunter: structured provenance, ownership, evidence, severity, and disposition fields were flattened into `reason` — verified on migrated records including DW-6/DW-7; external ledger-owner work.
  - `[medium]` `[defer]` Blind hunter: generated ledger locations are truncated or non-actionable — verified missing closing braces and symbol-only locations; external ledger-owner work.
  - `[medium]` `[defer]` Blind hunter: the migration changes a whitespace reproducer from three spaces to one — the reviewed diff shows byte-changing normalization in external ledger data; ownership remains outside Story 4.6.
  - `[medium]` `[defer]` Blind hunter: migrated origin headings misattribute records from other stories — verified flattened origin metadata cannot preserve the actual source story reliably; external ledger-owner work.
  - `[low]` `[defer]` Blind hunter: a machine-local absolute `source_spec` remains in migrated reason text — verified at DW-362/DW-363-area records; low portability harm in external ledger work.
  - `[high]` `[defer]` Edge-case hunter: 455 structured records are silently omitted by the bullet-only governance parser — independently reproduced by the zero-count successful checker run; same external checker/ledger root cause.
  - `[medium]` `[defer]` Edge-case hunter: a closed regression class is reopened as DW-450 — verified `status: closed` inside `reason` and outer `status: open`; external ledger-owner work.
  - `[medium]` `[defer]` Edge-case hunter: top-level `source_spec` provenance is lost — verified it is flattened into free text and unavailable to structured consumers; external ledger-owner work.
  - `[medium]` `[defer]` Edge-case hunter: leading-dot repository locations are stripped — verified `.github/...` becomes `github/...`, breaking path resolution; external ledger-owner work.
  - `[medium]` `[defer]` Edge-case hunter: placeholder braces are truncated in generated locations — verified `acceptances/{subject_sha256}` becomes `acceptances/{subject_sha256`; external ledger-owner work.
  - `[false]` `[reject]` Edge-case hunter: wrapper remains unfinished at `in-review` — this is the mandated review-stage lifecycle value, not the finalized artifact value.
  - `[false]` `[reject]` Edge-case hunter: staged `deferred-work.md` necessarily violates the committed two-path scope — the file is pre-existing unrelated work and is excluded through an explicit story-only commit pathspec.
  - `[medium]` `[bad_spec]` Edge-case hunter: owner membership can drift between candidate and approval — the amended operator action and re-derived normative approval contract use only candidate-commit immutable membership.
  - `[high]` `[defer]` Verification-gap reviewer: normal docs validation treats the structured ledger as empty and all ledger-governance tests are skipped — verified the bullet-only parser and zero-count successful run; external governance-owner work.
  - `[medium]` `[defer]` Verification-gap reviewer: migrated resolved entries retain outer open status — verified multiple cited records; external ledger-owner work.
  - `[false]` `[reject]` Intent auditor: the wrapper lifecycle diverges from the requested operator handoff — review status is transient and finalization occurs only after commit and review evidence.
  - `[false]` `[reject]` Intent auditor: the unrelated ledger migration dominates the unified diff and therefore the story scope — it appeared after the clean baseline gate and remains outside the pathspec-constrained candidate commit.
  - `[medium]` `[bad_spec]` Intent auditor: document verification was only self-reported and no executable static test appeared in the diff — the plan now contains and requires the exact static contract/matrix test before any claims are recorded.
  - `[false]` `[reject]` Intent auditor: sharding runtime behavior is not exercised — the defensible and implemented intent is explicitly a spec renegotiation; production evidence is an operator action and runtime implementation is unauthorized.
  - `[false]` `[reject]` Intent auditor: orchestrator status does not prove verification — the diff correctly supplies content/scope evidence independently and leaves `sprint-status.yaml` untouched.
  - `[false]` `[reject]` Intent auditor: no commit exists yet despite the committed-outcome reading — commit is intentionally after review; the finding observes an intermediate workflow state rather than the finalized outcome.

### 2026-08-30 — Review pass 2
- verdicts: 38 findings — high 6, medium 18, low 1, false 13, maybe-false 0
- findings:
  - `[false]` `[reject]` Blind hunter: the unrelated ledger rewrite is necessarily Story 4.6 candidate content — it is pre-existing concurrent staged work, and the amended whole-commit assertion plus explicit story-only pathspec commit proves it remains outside the candidate.
  - `[false]` `[reject]` Blind hunter: transient `in-review` makes the final `awaiting-operator` claim false — the embedded final-state test ran before review and will run again after finalization; review status is intentionally temporary.
  - `[medium]` `[bad_spec]` Blind hunter: identity validation read mutable worktree bytes rather than staged candidate blobs — the verifier now loads each story artifact from Git's index and separately requires no unstaged story diff before commit.
  - `[high]` `[bad_spec]` Blind hunter: the path-restricted staged scope command could not detect another candidate path — final scope is now asserted over the complete baseline-to-`HEAD` name-status set after an explicit story-only commit.
  - `[high]` `[bad_spec]` Blind hunter: enumerated forbidden surfaces left most repository paths unchecked — the amended whole-commit equality permits exactly two rows and therefore covers every repository path.
  - `[medium]` `[bad_spec]` Blind hunter: post-commit checks neither failed on modified story bytes nor revalidated committed identity — finalization now requires index/worktree equality with `HEAD` and reruns the staged-blob verifier after commit.
  - `[medium]` `[bad_spec]` Blind hunter: malformed extra clause rows could evade the strict row regex — the verifier now counts every `| \`V1-` line in the bounded clause section and requires all 19 to parse.
  - `[medium]` `[bad_spec]` Blind hunter: invented IDs or duplicate predecessor ranges could pass — the verifier now compares the exact ID set and requires 19 unique source ranges.
  - `[medium]` `[bad_spec]` Blind hunter: arbitrary non-empty dispositions could pass — every disposition must now begin with `Retained`, `Amended`, or `Superseded`.
  - `[false]` `[reject]` Blind hunter: the mixed-position static test claims to prove every broad acceptance scenario — it is explicitly the matrix-row contract test; semantic completeness is independently judged by review and is not claimed by its PASS label.
  - `[medium]` `[bad_spec]` Blind hunter: duplicate frontmatter keys could be silently accepted by `yaml.safe_load` — the exact verifier now uses a loader that rejects every duplicate mapping key.
  - `[medium]` `[bad_spec]` Blind hunter: an empty or duplicate candidate owner set could make approval vacuous — the normative contract and verifier now require a non-empty unique set of authenticated stable identities from the candidate allowlist.
  - `[false]` `[reject]` Blind hunter: omission of one approval-record serialization makes the operator action impossible — the candidate/blob/digest identify the reviewed bytes and the authenticated field requirements are format-independent; section 8 intentionally delegates publication encoding.
  - `[medium]` `[bad_spec]` Blind hunter: capacity evidence had no validity identity or revalidation trigger — the successor must now bind trace, provider, topology, limits, and validity window and rerun evidence after any relevant drift.
  - `[high]` `[defer]` Blind hunter: structured deferred-work records remain invisible to the bullet-only checker — reproduced as 455 headings and a zero-count successful result; unrelated external ledger/governance ownership.
  - `[medium]` `[defer]` Blind hunter: accepted/resolved/closed ledger records are reopened as `open` — verified cited examples; unrelated external ledger ownership.
  - `[medium]` `[defer]` Blind hunter: migration flattens machine-readable provenance and dispositions — verified only a small minority retain top-level source/severity; unrelated external ledger ownership.
  - `[medium]` `[defer]` Blind hunter: generated locations lose dots/braces or contain non-locations — verified examples; unrelated external ledger ownership.
  - `[medium]` `[defer]` Blind hunter: migrated origins conflict with retained source provenance — verified cited cross-story attribution; unrelated external ledger ownership.
  - `[low]` `[defer]` Blind hunter: absolute machine-local paths remain in reason text — verified portability leak, but it is external ledger data and low impact relative to the governance failures.
  - `[medium]` `[defer]` Blind hunter: canonical open-record owner/review/grouping metadata is missing — verified the migrated shape cannot satisfy the current checker vocabulary even after parser support; unrelated external ledger ownership.
  - `[high]` `[defer]` Edge-case hunter: structured records are absent from governance input — independently reproduced; same external parser/migration root cause.
  - `[medium]` `[defer]` Edge-case hunter: DW-450's closed status is reopened — verified outer `open` contradicts retained closure; unrelated external ledger ownership.
  - `[medium]` `[defer]` Edge-case hunter: `source_spec` and accepted disposition are flattened — verified at DW-6; unrelated external ledger ownership.
  - `[medium]` `[defer]` Edge-case hunter: leading-dot repository paths are corrupted — verified `.github` becomes `github`; unrelated external ledger ownership.
  - `[medium]` `[defer]` Edge-case hunter: placeholder locations lose closing braces — verified at the acceptance path; unrelated external ledger ownership.
  - `[high]` `[bad_spec]` Edge-case hunter: post-commit checks could accept extra committed paths — the exact complete baseline-to-`HEAD` name-status assertion now rejects every additional path.
  - `[medium]` `[bad_spec]` Edge-case hunter: worktree bytes could differ from staged/committed bytes — the verifier now reads index blobs and finalization requires story paths equal `HEAD` before revalidation.
  - `[false]` `[reject]` Edge-case hunter: `in-review` is contradictory completion evidence — it is the workflow's temporary review state, not the finalized handoff.
  - `[high]` `[defer]` Verification-gap reviewer: the migrated ledger makes docs validation vacuously green and its tests are skipped — independently reproduced; unrelated external governance ownership.
  - `[false]` `[reject]` Intent auditor: the story is uncommitted — review necessarily precedes the required explicit story-only commit.
  - `[false]` `[reject]` Intent auditor: wrapper lifecycle is not yet `awaiting-operator` — review lifecycle is temporary and finalization occurs only after review and commit.
  - `[false]` `[reject]` Intent auditor: the embedded test cannot pass in the review diff — it validates final staged blobs, not the temporary unstaged review-status mutation, and is rerun after finalization.
  - `[false]` `[reject]` Intent auditor: external staged ledger content is part of the candidate — exact whole-commit verification distinguishes the eventual two-path candidate from preserved unrelated index state.
  - `[false]` `[reject]` Intent auditor: runtime behavior is untested — the selected intent is a specification renegotiation and expressly leaves production evidence and implementation unauthorized.
  - `[false]` `[reject]` Intent auditor: unchanged semantic sections mean no renegotiation occurred — the work reviews and exact-content-binds the simplified successor as a new candidate; a diff need not rewrite sound semantic clauses to make them authoritative candidates.
  - `[false]` `[reject]` Intent auditor: orchestrator ownership diverges — `sprint-status.yaml` remains untouched and is not used as verification.
  - `[false]` `[reject]` Intent auditor: skill/subagent use is not observable in the diff — that is a process property, not an artifact defect, and the workflow supplied it outside Git content.

### 2026-08-30 — Review pass 3
- verdicts: 33 findings — high 5, medium 15, low 1, false 12, maybe-false 0
- findings:
  - `[high]` `[defer]` Verification-gap reviewer: the concurrent structured ledger remains invisible to the bullet-only governance checker — reproduced 455 `### DW-*` records with a successful all-zero checker report; this is pre-existing external ledger/governance work and the Story 4.6 run must not edit it.
  - `[high]` `[defer]` Verification-gap reviewer: migrated DW identifiers conflict with the existing decision journal — verified `DW-450` names unrelated work in the ledger and `.bmad-loop/decisions.json`, while journal IDs `DW-459` and `DW-460` have no migrated record; external ledger-orchestrator ownership.
  - `[medium]` `[defer]` Verification-gap reviewer: completed ledger records are reopened by outer `status: open` fields — verified accepted/resolved/closed retained text at cited records; external ledger ownership.
  - `[high]` `[bad_spec]` Blind hunter: `PYTHONOPTIMIZE=1` disables every verifier assertion while leaving six green prints — the plan now refuses optimized execution before any assertion so a green run cannot be vacuous.
  - `[medium]` `[bad_spec]` Blind hunter: the declared successor file SHA-256 was never recomputed — the plan now hashes the staged successor and compares the complete-file SHA-256 to wrapper frontmatter.
  - `[medium]` `[bad_spec]` Blind hunter: wrapper approval and implementation flags were not cross-checked — the plan now requires both staged frontmatters to say approval absent and implementation unauthorized.
  - `[medium]` `[bad_spec]` Blind hunter: wrapper baseline, predecessor, successor-path, and superseded identities could drift — the plan now compares every declared static identity to canonical values and both staged artifacts.
  - `[high]` `[bad_spec]` Blind hunter: the 19 clause rows could be repointed to arbitrary predecessor ranges with recomputed hashes — the plan now pins the complete canonical ID-to-range-to-digest mapping before reproducing each range.
  - `[false]` `[reject]` Blind hunter: the 19 mapped clauses must be non-overlapping and collectively cover the predecessor — the contract binds 19 semantic source fragments, not a partition of every predecessor byte; canonical tuple pinning supplies the required identity guarantee.
  - `[medium]` `[bad_spec]` Blind hunter: current bytes did not prove predecessor provenance at commit `5ddda34f2ff0ffb0f72a60c44b265f2e4838a332` — the plan now loads that exact commit/path and compares its bytes to the staged frozen predecessor.
  - `[false]` `[reject]` Blind hunter: duplicate JSON object keys in the candidate allowlist can silently change current owners — the immutable candidate allowlist has no duplicate keys and is outside this two-path change; duplicate-key rejection was nevertheless added as a defense-in-depth verifier property.
  - `[false]` `[reject]` Blind hunter: consecutive-hyphen GitHub names make the current owner set malformed — the only candidate owner is `jpiquot`, so the claimed bad outcome does not occur; the syntax check was tightened without changing the owner source.
  - `[false]` `[reject]` Blind hunter: syntax validation falsely proves external authentication of owners — the verifier claims candidate allowlist structure and stable identifiers only; authenticated approval remains an explicit human operator action.
  - `[false]` `[reject]` Blind hunter: prefix-only operator-action validation lets the current instructions be arbitrary — both staged action lists contain the exact complete imperative instructions required by the intent; exact-list comparison was added without changing their content.
  - `[medium]` `[bad_spec]` Blind hunter: semantic phrase checks searched detached successor prose outside the hashed range — the plan now performs every scope, authority, comparison, and evidence phrase check against normative bytes only.
  - `[false]` `[reject]` Blind hunter: an unbounded validity window necessarily evades the gate — a downstream evidence specification owns the duration and method; the candidate requires that exact validity identity and expiry be bound rather than prescribing one universal duration.
  - `[medium]` `[bad_spec]` Blind hunter: capacity evidence could survive a changed implementation artifact or measurement method — the plan now binds both identities and requires revalidation on their drift.
  - `[medium]` `[bad_spec]` Blind hunter: negative signed v1 positions had no specified validation result — the plan now requires negative v1 scalars to produce `InvalidPosition` while zero alone remains `UnknownPosition`.
  - `[high]` `[defer]` Blind hunter: the concurrent ledger migration makes deferred-work governance vacuously green — independently reproduced and pre-existing outside Story 4.6; preserve it for the external owner.
  - `[medium]` `[defer]` Blind hunter: the concurrent ledger reopens resolved and closed records — verified cited outer/inner status contradictions; external ledger ownership.
  - `[medium]` `[defer]` Blind hunter: the concurrent ledger flattens provenance and governance metadata — verified canonical fields are embedded in free text and unavailable to the current checker; external ledger ownership.
  - `[medium]` `[defer]` Blind hunter: concurrent ledger locations lose braces or leading dots — verified cited malformed paths; external ledger ownership.
  - `[medium]` `[bad_spec]` Edge-case hunter: a changed sampling or statistical method did not invalidate capacity evidence — the plan now binds measurement-method identity and reruns evidence on drift.
  - `[low]` `[bad_spec]` Edge-case hunter: validity-window boundary semantics were ambiguous at the expiry instant — the plan now requires an exclusive UTC expiry and rejection at or after that instant.
  - `[medium]` `[defer]` Edge-case hunter: migrated ledger locations lose structural suffixes — verified in the concurrent ledger diff; external ledger ownership.
  - `[medium]` `[defer]` Edge-case hunter: legacy non-open dispositions become outer open records — verified in the concurrent ledger diff; external ledger ownership.
  - `[medium]` `[bad_spec]` Edge-case hunter: the successor claimed a validity profile while binding only a window — the plan now binds the validity-profile authority and derivation in addition to its exclusive UTC expiry.
  - `[false]` `[reject]` Intent auditor: the wrapper is not yet committed or `awaiting-operator` — `in-review` and an absent commit are mandatory transient workflow states; finalization occurs only after this review loop converges.
  - `[false]` `[reject]` Intent auditor: static checks do not perform human approval or production evidence — those actions are intentionally outside the repository and are the reason the final state is `awaiting-operator`.
  - `[false]` `[reject]` Intent auditor: most shard semantics predate the diff — Story 4.6 reviews and content-binds the simplified semantic candidate; sound retained clauses need not be rewritten to become part of the exact candidate.
  - `[false]` `[reject]` Intent auditor: runtime and automated test surfaces are unchanged — the captured intent is specification-only and expressly forbids runtime implementation before approval and evidence.
  - `[false]` `[reject]` Intent auditor: the complete review diff has three paths rather than the candidate's two — the third path is preserved concurrent index work that appeared after the clean gate; exact story-only commit and whole-commit verification exclude it.
  - `[false]` `[reject]` Intent auditor: reviewing the external ledger conflicts with preserving it — the workflow deliberately reviews the complete baseline diff for contamination while candidate scope and user authority require leaving that change untouched and uncommitted by this story.

### 2026-08-30 — Review pass 4
- verdicts: 41 findings — high 5, medium 19, low 2, false 15, maybe-false 0
- findings:
  - `[high]` `[defer]` Verification-gap reviewer: the concurrent structured ledger makes deferred-work verification vacuously green — reproduced 455 records and 446 open statuses while the checker returned all zero counts and exit 0; external ledger/governance ownership.
  - `[high]` `[defer]` Blind hunter: the concurrent ledger format is invisible to the bullet-only checker — independently reproduced; preserve the unrelated change for its owner.
  - `[medium]` `[defer]` Blind hunter: open migrated records lack owner, review date, and grouping — verified across the concurrent ledger; external ledger ownership.
  - `[medium]` `[defer]` Blind hunter: DW-6 loses its accepted disposition under outer `open` — verified in the concurrent ledger; external ledger ownership.
  - `[medium]` `[defer]` Blind hunter: resolved and closed records are reopened — verified the cited records in the concurrent ledger; external ledger ownership.
  - `[medium]` `[defer]` Blind hunter: reconciliation narratives become actionable open work — verified DW-24 and DW-25; external ledger ownership.
  - `[medium]` `[defer]` Blind hunter: provenance, ownership, evidence, resolution, prior status, and severity are flattened into reason text — verified; external ledger ownership.
  - `[medium]` `[defer]` Blind hunter: migrated locations are corrupted or non-actionable — verified missing leading dots/braces and code fragments used as locations; external ledger ownership.
  - `[medium]` `[defer]` Blind hunter: most migrated records lose structured severity — verified 364 of 455 lack the top-level field; external ledger ownership.
  - `[medium]` `[defer]` Blind hunter: migrated origin headings conflict with retained source provenance — verified the cited cross-story record; external ledger ownership.
  - `[low]` `[defer]` Blind hunter: copied full findings create unstable oversized ledger headings — verified in concurrent ledger content; low-impact external ledger quality issue.
  - `[false]` `[reject]` Blind hunter: the ledger rewrite necessarily violates the two-path story commit — it is preserved concurrent index work, and the required explicit pathspec commit plus whole-commit assertion excludes it.
  - `[false]` `[reject]` Blind hunter: checked tasks and `in-review` falsely claim committed finalization — the review status and absent commit are workflow-mandated transient states; tasks describe implementation completion while final commit checks are intentionally later.
  - `[low]` `[bad_spec]` Blind hunter: real-index wording appeared stale after the parent staged review inputs — the recorded result is now explicitly labeled as the fourth-derivation implementation-handoff snapshot, avoiding a claim about current index state.
  - `[high]` `[bad_spec]` Blind hunter: implementation authorization was circular because evidence required an implementation artifact — the plan now permits a separately authorized isolated non-production evidence candidate while continuing to forbid production, migration, cutover, or v2 authority.
  - `[medium]` `[bad_spec]` Blind hunter: only capacity evidence was artifact/drift-bound — the plan now requires every evidence row to bind the tested artifact, provider/configuration, and topology and invalidates all categories on drift.
  - `[medium]` `[bad_spec]` Blind hunter: option capacity comparisons did not bind equivalent artifacts, configurations, and resource budgets — the plan now requires equivalent baselines, optimization policy, provider configuration, resource budget, and measurement method for all three strategies.
  - `[false]` `[reject]` Blind hunter: zero-v1 versus malformed-v2 comparison precedence is ambiguous — structural validation explicitly precedes comparison and `UnknownPosition` applies only to an otherwise-valid zero v1 position, so malformed input yields `InvalidPosition`.
  - `[medium]` `[bad_spec]` Blind hunter: unknown outer metadata versions had no outcome — the plan now requires them to return `UnsupportedScheme` without discarding raw identity.
  - `[medium]` `[bad_spec]` Blind hunter: the verifier did not require the canonical `---` frontmatter opener — the plan now rejects every staged artifact that lacks exact `---\n` opening bytes.
  - `[medium]` `[bad_spec]` Blind hunter: normative predecessor table values could drift independently of verified bytes — the plan now parses the bounded section-1 table and requires the exact five canonical identities.
  - `[false]` `[reject]` Blind hunter: disposition prefixes permit the current rows to be contradictory — every current disposition was semantically reviewed and each exact canonical clause appears once; the static test is not claimed to replace semantic review.
  - `[false]` `[reject]` Blind hunter: substring checks make the current semantics negated or unrelated — the cited current normative sentences are affirmative and unambiguous, and independent review supplies semantic validation beyond structural tests.
  - `[false]` `[reject]` Blind hunter: GitHub login syntax is claimed as external authentication — the verifier checks the immutable candidate roster; authenticated approval remains a separate human action bound to that roster.
  - `[high]` `[defer]` Edge-case hunter: structured ledger headings delete checker-visible input — independently reproduced; external ledger/governance ownership.
  - `[medium]` `[defer]` Edge-case hunter: accepted debt is reopened — verified in concurrent DW-6; external ledger ownership.
  - `[medium]` `[defer]` Edge-case hunter: owner and evidence fields become unqueryable reason text — verified in the concurrent ledger; external ledger ownership.
  - `[medium]` `[defer]` Edge-case hunter: resolved Story 2.8 reconciliation becomes false open work — verified; external ledger ownership.
  - `[medium]` `[defer]` Edge-case hunter: dot-prefixed locations lose their leading dot — verified; external ledger ownership.
  - `[medium]` `[bad_spec]` Edge-case hunter: arbitrary four-byte frontmatter prefixes could pass — the plan now requires the exact canonical opening delimiter before YAML parsing.
  - `[false]` `[reject]` Edge-case hunter: case-only duplicate owners affect the candidate — the candidate owner set contains only `jpiquot`; case-fold uniqueness was added as defense in depth.
  - `[false]` `[reject]` Edge-case hunter: contradictory duplicate conformance rows currently pass — the current table has one row per tested scenario; exact-one matching was added as defense in depth.
  - `[false]` `[reject]` Edge-case hunter: unknown clause IDs evade row counting — every `| `V1-` row is counted before regex parsing, so any extra or malformed ID changes the required count or canonical mapping and fails.
  - `[medium]` `[bad_spec]` Edge-case hunter: index-loaded owners need not equal candidate-tree owners when unrelated staging exists — the plan now loads the allowlist from the immutable candidate baseline, which the exact two-path commit leaves unchanged.
  - `[high]` `[bad_spec]` Edge-case hunter: post-commit worktree equality could coexist with different staged story bytes — finalization now requires both worktree and index equality to `HEAD` before rerunning the index-blob verifier.
  - `[false]` `[reject]` Intent auditor: the story does not implement runtime sharding — the defensible selected intent is specification renegotiation and explicitly leaves implementation unauthorized.
  - `[false]` `[reject]` Intent auditor: the wrapper is still `in-review` and uncommitted — those are mandatory transient review states; finalization occurs after convergence.
  - `[false]` `[reject]` Intent auditor: static tests do not execute the production behavior — production evidence is an explicit human-owned operator action and is not claimed by repository-local verification.
  - `[false]` `[reject]` Intent auditor: human approval and evidence remain absent — that is the intended reason for the final `awaiting-operator` handoff, and both artifacts say approval absent and implementation unauthorized.
  - `[false]` `[reject]` Intent auditor: the complete review diff includes the concurrent ledger — full-diff review detects contamination while the story-only commit and whole-commit assertion exclude it without editing or reverting user work.
  - `[false]` `[reject]` Intent auditor: skill and subagent use are not visible in content — those are execution-process properties supplied by the workflow, not artifact defects.

### 2026-08-30 — Review pass 5
- verdicts: 32 findings — high 5, medium 16, low 2, false 9, maybe-false 0
- findings:
  - `[false]` `[reject]` Blind hunter: the concurrent ledger is necessarily part of the story change — commit `a823ef4a` contains exactly the wrapper and successor, while the unrelated staged ledger remains outside it.
  - `[high]` `[defer]` Blind hunter: the concurrent ledger format remains invisible to the bullet-only checker — reproduced all-zero success for 455 structured records; external ledger/governance ownership.
  - `[high]` `[defer]` Blind hunter: migrated ledger IDs conflict with `.bmad-loop/decisions.json` and leave decisions orphaned — verified cited identities; external orchestrator/ledger ownership.
  - `[medium]` `[defer]` Blind hunter: open migrated records lack owner, next-review, and grouping — verified; external ledger ownership.
  - `[medium]` `[defer]` Blind hunter: structured provenance and severity are flattened or missing — verified top-level field counts; external ledger ownership.
  - `[medium]` `[defer]` Blind hunter: DW-6 loses its accepted disposition under outer `open` — verified; external ledger ownership.
  - `[medium]` `[defer]` Blind hunter: reconciliation history becomes actionable open work — verified cited DW-24 through DW-26; external ledger ownership.
  - `[medium]` `[defer]` Blind hunter: resolved Story 2.6 and 2.11 defects are reopened — verified cited records; external ledger ownership.
  - `[medium]` `[defer]` Blind hunter: owner-ratified accepted debt is reopened — verified cited record; external ledger ownership.
  - `[medium]` `[defer]` Blind hunter: closed DW-450 is reopened — verified outer/inner status contradiction; external ledger ownership.
  - `[medium]` `[defer]` Blind hunter: generated ledger locations lose leading dots or closing braces — verified; external ledger ownership.
  - `[medium]` `[defer]` Blind hunter: many generated ledger locations are symbols or truncated code rather than paths — verified cited examples; external ledger ownership.
  - `[low]` `[defer]` Blind hunter: migrated reasons retain machine-local absolute paths — verified; low portability and disclosure harm in external ledger data.
  - `[low]` `[bad_spec]` Blind hunter: wrapper Code Map line ranges were stale — the plan now requires derivation to calculate the final normative and detached ranges after all normative edits.
  - `[medium]` `[defer]` Blind hunter: verified external ledger defects had no structured wrapper handoff — earlier `bad_spec` cascades made defers moot; after convergence they must be grouped into the wrapper's single `deferred` list without editing the ledger.
  - `[high]` `[bad_spec]` Blind hunter: unknown outer metadata could not round-trip through a two-variant public union — the plan now requires an opaque raw unsupported-position variant that preserves the exact outer version and payload while remaining unordered.
  - `[medium]` `[bad_spec]` Blind hunter: invalid-versus-unknown precedence could diverge — the plan now states that both operands are fully validated first and any invalid input wins before zero/unknown handling.
  - `[high]` `[bad_spec]` Blind hunter: evidence-only authority still excluded the v2 formats its mandatory proof needs — the plan now expressly permits isolated v2 persisted/public formats, allocator behavior, and topology solely inside the non-production evidence candidate.
  - `[medium]` `[bad_spec]` Blind hunter: a Story 4.5 no-change applicability declaration could not satisfy universal empirical production evidence — the plan now distinguishes content-bound applicability declarations from empirical rows and requires empirical evidence only when the declaration identifies an affected seam.
  - `[medium]` `[bad_spec]` Blind hunter: near-duplicate marker tokens with trailing text evaded marker counting — the verifier now counts unique bare tokens and separately requires exact LF termination.
  - `[high]` `[bad_spec]` Edge-case hunter: unknown metadata versions lacked a lossless public representation — same opaque-variant root cause and amendment as the blind finding.
  - `[medium]` `[bad_spec]` Edge-case hunter: exclusive expiry had no canonical parse format — the plan now requires UTC `YYYY-MM-DDTHH:MM:SSZ` at second precision and rejects every other encoding.
  - `[medium]` `[defer]` Edge-case hunter: migrated ledger records lack source severity — verified in concurrent external data.
  - `[medium]` `[defer]` Edge-case hunter: closed legacy work is migrated as open — verified in concurrent external data.
  - `[false]` `[reject]` Edge-case hunter: wrapper `in-review` contradicts the operator handoff — this is the mandatory transient review status and is finalized after convergence.
  - `[false]` `[reject]` Edge-case hunter: committed story equality cannot be reproduced — the only difference is the transient review-status byte; exact post-commit equality passed before review and will be rerun after finalization.
  - `[false]` `[reject]` Intent auditor: final lifecycle is not visible in the review snapshot — commit `a823ef4a` already proves agent-capable work was committed, while wrapper finalization intentionally follows review convergence.
  - `[false]` `[reject]` Intent auditor: the transient wrapper status differs from final `awaiting-operator` — review status is an intermediate workflow property, not the final handoff.
  - `[false]` `[reject]` Intent auditor: the supplied baseline diff includes unrelated ledger work — the committed candidate excludes it and leaves that staged user/orchestrator change untouched.
  - `[false]` `[reject]` Intent auditor: static checks do not exercise runtime sharding — the selected defensible intent is specification-only and defers runtime work until approval and evidence.
  - `[false]` `[reject]` Intent auditor: human approval and production evidence remain external — their absence is accurately recorded and drives `awaiting-operator`; repository checks do not claim otherwise.
  - `[false]` `[reject]` Intent auditor: skill/subagent execution is not shown by file content — this is a process property, not an artifact defect.

### 2026-08-31 — Review pass 6
- verdicts: 44 findings — high 9, medium 20, low 1, false 14, maybe-false 0
- findings:
  - `[high]` `[defer]` Blind hunter: the concurrent structured ledger is invisible to the bullet-only checker — already captured by the wrapper's high-severity grouped ledger-governance deferral; Story 4.6 leaves the ledger untouched.
  - `[medium]` `[defer]` Blind hunter: accepted and resolved ledger work is reopened — verified and covered by the existing grouped external deferral.
  - `[medium]` `[defer]` Blind hunter: closed DW-450 becomes actionable — verified and covered by the existing grouped external deferral.
  - `[medium]` `[defer]` Blind hunter: structured provenance, severity, disposition, and ownership are flattened — verified and covered by the existing grouped external deferral.
  - `[medium]` `[defer]` Blind hunter: open migrated records lack canonical governance metadata — verified and covered by the existing grouped external deferral.
  - `[medium]` `[defer]` Blind hunter: migrated repository paths lose dots and braces — verified and covered by the existing grouped external deferral.
  - `[medium]` `[defer]` Blind hunter: many migrated locations are symbols rather than actionable files — verified and covered by the existing grouped external deferral.
  - `[low]` `[defer]` Blind hunter: migrated reasons retain machine-local paths — verified low portability harm and covered by the existing grouped external deferral.
  - `[high]` `[defer]` Blind hunter: ledger IDs conflict with the decision journal — verified and covered by the existing grouped external deferral.
  - `[medium]` `[patch]` Blind hunter: zero-v1 versus unsupported/invalid input precedence was difficult to read from list order — patched the ordered outcomes and prose so invalid wins, then unsupported, then otherwise-valid zero/unknown.
  - `[medium]` `[patch]` Blind hunter: malformed recognized v2 data and structurally valid unsupported schemes were not sharply distinguished — patched structural/support classification and broadened the opaque raw identity variant only for valid unsupported identities.
  - `[false]` `[reject]` Blind hunter: an ordinary public object cannot preserve unknown raw identity bytes — the opaque variant explicitly owns the exact raw buffer and forbids interpretation or normalized reserialization.
  - `[false]` `[reject]` Blind hunter: the successor must define payload-size limits — transport and persisted-event size limits are existing implementation constraints outside this spec-only renegotiation; the raw variant creates no new accepted production authority.
  - `[high]` `[patch]` Blind hunter: evidence-only authority had no named approver — patched it to require unanimous authenticated approval from the exact candidate-commit architecture-owner set, with expiry, fencing, and teardown.
  - `[high]` `[patch]` Blind hunter: a no-change Story 4.5 declaration could be self-authored — patched it to require an exact seam diff/review and unanimous authenticated candidate-owner approval.
  - `[false]` `[reject]` Blind hunter: capacity validity can be arbitrarily extended by the implementation author — the exact validity-profile authority and derivation are bound and owner-reviewed; this semantic spec intentionally leaves duration policy to that downstream evidence profile.
  - `[high]` `[patch]` Blind hunter: newly admitted post-cutover pairs lacked a fail-closed first-reservation gate — patched lifecycle authority, collision-free identity, unused generation, readiness/recovery, and checkpoint staleness requirements.
  - `[medium]` `[patch]` Blind hunter: duplicate predecessor authority rows could collapse through `dict` — patched the verifier to require exactly five parsed identity rows before mapping comparison.
  - `[medium]` `[patch]` Blind hunter: marker tokens could be prefixed on a noncanonical line — patched both markers to require start-of-file or preceding LF as well as exact following LF.
  - `[false]` `[reject]` Blind hunter: cumulative baseline scope fails to prove the story candidate — the story candidate is the complete run from captured baseline through final `HEAD`; exact A/M whole-tree equality intentionally permits the workflow's reviewed follow-up commit.
  - `[high]` `[defer]` Edge-case hunter: structured ledger headings disappear from governance — independently verified and covered by the existing grouped external deferral.
  - `[medium]` `[defer]` Edge-case hunter: closed ledger work is reopened — verified and covered by the existing grouped external deferral.
  - `[medium]` `[defer]` Edge-case hunter: source provenance is flattened — verified and covered by the existing grouped external deferral.
  - `[medium]` `[defer]` Edge-case hunter: severity is lost from structured fields — verified and covered by the existing grouped external deferral.
  - `[medium]` `[defer]` Edge-case hunter: open items lack owner/review/grouping — verified and covered by the existing grouped external deferral.
  - `[medium]` `[defer]` Edge-case hunter: dot-prefixed paths are corrupted — verified and covered by the existing grouped external deferral.
  - `[medium]` `[defer]` Edge-case hunter: placeholder paths lose closing braces — verified and covered by the existing grouped external deferral.
  - `[high]` `[defer]` Edge-case hunter: ledger IDs were allocated without reserving decision-journal IDs — verified and covered by the existing grouped external deferral.
  - `[medium]` `[patch]` Edge-case hunter: zero-v1 versus unsupported input outcome was ambiguous — resolved by the same explicit invalid/unsupported/unknown ordering patch.
  - `[high]` `[patch]` Edge-case hunter: evidence-only authority lacked the accountable role — resolved by unanimous authenticated candidate-owner approval and lifecycle controls.
  - `[high]` `[patch]` Edge-case hunter: a false no-seam declaration could bypass Story 4.5 — resolved by required seam diff/review and unanimous candidate-owner approval.
  - `[medium]` `[patch]` Edge-case hunter: duplicate or malformed predecessor authority rows could pass — resolved by exact five-row parsing plus canonical mapping equality.
  - `[medium]` `[patch]` Edge-case hunter: the verifier could miss worktree drift in frozen v1 — patched each exact run to require worktree predecessor bytes equal the staged frozen blob.
  - `[medium]` `[patch]` Edge-case hunter: marker validation omitted the preceding line boundary — resolved by complete-line anchoring for both bare tokens.
  - `[false]` `[reject]` Intent auditor: the diff cannot prove use of the named skill — skill use is an execution-process property supplied by this run, not a content defect.
  - `[false]` `[reject]` Intent auditor: orchestrator status ownership diverges — `sprint-status.yaml` is untouched and absent from candidate history.
  - `[false]` `[reject]` Intent auditor: commit completion is not visible in a unified diff — commit `a823ef4a` and the final reviewed follow-up commit provide Git-history evidence independently of the review snapshot.
  - `[false]` `[reject]` Intent auditor: the wrapper remains `in-review` — that byte is the mandatory transient review state and is finalized to `awaiting-operator` after this pass.
  - `[false]` `[reject]` Intent auditor: imperative operator actions are missing — both artifacts contain the same three exact, non-empty imperative actions.
  - `[false]` `[reject]` Intent auditor: cross-artifact lifecycle verification fails — it passed before review and is rerun after restoring the final wrapper status.
  - `[false]` `[reject]` Intent auditor: the three-path review diff is the story commit — the ledger is concurrent staged work; exact candidate history contains only A wrapper and M successor.
  - `[false]` `[reject]` Intent auditor: the review fails to preserve unrelated ledger work — that change remains staged, byte-preserved, and excluded from both story commits.
  - `[false]` `[reject]` Intent auditor: no runtime sharding implementation exists — the defensible selected story is explicitly a specification renegotiation and keeps implementation unauthorized.
  - `[false]` `[reject]` Intent auditor: repository checks do not perform human approval or production evidence — those external actions are intentionally absent and enumerated for the operator.

## Design Notes

The current simplified successor is the active semantic draft. Historical digest `2310f121dc48f59713a9c6bbc6ffe2e63be374d4d8ecc6e8d710a0d9cf3674de` and blob `e12fc28b2aed3b1609a4d9b86935dd950357b6d9` identify a larger candidate whose final recorded review still contained unresolved `bad_spec` findings; they are recovery context, not authority. The pre-edit diagnostic digest `bf1a1e8651261de1bcc75bb6f75046d8f7fc93b8824482778da31b0b29aa03c8` is also non-authoritative because correcting the normative scope changes the hashed bytes. Hash only after normative review is complete.

## Verification

**Recorded results (2026-08-30):**

- Frozen-v1 worktree diff: exit `0`; Git blob
  `4c9edb37a8616aa373bd0054057c9e8eace6e0fa`; complete-file SHA-256
  `4bcff794a3c926f45e790ce462d562799db998feeab24aab1527ac6cf9ef1893`.
- Final staged-blob contract and matrix test: the embedded verifier printed all
  six `PASS` lines. A second run with `PYTHONOPTIMIZE=1` exited `1` before any
  assertion with `optimized Python disables verification assertions`, proving
  optimized execution is rejected.
- Final isolated whole-candidate name-status assertion: exactly `A` for this
  wrapper and `M` for the successor. Candidate whitespace validation exited `0`
  with no output.
- Final real-index inspection preserved the pre-existing staged
  `deferred-work.md` change while staging the two story paths. The explicit
  story-only commit excluded that unrelated path without editing or reverting
  it.
- Final post-commit validation reported exactly `A` for this wrapper and `M`
  for the successor relative to the candidate-scope baseline. Both worktree and
  index story bytes equaled `HEAD`, and committed-blob revalidation printed the
  same six `PASS` lines.

**Commands:**

- `git hash-object _bmad-output/implementation-artifacts/spec-dapr-global-event-ordering.md && sha256sum _bmad-output/implementation-artifacts/spec-dapr-global-event-ordering.md` -- expected: blob `4c9edb37a8616aa373bd0054057c9e8eace6e0fa` and file SHA-256 `4bcff794a3c926f45e790ce462d562799db998feeab24aab1527ac6cf9ef1893`.
- Run this exact staged-blob contract and matrix test both before commit and again after the committed-tree equality check below -- expected: six `PASS` lines and no assertion:

  ```bash
  python3 - <<'PY'
  import hashlib
  import json
  from pathlib import Path
  import re
  import subprocess
  import sys

  import yaml

  if sys.flags.optimize:
      raise SystemExit('optimized Python disables verification assertions')

  v1_path = '_bmad-output/implementation-artifacts/spec-dapr-global-event-ordering.md'
  v2_path = '_bmad-output/implementation-artifacts/spec-dapr-global-event-ordering-v2.md'
  wrapper_path = '_bmad-output/implementation-artifacts/spec-4-6-global-position-sharding-spec-renegotiation.md'
  allowlist_path = '_bmad-output/implementation-artifacts/1-20-github-approval-role-allowlist.json'
  baseline = '1194dfe59bcbc9b235390d1e46a7dfe4ee115d94'

  class UniqueKeyLoader(yaml.SafeLoader):
      pass

  def unique_mapping(loader, node, deep=False):
      mapping = {}
      for key_node, value_node in node.value:
          key = loader.construct_object(key_node, deep=deep)
          assert key not in mapping, f'duplicate YAML key: {key}'
          mapping[key] = loader.construct_object(value_node, deep=deep)
      return mapping

  UniqueKeyLoader.add_constructor(yaml.resolver.BaseResolver.DEFAULT_MAPPING_TAG, unique_mapping)

  def staged(path):
      return subprocess.check_output(['git', 'show', f':{path}'])

  def frontmatter(payload):
      assert payload.startswith(b'---\n')
      text = payload.decode('utf-8')
      end = text.index('\n---\n', 4)
      return yaml.load(text[4:end], Loader=UniqueKeyLoader)

  def unique_json_object(pairs):
      mapping = {}
      for key, value in pairs:
          assert key not in mapping, f'duplicate JSON key: {key}'
          mapping[key] = value
      return mapping

  v1, v2, wrapper = staged(v1_path), staged(v2_path), staged(wrapper_path)
  assert Path(v1_path).read_bytes() == v1, 'worktree predecessor differs from staged predecessor'
  for path, payload in ((v1_path, v1), (v2_path, v2), (wrapper_path, wrapper)):
      payload.decode('utf-8', errors='strict')
      assert not payload.startswith(b'\xef\xbb\xbf') and b'\r' not in payload and payload.endswith(b'\n'), path

  v1_lines = v1.splitlines(keepends=True)
  assert hashlib.sha256(v1).hexdigest() == '4bcff794a3c926f45e790ce462d562799db998feeab24aab1527ac6cf9ef1893'
  assert hashlib.sha256(b''.join(v1_lines[11:35])).hexdigest() == '90be324c35d1545fd7c4dd53393ef27b08d2e6a3891d1bc9c6f38c9145740c10'
  assert hashlib.sha256(b''.join(v1_lines[10:36])).hexdigest() == 'c827761ba1f58aa6fde85ca8acedfdfdcc5097cbcbd470d2887a1e4d073d5d2c'
  assert subprocess.run(['git', 'hash-object', '--stdin'], input=v1, stdout=subprocess.PIPE).stdout.decode().strip() == '4c9edb37a8616aa373bd0054057c9e8eace6e0fa'
  provenance_commit = '5ddda34f2ff0ffb0f72a60c44b265f2e4838a332'
  assert subprocess.check_output(['git', 'show', f'{provenance_commit}:{v1_path}']) == v1
  v2_text = v2.decode('utf-8')
  clause_section = v2_text[v2_text.index('### 1.1 Exact disposition'):v2_text.index('## 2. Shard selection')]
  clause_lines = [line for line in clause_section.splitlines() if line.startswith('| `V1-')]
  rows = re.findall(r'^\| `(V1-[A-Z]+-[0-9]{2})` \| `(L([0-9]+):B([0-9]+)-B([0-9]+))` \| `([0-9a-f]{64})` \| ([^|]+) \|$', clause_section, re.MULTILINE)
  expected_clauses = {
      'V1-PROBLEM-01': ('L15:B13-B152', '0c68cd6d7d0f2c094d287ed44055803615d70374d5fa6896e48907e8979bd427'),
      'V1-PROBLEM-02': ('L15:B153-B317', '6edf7a21a1cd7be910fa0305694e45c85f7812547466a48134219fc3f7571f83'),
      'V1-APPROACH-01': ('L17:B14-B143', '69485cd508cd8b029e17f3fb7bc547214d6abac5ac19983a00036f192ef9af5b'),
      'V1-APPROACH-02': ('L17:B145-B276', '292f6a8c92901b8613d1ca1f2ec1a5835d5f32489864f782f31880bbe8a10803'),
      'V1-APPROACH-03': ('L17:B277-B384', 'cba5cd2562f1295274d034ee68c5caad2012d796ebc444e06538a1b553e918f1'),
      'V1-ALWAYS-01': ('L21:B12-B78', '24f68313de132e96bc578232fb45bb0e0c9b0281a4fc8fd3a6b7b187076c80c1'),
      'V1-ALWAYS-02': ('L21:B79-B131', 'c80a0344e68fb4272404b89b92ea0d13a4b51578dac61345ca2060ef4bc51e35'),
      'V1-ALWAYS-03': ('L21:B132-B215', '4cc4c1b73f6421db7072c555aae9c359378b704264a302d0105e66c3d18dc60a'),
      'V1-ALWAYS-04': ('L21:B216-B261', 'ae9052038db96ca79619ea3638b31da38174531b1dbd1a0e8e5e70c8c84aaf28'),
      'V1-ASK-01': ('L23:B15-B81', '10af81fc0e662671e4e4c9cb25194bef9a59d600403f9f8dc28b158f2de57e3d'),
      'V1-ASK-02': ('L23:B83-B126', '58545c552fe030a77258c1ebd1af20edcbffb1cea173194eb0840d3de1c75543'),
      'V1-ASK-03': ('L23:B131-B194', '1ae24060b647e77a702b1d4ba8e33e95db35a58686e440cd6e67e348b682fa90'),
      'V1-NEVER-01': ('L25:B11-B76', 'fb3495c3d6c9e0b3045ed876a65289776e606e21e12e9141195317553010548f'),
      'V1-NEVER-02': ('L25:B77-B162', 'd35c61b7fc7112389855ef766a32605dc55728f2b34cc67bc38b054166723875'),
      'V1-NEVER-03': ('L25:B163-B227', 'eb7af1f2df5fd0291e06ada3f868715ee0bf8f791c23e4dfce29192856736e11'),
      'V1-MATRIX-01': ('L31:B0-B192', '537c943ab6fd978efb4e904316a6f5ee2ebc79a07f054763279ad51c723caff1'),
      'V1-MATRIX-02': ('L32:B0-B197', '084b4196420fb288edb4defc630e4e65d912015a95895b75d77422329c43f699'),
      'V1-MATRIX-03': ('L33:B0-B145', '973e786b859b14b84d0012ed74216ff32cf10097d0898db32458634396ec7110'),
      'V1-MATRIX-04': ('L34:B0-B180', '4656e87697efd4547a9ea51ff987f17e346840bbbfccfcbad55e87d19f6ad355'),
  }
  assert len(clause_lines) == len(rows) == 19
  assert {row[0]: (row[1], row[5]) for row in rows} == expected_clauses
  for clause_id, _, line_text, start_text, end_text, declared, disposition in rows:
      line = v1_lines[int(line_text) - 1].removesuffix(b'\n')
      start, end = int(start_text), int(end_text)
      assert 0 <= start < end <= len(line) and hashlib.sha256(line[start:end]).hexdigest() == declared, clause_id
      assert disposition.strip().startswith(('Retained', 'Amended', 'Superseded')), clause_id
  print('matrix/predecessor-binding: PASS')

  begin_token = b'<!-- HX-GPOS-V2-NORMATIVE-BEGIN -->'
  end_token = b'<!-- HX-GPOS-V2-NORMATIVE-END -->'
  assert v2.count(begin_token) == v2.count(end_token) == 1
  begin_at, end_at = v2.index(begin_token), v2.index(end_token)
  assert begin_at < end_at
  assert begin_at == 0 or v2[begin_at - 1:begin_at] == b'\n'
  assert end_at == 0 or v2[end_at - 1:end_at] == b'\n'
  assert v2[begin_at:begin_at + len(begin_token) + 1] == begin_token + b'\n'
  assert v2[end_at:end_at + len(end_token) + 1] == end_token + b'\n'
  normative = v2[begin_at + len(begin_token) + 1:end_at]
  normative_text = normative.decode('utf-8')
  authority_section = normative_text[normative_text.index('## 1. Authority'):normative_text.index('### 1.1 Exact disposition')]
  authority_matches = re.findall(r'^\| ([^|]+?) \| `([^`]+)` \|$', authority_section, re.MULTILINE)
  assert len(authority_matches) == 5
  authority_rows = dict(authority_matches)
  assert authority_rows == {
      'Baseline commit': provenance_commit,
      'Git blob': '4c9edb37a8616aa373bd0054057c9e8eace6e0fa',
      'Complete file SHA-256': '4bcff794a3c926f45e790ce462d562799db998feeab24aab1527ac6cf9ef1893',
      'Frozen inner bytes SHA-256': '90be324c35d1545fd7c4dd53393ef27b08d2e6a3891d1bc9c6f38c9145740c10',
      'Complete frozen element SHA-256': 'c827761ba1f58aa6fde85ca8acedfdfdcc5097cbcbd470d2887a1e4d073d5d2c',
  }
  digest = hashlib.sha256(normative).hexdigest()
  v2_fm, wrapper_fm = frontmatter(v2), frontmatter(wrapper)
  assert re.fullmatch(r'[0-9a-f]{64}', digest)
  assert digest == v2_fm['normative_sha256'] == wrapper_fm['normative_sha256']
  assert re.findall(r'^\| Normative content SHA-256 \| `([0-9a-f]{64})` \|$', v2_text, re.MULTILINE) == [digest]
  v2_blob = subprocess.run(['git', 'hash-object', '--stdin'], input=v2, stdout=subprocess.PIPE).stdout.decode().strip()
  assert v2_blob == wrapper_fm['successor_blob']
  assert hashlib.sha256(v2).hexdigest() == wrapper_fm['successor_sha256']
  expected_wrapper = {
      'baseline_commit': '1194dfe59bcbc9b235390d1e46a7dfe4ee115d94',
      'baseline_revision': '1194dfe59bcbc9b235390d1e46a7dfe4ee115d94',
      'predecessor_path': v1_path,
      'predecessor_blob': '4c9edb37a8616aa373bd0054057c9e8eace6e0fa',
      'predecessor_sha256': '4bcff794a3c926f45e790ce462d562799db998feeab24aab1527ac6cf9ef1893',
      'predecessor_frozen_inner_sha256': '90be324c35d1545fd7c4dd53393ef27b08d2e6a3891d1bc9c6f38c9145740c10',
      'predecessor_frozen_element_sha256': 'c827761ba1f58aa6fde85ca8acedfdfdcc5097cbcbd470d2887a1e4d073d5d2c',
      'successor_path': v2_path,
      'superseded_normative_sha256': '2310f121dc48f59713a9c6bbc6ffe2e63be374d4d8ecc6e8d710a0d9cf3674de',
      'approval_state': 'absent',
      'implementation_authorized': False,
  }
  assert all(wrapper_fm[key] == value for key, value in expected_wrapper.items())
  assert v2_fm['predecessor_path'] == v1_path
  assert v2_fm['predecessor_blob'] == expected_wrapper['predecessor_blob']
  assert v2_fm['predecessor_sha256'] == expected_wrapper['predecessor_sha256']
  assert v2_fm['superseded_normative_sha256'] == expected_wrapper['superseded_normative_sha256']
  assert v2_fm['status'] == wrapper_fm['status'] == 'awaiting-operator'
  assert v2_fm['approval_state'] == wrapper_fm['approval_state'] == 'absent'
  assert v2_fm['implementation_authorized'] is wrapper_fm['implementation_authorized'] is False
  expected_actions = [
      'Approve the exact committed successor as every architecture_owner resolved from the candidate commit immutable allowlist, binding each approval to the candidate commit, successor blob, normative SHA-256, and reviewed content.',
      'Commission and preserve every production-provider and topology evidence category required by successor section 7 against the approved successor identity.',
      'Authorize a separately reviewed implementation story only after exact-content approval and every blocking evidence category are satisfied.',
  ]
  for fm in (v2_fm, wrapper_fm):
      assert fm['operator_actions'] == expected_actions
  print('matrix/candidate-identity: PASS')

  allowlist = json.loads(subprocess.check_output(['git', 'show', f'{baseline}:{allowlist_path}']), object_pairs_hook=unique_json_object)
  owners = allowlist['roles']['architecture_owner']
  assert isinstance(owners, list) and owners and len(owners) == len(set(owners)) == len({owner.casefold() for owner in owners})
  assert all(isinstance(owner, str) and '--' not in owner and re.fullmatch(r'[A-Za-z0-9](?:[A-Za-z0-9-]{0,37}[A-Za-z0-9])?', owner) for owner in owners)
  assert 'non-empty' in normative_text and 'unique' in normative_text and 'candidate commit' in normative_text
  print('approval-owner-set: PASS')

  outcomes = ['InvalidPosition', 'UnsupportedScheme', 'UnsupportedCrossScheme', 'UnsupportedCrossCanonicalization', 'UnsupportedCrossShard', 'UnsupportedCrossGeneration', 'UnknownPosition', 'Less', 'Equal', 'Greater']
  positions = [normative_text.index(f'`{outcome}`', normative_text.index('### 3.3 Validation')) for outcome in outcomes]
  assert positions == sorted(positions)
  for scenario, outcome in {'Same shard, different generation': 'UnsupportedCrossGeneration', 'Different tenant or domain': 'UnsupportedCrossShard', 'V1 versus v2': 'UnsupportedCrossScheme', 'Unknown or invalid data': 'InvalidPosition', 'Zero v1 with unsupported peer': 'UnsupportedCrossScheme', 'Unsupported versus malformed identity': 'InvalidPosition', 'Mixed-history cursor': 'no counter maximum is accepted as progress', 'Post-cutover new shard': 'cannot reserve'}.items():
      matches = [line for line in normative_text.splitlines() if line.startswith(f'| {scenario} |')]
      assert len(matches) == 1 and outcome in matches[0]
  assert 'Negative v1 values are invalid.' in normative_text
  assert 'Unknown outer metadata versions return `UnsupportedScheme`.' in normative_text
  assert 'opaque raw unsupported-position variant' in normative_text
  assert 'Unsupported outcomes take precedence over `UnknownPosition`.' in normative_text
  assert '`UnknownPosition` applies only when both operands are recognized, supported,' in normative_text
  assert 'the opaque\nvariant MUST NOT sanitize or preserve them as merely unsupported' in normative_text
  assert 'not malformed solely because its scheme\nor canonicalization discriminator is unrecognized' in normative_text
  assert 'Allocation positions MUST NOT be used as lossless committed-event cursors.' in normative_text
  print('matrix/mixed-position-comparison: PASS')

  assert f'Relative to baseline `{baseline}`' in normative_text
  assert '- `A` `_bmad-output/implementation-artifacts/spec-4-6-global-position-sharding-spec-renegotiation.md`' in normative_text
  assert '- `M` `_bmad-output/implementation-artifacts/spec-dapr-global-event-ordering-v2.md`' in normative_text
  assert 'Exact-content architecture-owner approval authorizes downstream planning only.' in normative_text
  for phrase in ('equivalent resource budget', 'non-production evidence candidate', 'evidence-only authority', 'authenticated unanimous approval', 'exact candidate-commit', 'isolated v2 persisted and public formats', 'MUST be fenced and drained', 'MUST be torn down', 'every empirical evidence row', 'applicability declaration', 'exact candidate diff', 'Author self-declaration is not approval', 'fail-closed shard admission', 'checkpoint whose exact shard set', 'trace identity', 'provider profile', 'provider configuration', 'topology fingerprint', 'acceptance limits', 'measurement method identity', 'implementation artifact identity', 'validity profile authority', 'exclusive UTC expiry', 'canonical UTC second precision', 'MUST be re-run'):
      assert phrase in normative_text, phrase
  print('matrix/scope-and-authority: PASS')
  print('frontmatter/operator-actions: PASS')
  PY
  ```

- `git diff --quiet -- _bmad-output/implementation-artifacts/spec-4-6-global-position-sharding-spec-renegotiation.md _bmad-output/implementation-artifacts/spec-dapr-global-event-ordering-v2.md && git diff --cached --name-status 1194dfe59bcbc9b235390d1e46a7dfe4ee115d94 -- _bmad-output/implementation-artifacts/spec-4-6-global-position-sharding-spec-renegotiation.md _bmad-output/implementation-artifacts/spec-dapr-global-event-ordering-v2.md` -- expected immediately before commit: no unstaged story bytes, then exactly `A` wrapper and `M` successor.
- `git diff --cached --check -- _bmad-output/implementation-artifacts/spec-4-6-global-position-sharding-spec-renegotiation.md _bmad-output/implementation-artifacts/spec-dapr-global-event-ordering-v2.md && git status --short` -- expected immediately before commit: no story whitespace errors; complete status inspected so unrelated `deferred-work.md` stays preserved outside the explicit story-only commit pathspec.
- `python3 -c 'import subprocess,sys; actual=subprocess.check_output(["git","diff","--name-status","1194dfe59bcbc9b235390d1e46a7dfe4ee115d94..HEAD"],text=True).splitlines(); expected=["A\t_bmad-output/implementation-artifacts/spec-4-6-global-position-sharding-spec-renegotiation.md","M\t_bmad-output/implementation-artifacts/spec-dapr-global-event-ordering-v2.md"]; sys.exit((print("committed candidate scope: PASS") or 0) if actual==expected else (print((actual,expected),file=sys.stderr) or 1))'` -- expected immediately after the explicit story-only commit: one `PASS` line proving every committed path, not an allowlist subset.
- `git diff --quiet HEAD -- _bmad-output/implementation-artifacts/spec-4-6-global-position-sharding-spec-renegotiation.md _bmad-output/implementation-artifacts/spec-dapr-global-event-ordering-v2.md && git diff --cached --quiet HEAD -- _bmad-output/implementation-artifacts/spec-4-6-global-position-sharding-spec-renegotiation.md _bmad-output/implementation-artifacts/spec-dapr-global-event-ordering-v2.md` followed by the exact staged-blob test above -- expected after commit: both exits 0 and the same six `PASS` lines, proving worktree and index story bytes equal the committed tree before revalidation.

**Manual checks (if no CLI):**

- Confirm every normative section observes the outer semantic surface, human approval authorizes planning only, all blocking evidence remains owed, and no historical digest/status is presented as current approval.

## Auto Run Result

### Summary

Story 4.6 adds an auditable wrapper and content-binds the simplified v2
composite tenant+domain global-position successor. The successor now defines
lossless known and unsupported position identities, fail-closed comparison and
shard admission, comparable evidence, evidence-only non-production authority,
candidate-bound ownership, and exact scope without changing runtime behavior.

### Files changed

- `_bmad-output/implementation-artifacts/spec-4-6-global-position-sharding-spec-renegotiation.md` — records intent, identities, five repair iterations, six review passes, verification, deferral, and operator handoff.
- `_bmad-output/implementation-artifacts/spec-dapr-global-event-ordering-v2.md` — publishes the exact normative successor and detached awaiting-operator status.

### Review findings

- Review passes 1 through 5 re-derived the candidate after verified planning
  gaps; each finding and KEEP constraint is recorded one-per-row above.
- Review pass 6 converged with eight patch groups: outcome precedence,
  unsupported identity classification, evidence-only approval, Story 4.5
  declaration approval, post-cutover shard admission, exact authority-row
  count, frozen-v1 worktree equality, and complete-line marker validation.
- The concurrent ledger/governance defects are represented by one high-severity
  structured deferral in frontmatter; the ledger itself remains untouched by
  this story.
- Every rejected finding and its evidence-backed reason is recorded one-per-row
  in the six `Review Triage Log` entries. Rejections cover transient review
  lifecycle observations, the intentionally external ledger diff, the selected
  spec-only scope, human-only evidence, and claims disproved by the exact
  contract or verifier.

### Follow-up review recommendation

`true` — review pass 6 applied three high-severity and five medium-severity
patch groups. Deferred and rejected findings are excluded from this count.

### Verification

- Exact staged-blob verifier: six `PASS` groups.
- Optimized Python guard: non-zero exit before assertions can be disabled.
- Frozen v1: Git blob, complete SHA-256, frozen ranges, provenance commit, and
  all 19 canonical clause tuples reproduced.
- Candidate identity: normative SHA-256
  `995fcecd16b3421ec9ff666d0884bfb5e436932aa49529c152fb7c439172a1fd`,
  successor blob `160331d25451928ff3c3dea2300b65cab4f97c3b`, and successor file SHA-256
  `bbec7a16661995849891fae2617cf74c281d3da155086d0e22a39d5a2488f59a`.
- Scope and whitespace: exactly `A` wrapper and `M` successor from captured
  baseline, with no story-path whitespace errors.
- Post-commit whole-scope, index/worktree equality, staged-blob revalidation,
  and commitlint are mandatory finalization checks immediately after commit.

### Residual risks

Exact-content architecture-owner approval, production-provider/topology
evidence, and separate implementation authorization remain absent and
operator-owned. Frozen v1 remains authoritative. The unrelated staged ledger
migration has verified governance defects and remains outside this story.
