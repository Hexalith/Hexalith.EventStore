# Reviewer-Gate Review - Architecture Spine Update 2026-07-19b (Rubric Walker)

- **Artifact:** `_bmad-output/planning-artifacts/architecture.md` (status `final`, updated 2026-07-19)
- **Review focus:** whole document, attacking hardest the 2026-07-19 multi-platform container-release delta (AD-11 container paragraph + NFR16-NFR17 binds, AD-22 deployed-mode identity clarification, Consistency "Release" row, frontmatter sources)
- **Inputs consulted:** `prd.md` (final, 2026-07-19; sections 8.1, 11.2), `sprint-change-proposal-2026-07-19.md` (sections 1-5), `epics.md` (Story 3.12 at line 1902, Story 1.20 at line 1089), repository ground truth (`global.json`, root `Directory.Packages.props`, `references/Hexalith.Builds/Props/Directory.Packages.props`, `tools/release-packages.json`, `src/Hexalith.EventStore.Admin.UI`, `references/Hexalith.FrontComposer` submodule state)

## Verdict

**CHANGES REQUIRED**

The 2026-07-19 delta itself is faithful, well-placed, and internally consistent: it ratifies the PRD-owner-ratified two-platform constraint verbatim, binds the right NFRs, resolves a real identity ambiguity in AD-22, and contradicts no other AD. Judged alone, the delta would pass with strengthening asks (M1, M2).

The whole-document gate fails on one High finding outside the delta: the spine's FrontComposer `3.2.2` literal (AD-21 Rule, Consistency UI row, Stack row) is now contradicted by the authoritative Hexalith.Builds version catalog — which AD-11 itself declares the sole NuGet version authority — currently pinning FrontComposer `4.0.1`, with the `references/Hexalith.FrontComposer` submodule at `v4.0.1`. An invariant Rule that names an exact version the sole version authority has moved past creates two competing version authorities, which is precisely the divergence class the spine exists to prevent.

No Critical findings. AD IDs remain stable and unique (AD-1 through AD-24, no renumbering, no duplicates).

---

## Findings - Critical / High

### H1. AD-21 hard-codes FrontComposer `3.2.2`; the sole version authority is at `4.0.1` [HIGH]

- **Where:** AD-21 Rule ("The UI composes matching `3.2.2` versions of `Hexalith.FrontComposer.Shell` and `Hexalith.FrontComposer.Contracts.UI`"), Consistency Conventions "UI" row ("matching FrontComposer `3.2.2` Shell/Contracts.UI dependencies"), Stack row ("Hexalith.FrontComposer.Shell / Contracts.UI | `3.2.2`").
- **Evidence:** `references/Hexalith.Builds/Props/Directory.Packages.props` line 9 sets `HexalithFrontComposerVersion` = `4.0.1`; the `references/Hexalith.FrontComposer` submodule is at `v4.0.1-78-gc585073c`. EventStore's root `Directory.Packages.props` only configures CPM and imports the catalog — there is no local override, exactly as AD-11 and FR21 require. `src/Hexalith.EventStore.Admin.UI` does not yet reference FrontComposer (the composition is future Story 7.14 work), so nothing at runtime pins `3.2.2` either.
- **Why it matters:** AD-11's Rule makes the Builds catalog the single source-owned version authority that "moves to latest validated compatible versions." AD-21's Rule freezes an exact version in invariant text. At HEAD these two ADs give a Story 7.14 implementer contradictory instructions: compose `3.2.2` (AD-21) or compose what the catalog resolves (`4.0.1`, AD-11/FR21). Two units following different ADs diverge — the exact failure mode the good-spine checklist targets. The Stack preamble's Story 3.11 refresh mechanism covers "version rows" in the Stack table only; it does not authorize rewriting AD-21's Rule literal or the Consistency row, so the drift has no self-healing path.
- **Required change:** restate AD-21 and the UI Consistency row to bind "matching Shell and Contracts.UI versions resolved from the Builds catalog (AD-11), with Debug source and Release package modes resolving the same package boundary" — the invariant is *matching versions and one boundary*, not a literal. If a specific version pin is genuinely load-bearing for Story 7.14 evidence, it belongs in the Stack table under Story 3.11's refresh discipline, not in an AD Rule.

## Findings - Medium

### M1. AD-11's explicit fail-closed clause is narrower than the ratified gate [MEDIUM]

- **Where:** AD-11 container paragraph: "Any other manifest shape or platform set fails the release evidence gate, closed."
- **Issue:** the enumerated fail-closed trigger covers only manifest shape and platform set. The preceding sentence lists four more validation steps — raw-bytes digest binding, descriptor resolution, per-child config verification, platform smoke — whose gate consequence on failure is unstated at spine level. Story 3.12's AC is stricter than the spine: "a single-platform manifest, wrong media type, missing/duplicate/extra platform, config mismatch, unresolved child, or digest mismatch fails the release evidence gate." A level-below implementer reading only AD-11 could defensibly report a digest-binding mismatch or a product smoke failure as a warning on an otherwise "conforming shape" release. The spine must be at least as strong as the story it governs, or the story's guarantee is story-local and can be renegotiated per-story.
- **Also unstated:** the gate outcome for emulation/setup failure. "Reported separately and is never recorded as a product pass" fixes classification but not disposition — does an un-smokeable child block the release evidence gate (release fails), or does the release complete and merely stay non-authorizing for Story 1.20? Both readings are defensible; the divergence point is real (a CI implementer vs. a release owner will pick opposite defaults).
- **Required change:** one sentence: every enumerated validation step failure — digest binding, descriptor resolution, config mismatch, product smoke — fails the release evidence gate closed; emulation/setup failure is classified separately but still leaves the gate unpassed (no pass without both platform smokes).

### M2. The release container inventory is a silent dimension [MEDIUM]

- **Where:** AD-11 / Consistency "Release" row, versus AD-21 and Story 3.12.
- **Issue:** the spine pins the package inventory precisely ("The inventory remains 14 packages until Story 8.2..."), but no spine-level statement fixes the released *container* set. AD-11 says "The EventStore container release" (definite singular, by implication), and Story 3.12's AC states "only `eventstore` is published as a container" — but that guarantee has no invariant backstop. Meanwhile AD-21 assigns `eventstore-admin-ui` a "container identity," which is a topology identity (AppHost resource), not a registry release artifact — the spine never says so. A level-below team could add an `eventstore-admin-ui` registry publication without violating any AD as written, while violating Story 3.12's exclusion. Related: the container repository/registry identity itself (`registry.hexalith.com/eventstore` in the proposal's evidence) is neither decided, deferred, nor flagged as an open question anywhere in the spine — the one operational-envelope gap the delta introduces.
- **Required change:** add to AD-11 (or the Release row): the release container inventory is exactly one mapping, `eventstore`; AD-21's `eventstore-admin-ui` identity is AppHost/topology-scoped and is not a released registry artifact; container repository identity is owned by the release/deployment configuration (decide or explicitly defer it).

### M3. Stack table drift against the live catalog at HEAD [MEDIUM]

- **Where:** Stack table, verified against `references/Hexalith.Builds/Props/Directory.Packages.props` at HEAD.
- **Stale rows:** NSubstitute `6.0.0-rc.1` (catalog: `6.0.0` stable); OpenTelemetry exporter/hosting/ASP.NET/HTTP `1.16.0` and runtime instrumentation `1.15.1` (catalog: `1.17.0`); CommunityToolkit.Aspire.Hosting.Dapr `13.4.0-preview.1.260602-0230` (catalog: `13.4.1-beta.686`); ASP.NET Core repository seed `10.0.9` (catalog pins the `10.0.10` family, matching the row's own "required security baseline `10.0.10`"); Hexalith.Commons.UniqueIds `2.28.1` (catalog: `2.28.2`); plus H1's FrontComposer row.
- **Verified current:** .NET SDK `10.0.302`/`rollForward: latestPatch` (matches `global.json`), Aspire.Hosting `13.4.6`, Keycloak/Kubernetes `13.4.6-preview.1.26319.6`, Dapr .NET SDK `1.18.4`, MediatR `14.2.0`, FluentValidation `12.1.1`, Microsoft.CodeAnalysis `5.6.0`, FluentUI `5.0.0-rc.4-26180.1`, xUnit v3 `3.2.2`, Shouldly `4.3.0`.
- **Mitigation acknowledged:** the Stack preamble delegates version-row refresh to Story 3.11 "only from accepted shared-catalog and compatibility evidence," and the 3.11→3.5→1.20 refresh chain is an accepted mechanism. The drift is therefore process-covered, but "named tech is verified-current" fails at HEAD for six rows, and one of them (ASP.NET seed `10.0.9`) reads as the seed sitting *below* its own stated security baseline — actively misleading. Recommend triggering the Story 3.11 refresh (or annotating the rows as pending-refresh) rather than leaving `final` status over stale rows.

## Findings - Low

### L1. Instance evidence (`v3.75.0`) embedded in AD-11 Rule text [LOW]

Naming the specific failed release inside an invariant is an altitude wobble — the timeless rule is "published artifacts are immutable; a nonconforming release remains recorded as non-authorizing failed evidence and is corrected only by a conforming later semantic version." The `(v3.75.0)` parenthetical does useful brownfield-ratification work tonight and matches the proposal's disposition exactly, so it is acceptable; prefer moving the instance citation to the Story 1.20 packet reference on the next edit so the Rule does not accrete a version ledger.

### L2. "No variant descriptor" may fail-close a conforming build [LOW]

AD-11 forbids any `variant` descriptor. `linux/arm64` platform entries commonly carry `"variant": "v8"` when indexes are assembled by generic registry tooling (e.g., buildx imagetools), while .NET SDK-emitted descriptors map RIDs without a variant. The constraint is PRD/epics-ratified verbatim, so the spine is correct to carry it — but Story 3.12 implementation must verify the corrected shared publisher emits variant-free arm64 descriptors; if real SDK output includes a variant, the fix is a ratified amendment via change proposal, never an ad hoc validation weakening. Flagged so the implementer discovers this in design, not in a failed corrective release.

### L3. Frontmatter `companions` lists the document itself [LOW]

`companions` contains `_bmad-output/planning-artifacts/architecture.md` — a self-reference. Probably a leftover from a projection/split; harmless but incorrect metadata. Remove or replace with the intended companion.

### L4. Inconsistent `[ADOPTED]` status markers [LOW]

AD-13, AD-14, AD-15, and AD-17 lack the `[ADOPTED]` marker carried by the other twenty ADs. If all twenty-four are adopted (nothing in the document says otherwise), label them uniformly; a status-less AD invites "is this one still proposed?" relitigation.

### L5. `Hexalith.FrontComposer.Contracts.UI` is not pinned in the Builds catalog [LOW]

AD-21 names `Hexalith.FrontComposer.Contracts.UI` as a composed package, but the catalog pins `Contracts`, `Mcp`, `Shell`, `SourceTools`, `Testing` — no `Contracts.UI` entry exists. Before Story 7.14 composition, the pin must be added under catalog authority (Builds-side), or the AD's package name corrected if `Contracts` is the real artifact. Fold into the H1 rework.

### L6. Capability map omits the shared-publisher location [LOW]

The FR17-FR22/FR25 row's "Lives in" (`.github/workflows`, `tools/release-packages.json`, central props, `references/` layout) predates the container paragraph; the container-release rule is now partly enforced in the Hexalith.Builds shared publisher and the EventStore release caller. Add the shared publisher/caller boundary to the row so the map answers "where does the two-platform gate live?"

---

## Delta Verification Record (checks that passed)

1. **PRD fidelity:** AD-11's container paragraph carries PRD 8.1's two ratified bullets exactly — .NET SDK container support (no Dockerfiles), one immutable OCI image index with exactly `linux/amd64` + `linux/arm64`, fail-closed validation on any other manifest shape or platform set. No weakening, no scope creep.
2. **NFR binds:** PRD 11.2 assigns Story 3.12 to NFR9/NFR11/NFR16/NFR17; AD-11 binds all four (NFR9-NFR11 pre-existing, NFR16-NFR17 added). No conflict with other binders: AD-12 keeps NFR16's persisted-evidence discipline (AD-11's raw-registry-bytes rule *instantiates* it); AD-24 owns NFR17's secrets clauses while AD-11 takes its immutable-image clause — complementary split, not a contention.
3. **AD-22 clarification:** the deployed-mode identity ("the validated OCI image index digest; a per-platform child digest counts only by mapping to that index through recorded provenance, never as a substitute identity") *resolves* the ambiguity in Story 1.20's AC phrasing ("the deployed EventStore image digest") rather than contradicting it, and is internally consistent with AD-11's digest-binding rule. This is exactly the divergence-point-fixing a spine should do.
4. **No cross-AD contradiction:** AD-9's "publish targets" remain topology-slice concerns (AppHost/YAML), disjoint from registry release objects; AD-12 is reinforced, not bypassed; AD-21's `eventstore-admin-ui` identity is untouched by the delta (the latent inventory ambiguity is pre-existing — see M2); AD-24's ACA non-conformance and secret rules are unaffected.
5. **Media types verified:** `application/vnd.oci.image.index.v1+json` (OCI index) and the observed failure's `application/vnd.docker.distribution.manifest.v2+json` (Docker v2 manifest) are the correct current identifiers.
6. **Immutability posture:** "immutable non-authorizing failed release v3.75.0, corrected only by a conforming later semantic version" matches proposal sections 1-4 and Story 1.20's fail-closed `approved_*`/`still blocked` discipline; Story 3.12's exclusions (no v3.75.0 mutation, no proof-result approval) are preserved, and the spine correctly leaves approval authority with Story 1.20's owner gates.
7. **Frontmatter sources:** the three new sources (sprint-change-proposal-2026-07-17, -2026-07-18-story-3-1-live-sidecar-topology, -2026-07-19) are present; `updated: 2026-07-19` is correct.
8. **AD ID stability:** AD-1..AD-24, unique, sequential, no renumbering; the delta added paragraphs to existing ADs rather than minting IDs — correct discipline.
9. **Deferred table:** nothing deferred re-opens the platform-set, index-shape, or identity decisions; the container decision is fully decided, not split across Deferred. Package inventory (14, verified against `tools/release-packages.json`) and its Story 8.2 expansion trigger remain coherent with the delta.
10. **Brownfield ratification (delta scope):** the delta ratifies the observed v3.75.0 registry state as failed evidence instead of contradicting or rewriting it — correct brownfield posture.

## Disposition Summary

| # | Severity | Finding | Blocking? |
| --- | --- | --- | --- |
| H1 | High | FrontComposer `3.2.2` literal in AD-21/Consistency/Stack vs. catalog authority at `4.0.1` | Yes |
| M1 | Medium | AD-11 fail-closed enumeration narrower than the ratified gate; emulation-failure disposition unstated | Should fix with delta |
| M2 | Medium | Released container inventory / registry identity dimension silent | Should fix with delta |
| M3 | Medium | Six stale Stack rows at HEAD (process-covered by Story 3.11 refresh) | No, but trigger refresh |
| L1-L6 | Low | Instance evidence in Rule; variant-descriptor risk; companions self-reference; missing `[ADOPTED]` tags; Contracts.UI unpinned; capability-map location gap | No |

Re-gate condition: land H1 (restate AD-21/UI row to bind catalog-resolved matching versions) and the two delta-strengthening sentences (M1, M2). M3 may ride the existing Story 3.11 refresh chain. The 2026-07-19 container-release delta itself needs no retraction.
