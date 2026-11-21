# EventStore architecture validation

**21 November 2026 · Phase 4 · Full-spine review**

> **Changes required before baseline approval.** All 36 decision entries pass the structural lint. Four independent reviews found **8 high, 4 medium and 1 low** distinct findings, with no critical findings. Phase 4 readiness remains **Reject**. The publication evaluator reports no authority or seal.

## High-priority findings

### 1. The credential rules reject an approved platform command

**AD-18 and AD-36, lines 285–288 and 432.** The sending and receiving rules allow a single credential kind. Story 2.14 records a narrowly scoped platform command that requires a workload assertion and separately validates the forwarded human identity against the bound subject. Implementations following the spine would reject this approved operation. Document the exact route and its two independent checks in the outbound, inbound and catalog rules. Preserve single-kind admission for ordinary operations. [Story 2.14](https://github.com/Hexalith/Hexalith.EventStore/blob/df5dc226af69292b13dc0676e408e56081e0ff2a/_bmad-output/implementation-artifacts/spec-2-14-canonical-tenant-boundary.md#L120) · [Command handler](https://github.com/Hexalith/Hexalith.EventStore/blob/df5dc226af69292b13dc0676e408e56081e0ff2a/src/Hexalith.EventStore/Controllers/CommandsController.cs#L118)

### 2. A protected app channel needs the receiver token in Development

**AD-28, line 380.** The spine mandates token validation outside Development. The frozen Story 5.14 contract and the shared validator require a real receiver token on channel-protected routes in every environment. A Development host and its caller can therefore disagree before any business operation runs. Align AD-28 and the environment guidance with the shared validator and its Development tests. [Story 5.14](https://github.com/Hexalith/Hexalith.EventStore/blob/df5dc226af69292b13dc0676e408e56081e0ff2a/_bmad-output/implementation-artifacts/spec-5-14-authenticated-fallback-and-dapr-framework-route-protection.md#L31) · [Channel validator](https://github.com/Hexalith/Hexalith.EventStore/blob/df5dc226af69292b13dc0676e408e56081e0ff2a/src/Hexalith.EventStore.ServiceDefaults/Authentication/DaprAppChannelToken.cs#L49)

### 3. Authentication alone cannot authorize an unclassified endpoint

**AD-16, line 261.** “Authenticated fallback” admits an interpretation in which any signed-in user can reach a route lacking policy metadata. Story 5.14 and the installed fallback deny an unclassified endpoint for every caller. Specify that default explicitly, alongside named route policies and the enumerated anonymous exceptions. Verify the negative case with an authenticated caller. [Story 5.14 acceptance cases](https://github.com/Hexalith/Hexalith.EventStore/blob/df5dc226af69292b13dc0676e408e56081e0ff2a/_bmad-output/implementation-artifacts/spec-5-14-authenticated-fallback-and-dapr-framework-route-protection.md#L37) · [Host policy registration](https://github.com/Hexalith/Hexalith.EventStore/blob/df5dc226af69292b13dc0676e408e56081e0ff2a/src/Hexalith.EventStore.ServiceDefaults/Authentication/EventStoreHttpHostSecurityExtensions.cs#L41)

### 4. The OpenBao floor predates published security fixes

**AD-24/AD-26 stack entry, line 469.** Both the spine and profile comparator accept the October floors of `2.7.1` and `2.6.4`. Two October 26 advisories identify `2.7.2` and `2.6.5` as the first patched releases for those advisories. OpenBao published 2.7.3 and 2.6.6 on November 19. Security and Platform should review the supported branch and candidate, then align the spine and profile check with the approved floor. The production profile still requires ratification. [Profile comparator](https://github.com/Hexalith/Hexalith.EventStore/blob/df5dc226af69292b13dc0676e408e56081e0ff2a/src/Hexalith.EventStore.Contracts/Deployment/ProductionProfileValidator.cs#L501) · [October advisory 1](https://github.com/openbao/openbao/security/advisories/GHSA-5pmx-2p74-x478) · [October advisory 2](https://github.com/openbao/openbao/security/advisories/GHSA-q824-44ff-f5j5) · [Release 2.7.3](https://github.com/openbao/openbao/releases/tag/v2.7.3) · [Release 2.6.6](https://github.com/openbao/openbao/releases/tag/v2.6.6)

### 5. The erasure verifier has conflicting key-custody rules

**AD-24 and the Story 4.16 handoff, lines 326, 576 and 600.** AD-24 assigns dedicated keys to internal proofs and includes the erasure capability. The approved Story 4.16 integration uses existing HMAC key generations and explicitly treats a verifier holding them as signing-capable. The two rules imply different trust and rotation boundaries. Name the present custody model, the approved 4.16 scope, and the transition to any future dedicated keys. Keep key distribution gated on qualification and owner-controlled migration. [Story 4.16 approved integration](https://github.com/Hexalith/Hexalith.EventStore/blob/df5dc226af69292b13dc0676e408e56081e0ff2a/_bmad-output/implementation-artifacts/spec-4-16-append-write-once-conformance-through-enforced-envelope.md#L87)

### 6. The assurance rule conflates required and achieved independence

**AD-11 and the approval convention, lines 226 and 442.** Roster size sets the required assurance level. Achieving independent approval also requires an authorized human whose numeric identity differs from the author’s. The 24-hour interval starts at the authenticated push to `main` that last changed the bound subject. “Last authored change” can be days earlier. State both checks in the common rule and have downstream records consume the achieved level. [PRD assurance control](https://github.com/Hexalith/Hexalith.EventStore/blob/df5dc226af69292b13dc0676e408e56081e0ff2a/_bmad-output/planning-artifacts/prd.md#L224) · [Approval window](https://github.com/Hexalith/Hexalith.EventStore/blob/df5dc226af69292b13dc0676e408e56081e0ff2a/tools/phase_4_transition.py#L604) · [Approver identity check](https://github.com/Hexalith/Hexalith.EventStore/blob/df5dc226af69292b13dc0676e408e56081e0ff2a/tools/phase_4_transition.py#L620)

### 7. Release appears to depend on its own successor

**AD-11, line 222.** The final sentence asks for both release and deployment records and seals before *either* transition can pass. The evaluator checks the chain ending at the requested transition. Requiring the later promotion record would block a valid release. Specify separate inputs for each step. Release consumes its verified predecessor, release-owner record and applicable seal. Promotion subsequently consumes that release plus the qualified deployment, deployment-owner record and its own seal. [Publication chain contract](https://github.com/Hexalith/Hexalith.EventStore/blob/df5dc226af69292b13dc0676e408e56081e0ff2a/docs/architecture/publication-authority-v1.md#L17) · [Terminal-state check](https://github.com/Hexalith/Hexalith.EventStore/blob/df5dc226af69292b13dc0676e408e56081e0ff2a/tools/publication_authority.py#L409)

### 8. Consumer removal crosses two distinct publication subjects

**AD-11 and AD-22, lines 224 and 312.** EventStore publishes a package and Platform promotes a separately composed host. AD-22’s “same immutable subject” wording can make a verifier reject that valid two-chain arrangement or apply a package receipt to a host. Specify how the promoted profile binds the exact package identity, how the host retains its own chain, and how a consumer-owner receipt binds the profile and the exact proposed removal. [Subject ownership](https://github.com/Hexalith/Hexalith.EventStore/blob/df5dc226af69292b13dc0676e408e56081e0ff2a/docs/architecture/publication-authority-v1.md#L34) · [Package-to-profile binding](https://github.com/Hexalith/Hexalith.EventStore/blob/df5dc226af69292b13dc0676e408e56081e0ff2a/docs/architecture/publication-authority-v1.md#L185)

## Medium and low findings

| Priority | Decision and consequence | Proposed disposition |
| --- | --- | --- |
| Medium | **AD-6, line 169.** The global sequence can contain a gap when a reserved range outlives a failed aggregate commit. A reader waiting for every integer can stall. | State that global positions are unique and monotonic while each aggregate has its own gapless sequence. [Story global ordering](https://github.com/Hexalith/Hexalith.EventStore/blob/df5dc226af69292b13dc0676e408e56081e0ff2a/_bmad-output/implementation-artifacts/spec-dapr-global-event-ordering.md#L71) |
| Medium | **AD-11, line 205.** With the primary property unset, MSBuild and the spine accept the legacy `UseNuGetDeps=false` source-selection alias. The approved FR21 contract requires explicitly setting `UseHexalithProjectReferences=true`. | Ask the build and product owners to resolve the contract and align the decision, evaluation conditions and tests. [Source-selection logic](https://github.com/Hexalith/Hexalith.EventStore/blob/df5dc226af69292b13dc0676e408e56081e0ff2a/Directory.Build.props#L48) · [Story 3.5 owner decision](https://github.com/Hexalith/Hexalith.EventStore/blob/df5dc226af69292b13dc0676e408e56081e0ff2a/_bmad-output/implementation-artifacts/3-5-shared-package-catalog-and-source-package-reference-modes.md#L84) |
| Medium | **AD-12, line 234.** The spine describes every fixture job as merge-blocking. Of the three named fixture jobs checked, the accessible active `main` rules list only the Phase 4 risk-matrix fixtures. Classic branch-protection details returned HTTP 403. | Distinguish required policy from verified enforcement. Confirm any additional required contexts with the repository owner. Preserve separate live evaluations and per-transition seals. [Active rules](https://api.github.com/repos/Hexalith/Hexalith.EventStore/rules/branches/main) |
| Medium | **Status table, line 602.** G-NFR8 is described as waiting on runtime evidence from Story 6.4. The PRD records Story 6.3’s specification gate as approved and assigns runtime acceptance to Story 6.4. | Record the specification decision as passed for its bound digest and keep runtime NFR8 acceptance open. [PRD gate and scope](https://github.com/Hexalith/Hexalith.EventStore/blob/df5dc226af69292b13dc0676e408e56081e0ff2a/_bmad-output/planning-artifacts/prd.md#L737) |
| Low | **AD-25 companion.** The provenance README still describes hosted OQ8 evidence as pending. The spine correctly records successful fixture and live jobs on the committed revision. | Date the historical note or point to the revision-specific run. Preserve the immutable design bytes and the separate assured-decision status. [Provenance README](https://github.com/Hexalith/Hexalith.EventStore/blob/df5dc226af69292b13dc0676e408e56081e0ff2a/docs/architecture/oq8/README.md#L37) · [Hosted run](https://github.com/Hexalith/Hexalith.EventStore/actions/runs/44892480313) |

## What the checks established

| Check | Observed result |
| --- | --- |
| BMAD structural lint | Zero findings. AD-1 through AD-36 retain their IDs. All 50 local source references resolve. |
| OQ8 governing design | Offline identity validator passed. All 111 OQ8 fixture tests passed. Hosted fixtures and live evaluation passed for committed `df5dc226a`, and the manual assured-decision job was skipped. The uncommitted spine has a different file digest. |
| Production profile | AD-26 is the only decision tagged as an assumption. Its ratification subject matches the committed subject, `d49f8a061eb6a0faf1d31577d571ceae497a38d67404c4901151d1dc4ea90e16`. Ratification and profile qualification remain open. |
| Phase 4 risk matrix | Evaluator exited 1. All 12 high-risk rows lack the required authenticated CI seal and owner attestation. The five standard-control rows passed their classification checks. All transition decisions retain their separate requirements. |
| Publication authority | Evaluator exited 1 with `authority: unproven` and `seal: null`. The required owner records, qualified profile, verified predecessors and other listed prerequisites remain outstanding. |
| Phase 4 transition fixtures | 42 of 43 passed. The G-TENANT fixture at line 543 expects `GATE_UNBOUND`. The evaluator returned `SUBJECT_MISMATCH` because the request omitted bound subjects. The isolated test reproduced the same failure. Story 9.2 owns the fixture correction. [Failing assertion](https://github.com/Hexalith/Hexalith.EventStore/blob/df5dc226af69292b13dc0676e408e56081e0ff2a/tools/tests/test_phase_4_transition.py#L543) · [November audit](https://github.com/Hexalith/Hexalith.EventStore/blob/df5dc226af69292b13dc0676e408e56081e0ff2a/_bmad-output/planning-artifacts/sprint-change-proposal-2026-11-20.md#L28) |

The catalog check confirmed the documented .NET SDK, Aspire and Dapr SDK selections against their recorded and refreshed sources. The scoped OQ8 availability results and the separate production qualification requirements are accurately described. The architecture already identifies that the first readiness seal needs an owner-defined input contract. These existing gates remain open independently of the 13 findings above.

## Reviewed revision and evidence

The subject is the uncommitted [EventStore architecture spine](../../../architecture.md) on branch `work` at `df5dc226af69292b13dc0676e408e56081e0ff2a`. Its SHA-256 is `7ad8fe0af4e9eb42903b3a2d08577b07eef21112f2a9f8a8a64fd01c15a921f6`. The repository records Builds `e303acd8`. The working checkout has Builds `e1380b81`, with CiCd, McpCli, Platform and Tenants also ahead of their recorded revisions.

The four independent reviews cover the decision rubric, technology and implementation facts, disagreements between implementers, and assurance and release governance. Overlapping findings count once at their highest verified severity. [Rubric](review-validate-2026-11-21-rubric.md) · [Technology](review-validate-2026-11-21-technology.md) · [Implementation divergence](review-validate-2026-11-21-divergence.md) · [Assurance](review-validate-2026-11-21-assurance.md)

### Reproduce the local checks

Run from the EventStore repository root.

```sh
uv run .agents/skills/bmad-architecture/scripts/lint_spine.py --workspace _bmad-output/planning-artifacts/architecture/architecture-eventstore-2026-07-05
python3 -B tools/validate-oq8-governing-design.py
python3 -B -m unittest discover -s tools/tests -p test_oq8_governing_design.py
python3 -B -m unittest discover -s tools/tests -p test_phase_4_transition.py
python3 -B tools/validate-phase-4-gate-risk-matrix.py _bmad-output/implementation-artifacts/phase-4-gate-risk-matrix.json
python3 -B tools/validate-publication-authority.py evaluate
```
