# Adversarial General Review — EventStore Phase 4 PRD

- **PRD:** `_bmad-output/planning-artifacts/prd.md`
- **Repository baseline:** `1b6f08d4` (`main`, clean working tree), reviewed 2026-09-09
- **Addendum:** none found
- **Verdict:** **REJECT as an authoritative or implementation-readiness gate.** The document contains unusually candid risk and evidence notes, but two defects are disqualifying: normative OQ8 behavior is delegated to bytes that this repository cannot reproduce, and the MVP success model can accept a known append data-loss mode. Several live traceability and Story 6.1 assertions also contradict both other sections of the PRD and current tracked artifacts. The PRD remains useful as a recovery history and requirements inventory after repair; it is not safe as the final decision baseline it claims to be.

## Critical (2)

### C1. Normative OQ8 requirements are unavailable to the repository that must implement and verify them

- **Location:** §1.1, especially “Governing design identity” and “Scope of supersession” (lines 90-96); FR27, NFR7, and NFR16.
- **Evidence / note:** The PRD calls itself the authoritative FR/NFR baseline (§0) but states that an external Hexalith.Folders document is more authoritative for partitioning, canonical-intent fields, state-machine outcomes, ordering, exact retention and compaction timers, tombstone fields, public errors, failure handling, and evidence denominators. It explicitly admits that the design bytes are not tracked here and that “nothing inside this repository can reproduce that check.” The repository contains only the digest in validators and evidence records, not the normative bytes. A digest proves identity only after the input is available; it does not tell an implementer or reviewer what the requirements are. This makes the most safety-sensitive portions of FR27/NFR7/NFR16 non-reproducible from the alleged authority repository.
- **Fix:** Track an immutable, license-permitted copy of the governing design or a complete normative projection of every superseding clause in this repository. Bind it to repository/path/commit/digest, add a reproducible verification command, and make absence or digest mismatch a hard readiness failure. Propagating only the citation (OR11) is insufficient.

### C2. The MVP can be declared successful while a known concurrent append loses durable data

- **Location:** Vision §2 (lines 100-104); NFR7 (line 311); MVP exclusions §9.2 (lines 372-374); SM11 and SM-C5 (lines 412 and 421); OR4 (line 525).
- **Evidence / note:** Story 4.5 observed `same-key-overwrite-raw-durable-write-lost`; no append fence exists, and the deferral has neither owner nor trigger. Nevertheless, §9.2 excludes the guard from MVP and SM11 treats either an implemented production-path guard **or an out-of-MVP declaration** as satisfying a silent-loss class. That permits a five-of-five “hardening” result by improving the paperwork while the loss remains possible. This conflicts with NFR7’s “must avoid silent data loss,” SM-C5’s prohibition on weakening the definition, and the product bet that operational hardening makes the platform safe to operate. For an event store, an unbounded supported topology that silently loses concurrent writes is a release-safety defect, not merely backlog hygiene.
- **Fix:** Either deliver and prove provider-portable append fencing before MVP completion, or explicitly narrow the supported MVP operating envelope so the race cannot occur (including enforced topology/configuration guards and operator-visible failure behavior). Do not score a deferral as equivalent to a guard; keep SM11 failed until the loss is prevented within the supported envelope.

## High (6)

### H1. The traceability section preserves OR12 defects after declaring OR12 retired

- **Location:** SM2 (line 400); FR-to-story rows (lines 429-465); traceability explanation (line 467); retired OR12 disposition (lines 535 and 541).
- **Evidence / note:** The PRD says eight FRs still have multiple primary owners and that FR1 is owned by Story 4.11, then later says OR12 is retired, the duplicates were resolved, and FR1 is actually owned by Story 1.11. Current `epics.md` confirms Story 1.11 as sole primary FR1 owner and contains the demotions/partition rules. Thus the same final document supplies mutually exclusive answers to “who owns this requirement,” and its headline SM2 disposition is stale.
- **Fix:** Regenerate §11.1 from current `epics.md`; map FR1 to 1.11, remove retired OR12 warnings, encode the approved FR12/FR15 slice partitions, and recompute SM2. Add the proposed drift guard before calling traceability complete.

### H2. Story 6.1 readiness and path claims are false at the reviewed repository baseline

- **Location:** §11.3 (lines 506-509) and OR5 (line 526).
- **Evidence / note:** The PRD says the required `spec-folded-snapshot.md` is absent, that only `spec-6-1-folded-snapshot-frozen-spec.md` exists as `draft`, and therefore Story 6.2 is unauthorized. At HEAD, both files are tracked. The required `spec-folded-snapshot.md` has `status: approved-authorized` and `story_6_2_authorized: true`; the Story 6.1 artifact says `status: done`. `sprint-status.yaml` still says Story 6.1 is `review`, exposing a new three-source status conflict rather than the path drift the PRD describes. Acting on the PRD would incorrectly block authorized work while failing to surface the actual tracker reconciliation needed.
- **Fix:** Establish the authority order among normative spec, story artifact, and sprint tracker; reconcile all three atomically; retire or rewrite OR5; and update §11.3 from the reconciled result.

### H3. “Final” document status is conflated with an explicitly invalid readiness verdict

- **Location:** frontmatter `status: final` (line 3); §1 lines 88-89; §11.3 lines 496-500; OR1 (line 524).
- **Evidence / note:** The latest readiness report is still dated 2026-08-01. The PRD correctly says its gate has reopened after scope/requirement changes and a rejected retrospective, and OR1 remains blocking. Yet the artifact is marked final and titled “Implementation Readiness Recovery PRD,” with no separate machine-readable readiness state. Consumers that inspect frontmatter or the title can reasonably treat it as a closed gate even though the prose says the opposite.
- **Fix:** Separate `document_status` from `implementation_readiness_status`, include the authority report path/date/baseline SHA, and set readiness to `blocked` or `revalidation-required` until a fresh gate passes. Never let `status: final` be the only parsable state on a knowingly reopened readiness artifact.

### H4. Omnibus requirements cannot be closed safely at clause level

- **Location:** FR26, FR33, FR34, NFR17; OR7 (line 528); §11.1 and §11.2.
- **Evidence / note:** These requirements combine many independently fail-able behaviors—security, tenant filters, secrets, CLI confirmation, snapshots, replay cost, upcasting, cancellation, dead letters, audit, OpenBao, health, resiliency, images, and CI—under one stable ID. Story rows show declaration or ownership, not which clause is delivered. The PRD itself admits that a done story can appear without closing a named clause, but OR7 is not marked blocking even though its trigger is “before the next readiness re-run.” This makes a green FR/NFR status underdetermined.
- **Fix:** Sub-letter every independently testable clause and give each clause an owner, evidence class, current state, and closure rule; alternatively, add a generated clause-to-story-to-evidence matrix that fails on gaps. Make completion of this control a readiness prerequisite.

### H5. High-risk evidence can be self-approved and has no bound correctness gate

- **Location:** Owner Roles glossary (line 148); Parity Packet (line 150); OR10 and OR13 (lines 531 and 533).
- **Evidence / note:** The PRD acknowledges that an approval by the author satisfies the record but not the control, and that only Story 8.11 currently requires non-authorship. It also acknowledges that no test lane/trigger is bound to the act of recording a story done against a high-risk NFR. These are not editorial refinements: they are the controls meant to prevent author-asserted parity or status changes from becoming release truth, and the PRD cites prior contradictions caused by their absence.
- **Fix:** Define the minimum independent approval/evidence rule per high-risk closure and bind the exact CI/test lane and trigger that must pass before status mutation. Make the gate mechanically enforced and require sealed evidence identities for parity, security, and loss-prevention claims.

### H6. There is no coherent Phase 4/MVP exit decision rule

- **Location:** §9; §10; §11.3; §12.
- **Evidence / note:** Metrics mix document-existence milestones, story decomposition, runtime outcomes, and post-MVP gates. Several live metrics have no disposition, denominator, measurement command, evidence location, cadence, or accountable evaluator. No section states which FRs/NFRs and metrics must pass, which may be deferred, and who may declare MVP complete. “Epics 1-7 are in scope” is not an exit rule, especially when explicit exclusions weaken an NFR and Epic 8 is simultaneously “committed” but non-blocking.
- **Fix:** Add one release/phase exit table: required gate, exact pass condition, evidence artifact/command, owner/approver, current result, and allowed waiver policy. Keep post-MVP commitments in a separate non-gating roadmap block.

## Medium (3)

### M1. The PRD is acting as a volatile sprint-status mirror

- **Location:** §§6.8, 10, 11, and 12; repeated `done`, `backlog`, `in-progress`, receipt counts, and retrospective states.
- **Evidence / note:** Product intent and scope are mixed with mutable story statuses, receipt counts, commit anecdotes, and tracker arbitration. The Story 6.1 and OR12 failures demonstrate the predictable result: the “authoritative” requirements document becomes stale within minutes of repository changes. This also makes review harder because stable requirements and transient execution state cannot be distinguished.
- **Fix:** Keep stable requirement intent and acceptance boundaries in the PRD. Move live status and receipt counts to a generated dashboard/ledger, and link to a baseline-specific snapshot only when a historical decision requires it.

### M2. Source authority and supersession are not normalized

- **Location:** frontmatter `source_artifacts` (lines 7-66); §1; §13.
- **Evidence / note:** The PRD lists dozens of proposals and reports but records no per-source approval state, approving identity, baseline commit/digest, affected clauses, or supersession relationship. The assumption index’s statement that scope is preserved from “approved change proposals” therefore cannot be audited from the frontmatter alone. OQ8 has a special authority order; the rest of the source set does not.
- **Fix:** Replace the flat list with a compact source register containing identity/digest, approval evidence, clauses affected, and `supersedes/superseded-by`. Generate the current requirement set from that register or validate it against one.

### M3. Product outcomes are crowded out by implementation prescriptions

- **Location:** FR2-FR8, FR11-FR12, FR17-FR25, FR29-FR34, NFR17, and §8.
- **Evidence / note:** The PRD often mandates exact .NET types, method names, paths, DAPR component names, YAML policy names, package modes, and test mechanics. Some are legitimate public/runtime contracts, but many are solution design or delivery controls. With no addendum, the document offers no explicit distinction between durable product need, public compatibility contract, and replaceable implementation choice. That raises change cost and makes product-level review harder.
- **Fix:** Label each prescription as public contract, mandatory platform constraint, or current design choice. Keep public contracts and non-negotiable constraints in the PRD; move replaceable mechanisms and historical rationale to `architecture.md` or an addendum.

## Low (1)

### L1. Scope and decision state arrive too late for executive use

- **Location:** scope is first consolidated in §9 after the glossary, concerns, 37 FRs, and 19 NFRs; OR9 (line 530).
- **Evidence / note:** A decision-maker must traverse most of the document before learning the actual MVP exclusions, including the append-fencing exclusion. The PRD already records this as owed editorial work. In a safety-sensitive plan, the exclusions are not ancillary detail.
- **Fix:** Put a one-page decision summary immediately after the purpose: supported operating envelope, in/out/post-MVP scope, known unsafe gaps, current readiness state, and required decision owners. Preserve stable anchors with explicit legacy aliases if needed.

## Severity Counts

| Severity | Count |
| --- | ---: |
| Critical | 2 |
| High | 6 |
| Medium | 3 |
| Low | 1 |
| **Total** | **12** |

## Gate Recommendation

Do not use this PRD to authorize implementation-readiness, MVP completion, consumer migration, or release. C1 and C2 must be resolved before any safe gate; H1-H3 must then be reconciled against one repository baseline. After clause-level controls and a real exit rule are in place, re-run implementation readiness and bind the resulting report to the PRD frontmatter.
