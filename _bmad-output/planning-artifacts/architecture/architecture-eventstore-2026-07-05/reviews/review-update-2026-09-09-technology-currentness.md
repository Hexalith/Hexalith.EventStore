# Technology Currentness / Reality-Check Review

**Artifact:** `ARCHITECTURE-SPINE.md`  
**Reviewed:** 2026-09-09  
**Lens:** named-technology currentness, repository reality, and current-versus-target topology  
**Verdict:** **CONDITIONAL PASS** — no critical technology mismatch; one high-severity topology omission and one medium-severity false version-availability claim should be fixed before finalization.

## Findings

### HIGH — The diagram labeled as the current AppHost topology omits two normally composed services

- **Location:** Structural Seed, current topology diagram, lines 330-346, especially `Sample[sample + API + UI]` and the outgoing AppHost edges.
- **Evidence:** `src/Hexalith.EventStore.AppHost/Program.cs:151-188` resolves and composes both `tenants` and `tenants-api`; in ordinary run mode the path-based fallback at lines 157-161 does this even when `HEXALITH_TENANTS_SOURCE` is not defined. `tests/Hexalith.EventStore.AppHost.Tests/Configuration/TenantsApiLaunchSettingsTests.cs:138-167` verifies that both resources exist, that `tenants` has a Dapr sidecar referencing `statestore` and `pubsub`, and that `tenants-api` has an invocation-only sidecar and references EventStore. The diagram contains neither service. The optional `eventstore-test-subscriber` at `Program.cs:327-347` is less important and may remain omitted if the diagram says it shows the normal topology.
- **Impact:** The diagram is presented as repository truth, but an implementer using it alongside AD-9 and AD-33 can omit the `tenants`/`tenants-api` app IDs, Dapr ACL entries, and route-catalog fingerprints from topology work. That creates the exact cross-unit route and security drift those decisions are intended to prevent.
- **Fix:** Add `tenants` and `tenants-api` to the current subgraph. Show `tenants` using Redis-backed `statestore` and `pubsub`, and `tenants-api` invoking EventStore without direct component access. Alternatively, relabel the diagram as a deliberately partial “EventStore core and samples” view and explicitly state that Tenants is omitted; adding the nodes is safer because AD-33 makes app IDs load-bearing.

### MEDIUM — `Microsoft.CodeCoverage 18.11.0` is not available in the authoritative NuGet feed

- **Location:** Stack table, line 301: “`18.11.0` is available”.
- **Evidence:** The repository authority pins `Microsoft.CodeCoverage` `18.10.0` in `references/Hexalith.Builds/Props/Directory.Packages.props`. As of 2026-09-09, the official [NuGet V3 package-version index](https://api.nuget.org/v3-flatcontainer/microsoft.codecoverage/index.json) ends at `18.10.0`; a request for the `18.11.0` package returns HTTP 404.
- **Impact:** The spine creates a phantom dependency-refresh target and weakens trust in the dated stack audit. It may send the Builds owner into unnecessary update work.
- **Fix:** Replace the posture with “Current public NuGet version; retain until the next tested catalog refresh,” or remove the availability sentence entirely.

## Verified Claims

The following material claims were checked and need no correction:

- **.NET:** `global.json` pins SDK `10.0.400` with `latestPatch`; Microsoft's [.NET 10 release metadata](https://builds.dotnet.microsoft.com/dotnet/release-metadata/10.0/releases.json) reports the 2026-09-08 security release `10.0.12` and SDK `10.0.401`. The spine correctly separates repository state from an authorized dependency update.
- **Aspire and Dapr SDK:** the Builds catalog pins Aspire `13.5.3`, CommunityToolkit Dapr `13.5.0-preview.1.260825-0345`, and Dapr .NET SDK `1.18.5`. The pinned CommunityToolkit prerelease exists in the official NuGet feed, and the explicit preview exception is accurate. The official Aspire package feed lists `13.5.3`, and the official Dapr.Client feed lists `1.18.5` as the latest stable package.
- **Dapr runtime:** `.github/workflows/integration.yml:24-25` pins CLI `1.18.0` and runtime `1.18.2`; `deploy/README.md:243,261,279` uses `daprio/daprd:1.18.0` examples. Dapr's official [v1.18.3 release](https://github.com/dapr/dapr/releases/tag/v1.18.3) is marked latest, so the spine's current-versus-target wording is accurate.
- **PostgreSQL component:** `deploy/dapr/statestore-postgresql.yaml:18-29` uses component `statestore`, type `state.postgresql`, version `v1`, with `actorStateStore: true`. Dapr's official [PostgreSQL component documentation](https://docs.dapr.io/reference/components-reference/supported-state-stores/setup-postgresql-v2/) says v1 remains available and is not deprecated, while v1/v2 cannot use the same table and currently have no migration path. Retaining v1 behind a separate migration decision is reality-based.
- **OpenBao:** Dapr's official [OpenBao documentation](https://docs.dapr.io/reference/components-reference/supported-secret-stores/openbao/) confirms there is no dedicated OpenBao component and directs users to `secretstores.hashicorp.vault` v1, with `openbao` as a valid component name. AD-24 accurately describes a target production profile, not current delivery.
- **App-channel token:** Dapr's official [app API token documentation](https://docs.dapr.io/operations/security/app-api-token/) confirms `APP_API_TOKEN`, inbound `dapr-api-token`, and restart/rollout-based rotation. The spine's AD-24/AD-28 technology mechanics are accurate. Repository implementation is currently limited to `Hexalith.EventStore.Operations`, consistent with the spine treating the wider rule as required target state rather than completed delivery.
- **Current versus target:** local AppHost state and pub/sub are Redis-backed (`HexalithEventStoreExtensions.cs:185-202` and AppHost Dapr YAML); production PostgreSQL and RabbitMQ/Kafka/Service Bus files are templates under `deploy/dapr/`; OpenBao is not currently wired. The separate “AD-26 target, not current delivery evidence” subgraph correctly avoids presenting those target components as delivered.

## Severity Summary

| Severity | Count |
| --- | ---: |
| Critical | 0 |
| High | 1 |
| Medium | 1 |
| Low | 0 |

