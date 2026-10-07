# Technology Reality Review — Update 2026-10-07

- **Subject:** `_bmad-output/planning-artifacts/architecture.md`, SHA-256 `e15b079f475c03603ac895213bfdd8ce0f68d0fdb90985d23e38d7874ac3f6cf` (552 lines, worktree; root `HEAD` `27ac3c62`).
- **Evidence class:** `tool-persona`, under the PRD Assurance Control. This review is evidence only. It is not an approval, a ratification or an `independent` review.
- **Lens:** were the committed decisions web-researched or reality-checked? The review covers current versions, whether each named technology exists and fits, and whether the repository paths, types and artifacts the ADs depend on exist or are declared absent.
- **Method:** read-only. There was no build, restore, test, Git mutation or submodule update. Pinned inputs (`global.json`, `Directory.Build.props`, `Directory.Packages.props`, `.github/workflows/`, `deploy/` and the AppHost `.csproj`) have no worktree diff against `HEAD`. The Builds submodule worktree is clean at gitlink `ba4ca78c` (2026-10-05).
- **Upstream sources:** the .NET release-metadata JSON; the GitHub Releases and Security-Advisories APIs for `dapr/dapr`, `dapr/cli`, `dapr/dotnet-sdk`, `dapr/components-contrib`, `microsoft/aspire`, `CommunityToolkit/Aspire`, `microsoft/fluentui-blazor` and `openbao/openbao`; Dapr source at tag `v1.18.4` and on `master`; NuGet flat-container and registration endpoints; and the Dapr docs (v1.18 banner, every page "Last modified October 6, 2026"). All lookups were made on 2026-10-07.
- **Prior check:** `reconcile-update-2026-10-07-stack-reality.md` was re-verified rather than trusted. Its version claims hold. This review adds 1 new Aspire patch, 1 OpenBao advisory the reconcile missed (`GHSA-gf3j-hm38-jhh5`), and several Dapr behaviour facts that it did not cover.

## Verdict

The Stack table's version cells are accurate for the repository and current upstream as of this morning, apart from Aspire `13.6.1`, which was published to NuGet at 06:03 UTC today. Four decisions rest on unverified or wrong technology assumptions:

- The spine understates the DAPR runtime floor and misses a `1.14.4` Kubernetes guide.
- It conflates Dapr's two `dapr-api-token` mechanisms.
- It assumes Dapr component scopes can prove the AD-5 "actor-only writer" envelope, which they cannot.
- It names a phantom `ReleaseEvidenceCodec` as owned by Builds.

There are **0 critical, 4 high, 6 medium and 8 low** findings.

## Critical

None.

## High

### H1 — The DAPR runtime floor is understated, and the only Kubernetes install guide pins an unsupported `1.14.4`

- **Spine:** Stack row `architecture.md:427` ("deployment examples `1.18.0` … predate the 1.18.2 CVE fixes"). Gate row `:548` names only `deploy/README.md` and the Docker Compose guide.
- **Reality:**
  - `docs/guides/deployment-kubernetes.md:143` (`dapr init -k --runtime-version 1.14.4`) and `:151` (Helm `--version 1.14.4`) are the Kubernetes instructions. Kubernetes is the AD-26 target mode. The Dapr support policy (N-2) lists `1.14.x` as **Unsupported** (https://docs.dapr.io/operations/support/support-release-policy/, modified 2026-10-06). The same guide says "four DAPR system pods" (`:88,154,160`), which omits the Scheduler.
  - The pin is a functional floor as well as a CVE gap. Before `1.18.2`, Scheduler-backed reminders, the default since 1.15, validated every `||` segment of the reminder name, including the actor ID, against DNS-1123. Any character outside `[a-z0-9.-]` failed (https://github.com/dapr/dapr/releases/tag/v1.18.2, 2026-07-21; PR dapr/dapr#10120). EventStore's actor ID is `{TenantId}:{Domain}:{AggregateId}` (`src/Hexalith.EventStore.Contracts/Identity/AggregateIdentity.cs:55`), and `AggregateActor` arms drain-recovery reminders (`src/Hexalith.EventStore.Server/Actors/AggregateActor.cs:1765,3866`). `:` is outside DNS-1123, so drain reminders probably fail to register on the `1.18.0` examples. This was not reproduced here.
  - `1.18.3` (2026-08-14) fixed "All sidecars in a namespace disconnected from Placement when a single sidecar disconnected mid-dissemination", which halted actors on healthy hosts (https://github.com/dapr/dapr/releases/tag/v1.18.3). That fix is a direct input to the AD-5 "single activation through placement failover" envelope (`:140`). CI runs `1.18.2` (`.github/workflows/integration.yml:25`). The latest stable release is `1.18.4` (2026-09-09).
- **Fix:** Record a minimum runtime of `≥1.18.3` (target `1.18.4`) as an AD-26/AD-5 input, citing the reminder-name and placement fixes. Add `docs/guides/deployment-kubernetes.md` to the `:548` contradictions row.

### H2 — Dapr's two `dapr-api-token` mechanisms are conflated (AD-18, AD-24, AD-28, AD-36)

- **Spine:** AD-18 `:244` has the handler replace outbound `dapr-api-token` "from trusted configuration". AD-28 `:342` has the receiver validate `dapr-api-token` against `APP_API_TOKEN`. AD-36 `:394` says every internal call "presents the receiver's AD-28 channel token". The AD-24 contract `:295` inventories "each receiver's `APP_API_TOKEN`" only. `DAPR_API_TOKEN` appears 0 times in the spine, the PRD and the epics.
- **Reality:** Dapr uses the same header for two secrets.
  - `DAPR_API_TOKEN` authenticates app → own sidecar. Kubernetes uses `dapr.io/api-token-secret` (https://docs.dapr.io/operations/security/api-token/).
  - `APP_API_TOKEN` authenticates sidecar → app. Kubernetes uses `dapr.io/app-token-secret` (https://docs.dapr.io/operations/security/app-api-token/).
  - The receiver's sidecar stamps its own `APP_API_TOKEN` (`pkg/channel/http/http_channel.go:649-650` at v1.18.4) and does not forward the caller's token (dapr integration test `tests/integration/suite/daprd/serviceinvocation/http/appapitoken/remotereceivernotoken.go`). A caller therefore never "presents the receiver's channel token".
  - The code already follows Dapr. The outbound token is `DAPR_API_TOKEN`: `samples/Hexalith.EventStore.Sample.Api/Program.cs:34`, `src/Hexalith.EventStore.Admin.UI/AdminUIServiceExtensions.cs:113`, and the `EventStoreServiceCollectionExtensions.cs:61` docs ("DAPR API token"). The AppHost comment `src/Hexalith.EventStore.AppHost/Program.cs:419` says the app-channel token is "shared only with its own sidecar".
  - The Kubernetes sample `samples/deploy/kubernetes/dapr-annotations-example.yaml:20` sets only `app-token-secret`.
- **Risk:** Read literally, AD-36 hands every receiver's `APP_API_TOKEN` to its callers. The production profile and the OpenBao contract also omit the sidecar API token.
- **Fix:** Name both tokens. Rewrite AD-36 so the channel token is sidecar-stamped and the caller supplies only its own `DAPR_API_TOKEN` plus the assertion. Add `DAPR_API_TOKEN` to the AD-24 contract and the AD-26 profile, or record an owner decision to leave it off.

### H3 — Component scopes and ACLs cannot prove the AD-5 no-second-writer envelope or the AD-1 "no private actor-key access" rule; `keyPrefix` is unmentioned and drifts between Dev and production

- **Spine:**
  - AD-1 `:114`: "application code never reads or writes Dapr private actor-state keys".
  - AD-5 `:140`: the envelope covers "the actor-only `IActorStateManager` write path, component scopes and ACLs".
  - AD-26 `:326` binds `statestore`/`actorStateStore: true`.
  - `keyPrefix` appears 0 times.
- **Reality:**
  - Actor state keys are `<App ID>||<Actor type>||<Actor id>||<state key>` in the same component (https://docs.dapr.io/reference/api/state_api/).
  - Component `scopes` are per component, not per key.
  - With `keyPrefix: none`, every scoped app addresses raw keys (https://docs.dapr.io/developing-applications/building-blocks/state-management/howto-share-state/).
  - Even with the default `appid` prefix, the `eventstore` app's own state-API key `AggregateActor||<id>||<k>` becomes the actor key.
- **Repository:**
  - The Dev actor store sets `keyPrefix: none` and scopes `eventstore`, `eventstore-admin` and `tenants` (`src/Hexalith.EventStore.AppHost/DaprComponents/statestore.yaml:41-43`; `src/Hexalith.EventStore.Aspire/HexalithEventStoreExtensions.cs:190,231` "keys are shared across appIds").
  - The production example `deploy/dapr/statestore-postgresql.yaml:20-35` omits `keyPrefix` (default `appid`) but still scopes `eventstore-admin`.
  - Admin therefore reads different keys in Dev and in production, which is AD-9 drift.
- **Fix:** In AD-5/AD-26, either require a dedicated actor-state component scoped to `eventstore` only, with `keyPrefix` bound into the profile digest, or state that scopes and ACLs cannot falsify a second writer and that the writer inventory must be proven at code level (Story 3.17/4.16).

### H4 — `ReleaseEvidenceCodec` is a phantom artifact stated as Builds-owned

- **Spine:** AD-11 `:192`: "The SHA-pinned shared Builds publisher/validator owns … `ReleaseEvidenceCodec`". Epics ACs depend on it (`epics.md:2934,2963`).
- **Reality:** There are zero hits in EventStore `src/`, `tools/` or `tests/`, and none in `references/Hexalith.Builds` at gitlink `ba4ca78c`. Builds has `Github/publish-containers/oci_registry_validator.py` and `publication_preflight.py`, but no codec. The spine does not declare the codec absent. This is the same class as the earlier "AD-19 phantom types" trap.
- **Fix:** Declare `ReleaseEvidenceCodec` absent with an owning story (Builds or 3.19), or bind AD-11 to the existing Builds validator identity.

## Medium

### M1 — The OpenBao Stack row reports a repository value that does not exist, and the Dapr Vault component's limits are unrecorded

- **Spine:** The `:430` "Repository value" cell reads `secretstores.hashicorp.vault` v1, but there are zero `openbao` hits in `src/` or `deploy/`. `deploy/dapr/openbao-secret-contract.yaml` is absent, and AD-24 `:295` only implies its absence ("Missing contract … blocks").
- **Upstream:**
  - The Vault component is Stable v1 and OpenBao is listed Stable through it, since 1.16 (https://docs.dapr.io/reference/components-reference/supported-secret-stores/).
  - It authenticates only with `vaultToken` or `vaultTokenMountPath`. The docs mention no Kubernetes or AppRole auth and no token renewal.
  - Secrets referenced in a component definition are resolved only at initialization: "Rotating the secret in Vault does not trigger a reload" (https://docs.dapr.io/reference/components-reference/supported-secret-stores/hashicorp-vault/).
- **Fix:** Mark the row "specified; no committed component or contract". Record token-only auth, init-time resolution and the bootstrap-token TTL constraint in AD-24 or the `:542` row.

### M2 — The production component examples contradict AD-24, and `{env:…}` is not Dapr syntax

- **Repository:**
  - `deploy/dapr/statestore-postgresql.yaml:23-26` holds the credential-bearing connection string as `value: "{env:POSTGRES_CONNECTION_STRING}"`.
  - `deploy/dapr/accesscontrol*.yaml` use `{env:DAPR_TRUST_DOMAIN|…}`.
  - No component has `auth.secretStore: openbao` or a `secretKeyRef`.
- **Dapr:** Dapr interpolates only `{uuid}`, `{podName}`, `{namespace}` and `{appID}` (https://docs.dapr.io/reference/resource-specs/component-schema/). Native references are `secretKeyRef` and `envRef` (`pkg/apis/common/namevalue.go`). The repository's own Dev component already notes this (`AppHost/DaprComponents/statestore.yaml:30`).
- **Fix:** Add the `deploy/dapr/*.yaml` components to the `:548` contradictions row, not just `deploy/README.md`.

### M3 — The Dapr Cryptography API is alpha, and the spine does not record it

- **Spine:** AD-23 `:287` relies on "Dapr key-operation qualification" (Story 8.6). The exceptions register lists `crypto.azure.keyvault`.
- **Reality:**
  - The API is "alpha", in place since v1.11, at HTTP `v1.0-alpha1/crypto` (https://docs.dapr.io/operations/support/alpha-beta-apis/; https://docs.dapr.io/developing-applications/building-blocks/cryptography/cryptography-overview/).
  - The high-level Encrypt/Decrypt calls emit **Dapr Crypto Scheme v1**. The name is verified correct, and AD-23 forbids it as a `pdenc-v2` replacement.
  - Exact-version wrap and unwrap exist only as gRPC `Subtle*Alpha1` RPCs. The .NET SDK's `DaprClientGrpc.cs` references `SubtleWrapKeyAlpha1`.
- **Fix:** Record the alpha status and the Subtle-API dependency so that Story 8.6 expects a likely AD-34 exception or an unavailable outcome.

### M4 — The Dapr Scheduler is missing from the AD-26 profile digest and the restore posture, and 1.19 changes the placement authority

- **Dapr:** Actor reminders have been Scheduler-backed, using embedded etcd, by default since 1.15 (v1.18.2 notes). EventStore drain recovery and DomainService typed reminders depend on them (`src/Hexalith.EventStore.DomainService/ReminderActor.cs`, `AggregateActor.cs:1765`).
- **Spine:** AD-26 `:330` binds placement and failover but not Scheduler HA, storage or backup. The RTO/RPO row `:540` is silent on it.
- **Upcoming:** Dapr `1.19.0-rc.1` (2026-10-01) adds "Placement -> Scheduler" placement authority (PRs dapr/dapr#10364 and #10440). The AD-5 envelope proof is therefore specific to the runtime implementation.
- **Fix:** Bind the Scheduler configuration and its restore into the profile. Require the envelope to be re-proven on any change of runtime minor version.

### M5 — `dapr-caller-app-id` semantics in 1.18.x, and an undeclared AD-28 non-conformance in Operations

- **Verified behaviour:** The receiving sidecar stamps `dapr-caller-app-id`, `dapr-caller-namespace` and `dapr-callee-app-id` on cross-app calls (dapr test `serviceinvocation/http/appapitoken/remoteheaders.go`). In 1.18.x, self-invocation short-circuits to `invokeLocal` without stamping them. The fix is only in `1.19.0-rc.1` (PR dapr/dapr#10139, merged 2026-07-03). AD-28's "when present … serves only to deny" is consistent with this behaviour.
- **Gap:** `src/Hexalith.EventStore.Operations/Endpoints/DeadLetterOperationsEndpointExtensions.cs:227-235` authorizes when `dapr-caller-app-id` matches and any non-empty bearer is present, without validating the bearer. AD-31 `:360` gates Operations as non-production, but the spine does not list this as an AD-28 non-conformance, the way row `:529` does for the gateway routes.
- **Fix:** Add it to the `:529` row or the AD-31 row.

### M6 — MediatR license posture is unrecorded

- **Reality:** MediatR `14.2.0` (catalog `:194`) is licensed under RPL-1.5 or a commercial Lucky Penny Software agreement (nupkg `LICENSE.md`; https://luckypennysoftware.com/license). The released `Server` and `Gateway` packages reference it (`src/Hexalith.EventStore.Server/Hexalith.EventStore.Server.csproj:37`, `src/Hexalith.EventStore.Gateway/Hexalith.EventStore.Gateway.csproj:49`). The host suppresses the license log (`src/Hexalith.EventStore/appsettings.json:6`, `"LuckyPennySoftware.MediatR.License": "None"`).
- **Spine:** The Stack row `:431` says only "Repository authority".
- **Fix:** Record an owner license decision for MediatR and its consumers.

## Low

- **L1 — Aspire `13.6.1` is out.** NuGet published `Aspire.Hosting` and `Aspire.Hosting.Redis` `13.6.1`, plus Keycloak and Kubernetes `13.6.1-preview.1.26506.6`, at 2026-10-07 06:03 UTC. No GitHub release or notes exist yet. Row `:425`, "Current", is hours stale. A tested Builds refresh is still required.
- **L2 — FrontComposer `4.6.0` exists.** It was published on NuGet 2026-10-05 and its nuspec depends on Fluent UI `5.0.0`. The pinned `4.5.0` depends on `5.0.0-rc.5-26219.1`, and the FrontComposer submodule is already at `c561b321`. Row `:432` says "Builds-owned" but does not mention that `4.6.0` is available.
- **L3 — Deployment-doc hygiene.**
  - `docs/guides/deployment-docker-compose.md:294` also uses `daprio/placement:latest`, not only `daprd:latest` (`:226,252,273`).
  - The `deploy/README.md:254-305` override uses the deprecated `-components-path` flag (marked deprecated as "Alias for --resources-path" in daprd v1.18.4 `cmd/daprd/options/options.go:138-139`) and sets no `APP_API_TOKEN`.
- **L4 — Minor lags.** The DAPR CLI is `1.18.0` (`integration.yml:24`; Builds `Github/dapr-init/action.yml:8`) against the latest `1.18.2` (2026-08-21). CodeCoverage `18.12.0` and Microsoft.Identity.Web `4.16.0` are available. All are catalog-owned.
- **L5 — Source list gaps.** Claims lack frontmatter sources: the v1.18.2 CVE claim (https://github.com/dapr/dapr/releases/tag/v1.18.2), Fluent UI v5.0.0 GA (https://github.com/microsoft/fluentui-blazor/releases/tag/v5.0.0, 2026-09-28), Aspire 13.6.0, the CommunityToolkit NuGet page, the Dapr crypto overview (AD-23), the Dapr API-token page (AD-18), and the actors features/concepts page (AD-5). All 8 cited URLs resolve with HTTP 200.
- **L6 — AD-33 schema and codec are absent and undeclared.** AD-33 `:372` says `Contracts` "owns the schema and versioned canonical codec" for the routing envelope. `src/Hexalith.EventStore.Contracts` has no route-catalog schema or codec; only `Server/Projections/NamedProjectionRouteCatalog.cs` and `Client/Projections/ProjectionRouteCatalogFingerprint.cs` exist. Row `:539` implies the file is future work but does not say the schema and codec are absent.
- **L7 — Transactional outbox.** AD-7 `:152` and AD-34 `:380` forbid a transaction that spans pub/sub and prescribe durable intent plus readback. Dapr ships a transactional outbox for the state-transaction API only, not for actor state (https://docs.dapr.io/developing-applications/building-blocks/state-management/howto-outbox/). Under AD-1's "highest applicable Dapr abstraction" rule, the spine should record why the outbox is not used for non-actor read-model or projection paths.
- **L8 — OpenBao floor confirmed.** The `2.7.1`/`2.6.4` floor (row `:430`) covers the 2026-10-01 advisories, including `GHSA-gf3j-hm38-jhh5` (Kubernetes auth renews revoked tokens), which the prior reconcile missed. It also covers the 2026-09-23 critical RCE `GHSA-j6wc-jpvg-xfxq` / CVE-2026-104090 and the high ACL bypasses fixed in `2.7.0`/`2.6.3`. No change is needed. The floor could cite https://github.com/openbao/openbao/security/advisories.

## Verified As Claimed

| Claim (spine line) | Evidence (2026-10-07) |
| --- | --- |
| .NET SDK `10.0.401`, `rollForward: latestPatch`; runtime and ASP.NET `10.0.12` security release 2026-09-08 (`:422-424`) | `global.json:3-4`; releases.json `latest-release` 10.0.12, six CVEs, support phase `active`; `Directory.Build.props:60` `net10.0` |
| Aspire / AppHost SDK `13.6.0`; Keycloak and Kubernetes preview (`:425`) | catalog `:135,139-140`; `src/Hexalith.EventStore.AppHost/Hexalith.EventStore.AppHost.csproj:1` |
| CommunityToolkit DAPR `13.6.0-preview.1.261001-0243`, no later stable (`:426`) | catalog `:17,158`; NuGet latest stable is `13.0.0` |
| DAPR CI `1.18.2` / CLI `1.18.0`; `1.18.4` latest stable 2026-09-09 (`:427`) | `integration.yml:24-25`; GitHub: `1.18.5-rc.2` and `1.19.0-rc.1` are prerelease; no GHSA affects 1.18.x (latest GHSA-85gx-3qv6-4463 affects ≤1.17.4) |
| Dapr .NET SDK `1.18.10` current (`:428`) | catalog `:161-168`; GitHub 2026-09-26; NuGet latest stable |
| `state.postgresql` v1 Stable, no deprecation, v2 incompatible with no migration (`:429`, AD-26) | setup-postgresql-v1 page; supported-state-stores table: v1 Stable with Transactional, ETag, TTL and Actors; `deploy/dapr/statestore-postgresql.yaml:20-29` |
| Fluent UI `5.0.0` GA, RC exception retired (`:432`) | catalog `:250-251`; GitHub v5.0.0 2026-09-28; NuGet stable |
| OpenTelemetry `1.19.1` / instrumentation `1.19.0`; MediatR `14.2.0`; FluentValidation `12.1.1`; xUnit v3 `4.0.1`, Shouldly `4.3.0`, NSubstitute `6.2.0`; CodeCoverage pin `18.11.2` | catalog `:290-297,194,173,343,318,283,267`; all latest stable except CodeCoverage (`18.12.0` available) |
| `APP_API_TOKEN` → `dapr-api-token`, startup-loaded, rotation needs a restart or rollout (AD-24/AD-28) | app-api-token page; `http_channel.go` stamps the token on `InvokeMethod` (pub/sub, actors, `/dapr/config`); the app health probe carries no token, consistent with AD-16 anonymous `/alive` (`HexalithEventStoreDomainModuleExtensions.cs:14`) |
| Transactions are scoped to one component (AD-7/AD-34) | `POST /v1.0/state/<storename>/transaction` (state API reference) |
| Only one actor state store; actor placement does not guarantee single activation, so the AD-5 envelope must be proven | state-management overview; the actors features/concepts page gives no at-most-one guarantee across rebalancing |
| "Dapr Crypto Scheme v1" is the real name (AD-23) | cryptography overview |
| `secretstores.hashicorp.vault` serves OpenBao (AD-24) | openbao page: "no dedicated OpenBao Secrets Store … tested and confirmed" |
| AD-25 Folders authority bytes | `Hexalith/Hexalith.Folders@a9cfea91:docs/exit-criteria/oq8-idempotency-design.md` SHA-256 `1a55b030…dcd8` matches (10,474 bytes) |
| AD-23 payload spec full-file SHA-256 `542f0b6e…3c82` | matches the worktree and `HEAD` |
| Builds gitlink `ba4ca78c` (`:418`) | `git ls-tree HEAD references/Hexalith.Builds` |

## Named Repository Artifacts

| Artifact (spine line) | State | Declared in spine? |
| --- | --- | --- |
| `docs/architecture/dapr-infrastructure-exceptions.yaml` (`:380-382`) | exists; `accepted_exceptions: []`, 2 unresolved rows | yes ("register is empty") |
| `tools/release-packages.json` (`:182,186`) | exists, 14 packages | yes |
| `tools/validate-consumer-removal-authority.py`, `_bmad-output/implementation-artifacts/evidence/consumer-removal-manifest.json` (`:281`) | absent | yes, future (Story 3.20 builds them, `:531`) |
| `deploy/dapr/production-profile.yaml` (`:330`) | absent | yes ("The file is absent") |
| `deploy/dapr/eventstore-routing-catalog.json` (`:372`) | absent | implied future (`:539`); schema and codec absence undeclared (L6) |
| `deploy/dapr/openbao-secret-contract.yaml` (`:295`) | absent | only implied ("Missing contract blocks"); see M1 |
| `ReleaseEvidenceCodec` (`:192`) | **absent everywhere** | **no** (H4) |
| `docs/reference/aot-and-trimming-posture.md` (`:546`) | absent | yes, future (Story 6.7) |
| `JwtBearerAuthenticationContract` (`:176`) | `src/Hexalith.EventStore.ServiceDefaults/Authentication/JwtBearerAuthenticationContract.cs:14` | n/a |
| `IQueryCursorCodec`, `QueryCursorScope` (`:152`) | `src/Hexalith.EventStore.Client/Queries/IQueryCursorCodec.cs:21` (+9 refs) | n/a |
| `ProjectionDispatchResponse`, `ProjectionDispatchOutcome` (`:263`) | `src/Hexalith.EventStore.Contracts/Projections/ProjectionDispatchResponse.cs:8` (+55 refs) | n/a |
| `AddEventStoreDaprServiceInvocation` (`:250`) | `src/Hexalith.EventStore.Client/Registration/EventStoreServiceCollectionExtensions.cs:63` | n/a |
| `IPersonalDataPolicy`, `IErasureStateProvider`, `IEventStoreGatewayClient`, `AddEventStoreDomainService`/`UseEventStoreDomainService` | present in Contracts, Client and DomainService | n/a |
| `Hexalith.EventStore.Operations` (`:360,454`) | project exists in `.slnx:35`; not in AppHost | yes (AD-31 non-production) |
| `Hexalith.EventStore.PayloadProtection` / `.AzureKeyVault` (`:463`) | core exists / adapter absent | yes |
| `references/Hexalith.McpCli` (AD-35) | root submodule gitlink `e159f82b` | n/a |
| Story 6.6 Dapr-only and trusted-code amendments; `6-5-integration/metadata-adapter-contract.md`; `spec-shared-payload-protection-dapr-amendment-2026-10-05.md`; `4-8-eventstore-oq8-platform-evidence.yaml` | all exist and are tracked | n/a |
| Gateway `MapSubscribeHandler` / `MapActorsHandlers` without authorization (`:529`) | `src/Hexalith.EventStore/Program.cs:47-48` | yes, as a non-conformance |
| Frontmatter sources | all present; `sprint-change-proposal-2026-10-07.md` is untracked (uncommitted session work) | n/a |
