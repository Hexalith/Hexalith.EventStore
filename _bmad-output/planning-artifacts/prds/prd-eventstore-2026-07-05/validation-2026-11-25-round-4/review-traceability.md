# EventStore requirements traceability review (round 4)

Reviewed the working tree of EventStore `main` at `517fedfe1357e2157e17d1d8621637a31cd0e961`: `prd.md` SHA-256 `8ba4e8c044653fbcb17230d4886c9bef256f37ef81acc3cbb3bd27218fb20923` and `epics.md` SHA-256 `3297793cbc74cb9e4ec42c98fba5d87385c90ae1565ae2c2f2f43641dc7a3aee`, both with today's uncommitted edits. Line numbers refer to those bytes. Planning documents are under `_bmad-output/planning-artifacts/`. The sprint tracker is `_bmad-output/implementation-artifacts/sprint-status.yaml`.

## Verdict

All three assigned round-3 findings are resolved. The FR7 clause owners now declare their clauses, Story 9.4 checks FR15 against §11.1, and the Epic 3 overview matches the tracker for Story 3.21. The two ledgers mirror each other exactly. All 56 FR/NFR definitions are identical. All 49 clause rows have identical primary and supporting owners, and today's FR4-C3 supporting role for Story 2.18 appears in both ledgers and in Story 2.18's own declaration. The 37 §11.1 rows and the 19 §11.2 rows agree with every detailed story declaration. The repository's coverage reader accepts 143 of 146 story headings, with the same three historical exceptions as round 3.

The remaining defects lie below the clause ledger, in the story-level ownership rules that Stories 9.4 and 9.5 must enforce. Seven NFRs still have competing unqualified primary claims, which the epic Primary-ownership rule forbids. NFR17 has three primary claimants outside its five-clause partition. Sixteen clause owners name their clause only in prose. UX-DR42 has no primary owner. Twelve detailed stories still report a lifecycle state that the tracker contradicts. None of these makes the PRD unusable, so there are no critical or high findings: two are medium and three are low.

## Disposition of round-3 findings in this lens

| Round-3 finding | Disposition | Current evidence |
| --- | --- | --- |
| H5 · FR7 clause owners disagree with the detailed stories | **Resolved** | Both ledgers assign FR7-C1 to Story 1.17, C2 to 1.6, C3 to 1.15, C4 to 1.18 and C5 to 1.19 (PRD 440–444; epics 344–348). Each of these stories now declares its clause as primary: 1.6 at epics 724 ("Primary FR7-C2"), 1.15 at 1093 ("… and FR7-C3 (coordinated persistence)"), 1.17 at 1204 ("Primary FR7-C1"), 1.18 at 1260 ("Primary NFR6 and FR7-C4") and 1.19 at 1316 ("Primary NFR8 and FR7-C5"). No story still claims FR7 as supporting while owning a clause, and none makes an unqualified FR7 claim. Story 9.4 AC1 (epics 8177) requires "each clause's primary story declares the same clause … including all five FR7 clauses", and the G-CLAUSE row (PRD 742) asks Story 9.4 to verify primary and supporting assignments "against the detailed stories". The fix arrived in committed `8627d32cb`, under the approved 21 November proposal. Today's run added the FR5-C3 identifier to Story 1.15. Residual: Story 1.17's same declaration still contains an unqualified "NFR12", which is reported under new finding 1. |
| M2 · Story 9.4 compares FR15 against a ledger with no FR15 rows | **Resolved** | Story 9.4 AC1 (epics 8177) now reads "Check FR12 and FR36 against those ledgers. Check FR15’s six slices against PRD §11.1 and the epic FR15 completion rule." PRD §11.1 (line 621) and the epic FR15 completion rule (epics 477) name the same six slice owners: 2.4, 2.5, 2.6, 2.11, 2.12 and 2.14. Each of those stories declares its FR15 slice as primary (epics 1626, 1671, 1722, 1961, 2012 and 2121). No other story claims primary FR15. The text came from `8627d32cb`. |
| L1 · Epic 3’s overview sends an active qualification story back to backlog | **Resolved** | The Epic 3 story set (epics 406) now reads "Story 3.21 (in progress; [qualification spec](…))" and "3.19 (in progress)". This matches the tracker (lines 148 and 154) and `spec-3-21-…` frontmatter `status: 'in-progress'`. The link resolves. The 3.21 detailed section also says "Story 3.21 remains in progress" (epics 3391). Residual: Story 3.19's own detailed paragraph still says "Backlog" (epics 3307). That drift is reported under new finding 5. |

## New findings

### 1. Medium · Seven NFRs still have competing unqualified primary owners

**Location.** Epic Primary-ownership rule at epics 473; story declarations listed below; PRD §11.2 rows at lines 661, 663–666, 669 and 670; G-MVP-COVERAGE at PRD 752 and OR27 at PRD 800.

**What is wrong.** The rule says every NFR has exactly one primary owner, or explicitly named disjoint slices, and that "an unqualified primary claim on a requirement another story also claims unqualified is forbidden." The following unqualified claims remain:

- **NFR7:** 4.1 (3473), 4.2 (3516), 4.4 (3622), 4.5 (3685)
- **NFR9:** 3.8 (2664), 3.11 (2832), 3.14 (2995)
- **NFR10:** 3.1 (2282), 3.7 (2608)
- **NFR11:** 3.8, 3.14
- **NFR12:** 1.17 (1204), 3.13 (2949), 3.15 (3054)
- **NFR15:** 7.4 (5864, "…slice, NFR15, and UX-DR22"), 7.19 (6841)
- **NFR16:** 1.20 (1372), 3.10 (2776), 3.13, 3.14

That makes 20 claims across 16 stories.

§11.2 copies these claims into its primary column as "Declared" without disposing of them. G-MVP-COVERAGE and OR27 require Story 9.5 to reject duplicate ownership. G-NFR-OWNERSHIP (PRD 751) covers only NFR12 among these seven, so no story or refinement is assigned to resolve the other six before Story 9.5's validator fails on them.

NFR7 shows the conflict most clearly. Story 4.5 declares "Primary FR31 and NFR7", but PRD OR3 (line 830) and §11.3 (line 704) say its observation cannot authorize NFR7 closure, and Story 4.16 owns class (c).

Today's update made this correction for FR5 and FR36 "so the Story 9.4 declaration check can pass" (memlog line 73), but it did not make the same correction for these NFR claims.

Lower-risk cases belong in the same pass. NFR6, NFR8 and NFR14 each have one unqualified claim beside named slices: 1.18, 1.19 and 7.14. For NFR4, Story 5.3's "committed-secret removal" slice overlaps Story 7.6's "committed-secret exclusion" claim.

**Correction.** Name the slice each of the 20 claims delivers, as Story 4.16 does with "NFR7 class (c)", or demote the claim to supporting and move its §11.2 entry. The coverage reader compares only the union of NFRs, so its result stays the same.

### 2. Medium · NFR17 has three primary claimants outside its five-clause ledger

**Location.** PRD §7.1, lines 427 and 475–479; epic ledger at 379–383; Stories 3.14 (epics 2995), 5.6 (4633) and 5.8 (4791); PRD §11.2 NFR17 row at line 671.

**What is wrong.** Both ledgers divide NFR17 into five clauses, C1–C5. Their primary owners are Stories 7.6, 7.7, 7.8, 7.9 and 7.21, and no supporting stories are named. Three other stories also declare primary NFR17 ownership:

- Story 3.14: "primary NFR9, NFR11, NFR16, and NFR17", with no qualifier
- Story 5.6: "the AppHost-loaded runtime slice of FR32, NFR2, and NFR17"
- Story 5.8: "the topology-verification slice of FR32, NFR2, NFR16, and NFR17"

§11.2 lists all three as primary and also notes that §7.1 assigns C1–C5 to the Epic 7 stories.

Story 9.4 AC1 checks only whether each clause owner declares its clause, so it passes while two answers remain. Under the first, the Story 5.6 and 5.8 slices are additional closure conditions, but they are missing from the rule that "a parent requirement closes only when every listed clause passes". Under the second, their primary claims carry no authority.

**Correction.** Change the three NFR17 claims to supporting, naming the clause each one serves (for example, 3.14 → NFR17-C4), and move them to §11.2's supporting column. If the owner wants them to remain closure conditions, add them as named supporting stories on the relevant clause rows in both ledgers, as was done for FR4-C3 today.

### 3. Low · Sixteen clause owners declare their clause only in prose

**Location.** Story 9.4 AC1 at epics 8177. The prose-only declarations are:

- FR12-C1: Story 2.2 (1525)
- FR26-C1 to C4: Stories 5.1–5.4 (4369, 4417, 4471 and 4527)
- FR33-C1 to C4: Stories 6.1–6.4 (5152, 5217, 5280 and 5350)
- FR34-C1 to C7: Stories 7.1–7.7 (5678, 5743, 5801, 5864, 5927, 5990 and 6077)

**What is wrong.** Thirty-three of the 49 clause owners name their clause ID. The other 16 use slice prose under the parent requirement. These prose claims satisfy the Primary-ownership rule, but a literal check of "declares the same clause" fails for all 16. That undercuts the memlog's reason for adding the FR5 and FR36 identifiers today.

The Story 2.2 prose, "generator discovery, controller-emission, gateway-delegation, and query-metadata-header slice", also leaves out three parts of FR12-C1: diagnostics, `304` and safe Problem Details. Its acceptance criteria cover `304` and Problem Details but not generator diagnostics, which the repository tests only in `tests/Hexalith.EventStore.RestApi.Generators.Tests/RestApiDiagnosticTests.*.cs`.

**Correction.** Append each clause ID in parentheses to these 16 declarations, as was done for FR5-C1–C3 and FR36-C1/C2.

### 4. Low · UX-DR42 has no primary story owner

**Location.** epics 287 (UX-DR42 definition), 399 (Epic 2 cross-cutting coverage), 441 (Epic 7 owns "UX-DR1-UX-DR41") and 473 (the rule); support-only declarations at 800, 1525, 1575, 1671, 1722 and 1864.

**What is wrong.** Exactly one story is primary for each of UX-DR1 to UX-DR41, and none is duplicated. UX-DR42, covering the Sample accepted-submission and Tenants projection-confirmed UI flows, appears only in supporting declarations. These include Story 1.8's "Supports primary FR9, NFR14, and UX-DR42", which, like every other "Supports primary" declaration, means supporting coverage. This violates the epic rule only and does not affect a PRD requirement, which is why the severity is low. The owner of this correction is the epics owner.

**Correction.** Name one primary story for UX-DR42 in that story's declaration.

### 5. Low · Twelve detailed stories report a lifecycle state that the tracker contradicts

**Location.** The latest "Current reconciliation" paragraph in each story, compared with the tracker:

| Story | Epics line | Epic state | Tracker line | Tracker state |
| --- | --- | --- | --- | --- |
| 2.13 | 2081 | Backlog | 96 | done |
| 2.16 | 2220 | Backlog | 104 | done |
| 2.17 | 2246 | Backlog | 110 | done |
| 3.19 | 3307 | Backlog | 148 | in-progress |
| 5.5 | 4583 | backlog | 209 | in-progress |
| 5.12 | 5034 | In-progress | 221 | done |
| 5.13 | 5080 | Backlog | 222 | done |
| 8.3 | 7265 | in progress | 287 | done |
| 8.4 | 7338 | backlog | 288 | done |
| 8.5 | 7416 | backlog | 289 | done |
| 8.6 | 7494 | Backlog | 296 | in-progress |
| 8.12 | 7954 | Backlog | 307 | done |

**What is wrong.** The PRD and the epic overviews already state the current state for several of these stories. Story 2.13 is closed (epics 398). Stories 2.16 and 2.17 are done (PRD 798–799). Story 3.19 is in progress (PRD 747 and 802; epics 406). The detailed sections still tell a story owner otherwise.

The paragraphs for 2.13, 5.5, 8.4, 8.5 and 8.6 are undated. G-BASELINE (PRD 737) and OR15 (PRD 784) acknowledge that lifecycle reconciliation remains open, and they assign the guard to Story 9.3. This list adds the exact cases that guard will find.

**Correction.** Replace each label with the tracker state, or add a dated pointer to the tracker. Start with 2.13, 2.16, 2.17 and 3.19, whose states the PRD already records.

## Mechanical checks

- **Bytes.** Ran `sha256sum` on both files before reading and again before writing. Both matched the requested digests, at HEAD `517fedfe1`. Ran `git diff` on `prd.md`, `epics.md` and `.memlog.md` to identify today's edits: 10 PRD lines and 18 epics lines changed.
- **Requirement definitions.** Parsed the PRD §6–§7 tables and the epics Requirements Inventory and compared them as exact strings. Result: 56 of 56 identical (37 FRs and 19 NFRs), with no duplicates and no extra IDs on either side.
- **Clause ledgers.** Parsed PRD §7.1 (lines 431–479) and the epic Stable Clause Ledger (lines 335–383). Result: 49 of 49 clause IDs, no duplicates, and identical primary and supporting sets. All 49 epic evidence cells read `pending`.
- **Primary declarations.** Extracted 146 `### Story` headings: 142 numeric stories plus 6.5a–6.5d. Of these, 145 have exactly one `**Requirements coverage:**` line; historical Story 4.8 has none. All 49 clause owners make a primary claim on the parent requirement. Thirty-three name the clause ID directly or through a range such as "FR4-C1 through FR4-C4"; the other 16 are listed in finding 3.
- **Supporting declarations.** All 19 named supporting roles in the ledgers also appear in the supporting stories' own declarations. In the reverse direction, the only clause IDs mentioned outside their roles are Story 2.2's references to Story 2.15's FR12-C3 and FR12-C4, which do not claim ownership.
- **§11.1 coverage table.** In all 37 rows, the listed primary stories declare primary ownership of that FR. No other story makes an unqualified primary claim on any FR; "stays with" demotions were excluded. The epic column matched the epics FR Coverage Map in 37 of 37 rows.
- **§11.2 coverage table.** Compared all 19 rows with every story's primary and supporting NFR claims: 78 primary story–NFR pairs and no divergences in either column.
- **Ownership rule.** Applied the Primary-ownership rule to every primary claim. Seven NFRs have two or more unqualified claimants: 20 claims in 16 stories (finding 1). For FRs, the result was zero. For UX-DRs, 41 of 42 have exactly one primary owner, none is duplicated, and UX-DR42 has none (finding 4).
- **Coverage reader.** Imported `tools/phase_4_story_completion.py` read-only with `python3 -B -I` and called `coverage(prd, epics, id)` for all 146 headings. It accepted 143, including 139 of 142 numeric stories and all four 6.5 lettered stories. The exceptions match round 3: Stories 3.22 and 3.23 return `COVERAGE_MISMATCH` (PRD has none; epics have NFR10, NFR11 and NFR16), and 4.8 returns `COVERAGE_AMBIGUOUS` because it has no declaration. The CLI write paths were not run. No `__pycache__` was created, and the tracked tree was unchanged.
- **Lifecycle labels.** Compared the latest "Current reconciliation" state in every detailed story with the tracker. Twelve mismatches were found (finding 5). Also checked the Epic 3 overview and Story 3.21 against the tracker and the Story 3.21 spec frontmatter (L1).
- **History.** Ran `git log -S` and confirmed that the H5 and M2 texts came from `8627d32cb`.
