---
title: 'FrontComposer Story 11.24 EventStore Runtime Identity Successor'
type: 'refactor'
created: '2026-08-10'
status: 'in-progress'
review_loop_iteration: 0
baseline_commit: '8358ffc399bdb1f1574bd049f17b3b6ebf907619'
context:
  - '{project-root}/references/Hexalith.EventStore/_bmad-output/project-context.md'
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** Story 1.20's approved `999.1.20-proof.fa2d1c9910f8` archives are unrecoverable, so its authority cannot unblock Hexalith.FrontComposer Story 11.24. A successor must bind one exact tested EventStore source to retrievable package bytes, catalog provenance, isolated restore evidence, and fresh named approvals.

**Approach:** Use the already-published exact tuple EventStore source `bb94d93e9b84132cff83a38fba84f25455820d31`, version `3.91.1`, and Builds catalog commit `a8a50859fa2f27f511a9470dfe1e3ae54d0ebc1a`. Freeze independently reproduced evidence into a new review subject, stop for content-bound EventStore Owner and Release Owner receipts, then authorize only that unchanged subject for FrontComposer Story 11.24.

## Boundaries & Constraints

**Always:** Name NuGet.org signed-feed bytes as the SHA-256 domain; retain all 14 IDs from `tools/release-packages.json`; distinguish Builds catalog exposure `a8a50859...`, release execution `f75daebd...`, and the release source's historical Builds gitlink `824d7ef1...`; require fresh isolated-cache restore/tool-install results and two roster-authorized approval receipts.

**Ask First:** Any candidate other than the exact `bb94d93...` / `3.91.1` tuple, any Builds runner/schema change prompted by its separate `3.88.0` pin, or any approval scope beyond FrontComposer Story 11.24.

**Never:** Reuse or rebuild the retired proof packages; infer equality from ancestry, current `main`, release success, or catalog presence; reuse Tenants Story 2.12's waiver; modify FrontComposer, runtime behavior, release workflows, package inventory, or historical Story 1.20/3.13 evidence; set `final_decision: available` or `authorize_consumer_migration: true` before both exact-subject receipts validate.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|----------|---------------|---------------------------|----------------|
| Complete successor | Exact tuple, 14 hashes, isolated restore, two valid receipts | Durable record says `available` and authorizes FrontComposer Story 11.24 | Reject any changed or missing binding |
| Approval checkpoint | Reproduced evidence is complete but receipts are absent | Frozen non-authorizing subject plus exact owner actions | Stop; keep decision unavailable and migration false |
| Prohibited substitute | Old proof, ancestry, tracked-main, or Tenants waiver | No authority granted | Record the exact rejected basis |

</frozen-after-approval>

## Code Map

- `_bmad-output/implementation-artifacts/1-20-owner-approved-parity-closure-proof-packet.md` -- read-only historical authority; its package identity and approvals cannot transfer.
- `_bmad-output/implementation-artifacts/3-13-deployed-runtime-parity-closure-proof-packet.md` -- read-only 0/14 unrecoverability and no-migration verdict.
- `tools/release-packages.json` -- exact ordered 14-package inventory; current SHA-256 `6b0b70b856839d4117bcd969f6a2de0093c477c109cb79f3f2882b1f05effcae`.
- `scripts/validate-consumer-package-references.py` -- existing 13-library plus one-tool isolated consumer validator.
- `references/Hexalith.Builds/Props/Directory.Packages.props` at `a8a50859...` -- exact catalog exposure for `3.91.1`; Admin.Cli remains manifest-owned rather than cataloged.
- `_bmad-output/implementation-artifacts/evidence/frontcomposer-story-11-24/bb94d93e9b84132cff83a38fba84f25455820d31/` -- new package manifest, restore receipt, release/catalog provenance, roster, review subject, and content-bound approvals.
- `_bmad-output/implementation-artifacts/frontcomposer-11-24-runtime-identity-successor.md` -- new durable decision record; unavailable until both receipts validate.
- `tests/Hexalith.EventStore.Contracts.Tests/Packaging/FrontComposerRuntimeIdentitySuccessorTests.cs` -- new fail-closed schema, exact-tuple, hash, scope, and approval gate.

## Tasks & Acceptance

**Execution:**
- [x] Evidence directory -- retain the 14 NuGet.org SHA-256 values, exact package metadata commit, successful exact-source CI/release identities, Builds identities, and fresh isolated restore/tool-install receipt.
- [x] Review subject and roster -- hash-bind the exact candidate, evidence, limitations, `github:jpiquot` EventStore/Release roles, and scope `Hexalith.FrontComposer Story 11.24`.
- [ ] External checkpoint -- obtain separate durable EventStore Owner and Release Owner acceptances of the unchanged subject; do not self-approve or infer approval from workflow actors.
- [ ] Successor record -- after both receipts validate, record literal `final_decision: available` and `authorize_consumer_migration: true` for only the bound tuple and consumer scope.
- [x] Focused test -- reject missing/late/wrong-role receipts, subject drift, package/hash/Builds drift, old proof reuse, ancestry, and the Tenants waiver.

**Acceptance Criteria:**
- Given the exact source tag, successful exact-source test/release runs, and 14 independently retrieved packages, when validation runs, then every package embeds `bb94d93...`, matches its NuGet.org SHA-256, and restores in an isolated package-only or tool consumer.
- Given Builds `a8a50859...`, when catalog provenance is checked, then it exposes `3.91.1` without conflating the `3.88.0` runner/schema pin or historical release identities.
- Given two valid content-bound owner receipts, when the final record is evaluated, then it alone records `available`, migration `true`, and explicit FrontComposer Story 11.24 scope.
- Given any absent or mismatched prerequisite, when the gate runs, then it remains non-authorizing without an ancestry or waiver exception.

## Spec Change Log

- 2026-08-10: Reproduced the exact signed-feed package tuple, froze review subject
  `9d074dfd0758a8934f122aab18659627dff1cf5d4c3e548b222cc0d79a881065`, recorded the
  non-authorizing approval checkpoint, and added 23 passing focused cases. External owner receipts
  remain required before the successor record may become available.

## Design Notes

Publication is unnecessary: public `3.91.1` already supplies an exact tested source/package tuple. The only external checkpoint is fresh migration authority; release workflow execution by `jpiquot` is evidence, not either required approval.

## Verification

**Commands:**
- `sha256sum -c nuget-sha256.txt` -- all 14 independently downloaded NuGet.org archives pass.
- `python3 tools/validate-release-packages.py <isolated-packages> 3.91.1` -- exactly 14 packages validate.
- `python3 scripts/validate-consumer-package-references.py <isolated-packages>` -- 13 library consumers and one tool consumer pass with no project edges.
- `dotnet build tests/Hexalith.EventStore.Contracts.Tests/Hexalith.EventStore.Contracts.Tests.csproj -c Release -m:1` then run the built test assembly with `-class` -- focused successor tests pass.

**Observed 2026-08-10:** all 14 NuGet.org archives passed repository-signature verification and
release inventory validation; 13 isolated library consumers and one isolated tool consumer passed;
the focused Release build completed with zero warnings/errors; and the direct xUnit v3 class run
executed 23 tests with 23 passed, zero failed, skipped, or not run.
