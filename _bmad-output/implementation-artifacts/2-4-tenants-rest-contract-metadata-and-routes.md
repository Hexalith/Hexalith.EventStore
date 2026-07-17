---
created: 2026-07-15
story_id: "2.4"
story_key: 2-4-tenants-rest-contract-metadata-and-routes
status: review
split_from: 2-4-tenants-external-api-host-adoption
crosswalk: ../planning-artifacts/story-id-migration-2026-07-15.md
---

# Story 2.4: Tenants REST Contract Metadata And Routes

Status: review

The parent Story 2.4 spec records contract metadata, generator diagnostics, and focused
tests. This split child reviews only route/verb/tenant-source/entity/API-scope identity and
deterministic invalid/duplicate diagnostics. `done` requires the Tenants maintainer-approved
PR/commit, exact Tenants SHA, accepted contract scope, and focused test results. Historical
authority: `spec-2-4-tenants-external-api-host-adoption.md` and its implementation review.

### Review Findings

- [ ] [Review][Decision] [high] Split Story 2.4 lacks its required external acceptance evidence — The reviewed range pins Tenants commit `80d23613612088a0c3fee23eb149f34ce08e9729` and records focused tests, but no Tenants maintainer-approved PR/commit or explicit contract-scope acceptance is documented. The story cannot move to `done` until that external approval is supplied or the acceptance gate is deliberately changed.
- [ ] [Review][Decision] [high] The external API host receives unused reusable service-account credentials — `tenants-api` only validates and forwards the caller bearer, but the mandated `WithEventStoreClientCredentials` call also injects client ID, username, and password. Decide whether to replace it with validation-only authority/audience wiring or document and implement the intended service-account use.
- [ ] [Review][Patch] [high] Release builds use the packaged REST generator while tests silently use a local source analyzer, leaving shipped behavior unverified and allowing mixed package/source Debug graphs [references/Hexalith.Tenants/src/Hexalith.Tenants.Api/Hexalith.Tenants.Api.csproj:8]
- [ ] [Review][Patch] [medium] Source-mode `tenants-api` topology is guarded only by source-text assertions and is not compiled by normal CI [tests/Hexalith.EventStore.AppHost.Tests/Configuration/TenantsApiLaunchSettingsTests.cs:29]
- [ ] [Review][Patch] [medium] The Tenants domain-service boundary guard returns green when the required submodule is absent from CI [tests/Hexalith.EventStore.DomainService.Tests/DomainModuleAuthoringGuardrailTests.cs:394]
- [ ] [Review][Patch] [medium] The Tenants UI controller-boundary guard misses qualified, whitespace-formatted, and alternate MVC/controller syntax [references/Hexalith.Tenants/tests/Hexalith.Tenants.UI.Tests/TenantsUiCompositionTests.cs:163]
- [ ] [Review][Patch] [medium] The API minimal-endpoint guard misses alternate receivers and `MapFallback` variants [references/Hexalith.Tenants/tests/Hexalith.Tenants.IntegrationTests/TenantsApiStructuralTests.cs:19]
- [ ] [Review][Patch] [medium] Dependency boundary guards inspect literal project XML with case-sensitive matching instead of the evaluated MSBuild graph [references/Hexalith.Tenants/tests/Hexalith.Tenants.IntegrationTests/TenantsApiStructuralTests.cs:28]
- [ ] [Review][Patch] [medium] The root DAPR ACL guard uses an order-sensitive text parser that can ignore valid YAML operation mappings [tests/Hexalith.EventStore.AppHost.Tests/Configuration/TenantsApiLaunchSettingsTests.cs:86]
- [ ] [Review][Patch] [medium] Cancellation-token assertions prove only that captured tokens are cancelable, not that request cancellation reaches the gateway [references/Hexalith.Tenants/tests/Hexalith.Tenants.IntegrationTests/TenantsApiGeneratedControllerTests.cs:102]
- [ ] [Review][Patch] [medium] Symmetric-key startup failure branches have no behavioral host tests [references/Hexalith.Tenants/src/Hexalith.Tenants.Api/Program.cs:50]
- [ ] [Review][Patch] [medium] Production-style authority-mode JWT validation is not exercised [references/Hexalith.Tenants/src/Hexalith.Tenants.Api/Program.cs:25]
- [ ] [Review][Patch] [medium] Blank and whitespace-only DAPR endpoint/port fallback behavior is implemented but unverified [references/Hexalith.Tenants/tests/Hexalith.Tenants.IntegrationTests/TenantsApiGatewayHandlerTests.cs:25]
