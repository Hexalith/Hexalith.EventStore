---
title: 'Restore Builds package-gate fixture schema inventory'
type: 'bugfix'
created: '2026-10-23'
status: 'done'
route: 'oneshot'
review_loop_iteration: 0
baseline_commit: 'cd193902a52abb02572528958436d79a8ae25b9a'
approval: 'User authorized CI repair and continued tracking of latest main'
context: []
---

<frozen-after-approval reason="user-authorized CI repair">

## Intent

Builds CI run 39927799665 fails its package qualification gate because its synthetic package fixture includes only the module-manifest schema while production qualification now checks the module-manifest, test-environment, and test-result schemas. The real Module CLI project already ships all three. Update only the synthetic fixture to copy and archive those exact source schemas. Preserve production package checks, embedded-schema verification, intentional failure controls, and inventory withholding. Synchronize the parent pointer after committing the owning Builds repair.

</frozen-after-approval>

## Implementation Notes

- Owner: `references/Hexalith.Builds/Tools/test-g4-tool-package-contract-gate.ps1`; copy the three required schema files with their original names into the isolated fixture directory, and add them beneath `tools/net10.0/any/schemas/` in the synthetic module archive.
- `Tools/test-g4-tool-package-contracts.ps1` and the real packable projects remain unchanged.
- Narrow verification: `pwsh -NoProfile -File ./Tools/test-g4-tool-package-contract-gate.ps1` from Builds; expect its intentional failure, duplicate-output, and artifact-validator controls to pass. Review the fixture diff before committing and pushing; preserve exact pinned commitlint evidence for each owning repository.

- Owning Builds baseline, read directly from Git: `70ead608ac01f66dfcd80c13bb71152dac6c128e`. The parent baseline above identifies the EventStore checkout and does not attribute upstream Builds source changes to this fixture repair.
- Verification passed: the exact failing CI command completed with exit 0. Reused-output, pre-package, post-package, and duplicate-output controls withheld inventory as intended; all 63 artifact-validator scenarios passed. Production schema and embedded-resource assertions remain unchanged.

## Review Triage Log

| Finding | Verdict and evidence | Disposition |
|---|---|---|
| Three schema names are repeated in fixture preparation and generated archive script | low: both independent fixture stages intentionally use the same explicit required inventory; there is no current disagreement, and introducing an inventory file or runtime discovery adds machinery to this bounded fixture repair | Rejected as unnecessary generalization; no deferred work |
| New schema assertions lack dedicated omission/corruption mutations | low: this repair changes only synthetic positive inputs; production assertions are untouched, and the failed CI already demonstrated that missing schemas are rejected. New diagnostic-mirroring tests are unnecessary for the fixture correction | Rejected; existing rejection and artifact scenarios passed |
| Owning repository baseline missing from repair record | low: the original baseline identifies the parent while the fixture belongs to Builds | Patched these implementation notes with the exact owning Git baseline |
