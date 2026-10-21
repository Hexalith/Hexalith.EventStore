# Sprint Change Proposal: Bound Evidence Storage and Retire Bulky Run Output

Date: 2026-10-21
Prepared at: 2026-10-21 11:41 UTC
Requested by: Administrator
Review mode: Batch, selected by the user
Status: Approved by Administrator on 2026-10-21; planning changes applied and handed off
Approval evidence: User reply “approve” in this course-correction conversation, recorded at 2026-10-21 11:54 UTC
Scope: Moderate; backlog additions and coordinated work in three owning repositories
Recommended path: Direct adjustment within existing epics

## 1. Issue Summary

Acceptance evidence has become a large part of the source checkout. EventStore's Story 6.1 P1R published-run requalification retains command outputs, history inventories, and repeated attempts. FrontComposer's Story 13.6 retains browser reports, traces, videos, screenshots, and archives of successful, failed, and interrupted runs. Some archives are also present as split parts. Automatic staging then carries these outputs into Git and downstream submodule checkouts.

The user reports October growth of approximately 5.9 G for EventStore and 5.0 G for FrontComposer, compared with earlier growth of 50–100 MB per month. This is an evidence-lifecycle and tooling defect. Hash binding requires trustworthy, retrievable bytes; it does not require every copy of every run to remain in the current source tree.

### Verified local baseline

Both repositories were clean on `main` when inspected:

| Repository | Examined HEAD |
| --- | --- |
| EventStore | `77c3300c12c07494530e1f203389d3147d04ea37` |
| FrontComposer, root-declared submodule | `248cdb7aac650cab015ab49a45210f61b80e2f43` |

The inventory below measures regular, tracked working-tree files under `_bmad-output` at those revisions. Byte totals are exact; GB means decimal gigabytes. It excludes Git object storage, nested submodules, ignored output, and untracked files. It is a current snapshot, not a recomputation of monthly growth or historical commit sizes.

| Measurement | EventStore | FrontComposer |
| --- | ---: | ---: |
| Tracked files under `_bmad-output` | 33,325 | 25,194 |
| Tracked bytes under `_bmad-output` | 5,949,616,042 | 5,311,290,533 |
| Approximate GB | 5.95 | 5.31 |
| Bytes in evidence directories starting `6-1-p1r` | 4,685,602,743 | N/A |
| Bytes in `evidence/story-6-4` | 603,475,959 | N/A |
| Bytes in `evidence/story-13-6` | N/A | 5,276,682,137 |
| JSON bytes across `_bmad-output` | 5,410,865,547 | 628,217,527 |
| Additional copies of identical Git blobs across `_bmad-output` | 1,541,634,838 | 1,251,763,665 |

Identical-blob counts sum each duplicate copy beyond the first. They describe repeated checkout content; they do not imply that Git stores another full object for every repeated path. Within EventStore's `6-1-p1r*` evidence directories alone, additional copies account for 1,330,208,668 bytes.

Concrete observations:

- `tools/p1r_published_executor.py`, `Executor.save` and `Executor.record`, writes the entire growing command list after every command. Rows retain complete decoded output and `output_sha256`. There are 122 tracked `commands.json` files totaling 721,641,681 bytes.
- One file, `evidence/6-1-p1r-31190-published-run/execution-f3d07b03cc0543cda7dd8a79e795dbe1/commands.json`, contains 5,116 commands and is 32,330,292 bytes. Its decoded output strings alone occupy 26,273,471 UTF-8 bytes.
- `evidence/6-1-p1r-successor-enrollment-20261014/history-before.json` is 71,092,957 bytes. Other history/preservation snapshots exceed 47–62 million bytes.
- FrontComposer's Story 13.6 contains 75 split-part files totaling 2,417,378,821 bytes; 15 are exactly 48 MiB. Individual unsplit archives also exceed 50 MiB. This confirms retained parts, without attributing their creation to a particular script in this assessment.
- EventStore `.gitignore` deliberately re-includes evidence logs and selected package archives. `.gitattributes` preserves exact bytes for many sealed packets. New ignore rules alone will not remove already tracked files.
- The locally installed `.agents/skills/pushall/SKILL.md`, per-repository step 3, stages everything using `git add -A`. It has no file-size selection step. It was inspected as the affected artifact; `/pushall` was not invoked.
- FrontComposer already has a `50 * 1024 * 1024` size check for each selected report root in `tests/e2e/scripts/validate-a11y-artifacts.mjs`. It is not a Git admission check and does not bound all retained attempts and archives. The missing control is broader than the absence of every size check.

The user attributes the largest historical changes to EventStore commits `af24499` and `6902748`, and FrontComposer commits `b5f8e8a` and `6ab8d2d`. Those commit-size estimates, the approximately 15 consuming repositories, and the approximately 140 G fleet saving remain user-supplied estimates. This proposal does not require reproducing that historical audit.

### Source documents and existing authority

Planning discovery loaded the top-level PRD, epics, architecture, and UX files in each affected product repository for full-text search, heading inventories, and targeted examination of the affected requirements and stories. There are no matching top-level `*spec-*.md` inputs in EventStore's planning directory. Relevant implementation specs and test consumers were examined separately.

- EventStore PRD §6.3 and §8.1 own repository/release reliability; §0 and Story 9.1 govern corrective-work authorization.
- EventStore architecture AD-11/AD-12 require truthful, bound release and high-risk evidence. They do not authorize a storage migration to change a verdict or approval.
- EventStore's historical P1R specs explicitly retain failed attempts and protect original evidence. For example, `spec-6-1-p1r-tooling-hardening.md` requires preservation of historical evidence and failed attempts. A dated migration amendment must reconcile storage location before protected paths are retired.
- FrontComposer Story 13.6 already requires “one candidate-bound result references the evidence instead of duplicating it into new reports or workflows.” Its AE-01–AE-09 and manual assistive-technology requirements remain the acceptance contract.
- FrontComposer PRD §5.7 requires durable release evidence on the GitHub Release and treats a 30-day Actions artifact as supplemental. Temporary CI retention cannot become the only backing for an enduring acceptance record.

## 2. Impact Analysis

### Epic and story impact

| Area | Impact and proposed owner |
| --- | --- |
| EventStore Epic 3 | Add Stories 3.22 and 3.23 for bounded evidence production/admission and dependency-safe retirement. Existing 3.21 remains runtime/broker qualification. |
| EventStore Epic 6 | P1R tooling and evidence consumers change under the new corrective stories. Preserve the recorded completion of 6.1/6.2 and the current 6.4 work; storage changes do not requalify a runtime. |
| EventStore Epic 4 | OQ8 packets are real test inputs. Exclude them from initial retirement unless their consumers are explicitly migrated and validated. |
| EventStore Epic 5 | Security tests directly read P1R receipts and exact sealed content. Move required regression material to bounded fixtures or a verified materialization path before removing its source files. |
| EventStore Epic 9 | Follow the current corrective-work authorization/bootstrap contract. Story 9.3 owns planning digest reconciliation; changes must not auto-repin approvals. |
| EventStore Epics 1, 2, 7, 8 | No runtime scope change. New evidence runs inherit the storage policy. Existing accepted evidence is assessed by consumer, not deleted by epic number. |
| FrontComposer Epic 13 | Keep 13.6 and 13.7 completion records and all accessibility requirements. Reconcile result links and attempt identities through a storage amendment. |
| FrontComposer Epic 15 | Add Stories 15.6 and 15.7 for external browser artifacts and safe retirement. Coordinate with backlog 15.1's artifact-integrity validator. |
| FrontComposer Epics 16–17 | Future compatibility and publication evidence use the same retention distinction. Preserve release assets, ledger dispositions, and their separate approval gates. |
| FrontComposer Epics 1–12 and 14 | No feature resequencing or reopening. Existing regressions stay applicable; future run output follows the policy. |
| Hexalith.AI.Tools | Own the distributable `/pushall` change and installation propagation. Confirm its authoritative source and supported installations during implementation; do not patch a skill underneath this repository's `references/`. |

No new epic is needed. Prioritize prevention before another large qualification run; sequence removal after consumer migration. Unrelated product work can continue.

### Concrete consumer dependencies

| Consumer | Verified dependency | Required disposition |
| --- | --- | --- |
| `SecretsProtectionTests.cs`, including `SealedPublishedExecutorReceipt_ChangedSourceOrAdjacentCredentialIsScanned` and `HistoricalExecutorSource_RequiresExactSealAndScansAdjacentCredentials` | Direct `File.ReadAllText` on named P1R receipt paths; tests mutate seal/source/credential material | Preserve the same positive/negative coverage using small, explicit fixtures, or materialize exact historical bytes when exact history is the behavior under test. No broad security exemption. |
| `SecretsProtectionTests.EvidenceRecognition.cs` | Contains bounded Python fixture-recognition logic, including a literal `commands.json` read in a recognized test-source block | Update only affected recognized source shapes and their tests; this file alone is not the complete dependency inventory. |
| `Oq8PlatformClosureTests.cs`, `CreateFixture` | Copies `story-4-14`, `story-4-15`, successor v2/v3/v4 directories, and historical selection inputs | Keep those bounded packets or provide verified explicit materialization. Do not remove them because their paths contain `evidence`. |
| `p1r_published_qualification.py`, `p1r_check_witnesses.py`, `p1r_isolation_evidence.py`, related runtime/import code | Parse literal command outputs and validate hashes, witnesses, environment identity, and cleanup | Introduce a versioned output resolver before compacting producer records. A hash or excerpt alone cannot establish a parsed witness. |
| `tools/tests/test_p1r_published_executor.py` | Exercises command outputs and their hashes across producer-shaped positive and mutation fixtures | Retain equivalent success and rejection coverage for the new schema and legacy inputs. |
| FrontComposer `validate-a11y-artifacts.mjs` and `a11y-state-evidence.mjs` | Require actual reports and bind state/screenshot evidence to engine, run, test, and retry | Validate external materialized reports using the same identities and screenshot checks; missing or substituted proof must fail. |

This is an initial dependency assessment. Implementation must enumerate every selected deletion's code, script, workflow, receipt, checksum, documentation, and selector reference, including historical readers. Search hits are not permission to remove a file.

### Artifact conflicts and product scope

- PRDs need repository evidence-storage constraints, without weakening high-risk, release, privacy, accessibility, or manual-check requirements.
- Architecture needs an explicit storage/reference rule alongside existing evidence and assurance rules. Whole-file digest changes may invalidate planning bindings and require the owning reconciliation; old receipts stay bound to their original subjects.
- UX matrices and user-visible behavior need no change. FrontComposer UX governance needs one storage clarification; EventStore's UX handoff needs no edit.
- Tests, producers, report validators, `.gitignore`, relevant `.gitattributes`, CI artifact publication, evidence indexes, and `/pushall` selection are affected.
- MVP functionality remains unchanged. This correction adds maintainability work and does not close any product, release, production, or independent-assurance gate.

## 3. Recommended Approach

### Option assessment

| Option | Effort | Risk | Decision |
| --- | --- | --- | --- |
| Direct adjustment: bounded production, external artifact references, staging guard, then targeted removal | Medium | Medium; highest at exact-byte consumer migration | Recommended. Solves recurrence and reduces current checkouts while retaining evidence integrity. |
| Roll back completed P1R/accessibility implementation | High | High | Reject. Reverting working features does not repair the evidence lifecycle and risks accepted behavior. |
| Reduce MVP or accessibility/high-risk acceptance | Low planning effort, high product cost | High | Reject. No functional requirement has become infeasible. |
| Rewrite Git history or delete repository object storage | High coordination cost | High | Outside scope. Existing history remains reachable. |

### Storage policy proposed for approval

1. Git retains source, acceptance summaries, small receipts/manifests, attempt dispositions, and deliberately bounded regression fixtures. Every attempt has a durable summary with its original outcome; failed and interrupted attempts remain distinguishable from accepted proof.
2. Full command output, history captures, browser reports, traces, videos, run screenshots, and run archives live outside the repository working tree. Producers default to an external artifact root and reject destinations resolving inside the owning checkout. CI paths under a temporary checkout must be explicitly ignored and excluded from staging. Tracked visual baseline fixtures remain source-owned and outside the generated-evidence exclusions.
3. Routine diagnostic artifacts have a proposed 30-day retention. Full bytes required to reproduce an enduring acceptance/approval receive a durable, immutable artifact identity, such as a versioned Release asset or an equivalent owner-managed content-addressed store, before checkout removal. A local temporary directory alone is not an enduring evidence location. Active failures/holds retain required bytes until disposition; expiration never converts unavailable evidence to PASS.
4. A committed reference identifies repository, source/candidate SHA, run/attempt/test/browser as applicable, producer/schema version, artifact locator and immutable identity, exact retained-byte length and SHA-256, retention class, and disposition. Hashes describe the actual byte representation. Existing decoded-output hashes remain meaningful for legacy records; a raw-byte hash must have a separate, explicit field if the representation differs.
5. New diagnostic excerpts are bounded to 2,048 UTF-8 bytes per command. Full command journals may remain external. Committed metadata for an attempt is capped at 10 MiB in aggregate; when a command index exceeds the allowance, commit its totals and immutable artifact reference rather than paging, duplicating, or splitting it to evade the cap. Required witness output is fetched/materialized and verified before evaluation.
6. `/pushall` warns and omits candidate regular-file blobs larger than 10 MiB (10,485,760 bytes). It also warns and omits generated artifact types regardless of size, and limits newly added/modified generated-evidence bytes selected for one auto-commit to 50 MiB (52,428,800 bytes). This aggregate budget prevents thousands of small files from recreating a gigabyte commit. Select complete summary/receipt groups within budget; report omitted groups. No split archives or new packet layouts may evade either budget.
7. Eligible source changes can still be committed when unrelated output is omitted. Exclusions remain on disk and appear in the final report with path, size, reason, and artifact destination if known. Preserve user changes and staged content: use an isolated selection/index strategy where needed, with tests for partial staging. Never silently reset or unstage the user's real index. Required evidence unavailable externally must keep the related acceptance claim unproven, even if unrelated source is eligible.
8. Redaction and existing secret/privacy rules apply before writing excerpts and external artifacts. Moving output outside Git does not permit storing material forbidden by the current evidence policy.

The aggregate and excerpt numbers above are proposed implementation budgets, not pre-existing requirements or measured output sizes. Approval accepts them as defaults; changes to those defaults must remain explicit in the implementation spec.

### Sequence and estimate

| Order | Work package | Rough engineering effort |
| --- | --- | --- |
| 1 | Add `/pushall` selection warnings, aggregate accounting, and generated-type exclusions in its owning source; propagate supported installations | 0.5–1 day |
| 2 | Version EventStore producer/consumer handling; emit bounded summaries and external output; add ignore rules for new runs | 1–2 days |
| 3 | Route FrontComposer run artifacts externally; retain compact result references and adapt validation | 0.5–1 day |
| 4 | Build retirement manifests, migrate bounded fixtures, verify durable retrieval and exact-byte consumers, then remove selected tracked output | 1–3 days across both product repos |
| 5 | Validate clean checkouts and approved root submodule pointer updates; measure actual savings | 0.5 day |

Total estimate: approximately 3.5–7.5 engineering days, excluding artifact upload/download time and owner review. This is an estimate to refine during implementation, not a delivery commitment. The main uncertainty is how many sealed consumers need migration. There is no requirement to rerun the entire expensive P1R or browser qualification merely to change storage.

Removing tracked evidence from the current branch reduces working-tree content after consumers update their root-declared submodule pointers. It does not shrink retained Git history, `.git/modules` object databases, or old refs. Actual fleet savings require measuring which checkouts update and which bytes remain as small fixtures. The user's approximately 140 G is a potential working-tree saving, not a guaranteed total-disk recovery.

## 4. Detailed Change Proposals

Administrator approved these edits with the reply “approve” on 2026-10-21. The PRD, architecture, epic/story, tracker and FrontComposer UX planning edits are applied. Producer/validator code, shared `/pushall` distribution, artifact publication and exact-path retirement remain implementation work in the named backlog stories. The OLD/NEW text below records the approved change against the examined baseline.

### P1. PRD repository constraints

EventStore: append to PRD §8.1, Repository And Build. FrontComposer: append an equivalent repository constraint in PRD §7 and link it from §5.7 without changing the release ledger or durable release-evidence requirements.

**OLD:** No repository constraint bounds generated qualification output or specifies how to retire a hash-bound file from the checkout.

NEW, proposed text:

> Generated qualification evidence retains one bounded candidate/run/attempt summary and content-bound references in Git. Full output and generated reports, traces, videos, screenshots, history captures, and archives use external artifact storage. Acceptance-relevant bytes remain durably retrievable and verified; missing or mismatched proof fails its consuming gate. New attempt metadata totals at most 10 MiB, command excerpts at most 2,048 UTF-8 bytes, and automatic Git admission omits blobs over 10 MiB and generated-evidence selections over 50 MiB with an explicit warning. Archive splitting cannot bypass these limits. Existing bound evidence is retired only through an enumerated consumer migration preserving historical identities and dispositions.

Rationale: make repository cost bounded while retaining exact proof, source-owned baselines, and existing product requirements. Add these work packages to the PRD's follow-on ownership mapping; do not invent a new functional feature or renumber existing requirements.

### P2. Architecture evidence storage rule

EventStore: extend the evidence-lifecycle guidance under AD-12 and Consistency Conventions. FrontComposer: extend its evidence guidance under FR-24 Release Evidence Architecture, preserving its publication authority boundary.

**OLD, EventStore AD-12:** “High-Risk Verification Requires Persisted Evidence.” Existing exact-subject rules describe validation and assurance without a bounded checkout-storage contract.

**OLD, FrontComposer architecture:** “Run artifacts are short-retention replay material” within the publication architecture; accepted evidence and replay material need an explicit storage distinction for browser qualification too.

NEW, proposed text:

> Persisted proof may be stored outside the source checkout. A versioned manifest binds immutable artifact identity, exact retained bytes, source/producer identity, attempt disposition, and retention. A consumer that needs literal output must materialize and verify it before evaluation; a hash or excerpt alone cannot satisfy a content-dependent witness. Unit regressions use bounded fixtures; historical exact-byte verification uses an explicit materialization lane. Storage relocation preserves original subjects and approvals. New manifests describe the migration and never rewrite sealed receipts or confer release/readiness authority.

Proposed data flow: producer → external artifact → hash-verified materializer → existing verifier; Git carries the summary, reference, and outcome. Update only relevant evidence diagrams, if present. EventStore's production topology and FrontComposer's UI flows do not change.

Rationale: separate artifact durability from checkout location and prevent unchecked references from becoming proof. Route digest/approval renewal through the existing owner process, including EventStore Story 9.3. No automatic seal or retrospective approval.

### P3. EventStore Story 3.22 — Bounded Qualification Evidence Production and Admission

**OLD:** No Story 3.22 exists in the examined epic plan. `Executor.record` places complete decoded output in each row and `save` repeatedly writes `commands.json`; several consumers parse those literal values.

**NEW:** Add Story 3.22 under Epic 3 and a matching backlog entry, `3-22-bounded-qualification-evidence-production-and-admission`.

As a maintainer, I want qualification runs to retain bounded summaries and verified external output, so that repeatable proof does not make every source checkout grow with raw run volume.

Acceptance criteria:

1. A versioned producer/reader contract retains command identity, status, timing, output length/hash, representation, bounded excerpt, and artifact reference. Full output is external and written once per artifact identity. Large history inventories are external; committed preservation records use bounded inventories/digests, not repeated full captures.
2. All content-dependent witness/import/cleanup checks resolve and verify complete output through a common bounded reader before parsing. Legacy packet readers remain available for original sealed inputs. Missing, corrupt, truncated, wrong-size, wrong-attempt, substituted, or unavailable output cannot pass. Tests use small fixtures and exercise those cases.
3. New-run `.gitignore` exclusions follow the existing log/package negations so the final matching rule wins. They cover `_bmad-output/**/evidence/` archive types (`*.zip`, `*.gz`, `*.part*`), videos (`*.webm`), `trace/` and `traces/`, `commands.json`, and generated report/result trees. Run screenshots are excluded by generated-output directory; source-owned snapshot baselines remain tracked. Do not broadly ignore all JSON or all evidence.
4. A representative 5,116-command synthetic run proves the committed metadata budget and excerpt cap without live Docker/.NET probes; output volume does not create additional retained copies. Independently exercise at least one real producer/reader round trip with external output.
5. Security recognition and secret-scan regressions preserve their rejection behavior. A schema change cannot bypass source seals or adjacent-credential scanning.
6. The final result records exact verification commands and bounded outcomes. No new P1R usability, package/runtime qualification, or story-completion authority is inferred.

Dependencies: approved storage amendment; current corrective-work authorization; coordinated `/pushall` rollout. Primary paths: `tools/p1r_published_executor.py`, its qualification/witness/runtime/isolation readers and focused tests, `.gitignore`, and concise evidence guidance.

### P4. FrontComposer Story 15.6 — External Browser Artifacts and Bounded Accessibility Receipts

**OLD, Story 13.6:** “one candidate-bound result references the evidence instead of duplicating it into new reports or workflows.” Current output selection permits retained report/archive copies under story evidence.

**NEW:** Preserve Story 13.6 and its completion record; append a dated storage amendment linking new Story 15.6 under Epic 15. Add backlog entry `15-6-external-browser-artifacts-and-bounded-accessibility-receipts`.

As a framework maintainer, I want browser proof stored once outside Git with a bounded candidate-bound receipt, so that accessibility verification remains reviewable across retries without retaining every report copy in the checkout.

Acceptance criteria:

1. Configure existing output variables (`FC_E2E_OUTPUT_DIR`, `FC_E2E_HTML_REPORT_DIR`, `FC_E2E_JSON_PATH`, `FC_E2E_JUNIT_PATH`) from one external run root. Keep source-owned `specimen-accessibility.spec.ts-snapshots` fixtures tracked. Reuse existing test runners and validators.
2. Validate the actual HTML/JUnit/JSON/state/screenshot evidence from the materialized artifact. Preserve browser/run/test/retry binding, PNG checks, and final-successful-attempt selection. An earlier failed retry, different engine, or altered screenshot never establishes the accepted attempt.
3. Commit one bounded summary/receipt per attempt and one selected-result reference. Record failed/interrupted dispositions without copying their full reports. Publish retained full output with explicit retention before claiming durable evidence.
4. Remove any automation that splits run archives to meet a Git per-file limit. Check sizes before Git staging and apply matching generated-evidence ignore patterns after re-inclusions. The existing report-root check may remain as a producer diagnostic; it does not substitute for aggregate admission.
5. AE-01–AE-09, required manual assistive-technology/device evidence, opt-in UX review disposition, and separately owned approval gates keep their exact acceptance semantics.

**UX governance OLD:** Visual/accessibility changes require evidence mapped to the canonical matrices.

UX governance NEW, append after that rule:

> Matrix evidence uses one candidate-bound result and verified external artifact references. Storage location does not change AE-01–AE-09, manual evidence, or approval requirements. Committed visual baseline fixtures remain source; generated run screenshots and reports are artifacts.

Rationale: implement the existing reference-without-duplication rule at the output boundary. No wireframe, navigation, interaction, or accessibility threshold changes.

### P5. Shared `/pushall` preflight and file selection

**OLD:** Per-repository step 3 treats a dirty worktree as a reason to stage everything with `git add -A` and then auto-commit. There is no size-aware file selection or omitted-output report.

**NEW:** Replace that staging selection in the authoritative Hexalith.AI.Tools source and propagate supported installations:

1. Enumerate candidate changes with NUL-safe paths; evaluate the blob that would actually be committed, including already staged files and partially staged paths. Deletions remain eligible regardless of the removed file's size. Gitlink pointers are not regular-file blobs and must not trigger submodule-content traversal.
2. Exclude generated artifact types and blobs over 10 MiB. Bound newly selected generated evidence to 50 MiB per automatic commit, considering complete attempt summary/receipt groups. Warn with size, reason, and retained local path. Never create `.part*` files or use force-add to defeat the policy.
3. Stage/commit the eligible selection without losing or silently unstaging excluded user changes. Demonstrate the behavior with untracked, tracked-modified, pre-staged, partially staged, binary, newline-containing-path, deletion, exact-boundary, and many-small-file cases. If safe branch switching/merging is prevented by retained changes, report those operations as skipped under existing preservation rules.
4. Report source commit/push outcomes separately from omitted evidence. Never report a fully clean/synchronized repository when exclusions remain. Preserve existing exact commitlint preflight and root-declared-submodule boundaries.

Rationale: let useful eligible work proceed while making large accidental additions visible. This is a proposed skill change, not permission to execute `/pushall` during this course-correction session.

### P6. Targeted retirement from the current tree

**OLD:** Existing tracked bulky evidence, including duplicates, remains part of every updated checkout. Receipts, source-inspection tests, selectors, and frozen specs can depend on those bytes and paths.

**NEW:** Add EventStore Story 3.23, `3-23-retire-bulky-qualification-evidence`, and FrontComposer Story 15.7, `15-7-retire-bulky-browser-evidence`, in their existing epics. Retirement follows 3.22/15.6 respectively.

For each owning repository:

1. Create a bounded retirement manifest listing the exact paths, sizes, original hashes and commit identities, consuming readers, retained small fixtures, external artifact identities, and reason. Group repeated paths by shared artifact identity while preserving logical attempt identities and original outcomes. Keep a compact one-to-one path mapping for removed paths; externalize an oversized detailed inventory behind its own verified reference.
2. Amend preservation/frozen storage obligations through the dated owner process. Preserve original receipts and subjects; append a migration record. Do not regenerate or repin historical evidence to make it appear unchanged.
3. Upload or identify durable acceptance-relevant bytes and verify retrieval from a clean environment. Historical Git objects may be the explicitly selected source for legacy checks only when availability and verification are demonstrated; a shallow/default checkout cannot silently assume those objects exist.
4. Move content needed for ordinary unit tests into bounded offline fixtures with the same positive and hostile cases. Exact-historical-byte tests use explicit verified materialization. OQ8 directories remain outside the initial removal set unless this work proves their migration safe.
5. Remove the enumerated tracked output using `git rm` in its owning repository after dependent checks pass. Preserve any needed local diagnostic copy outside the checkout first. Do not run a blanket evidence deletion, history rewrite, object-store cleanup, or nested submodule update.
6. Re-run the affected tests and evidence validators on the post-removal candidate in a clean checkout using the approved external retrieval path. Report pre/post tracked bytes, remaining oversized files, removed duplicate checkout bytes, and unresolved consumers. Update owner-approved root-declared submodule pointers in a later owning-repository change; never sweep other workspaces recursively.

Rationale: removing current-tree content delivers the requested checkout reduction while protecting active validators. Historical Git storage stays unchanged. Any old “option A” workspace-copy workaround is replaced by this plan; no separate workspace deletion operation is inferred from that name.

## 5. Implementation Handoff and Success Criteria

### Responsibilities

| Recipient | Responsibility | Deliverable |
| --- | --- | --- |
| Product owner / backlog maintainer | Approve scope and budgets; add 3.22/3.23 and 15.6/15.7; sequence prevention before removal | Approved backlog/spec amendments and tracker entries |
| EventStore developer | Implement producer/reader schema and bounded fixtures; inventory consumers; migrate and remove selected output | 3.22 and 3.23 with focused verification |
| FrontComposer developer / QA owner | Externalize browser output and keep exact accessibility proof binding; migrate result references; retire selected runs | 15.6 and 15.7 with browser-artifact validation |
| Hexalith.AI.Tools maintainer | Change authoritative `/pushall` selection and propagate installed variants | Tested, distributed staging guard |
| Architecture/security and evidence owners | Review changes to sealed consumers, retention, and historical storage obligations | Dated migration decisions and truthful gate dispositions |
| Consuming repository maintainers | Update only their own root-declared EventStore/FrontComposer pointers after owning changes land | Measured working-tree reduction per updated checkout |

These are handoff roles, not messages sent or agents launched. The approved backlog and this handoff route the work to the Product owner and developers; no external message, code implementation or artifact publication has occurred in this workflow.

### Required validation during implementation

- Run the focused P1R Python suites affected by the producer/reader change, including `python3 -m unittest discover -s tools/tests -p 'test_p1r_published_executor.py'`; exercise required verifier rejection behavior under optimized Python where the existing specs require it.
- Build and run the affected Server security and Contracts packaging test classes by their individual test projects. Follow the repository's Microsoft.Testing.Platform/xUnit direct-assembly filtering guidance; do not substitute a solution-level test invocation. Re-run OQ8 validation if any of its bound paths change.
- In FrontComposer's `tests/e2e`, run `node --test scripts/a11y-state-evidence.test.mjs` and `npm run validate:a11y-artifacts` against the materialized proof. Verify the external-output path with a focused existing accessibility lane. Existing proof bytes need not be recaptured unless behavior or the proof contract changes.
- Test `/pushall` selection in disposable repositories, without pushing to real remotes, including pre-staged exclusions and aggregate-budget cases.
- Verify final Git selection and ignore behavior, retrieval hash/size failures, unavailable/expired artifacts, and clean-checkout consumer behavior before deletion is considered complete.

These are implementation requirements, not claims of code-test execution during this planning workflow.

### Acceptance of the correction

1. A repeat qualification produces no tracked full command log, full history capture, generated browser report/trace/video/archive, or split archive part under the new evidence policy.
2. Every new attempt keeps a bounded, candidate-bound summary and its actual disposition. Accepted proof resolves to complete verified bytes; unavailable proof remains unproven.
3. `/pushall` warns and omits oversized/artifact files while committing eligible changes, preserves excluded content/index state, and catches a large aggregate of small evidence files.
4. Security, OQ8, witness, and browser identity/screenshot checks retain equivalent rejection coverage after selected evidence is removed. Ordinary unit regressions remain runnable offline from bounded fixtures.
5. Selected bulky tracked output is absent from current checkout trees, quantified by exact pre/post bytes and remaining exceptions. There is no promised reduction in Git object history.
6. Completion records and independent approval gates retain their existing meanings. No product readiness, publication, promotion, consumer removal, or past qualification result is inferred from storage cleanup.

### Review and approval state

The user supplied the trigger, selected Batch mode, and explicitly approved the complete proposal with “approve” on 2026-10-21. Approval includes the documented budgets, four work packages, shared `/pushall` selection change and dependency-safe retirement plan. This is conversation approval of scope, not a cryptographic seal, a historical evidence amendment by itself, or independent Product/Release/UX acceptance.

Planning application is complete in both owning repositories. EventStore Stories 3.22/3.23 and FrontComposer Stories 15.6/15.7 are defined in their epic plans and recorded as `backlog`; existing statuses are preserved. PRD and architecture storage constraints are applied, and FrontComposer UX governance and Story 13.6 carry the dated storage clarification. The FrontComposer-local [approved amendment](../../references/Hexalith.FrontComposer/_bmad-output/planning-artifacts/sprint-change-proposal-2026-10-21.md) records its implementation handoff without copying this entire proposal.

The Product owner/developer handoff is the ordered work plan in §3 and role/validation assignments in §5. Begin the shared Hexalith.AI.Tools admission guard and prevention Stories 3.22/15.6 before retirement Stories 3.23/15.7. Hexalith.AI.Tools source discovery/distribution is an explicit implementation deliverable in P5; no skill under `references/` is modified or invoked. Exact retirement manifests, frozen-subject amendments, durable artifact retrieval, bounded fixtures and post-removal checks must pass before the enumerated removal occurs.

Code, skills, evidence bytes, existing receipt/approval identities, Git indexes, HEADs and submodule pointers remain unchanged by this planning application. Original planning digests in Appendix B identify the reviewed inputs; existing sealed bindings and `epics.md` input digests are not automatically repinned. EventStore Story 9.3 retains baseline-reconciliation ownership. No additional proposal approval is required to start the approved implementation work packages within their stated constraints.

## Appendix A. Change Navigation Checklist

`[x]` means analysis/proposal complete; `[N/A]` means inapplicable; `[!]` means pending approval or implementation and is not claimed complete.

| Item | Status | Finding or next action |
| --- | --- | --- |
| 1.1 Triggering story | [x] | EventStore 6.1 P1R and FrontComposer 13.6. |
| 1.2 Core problem | [x] | Failed storage approach and missing Git admission safeguards. |
| 1.3 Supporting evidence | [x] | Local tracked-byte inventory, producer behavior, duplicate blobs, split parts, direct readers. |
| 2.1 Current epics | [x] | Feature acceptance remains viable; storage follow-ups use Epics 3 and 15. |
| 2.2 Epic changes | [x] | Four focused stories proposed; no new or deleted epic. |
| 2.3 Remaining epics | [x] | Cross-cutting policy for new evidence; no unrelated runtime scope changes. |
| 2.4 Invalidated epics | [N/A] | None invalidated. |
| 2.5 Order/priority | [x] | Prevention first, materialization/fixtures next, removal last. |
| 3.1 PRD conflicts | [x] | Add storage constraints without reducing MVP or proof requirements. |
| 3.2 Architecture conflicts | [x] | Specify verified external proof and preserve original approval subjects. |
| 3.3 UX conflicts | [x] | FrontComposer storage clarification only; existing matrices unchanged. |
| 3.4 Secondary artifacts | [x] | Producers, validators, security fixtures, OQ8, ignores/attributes, CI, skill installation. |
| 4.1 Direct adjustment | [x] | Viable; medium effort/risk. |
| 4.2 Feature rollback | [x] | Evaluated and rejected; high effort/risk. |
| 4.3 MVP review | [N/A] | No MVP reduction needed. |
| 4.4 Recommended path | [x] | Direct adjustment plus staged current-tree retirement. |
| 5.1 Issue summary | [x] | Section 1. |
| 5.2 Artifact impact | [x] | Section 2. |
| 5.3 Recommendation | [x] | Section 3. |
| 5.4 MVP/action plan | [x] | Requirements preserved; ordered work and estimates recorded. |
| 5.5 Handoff plan | [x] | Section 5 names roles, deliverables, and validation. |
| 6.1 Checklist review | [x] | Pending work is explicitly separated from completed analysis. |
| 6.2 Proposal accuracy | [x] | Local measurements separated from reported estimates; consumer constraints recorded. |
| 6.3 User approval | [x] | Administrator replied “approve” on 2026-10-21; complete proposal and budgets approved. |
| 6.4 Sprint status | [x] | Four new backlog rows applied in the owning repositories; existing status values preserved. |
| 6.5 Handoff confirmation | [x] | Approved work routed through the updated backlog and §5 PO/developer responsibilities; no external messages sent. |

## Appendix B. Planning Input Identities

Paths are relative to the owning repository's `_bmad-output/planning-artifacts/`. Digests identify the examined input bytes, not new approval seals.

| Repository | Artifact | SHA-256 |
| --- | --- | --- |
| EventStore | `prd.md` | `d0440bf973e4accd1fea80bbf0ef22ac392338ca29b39ada0cbe7708f4fa0efb` |
| EventStore | `epics.md` | `87c047e0a81866394d06f1c33476a54e1521c622f47d28dceb67bdd319f22468` |
| EventStore | `architecture.md` | `7ad1e596089d92334ff7b512bfe52cb485319d87698544c7f1be3911177b9980` |
| EventStore | `ux.md` | `a135c9690bbd1dd157f00fdb389e7a5c8bec691e32826c5fdb12b97d07da95c1` |
| FrontComposer | `prd.md` | `b547fc0e1b5d6141018bbd8dbbfd4f66b8c81a738837ba75e743e61c0f3ec224` |
| FrontComposer | `epics.md` | `c9afdfacb337c4843ef869c37ddea50a3e476abd458ee4aacbc6b1686cb17815` |
| FrontComposer | `architecture.md` | `c8e58230526894e0bce5df46c6e4726892d617eaa766d40311ad524ff9b78d9a` |
| FrontComposer | `ux-design.md` | `6e5f008ab5c90b5afbbbfcee4a973a46017aa68ac625a3617c6b6479c88848e3` |

## Appendix C. Workflow Execution Record

- Loaded the allowed local `bmad-correct-course` skill, its resolved workflow/config, checklist, and repository baseline. Activation has no prepend/append steps or persistent facts.
- Confirmed the provided trigger and the user's Batch preference; inspected clean repository state and the affected planning/implementation sources.
- Completed impact and option assessment, drafted explicit before/after edits, and prepared the moderate-scope implementation handoff.
- Proposal validation passed: required sections, all 26 checklist entries, unused proposed story IDs, all eight recorded input digests, LF endings, final newline, and trailing-whitespace checks. `git diff --check` returned 0 in both product repositories. Only this new proposal appears in EventStore status; FrontComposer remains clean. No implementation tests were run for this documentation-only change.
- Administrator approved the complete proposal with “approve” on 2026-10-21; no second confirmation was requested.
- Applied the approved PRD/architecture constraints, four epic stories and backlog entries, and FrontComposer UX/Story 13.6 storage amendment in their owning repositories. Recorded the FrontComposer handoff in its local approved amendment.
- Application validation passed: both sprint YAML files parse without duplicate keys; exactly two new `backlog` stories were added per repository; all pre-existing tracker values, EventStore `inputDocumentDigests`, and FrontComposer Product/G-4/OI-16/OI-19 states are unchanged. New local Markdown links resolve, native LF/CRLF endings are preserved, and `git diff --check` returns 0 in both repositories. Git indexes and HEADs match the pre-application snapshots; only the eleven approved planning/tracking files changed. No code tests were needed for this planning-only application.
- Correct Course planning and handoff are complete. The implementation work remains in Stories 3.22/3.23 and 15.6/15.7 and shared-tooling proposal P5; no code, evidence, Git history or submodule pointer changed.
