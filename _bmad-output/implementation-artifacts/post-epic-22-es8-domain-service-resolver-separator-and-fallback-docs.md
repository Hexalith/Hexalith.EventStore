# Post-Epic 22 ES-8: DomainServiceResolver Separator And Fallback Docs

Status: done

Context created: 2026-05-27
Story key: `post-epic-22-es8-domain-service-resolver-separator-and-fallback-docs`
Source proposal: `_bmad-output/planning-artifacts/sprint-change-proposal-2026-05-27-eventstore-parties-review-residuals.md` (finding ES-8)
Epic: Post-Epic-22 EventStore<->Parties Review Residuals
Scope: Minor (Developer). Lock down the domain-service resolver's sanitized wildcard key contract and publish the resolver fallback order in docs and executable tests.

## Story

As an EventStore platform maintainer,
I want domain-service registration key formats and fallback order to be explicit, non-colliding, and tested,
so that downstream bounded contexts such as Parties can configure wildcard domain-service routing without relying on code comments or accidental key-shape behavior.

## Background & Verified Residual

ES-8 is a verified post-Epic-22 residual from the EventStore<->Parties review:

- `src/Hexalith.EventStore.Server/DomainServices/DomainServiceResolver.cs` already documents and implements fallback order in comments and code, but the order is not published in operator/developer docs and not fully locked by tests.
- The current sanitized wildcard registration key is `wildcard_{domain}_{version}`. This is currently narrow-risk because `AggregateIdentity` validates domains as lowercase alphanumeric plus hyphens only, and `DaprDomainServiceInvoker.ValidateVersionFormat` restricts versions to `v` plus digits. Still, the collision-safety rule lives implicitly across separate classes.
- Current development configuration uses pipe wildcard registrations such as `*|counter|v1` and `*|greeting|v1` in `src/Hexalith.EventStore/appsettings.Development.json`. Parties needs to re-verify `*|party|v1` after this change.
- Docs have drift: `docs/guides/configuration-reference.md` still presents `ConfigStoreName` as default `"configstore"`, while `DomainServiceOptions.ConfigStoreName` defaults to `null` and the resolver only calls DAPR configuration when the option is explicitly non-empty.
- `docs/concepts/event-versioning.md` says version-based routing uses the DAPR configuration store and static fallback registration, but the architecture now defines convention-first routing with static registrations before the opt-in config store.

This story should not change command processing semantics, DAPR service invocation, public REST contracts, query routing, payload protection, AppHost composition, package dependencies, or Parties code. It should make the existing resolver contract durable and understandable. Domain service resolution order is contractually significant and must remain stable unless code, docs, and tests are updated together.

## Acceptance Criteria

1. **Sanitized wildcard key contract is explicit and non-colliding.**
   - Given a sanitized wildcard registration is used for Kubernetes/env-friendly configuration
   - When a maintainer reads the code and docs
   - Then the accepted sanitized key shape is documented as one of:
     - current guarded form: `wildcard_{domain}_{version}`, valid only because `domain` excludes `_` and `version` matches `^v[0-9]+$`
     - or a replacement escaping/separator scheme that is mechanically non-colliding and backward-compatible where required
   - And `DomainServiceResolver` must not build an ambiguous sanitized key from an invalid domain or invalid version
   - And targeted tests prove domains containing `_` and malformed versions such as `1`, `v1_2`, and `v01x` cannot reach a sanitized wildcard lookup path
   - And the implementation records whether domain validation belongs inside `DomainServiceResolver` or remains an upstream invariant; if upstream, tests or cited production call paths must prove domains are validated before resolver lookup
   - And if domains ever allow `_`, sanitized wildcard key safety must be revisited because `wildcard_{domain}_{version}` can become ambiguous
   - And if the implementation changes the sanitized key shape, the old `wildcard_party_v1`/`wildcard_counter_v1` compatibility impact is documented and tested.

2. **Resolver fallback order is a documented contract and executable, not comment-only.**
   - Given exact, wildcard, sanitized wildcard, config-store, and convention registrations can all exist
   - When `ResolveAsync` runs
   - Then docs and tests prove this fixed precedence order:
     1. exact static registration keyed by `tenant:domain:version`
     2. exact static registration keyed by `tenant|domain|version`
     3. pipe wildcard static registration keyed by `*|domain|version`
     4. sanitized wildcard static registration keyed by the selected sanitized key shape
     5. opt-in DAPR config-store lookup when `ConfigStoreName` is non-empty
     6. convention fallback: `AppId = domain`, `MethodName = "process"`
   - And exact registrations always beat wildcard registrations
   - And pipe wildcard registrations beat sanitized wildcard registrations while both forms are supported
   - And static registrations beat DAPR config-store entries
   - And convention fallback is tested to return `AppId = domain` and `MethodName = "process"` after all explicit registrations and opt-in config-store lookup miss.

3. **Config-store optionality is documented truthfully.**
   - Given `DomainServiceOptions.ConfigStoreName` defaults to `null`
   - When docs describe domain-service routing
   - Then docs state that config-store routing is opt-in, not the default
   - And tests prove `ConfigStoreName = null`, empty string, or whitespace skips DAPR config-store lookup
   - And docs state that absent or unavailable config store falls through to convention routing only after static registrations miss
   - And docs avoid calling static registrations a "fallback" after config store where the code checks static registrations first.

4. **Developer/operator docs publish supported registration key formats.**
   - `docs/guides/configuration-reference.md` must document supported static registration keys:
     - `tenant|domain|version` for config-friendly exact registrations
     - `tenant:domain:version` for canonical exact registrations where the configuration source supports colons
     - `*|domain|version` for wildcard tenant registrations
     - the selected sanitized wildcard form for Kubernetes/env-friendly wildcard registrations
   - The docs must name `*|domain|version` as the pipe wildcard form and `wildcard_{domain}_{version}` as the sanitized wildcard form so readers do not confuse the two.
   - The docs must clarify that `tenant:domain:version` is a canonical resolver key but is not recommended for JSON/env configuration sources that treat `:` as a hierarchy separator; `tenant|domain|version` is the recommended config-friendly exact key.
   - `docs/guides/dapr-component-reference.md` configuration-store section must align with the opt-in resolver behavior and must not imply AppHost wires `configstore` by default.
   - `docs/concepts/event-versioning.md` must describe the real precedence order for version routing and rollback, or point to one canonical section that lists the same order.
   - `docs/guides/configuration-reference.md` and `docs/concepts/event-versioning.md` must publish the same fallback order and key formats.
   - `_bmad-output/planning-artifacts/architecture.md` D7 service-discovery note must be updated if the selected sanitized key shape, collision guard, or docs wording differs from the current architecture text.

5. **Regression tests cover docs and Parties-relevant examples.**
   - Add or update focused tests in `tests/Hexalith.EventStore.Server.Tests/DomainServices/DomainServiceResolverTests.cs` for fallback order and collision guard/key-shape behavior.
   - Add at least one options-binding test using real `IConfiguration` binding for `EventStore:DomainServices:Registrations`, proving pipe wildcard and sanitized wildcard keys materialize into `DomainServiceOptions.Registrations` as expected; direct dictionary construction alone is not enough evidence.
   - Add or update documentation/static tests in an existing server-test location, preferably near `DaprComponentValidationTests` if extending the current doc-drift guard pattern.
   - Tests must include a Parties-shaped registration example such as `*|party|v1` and/or the selected sanitized form for `party` v1.
   - Tests must fail if docs again claim `ConfigStoreName` defaults to `"configstore"` or omit the selected sanitized wildcard key format.

6. **Runtime behavior remains stable.**
   - `DaprDomainServiceInvoker` still extracts `domain-service-version`, defaults to `v1`, lowercases versions, validates `^v[0-9]+$`, and passes tenant/domain/version to `IDomainServiceResolver`.
   - `DomainServiceRegistration` shape remains unchanged.
   - No new package dependency is added for this story.
   - Test scope stays targeted to resolver/docs behavior; no Aspire/AppHost changes are expected.
   - Domain services still receive no direct state-store/pubsub access.
   - DAPR access-control YAML, statestore/pubsub YAML, query routing, command status, and public contracts remain unchanged.

7. **Validation evidence is recorded.**
   - Run the targeted resolver/doc test set.
   - Run at least `dotnet test tests/Hexalith.EventStore.Server.Tests/Hexalith.EventStore.Server.Tests.csproj --filter "FullyQualifiedName~DomainServiceResolverTests|FullyQualifiedName~DomainServiceIsolationTests|FullyQualifiedName~DaprComponentValidationTests" --no-restore`.
   - Run `dotnet build src/Hexalith.EventStore.AppHost/Hexalith.EventStore.AppHost.csproj --no-restore` if AppHost/architecture docs are touched in a way that changes runtime assumptions; otherwise record why it is not necessary.
   - Aspire live runtime validation is not required unless AppHost code changes, but if attempted and Docker is unavailable, record the exact blocker.

## Tasks / Subtasks

- [x] **ST0 - Reconfirm current resolver and documentation drift.** (AC: 1, 2, 3, 4)
  - [x] Re-read `DomainServiceResolver.cs`, `DomainServiceOptions.cs`, `DomainServiceRegistration.cs`, and `DaprDomainServiceInvoker.cs`.
  - [x] Re-read `DomainServiceResolverTests.cs` and `DomainServiceIsolationTests.cs` before editing; extend existing tests rather than creating duplicate harnesses.
  - [x] Re-read `docs/guides/configuration-reference.md`, `docs/guides/dapr-component-reference.md`, `docs/concepts/event-versioning.md`, and architecture D7 service discovery.
  - [x] Confirm the repo still has no `Hexalith.Parties` code path in scope; record Parties as downstream handoff only.

- [x] **ST1 - Choose and implement the sanitized wildcard collision policy.** (AC: 1, 5, 6)
  - [x] Prefer preserving the current `wildcard_{domain}_{version}` key shape if an explicit guard/test proves it is non-colliding under the current domain and version rules.
  - [x] If preserving the key shape, centralize or clearly name the sanitized wildcard key builder so code and tests share the same contract.
  - [x] Add an explicit guard or test-backed invariant that domains used for sanitized wildcard key construction cannot contain `_`, and versions cannot contain `_` or non-`v{digits}` content.
  - [x] Verify invalid domain/version examples cannot reach sanitized wildcard lookup: `domain_with_underscore`, `1`, `v1_2`, and `v01x`.
  - [x] Decide and record whether domain-name validation belongs in `DomainServiceResolver` or remains an upstream invariant. If preserving the upstream invariant, add tests or cite production command paths proving domains are validated before resolver lookup.
  - [x] Add a cross-invariant guard: if domain validation ever permits `_`, sanitized wildcard key safety must fail a test or be revisited deliberately.
  - [x] If changing the key shape instead, provide compatibility behavior or a deliberate migration note for existing `wildcard_counter_v1` and Parties `wildcard_party_v1` style keys.

- [x] **ST2 - Lock resolver precedence with tests.** (AC: 2, 5)
  - [x] Add tests showing colon exact and pipe exact both override wildcard, sanitized wildcard, config-store, and convention.
  - [x] Add a test showing pipe exact wins over pipe wildcard for the same tenant/domain/version.
  - [x] Add a test showing pipe wildcard wins over sanitized wildcard.
  - [x] Add a test showing static registration wins over a conflicting config-store entry when `ConfigStoreName` is enabled.
  - [x] Add tests showing `ConfigStoreName = null`, `""`, and whitespace do not call DAPR config-store lookup.
  - [x] Add or preserve a test showing final convention fallback returns `AppId = domain` and `MethodName = "process"`.
  - [x] Add options-binding coverage using real `IConfiguration` binding for pipe wildcard and sanitized wildcard registration keys.
  - [x] Keep existing no-cache/config-store tests intact; do not introduce resolver-level caching.

- [x] **ST3 - Publish the supported key formats and fallback order.** (AC: 3, 4, 6)
  - [x] Update `docs/guides/configuration-reference.md` Domain Services section with the exact precedence order and key examples.
  - [x] Correct the `ConfigStoreName` default from `"configstore"` to `null` / opt-in wherever the docs claim the default is `"configstore"`.
  - [x] Clarify that colon exact keys are canonical resolver keys but may not be suitable for every .NET configuration source because `:` is a hierarchy separator; recommend pipe exact keys for JSON/env configuration.
  - [x] Update `docs/guides/dapr-component-reference.md` Configuration Store section to preserve the current AppHost status: `configstore` is not wired by default, and `dapr-configstore` health can be degraded by design.
  - [x] Update `docs/concepts/event-versioning.md` so version-routing guidance says static registrations are checked before the opt-in config store, and convention fallback is the zero-config default.
  - [x] Define pipe wildcard and sanitized wildcard in a compact key-format table, or link both docs to one canonical table.
  - [x] Update `_bmad-output/planning-artifacts/architecture.md` only if the selected sanitized key shape or fallback-order text changes.

- [x] **ST4 - Add doc-drift/static regression coverage.** (AC: 3, 4, 5)
  - [x] Add tests that fail if docs state `EventStore:DomainServices:ConfigStoreName` default is `"configstore"`.
  - [x] Add tests that fail if docs omit `*|domain|version`, the selected sanitized wildcard form, `ConfigStoreName = null` / opt-in wording, or the complete fallback order.
  - [x] Add tests or review checks proving `docs/guides/configuration-reference.md` and `docs/concepts/event-versioning.md` publish the same order or share a canonical reference.
  - [x] Include a Parties-shaped example (`party`, `v1`) in either resolver tests or docs tests.

- [x] **ST5 - Validate and record evidence.** (AC: 7)
  - [x] Run the targeted server tests listed in AC 7.
  - [x] Run any narrower tests added by name if the broad filter misses them.
  - [x] Record AppHost build/Aspire status only as needed; AppHost code changes are not expected.
  - [x] Update the Dev Agent Record with validation results and any blocked runtime proof.

### Review Findings

- [x] [Review][Patch] Resolver-local domain validation omits the 64-character AggregateIdentity limit [src/Hexalith.EventStore.Server/DomainServices/DomainServiceResolver.cs:180]
- [x] [Review][Patch] Configuration docs overstate environment-variable support for pipe and sanitized wildcard keys [docs/guides/configuration-reference.md:189]
- [x] [Review][Patch] Generated API docs still claim ConfigStoreName defaults to "configstore" [docs/reference/api/Hexalith.EventStore.Server/Hexalith.EventStore.Server.DomainServices.DomainServiceOptions.md:28]
- [x] [Review][Patch] DAPR component reference overview still presents the configuration store as always connected [docs/guides/dapr-component-reference.md:18]
- [x] [Review][Patch] sprint-status.yaml contains unrelated Unicode mojibake in historical comments [_bmad-output/implementation-artifacts/sprint-status.yaml:202]
- [x] [Review][Patch] Story/evidence file is untracked, so AC7 evidence is outside the reviewable git diff [_bmad-output/implementation-artifacts/post-epic-22-es8-domain-service-resolver-separator-and-fallback-docs.md]

## Dev Notes

### Current Resolver Contract

`DomainServiceResolver.ResolveAsync` currently:

- Validates tenant/domain/version are not null or whitespace, lowercases version, and delegates version format validation to `DaprDomainServiceInvoker.ValidateVersionFormat`.
- Checks exact static registration by colon key: `tenant:domain:version`.
- Checks exact static registration by pipe key: `tenant|domain|version`.
- Checks wildcard static registration by pipe key: `*|domain|version`, then rewrites returned `TenantId` to the actual caller.
- Checks sanitized wildcard static registration by current key: `wildcard_{domain}_{version}`, then rewrites returned `TenantId`.
- Calls DAPR configuration only when `DomainServiceOptions.ConfigStoreName` is not null/whitespace.
- Falls back to convention routing: `AppId = domain`, `MethodName = "process"`.

Implementation note: the current sanitized wildcard key is `wildcard_{domain}_{version}`; keep the actual code string correct as `wildcard_{domain}_{version}`. If touching nearby docs/comments, fix any malformed prose while preserving behavior.

### Registration Key Format Table

| Name | Key shape | Purpose |
| --- | --- | --- |
| Canonical exact | `tenant:domain:version` | Exact tenant/domain/version registration when the configuration source supports colons. |
| Config-friendly exact | `tenant|domain|version` | Exact registration for JSON, environment-variable, and other configuration sources where colons are section separators. |
| Pipe wildcard | `*|domain|version` | Wildcard tenant registration that covers any tenant for one domain/version. |
| Sanitized wildcard | `wildcard_{domain}_{version}` | Kubernetes/env-friendly wildcard form; safe only while domains exclude `_` and versions match `^v[0-9]+$`. |

The order above is normative. A code change that reorders these keys must update tests and docs in the same change.

Options-binding evidence matters. Resolver tests built with direct dictionaries prove lookup behavior, but ES-8 also needs at least one test that binds `DomainServiceOptions` from `IConfiguration` so JSON/env-style key shapes are proven at the configuration boundary.

### Collision Analysis

The current collision risk is mitigated by existing validators:

- `AggregateIdentity.ValidateTenantOrDomain` allows lowercase alphanumeric and hyphens only, with no leading/trailing hyphen. Domains therefore cannot contain `_`.
- `DaprDomainServiceInvoker.ValidateVersionFormat` requires `^v[0-9]+$`. Versions therefore cannot contain `_`.
- With those invariants, `wildcard_{domain}_{version}` is structurally unambiguous for sanitized wildcard keys.

The gap is that `DomainServiceResolver` does not make this invariant obvious at the key-building point. A future change could loosen domain validation or call the resolver directly with a raw domain. Add guardrails/tests close to the resolver so the safety contract does not depend on a developer reading multiple files.

Do not add generic key normalization that rewrites existing `*|domain|version` or `wildcard_{domain}_{version}` keys before dictionary lookup. The story is about preserving and documenting the current supported key shapes, not inventing a new registration schema.

### Documentation Drift To Correct

- `docs/guides/configuration-reference.md` line range around Domain Services currently lists `ConfigStoreName` default as `"configstore"`. The code default is `null`, and the config store is opt-in.
- The validation table later in the same doc repeats `"configstore"` as the default for `EventStore:DomainServices:ConfigStoreName`.
- `docs/concepts/event-versioning.md` currently says version-based service resolution uses the DAPR configuration store and describes static registrations as fallback. That wording should be updated to: static registrations first, opt-in config store after static miss, convention fallback last.
- `docs/guides/dapr-component-reference.md` already states the AppHost does not wire `configstore` by default; preserve that and cross-link it from the configuration reference where useful.

### Files Expected To Touch

- `src/Hexalith.EventStore.Server/DomainServices/DomainServiceResolver.cs` - likely update key-building guard/helper/comments only; keep resolver behavior stable unless AC1 requires a deliberate key-shape migration.
- `tests/Hexalith.EventStore.Server.Tests/DomainServices/DomainServiceResolverTests.cs` - primary fallback-order and key-shape coverage.
- `tests/Hexalith.EventStore.Server.Tests/Security/DomainServiceIsolationTests.cs` - extend only if tenant-scope behavior is directly relevant; do not duplicate resolver tests unnecessarily.
- `tests/Hexalith.EventStore.Server.Tests/DaprComponents/DaprComponentValidationTests.cs` or another existing static-doc test file - doc-drift guard coverage.
- `docs/guides/configuration-reference.md` - main public documentation for registration key formats/defaults.
- `docs/guides/dapr-component-reference.md` - config-store optionality and AppHost status.
- `docs/concepts/event-versioning.md` - version-routing precedence.
- `_bmad-output/planning-artifacts/architecture.md` - only if the architecture D7 text needs to reflect a changed sanitized key contract.

### Must Preserve

- `ConfigStoreName = null` remains the default unless a separate architecture decision changes the zero-config story.
- `DomainServiceOptions.Registrations` remains a dictionary of `string` to `DomainServiceRegistration`; do not introduce a new schema for this minor story.
- `DomainServiceRegistration` remains unchanged; do not add fields or public contract changes.
- No Aspire/AppHost change is expected for ES-8.
- No new package dependency is allowed for ES-8.
- Test additions should stay targeted to resolver precedence, validation invariants, and docs drift.
- Existing `*|counter|v1`, `*|greeting|v1`, and `system|global-administrators|v1` registrations must still bind.
- Existing `wildcard_party_v1` behavior should remain compatible unless the story explicitly records a migration.
- No payload, command body, or event content should be logged in new tests or diagnostics.

### Party-Mode Review Hardening

Party-mode review on 2026-05-27 reached consensus that ES-8 is valid but needed sharper executable requirements before development:

- Winston: resolver order is a contract; tests must prove precedence, not only that each shape works.
- Amelia: mark changes requested until config-store opt-in, collision ownership, exact docs files, targeted tests, and no package/AppHost changes are explicit.
- Murat: highest risks are wrong-resolution precedence regression, sanitized wildcard collision, accidental config-store activation, and doc drift.
- Paige: fallback order must be normative; pipe wildcard and sanitized wildcard need a clear table; docs should share one order or point to one canonical section.

### Advanced Elicitation Hardening

Advanced elicitation on 2026-05-27 added three proof types and one unresolved design decision:

- Options binding proof: direct dictionary tests are insufficient; at least one real `IConfiguration` binding test must prove pipe wildcard and sanitized wildcard registration keys reach `DomainServiceOptions.Registrations`.
- Configuration-source warning: `tenant:domain:version` is canonical for resolver lookup, but docs should recommend `tenant|domain|version` for JSON/env sources because `:` is a hierarchy separator.
- Cross-invariant guard: if domain validation ever allows `_`, `wildcard_{domain}_{version}` safety must be revisited.
- Design decision: implementation must choose and record whether resolver-local domain validation is added or the existing upstream domain validation invariant remains the contract.

### Previous Story Intelligence

- ES-6 kept a string fallback only as last-resort compatibility after adding typed detection. Apply the same philosophy here: if the sanitized wildcard key is preserved for compatibility, make the guard explicit rather than silently relying on comments.
- ES-7 proved that comments and docs drift unless a regression test catches the trusted source. Use executable/static coverage for fallback order and docs defaults, not only prose.
- ES-1 through ES-7 kept each residual focused. ES-8 should not reopen ES-9 Contracts/DAPR decoupling, ES-7 state-store source-of-truth, or ES-6 actor-not-found behavior.

### Git Intelligence

Recent commits show the expected pattern for this cluster:

- `6bcb6f95 fix(apphost): align statestore yaml source` - ES-7 aligned docs/tests/code around one source of truth.
- `3f301470 fix(server): harden actor not-found detection` - ES-6 added typed/fallback behavior with regression coverage.
- `03000720 docs: close post-epic 22 ES-5 review` and `712b6d31 test(contracts): add domain service wire result backcompat fixture` - recent ES stories prefer focused tests and narrow docs updates over broad rewrites.

### Latest Technical Information

- Official DAPR Configuration API docs describe fetching configuration items by store name and key list. This matches the current opt-in `DaprClient.GetConfiguration(configStoreName, [configKey], ...)` path. Source: https://docs.dapr.io/reference/api/configuration_api/
- Microsoft ASP.NET Core configuration docs state hierarchy separators use `:` and environment variables commonly use `__` as the portable separator. This is why pipe/sanitized dictionary keys need explicit docs when routed through JSON/env/config sources. Source: https://learn.microsoft.com/aspnet/core/fundamentals/configuration/
- Kubernetes ConfigMap docs allow alphanumeric, `-`, `_`, and `.` in data keys; environment-variable names are more restrictive in practice. The current sanitized wildcard form was introduced for Kubernetes/env-friendly emission, so docs should distinguish ConfigMap data keys from env-var keys instead of implying every source supports `*|...`. Source: https://kubernetes.io/docs/concepts/configuration/configmap/

### Project Context Reference

Apply `_bmad-output/project-context.md`:

- Treat warnings as build-breaking.
- Use centralized package management; no package should be added for ES-8.
- DAPR access control is deny-by-default and service invocation paths must be intentional.
- Domain services should not receive direct state store/pubsub access.
- Validate with targeted test projects individually.
- Prefer official Aspire, Microsoft, DAPR, and NuGet docs when changing version-sensitive infrastructure guidance.

### Aspire Baseline

Before creating this story, the repo instruction to establish Aspire state was attempted on 2026-05-27:

- First PowerShell attempt used bash-style `EnableKeycloak=false` and failed before running Aspire.
- Retried with `$env:EnableKeycloak='false'; aspire run --project src\Hexalith.EventStore.AppHost\Hexalith.EventStore.AppHost.csproj --non-interactive --detach --format Json`.
- AppHost build succeeded with 0 warnings and 0 errors, then Aspire exited with code 2 because Docker is not running or not installed.
- Aspire MCP tools were searched for in this Codex session and were not available.

## References

- [Source: `_bmad-output/planning-artifacts/sprint-change-proposal-2026-05-27-eventstore-parties-review-residuals.md#ES-8`] - ES-8 residual scope and Parties handoff.
- [Source: `src/Hexalith.EventStore.Server/DomainServices/DomainServiceResolver.cs`] - current resolver fallback order and sanitized wildcard key.
- [Source: `src/Hexalith.EventStore.Server/DomainServices/DomainServiceOptions.cs`] - `ConfigStoreName` default and `Registrations` dictionary.
- [Source: `src/Hexalith.EventStore.Server/DomainServices/DaprDomainServiceInvoker.cs`] - version extraction/default/validation.
- [Source: `src/Hexalith.EventStore.Contracts/Identity/AggregateIdentity.cs`] - domain validation that excludes underscores.
- [Source: `src/Hexalith.EventStore/appsettings.Development.json`] - existing wildcard sample registrations.
- [Source: `tests/Hexalith.EventStore.Server.Tests/DomainServices/DomainServiceResolverTests.cs`] - current resolver coverage.
- [Source: `tests/Hexalith.EventStore.Server.Tests/Security/DomainServiceIsolationTests.cs`] - tenant/domain routing and config-store exact-key coverage.
- [Source: `docs/guides/configuration-reference.md`] - public domain-service configuration docs requiring correction.
- [Source: `docs/guides/dapr-component-reference.md`] - DAPR config-store optionality/AppHost status.
- [Source: `docs/concepts/event-versioning.md`] - version-routing docs requiring precedence correction.
- [Source: `_bmad-output/planning-artifacts/architecture.md#D7-Domain-Service-Invocation----DAPR-Service-Invocation`] - architecture service-discovery order.
- [External: DAPR Configuration API](https://docs.dapr.io/reference/api/configuration_api/)
- [External: ASP.NET Core configuration](https://learn.microsoft.com/aspnet/core/fundamentals/configuration/)
- [External: Kubernetes ConfigMaps](https://kubernetes.io/docs/concepts/configuration/configmap/)

## Dev Agent Record

### Agent Model Used

GPT-5 Codex

### Debug Log References

- 2026-05-27: Loaded bmad-dev-story workflow customization, project context, sprint status, and story file.
- 2026-05-27: Attempted Aspire baseline with `EnableKeycloak=false aspire run --project src\Hexalith.EventStore.AppHost\Hexalith.EventStore.AppHost.csproj --non-interactive --detach --format Json`; AppHost build succeeded with 0 warnings/errors, then Aspire exited code 2 because Docker is not running or installed.
- 2026-05-27: Re-read resolver/options/registration/invoker files, resolver/isolation/DAPR validation tests, domain-service docs, architecture D7 text, and confirmed no `Hexalith.Parties` code path is in scope.

- 2026-05-27: Added red tests for resolver-local domain validation, invalid versions, precedence ordering, config-store optionality, options binding, and docs drift; initial targeted test run failed on stale docs and underscore-domain sanitized lookup as expected.
- 2026-05-27: Implemented `CreateSanitizedWildcardKey`, resolver-local domain validation, docs updates, and static docs guard tests.
- 2026-05-27: Validation PASS - `dotnet test tests\Hexalith.EventStore.Server.Tests\Hexalith.EventStore.Server.Tests.csproj --filter "FullyQualifiedName~DomainServiceResolverTests|FullyQualifiedName~DomainServiceIsolationTests|FullyQualifiedName~DaprComponentValidationTests" --no-restore` (101 passed).
- 2026-05-27: Validation PASS - `dotnet test tests\Hexalith.EventStore.Client.Tests\Hexalith.EventStore.Client.Tests.csproj --no-restore` (399 passed).
- 2026-05-27: Validation PASS - `dotnet test tests\Hexalith.EventStore.Contracts.Tests\Hexalith.EventStore.Contracts.Tests.csproj --no-restore` (513 passed).
- 2026-05-27: Validation PASS - `dotnet test tests\Hexalith.EventStore.Sample.Tests\Hexalith.EventStore.Sample.Tests.csproj --no-restore` (74 passed).
- 2026-05-27: Validation PASS - `dotnet test tests\Hexalith.EventStore.Testing.Tests\Hexalith.EventStore.Testing.Tests.csproj --no-restore` (144 passed).
- 2026-05-27: Validation PASS - `dotnet build src\Hexalith.EventStore.AppHost\Hexalith.EventStore.AppHost.csproj --no-restore` (0 warnings, 0 errors).
- 2026-05-27: Validation PASS - `git diff --check` (no whitespace errors; Git reported line-ending conversion warnings only).
- 2026-05-27: Full `Hexalith.EventStore.Server.Tests` project was attempted and failed on unrelated/environmental checks: DAPR/Redis/placement/scheduler unavailable, plus pre-existing health/access-control topology expectation mismatches outside ES-8 scope.
- 2026-05-27: Code review found six patch items: domain length validation, environment-variable wording, stale API docs, optional config-store overview wording, sprint-status encoding churn, and untracked story evidence visibility.
- 2026-05-27: Code review patches applied. Validation PASS - `dotnet test tests\Hexalith.EventStore.Server.Tests\Hexalith.EventStore.Server.Tests.csproj --filter "FullyQualifiedName~DomainServiceResolverTests|FullyQualifiedName~DomainServiceIsolationTests|FullyQualifiedName~DaprComponentValidationTests" --no-restore` (102 passed).
- 2026-05-27: Code review patches applied. Validation PASS - `git diff --check` (no whitespace errors; Git reported line-ending conversion warnings only).

### Completion Notes List

- Preserved the existing `wildcard_{domain}_{version}` sanitized wildcard key shape for compatibility and made the separator-safety invariant explicit in `DomainServiceResolver`.
- Chose resolver-local domain validation, matching `AggregateIdentity` domain rules, so domains containing `_` cannot reach sanitized wildcard lookup even if a future direct resolver caller bypasses upstream aggregate identity construction.
- Locked resolver precedence with tests for colon exact, pipe exact, pipe wildcard, sanitized wildcard, opt-in config store, and convention fallback.
- Added real `IConfiguration` options-binding evidence for `*|party|v1` and `wildcard_counter_v1` registrations.
- Updated domain-service routing docs in configuration reference, event versioning, DAPR component reference, and architecture D7 to publish the same key formats and fallback order.
- Applied code review patches: mirrored the 64-character domain limit in resolver-local validation, clarified configuration-source support for pipe/sanitized keys, updated stale API-reference and DAPR overview docs, repaired sprint-status mojibake, and marked this story as intent-to-add so it appears in `git diff HEAD`.

### File List

- `_bmad-output/implementation-artifacts/post-epic-22-es8-domain-service-resolver-separator-and-fallback-docs.md`
- `_bmad-output/implementation-artifacts/sprint-status.yaml`
- `_bmad-output/planning-artifacts/architecture.md`
- `docs/concepts/event-versioning.md`
- `docs/guides/configuration-reference.md`
- `docs/guides/dapr-component-reference.md`
- `src/Hexalith.EventStore.Server/DomainServices/DomainServiceResolver.cs`
- `tests/Hexalith.EventStore.Server.Tests/DaprComponents/DaprComponentValidationTests.cs`
- `tests/Hexalith.EventStore.Server.Tests/DomainServices/DomainServiceResolverTests.cs`

## Verification Status

Implementation and code review patches complete. Status: done. Targeted resolver/docs validation passed; live Aspire/runtime validation remains blocked by Docker/DAPR prerequisites as recorded in the Dev Agent Record.

## Change Log

| Date | Version | Description | Author |
| --- | ---: | --- | --- |
| 2026-05-27 | 1.1 | Applied code review patches, refreshed targeted validation, and closed ES-8 as done. | Codex |
| 2026-05-27 | 1.0 | Implemented ES-8 resolver validation, precedence/options/docs tests, docs corrections, and validation evidence; story ready for review. | Codex |
| 2026-05-27 | 0.1 | Created ready-for-dev post-Epic-22 ES-8 story covering sanitized wildcard key collision guard, resolver fallback-order tests, routing docs corrections, Parties re-verification, and validation evidence. | Codex |
| 2026-05-27 | 0.2 | Applied party-mode review hardening: normative resolver precedence contract, config-store opt-in tests, collision invariant examples, wildcard key-format table, doc-drift parity checks, and explicit no-AppHost/no-package scope. | Codex |
| 2026-05-27 | 0.3 | Applied advanced elicitation refinements: options-binding evidence, configuration-source warning for colon keys, resolver-vs-upstream domain validation decision, cross-invariant guard, and no key-normalization warning. | Codex |

## Story Completion Status

Implementation and code review patches complete. Status: done.
