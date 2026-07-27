# Sprint Change Proposal — Story 2.12 Runtime-Identity Re-Scope

- Date: `2026-07-27`
- Trigger story: **2.12 — Tenants Runtime Identity Adoption And Package-Mode Validation**
- Prepared by: Amelia (Developer) via `bmad-correct-course`
- Decided by: `Administrator` / release owner `jpiquot`
- Source decision record: `_bmad-output/implementation-artifacts/evidence/story-2-12/rescope-decision-2026-07-27.md`
- Scope classification: **Moderate** (acceptance-criteria + architecture-decision amendment; no epic added, removed, or resequenced)

---

## 1. Issue Summary

Two of Story 2.12's five acceptance criteria became **impossible to satisfy as literally written**.
Neither failure was caused by the implementation; both are properties of the surrounding
repository automation and of artifact retention.

### AC2 — the frozen source pin is not durable

Story 2.12 (and AD-22) require Tenants' `references/Hexalith.EventStore` gitlink to equal the
frozen owner-approved SHA `fa2d1c9910f8976553adb33dcdb1c9ff2ea75594`. That pin has been
overwritten **five times** on Tenants `main` since it was correctly adopted:

| Tenants `main` | `references/Hexalith.EventStore` | Cause |
| --- | --- | --- |
| `902065e` / `db09a84` | `fa2d1c9910f8` | approved adoption (correct) |
| `230a533d` | `737b3e5a` | `/pushall` mechanical merge clobber |
| `4ca5f86` | `b2d34025` | automated `build(deps)` bump |
| `f1053a31` | `c8c70030` | automated `build(deps)` bump, **observed live mid-session** |

`git log 902065e..HEAD -- references/Hexalith.EventStore` counts 5 moves. The approved SHA is now
46 commits behind EventStore `main`. Restoring the pin is **proven green** (unpublished proof
commit `b8698e9d`: verifier + source-consumer procedure exit 0), but provably ephemeral — it
would regress within hours for the third time.

### AC3 — the approved package bytes do not exist

AC3 requires byte-equality against the 14 `.nupkg` files at `999.1.20-proof.fa2d1c9910f8`
(manifest SHA-256 `4271ddc7…dbe0bc`). Every avenue the External Prerequisite Contract names has
now been executed and returned negative:

| Avenue | Result |
| --- | --- |
| Original transient build directory | Deleted; 5 *other* runtimes survive, the approved one does not |
| Whole-filesystem scan (incl. mounted Windows volumes) | **0 of 14** approved `Hexalith.EventStore*` `.nupkg` |
| nuget.org | Approved proof version absent (87 published Gateway versions; nearest `3.83.0`) |
| Azure WORM raw-evidence archive (locked to 2033-08-01) | Logs and manifests only, no `.nupkg` |
| Retained GitHub Actions artifacts | Test-result bundles only |
| GitHub Packages, **with `read:packages` granted** | 185 org NuGet packages, **no** `Hexalith.EventStore*` at any version |

AC3 compares against artifacts that no longer exist anywhere. This is not a retrieval gap to be
retried — it is a closed, negative result.

### Issue category

**Technical limitation discovered during implementation** (artifact non-retention) compounded by a
**failed approach** (frozen consumer pin under automated submodule bumping).

---

## 2. Impact Analysis

### Epic impact

| Item | Assessment |
| --- | --- |
| Epic 2 (containing 2.12) | **Completable as planned** with amended AC2/AC3. No epic added, removed, or resequenced. |
| Stories 2.4–2.7, 2.10, 2.11 | **No impact.** All are `done`; none depends on the frozen pin or the proof-version bytes. |
| Story 1.20 | **No impact.** Remains `done`; its A/B/C verifier passes from EventStore `main` `c8c70030` (exit 0). Its authority is preserved as the activation gate; only its *pins* become historical. |
| Story 3.12 | **No impact.** Governed by AD-11/AD-12 and AD-22 *deployed* mode, which this proposal does not touch. |
| Parties Story 8.6 | **Explicitly unaffected** — protected by the scoped-carve-out choice below. |

### Artifact conflicts

| Artifact | Conflict | Action |
| --- | --- | --- |
| `epics.md` Story 2.12 AC2 (`:1528-1531`) | Requires the frozen SHA | **Replace** |
| `epics.md` Story 2.12 AC3 (`:1533-1536`) | Requires approved version + byte equality | **Replace** |
| `epics.md` Story 2.12 Focused validation (`:1516`) | Names "exact package-byte/hash verification" | **Replace** |
| `epics.md` Guardrails (`:290`) | Restates AD-22 with no exception | **Amend** — add scoped-exception pointer |
| `epics.md` Parties parity gate (`:135`) | Could be read as re-scoped for Parties | **Amend** — add a non-extension sentence |
| `architecture.md` **AD-22** (`:298-306`) | Source clause and package clause both contradicted | **Amend** — append a dated, scoped exception; general rule retained |
| Story file `2-12-…md` | AC2/AC3, pins table, External Prerequisite Contract, tasks, Dev Notes, frontmatter | **Update** |
| `evidence/story-2-12/prerequisites.md` | Status `blocked` on a now-retired requirement | **Append** closing disposition |
| `sprint-status.yaml` | — | **No change.** 2.12 stays `in-progress`; no epic/story added, removed, or renumbered |
| **`prd.md`** | — | **No change.** `:275` (NFR9) and `:289` govern EventStore's own release reproducibility and source opt-in, not consumer identity. FR15/FR21/FR22/NFR9/NFR12/NFR16 all remain satisfiable under the amended ACs |
| **`ux.md`** | — | **No change.** Dependency-identity story; no UX surface |

### Technical impact

- **No code change** is required by this proposal. The Gateway conditional alignment (`a7ca142`)
  and its `PackageGovernanceTests` host rule are already published on Tenants `main` and pass.
- **No dependency identity change** is required. Tenants `main` already satisfies the amended
  AC2 and AC3 as written below:
  - gitlink `c8c70030` == submodule checkout `HEAD` == EventStore `origin/main` HEAD (reachable ✓)
  - Builds pin `1b1c0b0` → catalog `HexalithEventStoreVersion` = `3.83.0`, **published on
    nuget.org**, with the `Hexalith.EventStore.Gateway` entry from PR #47 retained (AC4 ✓)
- **Re-validation is required.** The green dual-mode matrix (Contracts 115/115, Server 738/738,
  UI 1266/1266, Integration 167 passed / 1 skipped, `--warnaserror` 0/0, in *both* modes) was run
  on a proof clone pinned back to `fa2d1c9910f8`. Under the amended AC2 the binding pin is
  whatever Tenants `main` carries, so the matrix must be re-run at the accepted `main` commit.

---

## 3. Recommended Approach

**Selected path: Direct Adjustment (Option 1).**

| Option | Verdict | Rationale |
| --- | --- | --- |
| **1 — Direct Adjustment** | ✅ **Selected**. Effort **Low**, risk **Low** | Amend AC2/AC3 and record a scoped AD-22 exception. The implementation, tests, and dependency graph already satisfy the amended criteria; only re-validation remains. |
| 2 — Rollback | ❌ Not viable | Nothing to roll back. The Gateway alignment is correct and passing; the premature proof-version Builds pin was already reverted (`bb02cdc8` → `3.82.0`, now `1b1c0b0` → `3.83.0`). Rolling back would *re-break* `main`. |
| 3 — PRD MVP review | ❌ Not needed | MVP is unaffected. No FR/NFR is dropped; FR21/FR22/NFR9/NFR16 remain enforced by the amended criteria and by AD-11/AD-12, which are untouched. |

### Design decisions taken (owner-confirmed 2026-07-27)

1. **AC2 shape — reachable-on-`main` + checkout equality.** The gate is not removed, it is made
   durable: the gitlink may be any commit reachable from EventStore `origin/main`, but must equal
   the submodule checkout `HEAD`, must not involve edited EventStore content or nested submodule
   initialization, and the exact validated SHA must be recorded. The automated bump becomes legal;
   gitlink/checkout drift and off-`main` commits still **fail closed**.
2. **AD-22 — scoped carve-out, not a global rewrite.** AD-22 keeps its exact-SHA / byte-equal rule
   as general policy and gains one dated exception naming Story 2.12 and the irrecoverable-bytes
   finding. Parties Story 8.6, deployed-mode closure, and every future consumer remain under the
   strict rule.
3. **AC1 and the 1.20 linkage survive.** AC1 stays as the fail-closed activation gate (satisfied
   2026-07-27, verifier exit 0). The *Activation Decision And Immutable Pins* table is relabelled
   **historical record** rather than binding pins. The audit trail is preserved; activation is not
   re-litigated.
4. **AD-12 is not weakened.** Byte equality is waived; persisted-path evidence is not. The amended
   AC3 requires evaluated `project.assets.json` proof, and AC5 continues to require the
   projection/query/provenance/freshness suites.

### Risk assessment

| Risk | Mitigation |
| --- | --- |
| The waiver is later read as general precedent | The AD-22 exception is dated, names one story and one consumer, and states explicitly that it confers no authority elsewhere. `epics.md:135` gets a matching non-extension sentence. |
| Tracking `main` loses the exact-tested-runtime guarantee | Accepted trade-off, stated in the exception text. Mitigated by requiring the evidence to record the exact SHA the matrix ran against, so any closure is still bound to one reproducible commit. |
| The gitlink moves again between validation and review | Real and expected. Handled procedurally: the accepted evidence names its SHA; a later bump does not invalidate a receipt that identifies its own commit. |
| Optional hardening (a Tenants CI reachability check) not adopted | Deliberately **out of scope** for 2.12 — it is Tenants-repo work. Recorded below as a follow-up candidate, not a gate. |

---

## 4. Detailed Change Proposals

### 4.1 `_bmad-output/planning-artifacts/epics.md` — Story 2.12, Focused validation (`:1516`)

**OLD**

> **Focused validation:** separate Debug/source and Release/package restores/builds; scoped Tenants Contracts, Integration, UI, and Server tests; exact package-byte/hash verification; and no mixed source/package EventStore graph.

**NEW**

> **Focused validation:** separate Debug/source and Release/package restores/builds; scoped Tenants Contracts, Integration, UI, and Server tests; evaluated `project.assets.json` dependency-type and version verification in each mode; and no mixed source/package EventStore graph.

**Rationale:** byte/hash verification is retired with AC3; the evaluated-assets check is what
actually remains provable and is what AD-12 requires.

---

### 4.2 `epics.md` — Story 2.12 **AC2** (`:1528-1531`)

**OLD**

> **Given** Story 1.20 authorizes migration and names the approved EventStore source SHA
> **When** Debug/source mode is adopted
> **Then** `references/Hexalith.EventStore` gitlink and checkout both equal that SHA, no EventStore submodule content is edited
> **And** only Tenants-root-declared submodules are initialized.

**NEW**

> **Given** Tenants tracks EventStore `main` through its automated `build(deps)` submodule bump rather than a frozen owner-approved pin
> **When** Debug/source mode is validated
> **Then** `references/Hexalith.EventStore` gitlink equals the checked-out submodule `HEAD`, that commit is reachable from EventStore `origin/main`, and no EventStore submodule content is edited
> **And** only Tenants-root-declared submodules are initialized, and the recorded evidence names the exact EventStore SHA the validation matrix was run against.

**Rationale:** Decision 1. Keeps a real fail-closed identity gate (drift and off-`main` commits
still fail) while surviving the repository's own automation.

---

### 4.3 `epics.md` — Story 2.12 **AC3** (`:1533-1536`)

**OLD**

> **Given** the approved package version and hashes
> **When** Release/package mode restores from an isolated cache
> **Then** every resolved `Hexalith.EventStore*` asset is a package at the exact version, fetched bytes match the approved hashes
> **And** the selected Builds commit already exposes that version.

**NEW**

> **Given** the Tenants-pinned Builds commit declares a single published `HexalithEventStoreVersion` and centrally declares every consumed `Hexalith.EventStore*` package under it
> **When** Release/package mode restores
> **Then** every resolved `Hexalith.EventStore*` asset is `type: package` at exactly that catalog version, that version is resolvable from the configured public package source, and zero EventStore project edges remain, including transitive ones
> **And** no `Version`, `VersionOverride`, fallback property, or Tenants-local `PackageVersion` entry supplies that version, and the evaluated `project.assets.json` files are the recorded evidence.

**Rationale:** Decision 2. Byte equality against a non-existent manifest is replaced by an
identity gate that is both real and verifiable: exact catalog version, package-typed edges, zero
project edges, no local version authority (preserving AD-11's central-catalog rule), evaluated
assets as evidence (preserving AD-12's persisted-evidence rule).

---

### 4.4 `epics.md` — Guardrails, AD-22 restatement (`:290`)

**OLD**

> - AD-22 requires owner-approved exact EventStore artifact identity before consumer infrastructure removal; use source SHA, package versions/hashes, or deployed image digest as applicable, never the consumer repository SHA.

**NEW**

> - AD-22 requires owner-approved exact EventStore artifact identity before consumer infrastructure removal; use source SHA, package versions/hashes, or deployed image digest as applicable, never the consumer repository SHA. One dated, scoped exception exists: Story 2.12 (Tenants) validates a tracked-`main` source commit and the published Builds-catalog package version because the Story 1.20 approved package bytes were proved unrecoverable. The exception is recorded in AD-22, is limited to that story and that consumer, and extends to no other consumer or mode.

---

### 4.5 `epics.md` — Parties Projection/Query Parity Gate (`:135`)

**OLD** (end of paragraph)

> …Source-mode consumers verify the EventStore submodule SHA; package-mode consumers verify exact package versions and hashes; deployed consumers verify the image digest maps to the approved EventStore SHA. The consuming repository SHA is never compared to the EventStore SHA.

**NEW** (same paragraph, one sentence appended)

> …The consuming repository SHA is never compared to the EventStore SHA. The dated Story 2.12 tracked-`main` / published-catalog exception recorded in AD-22 is scoped to Tenants and does not relax these rules for Parties Story 8.6 or any other consumer.

**Rationale:** prevents the carve-out from silently propagating to the Parties gate — the single
highest-value guard in this whole proposal.

---

### 4.6 `_bmad-output/planning-artifacts/architecture.md` — **AD-22** (append after `:306`)

**NEW** (appended as a final indented block within AD-22; the existing Rule and the three
source/package/deployed clauses are **unchanged**)

> **Scoped exception — Story 2.12 (Tenants), recorded 2026-07-27.** The Story 1.20 approved package bytes at `999.1.20-proof.fa2d1c9910f8` were proved unrecoverable from every avenue the packet named — whole-filesystem scan, nuget.org, the locked Azure WORM raw-evidence archive, retained GitHub Actions artifacts, and GitHub Packages with `read:packages` granted (0 of 14 present; the org exposes 185 NuGet packages and none is a `Hexalith.EventStore*` package). The approved source SHA `fa2d1c9910f8976553adb33dcdb1c9ff2ea75594` was additionally overwritten five times on Tenants `main` by a `/pushall` merge and recurring automated `build(deps)` submodule bumps, so a frozen consumer pin is not durable under current repository automation. For Story 2.12 only, the release owner accepted a re-scoped identity gate: **source mode** proves the `references/Hexalith.EventStore` gitlink equals the submodule checkout `HEAD` and that commit is reachable from EventStore `origin/main`, with the validated SHA recorded in the evidence; **package mode** proves every resolved `Hexalith.EventStore*` asset is `type: package` at exactly the Tenants-pinned Builds catalog's published `HexalithEventStoreVersion`, with zero EventStore project edges and no consumer-local version authority. AD-11's central-catalog rule and AD-12's persisted-path evidence requirement apply unchanged; only byte equality against the retired 14-package manifest is waived. This exception is dated, names one story and one consumer, and confers no authority on Parties Story 8.6, on any other consumer, on deployed-mode closure, or on any future frozen-identity relief. A later consumer requiring equivalent relief needs its own approved amendment.

**Rationale:** the carve-out lives inside AD-22 so no reader of AD-22 can miss it, while the
[ADOPTED] general rule and its title remain intact for every other binding.

---

### 4.7 Story file `_bmad-output/implementation-artifacts/2-12-…md`

| Section | Change |
| --- | --- |
| Frontmatter | `package_lane_status: blocked` → `re-scoped`; add `rescope_decision:` and `sprint_change_proposal:` pointers; retain `authorization_story: 1-20` |
| **AC2** | Replace with the AC 4.2 text in numbered-list form |
| **AC3** | Replace with the AC 4.3 text in numbered-list form |
| **Activation Decision And Immutable Pins** | Retitled *Activation Decision And Historical Pins*; table relabelled **historical record**, not binding; the proof version, 14-package manifest SHA-256, and per-package hashes marked retired |
| **Known Completion Gate At Creation** | Marked resolved/superseded — its three conditions are answered by the amended ACs |
| **External Prerequisite Contract** | Marked **retired**; its surviving useful part (the central `Hexalith.EventStore.Gateway` catalog entry, still required by AC4) called out as retained |
| **Tasks** | Source-identity task rewritten to the reachable-on-`main` check; package-lane task rewritten to catalog-version/assets verification (isolated-cache, source-mapping, manifest, and byte-compare subtasks removed); a re-validation task added |
| **Dev Notes → AD-22 bullet** | Rewritten to cite the scoped exception |
| **Change Log** | New dated entry recording this proposal |

*(Full before/after text applied at implementation; every change is mechanical from §4.2–§4.6.)*

---

### 4.8 `evidence/story-2-12/prerequisites.md`

Append a closing section: overall status `blocked` → **`superseded`**. The Builds catalog
prerequisite stays `passed` (its Gateway entry survives in `1b1c0b0`). The original package-byte
prerequisite is recorded as **retired by owner decision**, not as satisfied — the negative audit
stands in full as the justification for the AD-22 exception.

**Rationale:** the audit is the evidence for the amendment; it must not be rewritten to look
resolved.

---

## 5. Implementation Handoff

**Scope: Moderate.** No epic added, removed, or resequenced; `sprint-status.yaml` needs no
structural edit. But acceptance criteria and an adopted architecture decision change, so this is
above a direct developer fix.

| Recipient | Responsibility |
| --- | --- |
| **Amelia (Developer)** — this workflow | Apply §4.1–§4.8 exactly as approved. No code, dependency identity, or test change. |
| **Winston (Architect)** | Ratify the AD-22 scoped exception text and confirm the Parties 8.6 non-extension sentence is sufficient. |
| **Amelia (Developer)** — next session, `bmad-dev-story` | Re-run the dual-mode matrix at the **accepted Tenants `main`** commit under the amended criteria, then take 2.12 to `review`. |
| **`jpiquot` (Tenants maintainer)** | Approve the exact accepted Tenants SHA; AC5's maintainer-approval clause is unchanged. |

### Success criteria for closure

1. Dual-mode matrix re-run at the accepted Tenants `main` commit (currently `0ded4a1`, ES gitlink
   `c8c70030`, Builds `1b1c0b0` → catalog `3.83.0`), recording that exact SHA:
   Contracts, Server, UI, Integration green in **both** modes, `--warnaserror` 0/0.
2. AC2 proof: gitlink == checkout `HEAD`, reachable from EventStore `origin/main`, clean
   worktrees — run on a **pristine checkout before the lane's restore** (MSBuild writes ignored
   `obj/` artifacts into the EventStore submodule and trips `--ignored=matching`).
3. AC3 proof: evaluated `project.assets.json` shows every `Hexalith.EventStore*` edge
   `type: package` at `3.83.0`, zero project edges; source mode shows 7 project edges / 0 package
   edges. Both directions already proved structurally — re-prove at the accepted commit.
4. AC4 unchanged and already satisfied: Gateway and DomainService resolve identically in both
   directions; no mixed graph reachable.
5. Maintainer-approved Tenants SHA recorded; EventStore `references/Hexalith.Tenants` gitlink
   advanced to it; root pointer/cleanliness guards re-run.

### Explicitly out of scope

- A Tenants CI check enforcing gitlink reachability (**candidate follow-up**, Tenants-repo work —
  not adopted as an AC, per the owner's AC2 choice).
- Rebuilding the 14 packages from `fa2d1c9910f8` under change control — retired with the
  External Prerequisite Contract; no longer needed.
- The deferred sibling mutation gates (member/config/metadata/lifecycle), the pre-Story-4.7
  producer alias, and the legacy Fluent-v4 token debt.

---

## 6. Checklist Record

| § | Item | Status |
| --- | --- | --- |
| 1.1 | Triggering story identified | [x] Done — Story 2.12 |
| 1.2 | Core problem defined and categorised | [x] Done — technical limitation + failed approach |
| 1.3 | Initial impact and evidence gathered | [x] Done — 5-move pin table; 6-avenue negative audit |
| 2.1 | Current epic still completable | [x] Done — yes, with amended AC2/AC3 |
| 2.2 | Epic-level changes required | [x] Done — modify AC only; no epic added/removed/redefined |
| 2.3 | Remaining epics reviewed | [x] Done — Epic 3 (3.12) and Epic 8 unaffected |
| 2.4 | Epics invalidated / new epics needed | [N/A] — none |
| 2.5 | Epic order or priority change | [N/A] — none |
| 3.1 | PRD conflicts | [x] Done — **none**; NFR9 and §8.1 govern EventStore's own release, not consumer identity |
| 3.2 | Architecture conflicts | [!] Action-needed — **AD-22** amended (§4.6); AD-11 and AD-12 untouched and re-affirmed |
| 3.3 | UI/UX conflicts | [N/A] — dependency-identity story, no UX surface |
| 3.4 | Other artifacts | [!] Action-needed — story file, prerequisites receipt; `sprint-status.yaml` unchanged |
| 4.1 | Option 1 Direct Adjustment | [x] **Viable — selected**; effort Low, risk Low |
| 4.2 | Option 2 Rollback | [x] Not viable — nothing to roll back; would re-break `main` |
| 4.3 | Option 3 PRD MVP review | [x] Not viable/needed — MVP unaffected |
| 4.4 | Path selected with rationale | [x] Done — Option 1, §3 |
| 5.1-5.5 | Proposal components | [x] Done — §1–§5 |
| 6.4 | `sprint-status.yaml` update | [N/A] — no epic/story added, removed, or renumbered; 2.12 stays `in-progress` |

---

## 7. Scope Statement

This proposal amends acceptance criteria and one architecture decision. It changes **no code, no
test, no dependency identity, and no published repository state**. Story 2.12 remains
`in-progress` and below `review` until the re-validation in §5 completes.
