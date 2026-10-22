---
title: 'Qualify current EventStore source for Folders Story 12.2'
type: 'bugfix'
created: '2026-10-22'
status: 'in-review'
route: 'oneshot'
baseline_commit: '995117f9b899a2994e99257091b0ea35cfbe5090'
review_loop_iteration: 0
context: []
---

<frozen-after-approval reason="explicit user authorization of investigation, fixes and qualification">

## Intent

Qualify the current EventStore source, including the existing admission/rate-limit correction, for Folders Story 12.2 consumption. Compare the latest Folders selected-candidate handoff with current source; investigate and fix verified SDK-owned defects. Build Debug assets with project references, run affected tests, individual SDK regression projects and relevant real-Dapr acceptance. Preserve all existing changes and historical evidence. Produce a sealed consumer handoff with canonical revisions, source/binary hashes, commands/results, runtime identities, cleanup and remaining failures; distinguish fresh execution from reused evidence. Keep assertions, deadlines, retries, admission rules and storage limits intact. Do not publish, deploy, change pins, stage, commit or push; leave Folders source unchanged.

</frozen-after-approval>

## Implementation Notes

- The latest consumer startup packet passes against an older explicitly selected SDK binary graph. Current actor admission/token/rate-limit source matches its own later SDK packet; seven further ordinary source changes concern Admin/protection and must be preserved.
- Verified small SDK testing defect: `TestServiceOverrides.RemoveDaprHealthChecks` omits the exact `projection-delivery-unresolved-conflict` registration requiring Dapr state. Extend this existing isolated-test boundary and its regression test, preserving unrelated health checks and production readiness behavior.
- Use a new append-only packet at `evidence/folders-12-2-current-source-20261022T134028Z`. Capture initial/final source, index, historical evidence identities. Existing Folders edits are inputs and remain untouched.
- No intent gaps or irreversible operations. The implementation footprint is the existing testing helper/test plus evidence/spec files; no new public API or dependency change. The user's explicit execution scope supplies authorization to proceed. Broad qualification may expose additional verified defects; retain each first failure and fix only SDK-owned causes.
- Run the solution restore/build only in Debug/project-reference mode. Execute individual SDK test assemblies; retain full command receipts/XML, use fresh disposable infrastructure for actor admission and bounded projection process acceptance. Avoid building or selecting Folders outputs in this producer task.
- Review the resulting change and handoff with an independent reviewer before sealing. Do not infer Folders adoption, package/Release qualification, whole-story acceptance or performance closure from producer tests.
- Full Contracts regression exposed two later sealed inherited-owner reproduction projects outside its older frozen MSBuild enrollment. Preserve the original 984-file count/digest, separately bind the two new exact paths/bytes, and retain regular-file/parent-link and live-build exclusion checks. Add positive/tamper regressions in `ContractsPackageDependencyTests.InheritedOwnerEvidence.cs`; never edit the captures.
- Full Server regression reports 21 preexisting secret-material locations. Every location matches the starting canonical revision. Preserve this failed broad gate; the task prohibits editing historical evidence or weakening assertions. No whole-source green disposition is allowed while it remains failed.
- Review identified evidence-tool gaps. Preserve original executions, enforce frozen PE/PDB pins, include dependency sources, join worker self-reports to actual module observations, and gate results on nonempty inventories plus successful lifecycle/preservation checks. Explicitly distinguish build-context binary variants and v1 executed/v2 syntax-only launcher tooling.
- Execution complete: Debug solution/source build passed; helper suite155, affected HTTP33, Client2073+2 existing skips, DomainService1164, final Contracts2442+2 existing skips, focused frozen guard12 and all other selected SDK project suites executed. Actor acceptance passes47 checks; bounded acceptance passes9 cases across30 workers, including both contracts at10001/32769 events and inherited original replay. Full counts, commands and skips remain in the sealed packet.
- Qualification stays `in-review` / not fully qualified: the full Server run has6360 passes,25 skips and one preexisting secret-material guard failure across21 unchanged locations. The strict external identity audit lacks22 Server maps despite authenticating all120 CLR module proofs and98 matching OS-map observations. Preserve both open gates without changing assertions or evidence. Folders current-source adoption, SM4, package/PD10 and whole-story acceptance remain outside this producer disposition.
- Final preservation passes all47976 historical files, Folders source/existing edits, canonical revisions, dependency revisions and Git indexes. Six exact owned fixture containers and their materialized directories are absent. The unrelated Tenants AppHost remains at PID67730; the identified temporary verifier is archived and removed. No Git recording, publishing, deployment or pin update was performed in the working repositories.
- Independent final review found no material inconsistency in the negative qualification disposition or its evidence. All review findings and corrections are retained in `review-triage.json`. No whole-story completion is recorded.
