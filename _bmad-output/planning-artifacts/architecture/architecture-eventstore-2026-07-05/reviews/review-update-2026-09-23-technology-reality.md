# Technology and Repository Reality Review — 2026-09-23

**Artifact:** `_bmad-output/planning-artifacts/architecture.md` (draft, updated 2026-09-23)

**Lens:** Current technology and brownfield reality for AD-10, AD-11, AD-26 and their supporting stack/seed.
**Verdict:** **FAIL, two high findings.** The new boundaries are defensible as architecture targets, but AD-10's named inventory misses a JWT-binding Tenants host and the Stack presents an obsolete repository snapshot as current context. No finding authorizes AD-26 ratification, release, promotion, or readiness.

## Findings

### H1 — AD-10 must identify both Tenants JWT-binding hosts

**Location:** AD-10 JWT contract and Deferred “Shared JWT host conformance” row.

**Evidence:** The new text names “Tenants API.” `references/Hexalith.Tenants/src/Hexalith.Tenants.Api/Program.cs:20-63` is an external REST host that configures JWT locally, with default issuer/audience values and no explicit signing-algorithm allowlist or shared-options startup validator. `references/Hexalith.Tenants/src/Hexalith.Tenants/Program.cs:125-158` is another JWT-binding host: it registers authentication and imported controllers and maps them. That domain host uses EventStore's `ConfigureJwtBearerOptions` and `ValidateEventStoreAuthenticationOptions`, which delegate to `JwtBearerAuthenticationContract`, plus a Tenants production validator. The two host identities therefore have different current adoption states. Naming only “Tenants API” leaves the domain-service host's required fingerprint and conformance ownership ambiguous; one team could count it under Tenants and another could omit it from the all-host gate. `src/Hexalith.EventStore.ServiceDefaults/Authentication/JwtBearerAuthenticationContract.cs` supplies the core validation rules, but a repository search found no JWT host/config fingerprint inventory or conformance gate. The spine correctly says adoption is not yet proven.

**Disposition:** Amend AD-10 and the Deferred row to name `Hexalith.Tenants` domain-service host and `Hexalith.Tenants.Api` REST host separately. State which existing shared-contract use is present and which host still has local JWT configuration; require distinct registered fingerprints and positive/negative conformance for both. Keep NFR3 all-host readiness failed until the inventory and gate exist. Do not infer runtime delivery from the adopted decision.

### H2 — The Stack snapshot is stale against its declared repository authority

**Location:** “Stack And Version Seed,” currently introduced as repository state observed 2026-09-09.

**Evidence:** Current `global.json` pins SDK `10.0.401`, while the table says `10.0.400`. The catalog identified by AD-11 as version authority, `references/Hexalith.Builds/Props/Directory.Packages.props`, now pins Aspire.Hosting `13.5.4` (table `13.5.3`), CommunityToolkit Aspire Dapr `13.5.1-beta.757` (table `13.5.0-preview.1.260825-0345`), Dapr.Client `1.18.8` (table `1.18.5`), ASP.NET Core/SignalR `10.0.12` (table `10.0.11`), FrontComposer `4.5.0` (table `4.4.0`), OpenTelemetry `1.19.0` (table `1.18.0`), CodeCoverage `18.11.2` (table `18.10.0`), and xUnit v3 `4.0.1` (table `4.0.0`). `integration.yml` still pins Dapr runtime `1.18.2`, and `deploy/README.md` still shows `daprio/daprd:1.18.0`, as the table says. These are repo observations, not a proposal to update dependencies.

**Disposition:** Refresh the table's repository-value cells from the current authoritative files and date the new observation. Keep the 2026-09-09 observations in the existing historical review/memlog. Do not describe a current catalog pin as a pending update. Keep the Dapr runtime/deploy example split explicit pending a tested AD-26 production pin. Retain preview/RC channel labels and the compatibility exception where the catalog still uses one.

### M1 — “Generated host fixtures” need a concrete runtime scope

**Location:** AD-10 JWT contract.

**Evidence:** `samples/Hexalith.EventStore.Sample.Api/Program.cs:15-27` is a real host for generated `[Authorize]` controllers and already registers ServiceDefaults JWT options. `tests/Hexalith.EventStore.RestApi.Generators.Tests/RestApiManifestGenerationTests.cs` uses Roslyn compilations and generated-source assertions; those source fixtures do not start a JWT-binding HTTP host. The source generator emits controllers, while a consuming application owns host authentication. No JWT fingerprint inventory or generated-host runtime conformance fixture was found by the focused repository search.

**Disposition:** Specify whether “generated REST API host fixtures” means a runnable generated-controller host test (with a named fixture/host identity), or clarify that all consuming JWT-binding hosts are covered by the future-host registration rule and that compile-only generator tests are insufficient as conformance evidence. The actual Sample API remains explicitly in scope.

## Verified Target And Open Implementation Gates

- AD-11 now uses exactly the PRD's five names in order: `built`, `evidence-candidate-published`, `evidence-validated`, `release-available`, `production-promoted`. It distinguishes the Story 3.15 evidence result from release and promotion authority. This is a target contract, not a claim that authority transitions have been implemented.
- AD-26 remains `[ASSUMPTION]`, calls for explicit owner ratification, and says the sole canonical `deploy/dapr/production-profile.yaml` is absent. Focused `rg --files deploy tools` found neither that file nor `tools/validate-publication-authority.py`; the publication-authority evidence directory also does not exist. The validator and current-subject authority record remain blockers. No profile or later publication state is currently authorized.
- The selected `state.postgresql` v1 and OpenBao through `secretstores.hashicorp.vault` v1 still fit documented Dapr components. [Dapr's PostgreSQL v2 reference](https://docs.dapr.io/reference/components-reference/supported-state-stores/setup-postgresql-v2/) says v2 cannot share/migrate v1 state and v1 remains available. [Dapr's OpenBao reference](https://docs.dapr.io/reference/components-reference/supported-secret-stores/openbao/) confirms the Vault component works for OpenBao. [Dapr's v1.18.3 release](https://github.com/dapr/dapr/releases/tag/v1.18.3) supports the table's assertion that 1.18.3 exists. These sources establish technology fit, not tested production proof.

## Validation And Limits

Read-only checks covered `global.json`, the Builds package catalog, `integration.yml`, `deploy/README.md`, the shared JWT contract, EventStore and Sample API registration, both Tenants hosts, the generator test harness, and the AD-10/11/26 text. Focused searches for JWT host/config fingerprints and publication-profile/authority files returned no matches. No build or runtime test was run because this review changed no implementation. The earlier 2026-09-09 review remains historical evidence for its then-current snapshot; this review does not rewrite it.

## Rereview Of Saved Corrections — 2026-09-23

**Updated verdict:** **FAIL, one remaining high finding.** H1, H2, and M1 above are closed as architecture wording findings by the saved edit. Runtime conformance and publication/production gates remain unimplemented and blocked, as the spine says. The original findings remain above as historical evidence.

| Earlier finding | Disposition against current saved architecture |
| --- | --- |
| H1, two Tenants hosts | **Closed in text.** AD-10 and the Deferred row now name both the Tenants domain-service host and Tenants API separately; adoption is explicitly unproven. |
| H2, stale Stack | **Closed.** The Stack is dated 2026-09-23 and its SDK/package values match current `global.json` and the root-declared Builds gitlink's `Props/Directory.Packages.props`. CI Dapr `1.18.2` and deploy example `1.18.0` remain accurately distinct from a future production pin. |
| M1, generated-host fixture | **Closed in text.** AD-10 now requires a runnable generated-controller host fixture; generated source alone cannot close the gate. |

### H3 — Admin UI is a JWT-binding host missing from the explicit conformance inventory

**Location:** AD-10 JWT contract and the Deferred “Shared JWT host conformance” row.

**Evidence:** `src/Hexalith.EventStore.Admin.UI/Program.cs:13-17` builds a runnable Admin UI host. `src/Hexalith.EventStore.Admin.UI/AdminUIServiceExtensions.cs:34-36,185-188` registers `.AddAuthentication("Bearer").AddJwtBearer()`, calls `UseAuthentication`, and maps interactive Razor components. It does not register the shared `JwtBearerAuthenticationContract` or its options validator. AD-10's generic “every ... JWT-binding host” rule should include this host, but its explicit named list and readiness-gate row include Admin Server Host and omit Admin UI. A closed host inventory constructed only from the named list would miss a real JWT registration. Separately, `samples/Hexalith.EventStore.Sample.BlazorUI/Program.cs:18-29` exposes a UI without inbound JWT registration; the wording “every externally reachable or JWT-binding host consumes this [JWT] contract” may unintentionally demand JWT on that UI. This follows the PRD's similarly broad NFR3 wording, so product/security owners should settle the non-JWT UI interpretation rather than treating the sample's current configuration as proven conformance.

**Disposition:** Explicitly name Admin UI as an existing JWT-binding host in AD-10 and the all-host proof inventory; require its shared-contract adoption or an approved scheme-specific exception. Clarify that an externally reachable host with no inbound JWT registration needs an approved authentication posture and inventory disposition, while the shared JWT contract applies whenever it binds JWT. Keep G-AUTH-HOSTS/NFR3 failed until current host registrations, fingerprints, and negative tests are proved.

### L1 — Dapr release source link lags the refreshed table

The Stack says Dapr runtime `1.18.4` is available. The official [Dapr v1.18.4 release](https://github.com/dapr/dapr/releases/tag/v1.18.4) confirms the claim, but architecture frontmatter `sources` still links to v1.18.3. Point the source entry to v1.18.4 for direct traceability. This is a citation maintenance issue, not a version-fit failure.

**Rereview checks:** Compared the saved AD-10 and Deferred text, saved Stack, `global.json`, pinned Builds catalog, Admin UI and Sample Blazor UI host registrations, and Dapr's official v1.18.4 release. No build or runtime conformance test was performed; the review does not claim implementation proof.

## Final Disposition After Saved Follow-Up — 2026-09-23

**Final technology/reality verdict: PASS for architecture wording; implementation readiness remains BLOCKED.** The original H1/H2/M1 and interim H3/L1 findings above are preserved as dated review history; all are closed against the current saved spine.

- **H3 closed:** AD-10 and the Deferred gate now explicitly include Admin UI and Sample Blazor UI as distinct host identities. The text states that an externally reachable host without a protected REST controller remains inventoried and that absent inbound authentication fails conformance. This represents the current Sample Blazor UI gap accurately without claiming it is fixed. Admin UI's local `.AddJwtBearer()` remains an implementation gap covered by the failed all-host gate.
- **L1 closed:** Frontmatter now cites the official [Dapr v1.18.4 release](https://github.com/dapr/dapr/releases/tag/v1.18.4), matching the refreshed Stack assertion. The Stack's package and SDK values still match the current root-declared Builds gitlink and `global.json`.
- **AD-11/AD-26 unchanged in substance:** The five publication states, separate release/deployment authority, absent canonical profile and validator, and `[ASSUMPTION]` ratification posture remain explicit. No production profile or later publication state is approved by this review.

Final check read the saved AD-10, AD-11, AD-26, Stack, Deferred gate, and source entry; no build or runtime test was run. The review file passed `git diff --check` before this final append.
