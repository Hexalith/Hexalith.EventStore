# DW6 Deferred-Work Governance ATDD Artifacts

This folder supports the red-phase ATDD scaffolds for `post-epic-deferred-dw6-deferred-work-governance`.

## Entrypoint

The committed checker entrypoint declaration is canonical fixture evidence for
DW6 and follow-up governance stories:

`_bmad-output/test-artifacts/deferred-work-governance/entrypoint.txt`

`scripts/check-deferred-work.ps1` and `scripts/check-deferred-work.sh` are
co-canonical thin wrappers over `scripts/check-deferred-work.py`. Each emits
identical exit codes and JSON output; they only differ in shell host. Pick the
host most natural for the runtime:

- **Local-dev default (Windows / PowerShell):**
  `pwsh:scripts/check-deferred-work.ps1` — the value committed to
  `entrypoint.txt` and the shape that ATDD invokers prefer.
- **CI on GitHub Actions Linux runners:**
  `bash scripts/check-deferred-work.sh` — invoked from
  `.github/workflows/docs-validation.yml` because the runner is Linux. The
  bash wrapper executes the same Python entrypoint, so behavior is equivalent.

Both wrappers are first-class and DW9 declares them co-canonical: a change to
either entrypoint must keep them behaviourally identical and must update
`entrypoint.txt`, this README, the wrapper smoke commands, and any ATDD test
expectations in the same change. Supported schemes for `entrypoint.txt` remain:

- `pwsh:scripts/<checker>.ps1`
- `sh:scripts/<checker>.sh`
- `dotnet:<assembly-qualified-static-checker-type>`

The checker should support JSON output with:

- `exitCode`
- `counts` for `OPEN`, `STORY`, `ACCEPTED-DEBT`, `RESOLVED`, `DUPLICATE`, `NO-ACTION`, and `unclassified`
- `diagnostics[]` with `file`, `rule`, `disposition`, `heading`, `excerpt`, `line`, and `hint`

## Snapshot

Before editing `_bmad-output/implementation-artifacts/deferred-work.md`, capture:

`_bmad-output/test-artifacts/deferred-work-governance/deferred-work-snapshot.md`

The ledger-sweep scaffolds use that snapshot to verify that historical review text was preserved while metadata was added narrowly.
