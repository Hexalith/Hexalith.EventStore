# Reviewer-Gate Re-Review - Architecture Spine Update 2026-07-19b (Rubric Walker)

- **Artifact:** `_bmad-output/planning-artifacts/architecture.md` (status `final`, updated 2026-07-19)
- **Prior review:** `review-update-2026-07-19b-rubric-walker.md` (verdict CHANGES REQUIRED: H1, M1-M3, L1-L6)
- **Re-review scope:** closure of every prior finding against the current spine text; regression sweep of the corrections for new checklist violations (enforceability, cross-AD contradiction, silent dimensions, PRD fidelity per `prd.md` section 8.1); AD ID stability.
- **Ground truth re-verified at HEAD:** `references/Hexalith.Builds/Props/Directory.Packages.props`, `global.json`, `prd.md` section 8.1 (lines 279-288), `epics.md` Story 3.12 (line 1902 AC block).

## Verdict

**PASS**

The re-gate condition from the prior review is met: H1 is landed exactly as required (AD-21 and the UI Consistency row now bind catalog-resolved matching versions through the single `HexalithFrontComposerVersion` variable, with the catalog-add-before-adoption rule closing L5 in the same stroke), and the two delta-strengthening asks (M1, M2) are landed stronger than requested. M3 was not left to ride the refresh chain — all Stack rows now verify current against the Builds catalog at HEAD. The corrections introduce no new Medium-or-higher finding, contradict no other AD, weaken no PRD 8.1 constraint, and mint or renumber no AD ID (AD-1 through AD-24, sequential, unique, unchanged).

Two new Low observations and three carried-forward Lows remain; none blocks. `final` status is now warranted.

---

## Per-Finding Closure

### H1 - FrontComposer `3.2.2` literal vs. catalog authority at `4.0.1` — CLOSED

- **AD-21 Rule** (line 286) no longer freezes a version. New binding text: "Every consumed FrontComposer package resolves from the Builds catalog's single `HexalithFrontComposerVersion` variable (currently `4.0.1`), so all FrontComposer packages move together; a consumed package absent from the catalog (`Contracts.UI` today) is added there under that variable before adoption, never pinned locally. Debug source and Release package modes resolve the same package boundary at the same version." This is precisely the required invariant shape: *matching versions, one boundary, catalog-owned* — the literal is now a descriptive snapshot, not a competing authority.
- **UI Consistency row** (line 349): "composes FrontComposer Shell/Contracts.UI dependencies at the Builds catalog's single `HexalithFrontComposerVersion` (currently `4.0.1`)". Aligned.
- **Stack row** (line 372) renamed "Hexalith.FrontComposer packages": "Catalog `HexalithFrontComposerVersion` `4.0.1`; matching root-declared source in Debug and centrally pinned NuGet packages in Release". Verified: catalog line 9 sets `HexalithFrontComposerVersion` = `4.0.1`.
- No FrontComposer `3.2.2` literal remains anywhere in the spine (the Stack's xUnit v3 `3.2.2` is a coincidental, correct, unrelated pin).
- AD-11 (catalog sole authority) and AD-21 now give a Story 7.14 implementer one instruction. The two-authorities divergence is eliminated.

### M1 - Fail-closed clause narrower than the ratified gate — CLOSED

AD-11's container paragraph (line 131) now states: "Every validation failure fails the release evidence gate closed - wrong media type, platform-set mismatch, digest or size mismatch, unresolved descriptor, config mismatch, or product smoke failure - and emulation or environment setup failure is classified separately from a product-image failure but blocks the gate equally: a platform whose smoke cannot run is unproven, not passed."

- Coverage check against Story 3.12's AC ("a single-platform manifest, wrong media type, missing/duplicate/extra platform, config mismatch, unresolved child, or digest mismatch fails the release evidence gate"): every AC failure class maps into the spine enumeration (single-platform / missing / duplicate / extra → platform-set mismatch; unresolved child → unresolved descriptor), and the spine adds size mismatch and product smoke failure. The spine is now at least as strong as the story it governs — the prior inversion is gone.
- The emulation/setup disposition ambiguity is resolved in the strict direction: separately classified (preserving the AC's "cannot be recorded as a pass") *and* gate-blocking. The CI-implementer vs. release-owner divergence point is fixed.
- The smoke contract is now spine-defined: same bounded support-safe health smoke on both immutable child digests, under the contract's declared minimal configuration, with "a dependency absent from that declared configuration is part of the contract, not grounds to skip or substitute the check". No skip-and-report-green path survives.

### M2 - Released-container inventory a silent dimension — CLOSED

- AD-11 now scopes the released set explicitly: "Released container repositories are exactly the release workflow's `container-projects` mapping - currently the single `eventstore` repository." Story 3.12's "only `eventstore` is published as a container" AC now has its invariant backstop, and repository identity ownership is assigned (the release workflow's mapping — the "owned by the release configuration" resolution the prior review offered).
- The `eventstore-admin-ui` ambiguity is resolved in place: "until then AD-21's `eventstore-admin-ui` is an AppHost/deployment topology identity, not a released registry artifact" — and AD-21's own "AppHost resource and container identity" wording is consistent with that reading, so no cross-AD contradiction.
- Future images are contract-gated, not silently addable: "Any future externally released container image, including `eventstore-admin-ui`, adopts this same index/platform/validation contract and an AD-22 identity mapping before its first release", plus "deployment profiles must not require a platform of any image that no release evidence proves." The add-a-registry-publication-without-violating-any-AD loophole is closed.
- The Consistency "Release" row (line 351) mirrors the scoping ("currently the single `eventstore`") and the fail-closed shared-validator binding — spine-internal consistency holds.

### M3 - Six stale Stack rows at HEAD — CLOSED

Every previously stale row re-verified against `references/Hexalith.Builds/Props/Directory.Packages.props` and `global.json` at HEAD:

| Row | Spine now | Catalog/repo HEAD | Status |
| --- | --- | --- | --- |
| NSubstitute | `6.0.0` | `6.0.0` (line 256) | Current |
| OpenTelemetry exporter/hosting/ASP.NET/HTTP | `1.17.0` | `1.17.0` (lines 263-269) | Current |
| OpenTelemetry runtime instrumentation | `1.17.0` (StackExchangeRedis `1.16.0-beta.1` noted) | Runtime `1.17.0` (line 270), StackExchangeRedis `1.16.0-beta.1` (line 272) | Current; the beta annotation honestly surfaces the one family outlier |
| CommunityToolkit.Aspire.Hosting.Dapr | `13.4.1-beta.686` | `13.4.1-beta.686` (line 135) | Current |
| ASP.NET Core / SignalR | `10.0.10` "(catalog and security baseline aligned)" | `10.0.10` family (lines 170-191) | Current; the misleading `10.0.9`-seed-below-baseline reading is gone |
| Hexalith.Commons.UniqueIds | `2.28.2` | `HexalithCommonsVersion` `2.28.2` (line 6) | Current |
| Hexalith.FrontComposer packages | Catalog `4.0.1` | `4.0.1` (line 9) | Current (H1) |

Rows verified-current in the prior review remain correct (.NET SDK `10.0.302`/`latestPatch` per `global.json`; Aspire.Hosting `13.4.6`; Keycloak/Kubernetes `13.4.6-preview.1.26319.6`; Dapr .NET SDK `1.18.4`; MediatR `14.2.0`; FluentValidation `12.1.1`; CodeAnalysis `5.6.0`; FluentUI `5.0.0-rc.4-26180.1`; xUnit v3 `3.2.2`; Shouldly `4.3.0`). The .NET SDK row's baseline restatement (`10.0.302`) is consistent with AD-11's closing sentence (SDK `10.0.302` / ASP.NET `10.0.10` respectively). "Named tech is verified-current" now passes at HEAD for the entire table.

### L2 - Variant-descriptor fail-close risk — CLOSED (resolution path ratified)

AD-11 now names the SHA-pinned shared Hexalith.Builds publisher/validator as the single shape authority, forbids local reinterpretation by Builds or the EventStore caller, and routes any shape change "through an approved proposal and a Builds change, never a validation weakening." The no-variant rule thereby ratifies the deployed validator's behavior, and the arm64 `"variant": "v8"` contingency the prior review flagged has exactly one sanctioned exit (proposal-gated amendment). The implementer-discovery concern is discharged at spine level; the Story 3.12 build-time check remains an implementation note, not a spine gap.

### L5 - `Contracts.UI` unpinned in the catalog — CLOSED (folded into H1 as recommended)

Verified at HEAD: the catalog pins `Contracts`, `Mcp`, `Shell`, `SourceTools`, `Testing` under `HexalithFrontComposerVersion` and has no `Contracts.UI` entry — matching AD-21's "(`Contracts.UI` today)" clause exactly. The rule "added there under that variable before adoption, never pinned locally" converts the gap from a latent trap into an explicit precondition with an owner (Builds catalog). Closed at spine level; the actual catalog addition is Story 7.14-era Builds work.

### L1 - `v3.75.0` instance evidence in AD-11 Rule text — CARRIED (accepted)

The parenthetical remains (line 131). Prior disposition stands: acceptable brownfield ratification; prefer relocating the citation to the Story 1.20 packet reference on a future edit. Non-blocking.

### L3 - Frontmatter `companions` self-reference — CARRIED (open)

Line 38 still lists `_bmad-output/planning-artifacts/architecture.md` inside its own `companions`. Harmless metadata defect, outside the correction scope. Non-blocking; fix on next touch.

### L4 - Inconsistent `[ADOPTED]` markers — CARRIED (open)

AD-13, AD-14, AD-15, and AD-17 (lines 139, 147, 193, 221) still lack the `[ADOPTED]` marker the other twenty ADs carry. Non-blocking; label uniformly on next touch.

### L6 - Capability map omits the shared-publisher location — CARRIED (open)

The FR17-FR22/FR25 row (line 453) still lists `.github/workflows`, `tools/release-packages.json`, central props, `references/` layout. AD-11 now names the shared Builds publisher/validator as the shape authority, so the map's omission is mitigated by the AD text, but the row still does not answer "where does the two-platform gate live?" directly. Non-blocking; add the publisher/caller boundary on next touch.

---

## Regression Sweep Of The Corrections (new-violation check)

1. **PRD 8.1 fidelity:** re-read at lines 279-288. The corrections preserve every ratified bullet — catalog sole authority with consumer-props-import-only (line 283 ↔ AD-11), latest-validated-compatible with documented exceptions and no search-driven downgrade (line 284 ↔ AD-11), `UseHexalithProjectReferences` intent semantics (line 285 ↔ AD-11), .NET SDK container support not Dockerfiles (line 286), one immutable OCI index with exactly `linux/amd64` + `linux/arm64` fail-closed (line 287). The new spine text only *strengthens* (smoke contract, digest binding, immutable tags, single validator authority); nothing is weakened or contradicted. The future-image adoption clause extends beyond PRD scope legitimately (architecture-level policy; the PRD constrains the EventStore container and is untouched).
2. **Story 3.12 consistency:** the strengthened enumeration, smoke contract, no-repointing rule, and container-projects scoping are each backed by or stronger than the AC block at `epics.md` line 1902; the stricter emulation disposition satisfies (does not contradict) "cannot be recorded as a pass". The variant prohibition matches the AC verbatim.
3. **Cross-AD consistency:** AD-11 ↔ AD-21 (`eventstore-admin-ui` identity scoping — complementary, verified above); AD-11 ↔ AD-22 (the recorded identity chain — index digest, both child manifest digests, both child config digests — is exactly what AD-11's validation resolves and binds; chain-only mapping with fail-closed unknowns coheres with "Tag resolution never authorizes deployment"); AD-11 ↔ AD-12 (enumeration gained "container release registry and smoke evidence" — reinforcing, not contending); Runtime topology row's digest-not-tag rule ↔ AD-11/AD-22 (consistent, and it operationalizes the "deployment profiles must not require a platform no evidence proves" clause). AD-9's topology-slice scope remains disjoint from registry release objects. No contradiction found.
4. **Enforceability:** every new clause names an authority (release workflow `container-projects` mapping; SHA-pinned shared Builds publisher/validator; catalog `HexalithFrontComposerVersion`) and a fail direction (closed). The smoke contract's "declared minimal configuration" is contract-by-reference to the single shape authority — implementable one level below without interpretation.
5. **Silent dimensions:** the correction removed the two the prior review found (container inventory, emulation disposition) and added none Medium-worthy. Two residual observations are recorded below as new Lows.
6. **AD ID stability:** AD-1 through AD-24 present, sequential, unique — 24 headings, same IDs, no renumbering, no new IDs minted. All corrections landed as text within existing ADs and rows. Correct discipline.
7. **Frontmatter:** `updated: 2026-07-19` and the sources list are unchanged from the reviewed revision — correct, since the corrections are part of the same 2026-07-19 revision.

## New Findings

### NL1. "(currently `4.0.1`)" snapshots in AD-21 Rule and UI row will go stale on the next catalog move [LOW]

The binding semantics are now catalog-owned, so drift no longer creates competing authorities (the H1 failure mode) — a future catalog move to e.g. `4.1.0` merely leaves a stale descriptive parenthetical in two invariant-section locations plus the Stack row. Story 3.11's refresh mandate covers "version rows" in the Stack table; the two parentheticals outside it have no named refresher. Prefer confining the literal to the Stack row on the next edit and letting AD-21/UI row say only "the catalog's single `HexalithFrontComposerVersion` variable". Non-blocking: the invariant text explicitly subordinates the literal to the variable, so a stale snapshot cannot redirect an implementer.

### NL2. The container smoke contract's declaration home is implied, not named [LOW]

AD-11 requires the smoke to run "under the smoke contract's declared minimal configuration" and makes skipping impossible, but the artifact in which that minimal configuration is *declared* is only implied by the adjacent single-shape-authority sentence (the SHA-pinned shared Builds publisher/validator). One clause — "the smoke contract is declared alongside the shared validator" or naming its file/workflow home — would remove the last interpretive step. Non-blocking: the single-authority rule plus proposal-gated change routing already prevents two teams declaring rival smoke contracts.

## Disposition Summary

| # | Prior severity | Finding | Status |
| --- | --- | --- | --- |
| H1 | High | FrontComposer literal vs. catalog authority | CLOSED |
| M1 | Medium | Fail-closed enumeration narrower than gate; emulation disposition | CLOSED |
| M2 | Medium | Container inventory / `eventstore-admin-ui` / repository-identity dimension | CLOSED |
| M3 | Medium | Six stale Stack rows | CLOSED (verified current at HEAD) |
| L1 | Low | `v3.75.0` instance in Rule text | Carried, accepted |
| L2 | Low | Variant-descriptor fail-close risk | CLOSED (proposal-gated authority) |
| L3 | Low | `companions` self-reference | Carried, open |
| L4 | Low | Missing `[ADOPTED]` markers (AD-13/14/15/17) | Carried, open |
| L5 | Low | `Contracts.UI` unpinned in catalog | CLOSED (add-before-adoption rule) |
| L6 | Low | Capability map missing shared-publisher location | Carried, open |
| NL1 | Low (new) | "(currently `4.0.1`)" snapshots outside the Stack table | Open, non-blocking |
| NL2 | Low (new) | Smoke-contract declaration home implied by adjacency | Open, non-blocking |

**Gate result: PASS.** No Critical, High, or Medium findings remain. The five open Lows (L3, L4, L6, NL1, NL2) are next-touch cleanups that do not impair the spine's build-substrate function, contradict no decision, and mislead no level-below implementer.
