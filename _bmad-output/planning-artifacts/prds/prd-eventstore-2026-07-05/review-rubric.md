# PRD Quality Review — eventstore Phase 4 Implementation Readiness Recovery

Review date: 2026-07-19. Scope: post-reconciliation re-validation after the 2026-07-19 update that absorbed six approved sprint-change proposals. The PRD passed a full rubric walk at its 2026-07-16 finalize; owner-deferred refinements from that walk (correctness-gate binding in §11.3, product-bet outcome metrics in §10, glossary cross-references, the structural front-load proposal, G5/goldens prose expansions, the OCI image-index promotion, optional 3.12 traceability additions, and the stale §11.3 coordinated-slice list) are acknowledged as dispositioned and are not re-raised below.

## Overall verdict

The 2026-07-19 delta is verified correct: the three new `source_artifacts` entries (`sprint-change-proposal-2026-07-17.md`, `sprint-change-proposal-2026-07-18-story-3-1-live-sidecar-topology.md`, `sprint-change-proposal-2026-07-19.md`) match the reconciled proposals and resolve on disk, and the §11.2 NFR10/NFR16 corrections now point at Story 7.10 "Integration CI Recovery", which is exactly the story in `epics.md` that declares NFR10 and NFR16 coverage. The PRD as a whole still holds together — vision, requirement inventory, MVP boundary, metrics, and FR traceability remain coherent and source-grounded. The one material residue is that §11.2 is now internally inconsistent about story numbering: the two corrected rows use post-2026-07-15 numbers while five sibling citations in the same table still carry pre-renumbering numbers, including an NFR15 row that now points at a story that does not cover NFR15.

## Decision-readiness — strong

The load-bearing decisions read as decisions. §0 states flatly that FR37/NFR19 are "a committed post-MVP capability" and that "this capability does not enlarge the Phase 4 MVP"; §1 assigns artifact ownership in four unambiguous bullets; §9.3 gives the post-MVP commitment teeth ("Story 8.2 blocks Parties Story 8.7 migration until the engine is implemented, reviewed, released or pinned, and proven"). Where design is unresolved, the PRD gates rather than hand-waves: FR24 requires the frozen global-ordering spec to be "updated before implementation," and FR31 mandates a verify-first spike "before choosing an optimistic-concurrency fencing design." §12 closes Open Questions honestly by pointing at the Story 8.1 gate rather than pretending nothing is undecided. The 2026-07-19 NFR17 rewrite strengthened this further — it commits to a named component posture (`auth.secretStore: openbao`, default-deny per-application access) instead of the earlier generic "must support secret stores."

The finalize-walk refinement about binding FR24/FR31 gate owners into §11.3 remains owner-deferred; it does not undermine the judgment above.

### Findings

No new findings.

## Substance over theater — strong

The content is earned throughout. NFRs carry product-specific bounds, not boilerplate: NFR1 pins the exact anonymous exception ("`/health`, `/alive`, `/ready` … explicitly pinned `AllowAnonymous`"), NFR6 names the dedup key (`MessageId`) and the production proof path, and the updated NFR17 specifies `secretKeyRef` wiring and the Kubernetes-Secrets bootstrap carve-out. §3 uses role lists and jobs-to-be-done rather than decorative personas — appropriate for a platform PRD, and every listed role maps to a §6 feature family. The Vision's central bet (§2: reuse and hardening "delivered together" or the phase fails) is falsifiable and specific to this product; it could not be swapped into another PRD unchanged.

### Findings

No findings.

## Strategic coherence — adequate

The thesis is stated and the feature arc follows it: §6.1 (self-service seams) is the reuse half, §6.4–§6.7 are the hardening half, and §6.8–§6.9 close the consumer-parity and payload-protection gates that make the reuse claim honest. Counter-metrics SM-C1–SM-C3 are real counterweights (e.g., SM-C2: "Do not count API smoke responses as integration evidence"). The residual weakness — SM1–SM5 largely validate planning-artifact completion rather than the product bet's outcome — was raised at finalize and owner-deferred; nothing in the 2026-07-19 delta changes that picture, so the dimension stays adequate rather than strong, with no new finding warranted.

### Findings

No new findings.

## Done-ness clarity — adequate

Each §6 feature family ends in a "Done evidence" block with observable consequences (e.g., §6.5: "Anonymous and cross-tenant admin access fails closed"; §6.8: Story 1.20 "names the exact EventStore runtime SHA"). §11.3 carries genuinely concrete acceptance detail — request-size limits of "`1_048_576` bytes" and "`10 * 1024 * 1024` bytes" for Story 5.2, and named spec output paths gating Stories 6.2/6.4/6.6. The omnibus FRs (FR26, FR33, FR34) bundle many clauses, but each clause is individually verifiable, and §1 deliberately delegates per-story acceptance criteria to `epics.md`, which the epic file honors with Given/When/Then criteria per story. Within the PRD's declared ownership split this is sufficient; it is "adequate" rather than "strong" only because an engineer still needs `epics.md` open to know done-ness for any single story — which is the design, not a defect.

### Findings

No new findings.

## Scope honesty — strong

§9.2 does real work: each exclusion names its disposition ("Admin interactive OIDC login implementation; backlog artifact only") and the GDPR bullet carefully separates what stays out (tombstoning, broker-history deletion) from what is in scope under FR5/Story 1.14 — a distinction that would otherwise be silently assumed wrong. §9.3 prevents the classic post-MVP dodge by binding Epic 8 to a concrete blocking relationship instead of "later." §13 asserts "No inline `[ASSUMPTION]` tags are present," and that claim is verifiably true (the only occurrence of the tag in the document is that sentence). Open-items density is appropriately near zero for a `status: final` baseline whose remaining decisions are explicitly gated.

### Findings

No findings.

## Downstream usability — adequate

The Glossary anchors the load-bearing nouns (External API Host vs Interactive UI Host, Support-Safe State, Projection-Confirmed Success) and they are used consistently across FRs, constraints, and done-evidence blocks. FR1–FR37 and NFR1–NFR19 are contiguous and unique; §11.1 maps all 37 FRs with no gaps; all 44 frontmatter `source_artifacts` paths resolve. The corrected NFR10/NFR16 rows now land on the right story.

The residue is in §11.2's other rows. The 2026-07-15 renumbering split old Story 7.2 "Admin Claims, Audit, And Honest Deferred Operations" into 7.2/7.3/7.4, old 7.3 "Production Deployment Hardening" into 7.6–7.9, and old 2.4 "Tenants External API Host Adoption" into 2.4/2.5/2.6 — but only the 7.4→7.10 citations were corrected on 2026-07-19. Five citations still use pre-split numbers, and one of them is now factually wrong: NFR15's sole cited story, 7.2, is today "Admin Claims Normalization," which declares NFR1/NFR2 and contains nothing about deferred-operation honesty; the story that covers NFR15 is 7.4 "Honest Deferred Admin Operations" (with 7.3 and 7.14 also declaring it). Since SM3 makes this table the gate evidence for high-risk NFR coverage "before Phase 4 implementation resumes," a readiness re-run consuming §11.2 as written would follow NFR15 to the wrong story.

### Findings

- **medium** §11.2 residual pre-renumbering story citations, inconsistent with the corrected rows (§11.2 NFR2, NFR4, NFR14, NFR15, NFR17) — the 2026-07-19 correction moved NFR10/NFR16 from 7.4 to 7.10 but left the same renumbering debt in sibling rows: NFR15 cites only "7.2" (now claims normalization; honest-deferred-admin is 7.4), NFR4 and NFR17 cite "7.3" (now admin audit; the deployment-hardening content those rows meant lives in 7.6–7.9, and 7.6 was already added alongside), and NFR2/NFR14 cite "2.4" (now contract metadata/routes; the Tenants external-host and UI client-library stories are 2.5/2.6). *Fix:* fold these five citations into the already-owed 2026-07-15 renumbering reconciliation (the same pass that owes the §11.3 coordinated-slice list): NFR15 → 7.4 (optionally +7.3, 7.14), NFR4 → replace 7.3 with 7.6, NFR17 → replace 7.3 with 7.7–7.9 as judged, NFR2 → 2.5 (or 2.4+2.5), NFR14 → 2.5/2.6.
- **low** NFR8 row cites stories that do not carry its content (§11.2 NFR8: "1.11, 1.14, 6.3, 6.4") — Story 1.11 is "Domain-Module Adoption Guardrails" (declares NFR14) and 1.14 is checkpoint erasure (declares NFR2/NFR16); the epic-side NFR8 declarations sit on 1.2, 1.9, 1.13, 1.16, 1.19, 2.11, and 4.7. The 6.3/6.4 citations carry the core bounded-cost intent, and NFR8 is outside SM3's high-risk set, so impact is low. *Fix:* correct alongside the medium finding in the same reconciliation pass.

## Shape fit — strong

This is a brownfield developer-platform and operations-hardening PRD (§3.3 says so explicitly), and the document is shaped accordingly: capability-spec FR tables grouped by audience-facing feature family, jobs-to-be-done instead of user-journey narratives, operational rather than user-facing success metrics, and UI concerns reduced to governance boundaries with design delegated to `ux.md`. Forcing UJs with named protagonists onto this product would have been overhead; the PRD correctly declined. As a chain-top document feeding epics, stories, and readiness re-runs, its traceability investment (§11) is proportionate — which is also why the §11.2 residue above is scored medium rather than shrugged off.

### Findings

No findings.

## Mechanical notes

- Frontmatter roundtrip: all 44 `source_artifacts` paths exist on disk. The three 2026-07-19 additions are correct and non-duplicative — `2026-07-18.md`, `2026-07-18-story-3-5-reconciliation.md`, and `2026-07-19-openbao-secret-store.md` were already listed from prior reconciliations, so the six reconciled proposals are all represented exactly once. `updated: 2026-07-19` matches.
- Delta cross-check against `epics.md`: Story 7.10 "Integration CI Recovery" declares "FR34, NFR10, NFR16" — both corrected §11.2 rows resolve to a story that actually owns the content. Story 3.1 (also cited by NFR10) does not declare NFR10 in its header but its content ("Re-Tier Live-Sidecar Tests From Release Gate") is the NFR10 behavior itself; acceptable as primary coverage.
- ID continuity: FR1–FR37 and NFR1–NFR19 each appear, contiguous, no duplicates. FR IDs are non-sequential across §6 subsections (FR25 sits in §6.3, FR26 in §6.5) because tables group by epic ownership rather than ID order — cosmetic only, and §11.1 disambiguates.
- Assumptions Index roundtrip: trivially clean; §13's "no inline tags" assertion is the document's only `[ASSUMPTION]` occurrence. No `[NOTE FOR PM]` or `[NON-GOAL]` tags remain — consistent with `status: final`.
- Glossary drift: none observed; "Support-Safe", "Projection-Confirmed Success", "External API Host" / "Interactive UI Host" are used with consistent casing and meaning across §4, §6, §8.3, and NFRs.
- Known-deferred, verified still-present but intentionally not re-raised: the stale §11.3 coordinated-slice list (still cites pre-split "7.2, 7.3, and 7.4"); the OCI image-index requirement absent from PRD text (approved proposal disposed `prd.md` as no-change); optional 3.12 traceability additions (epic-side NFR9/NFR11 declarations include 3.12; PRD rows do not).
