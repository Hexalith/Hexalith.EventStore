---
title: 'Story 9.2 authenticated command status-link gate'
type: 'feature'
created: '2026-11-22'
status: 'done'
route: 'dispatch'
review_loop_iteration: 0
story_key: '9-2-phase-4-high-risk-gate-matrix-and-assurance-control'
baseline_commit: '8319318850530d756db5b8b7555d0b9f804361e9'
context: ['{project-root}/AGENTS.md']
---

<frozen-after-approval reason="EventStore API and Test owner gate scope">

## Intent

Let the Phase 4 gate and readiness workflows verify Story 2.15’s evidence that a generated command status link uses a MessageId valid for the declared grammar. The matrix already identifies the subjects, CI jobs, required tests and approving roles for G-STATUS-ID. Enroll that binding in the existing assurance control.

## Boundaries & Constraints

Qualify the exact EventStore revision, workflow bytes and current completed attempt. Require successful results from the two jobs named by the matrix, including their actual checkouts, authenticated Actions checks and artifact uploads. Separately require a successful dedicated Phase 4 evaluation and the registered owner’s time-separated attestation to issue a decision. Recheck the inputs when consuming both the gate and readiness decisions.

Give the ambiguous numeric arguments and truncated length boundaries in the existing generator theories a stable identity derived from the actual runtime type or length. Preserve the inputs and behavioral assertions. The acceptance scope covers the generated Location, both declared grammars and the owner’s existing case inventory. Story 2.15 retains the gateway GET fallback decision and its own completion process.

| Scenario | Expected outcome |
| --- | --- |
| Verified jobs and required cases, matching subjects, approval and completed seal | Issue a G-STATUS-ID decision and accept it as one readiness prerequisite. |
| Wrong checkout, workflow, attempt, job, producer or archive | Refuse and identify the failed check. |
| Required case omitted, duplicated, skipped, failed or indistinguishable from another | Refuse even if the summary reports the expected total. |
| Changed gate result, superseded attempt or withdrawn approval at consumption | Refuse the affected decision and attribute a readiness refusal to G-STATUS-ID. |

</frozen-after-approval>

## Code Map

- `_bmad-output/implementation-artifacts/phase-4-gate-risk-matrix.json` declares 19 subject paths and the EventStore API and Test evaluator roles. `spec-2-15-messageid-only-command-status-identity.md` records the approved cases.
- `.github/workflows/ci.yml` and its pinned Builds workflow publish `contracts-test-results` and `blocking-test-results` from `ci / contracts` and `ci / build-and-test`. The existing jobs supply the four Release TRXs.
- `tools/phase_4_tenant_gate.py` verifies Actions jobs, checkout and upload logs, artifact provenance, archive bytes and compiled TRX identities. Its existing consumers require overall successful owner runs. G-STATUS-ID credits the two jobs its matrix declares.
- `tools/phase_4_transition.py`, `tools/phase_4_gate_decision.py` and `tools/phase_4_readiness.py` control request binding, sealed consumption and readiness rechecks. The OQ8 availability entry point accepts only its own gate.

## Tasks & Acceptance

- [x] Update `tests/Hexalith.EventStore.RestApi.Generators.Tests/RestApiGeneratedControllerErrorSemanticsTests.StatusIdentity.cs` and verify that the actual Release TRX distinguishes the five typed numeric cases and the 128/129-character boundaries.
- [x] Add the fixed G-STATUS-ID verifier under `tools/`. Check the declared grammar cases, generator source assertions, every runtime status theory, Sample runtime cases and Server catalog declarations. Bind the verifier, full declaration and authenticated result to the decision.
- [x] Wire the closed claim and result through `tools/phase-4-transition.schema.json`, the three controls above and `.github/workflows/phase-4-high-risk-gate-pass.yml`. Enroll the gate in readiness with the existing late approval and attempt checks.
- [x] Extend `tools/tests/test_phase_4_transition.py`, `tools/tests/test_phase_4_gate_decision.py` and the readiness schema fixture. Exercise the normal CLI paths with a positive control, evidence substitutions and late revocation.

Given a qualifying current run, when a sealed gate decision is consumed, then its evidence identifies the actual CI attempt, producers, artifacts and passing cases. Given a valid status-link decision alongside an unavailable readiness prerequisite, when readiness is evaluated, then that other prerequisite still blocks READY.

## Implementation Notes

The status-link verifier checks the two producing jobs and both archives on the exact CI attempt. It credits 166 named cases across four Release reports. The dedicated workflow can issue the gate decision after its separate successful seal and owner approval. Readiness now has three enrolled owner verifiers. Its acceptance still requires all 15 prerequisites.

## Spec Change Log

## Review Triage Log

| Finding | Verdict and disposition |
| --- | --- |
| Blind 1 · Producer archives at issuance | High. A current attempt can outlive either archive. Recheck the pinned archives at both decision boundaries. |
| Blind 2 · Execution ID collision | Medium. Only scoped results participated in the uniqueness check. Account for unrelated rows sharing a credited execution ID. |
| Blind 3 · XML namespace | Medium. Wildcard element queries admitted other report formats. Require the namespace emitted by the Microsoft TRX reporter. |
| Blind 4 · Encoded document declaration | Medium. The byte-level scan and XML decoder handled alternate encodings differently. Enforce the supported reporter format at the parser boundary. |
| Blind 5 · Reporter and fixture coupling | Medium. Synthetic reports derive names from the adapter and cannot detect a reporter-format change. Exercise the real Release report in automated verification. |
| Blind 6 · Report cardinality | Medium. The schema permitted a single entry for any assembly. Encode and test the four fixed cardinalities. |
| Blind 7 · Gate identity on refusal | Medium. Wrong-transition validation preceded gate selection. Preserve the recognized gate in the dedicated CLI diagnostic. |
| Blind 8 · New passing cases | False as a qualification defect. The owner binding covers a fixed inventory. Unknown cases in the covered classes require an inventory update before qualification. Document this condition. |
| Blind 9 · Verification record | Low. Intake results preceded the implementation run. The final verification below records the new Release results and case identities. |
| Blind 10 · Repository updates in review diff | False as a product change. The operator requested the repository updates before the build. The BMAD epic-context refresh records current dependencies. |
| Edge 1 · Readiness inventory limit | Medium. A legal request with 100 subjects and 100 evidence files can produce 212 input records after the extra verifier. Correct the previous limit of 211. |
| Edge 2 · Late archive expiry | High. The earlier readiness lookup checked the attempt alone. Verify archive availability again before issuing the aggregate. |
| Edge 3 · Assembly identity across rows | Medium. Different framework versions and assembly locations individually matched the original expression. Enforce a common .NET 11 Release assembly for each report. |
| Edge 4 · Unrelated execution identity | Medium. An unrelated result could share a credited execution ID. Include these collisions in the report check. |
| Edge 5 · Alternate XML encoding | Medium. An encoded declaration could reach the XML parser. Align supported encoding and declaration checks. |
| Verification 1 · Reporter integration | Medium. The original positive fixtures stayed green when C# display labels diverged. Exercise the real generator report in the fixture workflow. |
| Verification 2 · Hosted schema checks | Medium. Local Draft 2020-12 assertions were absent from the Phase 4 fixture workflow. Run them there. |
| Verification 3 · Misplaced archive member | Medium. Changed archive bytes and assembly paths exercised different checks. Repin an intact report in the wrong directory and require refusal. |
| Verification 4 · Maximum readiness input count | Medium. The reviewer reproduced a runtime result with 212 inputs that the schema rejected. Cover the boundary and the matching runtime contract. |

## Verification

Intake at the baseline above passed all 147 matrix fixtures and the readiness schema test. The live matrix exited 1. All five standard rows passed their structural checks and all twelve high-risk rows reported insufficient assurance.

The two archive SHA-256 values matched the GitHub artifact metadata for historical run 44780281320 at `db5db24707a2468e69cf86016c3bba2f3fb5d516`. Its reports contained 65 grammar, 71 runtime status, 20 Sample and 8 Server results. Three numeric theory rows displayed the same value of 1, and two displayed the same value of 2. The report omitted their CLR types. This run informs the test design. [Historical CI run](https://github.com/Hexalith/Hexalith.EventStore/actions/runs/44780281320)

The local Release generator project passed all 276 tests with zero skips. Its TRX reproduced the same five numeric rows under two names. All five execution IDs differed from the historical run, so the IDs cannot identify those cases across runs.

For implementation, run the changed Release test project and inspect the emitted TRX, then run the Phase 4 transition, gate-decision, risk-matrix and story-completion suites and the readiness schema check. Report the live matrix separately. The intake AppHost built with zero warnings and errors, then Aspire exited 2 while creating its connection directory under the read-only user profile. `aspire describe` found no running AppHost.

### Post-review verification on 22 November 2026

The reviewed working tree is based on EventStore `8319318850530d756db5b8b7555d0b9f804361e9`. A clean local clone with only the pinned Builds submodule initialized ran the new generator fixture step in package-reference mode. All 276 Release tests passed with zero skips. The reporter check verified 73 generator cases, including **Int16 1, Int64 1, Int64 2, Double 1, Byte 2** and the **128- and 129-character** boundaries. The actual generator TRX contained 71 distinct runtime rows for 71 cases.

The gate workflow evaluated and consumed four locally emitted Release TRXs against synthetic GitHub responses. It credited 65 Contracts, 73 Generator, 20 Sample and 8 Server cases. The previous generator report and a same-count typed-case substitution both failed. The changed fixture workflow also runs the Draft 2020-12 checks. Its exact schema setup and test commands passed locally.

| Post-review checks | Local result |
| --- | --- |
| `test_phase_4_transition.py` | 67 passed across three isolated unittest processes |
| `test_phase_4_gate_decision.py` | 19 passed |
| `test_phase_4_gate_risk_matrix.py` | 147 passed |
| `test_phase_4_story_completion.py` | 32 passed |
| `tools/tests/check_phase_4_readiness_schema.py` | 2 passed |
| Live matrix | Exit 1. Five standard rows passed their structural checks. All twelve high-risk gates reported insufficient assurance. G-STATUS-ID requires a live seal and a registered owner’s attestation. |

The separate Story 5.3 scanner suite passed 36 of 38 tests. Two tests required a fresh NuGet restore in an isolated process. The probe failed at restore with NU1301 because it could not reach `api.nuget.org:443`. The scoped 9.2 checks above ran successfully. Hosted execution and owner attestation remain outstanding for the new gate.
