# Adversarial General Review (Validate): EventStore Phase 4 PRD, 2026-10-07

- **PRD:** `_bmad-output/planning-artifacts/prd.md` (HEAD `40c92e08`, SHA-256 `d5632ba71c838ba7f0b8ca61a24889cb21edb4506531e823d8c0dfb12b7e6ae6`, frontmatter `updated: 2026-10-07`)
- **Addendum:** none
- **Reviewer stance:** adversarial, document logic first; repository greps used only to substantiate.
- **Out of scope by owner decision:** the `single-maintainer-attested` level itself. The absence of a second human is not counted as a defect anywhere below.

## Verdict

**REJECT as gate authority; usable only as a requirements inventory.** The PRD is candid that readiness is FAIL, but the machinery it defines for leaving FAIL does not hold together. §0 forbids every handoff that lacks a validator, and only a forbidden handoff can build that validator. The only way out is a "bootstrap rule" that lives in `epics.md`, which the PRD never sanctions. The single-maintainer seal, amended on 2026-10-07 to "never block `main`", now cannot block the transitions it names, because every one of them is a commit to `main`. Its "required run" is required only by the validator it is supposed to seal, and G-HIGH-RISK seals itself. The 2026-10-07 NFR1 and §9.2 edits cite AD-16 as the basis for an anonymous class that AD-16 (`[ADOPTED]`) forbids. They define a callback class that no OIDC callback can satisfy, and they turn the UI-host carve-out into either a permanent G-MVP-COVERAGE deadlock or an unstated waiver of fail-closed on the Admin UI. SM11 counts NFR7 class (e) as "delivered" on source-only evidence, which NFR7's own text and §1.1 forbid. "Release" is never defined, so §0 ("authorizes no … release") and OR30 ("Blocking for the next release") cannot both be true, and fourteen packages have already shipped. Downstream authors following this text will build different seals, different anonymous-endpoint inventories, and different release controls.

## Findings

### Critical (2)

#### C1. The path out of FAIL is circular, and its only escape is an owner note outside the PRD

- **Location:** §0 (lines 110, 112); §11.4 preamble (line 672); OR28 (line 742). Cross-reference: `epics.md` Story 9.1 "Bootstrap rule" (line 7531).
- **Quoted:** "This PRD therefore authorizes no `READY` verdict, … or general dependent implementation handoff until every mandatory gate passes" (l.110). "Before handoff, that exception requires … a passing `python3 tools/validate-corrective-work-authorization.py`" (l.112). "Until this exists, no gate-closing implementation handoff is authorized by the §0 exception" (l.742). "no mandatory row permits deferral, risk text, or owner assertion to substitute for proof" (l.672).
- **Why it breaks:**
  - Every corrective story (2.14, 2.15, 3.18-3.20, 4.16, 4.17, 5.11, 5.14, 9.2-9.5) needs a §0 record checked by a validator that Story 9.1 builds.
  - Story 9.1 cannot be handed off under §0, because its validator does not yet exist, and it cannot be handed off under general handoff either, because general handoff is barred until every gate passes.
  - The deadlock is broken only in `epics.md` ("a dated owner authorization … is recorded in the story before development starts"). That is an owner assertion with no Assurance Control, no 24-hour separation, and no seal, which is exactly what l.672 forbids. The PRD never authorizes it.
  - "General dependent implementation handoff" is undefined. The repository shows Story 6.6 `in-progress`, Story 5.5 `review`, and Story 8.3 `in-progress`, while `evidence/corrective-work-authorizations/` does not exist. Either those handoffs fall outside the prohibition, whose boundary is then unstated, or the prohibition is not being followed. A downstream author cannot tell which.
- **Fix:**
  - Add a PRD-level bootstrap clause that names Story 9.1 as the single bootstrap exception, its exact allowed paths, and the evidence it must leave. State that the first sealed transition re-validates Story 9.1 after the fact.
  - Define "dependent implementation handoff" as an enumerable class, for example "work whose acceptance consumes a failed gate's subject", and state explicitly that non-dependent work may proceed.

#### C2. The amended seal cannot block its own guarded transitions; "required run" is circular; G-HIGH-RISK seals itself

- **Location:** Glossary "Assurance Control" item (1) (line 201); OR10 amendment (line 713); OR13 (line 715); G-HIGH-RISK (line 690).
- **Quoted:** "that run blocks only its transition (a high-risk gate result to PASS, a story recorded against a high-risk NFR to `done`, readiness, `release-available`, `production-promoted`, or consumer removal) and never `main`" (l.201). "a seal belongs to one guarded transition and never blocks `main`" (l.713). "status-transition enforcement required before a story may be recorded `done` against a high-risk NFR" (l.715).
- **Why it breaks:**
  - **The transitions are commits to `main`.** A gate result, a tracker `done`, a readiness report, and an authority record are all files on `main`. A seal that never blocks `main` cannot stop any of them from landing. OR13 asks for enforcement before a story "may be recorded `done`", and the amended OR10 makes that impossible. The result is a tracker full of unsealed `done` values that are formally "ineffective" but indistinguishable from sealed ones. Those are the values every loop and sprint tool reads.
  - **"Required run" is circular.** The PRD never defines a "required run". `epics.md` (Story 9.2) defines it as "enforcement by the guarded transition's validator". So the seal is a run that the validator decides to require, of a workflow whose digest the matrix (written by the same author) pins. That validator is the code being sealed. Nothing outside the author's own commits fixes which validator or workflow bytes count. Validator, workflow, and subject can change in one push and then seal each other.
  - **G-HIGH-RISK is in its own matrix.** Moving it to PASS is itself a guarded transition, sealed by the validator whose correctness G-HIGH-RISK certifies. No bootstrap rule is given.
  - **Legacy `done` labels are unaddressed.** Labels set before the seal existed (5.1, 5.3, 4.9-4.15 against NFR3, NFR7, and NFR16) are neither grandfathered nor required to re-transition. Two teams will differ.
- **Fix:**
  - Define a guarded transition as a record that is void unless it carries a seal reference, and require G-BASELINE and G-MVP-COVERAGE to treat an unsealed high-risk `done` as an error, not as "ineffective".
  - Pin the seal workflow and validator to a predecessor commit that predates the subject, so that a validator change cannot seal its own subject.
  - Add an explicit G-HIGH-RISK bootstrap, for example a first seal re-run by the next transition with the validator pinned to a commit before the subject.
  - State how pre-existing high-risk `done` labels are treated.

### High (9)

#### H1. The new NFR1 anonymous class cites a decision that forbids it, cannot be satisfied by an OIDC callback, cannot be enumerated by the stated test, and has no owner

- **Location:** NFR1 (line 361); §11.2 NFR1 row (line 609); G-AUTH-HOSTS (line 686). Cross-reference: `architecture.md` AD-16 (lines 240-244); `epics.md` Story 5.14.
- **Quoted:** "on interactive UI hosts only, an enumerated set of static framework assets and authentication-protocol callback endpoints that carry no tenant, operational, or user data, each explicitly pinned `AllowAnonymous`, support-safe, and enumerated by endpoint-metadata tests (AD-16)".
- **Why it breaks:**
  - **The citation is false.** AD-16 is `[ADOPTED]` and says "Only `/health`, `/alive`, and `/ready` carry explicit `AllowAnonymous` metadata … Any future anonymous asset or callback first requires a governed PRD change." AD-16 has not been amended. Story 5.14 says UI hosts "are reported as failing conformance … this story adds no exemption".
  - **The class may be empty.** An OIDC callback (`signin-oidc`) receives an authorization code and/or an `id_token`, and an `id_token` is user data. Read literally, no callback qualifies. If teams ignore that condition, the class is unbounded.
  - **The test method misses assets.** Static assets served by static-file middleware are not endpoints and carry no endpoint metadata, so "enumerated by endpoint-metadata tests" cannot see them. The asset half of the class can grow without any test failing.
  - **No owner.** §11.2 gives Story 5.14 only "the AD-16 authenticated-fallback and Dapr framework-route slice". No story owns the UI-host enumeration, and G-NFR-OWNERSHIP does not list NFR1.
  - **G-AUTH-HOSTS governs NFR1 but does not test it.** It names NFR1-NFR3, yet its evidence is JWT-only. Nothing in the gate tests fallback or anonymous enumeration.
- **Fix:**
  - Drop the "(AD-16)" citation until AD-16 is amended.
  - Redefine the callback class as "endpoints whose responses disclose no data beyond the protocol exchange", and name the protocol endpoints.
  - Require the inventory to cover middleware-served paths as well as endpoints, for example an HTTP-level crawl of anonymous 2xx responses.
  - Assign a primary story, and add fallback and anonymous-enumeration evidence to a gate.

#### H2. "NFR1 conformance moves with them" either deadlocks MVP coverage or silently waives admin-surface fail-closed

- **Location:** §9.1 (line 490); §9.2 (line 507); SM10 (line 543); G-MVP-COVERAGE (line 693); §13 (line 770).
- **Quoted:** "Phase 4 MVP delivery is total over FR1-FR36, NFR1-NFR18" (l.490). "Until then they stay outside the canonical MVP production profile, and their NFR1 conformance moves with them" (l.507). "Interactive UI-host surfaces outside the MVP production profile are listed with that scope reason and never counted as covered" (l.543). "`N/A` is forbidden for MVP IDs" (l.693).
- **Why it breaks:**
  - **Two readings, both bad.** If UI-host surfaces stay in the `nfr1_surfaces` denominator but are "never counted as covered", SM10 can never reach 100% and NFR1 can never pass while login is post-MVP. That is the Option-B deadlock the owner tried to avoid. If they leave the denominator, that is an `N/A` in everything but name, which G-MVP-COVERAGE forbids.
  - **"Moves with them" has no defined meaning.** NFR1 is not profile-scoped. The routing proposal records that the Admin UI and Sample UI "map their pages with no policy" today. The Admin UI is an admin surface, which NFR1 says must fail closed. The text reads as a waiver for every non-production deployment, including shared and staging environments, and it stacks with H6.
  - **The re-entry criterion is undefined.** No story, gate, or evidence defines "before human interactive login exists".
  - **§13 understates the scope cut.** It calls this "one scope reduction", but the cut removes every interactive UI host from production, including the Admin shell (FR34-C11) and the Tenants UI that UJ3 depends on.
- **Fix:**
  - State whether UI-host surfaces are in or out of the denominator.
  - If out, record a bounded, reason-coded MVP exclusion that G-MVP-COVERAGE accepts, and say so in §9.1.
  - State the minimum posture for UI hosts outside production, for example network-isolated or authenticated by reverse proxy.
  - Bind re-entry to an AD-26 profile change plus a named login-evidence gate.

#### H3. SM11 counts NFR7 class (e) as delivered on evidence that NFR7 and §1.1 both reject

- **Location:** SM11 (line 544); NFR7 (line 367); §1.1 (line 133); G-OQ8 (line 679).
- **Quoted:** "Currently four of five are delivered: … class (e) duplicate side effects has implementation and closed source-only platform evidence from Stories 4.9-4.15" (l.544). From NFR7: "A loss class is delivered only when … production-path evidence proves that the guard or recovery prevents the loss" (l.367). From §1.1: "No OQ8 closure claim under FR27, NFR7, or NFR16 is authoritative" (l.133).
- **Why it breaks:** "Source-only" is by definition not production-path evidence; see the glossary entry "Production Path". The OQ8 governing bytes are unavailable and G-OQ8 fails. SM11 therefore tells downstream readers that only class (c) is outstanding, which steers Story 4.16 and the readiness reviewer toward a 4/5 baseline that does not exist. This is the C2 pattern from the prior review (paperwork scored as a guard) reappearing in a different class.
- **Fix:** Restate SM11 as 3/5. Record class (e) as "implemented, source-only; production-path proof and G-OQ8 outstanding", and tie its delivery to G-OQ8 plus a production-path receipt.

#### H4. The time-separated attestation has no trusted clock, no authentication mechanism, and no subject boundary

- **Location:** Glossary "Assurance Control" item (2) (line 201); §11.4 invalidation rule (line 672); G-BASELINE (line 678).
- **Quoted:** "an authenticated owner approval bound to the subject digest and created at least 24 hours after the last authored change to that subject".
- **Why it breaks:**
  - **No trusted clock.** "Last authored change" has no time source. Git author and committer dates are author-controlled and can be backdated. `architecture.md` fixes a trusted clock (repository push-activity time) only for AD-26 records. For every other subject, a solo maintainer can satisfy the 24-hour window instantly.
  - **No authentication mechanism.** "Authenticated" is not tied to anything: a signed commit, a platform review, or the CI identity.
  - **No subject boundary.** "Subject" is undefined per transition. For `READY`, the subject is the G-BASELINE manifest, which binds `sprint-status.yaml`, and concurrent loops edit that file continuously. Since "any governed … change invalidates", `READY` needs a 24-hour freeze of every bound artifact. The PRD never says so, which leaves the path out of FAIL ambiguous.
- **Fix:**
  - Define the clock as the platform push-activity time of the push that last changed the subject digest, for every subject.
  - Define the attestation carrier, for example a signed tag or record verified by the sealed run.
  - Define the subject per guarded-transition type.
  - State the freeze or exclusion rule for volatile inputs such as the tracker.

#### H5. G-HIGH-RISK lets the author classify any gate out of the Assurance Control, and the level for "standard" rows is undefined

- **Location:** §11.4 preamble (line 672); G-HIGH-RISK (line 690); per-row evaluator cells (lines 680-689, 693).
- **Quoted:** "Each entry is `high-risk` or a reason-coded `standard-control`; standard classification is not omission or waiver" (l.690). "Every passing row must retain … approval at its required assurance level …; `READY` carries the lowest assurance level of its inputs" (l.672).
- **Why it breaks:**
  - **No floor on classification.** The PRD does not say which gates must be high-risk. The matrix author can reason-code G-TENANT as `standard-control`, yet the G-TENANT row demands "approval at the G-HIGH-RISK Assurance Control level". The row and the matrix can disagree, and the PRD gives no precedence.
  - **No level for standard rows.** Rows such as G-BASELINE, G-CLAUSE, G-NFR8, G-NFR18, G-NFR-OWNERSHIP, and G-READINESS name no level. "Lowest level of its inputs" is undefined once one input has no level.
- **Fix:** Fix the high-risk set in the PRD, at minimum every row that already says "G-HIGH-RISK Assurance Control level". Give `standard-control` an explicit assurance level, and make the matrix validator reject any classification below a row's PRD-declared level.

#### H6. The NFR3 break-glass keys on environment names, so any production deployment not literally named "Production" can run symmetric keys

- **Location:** NFR3 (line 363); Glossary "Break-glass" (line 184).
- **Quoted:** "Production must always reject symmetric-key mode … the break-glass option admits symmetric mode only in environments that are neither Development nor Production". From the glossary: "which never reaches Production".
- **Why it breaks:** "Production" is an environment-name string. A real deployment named `Staging`, `prod`, `production-eu`, or `Live` is "neither Development nor Production", so `AllowInsecureSymmetricKey` admits HS256 there. Development also admits symmetric mode without break-glass, so a misnamed environment fails open twice. NFR4's "non-Development fallback" has the same name-based shape.
- **Fix:** Define production by the canonical profile and deployment identity (AD-26 or G-PUBLICATION-AUTH), not by environment name. Permit break-glass only on a closed allowlist of non-production environment identities, and reject unknown names.

#### H7. "Release" is undefined, so §0, NFR12, G-COMPAT, and OR30 contradict each other

- **Location:** §0 (line 110); NFR12 (line 372); G-COMPAT (line 685); OR30 (line 744); Glossary "Publication Lifecycle" (line 186).
- **Quoted:** "authorizes no … release … until every mandatory gate passes" (l.110). "Release must fail unless API and wire baselines plus representative package-only consumers pass" (l.372). "Fourteen release packages exist, but … an enforcing release gate [is] absent" (l.685). "Blocking for the next release" (l.744).
- **Why it breaks:**
  - The PRD uses "release" for NuGet or OCI publication and also for the `release-available` lifecycle state.
  - If §0 bars publication, OR30's trigger can never arrive before readiness, and the fourteen shipped packages contradict §0.
  - If publication continues, which the operator-dispatched `release.yml` allows, including its `bypass-validation` input that substitutes commitlint proof for CI proof, then §0 bars only `release-available`. In that case NFR12's "must fail" is false today and nothing in the PRD stops the next publication.
  - Two release owners would act oppositely.
- **Fix:** Define "package publication" and "`release-available`" as separate terms. State which gates block publication now. Either make NFR12 and OR30 publication blockers with an enforcing lane, or say explicitly that publication continues ungated until G-COMPAT, and name the risk owner.

#### H8. Licensing is handled by one ungated row while §8.1 keeps pulling in the next licence change

- **Location:** §8.1 catalog-currency bullet (line 444); OR30 (line 744); NFR11 and NFR12 (lines 371-372).
- **Quoted:** "Keep the shared Builds catalog on the latest validated compatible versions … never downgrade because search omits or unlists a package" (l.444). From OR30: "Record a commercial license, accept the RPL-1.5 obligations after review, or replace or re-pin the dependency".
- **Why it breaks:**
  - **The policy caused the problem.** The latest-version mandate has no licence criterion in "validated compatible", which is how MediatR `14.2.0` (RPL-1.5 or commercial) entered the catalog.
  - **OR30 is ungated.** No §11.4 row, no G-HIGH-RISK matrix entry, and no release lane enforces it.
  - **The re-pin option conflicts with §8.1.** "Re-pin" to a pre-commercial version is a downgrade, which §8.1 forbids unless it is documented as an exception, and OR30 does not mention that route.
  - **Scope gaps.** OR30 ignores already-published packages, consumers that inherit the dependency (Tenants retains MediatR composition per SM8 and DW-64), and the licence-notice suppression itself (`"LuckyPennySoftware.MediatR.License": "None"` in `src/Hexalith.EventStore/appsettings.json`).
  - **No supply-chain requirement exists.** There is no licence allowlist, SBOM, or provenance or signing requirement.
- **Fix:** Add an NFR, with a gate, for dependency licence compliance: an allowlist checked on catalog changes and on publication. Make licence change an explicit criterion in §8.1. Route OR30's re-pin option through the §8.1 exception mechanism. Cover inherited consumers and already-published versions.

#### H9. Several gates let the work being gated choose its own pass condition

- **Location:** G-TENANT (line 680); G-STATUS-ID (line 681); G-NFR8 and NFR8 (lines 684, 368); G-BASELINE and G-CLAUSE (lines 678, 683).
- **Quoted:** "Exact CI lane/command must be bound by that story" (G-TENANT and G-STATUS-ID). "the dependent validation command must be recorded in that spec" (G-NFR8). "No numeric value is inferred by this PRD" (NFR8). "The exact baseline validator lane/command is not yet defined" (G-BASELINE).
- **Why it breaks:** Stories 2.14 and 2.15 define the command that decides whether 2.14 and 2.15 pass. Story 6.3 sets the projection-cost budget that 6.4 must meet, with no PRD floor, so the budget can be written after the implementation's measured cost is known. Under one maintainer, the evaluator, the author, and the criterion-setter are the same identity, and only the 24-hour attestation separates them (see H4).
- **Fix:** Have the PRD fix minimum pass conditions, for example NFR8 bounds relative to stream length and page size. Require the gating command to be pinned in a predecessor record before the gated implementation starts, with its digest bound by G-HIGH-RISK.

### Medium (14)

#### M1. Ownership tables contradict each other on NFR17, NFR5, and NFR18

- **Location:** §7.1 NFR17-C1 to C5 (lines 431-435); §11.2 NFR17 row (line 625); §11.2.1 row 2 (line 634); §11.2 NFR5 row (line 613); G-NFR18 (line 691) versus NFR18 (line 378).
- **Quoted and why:**
  - **NFR17:** §7.1 makes 7.6, 7.8, and 7.9 the primary owners of NFR17-C1, C3, and C4. §11.2 lists those same stories as supporting only, and lists 3.14, 5.6, and 5.8 as primary even though they own no NFR17 clause.
  - **NFR5:** §11.2.1 says "NFR5's primary-owner gap remains open", while §11.2 assigns Story 2.16.
  - **NFR18:** G-NFR18 says "Document, owner, evidence, and validation command are absent", while NFR18 and the same row name Story 6.7.
  - G-CLAUSE and OR8 compare exactly these tables, so the PRD fails its own drift guard.
- **Fix:** Regenerate §11.2 primary columns from §7.1 for clause-bearing NFRs, and correct the stale §11.2.1 and G-NFR18 text.

#### M2. The §11.3 baseline register is stale on the day of the edit and carries no dates

- **Location:** §11.3 rows for `architecture.md`, `ux.md`, and `epics.md` (lines 647-650); Epic 8 bullet (line 658).
- **Quoted:**
  - `architecture.md` "SHA-256 `7e3dbc7b…`": HEAD is `7fd805a8…`.
  - `epics.md` "`d067c8fb…`": HEAD is `0697679b…`.
  - `ux.md` "`2927f97d…`": HEAD is `23b17217…`.
  - DESIGN "current `3f4f0181…`": HEAD is `7b743e79…`.
  - EXPERIENCE "current `11f75403…`": HEAD is `3985789a…`.
  - "Story 8.2 is authorized but still `backlog`": the tracker records 8.2 as `done` and 8.3 as `in-progress`.
  - The column header says "Identity/status at stated date", but these rows state no date.
- **Why it breaks:** G-BASELINE and OR14 treat this register as the drift baseline, so a reader cannot tell a stale observation from a current one. This is prior M1 (PRD as a status mirror), now worse.
- **Fix:** Date every row, or move the register into the Story 9.3 manifest and cite it by digest.

#### M3. Story 6.2 authority is revoked only in prose, while the machine-readable flag still says authorized

- **Location:** NFR8 (line 368); OR5 (line 721).
- **Quoted:** "its `approved-authorized` frontmatter is superseded until review patch P-D1 lands".
- **Why it breaks:** `spec-folded-snapshot.md` still carries `status: approved-authorized` and `story_6_2_authorized: true`, and its body still says "**AUTHORIZED**". Any loop or validator that parses the flag will start Story 6.2. NFR8 also still pins the pre-reopen digest `0b456b5f…` as "binding".
- **Fix:** Require P-D1, or an interim flag flip, before the PRD claims supersession. Remove "binding" from the reopened digest.

#### M4. The glossary declares a production profile "currently authorizing" in the same entry that says none is, and treats an `[ASSUMPTION]` as binding

- **Location:** Glossary "Canonical Production Profile Inventory" (line 188); §11.3 architecture row (line 648); G-PUBLICATION-AUTH (line 688).
- **Quoted:** "AD-26 defines exactly one currently authorizing production profile … The file is currently absent, so no profile is authorizing" (l.188). From the architecture row: "AD-26 remains `[ASSUMPTION]`" (l.648).
- **Why it breaks:** The entry contradicts itself, and it presents an unratified decision as defining the inventory that G-PUBLICATION-AUTH and G-CONSUMER bind.
- **Fix:** Reword the entry as "AD-26, once ratified, will define …", and make the gate rows depend on AD-26 ratification explicitly.

#### M5. Guarded wording labels the solo control "INDEPENDENT", and a test locks the mislabel in

- **Location:** §6.8 bullet (line 342); G-RUNTIME-PARITY result (line 687).
- **Quoted:** "TECHNICALLY VALIDATED; INDEPENDENT CONTROL OPEN". "TECHNICAL PASS; INDEPENDENT GATE BLOCKED".
- **Why it breaks:** With a one-human registry, the required level is `single-maintainer-attested`. Labelling the open control "independent" tells readers a second human is required, which contradicts the 2026-09-26 owner decision that the glossary records. `CorrectedDeployedRuntimeParityClosureTests.cs:4593` asserts the wrong wording.
- **Fix:** Change the wording to "ASSURANCE CONTROL OPEN" or "ASSURANCE GATE BLOCKED", and update the guard test in the same change.

#### M6. SM3 and OR13 measure "high-risk NFR" coverage at clause level, but most of those NFRs have no clauses

- **Location:** SM3 (line 527); §11.2 preamble (line 603); OR13 (line 715); §7.1 (lines 385-435).
- **Quoted:** "High-risk NFRs NFR1-NFR4, NFR7, NFR10-NFR11, and NFR14-NFR17 must map to complete clause-level primary coverage."
- **Why it breaks:**
  - §7.1 defines clauses only for NFR17. For the other high-risk NFRs, "complete clause-level coverage" has no denominator, so SM3 cannot be measured.
  - The PRD has two "high-risk" universes: the SM3 NFR list and the G-HIGH-RISK gate classification. OR13's "a story recorded against a high-risk NFR" does not say which one applies, or whether a supporting declaration counts.
- **Fix:** Either add clauses for the high-risk NFRs or restate SM3 at requirement level. Define "recorded against" as primary or supporting.

#### M7. "ULID-safe" is undefined and conflicts with the preserved v1 MessageId grammar

- **Location:** FR33 (line 313); Glossary "MessageId Contract Version" (line 198); "Aggregate Identity" (line 182); §8.2 (line 452).
- **Quoted:** "reject an event whose metadata identity components are absent or not ULID-safe rather than accepting it" (FR33). From the glossary: "Version 1 is the preserved legacy grammar: 1-128 ASCII alphanumeric or hyphen characters" and "ULID semantics where applicable".
- **Why it breaks:** One team reads "ULID-safe" as "is a ULID" and rejects every event whose identity carries a v1-derived or non-ULID aggregate ID. That breaks NFR12 v1 compatibility and can make legacy streams unreadable on replay. Another team reads it as "not parsed with `Guid.TryParse`". "Identity components" and the point of rejection (append or replay) are also undefined.
- **Fix:** Define "ULID-safe" per field. State which fields must be canonical ULIDs and which may carry v1 or `AggregateIdentity` strings, and state where rejection occurs.

#### M8. The "Production Path" definition does not require production-profile components

- **Location:** Glossary "Production Path" (line 206), compared with AD-26 (Redis is Development/test only).
- **Quoted:** "the real OS process, DAPR sidecar, durable state component, actor placement, and host pipeline are exercised".
- **Why it breaks:** Evidence gathered on Redis or any other "durable" component qualifies as production-path under the PRD, yet AD-26 says it is non-authorizing. NFR7, NFR16, and SM11 can then be "proven" on a provider the production profile excludes, and the append-race outcome is provider-specific.
- **Fix:** Require production-path packets to declare their component profile, and count only canonical-profile components toward the production-path classes in NFR7 and NFR16.

#### M9. The FR34 IntegrationTests lane only has to run, not pass

- **Location:** FR34 (line 323); FR34-C10 (line 424).
- **Quoted:** "the lane runs on every push to `main` and asserts persisted state for at least the FR23, FR27, and FR30 paths".
- **Why it breaks:** A lane that runs and stays red satisfies the literal text. Nothing requires it to pass or to block any transition.
- **Fix:** Require a passing result on the bound subject as evidence for FR23, FR27, and FR30 closure, and name the transition that the lane blocks.

#### M10. The role vocabulary is not closed, and the roster that sets the assurance level governs itself

- **Location:** Glossary "Owner Roles" (line 200) and "Assurance Control" (line 201).
- **Quoted:** "This PRD distinguishes six" roles, and "Its required level is computed from the owner-role registry".
- **Why it breaks:** Other parts of the PRD and architecture invoke roles the six do not include: architecture owner (FR5, §8.4), test owner and Test Architect, tracker owner, persistence owner, Builds owner, and the Platform deployment role (AD-26). Validators must reject "wrong-role issuers" against a vocabulary that does not exist. The registry is edited by the gated party, nothing makes the required level monotonic, and nothing defines how a human identity is distinguished from a tool account.
- **Fix:** Enumerate the full role set. Make registry edits a sealed, time-separated transition. Forbid lowering the required level for subjects already evaluated.

#### M11. "Or" escape hatches create alternative conformance paths with no selection rule

- **Location:** FR4 compared with FR4-C5 (lines 239, 391); FR16 (line 260); FR30 (line 290); FR12 (line 256).
- **Quoted and why:**
  - FR4 allows a "lossless lifecycle representation or owner-approved mapping", while FR4-C5 requires preserving "all six lifecycle states". A lossy mapping approved by the owner satisfies FR4 and violates its own clause.
  - FR16's DAPR path is required "only where a subscriber cannot hold a SignalR connection", and no one is named to determine that. The condition is vacuous.
  - FR30 says "complete their publication, drain them, or recover them". "Drain" can be read as discarding events, which is NFR7 class (d) loss.
  - FR12 says "omit `Location` or cause a safe rejection", which leaves two different client contracts.
- **Fix:** Pick one behavior per requirement, or state the selection criterion and who decides it. Remove "drain" or define it as non-lossy.

#### M12. NFR2's lowercase normalization folds non-ASCII lookalikes into valid tenants

- **Location:** NFR2 (line 362).
- **Quoted:** "performs invariant lowercase case normalization before comparison and authorization, then validates the 1-64-character grammar".
- **Why it breaks:** Unicode simple case mapping lowercases, for example, U+212A KELVIN SIGN to ASCII `k` before the ASCII grammar check runs, so distinct raw inputs alias one tenant. Any component that logs, caches, or forwards the raw value diverges. "Whitespace or other characters are not … repaired" is contradicted by the normalization step itself.
- **Fix:** Reject any non-ASCII input before normalizing, then lowercase ASCII only.

#### M13. The FR36 consumer binding names a source SHA for package-mode consumers

- **Location:** FR36 (line 334); FR21 (line 274).
- **Quoted:** "The consumer's checked-out EventStore SHA and deployment profile must match the production-promoted authority."
- **Why it breaks:** FR21 makes package references the default. A consumer's checked-out EventStore submodule SHA is irrelevant to what it runs, so the binding can match while the consumer runs different packages, or fail while it runs the right ones.
- **Fix:** Bind the consumer to the promoted package or OCI identities, keeping the SHA as provenance only.

#### M14. The PRD omits concerns that an event-sourcing platform carries, without excluding them

- **Location:** §5 (lines 216-226), §7, and §9.2.
- **Why it breaks:**
  - **Mixed-version compatibility.** FR33 requires upcasting, but nothing requires forward compatibility or mixed-version readers during rolling deployments, and nothing covers rollback after newer event schemas are written. FR37 requires this for payload protection only.
  - **Recovery targets.** There is no RPO, RTO, or restore-consistency requirement, yet AD-26 and Story 7.22 make restore posture a promotion precondition with no PRD anchor.
  - **Retention.** Event-stream, command-status, dead-letter, and audit-record retention are unstated, and §9.2 excludes only deletion.
  - **Performance and availability.** There are no targets beyond the deferred NFR8.
  - **Quotas.** There are no per-tenant quotas or rate limits; only admin body-size limits exist.
- **Fix:** Add requirements for each, or list each as an explicit exclusion in §9.2.

### Low (5)

#### L1. "Documented crypto-shred boundaries" is unbounded and depends on post-MVP capability

- **Location:** NFR17 (line 377); NFR17-C5 (line 435).
- **Why:** No acceptance content is defined. Crypto-shredding presupposes key-per-subject protection, which ships post-MVP (FR37, Epic 8), while GDPR-1 is out of MVP. It is unclear what the MVP documents.
- **Fix:** Define the required content, or move this to Epic 8 with an explicit MVP statement that crypto-shredding is not supported.

#### L2. Frontmatter finality predates material amendments, and the source list misses a same-day proposal

- **Location:** Frontmatter (lines 3-21, 96).
- **Why:** `status: final` and `prd_finalize_reviewed_head: dd55a6d1` predate the 2026-10-07 NFR1, §9.2, OR10, and OR30 edits. `sprint-change-proposal-2026-10-07-story-9-2-nfr1.md`, which states that "No PRD edit is proposed", is not in `source_artifacts`.
- **Fix:** Record a finalize pass for the 2026-10-07 edits, and add the proposal to `source_artifacts`.

#### L3. NFR5 allows metadata values in logs at Debug level

- **Location:** NFR5 (line 365).
- **Quoted:** "Framework logs must not expose metadata values above Debug level."
- **Why:** Production hosts with Debug enabled will log values. "Above" is also ambiguous in direction.
- **Fix:** Say "at Information or higher", or forbid value logging in non-Development environments.

#### L4. Implementation prescriptions still crowd the PRD

- **Location:** §8.3 (`FluentAccordion`, line 464); FR2 and FR3 method names; NFR17 YAML policy names.
- **Why:** This is prior finding M3, unchanged. Replaceable mechanisms sit beside product contracts without labels that distinguish them.
- **Fix:** As prior M3 and OR9.

#### L5. The NFR19 key-zeroing proof cannot observe what it claims, and may pass vacuously

- **Location:** NFR19 (line 379).
- **Quoted:** "proven by a test that inspects the buffer after disposal".
- **Why:** Copies made by the managed runtime are unobservable. If key operations stay in the Dapr crypto sidecar (§8.4), no in-process key exists and the test passes vacuously.
- **Fix:** Scope the test to in-process derived and unwrapped keys, using pinned or unmanaged buffers, and require a positive control that proves a key was present.

## Severity Counts

| Severity | Count |
| --- | ---: |
| Critical | 2 |
| High | 9 |
| Medium | 14 |
| Low | 5 |
| **Total** | **30** |

## Delta vs prior adversarial review (`review-adversarial-general.md`, 2026-09-09)

| Prior | Status | Note |
| --- | --- | --- |
| C1: OQ8 normative bytes unavailable | **Still open (now gated)** | Honestly fenced by G-OQ8, OR11, and Story 4.17, but not resolved. SM11 (H3) still leans on the unreproducible design. |
| C2: MVP success despite append loss | **Resolved in text** | SM11, SM-C5, G-APPEND, and the §9 safety boundary now refuse deferral credit. The same pattern recurs for class (e); see H3. |
| H1: OR12 traceability defects | **Resolved** | FR1 maps to 1.11; slices are partitioned. New ownership drift appears instead; see M1. |
| H2: Story 6.1 claims false | **Changed form, still open** | 6.1 is reopened, but the machine flag still says authorized; see M3. |
| H3: "Final" conflated with readiness | **Resolved** | Separate `implementation_readiness_*` frontmatter now exists. Residual: finality predates the 2026-10-07 edits (L2). |
| H4: Omnibus requirements not clause-closable | **Partially resolved** | §7.1 clauses exist for 9 IDs. NFR1-NFR16 remain unclaused while SM3 demands clause-level coverage (M6). |
| H5: Self-approval, no correctness gate | **Superseded by owner decision; new defects** | `single-maintainer-attested` is accepted. The replacement control is circular and unenforceable as amended (C2, H4, H5, M10). |
| H6: No exit decision rule | **Resolved structurally** | §11.4 exists. Its entry path is circular (C1), and several pass conditions are self-set (H9). |
| M1: PRD as volatile status mirror | **Still open, worse** | Every §11.3 digest is stale at HEAD, and the Epic 8 statuses are stale (M2). |
| M2: Source authority not normalized | **Partially resolved** | §1 and §11.3 disclaim list-as-approval. A same-day proposal is missing (L2). |
| M3: Implementation prescriptions | **Still open** | L4. |
| L1: Scope arrives late | **Still open** | OR9 deferred. The §9.2 UI-host exclusion is now a material scope cut that is buried (H2). |

**New in this pass:**
- **Introduced by the 2026-10-07 edits:** C2 (the OR10 "never blocks `main`" amendment), H1 and H2 (the NFR1 amendment and the §9.2 UI-host exclusion), and H7 and H8 (OR30).
- **Introduced by the 2026-09-26 roster-dependent control:** H4, H5, M10.
- **In pre-existing text but not raised before:** C1, H3, H6, H9, M4, M5, M7, M8, M9, M11, M12, M13, M14, L1, L3, L5.
- **Continuations of prior findings:** M1, M2, M3, M6, L2, L4.
