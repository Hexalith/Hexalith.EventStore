# DW6 Deferred-Work Governance ATDD Artifacts

This folder supports the red-phase ATDD scaffolds for `post-epic-deferred-dw6-deferred-work-governance`.

## Entrypoint

When implementation starts, declare the checker shape in:

`_bmad-output/test-artifacts/deferred-work-governance/entrypoint.txt`

Use one of:

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
