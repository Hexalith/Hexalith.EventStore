---
title: 'Story 9.2 authenticated tenant gate decision'
type: 'feature'
created: '2026-11-22'
status: 'done'
route: 'dispatch'
story_key: '9-2-phase-4-high-risk-gate-matrix-and-assurance-control'
context: ['{project-root}/AGENTS.md']
baseline_commit: '2ca76828bf1b9e666b1ed309da4a96f7252290b2'
---

<frozen-after-approval reason="BMAD implementation scope">

## Intent

Give the G-TENANT gate a business evidence adapter. A gate decision must identify the EventStore and Tenants revisions that passed the Story 2.14 qualification and authenticate the corresponding CI evidence, seal and human attestation.

## Boundaries & Constraints

On 22 November the owner approved a current-source gate. Both EventStore runs must qualify the exact EventStore head being evaluated. The qualifying Tenants source and package runs must share an exact Tenants head whose EventStore gitlink matches that SDK head.

Use the gate's declared subjects, required roles, assurance level and 24-hour rule. Re-fetch business evidence during consumption. The Story 2.14 owner supplies its NFR2 completion adapter. The Product and Test owners define the readiness decision. Both stories retain their current tracker status during this change.

| Input | Expected outcome |
| --- | --- |
| Four verified runs, matching SDK, required successful jobs and cases, verified fresh-backend receipts, valid approval and completed seal | Issue one G-TENANT decision identifying both repositories, their revisions and the verified artifact |
| Wrong repository, head, attempt, workflow, package cohort, SDK gitlink or required case | Refuse the decision |
| Missing, expired, modified or incomplete receipt, failed persistence/readback or missing attestation or seal | Refuse the decision and report the failed check |
| G-OQ8 request at its existing availability entry point | Continue to require fresh OQ8 verification |

</frozen-after-approval>

## Code Map

- `tools/phase_4_transition.py` binds the request, current commit, approvals and seal. Its GitHub client reads evidence from EventStore and Tenants. `tools/phase_4_tenant_gate.py` verifies the four business runs and their archived evidence.
- `tools/phase_4_gate_decision.py` and `tools/phase-4-transition.schema.json` define the decision contract. The dedicated gate workflow enables G-TENANT, and the OQ8 availability workflow uses the CLI's OQ8-only default.
- `_bmad-output/implementation-artifacts/phase-4-gate-risk-matrix.json` defines the 75 subject paths, four hosted lanes, required cases and owner roles. The Story 2.14 spec identifies the qualified SDK, Tenants revision, selected Builds revision, package cohort, artifact ID and digest.
- Tenants' `.github/workflows/source-reference.yml` produces `tenants-source-backend.json` and `tenants-source-catalog-result.json`. Its production checks and the Story 2.14 handoff define the fields and their expected relationships.

## Tasks & Acceptance

- [x] Extend the transition contract and add a fixed G-TENANT adapter under `tools/`. Verify EventStore CI and Integration, and Tenants source-reference and package CI, against the expected repositories, run attempts, revisions, workflow digests, authenticated jobs and required test outcomes. Check both receipt bodies, including their common producer, fresh backend, readback, audit outcomes and zero remaining leases. Bind the adapter and evidence identities to the approval and seal.
- [x] Extend the gate decision consumer and its closed report. Preserve the OQ8-only scope at the OQ8 entry point. Require a fresh authenticated evaluation for each decision.
- [x] Exercise positive and refusal paths in the transition and gate-decision suites. Verify both Story 2.14 completion targets continue to refuse while the owner adapter is missing.
- Given a complete qualification on the approved subjects, when the dedicated workflow consumes the matching sealed run, then the decision reports the verified subject and computed assurance level.
- Given any changed subject, missing proof or revoked qualification, when the same decision is attempted, then it refuses. A previously saved decision supplies no evidence.

## Implementation Notes

At intake, the qualification record named EventStore `2ec21f631a2e292fba8c5537fe665ab275ed1081` and Tenants `50f9a4acefccab31a5023b091997bf5b251f87b8`. Five of 75 gate-bound files differed on the newly pulled EventStore main, including both EventStore workflows. The approved decision requires fresh runs for that source.

The historical source-run artifact is `tenants-source-catalog-owner-profile` (ID `14707106934`). Its source-run reference and hash are recorded below.

## Verification

Run the complete Phase 4 transition and gate-decision suites and report the live matrix separately. Check wrong-subject and missing-attestation cases. Record the local and authenticated results with their exact source revisions.


### Implementation verification, 22 November 2026

The local implementation was tested on top of EventStore `6214575ca07e3bd5212d32c045c7fc32e711617d`. Four focused transition tests and the dedicated workflow decision test passed. The workflow test evaluated and consumed a sealed synthetic four-run qualification, then refused after the Tenants source attempt changed. It also exercised the OQ8 entry point, missing seal and saved-decision replay. The transition tests covered missing attestation, wrong revision, job, attempt, package, artifact and theory arguments, along with failed backend, readback and lease checks. The JSON Schema validator accepted the resulting request, candidate and sealed report and rejected extra properties and a cross-gate substitution.

Before job-level checkout and upload verification was added, the adapter passed a read-only replay of the four historical first attempts on EventStore `2ec21f631a2e292fba8c5537fe665ab275ed1081` and Tenants `50f9a4acefccab31a5023b091997bf5b251f87b8`. GitHub run, workflow, job, check, commit and artifact records and seven downloaded archives supplied the evidence. The source receipts and the Release package manifests agreed on EventStore 5.4.0. The manifests identified McpCli 3.0.0. Tenants selected Builds `055d7df5b49669d5b705ce163b0b2e187ecb9a50`. The adapter verified the source receipt archive's ID `14707106934` and SHA-256 `c7284f92b20466dc56001309ac51709c3065a1bf51b3a6e8add5f1a531b11046`. Applying these run pins to the synced EventStore revision returned `RUN_INVALID`.

The live matrix on the local checkout returned exit 1. G-TENANT reported `SEAL_MISSING`, `ATTESTATION_MISSING` and `ACHIEVED_BELOW_REQUIRED`. Both Story 2.14 completion targets returned exit 1 with `ADAPTER_UNAVAILABLE` for NFR2. A current decision requires new qualification and registered-human approval on its bound subjects. The dedicated workflow accepts the optional `PHASE4_TENANTS_READ_TOKEN` secret for cross-repository read access to Tenants Actions evidence.


### Job provenance verification after review

Seven focused transition tests and two gate-decision tests passed on changes based on `6214575ca07e3bd5212d32c045c7fc32e711617d`. The revised adapter accepted the Release assembly and case records in 10 historical TRXs. The authenticated EventStore Contracts, build-and-test and published-compatibility logs confirmed the expected checkout and pinned artifact uploads.

The historical Tenants qualification now refuses with `SOURCE_MISMATCH`. The owner-profile, Projection provenance, Published catalog HTTP and Aspire job logs show checkout `fbc30e4f04ea6080fe8a20a6224d8ecc7447044a`. The PR run metadata identifies `50f9a4acefccab31a5023b091997bf5b251f87b8`. A qualifying PR attempt must establish its original target through the attempt’s own PR association or a successful runner read of `GITHUB_BASE_REF` and must check out the exact claimed source in each credited job. A present-day PR lookup cannot supply the original association.

### Final regression results

All 52 tests discovered from `tools/tests/test_phase_4_transition.py` passed across two isolated unittest processes. All 18 tests in `tools/tests/test_phase_4_gate_decision.py` passed. The adjacent checks passed all 147 risk-matrix fixtures and 32 story-completion tests. Three independent reviewers reported 23 findings. The implementation and tests address all 23.

The live matrix returned exit 1 on the local checkout. Five standard controls passed and all 12 high-risk gates reported insufficient assurance. On synced EventStore `2ca76828bf1b9e666b1ed309da4a96f7252290b2`, the transition CLI rejected the historical four-run request with `RUN_INVALID`, achieved level `none` and an empty seal. The parent tracker remains `in-progress`.

## Spec Change Log

The review established a difference between GitHub’s PR head and the actual tested checkout, and between run-level artifact metadata and job-level upload provenance. The owner-approved exact-source decision requires verifying both at the job boundary. The pre-review archive parser result covers its original checks only.

## Review Triage Log

| Finding | Verdict | Evidence and disposition |
| --- | --- | --- |
| Blind 1, checkout revision | High | The Tenants Projection provenance, Published catalog HTTP and Aspire job logs all show checkout `fbc30e4f04ea6080fe8a20a6224d8ecc7447044a`. Run metadata reports `50f9a4acefccab31a5023b091997bf5b251f87b8`. Require the executed revision. |
| Blind 2, PR target | High | The run returned an empty PR list. A subsequent commit-to-PR lookup yields the PR's current target. Require historical trigger provenance where the two can differ. |
| Blind 3, artifact producer | High | Jobs overlap within a run. The authenticated upload step records the artifact name, ID and size. Bind the decision to that job record. |
| Blind 4, rerun archives | Medium | Multiple attempts share the run artifact listing. Select the exact pinned ID and verify its job and attempt. |
| Blind 5, compiled assembly | High | Archived TRXs identify the assembly and Release path in both storage and codeBase. Check these fields for credited rows. |
| Blind 6, REST coverage | High | The real REST report has nine cases. A class count alone can hide a missing mandatory read or refusal case. Require their identities. |
| Blind 7, theory coverage | High | The Admin platform-list test has two distinct required inputs. Check the actual inputs for required theory outcomes. |
| Blind 8, runtime receipt provenance | Medium | The runtime JSON does not independently name the fresh backend. The pinned source workflow writes the two paths on one hosted job and uploads them together. Require proof of that producing job, its qualification steps and its exact archive. |
| Blind 9, later attempt | High | Business validation spans multiple downloads. Recheck the four attempts after verifying all evidence. |
| Blind 10, timestamp diagnostics | Low | An invalid calendar value can reach datetime parsing. Return the relevant evidence refusal. |
| Blind 11, mislabeled failure | Medium | The dedicated CLI labeled a known tenant request with invalid coordinates as OQ8. Preserve its gate identity in the diagnostic. |
| Blind 12, coupled fixture | Medium | The fixture derives test coverage and jobs from the adapter tables. Add independent named controls for the signed-requester case and producer steps. |
| Edge 1, PR checkout | High | Hosted checkout logs establish the revision mismatch described in Blind 1. |
| Edge 2, changing attempt | High | The initial attempt lookup cannot detect a later retry while verifying another lane. Exercise late supersession. |
| Edge 3, retained rerun artifact | Medium | Matching a name across all attempts can discard a uniquely pinned current archive. |
| Edge 4, overlapping jobs | High | A creation time can match more than one job. Verify the original upload identity. |
| Edge 5, missing theory cases | High | The required behavior includes specific permission and denial branches. Exercise a substituted input with the same passing count. |
| Edge 6, XML encoding | Medium | An unsupported encoding can raise LookupError before a structured diagnostic is written. Handle it at the TRX boundary. |
| Edge 7, gate diagnostic | Medium | A known tenant request must retain its gate and scope on invalid input. |
| Verification 1, repository credentials | Medium | Earlier fixtures used the same credential for both repositories. Check distinct credentials at their respective HTTP boundaries. |
| Verification 2, workload-gateway case | High | Removing the signed-requester method from a verifier-derived fixture also removes it from its expected inventory. Add an independently named refusal. |
| Verification 3, downloaded bytes | High | The listing and approved pins can remain identical while downloaded bytes differ. Test a same-length archive change. |
| Verification 4, invalid coordinates | Medium | The real dedicated CLI returned exit 2 with the OQ8 identity for invalid tenant seal coordinates. Check the emitted tenant report. |


## Intake evidence

GitHub's read-only API reported successful first attempts for the recorded EventStore [CI](https://github.com/Hexalith/Hexalith.EventStore/actions/runs/44487369142) and [Integration](https://github.com/Hexalith/Hexalith.EventStore/actions/runs/44487368442) runs, and for the recorded Tenants [source](https://github.com/Hexalith/Hexalith.Tenants/actions/runs/44903471996) and [package](https://github.com/Hexalith/Hexalith.Tenants/actions/runs/44903472422) runs. The Tenants source artifact metadata matched ID `14707106934`, size 2,804 bytes and SHA-256 `c7284f92b20466dc56001309ac51709c3065a1bf51b3a6e8add5f1a531b11046`. The GitHub connector retrieved the archive. An independent SHA-256 check matched the API digest. Both JSON receipts identify SDK 5.4.0 at the recorded producer revision. The backend receipt binds Builds `055d7df5b49669d5b705ce163b0b2e187ecb9a50`. The runtime receipt lists four hosts, 24 operations, seven REST reads, successful tenant persistence, distinct audit-owner readback, public-system refusal and zero outstanding leases.

The newer EventStore CI failed its folded-snapshot compatibility, checkpointed package and build-and-test jobs. Both Story 2.14 completion dry runs returned `ADAPTER_UNAVAILABLE` for NFR2. [EventStore CI on the synced revision](https://github.com/Hexalith/Hexalith.EventStore/actions/runs/45002034760)
