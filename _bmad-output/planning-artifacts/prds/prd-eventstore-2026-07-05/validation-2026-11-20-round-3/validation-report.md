# EventStore Phase 4 PRD validation

**BMAD grade · Fair**

**Implementation readiness · Blocked (Reject)**

Prior review · 16 resolved, 3 partially resolved, 0 open. Current review · 0 critical, 6 high, 5 medium, 2 low.

Reviewed 2026-11-20T18:26:27+01:00 on the working tree of EventStore `main` at `db5db24707a2468e69cf86016c3bba2f3fb5d516`. PRD SHA-256 `4644f2b282a3ee7ff860869fd49a620cd32316284cdb594b32d45a6fea663a4b`. Epic plan SHA-256 `9d488d02e056d656b33c2316cde4c06a00dbc424ce9ea50172c8ad5ecbbcda76`. [PRD](../../../../../_bmad-output/planning-artifacts/prd.md) · [Epics](../../../../../_bmad-output/planning-artifacts/epics.md) · [Prior validation](../../../../../_bmad-output/planning-artifacts/prds/prd-eventstore-2026-07-05/validation-2026-11-20-post-update/validation-report.md) · [October readiness decision](../../../../../_bmad-output/planning-artifacts/implementation-readiness.md)

## Overall verdict

The PRD supports continued corrective work. The latest update fixed the deferred Admin response rules, the SM10 denominator, and the specific story assignments flagged in the prior review. Three prior findings remain partially resolved: the first readiness seal, the Production JWT exception in Story 5.3, and the evidence underlying the SM11 count.

The broader review found additional discrepancies between the PRD, governing specifications, and detailed stories. An accepted command can still be described as rejected. FR7 clause ownership and the authorization needed to start Story 9.4 also depend on which document a team reads. The October 6 readiness rejection remains in force.

## Rubric judgments

| Dimension | Judgment | Basis |
| --- | --- | --- |
| Decision-readiness | adequate | The stop decision, the 15 business gates and the publication order are clear. The first sealed readiness evaluation still lacks a defined input set. |
| Substance over theater | strong | The PRD specifies observable outcomes, including subscriber effect counts, deferred Admin responses and the negative tests for protected surfaces. |
| Strategic coherence | adequate | Adoption and operational risk have distinct measures. The loss-prevention numerator still requires evidence for each credited class. |
| Done-ness clarity | adequate | Stable clauses define most outcomes precisely. Production JWT startup, accepted command responses and tenant-free requests still receive inconsistent instructions across handoffs. |
| Scope honesty | strong | The approved Dapr append protection, deferred gateway GET decision and post-MVP Epic 8 each have explicit scope and separate approval boundaries. |
| Downstream usability | thin | The two requirement inventories match, but individual stories conflict with the ledgers on responsibility, evidence and prerequisites for mandatory work. |
| Shape fit | adequate | Named journeys, clause tables and a decision register suit a brownfield platform. Several active summaries need to point to the current authority. |

## Findings by severity



### Critical (0)

None.

### High (6)

**The first readiness seal has no defined inputs** · PRD §11.4 and OR1, lines 723, 743, 747 and 809

G-HIGH-RISK needs authenticated evidence for the readiness transition. G-READINESS consumes a passing G-HIGH-RISK result. The transition verifier checks prerequisites before issuing the evaluation that can later be authenticated. Starting with 15 passing business and coverage gates, the document never identifies an evaluation that can produce the first seal.

**Correction.** Define the provisional evaluation, its exact inputs, and the artifact the assurance check can authenticate. Specify how the final writer consumes all 16 results. Test that the first valid decision can complete without a prior readiness seal. [Transition evaluator](../../../../../tools/phase_4_transition.py) · [Risk matrix](../../../../../_bmad-output/implementation-artifacts/phase-4-gate-risk-matrix.json)

**Story 5.3 permits a Production configuration the PRD rejects** · PRD §6.5 and NFR3; Epic Story 5.3, lines 4476 and 4485–4493

The story calls its own acceptance criteria authoritative and permits the symmetric-key override “outside Development.” Production falls within that wording. The ratified NFR, frozen Story 5.3 specification and shared JWT implementation all require Production startup to fail.

**Correction.** Give Production its own unconditional startup-refusal case. Exercise the override in an explicitly permitted non-Production environment, with its normal issuer, audience, signature and lifetime checks. [Frozen JWT specification](../../../../../_bmad-output/implementation-artifacts/spec-5-3-production-authentication-guards-and-secret-stripping.md) · [Shared JWT validator](../../../../../src/Hexalith.EventStore.ServiceDefaults/Authentication/JwtBearerAuthenticationContract.cs)

**SM11 credits staged-state recovery without the required production-path evidence** · PRD NFR7 at line 405 and SM11 at line 584; Story 5.1 specification, line 431

SM11 credits class (a) toward three of five prevented loss classes. Story 5.1 identifies a deterministic in-memory state manager as its completion oracle. The cited result establishes a guard and its behavior in that test. It does not demonstrate prevention through the process, sidecar and durable component that NFR7 requires for current credit.

**Correction.** Keep the historical Story 5.1 result. For every class counted as currently qualified, bind the qualifying run, source revision, operating profile and result. Report the qualified count as unverified until those records exist. [Story 5.1 evidence](../../../../../_bmad-output/implementation-artifacts/spec-5-1-infrastructure-failure-cache-clear.md)

**An accepted command can be reported to the caller as rejected** · PRD UJ2, FR12 and FR12-C3; Epic Story 2.15 at line 2182

The PRD permits safe rejection after command acceptance when the response lacks a usable MessageId. The frozen Story 2.15 contract preserves HTTP 202 and `Retry-After: 1` and omits `Location`. The epic also allows a caller’s version claim to suppress the link even when the declared version and the returned ID yield a valid URI. Either outcome can mislead a caller about a successful submission.

**Correction.** For accepted commands, require 202 and preserve Retry-After. Omit the link for a missing or invalid identity or unusable declaration. Ignore caller version claims and use the endpoint’s declared grammar for valid IDs. Keep submission failures as a separate case. [Frozen status-link contract](../../../../../_bmad-output/implementation-artifacts/spec-2-15-messageid-only-command-status-identity.md) · [Generated response tests](../../../../../tests/Hexalith.EventStore.RestApi.Generators.Tests/RestApiGeneratedControllerErrorSemanticsTests.StatusIdentity.cs)

**FR7 clause owners disagree with the detailed stories** · PRD §7.1, lines 436–438; Epics lines 1091, 1202, 1258 and 1314

Both ledgers assign FR7-C3/C4/C5 to Stories 1.15/1.18/1.19. The first story omits FR7, the other two declare supporting roles, and Story 1.17 claims primary FR7. A team can complete the technical work and still have two incompatible answers about who closes it.

**Correction.** Confirm the disjoint clause assignments, update each story declaration and make the Story 9.4 check compare the detailed declarations with both ledgers. [Clause ledger and story declarations](../../../../../_bmad-output/planning-artifacts/epics.md)

**Mandatory corrective stories still require retired authorization tooling** · PRD §0 and OR28; Architecture line 561; Epic Stories 3.19, 5.13, 7.21 and 9.4

AD-12 assigns scoped corrective authorization to the owning spec. The architecture and several epic dependencies still require a Story 9.1 record. Story 9.4 even requires Story 9.1 to pass, although its validator and workflow were retired. The G-CLAUSE owner receives incompatible instructions about when work may start.

**Correction.** Point active dependencies to the scope, owner, subject and CI evidence recorded in each owning spec. Label retained Story 9.1 instructions as historical and preserve the independent assurance requirements. [Evidence policy](../../../../../_bmad-output/implementation-artifacts/spec-evidence-storage-simplification.md) · [Architecture handoff](../../../../../_bmad-output/planning-artifacts/architecture.md)

### Medium (5)

**The tenant instruction omits cataloged platform operations** · PRD UJ2 at line 193 and §8.2 at line 494; NFR2, FR12-C2 and Story 2.14

UJ2 and §8.2 each require one explicit authorized tenant for generated requests. The approved platform-operation case authorizes an operation and credential without a tenant. A conforming global request therefore cannot satisfy the general instruction literally.

**Correction.** Limit the one-tenant requirement to tenant-scoped requests. Describe the authorized platform scope and its rejection of mixed scopes alongside it. [Authorized scope cases](../../../../../_bmad-output/implementation-artifacts/spec-2-14-canonical-tenant-boundary.md)

**Story 9.4 compares FR15 against a ledger with no FR15 rows** · PRD §7.1 and §11.1; Epics lines 327–381, 475 and 8162

The validator story asks for FR15’s completion rule to name the same slices as the stable-clause ledger. FR15’s six approved slices live in §11.1 and the epic completion rule. The closed 49-row ledger contains no FR15 clause.

**Correction.** Point the FR15 comparison to §11.1 and the six-way epic completion rule. Retain the stable-clause comparison for FR12 and FR36. [Story 9.4 and FR15 completion rule](../../../../../_bmad-output/planning-artifacts/epics.md)

**The architecture still prescribes a check that blocks main** · PRD glossary line 224; Architecture line 567; October 7 architecture-routing decision

The architecture’s active status table calls for one blocking required check on `main`. The owner-approved rule binds each seal to its guarded transition. A branch-wide blocking implementation would also prevent corrective changes from landing while the gates are red.

**Correction.** Replace the active branch-check instruction with the per-transition rule and cite the October 7 decision. [October 7 owner decision](../../../../../_bmad-output/planning-artifacts/sprint-change-proposal-2026-10-07-architecture-routing.md)

**The Admin correlation-ID handoff omits an observed failing case** · PRD FR26-C4 at line 446 and §8.2; Epic Story 5.4, lines 4549–4552

The story calls for an incoming canonical ULID to survive the Admin host. The registered correlation middleware uses `Guid.TryParse` and generates a GUID on a miss. The epic ledger still shows the evidence as pending, but the handoff gives the owner no indication of this concrete failing case.

**Correction.** Record the observed mismatch against FR26-C4, preserve the historical story result, and assign the host-level correction and regression proof needed before clause closure. [Registered middleware](../../../../../src/Hexalith.EventStore.Admin.Server.Host/Program.cs) · [Admin correlation handling](../../../../../src/Hexalith.EventStore.Admin.Server.Host/Middleware/CorrelationIdMiddleware.cs)

**The FR4 closure check omits conditional-query evidence** · PRD §6.1, line 271, and FR4-C3 at line 427; Epic Story 2.18

The clause tests lossless ETag propagation. A response can propagate a tag intact even when it belongs to a later revision. Story 2.18 owns the correct-revision and wrong-revision cases, but neither closure ledger names that contribution.

**Correction.** Bind Story 2.18’s stale-tag refusal and matching-tag success to FR4 closure or to a mandatory MVP manifest entry. Keep the existing primary owner. [Story 2.18 and FR4 ledger](../../../../../_bmad-output/planning-artifacts/epics.md) · [November 20 audit decision](../../../../../_bmad-output/planning-artifacts/sprint-change-proposal-2026-11-20.md)

### Low (2)

**Epic 3’s overview sends an active qualification story back to backlog** · Epics line 404; sprint tracker line 152; Story 3.21 spec line 5

The overview describes Story 3.21 as backlog. Its tracker and parent specification both say in progress. The difference matters to the release owner coordinating the Dapr and broker qualification work.

**Correction.** Update or date the overview entry and link it to the current tracker or parent specification. [Story 3.21 qualification spec](../../../../../_bmad-output/implementation-artifacts/spec-3-21-production-dapr-runtime-pin-and-durable-broker-qualification.md) · [Sprint tracker](../../../../../_bmad-output/implementation-artifacts/sprint-status.yaml)

**The architecture still lists all OQ8 hosted evidence as pending** · Architecture AD-25 at line 338 and implementation-status table at line 584; PRD §1.1

Two committed revisions have successful hosted fixtures and live availability checks. The architecture’s blanket pending status obscures which committed inputs were checked and which candidate still needs a new run.

**Correction.** Point the current architecture status to the revision-specific evidence in §1.1 and keep the separate assured-decision requirement explicit. [Architecture handoff](../../../../../_bmad-output/planning-artifacts/architecture.md) · [Committed PRD run](https://github.com/Hexalith/Hexalith.EventStore/actions/runs/44775644088)

## Disposition of all 19 prior findings

| # | Prior finding | Disposition | Evidence and boundary |
| --- | --- | --- | --- |
| 1 | Order of the final readiness decision | Partially resolved | PRD §11.4 and OR1 put 16 results before G-READINESS and G-READINESS before READY. The contract still needs an input set for the first authenticated readiness evaluation. |
| 2 | Production JWT override | Partially resolved | PRD §6.5 and NFR3 forbid symmetric mode in Production. Story 5.3 still admits an HS256 override “outside Development.” The frozen specification requires Production startup to fail. |
| 3 | Deferred Admin operations | Resolved | FR34, NFR15, FR34-C4, §8.3 and Story 7.4 put authentication, scope and input checks before a typed 501 with no side effects. UX hides restore/import and shows backup/compaction as read-only. |
| 4 | Four primary story assignments and NFR17 roles | Resolved | The named FR4 and FR34 clauses each have one accountable story. NFR17-C1/C3/C4 agree with §11.2 and the detailed stories. The separate FR7 discrepancies are current findings. |
| 5 | SM11 credit for duplicate effects | Partially resolved | SM11 removes the historical class (e) credit and keeps (c) open. Its remaining numerator still includes class (a) without binding qualifying production-path evidence. Story 5.1 records an in-memory completion oracle. |
| 6 | Existing package compatibility check | Resolved | G-COMPAT, OR22 and Story 3.18 credit the signed 4.2.0 baseline comparison for all 14 package families. Wire, generated surface, application and release evidence remain due. |
| 7 | Tenants Production authentication implementation | Resolved | G-AUTH-HOSTS and OR23 describe the shared Production validator and the local non-Production path at Tenants `3bb4b818`. Host inventory, fingerprints, negative tests and approvals remain due. |
| 8 | Dependency version for the license decision | Resolved | OR30 distinguishes the historical MediatR 14.2.0 review from 15.0.0 in Builds and the restored Server/Gateway assets. The release owner must bind the product owner’s disposition to the candidate. |
| 9 | SM10 coverage denominator | Resolved | SM10 and Story 9.5 measure protected surfaces in the MVP production profile. Permitted anonymous checks and excluded UI surfaces are accounted for separately. |
| 10 | Historical Story 6.5 mappings | Resolved | The repository reader recovers NFR7 for 6.5c and NFR7/NFR12 for 6.5d through §11.2.2. Story 6.5 retains its numeric approval key and its four NFRs. |
| 11 | Durable consumer and coverage records | Resolved | §8.1, G-CONSUMER, G-MVP-COVERAGE, OR24 and OR27 bind the CI-generated inventories and digests to the owning spec and run. Story 3.20 and Story 9.5 match. |
| 12 | Committed OQ8 availability runs | Resolved | §1.1, G-OQ8 and OR11 identify the fixture and live successes on committed revisions and distinguish the skipped assured decision. A separate architecture status entry still needs correction. |
| 13 | Repeated owner assignment and tracker repair | Resolved | OR7/OR24 recognize the assignments to 3.19, 3.20 and 7.21. OR19 records Story 2.12 as done under its full key and Epic 2 as in progress. |
| 14 | November stories and lifecycle in the overviews | Resolved | Epic 2 reaches 2.19, Epic 5 reaches 5.15 and Epic 7 reaches 7.23. All four additions are in backlog. Stories 2.14 and 2.15 agree with the tracker. |
| 15 | Tenants host wiring and UJ1 | Resolved | UJ1 puts the outstanding host plumbing under FR9/SM8 and the affected FR1 guardrails. Other requirements close against their own evidence. |
| 16 | UX design status | Resolved | §3.3 points to §11.3, which records DESIGN and EXPERIENCE as final. Common-baseline approval remains outstanding. |
| 17 | Existing high-risk story-completion control | Resolved | OR13 names the completion writer and the bounded 5.3 adapter for NFR3/NFR4. Other affected stories still need their business adapters and exact evidence. |
| 18 | Generated links versus gateway GET lookup | Resolved | FR12-C4, §8.2, OR21 and Story 2.15 distinguish generated MessageId-only links from the separately owned GET compatibility decision. A different defect concerns the HTTP result after command acceptance. |
| 19 | Story 4.16 append protection in the MVP | Resolved | §6.4 and G-APPEND distinguish the Story 4.5 observation, the approved Story 4.16 Dapr conflict guard and general activation fencing. Prevention evidence remains outstanding. |

## Verification

- Compared all 37 FR and 19 NFR definitions and the 49 stable clause IDs, primary stories and supporting lists. Both ledgers match. All 49 epic evidence identities are still pending. The reader accepted 143 of 146 story headings, including all four historical Story 6.5 contributors. The three recorded exceptions are superseded Stories 3.22 and 3.23 and historical evidence ledger 4.8.
- All 80 PRD source references exist. All five content digests recorded as epic inputs differ from the working files. Baseline reconciliation and owner approval remain outstanding.
- Ran 305 focused tests covering the gate matrix, publication authority, release API compatibility, OQ8 design availability and the story coverage reader. All passed. The offline OQ8 validator also passed.
- The live gate-matrix command exited 1 on the reviewed PRD bytes. It enumerated 17 mandatory gates. Every one of the 12 high-risk rows reported achieved assurance `none`, with both the CI seal and the owner attestation missing. The live publication evaluator also exited 1 with `authority: unproven` and a null seal.
- GitHub reported successful fixture and live availability jobs on committed revision `d677fba671`; the assured-decision job was skipped. On the synchronized main revision `db5db2470`, the live availability job had succeeded, the fixture was queued and the assured decision was skipped at the time checked. These runs apply only to their identified commits. [Committed PRD run](https://github.com/Hexalith/Hexalith.EventStore/actions/runs/44775644088) · [Synchronized main run](https://github.com/Hexalith/Hexalith.EventStore/actions/runs/44780280952)
- The prior findings are tracked at the scope of their original correction. Finding 5 includes the instruction to bind the numerator to its evidence. The historical duplicate-effects credit was removed, but the remaining class (a) credit lacks that binding. The original four named ownership assignments in finding 4 are corrected. The FR7 ownership conflict is a separate finding.

## Reviewer files

- [PRD quality rubric](review-rubric.md) · Seven dimensions, with supporting evidence for the SM11 count, Admin correlation IDs and conditional-query acceptance.
- [Readiness and evidence](review-readiness.md) · The first readiness seal, signed package baselines, dependency licensing, OQ8 runs, and current versus retired authorization rules.
- [Product acceptance](review-acceptance.md) · Frozen specifications, generated HTTP status behavior, tenant scope, Production JWT startup and deferred Admin operations.
- [Traceability](review-traceability.md) · Detailed story declarations, both ledgers, the FR15 comparison and the story reader’s full inventory.

[Rubric](../../../../../.agents/skills/bmad-prd/assets/prd-validation-checklist.md)
