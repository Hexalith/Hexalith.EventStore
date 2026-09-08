# Brownfield Traceability Review — eventstore Phase 4 Implementation Readiness Recovery PRD

## Preamble

The PRD's epic-level FR map (section 11.1) is accurate: all 37 FR rows name the epic that epics.md declares, and every FR is referenced by at least one story body. The PRD is not accurate as an authoritative NFR/story/status source today: NFR3 and NFR4 were rewritten out-of-band on 2026-09-06 (commit `8312bced`) with no approving artifact, no frontmatter or memlog update, and no matching update to the epics.md Requirements Inventory; the new NFR3 text over-promises the break-glass behaviour the code actually implements; eight section 11.2 cells name stories whose epics.md bodies do not claim that NFR; and Story 5.3 — the sole NFR3/NFR4 owner — carries three different statuses across sprint-status.yaml, its spec, and epics.md. Method: line-scoped `grep` of FR/NFR ids per `### Story` section of epics.md, sprint-status.yaml key/status listing, `git show 8312bced`, `git log -S` on the tracker, and direct reads of `JwtBearerAuthenticationContract.cs`, `Directory.Build.props`, `Directory.Packages.props`, `release.yml`, and the Story 5.3 spec. No builds or tests were run; only this file was written.

## Findings

- **[critical]** NFR3/NFR4 were rewritten out-of-band with no authorizing artifact and no provenance update (§7, lines 272-273) — Commit `8312bced` (2026-09-06 13:44, "feat: update acceptance criteria and architectural constraints for authentication and authorization") replaced both NFR rows in `prd.md` (+2/-2), the Story 5.3 body in `epics.md`, and `epic-5-context.md`. No `sprint-change-proposal-2026-09-06*.md` exists; `sprint-change-proposal-2026-09-07.md` states "**No PRD, architecture, epic, or UX content changes**" and concerns Epic 3 only. The Story 5.3 spec (`implementation-artifacts/spec-5-3-production-authentication-guards-and-secret-stripping.md`) was first committed on 2026-09-07 (`dfbbde37`), *after* the PRD edit, and its repair log cites "the authoritative Epic 5.3 AC and NFR3" as the reason for the repair — the requirement was rewritten first and then used as authority, which is circular. The PRD frontmatter still reads `status: final` / `updated: 2026-08-16` (the diff touched no frontmatter line), and `prds/prd-eventstore-2026-07-05/.memlog.md` has no entry between the 2026-08-16 finalize and the 2026-09-08 validate-run open. *Fix:* either (a) author and approve a dated sprint-change-proposal that ratifies the 2026-09-06 NFR3/NFR4 text, bump `updated`, add memlog `(override)`/`(decision)` entries as was done for the July out-of-band edits, and propagate to the epics.md inventory (see medium finding below); or (b) revert `prd.md` lines 272-273 to the 2026-08-16 text and carry the detailed contract only in Story 5.3.
- **[high]** New NFR3 over-promises the break-glass scope the code implements (§7, line 272) — NFR3 says authentication "outside Development must reject symmetric-key mode **unless** explicitly enabled by ... `AllowInsecureSymmetricKey`", i.e. the option is a valid exception in every non-Development environment including Production. The implementation is stricter: `src/Hexalith.EventStore.ServiceDefaults/Authentication/JwtBearerAuthenticationContract.cs:118-122` returns `"{section}:SigningKey is forbidden in Production, including when AllowInsecureSymmetricKey is true."` before the option is consulted (lines 124-128 only admit it in non-Development, non-Production environments), and the log message at line 302 calls it a "non-Production symmetric JWT exception". The Story 5.3 spec agrees with the code, not the PRD: "**Never:** Permit symmetric JWT validation in Production even when the legacy override is set" (spec line 29). The epics.md Story 5.3 AC written by the same commit ("starts outside Development ... unless `Authentication:JwtBearer:AllowInsecureSymmetricKey` explicitly permits", epics.md ~line 3822) carries the same over-statement. *Fix:* reword NFR3 to "non-Development, non-Production environments may accept HS256 symmetric mode only when `AllowInsecureSymmetricKey` is explicitly enabled; Production rejects symmetric mode unconditionally", and align the Story 5.3 AC.
- **[high]** Story 5.3 — the only NFR3/NFR4 owner — has three contradictory statuses (§11.2, lines 424-425; §10 SM3, line 357) — `sprint-status.yaml:181` says `done`; the story's own spec frontmatter says `status: 'in-progress'`, `review_loop_iteration: 3`, `warnings: [oversized]`; `epics.md:3810` says "**Current reconciliation:** Story 5.3 remains backlog". `git log -S` shows the tracker moved `backlog` -> `done` in `c83cc4c3` (2026-09-07 11:48, "feat(tests): add tests for access token retrieval and handle missing credentials"), a test commit with no closure record, and the spec's Boundaries forbid the story from modifying `sprint-status.yaml` at all. SM3 ("NFR1-NFR4 ... map to concrete story coverage") therefore cannot be read off any single source. *Fix:* reconcile the three sources; if 5.3 is genuinely done, close the spec (`status: done`) with its review evidence and refresh the epics.md reconciliation paragraph; otherwise return the tracker row to `in-progress`/`review`.
- **[high]** Eight section 11.2 cells name stories whose epics.md bodies do not claim that NFR (§11.2, lines 422-436) — Line-scoped grep of each `### Story` section: NFR1 -> **5.3** (0 hits; its coverage line is "FR26, NFR3, and NFR4"); NFR6 -> **1.13** (0 hits; coverage is "FR4-FR7, FR9, NFR8, NFR16"); NFR14 -> **7.19** (0 hits; coverage names NFR15, NFR1-NFR2, NFR16); NFR16 -> **1.11** and **1.12** (0 hits; "Primary FR1; supporting FR9, FR10, NFR14" and "Primary FR10"), and **8.2, 8.3, 8.4, 8.5, 8.6** (0 hits each; 8.2's coverage is "NFR7, NFR12, NFR19"); NFR19 -> **8.8** (0 hits; coverage is "NFR9-NFR11 ... NFR16"). The epics.md "Story traceability rule" (line ~370) requires every story to name its NFR coverage, so these PRD cells assert ownership epics.md does not declare. All other 11.2 cells (NFR2, NFR3, NFR4, NFR7, NFR8, NFR9, NFR10, NFR11, NFR15, NFR17) are backed by story-body mentions. *Fix:* narrow the ranges — NFR16 to `1.9-1.10, 1.13-1.15, 3.11-3.15, 4.9-4.15, 7.10, 8.7-8.11`; NFR19 to `8.1-8.7, 8.9-8.11`; drop 5.3 from NFR1, 1.13 from NFR6, 7.19 from NFR14 — or add the missing NFR ids to the epics.md coverage lines if ownership is intended.
- **[medium]** epics.md Requirements Inventory still carries the pre-2026-09-06 NFR3/NFR4 text, so the "authoritative" PRD and its downstream inventory disagree (§7, lines 272-273) — `epics.md:111`: "NFR3: Production authentication must reject insecure symmetric-key mode unless explicitly break-glassed, require HTTPS metadata where appropriate, and pin accepted JWT algorithms." and `epics.md:113`: "NFR4: Committed configuration must not contain forgeable administrator signing keys, credentials, bearer tokens, decoded JWT payloads, or other operational secrets." — both are the old PRD wording; `8312bced` changed only the Story 5.3 body. The epics "Source-drift rule" requires reconciliation on input change. *Fix:* update `epics.md:111,113` to the ratified text (or revert the PRD) in the same change that resolves the critical finding.
- **[medium]** Section 11.3 pending/done assertions have drifted from the tracker (§11.3, lines 442-451) — (a) Line 444 says G5 stays pending "until Story 8.1's approved security specification authorizes Story 8.2"; `sprint-status.yaml:230` has `8-1: done` with the guarded comment "Security/architecture gate approved; Story 8.2 alone is authorized" — the first gate has passed and 8.2 is authorized-but-backlog. (b) Line 449 names `_bmad-output/implementation-artifacts/spec-folded-snapshot.md` as the required Story 6.1 output; that path does not exist, but `spec-6-1-folded-snapshot-frozen-spec.md` (created 2026-09-08, `status: 'draft'`) and `epic-6-context.md` now do, while epics.md lines 4257/4302/4320/4326 still bind Stories 6.1/6.2 to the `spec-folded-snapshot.md` path — an emerging path mismatch. (c) The 1.20/3.12 `done`, 3.13 `done` (rejected disposition), 3.14 `done` claims match the tracker; 3.15 is `in-progress` at 0/3 receipts (verifier exits 1 per the 2026-09-07 proposal), consistent with the PRD not claiming closure. (d) 4.15 and 5.4 are `review`, 4.7 `in-progress`. (e) Story 5.2's request-size limits (line 447) are implemented: `AdminRequestSizeLimits.OrdinaryJsonBody = 1024L*1024L`, `BackupImportJsonBody = 10L*1024L*1024L`, applied at `AdminBackupsController.cs:185` on `ImportStream`; the four Story 7.15-7.18 backlog files (line 453-456) all exist. *Fix:* rewrite line 444 as "Story 8.1 approved 2026-xx; 8.2 authorized, not started"; either rename the 6.1 spec to the bound path or update the PRD/epics.md path in one change.
- **[medium]** Stories 1.21 and 3.16 are invisible to the PRD although both carry FR/NFR coverage and one gates Epic 3 closure (§11.1 FR19/FR21/FR36, lines 396-398 and 413; §9.1, line 328) — `grep -c '1\.21\|3\.16' prd.md` = 0. epics.md declares Story 1.21 "Frozen Story 1.20 Evidence Integrity Repair" (`done`, references FR36, NFR16) and Story 3.16 "Latest-Compatible Dependency And Root Submodule Refresh" (`backlog`, references FR19, FR21, NFR9, NFR11, NFR12). `epic-3-context.md:54` makes 3.16 a condition for Epic 3 closure, and the 2026-09-07 proposal documents that its absence from the tracker previously hid an unbuilt story behind `pending_stories: []`. *Fix:* add 3.16 to the FR19/FR21 rows and to the section 9.1 release/repository bullet; add 1.21 to the FR36 row alongside 1.20.
- **[low]** The two-platform OCI image-index constraint is enforced outside this repository, and the three Builds identities in play disagree (§8.1, line 300) — No EventStore build file declares the platform set (`Directory.Build.targets` only sets `ContainerFamily=alpine` and pins `ContainerImageTag` to `ContainerProvenanceReleaseVersion`; no `RuntimeIdentifiers`). Enforcement lives in `Hexalith.Builds/Github/publish-containers/{oci_registry_validator,publication_preflight,smoke_container_platforms}.py`, reached through `release.yml:112` `domain-release.yml@22a578b5`; EventStore-side evidence is `tools/release_evidence_handlers/v3.py:30 PLATFORMS = ("linux/amd64", "linux/arm64")` and `ContainerPublishingGovernanceTests.cs:985-986`, which reads `docs/ci.md`, not a workflow. The Builds submodule worktree is at `a32cb422`, the committed gitlink at `35c3d1e5`, and the release pin at `22a578b5`. The constraint is not violated, but the PRD reader cannot verify it from this repo. *Fix:* add "enforced by the pinned Hexalith.Builds `publish-containers` gate; EventStore evidence handler `v3.py`" to the constraint bullet.
- **[low]** NFR3 bundles authorization clauses the authentication contract does not own, and its host scope differs between PRD, epics AC, and code (§7, line 272) — "role, and tenant validation remain mandatory in every mode" is not part of `JwtBearerAuthenticationContract` (it validates issuer, audience, signature, lifetime, algorithms, HTTPS metadata and key strength only); role/tenant checks live in the authorization layer. The PRD says "Authentication outside Development" generically; the 2026-09-06 epics AC narrows to "the EventStore gateway or Admin Server Host"; the code is wired into three hosts (`src/Hexalith.EventStore/Authentication/ValidateEventStoreAuthenticationOptions.cs`, `src/Hexalith.EventStore.Admin.Server.Host/Authentication/ValidateAdminServerAuthenticationOptions.cs`, `samples/Hexalith.EventStore.Sample.Api/Program.cs:22-27`). *Fix:* move the role/tenant clause to NFR1/NFR2 and state the host set explicitly.
- **[low]** Tracker hygiene that distorts SM2/SM4 reading (§10, lines 356-361) — `epic-2: in-progress` (`sprint-status.yaml:101`) although all twelve Epic 2 stories and `epic-2-retrospective` are `done`; the key `2-12-tenants-runtime-identity-adoption-and-package-mode-validatio` (line 113) is truncated; `4-8` has no row by design (guarded comment, line 167-168). None of this is a PRD defect, but SM2's "every FR maps to at least one epic and story" is being read against a tracker whose epic rollups are stale. *Fix:* out of PRD scope; raise with the sprint-status owner (guarded comment blocks must not be edited).

## Verification tables

### 11.1 FR row vs epics.md (epic-level agreement; story bodies that reference the FR)

| FR | PRD epic | epics.md inventory epic | Story bodies referencing FR | Agree |
| --- | --- | --- | --- | --- |
| FR1-FR3 | 1 | 1 | 1.1 (+1.11 for FR1) | yes |
| FR4 | 1 | 1 | 1.2, 1.9, 1.13, 1.16, 2.7, 2.11 | yes |
| FR5 | 1 | 1 | 1.3, 1.4, 1.9, 1.13, 1.14, 1.15 | yes |
| FR6 | 1 | 1 | 1.5, 1.9, 1.13 | yes |
| FR7 | 1 | 1 | 1.6, 1.10, 1.13, 1.17, 1.18, 1.19 | yes |
| FR8 | 1 | 1 | 1.7 | yes |
| FR9 | 1 | 1 | 1.8, 1.9, 1.10, 1.11, 1.13 | yes |
| FR10 | 1 | 1 | 1.11, 1.12 | yes |
| FR11 | 2 | 2 | 2.1, 2.4 | yes |
| FR12 | 2 | 2 | 2.2, 2.9, 2.11 | yes |
| FR13 | 2 | 2 | 2.3, 2.5, 2.6, 2.10, 7.14, 7.19 | yes |
| FR14 | 2 | 2 | 2.3, 2.10 | yes |
| FR15 | 2 | 2 | 2.4-2.7, 2.11, 2.12, 4.7, 7.19 | yes |
| FR16 | 2 | 2 | 2.8 | yes |
| FR17 | 3 | 3 | 3.1, 3.7, 3.10 | yes |
| FR18 | 3 | 3 | 3.2 | yes |
| FR19 | 3 | 3 | 3.3, **3.16** | yes (3.16 unlisted in PRD) |
| FR20 | 3 | 3 | 3.4 | yes |
| FR21 | 3 | 3 | 2.12, 3.5, 3.11, **3.16** | yes (3.16 unlisted) |
| FR22 | 3 | 3 | 2.12, 3.6-3.8, 3.11, 3.12, 3.14 | yes |
| FR23 | 4 | 4 | 4.1 | yes |
| FR24 | 4 | 4 | 4.6 | yes |
| FR25 | 3 | 3 | 3.7-3.9, 3.11, 3.12, 3.14 | yes |
| FR26 | 5 | 5 | 2.10, 5.1-5.4 | yes |
| FR27 | 4 (4.9-4.15) | 4 | 2.9, 4.2, 4.8-4.15 | yes |
| FR28 | 5 | 5 | 2.10, 5.5 | yes |
| FR29 | 4 | 4 | 4.3 | yes |
| FR30 | 4 | 4 | 4.4 | yes |
| FR31 | 4 | 4 | 4.5 | yes |
| FR32 | 5 | 5 | 5.6-5.9 | yes |
| FR33 | 6 | 6 | 1.19, 6.1-6.6 | yes |
| FR34 | 7 | 7 | 2.6, 2.11, 3.10, 7.1-7.14, 7.19, 7.20 | yes |
| FR35 | 7 | 7 | 7.15-7.18 | yes |
| FR36 | 1 (1.20) + 3 (3.13-3.15) | 1 and 3 | 1.2-1.4, 1.9, 1.10, 1.14-1.20, **1.21**, 2.12, 3.13-3.15 | yes (1.21 unlisted) |
| FR37 | 8 | 8 | 8.1-8.11 | yes |

All stories named in section 11 exist as `### Story` headings in epics.md and as keys in sprint-status.yaml (exception: 4.8 has no tracker row by design). No renumbering mismatches found.

### 11.2 NFR row vs epics.md story bodies

| NFR | PRD stories | Story bodies that mention the NFR | Missing in epics.md | Extra primary/supporting in epics.md not in PRD |
| --- | --- | --- | --- | --- |
| NFR1 | 5.2, 5.3, 5.5, 7.2, 7.3 | 2.8, 2.10, 3.10, 5.2, 5.4, 5.5, 5.7, 7.2, 7.3, 7.4, 7.7, 7.19, 8.x | **5.3** | 5.4, 5.7, 7.4, 7.7, 7.19 |
| NFR2 | 2.5, 5.2, 5.5, 5.6, 5.10 | 1.5, 1.9, 1.10, 1.14, 2.5, 3.10, 5.2, 5.5-5.8, 5.10, 7.x | none | 5.7, 5.8 |
| NFR3 | 5.3 | 5.3, 8.3, 8.6 | none | — |
| NFR4 | 5.3, 7.6 | 5.3, 5.4, 7.6, 8.x | none | 5.4 |
| NFR6 | 1.13, 7.1 | 1.6, 1.10, 1.18, 2.8, 4.1, 4.3, 4.6, 6.4, 7.1 | **1.13** | 1.18, 4.1, 4.3 |
| NFR7 | 4.1, 4.2, 4.4, 4.5, 4.9-4.15, 5.1 | all listed + 1.15-1.19, 4.6, 4.8, 6.4-6.6, 7.8, 7.11, 8.x | none | — |
| NFR8 | 1.16, 1.19, 6.2-6.4 | 1.2, 1.9, 1.13, 1.16, 1.19, 2.11, 4.7, 6.1-6.4 | none | — |
| NFR9 | 3.5, 3.8, 3.11-3.14 | all listed + 2.12, 3.3, 3.4, 3.6, 3.15, 3.16, 8.8 | none | 3.15, 3.16 |
| NFR10 | 3.1, 3.11, 7.10 | 3.1, 3.7, 3.8, 3.11, 7.10, 7.12, 7.13 | none | — |
| NFR11 | 3.6, 3.12, 3.14, 8.8 | 3.6, 3.8, 3.9, 3.12, 3.14-3.16, 7.9, 8.8 | none | 3.15, 3.16, 7.9 |
| NFR14 | 2.3, 2.5, 2.6, 7.14, 7.19 | 1.8, 1.11, 2.3, 2.5, 2.6, 2.10, 2.11, 7.5, 7.14 | **7.19** | 7.5 |
| NFR15 | 7.3, 7.4, 7.19 | 1.16, 2.6, 2.8, 3.10, 7.3, 7.4, 7.5, 7.19, 7.20 | none | 7.20 |
| NFR16 | 1.9-1.15, 3.11-3.15, 4.9-4.15, 7.10, 8.2-8.11 | 1.2-1.5, 1.9, 1.10, 1.13-1.21, 3.1, 3.2, 3.4-3.6, 3.8, 3.10-3.15, 4.2, 4.4, 4.5, 4.7-4.15, 5.8, 6.4, 6.6, 7.x, 8.1, 8.7-8.11 | **1.11, 1.12, 8.2, 8.3, 8.4, 8.5, 8.6** | 1.16-1.21, 4.2, 4.4, 4.5 |
| NFR17 | 3.12, 5.6, 7.6, 7.7, 7.8, 7.9 | 3.12, 3.14, 5.6-5.9, 7.6-7.10, 8.x | none | 3.14, 5.7-5.9 |
| NFR19 | 8.1-8.11 | 6.5, 6.6, 8.1-8.7, 8.9-8.11 | **8.8** | — |

### NFR3/NFR4 claims (2026-09-06 text) vs code

| Claim | Status | Evidence |
| --- | --- | --- |
| Option named `AllowInsecureSymmetricKey` | implemented | `ServiceDefaults/Authentication/JwtBearerAuthenticationOptions.cs:46`; config key `Authentication:JwtBearer:AllowInsecureSymmetricKey` (tests: `HostBootstrapTests.cs:332`, `JwtAuthenticationIntegrationTests.cs:250`) |
| Symmetric mode rejected "outside Development unless" the option is set | **implemented differently (stricter)** | `JwtBearerAuthenticationContract.cs:118-122` forbids `SigningKey` in Production even when the option is true; the option only admits non-Development, non-Production environments (`:124-128`); warning `:302` "non-Production symmetric JWT exception" |
| Development/break-glass symmetric mode accepts only HS256 | implemented | `:104-110` rejects any `AllowedAlgorithms` entry other than `HS256` in symmetric mode; `:163-165` sets `ValidAlgorithms = [HmacSha256]`; key >= 32 bytes `:112-116` |
| Nonempty explicit asymmetric allowlist, no implicit production default | implemented | `ResolveAuthorityAlgorithms` `:225-256` throws on empty list or non-member of `SupportedAsymmetricAlgorithms` (RS/PS/ES 256-512, `:16-27`); `Validate` `:91-99` fails startup; `JwtBearerAuthenticationOptions.AllowedAlgorithms` defaults to `[]` (`:26`) |
| HTTPS metadata required for authority/OIDC discovery | implemented (outside Development) | `:85-89` `RequireHttpsMetadata must be true outside Development`; `TryValidateAuthority` `:258-300` requires HTTPS scheme outside Development, no userinfo/query/fragment; default `true` (`Options.cs:41`) |
| Clock skew fixed at 60 seconds | implemented, not configurable | `:160` `ClockSkew = TimeSpan.FromMinutes(1)`; no `ClockSkew` property on the options record |
| Issuer, audience, signature, lifetime mandatory in every mode | implemented | `:152-159` `ValidateIssuer/Audience/IssuerSigningKey/Lifetime = true`, `RequireExpirationTime`, `RequireSignedTokens` |
| Role and tenant validation "remain mandatory in every mode" | not in this contract | authorization concern; not part of `JwtBearerAuthenticationContract` |
| Hosts covered | wider than epics AC | gateway `ValidateEventStoreAuthenticationOptions.cs`, Admin `ValidateAdminServerAuthenticationOptions.cs`, Sample API `samples/Hexalith.EventStore.Sample.Api/Program.cs:22-27` |
| NFR4: no committed signing key / password, including Development | consistent | tracked `appsettings*.json` carry `"SigningKey": ""` only; `AppHost/KeycloakRealms/hexalith-realm.json:203,226` hold `__HEXALITH_*_PASSWORD__` placeholders |
| PRD `updated` field / memlog updated for `8312bced` | **not updated** | diff touched only lines 272-273; frontmatter `updated: 2026-08-16` unchanged; `.memlog.md` has no 2026-09-06 entry |

### Section 8.1 constraints vs repository

| Constraint | Verified | Evidence |
| --- | --- | --- |
| `.slnx` only | yes | only `Hexalith.EventStore.slnx` at root; `ci.yml:20`, `advisory-tests.yml:55,58`, `integration.yml:63`, `release.yml:117` all reference it; no `.sln` |
| Unit tests by project | yes | `ci.yml:90,172`, `integration.yml:84-120` invoke per-project `dotnet test` |
| Central catalog in `references/Hexalith.Builds/Props/Directory.Packages.props` | yes | root `Directory.Packages.props` only enables CPM + transitive pinning and imports the Builds catalog (4 fallback paths); zero local `PackageVersion` items |
| `UseHexalithProjectReferences` explicit-true only | yes | `Directory.Build.props:48-51` default `false` for unset and configuration-less evaluation |
| SDK containers, no Dockerfiles | yes | no `Dockerfile*` outside `references/`; `Directory.Build.targets:16-18` container properties |
| Two-platform OCI image index, fail-closed | delegated | see low finding; `tools/release_evidence_handlers/v3.py:30`; Builds `publish-containers/*` at pin `22a578b5` |
| Root-declared submodules only under `references/` | yes | `.gitmodules` declares 7 `references/*` paths; no nested submodule directories initialized |

### MVP-scoped items (§9.1) whose owning stories are still unbuilt

| §9.1 bullet | Owning stories | Status today |
| --- | --- | --- |
| Security and tenant isolation remediation (trust-boundary closure, topology parity, reserved tenant) | 5.5, 5.6, 5.7, 5.8, 5.9, 5.10 | all `backlog`; 5.4 `review`; 5.3 disputed (see high finding) |
| Cost/evolution work behind spec-first gates | 6.1-6.6 | all `backlog` (6.1 spec draft created 2026-09-08 under a path the PRD/epics do not name) |
| Operator/admin/deployment/test recovery work | 7.1-7.14, 7.19, 7.20 | 16 stories `backlog`; only backlog-artifact stories 7.15-7.18 `done` |
| Release/repository reliability corrections | 3.15, 3.16 | 3.15 `in-progress` (0/3 receipts, verifier exit 1); 3.16 `backlog` and unnamed in PRD |
| Event correctness and recovery remediation | 4.7, 4.15 | 4.7 `in-progress`; 4.15 `review` |
| Projection/query parity completion incl. runtime-pin closure | 1.20 (done) + FR36 deployed half via 3.15 | source/package half done; deployed-runtime half open |
| Committed post-MVP (§9.3) | 8.2-8.11 | 8.1 `done`; 8.2 authorized but `backlog`; 8.3-8.11 gated |
