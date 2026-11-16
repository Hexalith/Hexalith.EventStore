# Epic 9 Context: Phase 4 Gate Decisions Are Machine-Enforced And Approved At A Declared Assurance Level

<!-- Compiled from planning artifacts. Edit freely. Regenerate with compile-epic-context if planning docs change. -->

## Goal

Make Phase 4 gate decisions executable rather than asserted. Every mandatory gate gets classified, every high-risk result needs sealed CI validation plus approval at the assurance level the owner-role registry requires, and that level is labelled on the result and on everything downstream. Baseline drift, clause closure and MVP coverage are computed from current content-bound inputs. This is MVP governance work, independent of the post-MVP payload-protection epic. No Epic 9 story grants readiness, release, deployment, migration or consumer-removal authority. The governed gates stay truthfully FAIL/BLOCKED until real evidence passes.

## Stories

- Story 9.1: Corrective-Work Authorization Record And Validator (closed `done`, superseded)
- Story 9.2: Phase 4 High-Risk Gate Matrix And Assurance Control
- Story 9.3: Planning Baseline Manifest And Drift Guard
- Story 9.4: Stable Clause Ledger And All-Clauses Validator
- Story 9.5: Phase 4 MVP Coverage Manifest And Validator

## Requirements & Constraints

- **Gate matrix.** The matrix classifies exactly 17 mandatory gates: G-BASELINE, G-OQ8, G-TENANT, G-STATUS-ID, G-APPEND, G-CLAUSE, G-NFR8, G-COMPAT, G-AUTH-HOSTS, G-RUNTIME-PARITY, G-PUBLICATION-AUTH, G-CONSUMER, G-HIGH-RISK, G-NFR18, G-NFR-OWNERSHIP, G-MVP-COVERAGE and G-READINESS.
  - Each gate is `high-risk` or a reason-coded `standard-control`. A standard classification is neither an omission nor a waiver.
  - The validator rejects omissions, unknown gates, new unclassified gates and classifications without a reason.
  - A high-risk entry binds its command, trigger, subject and evidence identities, pass condition, author and evaluator roles, required and achieved assurance level, and guarded transition.
- **Assurance level.** The required level comes from the owner-role registry: `single-maintainer-attested` while it names exactly one human, `independent` once it names two or more.
  - Tool personas (for example `bmad:*`) and CI identities never count as humans. A tool-persona review is evidence only, never an approval identity.
  - `single-maintainer-attested` needs three things: sealed CI validation; an authenticated owner attestation bound to the subject digest and made at least 24 h after the authenticated latest subject-changing push to `main` (missing history blocks verification); and the label on the result.
  - `independent` also needs an authenticated human approver who is not the author.
- **Solo maintainer.** The owner is currently the project's only person. Solo work is never labelled `independent`.
- **Rejected labels.** Validators reject a level that exceeds its evidence (`independent` with one human, self-approval, or one account in two roles), a level below the required one, an attestation inside the 24 h window, and a missing seal.
- **Downstream records.** `READY`, `release-available`, `production-promoted` and consumer-removal records carry the lowest assurance level among their inputs.
- **Guarded transitions (six).** A high-risk gate result to PASS, a story against a high-risk NFR to `done`, readiness, `release-available`, `production-promoted`, and consumer removal.
- **Truthful FAIL and fixtures.** Validators report today's failures; nothing mints, refreshes or approves a digest to pass. Every rejection path has a negative fixture observed failing beside a positive control. Inputs are content-hashed, so edits invalidate results.
- **Baseline reconciliation (9.3).** Baseline approval requires substantive reconciliation of requirements, ownership, statuses and lifecycle values across the PRD, architecture, detailed and top-level UX, epics, tracker and story records. Matching hashes alone never establish reconciliation or approval; a hash-only refresh is not reconciliation.
- **Stable clauses (9.4).** Every stable clause has exactly one primary slice and an evidence identity, pending until passing evidence exists. A parent requirement closes only when all its clauses have passing content-bound evidence; story status cannot substitute.
- **MVP coverage (9.5).** Coverage is total over FR1–FR36, NFR1–NFR18, every stable clause, and the `interactive_ui_hosts`, `nfr1_surfaces` and `high_tier_evidence_cases` inventories. Each entry binds owner, lifecycle, evidence, validator and result, approval, and blocking gate.
  - Omissions, duplicate ownership, unsupported `done`, stale, failed or unapproved evidence, incomplete denominators and MVP `N/A` fail.
  - FR37 and NFR19 do not count toward MVP.

## Technical Decisions

- **CI shape.** Each gate validator has two jobs:
  - A required fixture job that blocks merges.
  - A non-required live-evaluation job that runs on every push to `main`. It publishes a retrievable, content-bound PASS or FAIL (run output goes to workflow artifacts) and does not block merges. Its result is evidence, never a seal.
- **Workflow files.** Use a new workflow file; the Story 9.1 workflow no longer exists. Do not edit `.github/workflows/ci.yml` or `docs/ci.md` unless a Story 4.15 reseal is planned. The required build-and-test job is the shared Hexalith.Builds reusable workflow and accepts no caller-defined steps.
- **Seals.** A seal is the validator result that the guarded transition's validator retrieves from the CI platform. It must come from a required run of that transition's dedicated workflow, on the exact head SHA and workflow-file digest, under the platform's authenticated CI identity.
  - An author-supplied file is never a seal.
  - "Required" means the guarded transition's validator enforces it. It is never a branch-protection or ruleset check on `main`, because a bypass push skips those.
  - A missing or failing seal blocks only its own transition, never `main`.
  - Older text saying Story 9.2 builds one blocking required check on `main` is superseded.
- **Owner records.** Attestations and ratifications, including AD-26, never seal themselves. Each record binds its required level, subject digest and authenticated attestation evidence. The sealed run of the transition that consumes it checks the level and the 24 h window, then computes and labels the achieved level.
- **Baseline guards (9.3).** Guards compare content and lifecycle semantics as well as SHA-256 values. Architecture repinning propagates newly applicable AD IDs into affected story constraints. AD-26 ratification has its own subject digest, separate from the whole-file traceability digest. Open-assumption checks read actual decision and clause tags, not quoted mentions.
- **Predecessor results.** A predecessor result (for example `evidence-validated`) is re-run inside the consuming sealed run, using the validator retrieved from the pinned commit that its record binds. A mismatch voids the predecessor.
- **Evidence storage.** Proof is the repository's tests plus CI.
  - A story or spec records commands, pass/fail counts, commit SHA and CI run link; no committed evidence packet, manifest or external store exists.
  - `_bmad-output/**/evidence/` and `docs/evidence/` are git-ignored scratch. `scripts/check-committed-files.sh` rejects paths there, and files over 1 MiB, in hooks and CI.
  - An AC that names a committed record under those folders, or a deleted tool, is satisfied by the story/spec Verification section plus its CI run. The owning story re-specifies a deleted validator when it is scheduled.
  - Do not recreate deleted evidence machinery. The seal and assurance rules themselves are unchanged.

## UX & Interaction Patterns

There is no Epic 9 UI. Any surface that displays a supplied gate or authority result keeps its required and achieved assurance labels exactly as given, and is read-only.

## Cross-Story Dependencies

- **Story 9.1** is closed `done` as superseded. Its validator, schema, workflow, tests and fixtures were deleted. A future corrective-work exception is re-specified by the story that owns it.
- **Story 9.3** has an approved 2026-11-09 execution clarification. It runs under the story/spec-plus-CI policy, records its authorization and approval provenance in its own story/spec, does not need deleted 9.1 tooling, and must not restore deleted manifests, registries, seals or workflows.
- **Story 9.2** has an approved 2026-11-15 execution clarification that mirrors 9.3's: the owner's `/bmad-build 9.2` request authorizes it, recorded in its spec. It runs under the same story/spec-plus-CI policy and does not need deleted 9.1 tooling.
  - The matrix and the owner-role registry are tracked at `_bmad-output/implementation-artifacts/phase-4-gate-risk-matrix.json` and `_bmad-output/implementation-artifacts/owner-role-registry.json`, outside the ignored evidence folder. The earlier owner-role registry lived in the removed evidence tree.
  - Guarded-transition implementation is delivered by `spec-9-2-guarded-transition-assurance-control.md`: closed contract, shared stdlib `evaluate`/`consume` verifier, six dedicated dispatch workflows and a separate fixture check. Candidate evaluation never seals itself; consumption authenticates a completed successful exact-input run and rechecks approvals and predecessors.
  - Owner-written issue/PR conversation comments are the only approval transport: numeric registry identity, exact JSON/body digest, required role/level/subject and unedited platform timestamps. Generate copyable payloads; the owner posts them. The 24-hour window uses authenticated main push activity throughout; AD-26 preserves its exact ratification subject.
  - New story-completion claims against NFR1-NFR4, NFR7, NFR10-NFR12 and NFR14-NFR17 are guarded. Historical labels stay historical; historical evidence is revalidated whenever a new transition consumes it.
  - The concrete OQ8 integration runs its existing validator and genuine rejection fixtures, with synthetic platform approvals. Only the assurance subcondition is demonstrated. The mandatory G-OQ8 availability decision consumer is now adopted in OQ8 manual `assured-decision` and dedicated gate-pass consume mode (`spec-9-2-mandatory-oq8-gate-consumer.md`), binding its module/CLI and decision contract and requiring fresh platform consumption on every invocation. Ordinary availability remains diagnostic; a saved decision is never reusable authority. Other actual consumers, absent Stories 3.19/3.20 lifecycle consumers and missing business adapters keep Story 9.2 incomplete; G-RUNTIME-PARITY (AC 9) stays follow-on work. No required-check settings are changed.
  - The mandatory high-risk NFR completion boundary is delivered by `spec-9-2-mandatory-high-risk-nfr-completion-guard.md`. Persistent BMAD customization routes newly written spec `done` and tracker `done` through the guarded atomic writer; ordinary tracker synchronization remains `review`. Canonical claims bind story/spec/target identity, PRD primary/supporting and range coverage, epics declarations, tracker, shared controls and workflow. Omitted caller NFRs cannot narrow derived coverage; identity relabelling, conflicting mappings and source drift refuse. Exact historical labels remain historical only; new consumption authenticates the completed run and reruns approvals/predecessors. Genuine story-owned NFR adapters remain unavailable, so no governed completion can PASS and OQ8 evidence cannot substitute. This bounded slice does not close parent Story 9.2 or an NFR; runtime parity, readiness and Stories 3.19/3.20 remain separately owned. Local fixtures are not live approval or delivery CI.
- **No equivalent clarification exists for 9.4 or 9.5.** Their text still requires "Story 9.1 passed" plus a 9.1 authorization record, names manifest paths under the ignored evidence folder, and offers the deleted 9.1 workflow as an option. Only the blanket evidence-policy rule covers them.
  - Each story spec must settle where its manifest lives, and how the 9.1 dependency is met, with the owner before it freezes.
- **Baseline approval (9.3)** waits on its prerequisites; building the baseline guards does not. Minting an approved baseline requires architecture reviewer closure, AD-26 ratification, resolved UX assumptions and final statuses, and renewed epics. Current top-level UX finality does not reconcile upstream source conflicts or stale epics bindings.
- **Story 9.5** depends on Story 9.2's Assurance Control and Story 9.4's clause inventory.
- **Consumers.** Stories 2.14, 2.15, 3.18, 3.19, 5.11 and 4.16's final gate approve at the computed level; post-MVP 8.11 reuses the control.
- **Story 3.15** is `done` for FR36-C2 only, and both of its owner roles map to one account. It reopens only if a G-HIGH-RISK evaluation rejects its evidence. A new receipt set follows the G-RUNTIME-PARITY re-mint rule.

### Bounded Story 5.3 completion-adapter slice (2026-11-16)

`spec-9-2-story-5-3-nfr-completion-adapter.md` adds the fixed story-owned NFR3/NFR4 adapter, complete source/runtime/dependency binding, genuine bounded validator, whole-root authenticated review subject, closed report schema and both guarded completion targets. Fresh shared evaluate/consume retains registry assurance, authenticated owner comments and completed Actions runs, 24-hour separation and pinned predecessor reruns. Unsupported stories or missing/incomplete evidence refuse. Local controls pass (367 tests plus 18 explicit schema cases); genuine host/build/package/provider/script evidence succeeds, but full Story 5.3 consumption remains refused for six scanner-format gaps, three existing scan findings and incomplete integration evidence. The handoff records commands and exact identities; no live approval, seal or CI delivery is claimed. Historical labels remain historical and Story 9.2 stays `in-progress`. No all-host NFR3, whole-NFR, readiness or parent closure follows. VG2 requires genuine concurrent-source guarded-write coverage before any complete positive consumption is claimed.

### Bounded Story 5.3 scanner evidence repair (2026-11-16)

`spec-9-2-story-5-3-scanner-evidence-repair.md` repairs the scanner-format and generated-fixture prerequisites described above. The unchanged seven-capability probe detects all seven controls; the complete scanner class passes 299/299, diagnostics 25/25 and Phase 4 controls 258/258. Independent review findings were reproduced and corrected, including typed YAML, constructor tokenization, XML text boundaries and complete runtime token-acquisition expressions. These are local working-source checks; the pre-edit Aspire baseline was missed and no delivery CI, live approval or seal is claimed. Authentication/Keycloak integration within fixed bounds, VG2, other adapters/consumers and live assurance remain open. Historical labels are unchanged and parent Story 9.2 remains `in-progress`; scanner repair does not establish whole-NFR or parent completion.

### Bounded private authentication integration repair (2026-11-16)

`spec-9-2-story-5-3-private-authentication-integration.md` records the completed bounded slice. One private gateway/Counter/real-Keycloak cohort replaces duplicate fixtures and shared Redis; private TLS trust, checksum-pinned Development Dapr control-plane trust, SQLite discovery, production writer activation and protected metadata readiness remain enforced. Negative signed-token controls and actual persisted tenant-event separation run with the unchanged four selectors, 180-second lane and 1 MiB output bound. Exact-owner cleanup covers the lane and its bounded parent; a real forced-timeout probe removed its owned container and found none remaining. A concurrent production-profile commit required an owner-approved two-getter CA1822 repair that preserves both `unevaluated` values.

Final post-review local diagnostic `full-post-review-1` passes all 21 fixed adapter lanes: 689 .NET cases, 56 script cases and all seven scanner probes, with no gaps/failures/skips/errors/not-run. Authentication is 29/29 in 91.215 seconds, 175,408 bytes; fresh Debug authentication also passes 29/29. Broader Phase 4 controls pass 264/264, ownership/TLS/cutover controls 27/27 and additional stopped-extraction controls 5/5. Three independent reviews produced 19 findings; eight grouped internal corrections are complete, and two concurrent production-profile findings are deferred. Cleanup now includes stopped SDK extraction containers, removes only verified immutable identities, attempts remaining members after failures, and preserves failed handles for retry. An actual SDK interruption during extraction left zero owned containers or matching job processes after cleanup; private custody stays beneath wrapper temporary space outside checkout ancestry. Signed reserved-grant evidence contains both tenant-a and system, while persisted isolation binds tenant/aggregate/causation IDs. Input inventory SHA-256 is `203eb56142de351f2a4392d13376e753af68eb8b31e8697b15ce2ca827bcf44c`; the approved baseline remains `8b6d5bee5217b3f4bbd8fd689ad887aca863961b`, while the concurrent HEAD is `f85419d501f279e9a813435aa73538185ad26ada` plus this uncommitted slice. Earlier failures remain historical FAIL. No delivery CI, authenticated seal, approval consumption, whole-NFR or readiness result is issued. VG2 concurrent-source guarded-write proof, other adapters/consumers and live assurance remain open; parent Story 9.2 stays `in-progress`.

### Bounded guarded-write proof progress (2026-11-16)

`spec-9-2-story-5-3-guarded-concurrent-write-proof.md` adds an explicit expensive driver outside discovery and the 15-minute fixture workflow. It constructs real private Git identities after pinned commitlint validation, binds complete source/runtime inputs and all nine root gitlinks, substitutes only synthetic GitHub HTTP facts, and requires fresh unchanged business execution for every evaluation/consumption. Six concurrent production-profile test-fixture scanner ambiguities were repaired with runtime canaries, retained non-echo assertions and direct metadata lookups; production APIs, scanner policy, selectors and budgets are unchanged. Targeted checks pass 130 production-profile, 23 publication-authority and 299 scanner cases.

Latest local run-4 proves the spec target's positive guarded write and its fresh final-preparation concurrent-edit refusal: `SOURCE_MISMATCH`, edit/destination preservation and tempfile removal. Its first three genuine executions each pass all 21 lanes (689 .NET cases, 56 scripts, seven probes). Tracker evaluation then fails its real concurrent-tenant authentication request at the unchanged 15-second HTTP timeout under high host contention; tracker write/race controls and the required six-execution overall PASS remain unproven. VG2 stays open. Private head/tree is `f21ddd9cc0a59019569b0582e872d486fae9ee30` / `62151abf8c04f596b928e8b2111632d197e82951`, inventory `224ea7c7a527e2ca42f0cbf571e34bdc987570282962fbd8147133b68b9bbf85` (19,299 source files), based on approved `a40642d90bcf11dc866c0e1204d32eb78a7c238e`. Concurrent doc commit `70bf20dceaafd37229236e11d279a5d6ea434759` and later typed-query production commit `0be0c922883d4ea61b18e843e24472162261d934` are preserved but not validated by that earlier private subject. All jobs/owned cleanup finish; historical failures remain FAIL. No live approval, seal, delivery CI, whole-NFR, readiness or parent closure follows; Story 9.2 remains `in-progress`, and future full proof must capture current source afresh.

Final serial current-source controls pass 35 adapter cases, 32 completion cases and one focused genuine OQ8 evaluation/consumption case. The original 43-case transition run's 20 time-bound failures remain historical FAIL; this focused PASS does not establish a broad regression PASS or complete VG2.
