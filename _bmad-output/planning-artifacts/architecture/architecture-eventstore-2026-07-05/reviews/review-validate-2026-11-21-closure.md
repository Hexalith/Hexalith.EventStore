# Closure of the 21 November 2026 architecture validation

**25 November 2026 · Phase 4 · Spine closure (S1)**

> **All 13 findings are disposed of against the current spine. Ten are resolved in the spine. Three remain open with named owners outside the spine.** The spine stays `draft` because AD-26 remains an unratified assumption. Phase 4 readiness remains **Reject**.

## Subject

- **Spine:** [`architecture.md`](../../../architecture.md), SHA-256 `caa4d4c23064f2df4e3d5d8f9fd0dd21fbe88944d34f3776aa67e6defb2c53b7`, last changed by `831931885` (pushed 2026-11-22T12:02:35Z).
- **AD-26 ratification subject:** `5070ebdcf50d9fe7a7013be67d5c872e60563acf2ca4031382d0123754080803`, unchanged since `2b8895f18`. `831931885` changed the lead banner, the AD-35 rule and one implementation-status row for the McpCli AD-23 disposition, all outside the subject. The memlog records that AD-26 impact as NO.
- **Findings disposed of:** the [21 November validation report](validate-report-2026-11-21.md), which has 8 high, 4 medium and 1 low finding.
- **Authority:** edit P-7 of the [2026-11-25 sprint change proposal](../../../sprint-change-proposal-2026-11-25.md), approved by the owner on 2026-11-25.

## Dispositions

Line numbers refer to the spine bytes above.

| # | Finding | Disposition | Where the spine settles it | Open owner |
| --- | --- | --- | --- | --- |
| H1 | The credential rules reject an approved platform command | Resolved | AD-36 ([line 441](../../../architecture.md)) admits only platform-scoped `POST /api/v1/commands/bound/workload` with one forwarded human bearer beside the workload assertion. The human `sub` must equal the signed `PrincipalId` and the cataloged permission is checked. The AD-18 outbound rule ([line 297](../../../architecture.md)) forwards that bearer for this route only. | — |
| H2 | A protected app channel needs the receiver token in Development | Resolved | The AD-28 rule ([line 389](../../../architecture.md)) validates the receiver token on every channel-protected endpoint, including in Development. | — |
| H3 | Authentication alone cannot authorize an unclassified endpoint | Resolved | The AD-16 rule ([line 271](../../../architecture.md)) denies every caller on unclassified endpoints, including authenticated humans, workloads and the receiver's channel. | — |
| H4 | The OpenBao floor predates published security fixes | Resolved in the spine; retained for the comparator | The stack entry ([line 480](../../../architecture.md)) records the advisories, the 2.7.2 / 2.6.5 fixes and the 2.7.3 / 2.6.6 releases. It states that the old comparator confers no eligibility, and the AD-26 Production proof binds the reviewed supported-branch floor. The comparator itself still accepts `2.7.1` / `2.6.4`. | Security and Platform: review the supported branch and align the comparator |
| H5 | The erasure verifier has conflicting key-custody rules | Resolved | The implementation-status row ([line 587](../../../architecture.md)) names the bounded AD-24 custody decision. Story 4.16 keeps the digest ring for its signing-capable erasure verifier and qualifies it before distribution, and dedicated keys get their own owner. | — |
| H6 | The assurance rule conflates required and achieved independence | Resolved | AD-11 ([line 236](../../../architecture.md)) separates the roster-required level from achieved independence: distinct numeric identities, and the 24 hours measured from the authenticated subject-changing push. Downstream records carry the lowest achieved level. | — |
| H7 | Release appears to depend on its own successor | Resolved | AD-11 ([line 232](../../../architecture.md)) gives each transition its own inputs, and each chain ends at the requested state. | — |
| H8 | Consumer removal crosses two distinct publication subjects | Resolved | AD-22 ([line 321](../../../architecture.md)) lets a Platform-composed host run its own chain. The promoted profile links the two subjects by package ID, version and digest. | — |
| M1 | The global sequence can contain a gap | Resolved | AD-6 ([line 177](../../../architecture.md)) keeps aggregate sequences gapless and global positions unique and monotonic, with unused reserved positions left unused. | — |
| M2 | The legacy `UseNuGetDeps=false` alias selects source mode | Resolved in the spine; retained for the build | The AD-11 rule and its build-discrepancy note ([line 228](../../../architecture.md)) state the FR21 rule and record the discrepancy. `Directory.Build.props` lines 48–49 still map `UseNuGetDeps=false` to source mode. | Build owner: align property evaluation and tests, or obtain an amendment |
| M3 | Every fixture job is described as merge-blocking | Resolved | AD-12 ([line 244](../../../architecture.md)) distinguishes the merge-check policy from the rules observed on 2026-11-21. | — |
| M4 | G-NFR8 is described as waiting on Story 6.4 | Resolved | The status row ([line 613](../../../architecture.md)) records Story 6.3's approved specification gate and leaves runtime NFR8 acceptance with Story 6.4. | — |
| L1 | The provenance README describes hosted OQ8 evidence as pending | Resolved in the spine; retained for the README | AD-25 ([line 354](../../../architecture.md)) dates the README note to the 2026-11-10 import and points to the revision-specific run. [`docs/architecture/oq8/README.md`](../../../../../docs/architecture/oq8/README.md) line 41 still says CI evidence remains pending. | Story 4.17 owner: date the note or cite the run |

## Checks

| Check | Result |
| --- | --- |
| `uv run .claude/skills/bmad-architecture/scripts/lint_spine.py --workspace _bmad-output/planning-artifacts/architecture/architecture-eventstore-2026-07-05` | Zero findings |
| `sha256sum _bmad-output/planning-artifacts/architecture.md` | `caa4d4c23064f2df4e3d5d8f9fd0dd21fbe88944d34f3776aa67e6defb2c53b7`. This closure edits only this note and the memlog. |
| AD-26 ratification subject, recomputed by the AD-26 rule | `5070ebdcf50d9fe7a7013be67d5c872e60563acf2ca4031382d0123754080803` |
| Finding locations | Each line cited above was located in the current spine bytes on 2026-11-25 |

## What this closure does not do

It is not baseline approval, AD-26 ratification or an AD-11 attestation. G-BASELINE stays FAIL/BLOCKED until Story 9.3 validates and the owner approves one reconciled source set (steps S4–S7 of the proposal). The three retained items do not block S1. They remain open with their owners and are rechecked by the Story 9.3 comparison.
