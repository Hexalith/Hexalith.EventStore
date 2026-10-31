---
title: Restore provider verification query catalog fixture
type: bugfix
created: 2026-10-31
status: done
baseline_commit: 77e949a9d54444c43dcef2da17ea65f8c3674385
context:
  - /home/administrator/projects/hexalith/parties/references/Hexalith.EventStore/AGENTS.md
  - /home/administrator/projects/hexalith/parties/references/Hexalith.EventStore/CONTRIBUTING.md
  - /home/administrator/projects/hexalith/parties/references/Hexalith.AI.Tools/hexalith-llm-instructions.md
  - /home/administrator/projects/hexalith/parties/references/Hexalith.AI.Tools/hexalith-git-instructions.md
---

## Intent

FrontComposer Quality41180070793 fails its real EventStore provider verification: all nine authenticated query interactions fail, while commands and tenant/RBAC denial pass. Actual Kestrel query-fresh-data returns503 with Catalog authority cannot safely admit this operation. The provider fixture never initializes the catalog authority now required by production QueriesController and its principal lacks the new authenticated eventstore:surface claim.

Supply only this isolated test fixture with bounded validated committed schema-three catalog authority for its existing synthetic orders/GetOrders route and declared tenant scopes. Give its synthetic FrontComposer caller explicit ui surface. Add meaningful real HTTP success/cache and denial coverage, then rerun all19 existing committed FC interactions without editing their expectations. User authorizes necessary CI fixes, owner commit/push and publication without another human checkpoint; implementation handoff must stop before commit/push.

## Boundaries

Preserve production controller/catalog enforcement, tenant/RBAC checks, current credential checks, existing Pact inputs, response/cache semantics and content-free verifier diagnostics. Never initialize nested submodules, load any skill under references, disable catalog/surface gates, accept arbitrary routes/tenants, or add permissive catch-all catalog behavior. Keep each new C# type in its own file. Use exact source API inspection, not guessed signatures. Read owning build/configuration guidance before edits. No production source edits, dependency changes, commits, pushes, release dispatch or package publication in this handoff.

## Code Map and Scope

- tests/Hexalith.EventStore.ProviderVerification/ProviderVerificationHost.cs: production Kestrel pipeline and existing controlled fixture DI dependencies; install the bounded catalog alongside existing query router overrides.
- tests/Hexalith.EventStore.ProviderVerification/ProviderVerificationAuthenticationHandler.cs: synthetic test principal has sub/tenant/domain/permissions but no eventstore:surface; preserve credentials and denial behavior.
- tests/Hexalith.EventStore.ProviderVerification/ProviderVerificationRouteCatalog.cs: new bounded fixture authority using actual SDK catalog validation/commit models, matching only declared synthetic query and tenant fixtures.
- tests/Hexalith.EventStore.ProviderVerification.Tests/RealKestrelPactTests.cs: existing real HTTP tests only cover commands; add source query success/cache and missing-surface/undeclared-route controls through the real pipeline.
- tests/Hexalith.EventStore.ProviderVerification/ProviderVerificationCredential.cs: only if needed, a bounded fixture-only way for tests to omit surface without an untrusted request header or weakening production claims.
- This spec records exact validation and any necessary test-only file scope extension. Inspect relevant tracked csproj/Directory.Build.props/targets, editorconfig and gitattributes first.

## Acceptance and Verification

Given declared synthetic query, correct credential, tenant permissions and ui surface, a request through real Kestrel passes production routing and preserves existing ETag/cache responses. Given missing surface or undeclared route, the same real pipeline denies admission without calling the controlled router. All19 unchanged FrontComposer Pact interactions succeed with setup/readiness/teardown complete.

Build ProviderVerification.Tests in Release/package mode with warnings as errors and no nested initialization; run the individual test executable, then the provider CLI live-compatibility mode using absolute FC Pact, interaction-manifest and provider-state-catalog paths. Match the actual source mode identity; the3.128.4+commit report identifies a source-built gateway, not consumption of the published Gateway package. Baseline report is /tmp/fc-provider-verification-failed.json. Record exact commands, counts and any blockers here. Parent handles independent review, validated Conventional Commit and pushes after handoff.

## Implementation Notes

- Added `ProviderVerificationRouteCatalog` as a controlled provider-only dependency. Its SDK-validated sealed schema-three snapshots contain one exact `orders/GetOrders` route and select only `tenant-contract-a` or `tenant-contract-case` according to the active declared provider state. Other tenants, domains, discriminators and lifecycle mutation requests are refused. No production source or package dependencies changed.
- The synthetic caller now carries authenticated `eventstore:surface=ui`. `ProviderVerificationCredential.IncludeSurface` permits tests to omit that claim through the per-run fixture credential, never through a request header.
- Scope extension: `StatefulQueryRouter.InvocationCount` and the host's internal counter accessor prove denied HTTP requests never call the controlled router. Added `ProviderVerificationRouteCatalogTests.cs` for exact scope/digest checks, and seven Kestrel cases for fresh data, cached304, propagated tenant, absent surface, undeclared projection/query and outside tenant. Existing command fixture behavior remains intact.
- Validation from `/home/administrator/projects/hexalith/parties/references/Hexalith.EventStore` on2026-10-31:
  - `dotnet build tests/Hexalith.EventStore.ProviderVerification.Tests/Hexalith.EventStore.ProviderVerification.Tests.csproj --configuration Release -m:1 -p:NuGetAudit=false` exited0, zero warnings/errors.
  - `dotnet tests/Hexalith.EventStore.ProviderVerification.Tests/bin/Release/net10.0/Hexalith.EventStore.ProviderVerification.Tests.dll` exited0:78 passed, zero errors/failures/skips/not-run.
  - `git diff --check` exited0.
  - The full live CLI command below exited0. `/tmp/eventstore-provider-fixture-live.json` reports verdict`passed`,19/19 interactions passed, complete setup/teardown, matching runtime identity, successful startup/readiness/cleanup, stopped host and closed port, with no reason codes. Inputs were unchanged.

```bash
dotnet tests/Hexalith.EventStore.ProviderVerification/bin/Release/net10.0/Hexalith.EventStore.ProviderVerification.dll --verification-mode live-compatibility --pact-directory /home/administrator/projects/hexalith/parties/references/Hexalith.FrontComposer/tests/Hexalith.FrontComposer.Shell.Tests/Pact --manifest /home/administrator/projects/hexalith/parties/references/Hexalith.FrontComposer/tests/Hexalith.FrontComposer.Shell.Tests/Pact/interaction-manifest.json --provider-state-catalog /home/administrator/projects/hexalith/parties/references/Hexalith.FrontComposer/tests/Hexalith.FrontComposer.Shell.Tests/Pact/provider-state-catalog.json --report-output /tmp/eventstore-provider-fixture-live.json
```

Six ordinary existing JSON inputs were copied into the empty nested FrontComposer worktree for existing corpus-resolving tests, as CI does. Its `.git` remains absent; no nested submodule was initialized or updated. The source identity check scopes cleanliness to production source/build inputs, so these test-only edits retain legitimate live compatibility identity. This evidence claims current compatibility only, never historical migration approval or distributed production activation. Independent review, commits, pushes and hosted reruns remain with the parent handoff.

## Review Follow-up

- `ProviderVerificationRouteCatalog.AcquisitionCount` and its internal host accessor distinguish successful catalog acquisition from early authorization refusal. The existing authorized undeclared-projection HTTP case now proves one catalog acquisition and zero controlled-router invocations. Unknown-query and outside-tenant cases retain their earlier authorization denials and explicitly prove zero acquisitions. Missing-surface rejection proves one acquisition and zero router calls.
- A single authority/coordinator now runs ordinary and propagated tenant states sequentially in a focused test. Each new acquisition binds only the currently selected tenant; the prior tenant is rejected after the transition, and acquisitions are refused after both teardowns.
- A focused lifecycle test proves prepare, ready, commit, rollback and reconcile all refuse fixture mutation. A subsequent acquisition retains byte-identical canonical catalog content, digest and exact route/tenant inventory.
- Necessary scope extensions are limited to the fixture catalog/host counter seams, the existing two affected test classes, this spec and the compact report beside it. No production files or Pact expectations changed.
- Preserved the earlier complete successful live report at [provider-query-catalog-fixture-verification-2026-10-31.json](provider-query-catalog-fixture-verification-2026-10-31.json), outside evidence folders. It retains every original report field: all 19 interactions and their setup/teardown events, six input hashes, exact source/Builds/version identity and runtime match, startup/readiness/cleanup facts, stopped host and closed port. The exact full CLI command and 19/19 count are recorded above. This is the earlier full run; the parent performs full verification after this follow-up.
  - Retained compact report SHA-256: `be5d79d4e551b78184e6b588c5b86fb9b211bdcd787ba9b64eaefc6981ad123a`.
  - Original `/tmp` report SHA-256 before whitespace-only compaction: `34ee13e8a3af504104ba4264c4aa0bca99a46922afcfd3df9a41b8a1d07cd514`.
- Follow-up validation from the EventStore root: `dotnet build tests/Hexalith.EventStore.ProviderVerification.Tests/Hexalith.EventStore.ProviderVerification.Tests.csproj --configuration Release --no-restore -m:1 -p:NuGetAudit=false` exited zero with zero warnings/errors. Only the two affected test classes were then run with the exact command below: 14 passed, zero errors/failures/skips/not-run. `git diff --check` also passed.

```bash
dotnet tests/Hexalith.EventStore.ProviderVerification.Tests/bin/Release/net10.0/Hexalith.EventStore.ProviderVerification.Tests.dll -class Hexalith.EventStore.ProviderVerification.Tests.RealKestrelPactTests -class Hexalith.EventStore.ProviderVerification.Tests.ProviderVerificationRouteCatalogTests
```

Final parent verification after review follow-up: the complete provider test executable passed 80/80 tests, zero errors/failures/skips/not-run. The unchanged 19-interaction live CLI passed again; report `/tmp/eventstore-provider-final-live.json`.

Concurrent upstream integration: rebased onto `ed719ed52` and retained its unrelated production corrections. The intervening query-controller isolated activator registration is superseded by the bounded catalog fixture, so query tests continue through normal production controller activation and prove catalog acquisition. The upstream helper remains intact but is not registered by this fixture.

After integrating upstream `ed719ed52`, Release build again passed with zero warnings/errors; complete provider tests passed 80/80 and all 19 live interactions passed. The retained report is refreshed from `/tmp/eventstore-provider-integrated-live.json`, with exact source identity and all unchanged corpus hashes. Logs: `/tmp/eventstore-provider-integrated-build.log`, `/tmp/eventstore-provider-integrated-tests.log`, `/tmp/eventstore-provider-integrated-live.log`.

Hosted full CI caught the new record-with credential expression as unsupported by its tracked-secret scanner, despite the credential being generated per run. The optional fixture surface flag now enters the cryptographically random factory as a runtime boolean argument, preserving immutable credentials and the scanner unchanged. Provider80/80 tests and the full existing SecretsProtectionTests class pass; both builds have zero warnings/errors. Logs `/tmp/eventstore-provider-credential-tests.log`, `/tmp/eventstore-provider-secret-scan-tests.log`.
