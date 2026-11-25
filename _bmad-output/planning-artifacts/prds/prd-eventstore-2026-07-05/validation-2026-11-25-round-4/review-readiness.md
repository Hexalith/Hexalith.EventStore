# Phase 4 readiness and evidence review · round 4

Reviewed 2026-11-25 on the working tree of EventStore `main` at `517fedfe1357e2157e17d1d8621637a31cd0e961`. PRD SHA-256 `8ba4e8c044653fbcb17230d4886c9bef256f37ef81acc3cbb3bd27218fb20923`. Epics SHA-256 `3297793cbc74cb9e4ec42c98fba5d87385c90ae1565ae2c2f2f43641dc7a3aee`. Both digests match the brief. Lens: §0, §1.1, the §11.3 register, §11.4 gates and the final assessment, the OR register, seals and attestations, OQ8 runs, signed package baselines, dependency licensing, current versus retired authorization rules, `tools/phase_4_*.py` and the gate risk matrix.

## Verdict

For this lens, the PRD supports continued corrective work. All four assigned round-3 findings are resolved. §11.4 now names the sealable readiness request: 15 prerequisite inputs, and never a G-HIGH-RISK, G-READINESS or `READY` record. That definition matches the frozen readiness-decision spec, `tools/phase_4_readiness.py`, both matrix rows and the readiness fixtures, which passed. Active stories now take their authorization from the owning spec under AD-12. The architecture no longer prescribes a seal that blocks `main`, and it cites the revision-specific OQ8 run. This review found two new defects, neither critical nor high. The G-TENANT row and OR20 still list the G-TENANT gate validator as outstanding, although it was delivered on 22 November (medium). The PRD tells owners to "obtain" an AD-26 baseline disposition but does not record that the owner approved one on 25 November, or where its record is (low). Two residues are already routed and are not counted: the architecture readiness row at `architecture.md:586` and an implementation note in the readiness spec. Phase 4 readiness remains Reject. The frontmatter validation fields and the §0 lead wait for this round under P-11 and are not counted as findings.

## Disposition of round-3 findings in this lens

| Round-3 finding | Status | Current evidence |
| --- | --- | --- |
| H1 · The first readiness seal has no defined inputs | **Resolved** | `prd.md:729` now reads "the authenticated CI seal of a readiness request whose gate inputs are those 15 results, never a G-HIGH-RISK, G-READINESS or `READY` record" and links the spec. `spec-9-2-readiness-decision.md:21–25` (frozen, `done`) binds the 15 named prerequisites, then computes G-HIGH-RISK from those records plus the sealed attempt, then G-READINESS from 16. `tools/phase_4_readiness.py:7` removes both rows from `GATES`, and `:250` builds G-HIGH-RISK from `{**inputs, "readiness-attempt": attempt}`, followed by G-READINESS and `READY`. The matrix agrees: `phase-4-gate-risk-matrix.json:299` and `:345`. OR1 (`prd.md:815`) and the G-READINESS row (`:753`) are consistent with that order. Neither input depends on its own result. `test_readiness_sealed_order_source_hashes_and_minimum` starts from no prior readiness seal and derives the three ordered records with 17 `READY` inputs. `test_readiness_inventory_rejects_omission_duplicate_and_downstream_input` refuses an absent G-MVP-COVERAGE. Both passed. Routed residue, not counted: `architecture.md:586` still says "sealable inputs and the final writer still need an owner-defined contract", and the memlog routes that row to bmad-architecture. The spec's implementation note (`:53`) reports two enrolled consumers and 13 refusals. The code enrolls three: `GATE_DECISIONS` at `:8` includes G-STATUS-ID. The epic Story 9.2 binding order agrees with the code. |
| H6 · Mandatory corrective stories still require retired authorization tooling | **Resolved** | In the PRD, §0 (`prd.md:126`) and OR28 (`:801`) assign scope and evidence to the owning story or spec. The dependencies of Stories 3.19 (`epics.md:3278`), 3.20 (`:3319`), 5.13 (`:5046`), 7.21 (`:7031`), 9.2 (`:8041`, `:8043`), 9.3 (`:8121`), 9.4 (`:8170`) and 9.5 (`:8206`) cite the owner's scoped authorization in the owning spec under AD-12. Story 9.1 is marked superseded (`:8029`). The Epic 9 overview calls it historical (`:457`). Every other Story 9.1 mention says the deleted tooling "is not a prerequisite". In the architecture, the active status section (`architecture.md:580`) and AD-25 (`:364`) call the 9.1 machinery historical. No active requirement for a 9.1 record remains. |
| M3 · The architecture still prescribes a check that blocks `main` | **Resolved** | `architecture.md:244` says the seal "is never a branch-protection or ruleset check on `main`" and that a truthful FAIL "blocks only that transition and never `main`". `:586` says "An authenticated seal applies to one guarded transition". The PRD glossary (`prd.md:230`) and OR10 (`:772`) agree. `.github/workflows/phase-4-readiness.yml` is `workflow_dispatch`-only, and its line 24 states that a failure "never adds a required main check". |
| L2 · The architecture still lists all OQ8 hosted evidence as pending | **Resolved** | AD-25 (`architecture.md:355`) and the status row (`:603`) cite run [44892480313](https://github.com/Hexalith/Hexalith.EventStore/actions/runs/44892480313) on `df5dc226a`, refer to PRD §1.1 for earlier revisions and require a new hosted run for each changed candidate. GitHub reports fixtures and live evaluation successful and the assured decision skipped. The README's pending-CI note is labelled as dating from the 10 November import. The 25 November architecture closure note assigns it to Story 4.17. |

## New findings

### Medium · The G-TENANT row and OR20 still list the delivered gate validator as outstanding

**Location.** `prd.md:739` (G-TENANT current result: "The G-TENANT business validator and NFR2 story-completion adapter are outstanding.") and `prd.md:793` (OR20: "Complete the G-TENANT business validator and NFR2 story-completion adapter.").

**What is wrong.** On 22 November, `spec-9-2-tenant-gate-business-evidence.md` (status `done`) delivered the G-TENANT business evidence adapter, `tools/phase_4_tenant_gate.py`. The readiness verifier enrolls it: `tools/phase_4_readiness.py:8`. The approved 25 November proposal refers to "the 22 November G-TENANT business evidence verifier" at line 160 and says it is "done 22 November" at line 253. It defines the open Story 2.14 work as the thin NFR2 completion adapter that consumes this verifier (P-12). This PRD statement predates that delivery. A Story 2.14 or Test owner reading OR20 is told to build a component that already exists, and cannot tell whether OR20's first clause is complete. What actually remains is unclear: the NFR2 adapter, the Tenants evidence on shipping packages, the CI seal and the attestation.

**Correction.** In both places, state that the Story 9.2 G-TENANT gate adapter exists (link its spec) and that the NFR2 completion adapter, the shipping-package evidence, the seal and the owner attestation remain.

### Low · OR14 does not record that the AD-26 baseline disposition was approved

**Location.** `prd.md:783` (OR14: "obtain either per-role AD-26 ratification or the owner-approved AD-26 baseline disposition"), `prd.md:701` (§11.3 architecture row: "owner ratification of AD-26 or an approved replacement are outstanding") and `prd.md:737` (G-BASELINE). The same gap affects `epics.md:8121` (Story 9.3 dependencies).

**What is wrong.** Decision D3 in `sprint-change-proposal-2026-11-25.md:484` is the disposition record. It binds AD-26 as an unratified `[ASSUMPTION]` at subject `5070ebdcf50d9fe7a7013be67d5c872e60563acf2ca4031382d0123754080803` for G-BASELINE only, lapses if that digest changes, and is to be consumed by the Story 9.3 manifest. The PRD and Story 9.3 describe a disposition only as a possibility. They do not say that the owner granted one, its date, its subject digest or where its record is. The §11.3 register exists to record owner decisions and what they permit, yet it still lists the disposition as outstanding. A 9.3 implementer must therefore find the record in the proposal on their own. The recomputed subject is still `5070ebdc…`, so the disposition remains current. Route for bmad-architecture: commit `f6e5698e5` (DW-163) changed one AD-18 paragraph after the S1 closure. The spine moved from `caa4d4c2…` to `323ea5c9…` with no AD-26-impact line in the architecture memlog. The closure note therefore binds superseded bytes and needs a one-line memlog entry before S5.

**Correction.** Add one sentence to OR14 or to the §11.3 architecture row: "On 2026-11-25 the owner approved the AD-26 baseline disposition at subject `5070ebdc…` for G-BASELINE only ([proposal §7.1 D3](sprint-change-proposal-2026-11-25.md)); it lapses if that digest changes."

## Mechanical checks

- **Identity.** `sha256sum` of `prd.md` and `epics.md` matched the brief. `git diff HEAD` showed 5 PRD hunks (two `source_artifacts` entries, FR4-C3, FR26-C4, the §11.4 final assessment and the retired OR3 note) and 9 changed lines in the epics. `git diff --check` on both files exited 0.
- **Live risk matrix.** `python3 -B tools/validate-phase-4-gate-risk-matrix.py …phase-4-gate-risk-matrix.json` exited 1 and read PRD `8ba4e8c0…`. It reported 17 rows. The 5 standard controls passed classification. All 12 high-risk rows failed with `SEAL_MISSING`, `ATTESTATION_MISSING` and `ACHIEVED_BELOW_REQUIRED`. Seven also reported `BINDING_UNBOUND`: APPEND, COMPAT, AUTH-HOSTS, RUNTIME-PARITY, PUBLICATION-AUTH, CONSUMER and MVP-COVERAGE. The registry names 1 human. These results match `prd.md:749` and OR10.
- **Offline OQ8.** `python3 -B tools/validate-oq8-governing-design.py` exited 0 with design `1a55b030…`.
- **Fixture suites.** 289 tests passed:
  - gate risk matrix: 147;
  - gate decision: 19;
  - OQ8 governing design: 111;
  - `test_phase_4_transition.py -k readiness`: 10 (294 s);
  - `check_phase_4_readiness_schema.py`: 2.
- **Publication authority.** `python3 -B tools/validate-publication-authority.py evaluate` exited 1 with `"authority": "unproven"`, `"result": "FAIL"` and `"seal": null`. This matches `prd.md:747`.
- **Story 2.14 completion guard.** The `--target spec` and `--target tracker` commands each exited 1 with `SOURCE_MISMATCH`, because `prd.md` is uncommitted. That is the expected guard. The `ADAPTER_UNAVAILABLE` path could not be rechecked on these bytes.
- **Readiness code.** `GATES` contains 15 entries and `GATE_DECISIONS` 3 (OQ8, TENANT, STATUS-ID). The other 12 refuse with `ADAPTER_UNAVAILABLE` (`phase_4_readiness.py:123`). This matches the epic Story 9.2 binding order.
- **Hosted OQ8 runs.**
  - The four cited runs all report fixtures `success`, live evaluation `success` and the assured decision `skipped`: 44726206620 (`607072c68`), 44775644088 (`d677fba671`), 44780280952 (`db5db2470`) and 44892480313 (`df5dc226a`).
  - Run 44780280952, whose fixture job round 3 saw queued, has since completed successfully.
  - The 12 most recent OQ8 runs on `main`, through HEAD run 45712925585, all succeeded. The HEAD run skipped its assured job.
- **AD-26 subject.** Recomputed with the AD-26 byte procedure on the current `architecture.md` (`323ea5c9…`): `5070ebdc…`, unchanged. The S1 closure note binds `caa4d4c2…`.
- **Authorization rules.** Searched the epics and architecture for Story 9.1, authorization-record and corrective-work-authorization wording. The only active rules point to the owning spec under AD-12; every other hit is labelled historical or superseded. The dependencies of Stories 3.19, 3.20, 5.13, 7.21, 9.2, 9.3, 9.4 and 9.5 were read in full.
- **Tracker.** Compared 22 lifecycle values in `sprint-status.yaml` with PRD claims, including:
  - 2.14 in-progress, 2.15 review, 3.18 backlog, 3.19 in-progress, 3.20 backlog;
  - 4.5 in-progress, 4.16 backlog, 5.11 backlog;
  - 6.4 in-progress, 6.7 done;
  - 9.2 in-progress; 9.3, 9.4 and 9.5 backlog;
  - Epic 2 in-progress.

  All agree.
- **Workflows.** Six dedicated `phase-4-*` dispatch workflows exist, as `prd.md:749` states: consumer-removal, high-risk-gate-pass, high-risk-nfr-story-done, production-promoted, readiness and release-available. `oq8-governing-design.yml:47`, `:62` and `:98` run the commands cited in the G-OQ8 row and the glossary.
- **Signed baselines.** `.releaserc.json:20` invokes `tools/verify-release-api-compatibility.py`. `BASELINE_VERSION = "4.2.0"` is at `:35`. Neither file has changed since round 3's revision `db5db2470`.
- **Licensing.** The Builds gitlink is now `62876129`. `Props/Directory.Packages.props:370` still pins MediatR `15.0.0`, and the restored package's `LICENSE.md` contains the RPL terms. OR30 remains accurate. Its cited `e303acd8` is an earlier catalog commit that pins the same version.
- **Links.** All 40 local Markdown links in `prd.md` (38 unique) resolve, including the anchors for the 4.16 recheck and the 2.15 deferred-work entry. The one other match is a regex in NFR text at line 406, not a link. All 83 frontmatter `source_artifacts` paths exist.
