# Validation Report — eventstore Phase 4 Implementation Readiness Recovery

- **PRD:** `_bmad-output/planning-artifacts/prd.md` (SHA-256 `d5632ba7…`)
- **Rubric:** `.claude/skills/bmad-prd/assets/prd-validation-checklist.md`
- **Repository baseline:** `40c92e085d8a6463d469c1b340c410fec84a690f`
- **Run at:** 2026-10-07T16:41:08+02:00
- **Grade:** Poor

## Overall verdict

The PRD now fixes almost every content gap the 2026-09-10 review raised. It has a computed exit contract (§11.4), a 49-clause acceptance ledger (§7.1), five journeys with named protagonists, NFR2, NFR3, NFR12 and FR36 contracts defined by capability rather than by a closed list, and a named primary story for every gate. Its `final`-but-`Reject` posture is coherent and well defended. The weak point is the PRD's second job as a live readiness ledger. The §11.3 identity register is stale for every artifact it binds, and 18 of 56 FR/NFR texts differ from the `epics.md` inventory without the PRD listing them. The 2026-10-07 edits also brought in new inconsistencies: NFR1 adds an anonymous-endpoint class that is never listed and cites AD-16, which currently forbids it; the exit contract and SM10 cannot represent the UI-host carve-out; SM11 counts an OQ8 loss class that the PRD itself calls non-authoritative; and OR30, the MediatR licence blocker, sits outside the gate contract. The PRD is a trustworthy requirements source for the stop decision and for writing corrective stories. It is not yet a clean chain-top handoff.

The three extra reviewers materially sharpen that picture. Adversarial general (REJECT as gate authority) finds the machinery for leaving FAIL circular: §0 forbids every handoff that lacks a validator, only a forbidden handoff can build that validator, and the sole escape is a Story 9.1 "bootstrap rule" that lives in `epics.md`; the 2026-10-07 amendment that a single-maintainer seal "never blocks `main`" means it cannot block any transition it names, its "required run" is defined only by the validator it seals, and G-HIGH-RISK seals itself. Platform contract (NOT ACCEPTED as complete) finds two code-level criticals that no mandatory gate would catch: the public gateway maps Dapr actor callbacks with no app-layer authentication, and the gateway status endpoint still resolves `CorrelationId` values to status records against §8.2 and FR12-C4; the parent verified both against source. Brownfield truth (fail as the current chain-top baseline; pass as a fail-closed readiness posture; no critical) confirms the fail-closed safety claim and every recorded code contradiction, but shows that the §11.3 register reports September digests and statuses as current, the ownership tables cite `epics.md` story sections that declare something else, the Epic 8 and Epic 6 lifecycle statements are stale, and the current bytes (`d5632ba7…`) were never reviewed: 15 commits changed the PRD after the last sealed version, several under unrelated commit subjects. Rubric dimensions alone would grade Fair (no broken dimension and no critical finding, but seven high findings and two thin dimensions); the consolidated grade is Poor because the Adversarial general and Platform contract reviewers each report two critical findings, and any critical finding grades Poor.

Findings raised independently by two or more reviewers: the NFR1 UI-host anonymous class that contradicts AD-16 (all four reviewers); the stale §11.3 digests and `ux.md` status (all four); SM11 counting NFR7 class (e) as delivered on source-only evidence (rubric, brownfield, adversarial); OR30 sitting outside every gate with "release" undefined (rubric, adversarial, platform); "INDEPENDENT" status labels that contradict the single-maintainer Assurance Control (rubric, brownfield, adversarial); ownership-table contradictions on NFR5 and NFR17 (rubric, brownfield, adversarial); the imported AD-26 `[ASSUMPTION]` read as binding (rubric, brownfield, adversarial); the 18-of-56 PRD/`epics.md` FR/NFR text drift (rubric, brownfield); the UI-host carve-out that the exit contract and SM10 cannot represent (rubric, adversarial); Epic 9 missing from §9.1 (rubric, brownfield); the stale Epic 8 lifecycle (brownfield, adversarial); unreviewed post-seal bytes and unlisted source proposals (brownfield, adversarial); gate commands that are undefined or set by the gated story (rubric, adversarial); NFR2's Unicode case-folding alias (adversarial, platform); and smaller overlaps on stale OR rows, the Story 2.9 reopen wording in G-STATUS-ID, the SM8 denominator and the mixed-version rollout contract.

## Dimension verdicts

- Decision-readiness — adequate (unchanged from adequate)
- Substance over theater — strong (unchanged from strong)
- Strategic coherence — adequate (unchanged from adequate)
- Done-ness clarity — adequate (up from thin)
- Scope honesty — adequate (down from strong)
- Downstream usability — thin (up from broken)
- Shape fit — thin (unchanged from thin)

## Findings by severity

### Critical (4)

**[Adversarial general C1]** — The path out of FAIL is circular, and its only escape is an owner note outside the PRD (§0 lines 110, 112; §11.4 preamble line 672; OR28 line 742; cf. `epics.md` Story 9.1 "Bootstrap rule" line 7531)

Every corrective story (2.14, 2.15, 3.18–3.20, 4.16, 4.17, 5.11, 5.14, 9.2–9.5) needs a §0 record checked by `tools/validate-corrective-work-authorization.py`, which Story 9.1 builds. Story 9.1 cannot be handed off under §0, because that validator does not exist yet, nor under general handoff, which is barred until every gate passes. The deadlock is broken only by an `epics.md` "dated owner authorization … recorded in the story before development starts": an owner assertion with no Assurance Control, no 24-hour separation and no seal, which is exactly what §11.4 line 672 forbids and which the PRD never sanctions. "General dependent implementation handoff" is undefined. Stories 6.6 and 8.3 are `in-progress` and 5.5 is in `review` while `evidence/corrective-work-authorizations/` does not exist, so either those handoffs fall outside an unstated boundary or the prohibition is not being followed.

Fix: Add a PRD-level bootstrap clause that names Story 9.1 as the single bootstrap exception, with its allowed paths and required evidence, and have the first sealed transition re-validate it after the fact. Define "dependent implementation handoff" as an enumerable class (for example, work whose acceptance consumes a failed gate's subject) and state that non-dependent work may proceed.

**[Adversarial general C2]** — The amended seal cannot block its own guarded transitions; "required run" is circular; G-HIGH-RISK seals itself (Glossary "Assurance Control" (1) line 201; OR10 line 713; OR13 line 715; G-HIGH-RISK line 690)

The 2026-10-07 amendment says a seal "blocks only its transition … and never `main`". But every guarded transition is a commit to `main`: a gate result, a tracker `done`, a readiness report and an authority record are all files there. The seal can therefore stop none of them, and OR13's requirement to enforce it "before a story may be recorded `done`" becomes impossible. The tracker fills with unsealed `done` values that look the same as sealed ones. The PRD never defines "required run". `epics.md` Story 9.2 defines it as enforcement by the guarded transition's own validator, which is the code being sealed, so the validator, the workflow and the subject can change in one push and seal each other. G-HIGH-RISK sits in its own matrix with no bootstrap. Legacy high-risk `done` labels (5.1, 5.3, 4.9–4.15 against NFR3, NFR7 and NFR16) are neither grandfathered nor required to re-transition.

Fix: Make a guarded-transition record void unless it carries a seal reference, and have G-BASELINE and G-MVP-COVERAGE treat an unsealed high-risk `done` as an error. Pin the seal workflow and validator to a commit that predates the subject. Add an explicit G-HIGH-RISK bootstrap, and state how pre-existing high-risk `done` labels are treated.

**[Platform contract C1]** — The public gateway exposes Dapr actor callbacks with no app-layer authentication, and no PRD gate binds that surface (NFR1 line 361; §8.2 bullet 6 line 457; FR28 line 302; SM10 line 543; §11.2 NFR1 row line 609; G-AUTH-HOSTS line 686)

`src/Hexalith.EventStore/Program.cs:47-48` maps `MapSubscribeHandler()` and `MapActorsHandlers()` on the public REST pipeline, and the only authorization setup is a bare `AddAuthorization()` with no `/actors` guard. The gateway hosts the aggregate actors in-process, and `IAggregateActor.GetEventsAsync` takes no tenant or caller context. Any client that can reach the gateway port can therefore call `PUT /actors/AggregateActor/{id}/method/...` and bypass gateway authentication, NFR2 tenant validation and AD-5 single activation; the reviewer found this by inspection, not by exercising it. Anonymous-endpoint tests enumerate only `IAllowAnonymous` metadata, so endpoints that are open only because no fallback policy exists are invisible to them. G-AUTH-HOSTS is JWT-only, and SM10's `nfr1_surfaces` has no completeness rule. Verified by parent against source: `Program.cs:48` calls `app.MapActorsHandlers()` with no `.RequireEventStoreSidecarChannel()`, and the gateway project defines no authorization FallbackPolicy. The domain-service host, by contrast, chains `.RequireEventStoreSidecarChannel()` at `src/Hexalith.EventStore.DomainService/EventStoreReminderEndpointExtensions.cs:32`. Exploitability depends on whether the gateway app port is reachable on the network, but nothing in the code prevents it.

Fix: Add an NFR1 clause, bound in §11.4, that requires every endpoint on every host to carry an authorization requirement or appear in the closed anonymous list. Require AD-28 sidecar-channel authentication on Dapr framework routes, and make readiness fail when the token is unconfigured outside Development. Add an all-endpoints test across the gateway, Operations, Admin Server Host, Admin UI and generated hosts, and make framework routes mandatory members of `nfr1_surfaces`.

**[Platform contract C2]** — `CorrelationId` still selects public command-status resources at the gateway, and G-STATUS-ID cannot detect it (§8.2 bullet 4 line 455; FR12-C4 line 404; G-STATUS-ID line 681; OR21 line 735)

§8.2 and FR12-C4 say a `CorrelationId` never selects a status resource. AD-17 and AD-32 agree. Yet `CommandStatusController.cs:112-146` resolves the path segment through `ICommandCorrelationIndex.ResolveAsync`, which it calls "the sole compatibility lookup for a correlation identifier", and returns that command's status on a unique hit. The public OpenAPI text advertises "message or correlation identifier". The index is registered by default, a test pins the behaviour (`GetStatus_UniqueCorrelationIndexResolution_ReadsMessagePrimaryRecord`), and done Story 4.2 mandates the lookup (`epics.md:3365-3366`). Story 2.15 removes only the generator's `MessageId ?? CorrelationId` fallback, so G-STATUS-ID could pass while this contradiction is still live. Tenant scoping is preserved, so this is not a cross-tenant leak. Verified by parent against source: `src/Hexalith.EventStore/Controllers/CommandStatusController.cs:127-142` still resolves the path identifier through the correlation index as a compatibility lookup.

Fix: Choose one rule and apply it everywhere. Either (a) retire the lookup under NFR12, with a deprecation path and a SemVer-major release, and extend FR12-C4, G-STATUS-ID and Story 2.15 to the gateway status resource and the Story 4.2 text; or (b) narrow §8.2 and FR12-C4 to admit one bounded, tenant-scoped, deprecated compatibility lookup with a stated exit, and reconcile AD-17, AD-32 and Story 4.2 to that wording.

### High (21)

**[Rubric: Done-ness clarity · Brownfield truth H5 · Adversarial general H1 · Platform contract H5]** — NFR1's new UI-host anonymous class is never enumerated, cites AD-16, which forbids it, and has no owner (NFR1 line 361; §11.2 NFR1 row line 609; SM10 line 543; G-AUTH-HOSTS line 686; §9.2 line 507; `architecture.md` AD-16 line 244)

AD-16 is `[ADOPTED]` and has not been amended. It still allows only `/health`, `/alive` and `/ready`, and requires a governed PRD change before any anonymous asset or callback. Story 5.14's AC (`epics.md:4815`, 4828–4831) still requires only those three, and no OR tracks the AD-16 amendment: OR14 names AD-10, AD-11 and AD-26 only. Because the PRD contains no list, the tests become the requirement. The class is also ill-defined. "Carry no … user data" is a judgment, and read literally it excludes every OIDC callback, because an `id_token` is user data. "Static framework assets" is undefined: `MapStaticAssets` also publishes `wwwroot` and `_content/*`. Assets served by middleware carry no endpoint metadata, so "endpoint-metadata tests" cannot see them. No story owns the enumeration, G-NFR-OWNERSHIP omits NFR1, and G-AUTH-HOSTS evidence is JWT-only. Other shipped anonymous surfaces fit neither exception. The Admin UI has no fallback policy and maps `/dapr/subscribe` anonymously, and the gateway serves anonymous `/problems/*`, `/openapi/v1.json` and `/swagger` by default. `architecture.md:541` also still describes the pre-amendment Assurance Control. The §9.2 exclusion of UI hosts from production limits the exposure.

Fix: Enumerate the class in the PRD by rule, or name the single approved artifact that holds it. For example: GET/HEAD only; static-asset manifest entries under `_framework/` and `_content/`; and OIDC `signin-oidc`/`signout-callback-oidc` only once IAM-1 lands. Define a mechanical "data-free" test, and make the inventory cover middleware-served paths, for example with an HTTP-level crawl of anonymous 2xx responses. Until `bmad-architecture` amends AD-16, drop the "(AD-16)" citation or mark it "amendment owed", and track the amendment in OR14 or a new OR. Assign an owning story and add fallback and anonymous-enumeration evidence to a gate. Decide whether documentation endpoints are anonymous exceptions. Route the Story 5.14 AC and `architecture.md:541` to their owners.

**[Rubric: Scope honesty · Adversarial general H2]** — The exit contract cannot represent the 2026-10-07 UI-host carve-out, so MVP coverage either deadlocks or silently waives admin fail-closed (§9.1 line 490; §9.2 line 507; SM10 line 543; G-MVP-COVERAGE line 693; §13 line 770)

§9.2 moves interactive UI hosts' NFR1 conformance out of the MVP production profile. But §9.1 makes the MVP "total over FR1-FR36, NFR1-NFR18", G-MVP-COVERAGE forbids `N/A`, and SM10 targets 100% of `nfr1_surfaces`, while UI-host surfaces are "never counted as covered". So either SM10 and NFR1 can never pass while login stays post-MVP, or the denominator silently excludes those surfaces. That is an `N/A` in all but name, and the Story 9.5 validator would have to invent the rule. "Moves with them" has no defined meaning for an NFR that is not scoped by profile. The Admin UI and Sample UI map pages with no policy today, so the text reads as a fail-closed waiver for every non-production deployment, including shared and staging environments. No re-entry criterion is defined. §13 calls this "one scope reduction", which understates a cut that removes every interactive UI host, including the Admin shell (FR34-C11) and the Tenants UI that UJ3 depends on.

Fix: Give the carve-out stable clauses, for example NFR1-C1 for MVP surfaces and NFR1-C2 for the post-MVP interactive-UI-host class. Allow one reason-coded `outside-mvp-production-profile` state in G-MVP-COVERAGE for that clause only, define SM10's denominator explicitly, and say so in §9.1. State the minimum posture for UI hosts outside production (network-isolated or authenticated by a reverse proxy). Bind re-entry to an AD-26 profile change plus a named login-evidence gate.

**[Rubric: Strategic coherence · Brownfield truth H1 · Adversarial general H3]** — SM11 counts NFR7 class (e) as delivered on source-only evidence that NFR7 and §1.1 reject (SM11 line 544; NFR7 line 367; §1.1 line 133; §11.2 NFR7 row line 615; §11.3 OQ8 row line 653; G-OQ8 line 679)

SM11 says "four of five are delivered" and counts class (e) on "closed source-only platform evidence from Stories 4.9-4.15". NFR7 counts a class as delivered only when production-path evidence proves prevention. §1.1 makes no OQ8 closure claim authoritative until G-OQ8 passes, and G-OQ8 fails. §11.3 calls Story 4.15's completion "a bounded source-only packet claim", and its v4 record says it "grants no release approval, package authority…". The change from three to four landed silently in `98395e5f` ("fix(oq8): close story 4.15 lifecycle", 2026-09-21), with no proposal and no memlog entry. The §11.2 NFR7 note repeats the over-claim by naming only class (c). The thesis metric overstates hardening by one class, and it steers Story 4.16 and the readiness reviewer toward a 4/5 baseline that does not exist. That is the drift SM-C5 exists to prevent.

Fix: Restate SM11 as three of five, with class (e) shown as "implemented, source-only; not countable until G-OQ8 passes and production-path evidence is bound". Name class (e) next to class (c) in the §11.2 NFR7 note. The alternative is a logged, proposal-backed owner decision that amends NFR7 and §1.1 to accept this evidence.

**[Rubric: Downstream usability · Brownfield truth H2 · Adversarial general M2 · Platform contract L3]** — The §11.3 identity register is stale for every artifact it binds, and it misstates the `ux.md` status (§11.3 rows lines 647–650; §3.3 line 167)

Stated digests against the actual ones at HEAD:

- `architecture.md`: `7e3dbc7b…` stated, `7fd805a8…` actual.
- `epics.md`: `d067c8fb…` stated, `0697679b…` actual.
- `ux.md`: `2927f97d…` stated, `23b17217…` actual (`a135c969…` in the worktree).
- DESIGN: `3f4f0181…` stated, `7b743e79…` actual.
- EXPERIENCE: `11f75403…` stated, `3985789a…` actual.

The architecture and ux values are the digests recorded in `epics.md`, relabelled as each artifact's own, and they date from `0994c378` (2026-09-09). The claim "`epics.md` matches this top-level digest" is false. §11.3 and §3.3 call `ux.md` `final`, but HEAD reads `Status: draft`. The uncommitted worktree flips it, along with DESIGN and EXPERIENCE, to `final`, so the rows will drift again. The column header says "Identity/status at stated date", but no row states a date. §1 makes this register the PRD's content-identity mechanism, so these values are false anchors for G-BASELINE and OR14. G-BASELINE's FAIL remains correct, which is why Platform contract rates the architecture row low.

Fix: Generate the register as the Story 9.3 baseline manifest and replace the in-PRD digests with a pointer. Otherwise, date every row with its observation commit and separate "digest recorded in `epics.md`" from "artifact digest at `<commit>`". Delete the "epics matches" sentence and fix §3.3 line 167.

**[Rubric: Downstream usability · Brownfield truth M7]** — The `epics.md` Requirements Inventory differs from the PRD for 18 of 56 FR/NFR texts, and the PRD reports only digest drift (§11.3 rows lines 647, 650; G-BASELINE line 678; OR8 line 723; OR14)

FR5, FR12, FR15, FR16, FR26, FR33, FR34, FR36, FR37, NFR2, NFR3, NFR5, NFR7, NFR8, NFR12, NFR17, NFR18 and NFR19 differ; the other 38 are identical. Several differences weaken safety contracts:

- NFR2 in `epics.md` is still the pre-correction one-liner ("Tenant provisioning must reject the reserved `system` tenant name"; word similarity 0.36).
- NFR5 lacks the 16 / 2,048 bound.
- NFR7 has word similarity 0.30.
- NFR12 protects only "additive framework changes such as SignalR …" (0.10).
- FR36 lacks the five-outcome structure (0.20).

The 2026-10-07 proposal mentioned only NFR3 and NFR5. Stories written from the inventory would build to superseded security and compatibility contracts. NFR1, which matches since `40c92e08`, shows that propagation works when someone does it.

Fix: List the divergent IDs in §11.3 and OR14 as an explicit G-BASELINE and OR8 input. Put the security and compatibility rows (NFR2, NFR3, NFR12, FR12, FR36) first in the OR14 sequence.

**[Rubric: Decision-readiness · Adversarial general H8 · Platform contract M11]** — OR30's MediatR licence blocker sits outside every gate and recommends nothing, and licence compliance has no requirement (§12 OR30 line 744; §11.4; §8.1 line 444; NFR11–NFR12 lines 371–372)

OR30 is "Blocking for the next release", yet no §11.4 row, G-HIGH-RISK entry or release lane consumes it; G-PUBLICATION-AUTH, G-COMPAT and G-READINESS all ignore it. It admits that the released `Server` and `Gateway` packages reference MediatR 14.2.0 (Builds catalog `Directory.Packages.props:194`) and that the host suppresses the licence log (`"LuckyPennySoftware.MediatR.License": "None"`, `appsettings.json:6`). It still decides nothing about the published versions or about consumers that inherit the dependency, such as Tenants, and it lists three options with no recommendation or trade-off. The §8.1 latest-version mandate has no licence criterion, which is how an RPL-1.5-or-commercial version entered the catalog. OR30's "re-pin" option is a downgrade that §8.1 forbids unless it goes through the exception route. There is no licence allowlist, SBOM, provenance or signing requirement.

Fix: Bind OR30 into G-PUBLICATION-AUTH's `release-available` preconditions (or into G-COMPAT) with a named record. Recommend an option and state its consequence for consumers. Decide what happens to the published versions (notice, deprecate, or accept with a documented reason). Remove the log suppression or document why it stays. Add a dependency-licence-compliance NFR with a gate (an allowlist checked on catalog changes and on publication), make licence changes an explicit §8.1 criterion, and route any re-pin through the §8.1 exception mechanism.

**[Adversarial general H7 · Rubric: Decision-readiness (OR30 trigger)]** — "Release" is undefined, so §0, NFR12, G-COMPAT and OR30 contradict each other (§0 line 110; NFR12 line 372; G-COMPAT line 685; OR30 line 744; Glossary "Publication Lifecycle" line 186)

The PRD uses "release" both for NuGet and OCI publication and for the `release-available` lifecycle state. The rubric notes that OR30's trigger is not a lifecycle state at all. If §0 bars publication, OR30's trigger can never arrive before readiness, and the fourteen packages already shipped contradict §0. If publication continues, as the operator-dispatched `release.yml` allows (including a `bypass-validation` input that substitutes commitlint proof for CI proof), then §0 bars only `release-available`, NFR12's "Release must fail unless …" is false today, and nothing stops the next publication. Two release owners would act in opposite ways.

Fix: Define "package publication" and "`release-available`" as separate terms, and state which gates block publication now. Then either make NFR12 and OR30 publication blockers with an enforcing lane, or state explicitly that publication continues ungated until G-COMPAT passes and name the risk owner.

**[Rubric: Shape fit]** — Lifecycle state embedded in requirement text and test-pinned prose means the PRD cannot stay current (NFR8 line 368; NFR18 line 378; §6.8 lines 340–343; §11.3; §11.4 "Current result" cells; OR9 line 756)

The PRD is three documents in one (requirements baseline, exit contract and live status ledger), and it has grown from 583 to 770 lines (163 KB). NFR8 carries Story 6.1's reopen and patch P-D1, NFR18 carries an assignment date, and the §11.1 FR26 row carries Story 5.3's status. Readiness re-opens on any FR/NFR text change, and OR8 diffs that text against `epics.md`, so every lifecycle update edits a requirement. `Contracts.Tests` GUARDED comments pin three sections word for word (lines 338, 643 and 674), so correcting the prose requires a code change. The stale identities, stale ownership and 18-requirement divergence found in this run are the cost. OR9 keeps the restructure deferred to protect section anchors, and that stability now costs correctness.

Fix: Do the minimal OR9 split now:

- Keep FR/NFR text free of dates, story states and patch IDs. Move NFR8's Story 6.1 narrative to OR5 and §11.3, and NFR18's assignment to §11.2.
- Move the §6.8 status bullets, the §11.3 register and the §11.4 "Current result" cells into a generated ledger keyed by gate and clause ID, using the G-BASELINE and G-MVP-COVERAGE manifests.
- Leave anchor stubs in the PRD and retarget the GUARDED tests to the ledger.

**[Brownfield truth H3 · Adversarial general M2 (Epic 8 bullet)]** — Epic 8 lifecycle is wrong: Story 8.2 is done and 8.3 is in progress (§11.3 bullet line 658; SM4 line 533)

The PRD says "Story 8.2 is authorized but still `backlog` … Stories 8.3-8.11 remain predecessor-gated", and SM4 says Stories 7.14 and 8.2 "Both remain `backlog`". `sprint-status.yaml` has 8-2 `done` and 8-3 `in-progress`. `epics.md:6779` records 8.2 as done under `AR-20260914-01`, which authorizes 8.3, and the 8.3 file records implementation through a fourth review pass on 2026-09-17.

Fix: Restate the G5 path as 8.1 and 8.2 done, 8.3 in progress and 8.4–8.11 predecessor-gated. Correct SM4 so that only 7.14 remains backlog. G5 stays `needs-additive-api`.

**[Brownfield truth H4 · Adversarial general M1 (NFR17)]** — §11.1 and §7.1 primary slices are not what the cited `epics.md` story sections declare, and §7.1 and §11.2 disagree on NFR17 (§11.1 FR4, FR5, FR7, FR34 rows lines 564–594 and footnote line 599; §7.1 lines 388–435; §11.2 NFR17 row line 625)

The footnote says ownership "is declared by each `### Story` section in `epics.md`", but those sections disagree with the PRD:

- They declare the FR4 slices (1.16, 2.7, 2.11), the FR5 slices (1.3, 1.14), the FR7 slices (1.6, 1.18, 1.19) and FR34-C10 (7.11) as supporting, not primary.
- Story 1.15, which the PRD assigns FR7-C3, declares no FR7 at all.
- §7.1 makes 7.6, 7.8 and 7.9 primary for NFR17-C1, C3 and C4. §11.2 lists them as supporting, which matches epics, and lists 3.14, 5.6 and 5.8 as primary although they own no NFR17 clause.
- `epics.md` contradicts itself on FR12: Stories 2.9 and 2.11 still claim the primary slices that the PRD and the FR12 completion rule give to 2.15.
- §11.1 attributes FR4 to Epic 2, while the FR Coverage Map gives it to Epic 1 only.

G-CLAUSE and OR8 compare exactly these tables, so the PRD fails its own drift guard.

Fix: Label the §7.1 and §11.1 slices "PRD-proposed, not yet declared in `epics.md` (G-CLAUSE / Story 9.4)", or promote the declarations through correct-course. Regenerate the §11.2 primary columns from §7.1 for clause-bearing NFRs. Route the stale Story 2.9 and 2.11 coverage lines to the OR8 guard.

**[Brownfield truth H6 · Adversarial general L2 (finality)]** — The current PRD bytes are unreviewed; `status: final` and the finalize fields describe an earlier document (frontmatter lines 3–21; folder `.memlog.md` lines 106–120)

Reviewers sealed `9a35ebce…` at `9cde9f2d`, and `prd_finalize_reviewed_head: dd55a6d1` is not even the sealed head. Fifteen later commits changed requirement text (FR37, NFR1, NFR8, NFR18), the MVP production-profile scope (§9.2, §13), the Assurance Control, OR10, OR30, SM11 and the gate narrative, all with no reviewer gate. That unreviewed delta is the source of the SM11, register, ownership and NFR1 findings. Planning edits landed under unrelated subjects (`c60503c1` "feat: add obligation audit…", `b2c4bf79` "test: add LegacyCommandReplayJsonAdmissionTests…"), and those subjects feed semantic-release. The memlog says "nothing committed" for edits that are already in HEAD.

Fix: Run the reviewer gate on the current bytes before keeping `status: final`, or set `document_status: draft-pending-review`. Record the digest and head that reviewers actually examined, and append a memlog correction. Keep planning edits in separate `docs(planning)` commits.

**[Adversarial general H4]** — The time-separated attestation has no trusted clock, no authentication mechanism and no subject boundary (Glossary "Assurance Control" (2) line 201; §11.4 invalidation rule line 672; G-BASELINE line 678)

"Created at least 24 hours after the last authored change" has no time source. Git author and committer dates are author-controlled and can be backdated, and `architecture.md` fixes a trusted clock (push-activity time) only for AD-26 records, so a solo maintainer can satisfy the window instantly for every other subject. "Authenticated" is not tied to a signed commit, a platform review or the CI identity. "Subject" is undefined per transition. For `READY`, the subject is the G-BASELINE manifest, which binds `sprint-status.yaml`, and concurrent loops edit that file continuously. Since any governed change invalidates, `READY` needs a 24-hour freeze of every bound artifact, and the PRD never says so.

Fix: Define the clock as the platform push-activity time of the push that last changed the subject digest, for every subject. Define the attestation carrier, for example a signed tag or a record verified by the sealed run. Define the subject per guarded-transition type, and state the freeze or exclusion rule for volatile inputs such as the tracker.

**[Adversarial general H5]** — G-HIGH-RISK lets the author classify any gate out of the Assurance Control, and the level for "standard" rows is undefined (§11.4 preamble line 672; G-HIGH-RISK line 690; per-row evaluator cells lines 680–689, 693)

The PRD fixes no floor on which gates must be high-risk. The matrix author can therefore reason-code G-TENANT as `standard-control` even though the G-TENANT row demands "approval at the G-HIGH-RISK Assurance Control level", and the PRD gives no precedence between row and matrix. G-BASELINE, G-CLAUSE, G-NFR8, G-NFR18, G-NFR-OWNERSHIP and G-READINESS name no assurance level, so "`READY` carries the lowest assurance level of its inputs" is undefined.

Fix: Fix the high-risk set in the PRD, at minimum every row that already cites the G-HIGH-RISK level. Give `standard-control` an explicit assurance level, and make the matrix validator reject any classification below a row's PRD-declared level.

**[Adversarial general H6]** — The NFR3 break-glass keys on environment names, so any production deployment not literally named "Production" can run symmetric keys (NFR3 line 363; Glossary "Break-glass" line 184)

Break-glass admits symmetric mode in environments "that are neither Development nor Production". A real deployment named `Staging`, `prod`, `production-eu` or `Live` qualifies, so `AllowInsecureSymmetricKey` admits HS256 there. Development admits symmetric mode even without break-glass, so a misnamed environment fails open twice. NFR4's "non-Development fallback" has the same name-based shape.

Fix: Define production by the canonical profile and deployment identity (AD-26 or G-PUBLICATION-AUTH), not by environment name. Permit break-glass only on a closed allowlist of non-production environment identities, and reject unknown names.

**[Adversarial general H9 · Rubric: Done-ness clarity (low)]** — Gate pass commands are undefined or set by the very story being gated (G-BASELINE line 678; G-TENANT line 680; G-STATUS-ID line 681; G-CLAUSE line 683; G-NFR8 line 684; NFR8 line 368; OR17)

- G-BASELINE and G-CLAUSE commands are "not yet defined".
- G-TENANT and G-STATUS-ID commands "must be bound by that story", so Stories 2.14 and 2.15 define the command that decides whether they pass.
- G-NFR8 defers its command to the spec. Story 6.3 sets the projection-cost budget that 6.4 must meet, with no PRD floor, so the budget can be written after the measured cost is known.

Under one maintainer, the evaluator, the author and the criterion-setter are one identity, separated only by the 24-hour attestation. OR17 acknowledges the gap but is the only gate-infrastructure refinement without a primary story. The rubric rated the missing commands low; the adversarial reviewer rated the self-set pass condition high.

Fix: Fix minimum pass conditions in the PRD, for example NFR8 bounds relative to stream length and page size. Require each gating command to be pinned in a predecessor record before the gated implementation starts, with its digest bound by G-HIGH-RISK. Assign OR17 to the stories that own the validators (9.3, 9.4, 2.14, 2.15), and require each to name its command before starting.

**[Platform contract H1]** — NFR2 does not say whether a credential grant can set the request tenant scope; AD-27 says it never can, but the default generated mode does (NFR2 line 362; UJ2 line 170; §8.2 bullet 3 line 454)

AD-27 says that grants "authorize but never set the scope". Three code paths contradict it:

- The default `RestTenantSource.Claims` mode makes generated controllers use the sole `eventstore:tenant` grant as the request tenant (`RestApiControllerEmitter.cs:432-444`).
- The gateway status endpoint uses every grant as a lookup scope (`CommandStatusController.cs:90-114`).
- Admin Server fills a missing tenant with the caller's first grant (`AdminTenantAuthorizationFilter.cs:31-37`).

NFR2 conflates the single request scope with the grant set, in which a principal may legitimately hold many grants, and it leaves "duplicate" undefined. Story 2.14's ACs do not mention the `Claims` mode.

Fix: State the scope-source rule. Either take the scope from the route, body or header only and classify `Claims` mode as a breaking change under NFR12, or admit sole-grant derivation as a cataloged mode and amend AD-27 to match. Define grant-set semantics: many grants are allowed, each is canonicalized, and duplicates are collapsed after normalization. Mirror the rule in UJ2, §8.2 and Story 2.14.

**[Platform contract H2]** — G-TENANT's required evidence is narrower than NFR2 and narrower than Story 2.14's extended scope (G-TENANT line 680; FR12-C2 line 402)

G-TENANT requires only generated-controller and Tenants runtime tests. Story 2.14 was extended on 2026-10-07 to the gateway `ClaimsTenantValidator`, Admin Server, the SignalR hub and admin storage. The gateway admits a global administrator to any tenant, including `system` and an empty tenant (`ClaimsTenantValidator.cs:22-25`), and compares tenants with `Ordinal` without normalizing them. The hub uses the same validator, and Admin Server also compares with `Ordinal`. G-TENANT can therefore pass while those boundaries still violate NFR2.

Fix: Make G-TENANT's governed universe every NFR2 boundary by name: gateway command, query, status, stream and admin-storage routes; the SignalR hub; Admin Server; generated controllers; and the Tenants host. Add a §7.1 clause for the non-generator boundaries that Story 2.14 owns, and require identical accept and reject results across all of them.

**[Platform contract H3]** — Generated command endpoints have no retry-safe identity or idempotency-key contract (FR12 line 256; FR27 line 288; NFR7 class (e) line 367; UJ2 line 170)

Generated actions mint a new `MessageId` server-side (`RestApiControllerEmitter.cs:266-267`) and never forward `IdempotencyKey` or `CorrelationId`. Durable admission runs only when a key is present (`IdempotencyAdmissionCoordinator.cs:24-27`), so keyless commands take the legacy path in every environment. Every client retry through a generated API is therefore a new command, which is NFR7 class (e) duplicate side effects. AD-5 requires a key, an AD-25 admission and a fence for every production mutation, so once AD-5 is enforced, generated APIs must either fail closed or bypass admission. The PRD does not define the roles of `MessageId` (FR23) and `IdempotencyKey` (FR27) for consumers.

Fix: Add an FR12 clause that defines one caller-supplied idempotency channel (for example, an `Idempotency-Key` header mapped to `IdempotencyKey`) and states whether callers may supply `MessageId`. Relate `MessageId` dedupe to keyed admission. State that keyless production mutations fail closed. Extend NFR7(e)/SM11 to keyless entry points, and bind the clause to G-STATUS-ID or G-OQ8.

**[Platform contract H4]** — NFR3 contradicts the AD-10/AD-36 credential profiles, and the PRD has no workload-versus-delegated-user call contract (NFR3 line 363; FR28 line 302; §8.2 bullets 5–6 lines 456–457)

NFR3 makes role and tenant validation mandatory "in every mode". AD-10 defines three versioned validation profiles with per-host fingerprints. AD-36 says a workload principal "never carries tenant, role, or administrator claims" and that every sidecar-routed call is exactly one of delegated-user or workload. Read literally, NFR3 makes every workload assertion nonconforming. The PRD never names the call kinds that domain modules depend on. Generated hosts hand-roll delegated-user bearer forwarding (`InboundBearerForwardingHandler.cs:9-17`), and the platform provides no handler for it.

Fix: Restate NFR3 per validation profile, with the mandatory and forbidden claims for each. Promote the AD-36 call-kind rule into FR28 or §8.2. Require G-AUTH-HOSTS evidence for each profile fingerprint, and a platform-owned delegated-user forwarding handler, in line with FR1 and SM8.

**[Platform contract H6]** — Dead-letter CloudEvents use the caller-reusable `CorrelationId` as their `id` (FR23 line 286; FR34 line 323; NFR6 line 366; NFR12 line 372)

`DeadLetterPublisher.cs:56` sets `["cloudevent.id"] = safeMessage.CorrelationId`, and callers may supply and reuse a correlation ID across commands (`CommandsController.cs:184`; AD-32). CloudEvents requires `source` + `id` to be unique per distinct event, so a consumer or broker that deduplicates by `id`, as NFR6 directs, can silently collapse distinct dead letters. Publication failure is swallowed (`DeadLetterPublisher.cs:77`). FR23 covers only events, FR34 says nothing about dead-letter identity, and AD-8 requires poison records keyed by stable `MessageId`.

Fix: Extend FR23 or FR34-C1 so that every published CloudEvent, dead letters included, has a unique, stable `id` derived from `MessageId`, and state that correlation is never message identity. Classify the wire change under NFR12 and make dead-letter publication durability explicit.

**[Platform contract H7]** — A projection-backed `304` can be returned for changed data, and the PRD neither forbids nor gates it (FR4 line 239; NFR8 line 368; FR4-C3/C6 lines 389, 392)

Architecture records that "Under `Direct`, a lost regeneration may return `304 Not Modified` on changed data" (`architecture.md:549`). It routes the fix only to a Story 5.5 owner test. The client treats a `304` as projection-confirmed whenever provenance is `ProjectionBacked` and a strong ETag is present (`EventStoreGatewayClient.cs:205-228`). NFR8 therefore labels as authoritative a response that architecture knows can be stale.

Fix: Add an FR4 clause that allows a projection-backed `304` only when its validator derives from persisted read-model or checkpoint state; otherwise the response is `200` or degraded. Name the owner and bind the clause under G-CLAUSE.

### Medium (30)

**[Rubric: Decision-readiness · Brownfield truth M8 · Adversarial general M5]** — "INDEPENDENT" status labels contradict the single-maintainer Assurance Control, and a test locks them in (§6.8 line 342; G-RUNTIME-PARITY line 687)

"TECHNICALLY VALIDATED; INDEPENDENT CONTROL OPEN" and "TECHNICAL PASS; INDEPENDENT GATE BLOCKED" tell readers that a second human is required. With a one-human registry, the required level is `single-maintainer-attested`, which is cleared by sealed CI plus a time-separated owner attestation (Story 9.2). The labels therefore say the opposite of the 2026-09-26 owner decision. `CorrectedDeployedRuntimeParityClosureTests.cs` (lines 4562–4593) pins the labels. The PRD-bound Story 3.15 record (`3-15-corrected-deployed-runtime-parity-closure.md:24`) links an "independent Test Architect decision" that the same paragraph and the tracker call self-attested.

Fix: Rename the labels "ASSURANCE CONTROL OPEN" and "TECHNICAL PASS; ASSURANCE GATE BLOCKED", update the pinned test in the same change, and fix the link text in the 3.15 record.

**[Rubric: Decision-readiness]** — The residual risk accepted with the Assurance Control is never named (§4 Owner Roles line 200; Assurance Control line 201)

The owner "accepted its residual risk", but the PRD never says what that risk is. For example, the author may misread a requirement, or a validator may be green by construction. Sealed CI proves that the validator ran, not that it checks the right property. The 24-hour separation guards against haste, not against a blind spot the author shares with the validator.

Fix: Add one sentence that names what the control does not protect against and what partly compensates for it (the optional `tool-persona` adversarial review, mutation checks). Note that the control re-evaluates automatically when the registry gains a second human.

**[Rubric: Strategic coherence]** — MVP scope still follows the epic inventory rather than the thesis (§9.1 lines 490–500)

This has been open since 2026-09-10. With 17 failed gates and 24 blocking ORs, the PRD gives no order derived from its thesis: which outcomes are exit-critical for the bet, and which are enabling infrastructure. The 2026-10-07 work implies one (the 9.1 bootstrap, then gate validators, then the G-APPEND envelope), but only `epics.md` records it.

Fix: Add a short scope-logic paragraph that groups gates by thesis leg and enabling dependency: bootstrap (9.1), then gate infrastructure (9.2–9.5), then the safety gates (G-APPEND, G-TENANT, G-STATUS-ID, G-AUTH-HOSTS), then the parity → publication → consumer chain. Do not duplicate story sequencing.

**[Rubric: Done-ness clarity]** — "Bounded" is used without a bound or a pointer to one (FR6 line 241; FR17 line 270; FR30 line 290; FR34 line 323; FR34-C1 line 415; UJ4 line 172)

These requirements give neither a number nor the spec that will hold one:

- FR34 "bound in-memory deduplication" and FR34-C1 "bounded deduplication".
- FR6 "payload limits".
- FR17 "readiness retry".
- FR30, which has no recovery window.
- UJ4 "bounded recovery contract".

Any finite value satisfies them, so a story can close them by assertion.

Fix: Give each a number, or name the owning AD or spec that will hold it, as NFR8 does for the projection bound.

**[Rubric: Scope honesty · Brownfield truth M6]** — Epic 9 is missing from the MVP scope statement (§9.1 line 492; §11.4 owners)

§9.1 lists "Epics 1-7" and mentions Epic 8, but the PRD never mentions Epic 9. Yet `epics.md:389` calls Epic 9 an "MVP epic, independent of Epic 8". Its Stories 9.1–9.5 are primary owners of OR28, OR10, OR13, the §0 corrective-work bootstrap, G-BASELINE, G-CLAUSE, G-HIGH-RISK and G-MVP-COVERAGE.

Fix: Add Epic 9 to §9.1 as governance scope with no FR: gate machinery that grants no readiness, release or deployment authority. Say whether it enters the G-MVP-COVERAGE denominator.

**[Rubric: Scope honesty · Adversarial general M4 · Brownfield truth L4]** — The AD-26 `[ASSUMPTION]` is imported as binding: the glossary calls a profile "currently authorizing" while §13 lists no assumptions (Glossary line 188; G-PUBLICATION-AUTH line 688; G-CONSUMER line 689; §11.3 architecture row line 648; §13)

The glossary entry contradicts itself. It says "AD-26 defines exactly one currently authorizing production profile … The file is currently absent, so no profile is authorizing". Meanwhile §11.3 says AD-26 "remains `[ASSUMPTION]`", which `architecture.md:337` confirms. Two gates compute against it. Only the profile-slot mechanics bind now; the profile target binds only after ratification. §13 reports no assumptions.

Fix: Index imported assumptions in §13 (AD-26, its owner and the ratification trigger). Reword the glossary entry as "AD-26, once ratified, will define …". Make G-PUBLICATION-AUTH and G-CONSUMER depend on AD-26 ratification explicitly.

**[Rubric: Downstream usability · Brownfield truth M2 · Adversarial general M1 (NFR5, NFR18)]** — §11.2.1, §11.2 and G-NFR18 contradict each other on NFR5 and NFR18 ownership, and §11.2 omits declared owners (§11.2.1 line 634; §11.2 rows lines 613–625; G-NFR-OWNERSHIP line 692; G-NFR18 line 691)

- §11.2.1 says "NFR5's primary-owner gap remains open", while §11.2 and G-NFR-OWNERSHIP name Story 2.16.
- G-NFR18 says "Document, owner, evidence, and validation command are absent", while NFR18 and the same row name Story 6.7.
- The NFR7 row omits Story 4.16, the class (c) primary (`epics.md:4085`), and the supporting Story 4.17.
- The supporting cells omit 2.13 for NFR5, NFR12, NFR15 and NFR16, and 3.17 for NFR12 and NFR17.

Fix: Replace the §11.2.1 sentence with "NFR5's primary owner is Story 2.16; Story 2.13 supports". Correct G-NFR18, add 4.16 as the class (c) primary owner, and complete the supporting cells.

**[Brownfield truth M1]** — Story 6.5's event-versioning gate is done and 6.6 is in progress, but the PRD still says the specification is required (§11.3 bullet line 662; FR33-C5 line 413)

The PRD says "the event-versioning specification remains required before Story 6.6". In fact, 6-5 and 6-5a–6-5d are `done` and 6-6 is `in-progress`. `spec-event-versioning-upcasting.md` is `normative-approved` and says "Story 6.6 is ready".

Fix: Record FR33-C5's approved specification by identity and Story 6.6's in-progress state. The FR33-C6 evidence stays open.

**[Brownfield truth M3]** — The PRD contradicts itself on whether the default OQ8 validator passes (§11.3 OQ8 row line 653; G-OQ8 line 679; OR15 line 725)

§11.3 and G-OQ8 say the Story 4.15 v4 default validators pass. OR15, in a sentence added in `400589a5`, says the default probe "is blocked by v5 clean-checkout validation". `python3 -I tools/validate-oq8-platform-evidence.py` exits 0 on HEAD. `--pre-review` exits 1 with a 4.15 lifecycle drift, which is expected after closure.

Fix: Drop or date the OR15 sentence, or name the exact failing command and checkout mode.

**[Brownfield truth M4]** — OR14 still owes architecture work that has already landed (OR14 line 724)

OR14 still asks architecture to add the Tenants boundary to AD-10 and to align AD-11/AD-26 to the lifecycle vocabulary. AD-10 already names the Tenants domain-service host and API (`architecture.md:186`). AD-11 carries the full five-state lifecycle, and AD-26 uses it. All eleven inline `[ASSUMPTION]`s were owner-resolved on 2026-10-07 except AD-26.

Fix: Narrow OR14 to what remains: reviewer closure, the AD-26 ratification records, the AD-16 amendment, the UX assumptions and the epics renewal.

**[Brownfield truth M5 · Adversarial general L2 (source list)]** — Provenance gaps: proposals that changed or reference the PRD are unlisted, and the McpCli "Approved" banner has no source (frontmatter `source_artifacts` lines 23–96; banner line 101; FR26-C4 line 408)

Four sprint-change proposals are missing from `source_artifacts`, although the 2026-09-08 rule says every proposal is listed:

- `sprint-change-proposal-2026-10-05.md`, which added §8.4 and §11.2.1 and changed FR37.
- The 2026-09-27 proposal (Story 6.5 split, cites FR33-C5).
- The 2026-09-30 proposal (cites `prd.md:408`).
- The 2026-10-07-story-9-2-nfr1 proposal ("No PRD edit is proposed"; the rubric's mechanical notes also mention it).

The "Approved McpCli course correction (2026-09-27)" banner and the FR26-C4 rewrite (`4fcb2b5c`) cite no artifact. The 2026-09-27 proposal concerns the Story 6.5 split, and AD-35 attributes the correction to a Platform course correction that exists only in `references/Hexalith.Platform`.

Fix: Add the four proposals to `source_artifacts`. Cite the McpCli authority by repository, path and commit, or mark it unverified. Move the banner into §1.

**[Adversarial general M3]** — Story 6.2 authority is revoked only in prose, while the machine-readable flag still says authorized (NFR8 line 368; OR5 line 721)

The PRD calls the `approved-authorized` frontmatter "superseded until review patch P-D1 lands". `spec-folded-snapshot.md` still carries `status: approved-authorized` and `story_6_2_authorized: true`, and its body still says "AUTHORIZED", so any loop or validator that parses the flag will start Story 6.2. NFR8 also still pins the pre-reopen digest `0b456b5f…` as "binding".

Fix: Require P-D1, or an interim flag flip, before the PRD claims supersession. Remove "binding" from the reopened digest.

**[Adversarial general M6]** — SM3 and OR13 measure "high-risk NFR" coverage at clause level, but most of those NFRs have no clauses (SM3 line 527; §11.2 preamble line 603; OR13 line 715; §7.1 lines 385–435)

§7.1 defines clauses only for NFR17. For the other high-risk NFRs in SM3's list, "complete clause-level primary coverage" has no denominator, so SM3 cannot be measured. The PRD also has two "high-risk" universes: SM3's NFR list and the G-HIGH-RISK gate classification. OR13's "recorded against a high-risk NFR" says neither which universe applies nor whether a supporting declaration counts.

Fix: Either add clauses for the high-risk NFRs or restate SM3 at requirement level. Define "recorded against" as primary or supporting.

**[Adversarial general M7]** — "ULID-safe" is undefined and conflicts with the preserved v1 MessageId grammar (FR33 line 313; Glossary "MessageId Contract Version" line 198; "Aggregate Identity" line 182; §8.2 line 452)

Teams can read "ULID-safe" in two ways. One reading is "is a ULID", which rejects every event that carries a v1-derived or non-ULID aggregate ID. That breaks NFR12 v1 compatibility and could make legacy streams unreadable on replay. The other reading is "not parsed with `Guid.TryParse`". The PRD also leaves "identity components" and the point of rejection (append or replay) undefined.

Fix: Define "ULID-safe" per field. State which fields must be canonical ULIDs and which may carry v1 or `AggregateIdentity` strings, and where rejection occurs.

**[Adversarial general M8]** — The "Production Path" definition does not require production-profile components (Glossary "Production Path" line 206; cf. AD-26)

Under this definition, evidence gathered on Redis or any other "durable" component counts as production-path evidence, yet AD-26 says Redis is non-authorizing and for Development or test only. NFR7, NFR16 and SM11 could then be "proven" on a provider the production profile excludes, and the append-race outcome is specific to the provider.

Fix: Require production-path packets to declare their component profile. Count only canonical-profile components toward the NFR7 and NFR16 production-path classes.

**[Adversarial general M9]** — The FR34 IntegrationTests lane only has to run, not pass (FR34 line 323; FR34-C10 line 424)

The text says "the lane runs on every push to `main` and asserts persisted state for at least the FR23, FR27, and FR30 paths". A lane that runs and stays red satisfies that wording. Nothing requires it to pass or to block any transition.

Fix: Require a passing result on the bound subject as evidence for FR23, FR27 and FR30 closure, and name the transition the lane blocks.

**[Adversarial general M10]** — The role vocabulary is not closed, and the roster that sets the assurance level governs itself (Glossary "Owner Roles" line 200; "Assurance Control" line 201)

The PRD distinguishes six roles, but the PRD and architecture invoke others: architecture owner, test owner, Test Architect, tracker owner, persistence owner, Builds owner and Platform deployment role. Validators must reject "wrong-role issuers" against a vocabulary that does not exist. The gated party edits the registry. Nothing makes the required level monotonic, and nothing distinguishes a human identity from a tool account.

Fix: Enumerate the full role set. Make registry edits a sealed, time-separated transition. Forbid lowering the required level for subjects already evaluated.

**[Adversarial general M11]** — "Or" escape hatches create alternative conformance paths with no selection rule (FR4 vs FR4-C5 lines 239, 391; FR16 line 260; FR30 line 290; FR12 line 256)

- FR4 accepts an "owner-approved mapping", but FR4-C5 requires all six lifecycle states, so a lossy mapping can satisfy FR4 while violating its own clause.
- FR16's DAPR path applies "only where a subscriber cannot hold a SignalR connection", and nobody is named to decide that.
- In FR30, "drain them" can be read as discarding events, which is NFR7 class (d) loss.
- FR12 allows "omit `Location` or cause a safe rejection", which gives clients two contracts.

Fix: Pick one behavior per requirement, or state the selection criterion and who decides it. Remove "drain" or define it as non-lossy.

**[Adversarial general M12 · Platform contract M1]** — NFR2's invariant-lowercase normalization folds non-ASCII lookalikes into valid tenants (NFR2 line 362)

Unicode case mapping lowercases U+212A KELVIN SIGN to ASCII `k` before the ASCII grammar check. Platform contract confirmed this with a scratch `dotnet run` probe: `"Kcme".ToLowerInvariant() == "kcme"` returns `True`, where the first character is U+212A. Distinct raw inputs or grants therefore alias one tenant, and components that log, cache or forward the raw value diverge. The normalization also contradicts NFR2's own "not trimmed or repaired".

Fix: Reject any non-ASCII code point before normalizing, then fold only ASCII A–Z to a–z. Add Kelvin-sign and dotted-I negatives to G-TENANT.

**[Adversarial general M13]** — The FR36 consumer binding names a source SHA for package-mode consumers (FR36 line 334; FR21 line 274)

FR21 makes package references the default, so a consumer's checked-out EventStore SHA says nothing about what it runs. The binding can match while the consumer runs different packages, or fail while it runs the right ones.

Fix: Bind the consumer to the promoted package or OCI identities, and keep the SHA as provenance only.

**[Adversarial general M14 · Platform contract L4]** — The PRD omits concerns an event-sourcing platform carries, including the mixed-version rollout contract, without excluding them (§5 lines 216–226; §7; §9.2; FR33-C5/C6 lines 413–414)

- **Mixed-version compatibility:** nothing requires forward compatibility, mixed-version readers during rolling deployments, or rollback after newer schemas are written. FR37 covers this for payload protection only. Platform contract adds that AD-13's rule, "V2 admission fence stays until the writer and every serving reader and consumer are compatible", and the fail-closed evolution-service outcomes have no FR33-C5/C6 wording that consumers can see.
- **Recovery targets:** there is no RPO, RTO or restore-consistency requirement, although AD-26 and Story 7.22 make restore posture a promotion precondition.
- **Retention:** event-stream, command-status, dead-letter and audit retention are unstated.
- **Performance and availability:** there are no targets beyond the deferred NFR8.
- **Quotas:** there are no per-tenant quotas or rate limits.

Fix: Add requirements for each concern or list each as an explicit exclusion in §9.2. At minimum, add a one-line mixed-version rollout consequence to FR33-C6.

**[Platform contract M2]** — The MessageId version is undefined for shared surfaces, and "version-ambiguous" is undefined (Glossary "MessageId Contract Version" line 198; FR12 line 256)

AD-17 assigns a version per command-contract declaration. The generic `POST /api/v1/commands` and the shared status route have no declared version. The gateway accepts v1 grammar for every command (`SubmitCommandRequestValidator.cs:35-40`). Every v2 value is also a valid v1 value, so ambiguity cannot be decided syntactically.

Fix: Assign a version or a resolution rule to the generic submit and status surfaces, and define "version-ambiguous" operationally.

**[Platform contract M3]** — NFR5 reads as configurable defaults, and code lets operators raise the limits that architecture calls ceilings (NFR5 line 365)

The options validators check only `> 0` (`ProjectionChangeNotifierOptions.cs:125-131`; `SignalROptions.cs:59-65`). AD-8 says the limits are `Contracts`-owned ceilings over keys plus values, which options may lower but never raise. Receivers reject and broadcasters clip.

Fix: Restate NFR5 as non-raisable ceilings over keys plus values, with receiver-reject and broadcaster-clip behaviour. Add raising-attempt negatives to Story 2.16's evidence.

**[Platform contract M4]** — The `ProjectionVersion` token semantics are missing from the PRD (FR4 line 239)

AD-15 makes `ProjectionVersion` an opaque equality token that consumers must never parse, order or increment. FR4 lists it only as propagated metadata, although it is a consumer-visible header protected by NFR12.

Fix: Add an FR4 clause carrying AD-15's equality-only, scope-bounded, may-change-on-rebuild semantics.

**[Platform contract M5]** — There is no canonical public error contract, and generated hosts diverge from the gateway (NFR12 line 372; FR12-C1 line 401)

FR12-C1's "safe Problem Details" cannot be tested as written. Generated controllers emit `Type = "about:blank"` (`RestApiControllerEmitter.cs:667`). The gateway uses a 20-URI catalog with `code`, `category`, `retryable` and `clientAction` extensions. The same tenant rejection therefore carries a different `type` depending on the host.

Fix: Define the stable error contract: the type-URI catalog, required extensions, retryability, `Retry-After` rules and support-safe detail. Require the generator to match the gateway catalog.

**[Platform contract M6]** — The gateway's own 202 `Location` is built from the request `Host` (FR12 line 256)

FR12 regulates only generated commands. AD-17 requires `Location` to be built from trusted configuration. `CommandsController.cs:193-195` builds it from `Request.Scheme` and `Request.Host`, and `AllowedHosts` is `"*"`. The header therefore reflects any `Host` the caller supplies.

Fix: Extend the absolute, trusted-configuration `Location` rule to every public 202, including the gateway's, or require a validated forwarded-host configuration.

**[Platform contract M7]** — NFR12 has no deprecation window or signalling, and approval is not tied to the version bump (NFR12 line 372; G-COMPAT line 685)

NFR12 sets no minimum support or deprecation period. It requires no `[Obsolete]` or diagnostic ID, no OpenAPI `deprecated` marker and no `Deprecation`/`Sunset` header. Nothing binds the approval record to the semantic-release major bump. No API baseline tooling exists, which is consistent with G-COMPAT's FAIL.

Fix: Add a support window and deprecation signalling for each surface kind. Add a release-lane check that a major bump carries an approved proposal digest.

**[Platform contract M8]** — The NFR7 class (c) "supported operating envelope" is undefined in the PRD (NFR7 line 367; G-APPEND line 682; §9 line 486)

AD-5 defines the envelope: per-actor-host `actorStateStore` components, physical targets bound into the AD-26 digest, one-app-ID database grants, and re-proof on every DAPR minor-version change. G-APPEND binds none of these. The AppHost shares `statestore` (`actorStateStore: true`, `keyPrefix: none`) across `eventstore`, `eventstore-admin` and `tenants`. The unauthenticated actor callbacks in Platform contract C1 add a second-activation path.

Fix: Promote the envelope's mandatory elements, the AD-26 digest binding and the runtime re-proof trigger into G-APPEND's evidence and invalidation columns.

**[Platform contract M9]** — FR25 still mandates mutable `@main` shared gates, so a sealed result is not reproducible (FR25 line 276; Glossary "Assurance Control" (1) line 201; OR18 line 757)

`ci.yml`, `codeql.yml`, `commitlint.yml` and `dependency-review.yml` call `Hexalith.Builds@main`. A seal binds the caller workflow's digest, but not the reusable-workflow content resolved from `@main`. Only the release path is SHA-pinned. OR18 covers only the OCI platform set.

Fix: Classify each shared workflow as authorizing or advisory, and require a SHA pin for anything a seal or release consumes. Bind the resolved reusable-workflow SHAs into seal evidence, and amend FR25.

**[Platform contract M10]** — AD-18 outbound control-plane header ownership has no PRD requirement (None: no FR, NFR or §8.2 bullet)

The handler replaces `dapr-app-id` and `dapr-api-token` rather than appending them, and structural tests guard this. Architecture still records that omitting `.AddEventStoreDaprServiceInvocation` "is currently fail-open". No PRD owner or gate covers that residual.

Fix: Add a §8.2 bullet and an FR28 clause covering discard, replace and caller-own-token, plus fail-closed detection of missing registration.

### Low (13)

**[Rubric: Decision-readiness · Platform contract L2 · Brownfield truth L1 (OR20/OR21)]** — Gate evidence keeps the reopen alternative the owner already decided against (G-TENANT line 680; G-STATUS-ID line 681; OR20; OR21)

G-STATUS-ID's evidence column still requires an "Approved corrective or reopened Story 2.9", while the same row and OR21 name successor Story 2.15 as primary owner. G-TENANT and Story 2.14 are in the same state, and OR20 and OR21 still say "Reopen … or approve corrective successors".

Fix: State the decision, which is the successor story, and drop the reopen alternative. Name Story 2.15 (and 2.14) in the evidence columns.

**[Rubric: Substance over theater]** — Furniture the PRD itself acknowledges remains (§5 lines 214–226; SM1 line 526; SM5 line 528)

§5 restates §6 and §7, as OR9 admits. SM1 "cannot fail and does not measure Phase 4 delivery", and neither SM1 nor SM5 discriminates any longer.

Fix: Fold §5 into the OR9 pass. Move the achieved planning-recovery metrics to a one-line history note.

**[Rubric: Strategic coherence · Brownfield truth L3]** — SM8 undercounts residual per-domain plumbing (SM8 line 541)

SM8 counts duplicated plumbing in Tenants alone, although FR9 and UJ1 also cover Sample. Within Tenants, it names only the host composition (`Hexalith.Tenants/Program.cs:46-181`). `src/Hexalith.Tenants.AppHost` and `src/Hexalith.Tenants.Aspire` still ship (gitlink `81144734`), and that per-domain Aspire wiring is on FR9's own removal list.

Fix: Include Sample in the count, or state why it is excluded, and add the Tenants AppHost and Aspire projects.

**[Rubric: Downstream usability · Brownfield truth L1 · Brownfield truth L5]** — Stale cross-references: OR rows owe work already assigned, the OR2 note contradicts OR15, and the body cites the superseded readiness result (OR6 line 722; OR7 line 732; OR25–OR26 lines 739–740; OR2 note line 764; §0 lines 108–110; §1 line 127; OR1 line 750)

- OR6, OR7, OR25 and OR26 still ask for owners for NFR18, FR36-C3..C5/NFR17-C5 and NFR5/NFR13. Stories 6.7, 3.19, 3.20, 7.21, 2.16 and 2.17 are already assigned.
- The OR2 retirement note says `epics.md` still describes loop-3 partial delivery for Story 5.3, but OR15 says 5.3 was reconciled on 2026-10-07.
- §0, §1 and OR1 still present the 2026-09-10 Poor/Reject as the bound result. The frontmatter binds the 2026-10-06 `implementation-readiness.md` (verdict FAIL, against `a6fc951e…`).

Fix: Reword the OR rows as "deliver and prove", refresh the OR2 note, cite the 2026-10-06 report, and map FAIL to `reject` once. This is exactly the drift the OR8 guard should catch.

**[Brownfield truth L2]** — OR19's premises no longer hold (OR19 line 758)

`epic-2: in-progress` is now correct, because 2.13–2.17 are backlog. The Story 2.12 key is complete (`sprint-status.yaml:114`).

Fix: Retire or restate OR19.

**[Brownfield truth L6]** — Mandatory rows and blocking refinements without a primary story (G-NFR-OWNERSHIP; G-READINESS; OR14; OR17; OR30)

These rows name no primary story, although the 2026-10-07 proposal's success criterion 1 requires every mandatory gate and blocking refinement to name one. The Rubric's Done-ness clarity low also flags OR17's missing owner; this report merges it into the high finding on gate commands.

Fix: Name an owner story, or mark the row owner-only.

**[Brownfield truth L7]** — The run memlog miscounts the drift (folder `.memlog.md` line 107)

The memlog reports 18 unlogged commits. There are 16 non-merge commits, and three of them (`2dd7ebfb`, `9cde9f2d`, `94482189`) are logged in the planning-level memlog.

Fix: Append a correction.

**[Adversarial general L1]** — "Documented crypto-shred boundaries" is unbounded and depends on post-MVP capability (NFR17 line 377; NFR17-C5 line 435)

The PRD defines no acceptance content. Crypto-shredding presupposes key-per-subject protection, which ships post-MVP (FR37, Epic 8), and GDPR-1 is out of MVP.

Fix: Define the required content, or move this to Epic 8 with an explicit MVP statement that crypto-shredding is not supported.

**[Adversarial general L3]** — NFR5 allows metadata values in logs at Debug level (NFR5 line 365)

"Must not expose metadata values above Debug level" lets production hosts with Debug enabled log the values, and "above" is ambiguous in direction.

Fix: Say "at Information or higher", or forbid value logging in non-Development environments.

**[Adversarial general L4]** — Implementation prescriptions still crowd the PRD (§8.3 (`FluentAccordion`, line 464); FR2 and FR3 method names; NFR17 YAML policy names)

This is prior finding M3, unchanged. Replaceable mechanisms sit beside product contracts without labels that tell them apart.

Fix: Apply the fix from prior M3 and OR9.

**[Adversarial general L5]** — The NFR19 key-zeroing proof cannot observe what it claims, and may pass vacuously (NFR19 line 379)

A test cannot observe copies that the managed runtime makes. If key operations stay in the Dapr crypto sidecar (§8.4), no in-process key exists, and the test passes vacuously.

Fix: Scope the test to in-process derived and unwrapped keys in pinned or unmanaged buffers, and require a positive control that proves a key was present.

**[Platform contract L1]** — FR3's endpoint list is incomplete (FR3 line 238)

The SDK also maps `/project/v2`, `/project/v2/reconcile`, `/project/rebuild/{,stage/,commit/,abort/,verify/}v1` and `/project/rebuild/shared/v1` (`EventStoreDomainServiceExtensions.cs:244-449`).

Fix: Make the NFR12 / AD-33 inventory the authoritative endpoint list, and call FR3's five endpoints a minimum.

**[Platform contract L5]** — FR16 does not state that `PubSub` projection-change transport is non-production (FR16 line 260)

AD-8 makes `Direct` the only transport eligible for production. The options default to `Direct`, and the validator refuses `PubSub` without a binding issuer.

Fix: Add that limit to FR16.

## Delta vs 2026-09-10

- **Resolved in the PRD text:** no executable exit contract (§11.4 now has 17 computed rows plus a post-MVP G5 row); omnibus requirements without clause-level closure and acceptance ambiguity in FR4, FR5, FR7 and FR12 (the §7.1 ledger, 49 clauses); §11.2 omitting NFR5, NFR12 and NFR13; no user journeys (UJ1–UJ5); NFR12 scope, the NFR3 closed host list and FR36 consumer authority (capability-defined and gated). The three 2026-09-10 platform criticals (generated tenant boundary, `CorrelationId` `Location` fallback, silent append loss) are resolved at requirement level with truthful FAIL gates and named owners (Stories 2.14, 2.15, 4.16), but the code is unchanged (`RestApiControllerEmitter.cs:281` and `:407-445`; shared `statestore`).
- **Still open, correctly fail-closed:** OQ8 normative bytes (G-OQ8, OR11, Story 4.17); the NFR8 projection bound (G-NFR8, OR16, Story 6.3); the unapproved planning baseline (G-BASELINE); MVP priority still follows the epic inventory; FR25's mutable `@main` workflow governance (OR18 still non-blocking; sharpened by Platform contract M9). Partially resolved: the bound reject result (frontmatter binds the 2026-10-06 assessment while §0 still cites 2026-09-10) and ownership-versus-delivery traceability (G-MVP-COVERAGE manifest specified but unbuilt).
- **Worse:** the PRD as a volatile status ledger (583 → 770 lines, with lifecycle state now inside NFR8 and NFR18; raised to high); the register that documents the baseline is itself stale for every artifact, and PRD/`epics.md` text drift is 18 of 56; 15 post-seal commits changed the PRD with no reviewer gate, several under unrelated commit subjects.
- **New:** introduced by the 2026-10-07 edits — the NFR1 UI-host anonymous class against AD-16, the unrepresentable UI-host carve-out, OR30 outside the gate contract with "release" undefined, and the seal that "never blocks `main`" (Adversarial general C2); introduced by the 2026-09-26 roster-dependent control — no trusted clock or subject boundary, no classification floor, an open role vocabulary; introduced silently on 2026-09-21 — SM11 counting class (e); surfaced by code inspection — gateway actor callbacks (Platform contract C1), the gateway correlation lookup (Platform contract C2, a new gateway-side instance of the 2026-09-10 `CorrelationId` critical that G-STATUS-ID does not cover), the grant-scope rule, the idempotency channel, NFR3 against AD-36, dead-letter CloudEvent `id`, and the projection-backed `304`.
- **Net:** 2026-09-10 recorded 5 critical, 10 high, 4 medium and 0 low (rubric plus platform contract). This run records 0 critical, 7 high, 7 medium and 5 low from the rubric alone, and 4 critical, 21 high, 30 medium and 13 low across all four reviewers after deduplication. None of the four criticals is a carry-over; every earlier PRD-content defect has been addressed in the text, and every remaining rubric high is ledger drift or an inconsistency introduced by the 2026-10-07 edits.

## Mechanical notes

- **IDs:** FR1–FR37 (in thematic rather than numeric order, as before), NFR1–NFR19, UJ1–UJ5, SM1–SM12 and SM-C1–SM-C5 are unique and contiguous. §7.1 clause IDs are contiguous within each parent. OR1–OR30 are complete, with OR2, OR3 and OR12 retired and documented (line 760).
- **§11.4 inventory:** G-HIGH-RISK's enumerated gate list matches the 17 mandatory rows exactly. Every blocking OR is referenced by at least one gate row except OR17 (a meta item, acceptable) and OR30 (see Decision-readiness).
- **Range annotations:** §11.2's "literal range endpoint" vs "range-only interior" annotations are consistent with Story 8.1 ("NFR1–NFR4"), because they classify the NFR range, not the story range. The column header does not say so.
- **§10 heading:** "Planning-recovery metrics (achieved)" contains SM3, which is marked "not met".
- **Source artifacts:** every frontmatter `source_artifacts` path exists. `sprint-change-proposal-2026-10-07-story-9-2-nfr1.md` (approved, edits `epics.md` only, "No PRD edit is proposed") is not listed. That is acceptable, but listing it would record that the NFR1 propagation happened.
- **Assumptions Index:** it round-trips, since there are no inline `[ASSUMPTION]` tags. See Scope honesty for the imported AD-26 assumption.
- **Line length:** two lines exceed 2,000 characters: the Assurance Control glossary entry (line 201, 2,266) and OR15 (line 725, 2,174). They are hard to diff and review.
- **Terminology:** "Stories 22.7a-d" (line 504) is legacy numbering explained inline, which is fine. For "INDEPENDENT" used as a status label, see Decision-readiness.

## Reviewer files

- `review-rubric-validate-2026-10-07.md`
- `review-brownfield-truth-validate-2026-10-07.md`
- `review-adversarial-general-validate-2026-10-07.md`
- `review-platform-contract-validate-2026-10-07.md`
