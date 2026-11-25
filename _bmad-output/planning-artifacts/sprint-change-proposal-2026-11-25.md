---
date: '2026-11-25'
status: approved
approved: '2026-11-25'
approval: 'Owner (sole maintainer) approved the proposal and decisions D1–D8 as recommended; see §7.1'
approved_draft: 'c8c09d2b6954b9c7e9dd5f8cc6720284735cefbc (proposal SHA-256 bb64e550d5e611f9fee8a81518e0c759500f47be8f019a34ad634a3aa4024e84)'
mode: batch
change_scope: moderate
escalation: 'Not triggered: D1 selected path A. Switching later to a private Dapr build or an AD-34 provider exception makes the change major.'
routed_to: 'Product owner and Developer agents'
applied: 'P-1, P-2, P-3, P-4, P-5, P-6a, P-6b, P-7, P-9a, P-9b, P-10, P-12 and P-13 on 2026-11-25 (§5.1); P-8 and P-11 wait for their dependencies'
trigger: 'bmad-sprint-planning readiness gate FAIL at 3475b097857b71d4cc25d53f0dce810e7a7fc928'
starting_point: sprint-change-proposal-2026-11-21.md
rechecked_revisions:
  - 3475b097857b71d4cc25d53f0dce810e7a7fc928
  - 3e5ed0a8509beef27efd5663cbc762ece4b7d1b8
phase_4_readiness: reject
excluded_scope: 'G-1 Durable Task work (separately approved)'
---

# Sprint change proposal: Phase 4 append protection, planning baseline and assurance

**Approved by the owner on 2026-11-25. Phase 4 readiness remains Reject.** The latest sprint-planning run failed at commit `3475b0978`. This proposal rechecks its three blockers against the current checkout, using the applied [21 November proposal](sprint-change-proposal-2026-11-21.md) as the starting point. It records the next decisions, a delivery order and the planning edits that follow from them. Drafting wrote only this file. After the owner's approval (§7.1), waves 1–3 and the EventStore P-4 candidate intake were applied on 2026-11-25 (§5.1). No tracker, matrix or code was changed, and nothing was sealed, attested or sent outside the repository.

Most of the work below can start now as owner-authorized corrective work under AD-12. The exception is G-APPEND. No released Dapr version supplies the conflict protection that the approved Story 4.16 design requires, and no one has asked upstream for it yet. The owner chose to obtain it upstream (decision D1, path A), so Phase 4 readiness has no date that can be forecast until a supported Dapr release supplies it.

## 1. Issue summary

### 1.1 Trigger and recheck

The sprint-planning readiness gate returned FAIL at `3475b0978`. While this analysis ran, a concurrent session committed six deferred-work fixes up to `3e5ed0a85` (local and unpushed; `origin/main` is still `3475b0978`). Those commits change only `src/` and `tests/`. Every planning input, the tracker, the risk matrix, the owner registry and every file under `tools/` is byte-identical at both revisions. The results below therefore apply to both.

| Finding from the FAIL run | Recheck result | Status |
| --- | --- | --- |
| G-APPEND has no qualified capability or binding | The matrix reports `BINDING_UNBOUND`, owner Story 4.16. The 4.16 spec records NO-GO source screens for Dapr 1.18.7 and 1.19.1. New upstream releases since then are listed in §2.1. | Confirmed |
| All five epic input digests differ from the current files | All five differ from the `epics.md` frontmatter. Three also differ from the table reviewed on 21 November, because commit `831931885` (22 November) edited the PRD, the architecture and EXPERIENCE again. | Confirmed, and wider than reported |
| 56 requirement definitions and 49 clause ownership rows agree | Recomputed at HEAD: all 56 FR/NFR definitions match between the PRD tables and the epics Requirements Inventory, and all 49 clause rows agree on primary and supporting owners. | Confirmed |
| All 12 high-risk rows lack seals and attestations | `tools/validate-phase-4-gate-risk-matrix.py` exits 1 and reports `SEAL_MISSING`, `ATTESTATION_MISSING` and `ACHIEVED_BELOW_REQUIRED` on all 12. The five standard controls pass classification. The registry names one human, so the required level is `single-maintainer-attested`. | Confirmed |
| Seven rows have incomplete evaluator bindings | `BINDING_UNBOUND` rows and their owners: G-APPEND (4.16), G-COMPAT (3.18), G-AUTH-HOSTS (5.11), G-RUNTIME-PARITY (3.15), G-PUBLICATION-AUTH (3.19), G-CONSUMER (3.20) and G-MVP-COVERAGE (9.5). | Confirmed |
| Story 2.14 has no NFR2 completion adapter | Both `--target spec` and `--target tracker` exit 1 with `ADAPTER_UNAVAILABLE` for NFR2. The completion writer knows only the Story 5.3 adapter. | Confirmed |

The sprint validator returns `valid: true` with zero problems. The tracker is consistent; the gaps are in decisions, evidence and planning-source approval.

### 1.2 Problem statement

Three blockers compound each other:

1. **G-APPEND has no path to closure.** The approved design needs a Dapr capability that does not exist. Platform's qualification intake is preparing a test harness for it, and no owner decision says how the capability will be obtained.
2. **G-BASELINE cannot be approved as currently sequenced.** OR14 requires AD-26 to be ratified before the epic digests are renewed. Ratification must name a qualified broker and a qualified NFR7 class (c) protection path, and neither exists. So planning-baseline approval transitively waits on G-APPEND and broker qualification. Meanwhile the planning sources keep drifting after each review.
3. **Assurance is wired but unused.** The 9.2 controls, readiness request and three gate verifiers exist. Seven rows have no evaluator, twelve readiness prerequisites have no verifier, Story 2.14 has no adapter, and no live sealed decision has been attempted.

Issue type: a technical limitation (missing upstream capability) combined with a sequencing defect in the readiness plan.

## 2. Impact analysis

### 2.1 G-APPEND and Story 4.16

**Approved design.** Owner decisions of 1 November and 9 November require Dapr-owned atomic conflict protection with five properties:

- the commit is conditional on a durable actor-state revision;
- each event identity is created only once;
- the whole actor batch commits or rolls back atomically;
- raw writes, deletes and TTL cannot bypass the protection;
- governed erasure keeps working through its existing authority.

The protection must come from a supported, released SDK/runtime/component combination. A private build, a provider exception or activation fencing each needs a separate decision. [Story 4.16 spec](../implementation-artifacts/spec-4-16-append-write-once-conformance-through-enforced-envelope.md) · [1 November decision](sprint-change-proposal-2026-11-01-story-4-16-atomic-conflicting-writes.md) · [9 November intake](sprint-change-proposal-2026-11-09-readiness-recovery.md)

**Capability evidence on record.** On 1 November and 9 November, source inspection rejected two combinations: daprd 1.18.7 with components-contrib 1.18.6, and daprd 1.19.1 with contrib 1.19.0 and SDK 1.19.0. In both, the actor transaction accepts optional ETags but requires none. The PostgreSQL v1 component overwrites an existing key when the ETag is omitted. A current ETag still permits replacement. The actor conversion never carries `first-write`.

**New upstream releases since the last screen.** The tag identities below were resolved with `git ls-remote`. The file-level comparisons use the GitHub compare API. They are preliminary signals, not the bounded source screen.

| Release | Identity | Relevant change |
| --- | --- | --- |
| daprd 1.19.2, published 19 November | tag `v1.19.2` → `66efc169ba72dca18ab73394c98381d68cda88f0`. Its `go.mod` embeds components-contrib 1.19.2. | No file under `pkg/actors/state/` or `pkg/actors/api/` changed between 1.19.1 and 1.19.2. The release note on ETag and `first-write` consistency says: "Requests that do not set an ETag and do not use `first-write` behave as before." |
| components-contrib 1.19.1 and 1.19.2, published 16 and 18 November | 1.19.2 → `ab395ab88023febfb824de46d474c6d299e74fb1` | Fixes ETag and `first-write` handling in 14 state stores. An expired PostgreSQL v1/v2 row no longer matches an ETag. A Redis `Multi` with an ETag mismatch now applies nothing. Also includes the RabbitMQ NACK fix #4748 (`ac89e0966`). |
| Dapr .NET SDK 1.19.1, on NuGet | tag `v1.19.1` → `70499eb50a5d24dd2511263feec23dac14077fc3` | No `src/Dapr.Actors` file changed since 1.19.0. |

**Reconciliation.** The design, the capability evidence and the Platform intake agree with one another:

- **Design.** The new releases make *optional* concurrency checks consistent. They add no *mandatory* revision or create-once check to the actor path. The approved design therefore still has no supporting release. The expected screen outcome is NO-GO, but it must be recorded rather than assumed.
- **Platform intake.** Platform's `PLAT-ES416-QH` Q2 design revision R4 (aggregate `sha256:8d3849f084413f027ad22139db6ccfaa5fe44d2069be2b74e8a91a71fcee6d2e`, 40-file inventory) was prepared on 22 November. The requester approved it with "I approve all". Three formal receipts are pending: R-P1 (Platform contract and execution), R-B1 (Builds runner and tool) and R-E1 (EventStore consumer and assertions). Platform records U1 partial, U2 pending and U3–U5 blocked. R4 itself keeps Story 4.16 in backlog "until a supported Dapr release can atomically reject conflicting event writes". [R4 receipt record](../../references/Hexalith.Platform/_bmad-output/specs/spec-plat-es416-qh-q2-contract-implementation-r4/owner-acceptance.md)
- **What is missing.** The 9 November recheck listed three options and selected none:
  - **A:** pursue a supported upstream release;
  - **B:** authorize an exact private build;
  - **C:** approve an AD-34 provider exception with the architecture changes it requires.

  No upstream issue or request exists. Each Platform or Builds step therefore prepares for a capability that has no delivery route.

**Next decision (D1): choose the G-APPEND capability path.**

- **Owner:** the Product owner, acting in the Architecture and EventStore persistence roles. These roles are held by the sole maintainer, so the result is recorded at the `single-maintainer-attested` level.
- **Evidence the decision requires:**
  1. **E1. Bounded source screen.** Screen the current supported tuple (daprd 1.19.2 `66efc169…`, contrib 1.19.2 `ab395ab8…`, .NET SDK 1.19.1 `70499eb5…`, CLI 1.19.1) against the five contract rows of the 4.16 spec. Record it in the spec's Verification section under AD-12, with the official support-table pairing, fetch identities and per-row results. This is proposed edit P-1.
  2. **E2. Upstream status.** Record, from dapr/dapr, dapr/dotnet-sdk and dapr/components-contrib, whether any issue or pull request proposes a mandatory actor-state revision or create-once condition. For path A, the request text is the 4.16 spec's "Dependency ownership and unblock handoff" table. Filing it is an outward-facing action and needs explicit owner approval.
  3. **E3. Platform receipt state.** R-P1, R-B1 and R-E1 for R4, as recorded in Platform. These are independent of the capability.
  4. **E4. Path-specific inputs.**
     - Path A needs a review trigger, such as the first Dapr 1.20 release candidate or an owner-set date.
     - Path B needs the exact build identities, the support and custody owner, and a re-proof cadence.
     - Path C needs the exact AD-34 exception text. It also needs AD-5, AD-26 and 4.16 amendments, because the current 4.16 boundary forbids application SQL and the PRD says a PostgreSQL-only result is not provider-portable.

**Recommendation for D1:** choose path A with an explicit review trigger, and explicitly reject paths B and C for now. That keeps the approved design and every acceptance criterion intact and stops repeated re-triage. Its consequence must be stated plainly: G-APPEND, and therefore `READY`, has no forecastable date while path A is pending. If the owner needs a dated MVP, path A is the wrong choice, and B or C should be scoped as a major change through bmad-architecture.

**Platform receipt R-E1 (D2).** EventStore's R-E1 receipt can wait until D1 produces a candidate release or a selected build. Issuing it earlier commits adoption work for a harness that cannot run U3–U5. If the owner chooses path B, the harness becomes the critical path, and R-P1, R-B1 and R-E1 should then be issued together.

**What Stories 3.17 and 3.21 can advance now:**

| Story | Can advance now, EventStore-owned under AD-12 | Requires Platform acceptance or execution | Requires a qualified capability or AD-26 records |
| --- | --- | --- | --- |
| 3.17, backlog | <ul><li>AC1–AC6: inventory of Debug/source and Release/package graphs, row schema, guard in the existing Contracts architecture/packaging lane, negative fixtures, credential and call-site checks, and the published guide with its limits.</li><li>Static rows for every reachable actor-state write path: both actor transports and every generic state, bulk or transaction alias. These supply the denominator for 4.16 AC2.</li><li>The owner records the corrective scope in the 3.17 spec first.</li></ul> | Profile-specific qualification of rows against a concrete Platform component configuration, and live alias exclusion on a Platform profile. | Marking any append-protection row qualified. |
| 3.21, in progress | <ul><li>Record the AC1 disposition for the 1.19.2 tuple: unresolved and ineligible for protected-actor writes. This shares one screen with E1.</li><li>If the owner approves D5, prepare a non-promoting `runtime-lock-1.19.2.json` with release checksums and image identities.</li><li>The 1.19.1 lock and its results remain historical.</li></ul> | <ul><li>A Platform broker candidate on 1.19.2. Released fix #4748 meets Platform's 13 November trigger "await supported RabbitMQ fixes without exceptions" for RabbitMQ pressure.</li><li>Requalification of the other four rejected guarantees. RabbitMQ missing-binding still waits on the open upstream PR #4767.</li><li>Kubernetes rehearsal and restore.</li><li>Builds adoption of SDK 1.19.1 and CLI 1.19.1 pins.</li></ul> | Eligibility of the protected-write mode (AC1); broker and runtime selection by AD-26 owner records; handoff receipts to 5.7 and 3.19; 4.16 re-proof after any runtime minor change. |

### 2.2 G-BASELINE and Story 9.3

**Digest state at HEAD.** The AD-26 ratification subject was recomputed with the procedure defined in AD-26. The same implementation reproduces the 4.16 spec's earlier subject `b9e899ec…`.

| Input | Epic pin, abbreviated | Reviewed 21 November | Current SHA-256 | Changed by |
| --- | --- | --- | --- | --- |
| PRD | `b99effdb4142` | `3806aff36c58` | `8280436c5bbba45442b923409f1f729acab2e3712507ea4c7889acd3026ae0f3` | `831931885` (22 November) |
| Architecture | `7e3dbc7bd335` | `bb2c8d20efe7` | `caa4d4c23064f2df4e3d5d8f9fd0dd21fbe88944d34f3776aa67e6defb2c53b7` | `831931885` |
| DESIGN | `76be269737dd` | `d29c9db0f164` | `d29c9db0f164f0c8f27f2b58bd25475489c2539135697d30b638c0964f0d5727` | unchanged since review |
| EXPERIENCE | `06de15b34922` | `c584c954a776` | `98af78c9f26ef666cb4848df15616522bb3e58867a824619bbae176e6b07e3a2` | `831931885` |
| UX handoff | `2927f97d4fe7` | `5dc2d6782105` | `5dc2d6782105b4d09d79e68c1b557c93e5e2b7cf0939f04f86ffeb68beec5f7f` | unchanged since review |
| AD-26 ratification subject | — | `5070ebdcf50d…` | `5070ebdcf50d9fe7a7013be67d5c872e60563acf2ca4031382d0123754080803` | last changed by `2b8895f18`. The authenticated push that moved `main` to it was at 2026-11-21T11:33:17Z, so the 24-hour window opened 2026-11-22T11:33:17Z. |

**Substantive conflicts still open after the 21 November correction:**

| # | Conflict | Evidence | Owner |
| --- | --- | --- | --- |
| C1 | **McpCli disposition.** The PRD, architecture and EXPERIENCE now say remote Admin A1–A4 are confidential UI under adopted McpCli AD-23 and that "No generic Admin contract or transport is required or admitted". The `epics.md` header still says McpCli "requires an approved generic admin contract, authorization, and parity proof before removal". The new banners are labelled "(Story 5.11 candidate)". That label refers to McpCli Story 5.11, which is in review, but in EventStore documents Story 5.11 means the shared JWT contract that owns G-AUTH-HOSTS. | `831931885`; `epics.md` line 29; McpCli tracker row `5-11-reconcile-product-claims-and-the-migration-plan: review` | Product and architecture owners, with the McpCli maintainer |
| C2 | **Architecture review closure is incomplete.** The memlog records that the 21 November reviewer gate reconciled all 8 high and 4 medium findings at spine scope. However: <ul><li>The 22 November edit changed AD-35 and an implementation-status row after that review, and the memlog has no entry for it. AD-26 requires every architecture update to record whether changes outside the subject alter what AD-26 requires.</li><li>There is no closure record for the current spine bytes.</li><li>Three implementation discrepancies remain open by design: the OpenBao comparator floor (2.7.1/2.6.4, below the patched 2.7.2/2.6.5 and current 2.7.3/2.6.6), the legacy `UseNuGetDeps=false` alias, and the OQ8 README pending-CI note.</li></ul> | [21 November validation](architecture/architecture-eventstore-2026-07-05/reviews/validate-report-2026-11-21.md); memlog tail ends 2026-11-21 | Architecture owner. Security and Platform own the OpenBao floor, the build owner owns the alias, and Story 4.17 owns the README. |
| C3 | **AD-26 ratification is structurally blocked.** The Ratification paragraph requires each role record to name "the broker, DAPR runtime pin, restore posture, and NFR7 class (c) protection path and its qualified actor-transaction semantics". There is no selected broker (five guarantees rejected) and no qualified class (c) path (G-APPEND). Under the current OR14 wording, planning-baseline approval and epic renewal must therefore wait for production qualification. | AD-26 Ratification; PRD OR14; Story 9.3 Dependencies | Product and architecture owners (D3) |
| C4 | **SM11 overstates the loss-class evidence.** PRD SM11 credits three of five classes, including Story 5.1's class (a). AD-12 says Story 5.1 establishes only the historical class (a) guard and that "the count qualifying on the current profile is unverified". This is PRD round-3 High finding 3, still open. | PRD §10 SM11; architecture AD-12 Rule | Product owner with the persistence and Test owners |
| C5 | **The PRD validation record is stale.** PRD frontmatter cites the "post-update" report (Fair, 19 findings). The later round-3 report (Fair; 6 high, 5 medium, 2 low) is cited by the architecture but not by the PRD. No validation has recorded which round-3 findings the 21–22 November edits resolved. Among the spot-checked findings, SM11 (C4) and the FR4-C3 conditional-query evidence (Story 2.18 is not a supporting owner) still appear open. The first readiness seal inputs are now defined by the 22 November Story 9.2 readiness-decision spec. | [Round-3 report](prds/prd-eventstore-2026-07-05/validation-2026-11-20-round-3/validation-report.md) | Product owner |
| C6 | **The readiness record is stale.** The PRD frontmatter, the PRD §0 lead and `implementation-readiness.md` still describe the 6 October assessment. The `3475b0978` FAIL was not saved. This proposal records its rechecked findings until the readiness report is saved. | PRD frontmatter `implementation_readiness_last_*` | Product owner |

The G-1 Durable Task decision record (`docs/architecture/g1-durable-task-scope-decision.md`) is outside this proposal and outside the five epic inputs. Its commits `388b0ad67`, `8e2059087` and `3475b0978` changed no planning input.

**Sequence to renew the epic hashes.** Each step consumes the previous one. After S5, a planning-input edit sends the sequence back to S5.

| Step | Work | Owner | Exit evidence |
| --- | --- | --- | --- |
| S1 | Architecture closure on the current spine bytes: <ul><li>add the missing memlog entry for `831931885` (AD-26 impact NO; subject unchanged);</li><li>record a closure table for all 13 findings of 21 November;</li><li>list each retained discrepancy with its owner.</li></ul>The spine stays `draft`. (P-7) | Architecture owner | `lint_spine.py` reports zero findings; recomputed subject digest; closure table committed |
| S2 | Resolve C1 (D4), then align the `epics.md` header and the banner label with the decision. (P-8) | Product and architecture owners, McpCli maintainer | No remaining requirement for a generic Admin contract in any planning input, or banners returned to candidate wording |
| S3 | Decide AD-26 for baseline purposes (D3). Recommended: an owner-approved **AD-26 baseline disposition** that binds the current subject digest while ratification stays mandatory before G-PUBLICATION-AUTH, `production-promoted` and G-READINESS. Neither the AD-26 section nor anything else inside the ratification subject is edited. (P-9) | Product and architecture owners | PRD OR14, the G-BASELINE row and the Story 9.3 criteria updated; an AD-26 disposition record bound to `5070ebdc…` |
| S4 | Build the Story 9.3 drift guard and manifest lane under AD-12, with the required truthful initial FAIL. Fix C4 and C5 findings that are planning text. (P-10, P-11) | Test owner; Product owner for the text fixes | 9.3 spec Verification with the command, digests, findings and CI run link; negative fixtures failing beside a positive control |
| S5 | Validate one frozen candidate source set on one clean revision: <ul><li>the 9.3 lane;</li><li>a round-4 PRD validation that disposes of every round-3 finding;</li><li>architecture lint and OQ8 checks;</li><li>a UX contract comparison (/types, hidden restore/import, gated tenant provisioning).</li></ul> | Product, architecture, UX and Test owners | All validations recorded against the same revision; no unresolved conflict |
| S6 | Owner approval of that exact source set at `single-maintainer-attested` level, at least 24 hours after the authenticated push that last changed the bound bytes | Product owner, holding the PRD, architecture, epic, UX, Test and tracker roles | A dated approval in the 9.3 spec naming every digest |
| S7 | Renew the five epic digests in the same change as the AD-ID propagation required by Story 9.3 AC2, then rerun the 9.3 lane on that revision | Epic owner, Test owner | The 9.3 lane passes G-BASELINE on that revision |

### 2.3 Assurance and coverage

**Readiness prerequisites by state** (15 inputs to the readiness request; G-HIGH-RISK and G-READINESS are computed from them):

| State | Gates | What closes the gap |
| --- | --- | --- |
| Bound in the matrix, with an enrolled readiness verifier | G-OQ8, G-TENANT, G-STATUS-ID | Sealed run of the dedicated gate-decision workflow on the exact head, plus an owner attestation at least 24 hours after the authenticated subject-changing push. G-TENANT also needs Story 2.14's package evidence and NFR2 adapter. |
| Standard control (classification passes), no readiness verifier | G-BASELINE, G-CLAUSE, G-NFR8, G-NFR18, G-NFR-OWNERSHIP | Owner business validators (9.3, 9.4, 6.3 spec digest, 6.7 re-attestation and the 9.5 owner coverage result), each exposed to readiness as an artifact approval. The readiness contract already supports null run coordinates for these. |
| Unbound (`BINDING_UNBOUND`) | G-APPEND (4.16), G-COMPAT (3.18), G-AUTH-HOSTS (5.11), G-RUNTIME-PARITY (3.15), G-PUBLICATION-AUTH (3.19), G-CONSUMER (3.20), G-MVP-COVERAGE (9.5) | Each owner binds the command, trigger, subject paths, evidence identity and pass condition in the matrix, then supplies its readiness verifier. |

**Story 2.14 NFR2 completion adapter.** The 22 November G-TENANT business evidence verifier already authenticates the EventStore and Tenants runs, the SDK gitlink and the fresh-backend receipts. The missing adapter can be a thin story-completion adapter, analogous to `tools/phase_4_story_5_3.py`. It consumes the authenticated G-TENANT decision for the exact subjects and refuses on a mismatched subject or a missing seal or attestation, for both spec and tracker targets. The fix `fa891493a` is contained in tags v5.4.0 through v5.6.0, so the remaining package evidence is a Tenants adoption question, not an EventStore code change. (P-12)

**Order of the remaining Story 9.2–9.5 work:**

| Phase | Work | Owner | Depends on |
| --- | --- | --- | --- |
| A, can start now | <ul><li>Story 2.14 NFR2 adapter (P-12)</li><li>Story 9.4 all-clauses validator (the ledger exists and agrees 49/49)</li><li>Story 9.3 guard (S4)</li><li>9.2 readiness verifiers for the five standard controls</li><li>Owner re-attestation of the G-NFR18 page `99395b0a…` (OR6)</li></ul> | 2.14 owner; Test owner; Product owner | Owner corrective-scope records under AD-12 |
| B | <ul><li>Story 9.5 coverage manifest and validator, then bind G-MVP-COVERAGE.</li><li>Owners of 3.18, 5.11, 3.19 and 3.20 bind their rows when their stories are scheduled.</li><li>The 3.15 owner binds G-RUNTIME-PARITY to a current candidate.</li></ul> | Test owner; each story owner | 9.4 clause inventory; 9.2 Assurance Control |
| C | First live assured decisions for G-OQ8 and G-STATUS-ID, then G-TENANT after Tenants package evidence (D7). This retires `SEAL_MISSING` and `ATTESTATION_MISSING` for those three rows only. | Gate owners; Product owner for attestation | Phase A for G-TENANT; a stable subject for 24 hours |
| D | Bind G-APPEND and run AC1–AC11. | Persistence owner, Platform | D1 outcome; 5.6/5.7, 3.18, 7.6 and 7.22 prerequisites |
| E | OR1 final assessment (§9) | Product owner | All 15 prerequisites passing on the approved baseline |

### 2.4 Epic, artifact and technical impact

- **Epics.** Epic 4 (4.16) and Epic 9 (9.2–9.5) carry the critical path. Epic 3 (3.17, 3.21) can advance its EventStore-owned slices. Epics 2 (2.14), 5 (5.4/5.11 wording) and 7 (7.5 wording) are touched by text only. No epic is added, removed or reordered. The MVP scope is unchanged unless D1 selects path B or C.
- **PRD.** OR4, OR14, G-BASELINE, SM11, the frontmatter readiness and validation records, and the §0 lead (P-3, P-9, P-10, P-11).
- **Architecture.** The memlog and a closure note (P-7). No AD text change is proposed. AD-26 is deliberately not edited, so its subject digest and approval window are preserved.
- **UX.** The EXPERIENCE banner label only (P-8). The screens and flows are unchanged.
- **Tooling and CI.** A new Story 2.14 adapter module, readiness verifier enrollment, and the 9.3 and 9.4 validation lanes. Per the Hexalith.CiCd boundary, workflow files are not edited from this module. Any new workflow lane must follow the CiCd ownership rules and must not assert workflow YAML.
- **Tracker.** No lifecycle change is proposed. Every row keeps its current value.

## 3. Recommended approach

**Direct adjustment with one MVP-level decision (D1).** The existing stories already own every gap. What is missing is decisions, owner-scoped corrective work and a feasible order.

- **Rollback is not viable.** Reverting completed 9.2 controls, the 2.14/2.15 fixes or the 21 November reconciliation would discard working evidence without removing any blocker.
- **An MVP review is needed only for G-APPEND.** Narrowing NFR7 class (c) or accepting the risk is forbidden by OR4 and SM-C5. A dated MVP therefore requires path B or C, each a major change.
- **One sequencing change is recommended (D3).** It removes the dependency of planning-baseline approval on production qualification without weakening `READY`, because G-APPEND and G-PUBLICATION-AUTH remain mandatory inputs.

**Effort.** The planning-side corrective work is roughly 2–3 engineer-weeks:

| Item | Estimate |
| --- | --- |
| E1 screen and S1 closure | about 1 day together |
| Story 9.3 guard | 2–4 days |
| Story 9.4 validator | 1–2 days |
| Story 2.14 adapter | about 1 day |
| Standard-control verifiers | 1–2 days |
| Story 9.5 | 3–5 days |
| Story 3.17 module slice | 3–5 days |
| Story 3.21 EventStore intake | about 1 day, plus Platform execution |

Binding G-COMPAT, G-AUTH-HOSTS, G-PUBLICATION-AUTH and G-CONSUMER depends on Stories 3.18, 5.11, 3.19 and 3.20, which are multi-week stories.

**Risk.** High schedule risk from the upstream capability. Medium risk of renewed drift: concurrent sessions committed while this analysis ran, and `831931885` landed after the 21 November reviews. Freeze planning inputs between S5 and S7.

## 4. Detailed change proposals

The owner approved these edits on 2026-11-25. Waves 1–3 and the EventStore P-4 candidate intake were applied the same day (§5.1). The P-4 hosted broker request awaits Platform acceptance. P-8 and P-11 wait for their remaining dependencies. "Owner" means the accountable role. All Phase 4 roles are currently held by one human, so every approval is labelled `single-maintainer-attested`.

### 4.1 Stories and specs

**P-1 · Story 4.16 spec · new section after "Story 3.21 Handoff Recheck — 2026-11-09"**

- **Before:** The spec's latest capability evidence is the 9 November recheck of daprd 1.19.1, contrib 1.19.0 and SDK 1.19.0, ending "Draft/backlog status and G-APPEND FAIL/BLOCKED remain; implementation, runtime/dependency changes, key distribution and migration stop here."
- **After:** Append a section "Capability recheck — 1.19.2 tuple (<date>)" with:
  - the exact tuple: daprd `v1.19.2` `66efc169ba72dca18ab73394c98381d68cda88f0`, contrib `v1.19.2` `ab395ab88023febfb824de46d474c6d299e74fb1`, .NET SDK `v1.19.1` `70499eb50a5d24dd2511263feec23dac14077fc3`, CLI 1.19.1;
  - the official support-table pairing;
  - a per-row disposition for revision, create-once, whole-batch, raw transports and erasure;
  - the fetch commands and results;
  - the D1 decision, quoted with its date and review trigger.
- **Rationale:** D1 needs current evidence (E1). The 1.19.2 ETag/`first-write` fixes must be assessed and recorded, not assumed.
- **Owner:** EventStore persistence owner.
- **Dependencies:** None for the screen. The D1 quote follows D1.
- **Acceptance evidence:** Recorded fetch identities and per-row results; frozen blocks unchanged; `git diff --check` clean; status `draft` and tracker `backlog` unchanged.

**P-4 · Story 3.21 spec · new section "1.19.2 candidate intake"**

- **Before:** The spec's latest sections are the 14 November Platform handoff and the 15 November RabbitMQ upstream reconciliation. The candidate lock is `runtime-lock-1.19.1.json` (runtime 1.19.1, components 1.19.0).
- **After:** Record the AC1 disposition for the 1.19.2 tuple (from P-1): unresolved and ineligible for protected-actor writes. If D5 is approved, also record:
  - the non-promoting `runtime-lock-1.19.2.json` plan with release checksums and amd64/arm64 image identities;
  - the Platform candidate request (scope: requalify RabbitMQ pressure with #4748, rerun the other four rejected guarantees, retain historical packets);
  - the Builds SDK/CLI 1.19.1 dependency.
- **Rationale:** #4748 has been released, which meets Platform's 13 November trigger. EventStore's evaluation side should be ready before Platform executes.
- **Owner:** Story 3.21 owner (EventStore deployment role). Platform executes; Builds owns the shared pins.
- **Dependencies:** D5 and P-1 for EventStore intake. The hosted broker request awaits Platform acceptance and separate attempt authorization.
- **Acceptance evidence:** Lock checksums match the downloaded release artifacts; sealed lanes untouched; no promotion or selection claim; spec stays `in-progress`.

**P-6a · Story 3.17 spec · Verification and authorization record**

- **Before:** `status: backlog` with no recorded corrective scope.
- **After:** Record the owner's AD-12 corrective scope for AC1–AC6, including static rows for every reachable actor-state write path. Profile qualification remains Platform-owned, and append-protection rows remain unresolved.
- **Rationale:** This is the largest block of G-APPEND-adjacent work that needs neither Platform nor a capability.
- **Owner:** Story 3.17 owner, with the owner's authorization.
- **Dependencies:** Owner authorization only.
- **Acceptance evidence:** The 3.17 spec records the scope, commands, counts, commit and CI run link; negative fixtures fail beside a positive control.

**P-12 · Story 2.14 spec · Tasks & Acceptance**

- **Before:** "`python3 -B tools/validate-phase-4-story-completion.py --spec …spec-2-14-canonical-tenant-boundary.md --target spec` exits 1 with `ADAPTER_UNAVAILABLE` for NFR2. The same command with `--target tracker` produces the same refusal."
- **After:** Add an unchecked task: "Provide the NFR2 story-completion adapter (`tools/phase_4_story_2_14.py`) registered in `tools/phase_4_story_completion.py`. It consumes the authenticated G-TENANT gate decision for the exact EventStore and Tenants subjects and refuses on a mismatched subject, a missing seal or a missing attestation, for both spec and tracker targets, with a positive control using synthetic platform responses."
- **Rationale:** This is PRD OR20 and the Story 9.2 tenant handoff. The G-TENANT verifier it consumes already exists.
- **Owner:** Story 2.14 owner (EventStore, Tenants, Security and Test roles).
- **Dependencies:** The G-TENANT verifier (done 22 November). Live completion also needs Tenants package evidence on an EventStore release containing `fa891493a` (v5.4.0 or later), plus a seal and an attestation.
- **Acceptance evidence:**
  - Both commands return a gate-specific refusal instead of `ADAPTER_UNAVAILABLE` until real evidence exists.
  - The story-completion fixtures pass.
  - The spec stays `in-progress`, with the CI run link recorded.

### 4.2 Epics (`epics.md`)

**P-2 · Story 4.16 · after "Current reconciliation (2026-11-09)"**

- **Before:** "**Current reconciliation (2026-11-09):** Backlog, with approved scope and amended planning only. … The approved delivery intake is recorded outside frozen intent; Platform acceptance and execution remain pending."
- **After:** Keep that paragraph and add: "**Current reconciliation (2026-11-25):** Backlog. Dapr runtime 1.19.2, components-contrib 1.19.2 and .NET SDK 1.19.1 make optional ETag and `first-write` checks consistent, but the actor path still has no mandatory revision or create-once condition ([4.16 recheck](…)). Platform's PLAT-ES416-QH Q2 R4 design awaits receipts R-P1, R-B1 and R-E1. The owner's D1 capability-path decision ([proposal](sprint-change-proposal-2026-11-25.md)) governs the next step. G-APPEND remains FAIL/BLOCKED."
- **Rationale:** Keeps the epic, spec and tracker accounts aligned, as the OR15 lifecycle comparison requires.
- **Owner:** Epic owner (Product owner).
- **Dependencies:** P-1 and D1.
- **Acceptance evidence:** The 9.3 lifecycle comparison shows tracker `backlog`, spec `draft` and an epic reconciliation statement that agrees.

**P-5 · Story 3.21 · second acceptance block (runtime inventory)**

- **Before:** "The owner-approved 2026-11-08 refresh target is Dapr runtime/control-plane `1.19.1`, CLI `1.19.1` and embedded components-contrib `1.19.0`; retain Dapr .NET SDK `1.19.0` while latest-stable evidence agrees."
- **After (only if D5 is approved):** "The owner-approved 2026-11-25 candidate target is Dapr runtime/control-plane `1.19.2`, CLI `1.19.1`, embedded components-contrib `1.19.2` and .NET SDK `1.19.1`. The 1.19.1 target, its lock and its broker results remain historical comparison inputs, not current conformance."
- **Rationale:** 1.19.2 carries the released RabbitMQ NACK fix, the state-store concurrency fixes and the go-jose and brotli CVE fixes. Platform's 23 November proposal already prepares hosted 1.19.2.
- **Owner:** Product owner in the deployment role.
- **Dependencies:** D5.
- **Acceptance evidence:** Story 3.21 AC2 inventory binds the exact identities; the old lock is unchanged.

**P-6b · Story 3.17 · "Current reconciliation"**

- **Before:** "**Current reconciliation:** Backlog. The published guide/register are preliminary and unqualified; … Story 1.11’s domain scans do not enforce this whole-platform boundary."
- **After:** Append: "On <date> the owner authorized the module-owned inventory and guard slice (AC1–AC6) under AD-12. It enumerates every reachable actor-state write path as unqualified rows for Story 4.16 AC2. Profile qualification remains Platform-owned. Append-protection rows remain unresolved until a qualified capability exists."
- **Rationale, owner, dependencies and acceptance evidence:** as P-6a.

**P-8 · Header paragraph "Approved McpCli course correction (2026-09-27)"**, depending on D4

- **Before:** "… McpCli Epic 5 inventories stream, subscription, cluster, and destructive operations and requires an approved generic admin contract, authorization, and parity proof before removal."
- **After, if D4 adopts AD-23:** "… Under adopted McpCli AD-23, remote Admin A1–A4 remain confidential UI and local Admin A5–A8 are withdrawn from the public migration target. No generic Admin contract or transport is required. Each legacy operation is removed only after its owner-approved inventory disposition, confidential UI coverage or explicit withdrawal, and authorization, audit and persisted-behavior evidence."
- **Companion change:** In the PRD, architecture and EXPERIENCE banners, change "(Story 5.11 candidate)" to "(McpCli Story 5.11, <status>)".
- **If D4 does not adopt AD-23:** Return the three banners to candidate wording and restore the pre-`831931885` AD-35 and FR26-C4 text.
- **Rationale:** Removes conflict C1 and the collision with EventStore Story 5.11.
- **Owner:** Product and architecture owners, with the McpCli maintainer.
- **Dependencies:** D4; McpCli Story 5.11 leaving review.
- **Acceptance evidence:**
  - No planning input both requires and rules out a generic Admin contract.
  - The AD-35 text agrees across the PRD, architecture, EXPERIENCE and epics.
  - The AD-26 subject digest is unchanged.

**P-9b · Story 9.3 · Dependencies and first acceptance criterion**, depending on D3

- **Dependencies, before:** "Final baseline approval requires OR14 completion: architecture reviewer closure and AD-26 ratification or an approved replacement, …"
- **Dependencies, after:** "Final baseline approval requires OR14 completion: architecture reviewer closure and either per-role AD-26 ratification or an owner-approved AD-26 baseline disposition bound to the current ratification subject digest, …"
- **AC1, before:** "**And** it rejects any digest mismatch, `draft` status, open `[ASSUMPTION]`, active or unreconciled corrective-work authorization, and any approval that predates the bytes it binds."
- **AC1, after:** "**And** it rejects any digest mismatch, `draft` status, open `[ASSUMPTION]`, active or unreconciled corrective-work authorization, and any approval that predates the bytes it binds. The sole exception is an architecture whose only `[ASSUMPTION]` is AD-26 and whose `draft` status follows from it. That exception applies only while a current AD-26 baseline disposition binds the subject digest it records. A stale or missing disposition fails."
- **Rationale:** See P-9a.
- **Owner:** Product and Test owners.
- **Dependencies:** D3.
- **Acceptance evidence:** 9.3 fixtures cover four cases: a current disposition passes; a stale subject digest fails; another `[ASSUMPTION]` fails; `draft` without a disposition fails.

**P-13 · Story 9.2 · after "Audit acceptance addition (2026-11-20)"**

- **Before:** The section ends with "Run the complete transition fixture suite and report the live matrix separately. A passing fixture suite supplies no seal or attestation."
- **After:** Add: "**Readiness binding order (2026-11-25).** The readiness request enrolls owner verifiers for G-OQ8, G-TENANT and G-STATUS-ID. Twelve prerequisites still refuse:
  - the standard controls G-BASELINE (9.3), G-CLAUSE (9.4), G-NFR8 (6.3), G-NFR18 (6.7) and G-NFR-OWNERSHIP (9.5 owner result) enroll as artifact approvals;
  - the unbound rows G-COMPAT (3.18), G-AUTH-HOSTS (5.11), G-RUNTIME-PARITY (3.15), G-PUBLICATION-AUTH (3.19), G-CONSUMER (3.20), G-MVP-COVERAGE (9.5) and G-APPEND (4.16, after D1) enroll once their owners bind them.

  Story 9.2 stays in progress until every enrolled path has authenticated use on its exact subject."
- **Rationale:** Makes the remaining 9.2 integration work explicit and ties each gap to an owner.
- **Owner:** Test owner, with the Product owner.
- **Dependencies:** None.
- **Acceptance evidence:** The list matches the readiness-decision spec's enrolled verifiers; the tracker row stays `in-progress`.

### 4.3 PRD (`prd.md`)

**P-3 · §12 OR4**

- **Before:** "The owner approved only the bounded atomic conflict-protection scope on 2026-11-01; a qualified supported Dapr capability and all final proof remain pending."
- **After:** Append: "On <D1 date> the owner selected capability path <A|B|C> with review trigger <trigger> ([proposal](sprint-change-proposal-2026-11-25.md)). No released tuple through daprd 1.19.2, components-contrib 1.19.2 and .NET SDK 1.19.1 supplies the mandatory contract ([4.16 recheck](…))."
- **Rationale:** OR4 currently records no route to closure.
- **Owner:** Product owner.
- **Dependencies:** D1 and P-1.
- **Acceptance evidence:** The OR4, G-APPEND row and Story 4.16 statements agree under the 9.3 comparison.

**P-9a · §12 OR14 and the §11.4 G-BASELINE row**, depending on D3

- **OR14, before:** "… Verify each earlier architecture obligation against the current spine, retain any still-open obligation, complete reviewer closure and obtain the required AD-26 ratification or approved replacement before epic input renewal. Then validate and approve one content-bound baseline. …"
- **OR14, after:** "… Verify each earlier architecture obligation against the current spine, retain any still-open obligation and complete reviewer closure. Before epic input renewal, obtain either per-role AD-26 ratification or the owner-approved AD-26 baseline disposition. A disposition binds AD-26 as an unratified `[ASSUMPTION]` at its current ratification subject digest. It never selects a production target, broker, runtime pin, restore posture or class (c) path. Ratification remains a prerequisite of G-PUBLICATION-AUTH, `production-promoted` and G-READINESS. Then validate and approve one content-bound baseline. …"
- **G-BASELINE row, after:** In the evaluator column, add: "A current AD-26 baseline disposition satisfies the AD-26 condition for this row only."
- **Rationale:** Resolves C3. Ratification cannot name qualified inputs that do not exist. Making planning-baseline approval wait for them blocks epic renewal for an unknown period and invites more drift. `READY` is not weakened, because G-APPEND and G-PUBLICATION-AUTH still fail.
- **Owner:** Product owner, with the architecture owner.
- **Dependencies:** D3. Do not edit AD-26 or anything else inside its ratification subject.
- **Acceptance evidence:** PRD, epics (P-9b) and the 9.3 fixtures agree; the subject digest stays `5070ebdc…`.

**P-10 · §10 SM11**

- **Before:** "**SM11 (not met, NFR7).** Three of five silent-loss classes are credited toward the target of five. Story 5.1 guards staged-state flush loss (a), …"
- **After:** "**SM11 (not met, NFR7).** No class currently has the production-path run, source revision, operating profile and result that AD-12 requires for credit. Stories 5.1 (a), 4.2 (b) and 4.4 (d) hold historical guards. Each needs a current production-path result before it counts. Class (c) remains open under OR4. …"
- **Rationale:** Resolves C4 and round-3 High finding 3 without weakening the metric (SM-C5).
- **Owner:** Product owner, with the persistence and Test owners.
- **Dependencies:** None.
- **Acceptance evidence:** SM11 and AD-12 agree; the round-4 validation (S5) records the finding as resolved.

**P-11 · Frontmatter and §0 lead: readiness and validation records**

- **Before:**
  - `implementation_readiness_last_assessed: 2026-10-06`, `implementation_readiness_last_baseline: a6fc951e…` and `prd_source_validation_report: …/validation-2026-11-20-post-update/validation-report.md`;
  - §0 lead: "The October 6 assessment rejected the baseline."
- **After:**
  - `implementation_readiness_last_assessed: 2026-11-25`, `implementation_readiness_last_baseline: 3475b097857b71d4cc25d53f0dce810e7a7fc928` and `implementation_readiness_last_report` pointing at the saved readiness findings;
  - the validation fields pointing at the S5 round-4 report;
  - §0 lead: "The 25 November sprint-planning assessment at `3475b0978` confirmed the October 6 Reject decision."
- **Rationale:** Resolves C5 and C6.
- **Owner:** Product owner.
- **Dependencies:** The readiness findings are saved to `implementation-readiness.md` (bmad-sprint-planning, with owner consent); S5 for the validation fields.
- **Acceptance evidence:** The frontmatter, §0 and the saved report agree.

### 4.4 Architecture

**P-7 · Architecture memlog, plus a new closure note under `architecture/architecture-eventstore-2026-07-05/reviews/`**

- **Before:** The memlog ends with the 21 November "(event) Update verified on the working tree. Spine SHA-256 bb2c8d20…" entry. No closure record exists for the current bytes `caa4d4c2…`.
- **After:**
  - Memlog line: "(AD-26-impact) 2026-11-22. NO. `831931885` changed the lead banner, the AD-35 rule and one implementation-status row for the McpCli AD-23 disposition. The ratification subject is unchanged at `5070ebdcf50d9fe7a7013be67d5c872e60563acf2ca4031382d0123754080803`."
  - Closure note: a table that disposes of all 13 findings of 21 November against `caa4d4c2…` as resolved or retained. Retained items are the OpenBao comparator floor (Security and Platform), the `UseNuGetDeps` alias (build owner) and the OQ8 README note (Story 4.17 owner). The spine stays `draft`.
- **Rationale:** Resolves C2 and satisfies AD-26's memlog rule. Step S1.
- **Owner:** Architecture owner.
- **Dependencies:** None. If D4 reverts the banners, the closure note is regenerated after P-8.
- **Acceptance evidence:** `lint_spine.py` reports zero findings; recomputed subject digest; `git diff --check` clean.

### 4.5 Tracker and matrix

- **`sprint-status.yaml`:** no edit. Every row keeps its current lifecycle value, and the validator already passes.
- **`phase-4-gate-risk-matrix.json`:** no edit by this proposal. Rows are bound by their owning stories (§2.3), and classification changes remain pinned in the validator.

## 5. Implementation handoff

**Scope: Moderate.** The work is backlog sequencing and owner decisions across existing stories. D1 escalates to **Major** (PM and Architect, through bmad-architecture and bmad-prd) if the owner selects path B or C.

| Role | Responsibility |
| --- | --- |
| Product owner (sole maintainer) | Decisions D1–D8; approval of the S6 source set; P-3, P-9a, P-10, P-11 |
| Architecture owner | S1 / P-7; D3 with the Product owner; bmad-architecture for any D1 path B or C amendment |
| EventStore persistence owner | P-1 (E1 screen); E2 upstream status; P-2 after D1; the upstream request if D1 = A and publication is approved |
| Story 3.17 and 3.21 owners | P-4, P-5 and P-6 slices. Platform executes candidates; Builds owns the pins. |
| Test owner | Story 9.3 (S4/S5), Story 9.4, the standard-control readiness verifiers, P-13 |
| Story 2.14 owner | P-12 adapter; Tenants package evidence |
| McpCli maintainer | D4 input; McpCli Story 5.11 closure |
| Platform and Builds maintainers | R-P1 and R-B1 when D2 calls for them; 1.19.2 candidate execution after D5 |

**Success criteria:**

- D1 recorded with E1–E4.
- S1–S7 complete, with the epic digests renewed on one approved source set.
- Every readiness prerequisite either enrolled or carrying a named refusal and owner.
- The Story 2.14 adapter in place.
- At least one live assured gate decision completed at `single-maintainer-attested` level.
- Phase 4 readiness stays Reject until §8's OR1 conditions hold.

**Routing (approved 2026-11-25).** Moderate scope, routed to the Product owner and Developer agents. The deliverables are this proposal and the implementation order below. No tracker lifecycle change is part of it.

| Wave | Edits | State after approval |
| --- | --- | --- |
| 1 | P-1, P-6a, P-6b, P-7, P-10, P-12, P-13 | Applied 2026-11-25 |
| 2 | P-5, P-9a, P-9b | Applied 2026-11-25. The AD-26 disposition record that S3 requires is the D3 entry in §7.1. |
| 3 | P-2, P-3 | Applied 2026-11-25, after P-1 |
| 4 | P-4 | EventStore intake applied 2026-11-25 after P-1 and D5. Hosted broker request pending Platform acceptance. |
| 5 | P-8 | After McpCli Story 5.11 leaves review (D4) |
| 6 | P-11 | After the readiness findings are saved and S5 is complete |

The upstream request (D6) is not part of any wave. It is filed only after the owner reviews its exact text.

### 5.1 Applied edits and their checks (2026-11-25)

| Edit | File | Change |
| --- | --- | --- |
| P-1 | Story 4.16 spec | New section "Capability Recheck — 1.19.2 Tuple (2026-11-25)" with the exact tuple, the support-table state, seven unmet contract rows, the D1 quote and the fetch commands |
| P-6a | Story 3.17 spec | New section "Corrective Scope Authorization — 2026-11-25"; `updated` set to 2026-11-25; status `backlog` kept |
| P-12 | Story 2.14 spec | Unchecked NFR2 adapter task added to Tasks & Acceptance |
| P-7 | Architecture memlog and `reviews/review-validate-2026-11-21-closure.md` | AD-26-impact NO entry for `831931885`; closure table for all 13 findings, with 10 resolved and 3 retained with owners |
| P-2, P-5, P-6b, P-9b, P-13 | `epics.md` | Story 4.16 and 3.17 reconciliations, the Story 3.21 candidate target, the Story 9.3 dependency and AC1 exception, and the Story 9.2 readiness binding order |
| P-3, P-9a, P-10 | `prd.md` | OR4 D1 record, OR14 and the G-BASELINE row baseline disposition, SM11 correction; `updated` and `source_artifacts` refreshed |
| P-4 | [Story 3.21 spec](../implementation-artifacts/spec-3-21-production-dapr-runtime-pin-and-durable-broker-qualification.md#1192-candidate-intake--2026-11-25) and [1.19.2 lock](../../deploy/dapr/qualification/runtime-lock-1.19.2.json) | Applied 2026-11-25. Recorded the unresolved AC1 disposition, verified Linux amd64/arm64 archive checksums and registry digests, and documented Builds' SDK/CLI pins. The hosted broker request is pending Platform acceptance. The lock has no selected broker and is ineligible for production. Story 3.21 stays `in-progress`. |

Checks run on the working tree after the edits:

- `git diff --check` exited 0.
- The spine lint reported zero findings.
- `architecture.md` is unchanged at `caa4d4c2…`, and the recomputed AD-26 ratification subject is still `5070ebdc…`.
- The frozen blocks of the 4.16 and 2.14 specs are byte-identical to HEAD, and the 4.16 spec changed only by appending.
- No FR or NFR table row changed in the PRD or the epics.
- All 154 added lines and links were checked, and every local link and anchor resolves.
- `sprint_plan.py validate` returned `valid: true`.
- The risk-matrix validator still exits 1 with the same unbound and unsealed rows.
- The Story 2.14 completion validator now refuses with `SOURCE_MISMATCH`, because `prd.md` differs from the committed copy. It is expected to return to `ADAPTER_UNAVAILABLE` once these edits are committed. That has not been verified.

The edits are not committed, and the epic input digests are not renewed (S7).

## 6. Earliest corrective work under AD-12

Each item needs only the owner's scope record in its owning spec (PRD §0). The evidence is the spec's Verification section plus CI, as AD-12 requires.

1. **E1 source screen** (P-1, and the shared P-4 disposition). Read-only, about half a day.
2. **Architecture closure** (S1 / P-7). About half a day.
3. **Story 9.4 all-clauses validator**, with a truthful first FAIL.
4. **Story 9.3 drift guard and manifest lane** (S4), with a truthful first FAIL. Its requirement and clause checks should reproduce today's 56/56 and 49/49 agreement.
5. **Story 2.14 NFR2 adapter** (P-12).
6. **Readiness verifiers for the five standard controls** (Story 9.2).
7. **Story 3.17 module inventory and guard** (P-6), including the actor-state write-path rows.
8. **PRD SM11 correction** (P-10). This is a text fix that needs only the Product owner's approval.

Owner actions that are not corrective builds but can happen now: re-attest G-NFR18 (OR6), and save the readiness findings (P-11).

## 7. Decisions requiring owner approval

| ID | Decision | Recommendation |
| --- | --- | --- |
| D1 | G-APPEND capability path: A (upstream), B (private build) or C (AD-34 provider exception), with a review trigger | A with a review trigger, and B and C explicitly declined for now. Accept that `READY` has no forecast date while A is pending. |
| D2 | Issue R-E1 for PLAT-ES416-QH-Q2-R4 now, or defer | Defer until D1 yields a candidate. If D1 = B, issue R-P1, R-B1 and R-E1 together. |
| D3 | AD-26 for baseline purposes: per-role ratification, or an approved replacement (baseline disposition) | The baseline disposition (P-9) |
| D4 | McpCli AD-23 disposition: adopt in epics, or return the banners to candidate wording | Adopt once McpCli Story 5.11 leaves review, and relabel the banners |
| D5 | Story 3.21 candidate target: runtime 1.19.2, contrib 1.19.2, CLI 1.19.1, SDK 1.19.1, with a Platform candidate request | Approve as a non-promoting candidate |
| D6 | Publish the upstream capability request to the Dapr projects (outward-facing) | Approve if D1 = A, after reviewing the exact text |
| D7 | Timing of the first live assured decisions for G-OQ8, G-STATUS-ID and later G-TENANT | After a 24-hour stable subject; G-TENANT after P-12 and Tenants package evidence |
| D8 | Approve this proposal and authorize applying P-1 to P-13 | Approve P-1, P-6, P-7, P-10, P-12 and P-13 now; apply the others after their decisions |

### 7.1 Approval record

On 2026-11-25 the owner, as sole maintainer, approved this proposal and every recommendation in the table above as written. The approved draft is commit `c8c09d2b6` (proposal SHA-256 `bb64e550…4e84`). At that commit the planning inputs, tracker, matrix, registry and `tools/` are byte-identical to `3475b0978`.

| ID | Recorded decision |
| --- | --- |
| D1 | Path A: request the mandatory capability from upstream Dapr. Paths B and C are declined for now. The review trigger is the first Dapr 1.20 release candidate, the example in §2.1 E4. The owner may substitute a date when P-3 is applied. G-APPEND and `READY` have no forecast date while path A is pending. Scope stays Moderate. |
| D2 | R-E1 is deferred until D1 yields a candidate. |
| D3 | The AD-26 baseline disposition (P-9) replaces per-role ratification for G-BASELINE only. **Disposition record:** the owner binds AD-26 as an unratified `[ASSUMPTION]` at ratification subject `5070ebdcf50d9fe7a7013be67d5c872e60563acf2ca4031382d0123754080803`, which the push of 2026-11-21T11:33:17Z last changed, for G-BASELINE only. It selects no production target, broker, runtime pin, restore posture or class (c) path, and it lapses if the subject digest changes. The Story 9.3 manifest consumes this record (S4). |
| D4 | AD-23 is adopted in the epics once McpCli Story 5.11 leaves review, and the banners are relabelled then (P-8). |
| D5 | The Story 3.21 target is runtime 1.19.2, contrib 1.19.2, CLI 1.19.1 and SDK 1.19.1, as a non-promoting candidate with a Platform candidate request. |
| D6 | Approved in principle. Nothing is filed until the owner reviews the exact request text. |
| D7 | G-OQ8 and G-STATUS-ID get their first live assured decisions after 24 hours with their subject unchanged. G-TENANT follows P-12 and the Tenants package evidence. |
| D8 | The edits are authorized in the waves listed in §5. Waves 1–3 were applied on 2026-11-25. |

This approval is a planning decision under PRD §0 and AD-12. It is not an AD-11 seal or attestation and it is not AD-26 ratification. It changes no gate result and no tracker entry. Phase 4 readiness remains Reject.

## 8. Conditions for rerunning readiness

These are two different reruns.

**R1 · Sprint-planning gate rerun.** This checks whether a developer can implement the epics without inventing decisions. It can move from FAIL to CONCERNS once:

- D1 is recorded and P-2 and P-3 are applied, so G-APPEND has a decided, owned path even though it still fails;
- C1–C6 are resolved: S1–S3 are complete and P-8, P-10 and P-11 are applied;
- the 9.3 guard and 9.4 validator exist and report their truthful results;
- the Story 2.14 adapter exists;
- no planning input changed after the bytes the rerun examines, and those bytes are committed.

R1 does not require any gate to pass, and Phase 4 readiness stays Reject.

**R2 · OR1 final assessment.** The Product owner may start it only when all of the following hold:

1. S6 and S7 are complete: one clean, approved source set with renewed epic digests, and G-BASELINE passing on that revision.
2. The blocking refinements OR4, OR6, OR7, OR8, OR10, OR13, OR14, OR15, OR17, OR20–OR24 and OR27–OR29 are closed.
3. All 15 prerequisite gates pass on that same baseline. Each of the nine high-risk prerequisites has its matrix binding, a sealed run of its dedicated workflow on the exact head, and an owner attestation at least 24 hours after the authenticated subject-changing push.
4. AD-26 is ratified per role. This is needed through G-PUBLICATION-AUTH and G-APPEND even with a baseline disposition.
5. The readiness request binds the 15 results, the 9.2 consumption authenticates the completed attempt, G-HIGH-RISK is computed, and G-READINESS is recorded at `single-maintainer-attested` level before `READY` is derived.

Item 3 depends on G-APPEND, so R2 is not schedulable until D1 delivers a qualified capability. Any change to a bound requirement, artifact, evidence item, lifecycle, approval or scope returns the aggregate to Reject.

## 9. Verification performed for this proposal

All commands ran from the repository root at `3475b0978`, and the results were confirmed unchanged at `3e5ed0a85`. They are read-only except for this file. At approval, `git diff --stat 3e5ed0a85 c8c09d2b6` lists only this proposal and one test file (`DevelopmentRouteCatalogBuilderTests.cs`), so the results still hold.

| Command or check | Result |
| --- | --- |
| `python3 -B tools/validate-phase-4-gate-risk-matrix.py _bmad-output/implementation-artifacts/phase-4-gate-risk-matrix.json` | Exit 1. The 12 high-risk rows FAIL with `SEAL_MISSING`, `ATTESTATION_MISSING` and `ACHIEVED_BELOW_REQUIRED`, and seven of them also have `BINDING_UNBOUND`. The five standard controls PASS classification. Registry: 1 human. |
| `python3 -B tools/validate-phase-4-story-completion.py --spec …spec-2-14-canonical-tenant-boundary.md --target spec`, and the same with `--target tracker` | Both exit 1 with `ADAPTER_UNAVAILABLE` for NFR2 |
| `uv run .claude/skills/bmad-sprint-planning/scripts/sprint_plan.py validate --status-file …/sprint-status.yaml` | `valid: true`, zero problems |
| `sha256sum` of the five epic inputs, `epics.md`, the tracker, the matrix and the registry | Values as in §2.2. `epics.md` `d1a7554f…`, tracker `08d6a4e5…`, matrix `45ea3a30…`, registry `8157727a…` |
| In-memory comparison of PRD §6–§7 tables with the epics Requirements Inventory, and of PRD §7.1 with the epic clause ledger | 56/56 definitions identical; 49/49 clause rows agree on primary and supporting owners |
| AD-26 ratification subject recomputed over the last 25 architecture commits | Current `5070ebdc…`, unchanged by `831931885`. It reproduces the recorded `b9e899ec…` (1 November) and `d49f8a06…` (9–10 November) subjects. |
| `gh api repos/Hexalith/Hexalith.EventStore/activity` (read-only) | The push moving `main` from `a96015825` to `2b8895f18` occurred at 2026-11-21T11:33:17Z |
| `git ls-remote` for dapr/dapr, dapr/components-contrib and dapr/dotnet-sdk tags; GitHub compare and release APIs; NuGet index for `Dapr.Actors` | Identities and file-level changes as in §2.1; `Dapr.Actors` 1.19.1 published |
| `git diff --name-only 3475b0978 3e5ed0a85` restricted to planning, tracker, matrix and `tools/` paths | Empty |

The Story 9.2 transition fixture suite was started for reference and stopped after 10 minutes without a result. This proposal relies on the 22 November readiness-decision spec's recorded run instead of claiming a new one.

## Appendix: checklist record

| Item | Status | Note |
| --- | --- | --- |
| 1.1–1.3 Trigger, problem, evidence | [x] | §1 |
| 2.1–2.5 Epic impact | [x] | §2.4. No epic added, removed or reordered. |
| 3.1 PRD | [!] | P-3, P-9a, P-10, P-11 |
| 3.2 Architecture | [!] | P-7; AD-26 deliberately untouched |
| 3.3 UX | [!] | Banner label only (P-8) |
| 3.4 Other artifacts | [!] | Story 2.14 adapter, 9.3/9.4 lanes, readiness verifiers; CiCd boundary respected |
| 4.1 Direct adjustment | Viable | Recommended |
| 4.2 Rollback | Not viable | Discards evidence, removes no blocker |
| 4.3 MVP review | Viable only for D1 B or C | Major if selected |
| 4.4 Selected path | [x] | Direct adjustment with D1 |
| 5.1–5.5 Proposal components | [x] | §1–§5 |
| 6.1–6.2 Review | [x] | Rechecked at both revisions |
| 6.3 Owner approval | [x] | Approved 2026-11-25, D1–D8 as recommended (§7.1) |
| 6.4 Tracker update | [N/A] | No lifecycle change proposed |
| 6.5 Handoff confirmation | [x] | Moderate; routed to the Product owner and Developer agents (§5) |
