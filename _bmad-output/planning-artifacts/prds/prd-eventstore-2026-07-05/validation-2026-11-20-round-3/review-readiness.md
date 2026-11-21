# Phase 4 readiness and evidence review

The working PRD at SHA-256 `4644f2b282a3ee7ff860869fd49a620cd32316284cdb594b32d45a6fea663a4b` corrects seven of the eight prior findings assigned to this review. The business sequence for readiness is clearer. The sequence for obtaining its first authenticated CI result still needs a decision. The October 6 readiness rejection stands.

## Findings

### High · The first readiness seal has no defined input set

Section 11.4 says to obtain the readiness transition’s authenticated seal after the 15 business and coverage results, and then use that seal to pass G-HIGH-RISK. The G-READINESS row says its assessment consumes all 16 predecessor results, including the passing G-HIGH-RISK result. The existing evaluate/consume contract checks business prerequisites before issuing a candidate PASS. The seal consumer accepts a successful completed evaluation and repeats it. A readiness attempt using the 16 listed predecessors therefore needs a G-HIGH-RISK result whose own acceptance already needs the readiness seal. The matrix’s offline check of a seal’s shape supplies no authenticated result.

Define what a provisional readiness evaluation checks on the exact subject before it can be sealed. Identify the authenticated artifact that G-HIGH-RISK may consume and the checks applied by the final readiness writer after all 16 predecessors pass. Add a first-success case that begins with neither a passing matrix nor an earlier readiness seal, and a refusal case for an absent prerequisite. The absence of a current readiness adapter remains a separate delivery gap.

Evidence: PRD lines 224, 723, 743, 747 and 809. `tools/phase_4_transition.py` lines 755–783 and 861–874. `_bmad-output/implementation-artifacts/phase-4-gate-risk-matrix.json` lines 286–304 and 337–350. `tools/validate-phase-4-gate-risk-matrix.py` lines 2–6 and 545–554.

### Medium · Corrective-story handoffs retain a retired authorization prerequisite

PRD §0, §8.1 and OR28 assign corrective authorization to the owning story or specification under AD-12. The architecture’s active “Implementation Status And Production Gates” section requires a Story 9.1 record for *every* corrective story. Epic Stories 5.13, 7.21 and 9.4 still require that record. A team beginning the G-CLAUSE validator on approved scope would be told to obtain another record under a superseded mechanism.

Replace the active dependency in each affected story with the gate owner’s scoped authorization in the owning specification. Update the architecture gate instruction or mark it as the historical rule. Story 9.3 already contains a local supersession note that explains its historical acceptance language.

Evidence: PRD lines 122, 488 and 795. `_bmad-output/planning-artifacts/architecture.md` lines 229 and 561. `_bmad-output/planning-artifacts/epics.md` lines 5041, 7026, 8109 and 8155. The November 9 readiness-recovery proposal, lines 199–205, explicitly directs owners to use the story/spec and CI record.

### Medium · Architecture prescribes a branch-wide seal check

The architecture status table says Story 9.2 builds “one blocking required check on `main`”. The PRD glossary and October 7 owner decision impose the seal on the guarded transition, and allow ordinary work on `main` to continue while gates are red. A release engineer working from the architecture table could install a branch protection rule that prevents the corrective work from landing.

Replace the active architecture row with the transition-specific rule and reference the owner decision. Historical proposals can retain the old option as evaluation history.

Evidence: PRD line 224. `_bmad-output/planning-artifacts/architecture.md` line 567. `_bmad-output/planning-artifacts/sprint-change-proposal-2026-10-07-architecture-routing.md` lines 472–473. `.github/workflows/phase-4-readiness.yml` lines 54–56.

### Low · The architecture handoff still lists OQ8 CI as pending for all source

PRD §1.1 and Story 4.17 now identify hosted evidence for committed revisions. Architecture AD-25 and the current implementation-status table still instruct readers to wait for a separately authorized push or dedicated CI confirmation. For revision `d677fba671`, GitHub reports successful fixture and live design-availability jobs with the assured-decision job skipped. The working draft still needs its own hosted evidence when committed.

Point the architecture’s current status to PRD §1.1, or mark its November 10 status as historical. Preserve the independent authority, runtime and assurance requirements.

Evidence: PRD lines 145, 700, 732 and 767. `_bmad-output/planning-artifacts/architecture.md` lines 338 and 584. [Committed PRD run](https://github.com/Hexalith/Hexalith.EventStore/actions/runs/44775644088) · [Earlier baseline run](https://github.com/Hexalith/Hexalith.EventStore/actions/runs/44726206620)

## Prior findings

Statuses apply to the original PRD/epic defect. Open evidence or owner approvals affect readiness independently.

| Prior finding | Status | Current outcome and evidence |
| --- | --- | --- |
| 1 · Readiness requires its own result | Partially resolved | Frontmatter, opening decision, §11.4 and OR1 now order publication, promotion and consumer removal by subject, and put G-READINESS before READY. The first authenticated readiness evaluation still has ambiguous predecessors. See the high finding above. |
| 6 · G-COMPAT overlooks the existing release check | Resolved | PRD lines 738 and 789 distinguish assembly and isolated package resolution from outstanding wire, generated-surface, application, SemVer and release-subject approval work. `.releaserc.json:20` invokes the verifier. `tools/verify-release-api-compatibility.py:35,437,478` pins 4.2.0, enforces 14 package families and verifies official archive signatures. |
| 7 · Production Tenants API already consumes shared JWT validation | Resolved | G-AUTH-HOSTS and OR23 at lines 739 and 790 distinguish Production from the host-local non-Production configuration and require version, fingerprint, host inventory, negative tests and release assurance. Tenants `3bb4b818d0b3e138eb19af7f5e02b1c9a44d0272`, `src/Hexalith.Tenants.Api/Program.cs:49–55` and `Services/ProductionApiAuthentication.cs:41,52`, confirm shared Validate and Configure on the Production branch. |
| 8 · OR30 points licensing review at the historical package | Resolved | Line 797 distinguishes the October 14.2.0 review from 15.0.0 and assigns release disposition for the candidate actually shipped. Builds `e303acd8`, `Props/Directory.Packages.props:367`, and both restored Server and Gateway assets resolve 15.0.0. Its bundled LICENSE.md states the RPL-1.5 and vendor-agreement alternatives. Product and release owners retain the choice. |
| 11 · Consumer and MVP coverage evidence handoffs specify ignored storage | Resolved | PRD §8.1, G-CONSUMER, G-MVP-COVERAGE, OR24 and OR27 specify the CI-generated machine-readable inventories and a durable spec/run binding for digests, input identities and results. Story 3.20 at epic lines 3316–3323 and Story 9.5 at 8181 onward agree. AD-12 at architecture lines 229 and 436 confirms the evidence owner. The obsolete authorization dependency reported above concerns different stories. |
| 12 · Successful OQ8 CI omitted on committed source | Resolved | PRD §1.1, G-OQ8, OR11 and the Story 4.17 epic handoff identify completed hosted availability without granting assured status. Both cited runs on `607072c68` and `d677fba671` returned successful fixture and live jobs, with the assured-decision job skipped. Neither run covers this working PRD digest. The architecture handoff discrepancy appears separately above. |
| 13 · OR7, OR24 and OR19 assign completed bookkeeping | Resolved | PRD lines 785, 791 and 817 retain the owners for FR36-C3, FR36-C4/C5 and NFR17-C5 and request outstanding evidence. The tracker at lines 81 and 93 records Epic 2 in progress and the full 2.12 key as done. Epic Stories 3.19, 3.20 and 7.21 supply the named clause owners. |
| 17 · OR13 understates the completion control | Resolved | OR13 at line 768 names the shared completion writer and bounded Story 5.3 adapter and assigns the remaining adapters. `tools/phase_4_story_completion.py:148` derives the claim. `tools/phase_4_story_5_3.py:80` binds NFR3/NFR4. `tools/phase_4_transition.py:764–768` invokes the adapter and explicitly refuses the unfinished readiness business transition. |

The related live publication and assurance gates remain open. A license choice, new implementation work or a change to the PRD would require a separate action.
