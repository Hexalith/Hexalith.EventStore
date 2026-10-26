---
title: '5.12 Route and idempotency catalog construction'
type: 'feature'
created: '2026-10-25'
status: 'in-progress'
baseline_commit: '8e7c12211c05d7c8db926148550cd3b45844ffd4'
route: 'dispatch'
review_loop_iteration: 1
story_id: '5.12'
story_key: '5-12-route-and-idempotency-catalog-schema-codec-and-envelope'
context:
  - _bmad-output/implementation-artifacts/epic-5-context.md
  - _bmad-output/implementation-artifacts/investigations/story-5-12-code-map.md
  - _bmad-output/implementation-artifacts/investigations/story-5-12-declaration-proposal.md
  - AGENTS.md
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** Stories 2.14 and 2.15 lack the shared AD-33 route/scope/version catalog.

**Approach:** Add Contracts declarations, a versioned envelope, codec/validator and Development/test catalog derived from declarations/composition bindings, checked against current resolvers.

**Owner direction (2026-10-25):** Complete this before 2.14; use a library/local catalog. Activation stays in 5.13; storage identities stay unchanged.

**Owner decisions (2026-10-26):** Contracts owns operation identifiers; ServiceDefaults retains authentication and compatible forwarding constants. Human platform grants use exact `eventstore:permission` matches, with no wildcard or administrator bypass. General ingress stays in endpoint metadata; platform declarations bind namespace and `RequiredHumanPermission` separately from workload-hop credentials. Implement tooling and a concrete gap report first, then obtain/implement real missing declarations. This story stays open until the complete Development catalog passes; 2.14 remains pending.


**Owner approval (2026-10-26):** The owner accepted the [initial declaration decisions](investigations/story-5-12-declaration-proposal.md) and implementation across EventStore/Sample and the root-declared Tenants repository. Existing command operations declare MessageId v1 and Mutation/24-hour replay, with per-operation version-1 adapters, schemas and policies; the linked command intent, exact-string/role/null semantics, trusted-subject requirements, platform permissions and unactivated Development key references are authoritative. Preserve actual aliases, actor/handler query targets and projection outputs through compatible schema/parity changes. This authorizes authoring the previously missing definitions; it does not enable admission, provision secrets, change runtime authorization/routing, activate/deploy, mutate stored identities, or grant production readiness. No further approval is needed for routine implementation choices within those decisions.

## Boundaries & Constraints

**Always:** Bind stable route IDs, generation, root/facet digests, exact target app/method/version, admitted credential/operation and command MessageId declaration/version digest. Join each command to AD-25 metadata: adapter, operation, descriptor schema/digest, retention, digest-key generations, logical secret map and consumer. Preserve existing wire/hash protocols. Record proof in this spec plus CI under the October 23 policy.

**Never:** Invent missing adapter/policy declarations; serialize secret values or request intent; change runtime authorization/routing, activate a catalog, deploy, or claim production/OQ8 readiness. Do not rebuild deleted evidence machinery. Namespace/grants never synthesize a tenant named `system`.

## I/O & Edge-Case Matrix

| Scenario | Input/state | Result | Failure |
| --- | --- | --- | --- |
| Valid envelope | Complete declarations and bindings | Canonical UTF-8; stable SHA-256; exact decode/encode round trip | None |
| Invalid bytes | Duplicate/unknown fields, unsupported version, noncanonical JSON | Reject before accepting an envelope | Entry/field reason |
| Invalid mapping | Missing/duplicate/ambiguous key, join, declaration or facet mismatch | Reject | Safe diagnostic |
| Development | Explicit profile and complete declarations | Generated file equals effective command/query/projection routes | Drift fails |
| Override | Undeclared fallback or runtime override outside Development | Reject candidate | No production claim |

</frozen-after-approval>

## Code Map

See [investigation](investigations/story-5-12-code-map.md) for exact sources and missing inputs. Prefixes: `C=src/Hexalith.EventStore.Contracts`, `S=src/Hexalith.EventStore.Server`, `D=src/Hexalith.EventStore.ServiceDefaults`, `T=tests/Hexalith.EventStore.Contracts.Tests`.

- `C/Commands/ICommandContract.cs`, `C/Queries/IQueryContract.cs` — missing version/security declarations.
- `S/DomainServices/DomainServiceResolver.cs` — targets; query dispatch overrides method to `query`.
- `S/Commands/IdempotencyIntentAdapterRegistry.cs` — adapter lookup lacks domain.
- `D/Authentication/EventStoreWorkloadOperations.cs` — public identifiers; D references C.

## Tasks & Acceptance

**Execution:**
- [x] `_bmad-output/planning-artifacts/architecture.md` and its companion `.memlog.md` — record the three owner choices once answered, removing the obsolete no-Contracts-dependency explanation without altering other decisions.
- [x] `C/Routing/RouteContractDeclaration.cs`, `RouteCatalogEnvelope.cs`, `RouteCatalogEntry.cs`, `IdempotencyCatalogEntry.cs`, `PlatformAdmissionDeclaration.cs` in that directory — define closed versioned models; one type per file; require explicit command grammar versions.
- [x] `C/Routing/RouteCatalogCodec.cs`, `RouteCatalogValidator.cs` — implement canonical encoding/declaration hashing, exact-byte decoding, semantic validation and bounded entry/field failures using BCL APIs.
- [x] `C/Authorization/EventStoreOperationIdentifiers.cs`, `D/Authentication/EventStoreWorkloadOperations.cs`, `JwtBearerAuthenticationContract.cs` in the latter directory — implement approved declaration ownership while preserving existing authentication behavior; 2.14 implements new platform enforcement.
- [x] `src/Hexalith.EventStore.Client/Routing/DevelopmentRouteCatalogBuilder.cs` and `tools/routing-catalog.cs` — compose declared contracts with value-free Development bindings, generate/check the catalog through the shared codec; refuse missing required inputs.
- [x] `deploy/dapr/eventstore-routing-catalog.json` — generate only from a complete profile whose current effective routes are verified; never fill missing policy with defaults to make generation pass.
- [x] `T/Routing/RouteCatalogCodecTests.cs`, `RouteCatalogValidatorTests.cs`, `Fixtures/` in that directory — positive controls and checked-in negatives observed failing for every rejection, round trips and stable digests.
- [ ] `tests/Hexalith.EventStore.Server.Tests/DomainServices/DevelopmentRouteCatalogParityTests.cs`, `tests/Hexalith.EventStore.QueryRouting.Tests/DaprDomainQueryInvokerTests.cs` — compare actual resolution, query override and projection discovery; run through existing CI lanes.


- [x] `src/Hexalith.EventStore.Contracts/Routing/` and `src/Hexalith.EventStore.Client/Routing/` — support real discriminator compatibility, computed queries and actor versus domain-handler targets without changing existing v1 golden bytes/digests; keep route IDs distinct from wire values and declare bounded actual-handler alias resolution.
- [x] `src/Hexalith.EventStore.DomainService/IdempotencyIntentCommand.cs` and reusable declaration/adapter seams in Contracts/Client/DomainService — carry authenticated subject additively where required; implement reusable offline construction from owner metadata. Preserve existing constructor/API compatibility and runtime authorization.
- [x] `samples/Hexalith.EventStore.Sample.Contracts/`, `samples/Hexalith.EventStore.Sample/Counter/`, `samples/Hexalith.EventStore.Sample/Greeting/` — implement approved declarations and tested canonical intent for the four counter operations and existing SendGreeting handler; do not rename its current wire type or invent a projection.
- [x] `references/Hexalith.Tenants/src/Hexalith.Tenants.Contracts/` and domain-owned adapter/declaration files under its `src/Hexalith.Tenants.Server/` or `src/Hexalith.Tenants/` — implement the twelve approved operation definitions and actual public/internal query and projection metadata in their owning repository; preserve grants, stored identities and endpoint behavior. Tenants baseline is `665068ec0791fd2ea0e00e1c04b25d60ed0b9fbb`.
- [x] `tools/routing-catalog.cs`, `deploy/dapr/eventstore-routing-catalog.json` and their shared offline composition path — generate/check from compiled owner declarations and explicit Development bindings; derive every digest through the shared codec; treat approved secret references as unactivated declarations, never a claim of existing key availability.
- [x] Relevant Contracts/Client/DomainService/Sample tests, Server `DomainServices/DevelopmentRouteCatalogParityTests.cs`, QueryRouting tests, and Tenants-owned declaration/adapter tests — prove actual aliases and semantic payload equivalence/distinction, target identity, subject binding, all query targets/projection outputs, resolver precedence and source drift. Missing or ambiguous capability/fallback evidence must reject profile completeness.


**Acceptance Criteria:**
- Given complete declarations, when generation runs twice, then canonical bytes/digests match and runtime-target parity passes without changing dispatch behavior.
- Given changed declarations or malformed inputs, when validation/CI runs, then drift and each rejection fail beside a passing control.
- Given missing adapter/policy metadata or an unactivated production instance, when evaluated, then incompleteness/readiness remains explicit and this story cannot claim complete delivery from synthetic fixtures.

## Implementation Notes

The initial bounded tooling phase defined closed v1 construction inputs, deterministic catalog bytes, declaration/descriptor/idempotency-entry/facet digests, and an external root digest equal to SHA-256 of the exact retained envelope bytes. Synthetic complete fixtures use `fixture-*` targets and explicit test policies; they are not actual Development declarations. That phase did not include a deployment catalog or runtime adoption. The real-owner v2 continuation is recorded below; runtime adoption remains out of scope. V1 permits only workload command/query/projection target hops; general ingress remains endpoint metadata. Commands require exact declarations and individual AD-25 joins; query/projection fallbacks must enumerate finite discriminators, with exact declarations taking precedence.


- 2026-10-26 — Approved staged delivery is implemented through [catalog construction tooling](spec-route-catalog-construction-tooling.md). That bounded work item is complete after the follow-up review and fixes; this full story and its sprint row remain in-progress until complete real declarations and Development parity pass.

## Spec Change Log

- 2026-10-26: Review loop 1 — R3B3/R3B4/R3B5 exposed partial command inventory validation. Added the non-frozen shared-runtime-discovery correction plan and KEEP instructions; the known-bad state is a catalog passing parity for non-dispatchable/missing handlers or unsupported aliases. All verified review corrections are retained below. No code/user work is reverted under the explicit preservation constraint.

- 2026-10-26: Owner approved the linked declaration proposal. Added the explicit decision to frozen intent under that authorization, preserved the original baseline and prior verified tooling, and expanded the remaining implementation tasks to cover real aliases, computed/actor queries, domain declarations/adapters and Development parity. Tenants source changes are explicitly authorized; runtime adoption/provisioning stays out of scope.

- 2026-10-26: Reopened the bounded tooling item for the confirmed CLI path-error handling and AD-36 operation-ownership corrections. Parent scope, baseline, frozen intent, and incomplete Development/parity tasks are unchanged.

## Review Triage Log

- 2026-10-26: Follow-up findings F1/F2 in the [tooling spec](spec-route-catalog-construction-tooling.md) were confirmed and fixed. Invalid CLI file arguments now produce a bounded exit-3 error; AD-36 assigns identifiers to Contracts and compatible forwarding/authentication to ServiceDefaults. The implementation is ready for parent review; no runtime or full-profile completion is inferred.

### Real-catalog review, first pass (2026-10-26)

Every finding was classified before grouping. Reviewers read the root baseline plus the Tenants source diff. The package boundary remains a delivery blocker; source review does not waive it.

| Finding | Verdict | Evidence and route |
| --- | --- | --- |
| R3B1 | medium | Confirmed: configuration binding can throw InvalidOperationException containing the supplied conversion value; CLI handles neither this exception nor a safe catalog translation. Correct with a bounded diagnostic and subprocess negative/control. Patch. |
| R3B2 | medium | Confirmed: new Development construction calls File.ReadAllBytes before the 4 MiB inspection guard, unlike existing CLI modes. A large local input is allocated in full. Bound the initial reads and reuse the retained bytes when binding. Patch; distinct from the previously rejected arbitrary in-memory heap budget. |
| R3B3 | medium | Confirmed: ValidateCommands counts all methods named Handle, while EventStoreAggregate checks state, return and optional envelope/cancellation shapes and prefers a cancellation overload. A wrong-shape method can pass inventory, and supported paired overloads can be miscounted. Bad spec: use shared effective dispatcher discovery. |
| R3B4 | medium | Confirmed: the four fixed aggregate Type arguments exclude newly hosted aggregate classes in the already enrolled Sample/Tenants assemblies. Discover the complete hosted assembly inventory through SDK rules and compare it to the declared profile. Bad spec, same incomplete command-discovery root cause. |
| R3B5 | medium | Confirmed: adapter metadata is compared to itself; aliases are not compared with the actual CLR/contract lookup keys. The review probe's Bogus alias passes parity but actual dispatch rejects it. Bad spec, same incomplete command-discovery root cause; also bind each discovered handler to its actual domain. |
| R3B6 | medium | Confirmed: static attached declarations are not executable Domain/QueryType evidence. Actual Tenants handlers derive QueryType today, but the helper/test would accept a future mismatched instance route. Add owning-host verification comparing every registered executable handler's Domain/QueryType and attached declaration with packaged enrollment; keep metadata inspection claims accurate. Patch at the owning verification boundary, no root dependency on the executable. |
| R3B7 | medium | Confirmed: RequiresAuthenticatedSubject=true with no acting-subject:string field constructs, then every CreateIntent fails schema matching after injection. Validate this known schema relationship at construction, beside positive/negative controls. Patch. |
| R3B8 | medium | Confirmed: the adapter retains caller-owned collection references and registry matching rereads declarations after startup overlap checks. Mutating aliases can change acceptance or introduce ambiguity later. Snapshot nested policy collections and registry matching data before use; prove mutations cannot change accepted routes or canonical schemas. Patch. |
| R3B9 | medium | Confirmed: helper-only metadata tests supply registration directly, so deleting Program enrollment or endpoint Bind stays green. Add real-host HTTP metadata assertions. Patch, grouped with R3V1. |
| R3B10 | medium | Confirmed: independent fixture generation covers v1 only; new v2 tests are implementation-generated round trips. Add independent v2 declaration/facet/root vectors covering all new execution/scope metadata, retaining v1 vectors unchanged. Patch. |
| R3E1 | medium | Confirmed by both recorded package-build failures: published pins lack the new APIs. The approved intent explicitly excludes dependency updates/external publication, so no such mutation is performed. This is an already recorded open delivery gate, not a code patch or a new deferred ledger entry; story completion/CI success remain unclaimed. |
| R3E2 | medium | Carried bounded-spec B10/R2B5/R2E2/R2V2: unchanged producer paging can be lost on 304. Already deferred outside the approved no-routing/response-change intent. Preserve concurrent query work and do not patch or defer again. |
| R3V1 | medium | Pre-verified regression gap: actual metadata endpoint assertions cover query types only; neither new projection outputs nor real enrollment are checked. Extend the existing Tenants host test to prove both projection names, exact secondary outputs and absence of invented named capabilities. Patch, same root cause as R3B9. |

The R3B3/R3B4/R3B5 group requires a non-frozen plan correction before implementation. Other surviving corrections are included below so the re-derived source closes their demonstrated outcomes. No prior/user work is reverted: the approved continuation explicitly requires preserving it. The package gate and existing unrelated paging deferral are retained unchanged.

## Design Notes

Use a catalog-specific BCL codec. Human platform admission and downstream workload authorization are distinct. Missing AD-25 declarations block the complete catalog; configuration defaults cannot replace them.

## Verification

Bounded tooling is implemented and reviewed on source snapshot `54733d1b928c41f1e9fdf46841d043c8d482d648` plus reviewed working-tree patches and documentation. Final individual Debug/source builds passed with zero warnings/errors. Focused checks passed 74 Contracts and 37 Client cases; the compatibility lane passed 12 ServiceDefaults cases. Parent reran all full affected suites after review fixes: Contracts 1,315 passed/2 existing package-inventory skips; Client 2,217 passed/2 existing Dapr live-test skips; ServiceDefaults 16 passed. All 3,548 tests passed with no failures. CLI tests now use an isolated build, resolving the earlier shared-artifact collision. Three review layers completed; all catalog findings were resolved, with one unrelated query paging issue recorded in deferred-work.md. Exact commands, triage, and earlier failure boundaries are in the bounded tooling spec.

Direct CLI generation/validation exited 0 with golden root `d30010c9b1017f29c5ae9da9a39289d8865bfce91bd153b6000beaad0248f4c0`; actual-profile inspection exited 2 with four bindings/52 diagnostics and no deployment file. `git diff --check` and `bash scripts/check-committed-files.sh` passed. CI uses the existing Release/package lanes; it was not run in this session, so no CI URL is available. These results prove construction and compatibility, not a complete real profile or runtime-target parity.

### Bounded construction result (2026-10-26)

The [tooling spec](spec-route-catalog-construction-tooling.md) records the exact commands, source revision, test counts, golden root digest, and real-profile inspection. Its four configured bindings (`counter`, `greeting`, `global-administrators`, `tenants`) still lack authoritative route declarations and complete per-command AD-25 adapter/operation, policy, descriptor, retention, digest-generation, logical-map, and consumer metadata. The two existing `system` bindings also lack namespace/exact-human-permission declarations. Projection inventory and convention/dynamic fallback parity remain unverified. The offline report enumerates 52 missing-input diagnostics and explicitly limits its inventory to checked-in JSON configuration.

Synthetic fixture generation succeeds; actual-profile inspection exits nonzero and writes no deployment catalog. The full Development catalog and runtime-target parity tasks remain unchecked. Story 5.12 and its sprint row remain in-progress; Story 2.14 stays pending/backlog. Construction does not activate a catalog, implement platform admission, or establish production/OQ8 readiness.

### Follow-up correction verification (2026-10-26)

On source revision `dcd8b34976d51f447c19b4f84e1198b5950d454d` plus the six-file working-tree correction, `dotnet build tests/Hexalith.EventStore.Client.Tests/Hexalith.EventStore.Client.Tests.csproj -c Debug -p:UseHexalithProjectReferences=true -v quiet` exited 0 with zero warnings/errors. `dotnet tests/Hexalith.EventStore.Client.Tests/bin/Debug/net10.0/Hexalith.EventStore.Client.Tests.dll -class '*Routing.DevelopmentRouteCatalogBuilderTests'` passed 37/37 tests with zero skips/failures, including 18 new invalid-path subprocess cases beside passing fixture and actual-profile controls. `git diff --check` and `bash scripts/check-committed-files.sh` passed (9,128 tracked entries). The tooling spec records the exact error/output-preservation assertions and timing. Existing full-suite evidence remains historical; these narrow tool/documentation changes did not justify repeating it. No CI run, full-catalog completion, runtime parity, or production readiness is claimed.


### Follow-up review closure (2026-10-26)

The tooling follow-up is complete: parser/path error handling, inspection fidelity, secret-map consistency, app-ID compatibility, diagnostic indices and output-limit coverage were corrected without changing runtime behavior. Three independent reviews completed; the existing unrelated paging issue remains deferred. Parent ran each affected Debug/source build and full suite serially: **3,593 passed, four existing skips, zero failures**, with zero build warnings/errors. Source boundary is `dcd8b34976d51f447c19b4f84e1198b5950d454d` plus the reviewed working tree; exact commands and results are in the tooling spec. No CI run or catalog activation is claimed.

Source inspection reconfirmed the missing authoritative inputs: four Counter ICommandContract commands, twelve Tenants/global-administrator ICommandContract commands, and the convention-discovered SendGreeting command have no concrete trusted adapter registration in the inspected source trees. Their existing command names do not establish MessageId grammar, canonical-intent policy, retention tier, or key-map declarations. Seven IQueryContract declarations expose query/projection names, but actual projection discovery and complete route parity remain unverified. The owner was asked where to obtain the approved declarations; none were supplied during this follow-up. The full-catalog and parity tasks remain unchecked, and the story/sprint row stay in-progress under the frozen instruction not to invent these declarations.


### Proposed declarations after owner clarification (2026-10-26)

The owner does not have an external declaration source. A [concrete proposal](investigations/story-5-12-declaration-proposal.md) now recommends compatible MessageId v1, Mutation/24-hour replay for all 17 operations, explicit per-operation intent definitions, and value-free Development/platform metadata. It also records real alias/query/actor/projection representation gaps that must be resolved before claiming catalog parity. These are proposed decisions awaiting acceptance; the frozen intent is unchanged. No runtime or Tenants source changes were made in this planning pass.


### Approved implementation continuation (2026-10-26)

The owner approved the declaration proposal after stating no external definitions were available. The earlier missing-policy blocker is resolved by that approval. Work resumes on the remaining real-catalog and parity tasks; the existing tooling changes and all prior user work must be preserved. Do not stage, commit, push, create branches, update dependencies, initialize nested submodules or edit any other submodule. Do not load skills under references. Read owning repository guidance before editing Tenants and run its checks from that repository. Implementation uses Debug/source mode and individual test projects; new full verification is required for affected behavior. Existing baseline revisions are retained.

Before new code changes, `aspire ps --non-interactive` found no running AppHost. `aspire run --detach --isolated --apphost src/Hexalith.EventStore.AppHost/Hexalith.EventStore.AppHost.csproj --non-interactive` exited 0, and `aspire describe --apphost src/Hexalith.EventStore.AppHost/Hexalith.EventStore.AppHost.csproj --non-interactive` reported the topology Running/Healthy. The owned baseline instance was stopped with `aspire stop --apphost src/Hexalith.EventStore.AppHost/Hexalith.EventStore.AppHost.csproj --non-interactive` before implementation to release build resources.

### Real owner catalog implementation (2026-10-26)

Schema v2 adds `RouteExecutionDeclaration` to each declaration: the actual command payload or query contract/handler identity, a finite alias lookup set, the existing `clr-short-name-v1` extraction protocol where applicable, domain-service versus projection-actor targets, projection versus computed query results, actor type, delegated-context classification, and the outputs of each callable projection. Secondary index/audit outputs remain outputs, not independently callable projection routes. V1 omits these fields and retains its exact golden bytes and digest preimages. V2 declaration/facet/root digests bind the new metadata; the existing descriptor and idempotency-entry protocols remain unchanged.

V2 bindings also record `ResolverTenantPattern`. Counter, Greeting and Tenants use the verified wildcard profile; global-administrator operations record only the existing historical `system` resolver binding. Other tenants' convention targets are outside this profile. This field records resolution evidence: it does not synthesize a `system` request tenant from a platform namespace, migrate stored identities, or alter scope enforcement.

Owner metadata lives in Sample Contracts/Sample and Tenants Contracts/Server. All 17 commands have explicit MessageId v1, descriptor/policy v1 and Mutation/24-hour replay declarations. Typed offline adapters use the existing payload serializer; strings retain case/whitespace/Unicode, validated roles use enum names, null descriptions remain distinct from empty strings, and ignored payload target identifiers remain ignored. Global-administrator intent additionally requires the authenticated acting subject. The additive `IdempotencyIntentCommand.AuthenticatedSubject` init property preserves its existing positional constructor and deconstruction; an ignored gateway-only `SubmitCommand.AuthenticatedSubject` carries the authenticated principal/verified workload actor binding, never arbitrary extensions or a payload identity. No adapters are registered and admission remains unconfigured.

The developer-only `tools/Hexalith.EventStore.CatalogConstruction` library is shared by `tools/routing-catalog.cs` and Server parity tests. It derives from compiled owner declarations/adapters, compares the actual aggregate Handle inventory, joins packaged Tenants capability enrollment with local Sample discovery, requires complete custom-dispatcher/SDK projection evidence, checks every applicable exact resolver registration and the bounded profile convention case, then derives all digests through the shared codec. Missing, duplicate or changed capability evidence and target/configuration drift reject before writing a catalog. The root construction library references only packable Tenants Contracts/Server libraries, never its executable host. Tenants query handlers and the existing custom `/project` dispatcher reuse `TenantCatalogCapabilities` in Tenants.Server; owning-repository tests compare actual compiled query discovery by full declaration digest and execute dispatcher/output persistence parity. This packaged enrollment adds output inventory without granting named-dispatch capabilities or changing existing projection fingerprint protocols.

The Counter query declares `eventstore`, actor type `ProjectionActor`, and method `QueryAsync`; Tenants' eight queries declare `tenants/query`, including two internal computed queries with their existing delegated-context requirement. `projection:query` is an unactivated target-hop declaration. Current ActorProxy invocation does not establish workload-assertion enforcement for that operation; activation and protected framework/actor admission remain later-story work.

The approved `secretstore` / `eventstore-idempotency-ring` / `development-v1` references are metadata only. Every key reference uses `MapKey == Generation`; no secret availability, component provisioning, runtime key registration, production readiness, OQ8 authority, deployment or activation is asserted.

### Real-catalog verification matrix

| Matrix row / owner decision | Executable checks |
| --- | --- |
| Canonical bytes, stable digests and exact round trip | Contracts `RouteCatalogCodecTests`, `RouteCatalogV2Tests`; Server `CompiledOwnersGenerateStableCompleteCatalogAndCheckedInBytes`; CLI `development`, `check-development`, `validate` |
| Duplicate/unknown/missing fields, unsupported versions, noncanonical JSON | Existing checked-in Contracts fixtures and codec negatives, plus v2 execution-field negatives |
| Missing/duplicate/ambiguous joins, normalized aliases, facets and resolver scopes | Contracts `RouteCatalogValidatorTests`, `RouteCatalogV2Tests`; Server registry alias/overlap checks |
| Real owner policies and semantic equivalence/distinction | Sample `SampleCatalogTests`; Tenants `TenantIntentDeclarationTests` (all 12 operations, role forms, exact strings, Unicode, null/empty, target and trusted subject) |
| Complete Development profile and current targets | Server `DevelopmentRouteCatalogParityTests`; QueryRouting `DeclaredTenantQueriesUseActualQueryOverrideForEveryWireDiscriminator`; Tenants `TenantCatalogParityTests` (actual dispatcher output and stored contents) |
| Missing capability, target/source drift and bounded fallback evidence | Server missing/duplicate/changed-capability and effective-configuration rejection controls; actual compiled command/query inventories; historical global-administrator scope exclusion |
| Runtime override/production candidate rejection | Existing Contracts/Client rejection controls; real profile has no overrides and is explicitly unactivated |

### Verification history during real implementation

- `dotnet build tests/Hexalith.EventStore.Contracts.Tests/Hexalith.EventStore.Contracts.Tests.csproj -c Debug -p:UseHexalithProjectReferences=true -m:1 -v quiet` passed with zero warnings/errors. The initial existing routing lane passed 104/104. After v2 tests, 118/118 passed; additional normalized-alias and resolver-scope tests were then added and require the final rerun below. A new missing-field negative initially failed because its mutation did not remove the final JSON property; correcting the test mutation made the negative fail as intended beside its positive control.
- `dotnet build tests/Hexalith.EventStore.Sample.Tests/Hexalith.EventStore.Sample.Tests.csproj -c Debug -p:UseHexalithProjectReferences=true -m:1 -v quiet` passed with zero warnings/errors; `dotnet tests/Hexalith.EventStore.Sample.Tests/bin/Debug/net10.0/Hexalith.EventStore.Sample.Tests.dll -class '*SampleCatalogTests' -noLogo` passed 2/2.
- From Tenants, `dotnet build tests/Hexalith.Tenants.Server.Tests/Hexalith.Tenants.Server.Tests.csproj -c Debug -p:UseHexalithProjectReferences=true -m:1 -v quiet` built successfully, but the focused test run initially failed 30/30 at assembly loading: `Could not load file or assembly 'Hexalith.EventStore.Contracts, Version=3.128.2.0'`. Root source defaults were 3.128.0 while Tenants source edges set Version=3.128.2. Rebuilding with the temporary global `-p:Version=3.128.2` pin passed zero warnings/errors and all 30 focused cases. Source-only project-edge property propagation was then added in the owning build files; package pins remain unchanged. Normal unpinned source commands are the final verification requirement, not the temporary pin.
- The first new CLI async build rejected CA2007; adding `ConfigureAwait(false)` fixed it. A subsequent CLI run exposed the same source-graph identity mismatch for Gateway at capability reflection. Evaluation of ProjectReference metadata identified an ineffective initial MSBuild update condition; the path-based correction explicitly propagates the owning EventStore source version. No failed attempt wrote a catalog.
- `dotnet run tools/routing-catalog.cs -c Debug -p:UseHexalithProjectReferences=true -- development . deploy/dapr/eventstore-routing-catalog.json` then exited 0 and produced root `c156ce0b68bab43f561805db9cd02714d123026d388f65c764d7b6fa4e3b7308`, with 17 commands, nine queries and three callable projections. The tool explicitly reports unverified secret availability and activation.

The first unpinned Tenants rerun after edge propagation still failed 29/30 at DomainService/Client assembly loading. Investigation of evaluated references and `.deps.json` confirmed that restore did not propagate every edge override. Tenants now saves the centralized source version before applying its existing published pin, uses that source identity only in source mode, and preserves effective `Hexalith.EventStore.*` package versions with an explicit central-item update. MSBuild evaluation confirms source version 3.128.0 and package version 3.128.2. The normal Tenants command without a global Version override then passed with zero warnings/errors and **30/30 focused tests**.

The package gate is still blocked by release order. From Tenants, `dotnet build src/Hexalith.Tenants.Contracts/Hexalith.Tenants.Contracts.csproj -c Debug -p:UseHexalithProjectReferences=false --artifacts-path /tmp/story-5-12-package-check -m:1 -v quiet` exited 1 with zero warnings and ten errors: CS0234 (`Hexalith.EventStore.Contracts.Routing` is absent) and CS0246 (including `CommandIntentDeclaration`, `RouteContractDeclaration`, `IdempotencyDescriptorField`). The retained diagnostic log is `/tmp/story-5-12-tenants-package.log`. The pinned 3.128.2 EventStore packages predate these APIs. EventStore packages containing the construction seams must be released, then Tenants must consume/release its declarations, before EventStore's package-mode CI lanes can consume them. No version bump, dependency update, package publication, conditional exclusion of the new code, or CI success is claimed. This release-order gap keeps the overall story in-progress even when local Development construction/parity passes.

The normal root `dotnet build tests/Hexalith.EventStore.Server.Tests/Hexalith.EventStore.Server.Tests.csproj -c Debug -p:UseHexalithProjectReferences=true -m:1 -v quiet` passed zero warnings/errors. `dotnet tests/Hexalith.EventStore.Server.Tests/bin/Debug/net10.0/Hexalith.EventStore.Server.Tests.dll -class '*DevelopmentRouteCatalogParityTests' -class '*TrustedIdempotencyIntentTests' -noLogo` passed 27/27; the separate `-method '*Submit_GlobalAdministratorCommandCapturesAuthenticatedSubjectWithoutReceiptOrExtensionFallback'` run passed 1/1. An initial combined class-and-method filter selected zero tests and is not evidence; the separate nonzero runs are the evidence. The Counter test executes HandlerAwareQueryRouter over the actual QueryRouter and observes `counter:tenant-a:counter-1` / `ProjectionActor` at the invoker boundary, with no HTTP query invocation. The global-administrator scope test proves that an unmatched tenant resolves by convention to a different app outside the catalog's `system` scope. A further exact-tenant override negative was added after this run and is included in final validation.


### Final focused implementation evidence (2026-10-26)

All commands below use the normal source graph, without a global Version override. Builds are individual Debug projects with `-p:UseHexalithProjectReferences=true -m:1 -v quiet`; all completed with zero warnings/errors. The final source graph no longer references the nonpackable Tenants executable from root construction or root tests. Packaged Tenants.Server owns the capability enrollment; Tenants-owned tests load its actual executable handlers and dispatcher.

| Project / build command | Test invocation after build | Result |
| --- | --- | --- |
| `dotnet build tests/Hexalith.EventStore.Contracts.Tests/Hexalith.EventStore.Contracts.Tests.csproj -c Debug -p:UseHexalithProjectReferences=true -m:1 -v quiet` | `dotnet tests/Hexalith.EventStore.Contracts.Tests/bin/Debug/net10.0/Hexalith.EventStore.Contracts.Tests.dll -class '*Routing.*' -noLogo` | 131 passed |
| `dotnet build tests/Hexalith.EventStore.Sample.Tests/Hexalith.EventStore.Sample.Tests.csproj -c Debug -p:UseHexalithProjectReferences=true -m:1 -v quiet` | `dotnet tests/Hexalith.EventStore.Sample.Tests/bin/Debug/net10.0/Hexalith.EventStore.Sample.Tests.dll -class '*SampleCatalogTests' -noLogo` | 4 passed, including missing/non-string target-schema negatives |
| From Tenants: `dotnet build tests/Hexalith.Tenants.Server.Tests/Hexalith.Tenants.Server.Tests.csproj -c Debug -p:UseHexalithProjectReferences=true -m:1 -v quiet` | `dotnet tests/Hexalith.Tenants.Server.Tests/bin/Debug/net10.0/Hexalith.Tenants.Server.Tests.dll -class '*Routing.*' -noLogo` | 30 passed, including actual handler inventory by full declaration digest and persisted projection outputs |
| `dotnet build tests/Hexalith.EventStore.QueryRouting.Tests/Hexalith.EventStore.QueryRouting.Tests.csproj -c Debug -p:UseHexalithProjectReferences=true -m:1 -v quiet` | `dotnet tests/Hexalith.EventStore.QueryRouting.Tests/bin/Debug/net10.0/Hexalith.EventStore.QueryRouting.Tests.dll -class '*DaprDomainQueryInvokerTests' -class '*HandlerAwareQueryRouterTests' -noLogo` | 15 passed; the owner-discriminator case exercises all eight Tenants query types |
| `dotnet build tests/Hexalith.EventStore.Server.Tests/Hexalith.EventStore.Server.Tests.csproj -c Debug -p:UseHexalithProjectReferences=true -m:1 -v quiet` | `dotnet tests/Hexalith.EventStore.Server.Tests/bin/Debug/net10.0/Hexalith.EventStore.Server.Tests.dll -method '*Submit_GlobalAdministratorCommandCapturesAuthenticatedSubjectWithoutReceiptOrExtensionFallback' -noLogo` | 1 passed |

CLI commands all exited 0 with canonical root `c156ce0b68bab43f561805db9cd02714d123026d388f65c764d7b6fa4e3b7308`. The resulting schema-v2 file contains **29 routes: 17 commands, nine queries and three callable projections**. `cmp` proved repeated generation equals the retained deployment file:

```text
dotnet run tools/routing-catalog.cs -c Debug -p:UseHexalithProjectReferences=true -- development . /tmp/story-5-12-development-repeat.json
cmp deploy/dapr/eventstore-routing-catalog.json /tmp/story-5-12-development-repeat.json
dotnet run tools/routing-catalog.cs -c Debug -p:UseHexalithProjectReferences=true -- check-development . deploy/dapr/eventstore-routing-catalog.json
dotnet run tools/routing-catalog.cs -c Debug -p:UseHexalithProjectReferences=true -- validate deploy/dapr/eventstore-routing-catalog.json c156ce0b68bab43f561805db9cd02714d123026d388f65c764d7b6fa4e3b7308
```

Root and Tenants `git diff --check` passed. `bash scripts/check-committed-files.sh` passed all 9,128 tracked entries. These focused checks are implementation evidence; the parent review and broad affected suites are recorded separately when completed. Existing package-mode CI is not bypassed: the release-order blocker above remains explicit, and the story remains in-progress.


The final root command `dotnet tests/Hexalith.EventStore.Server.Tests/bin/Debug/net10.0/Hexalith.EventStore.Server.Tests.dll -class '*DevelopmentRouteCatalogParityTests' -class '*TrustedIdempotencyIntentTests' -class '*DefaultProjectionActorInvokerTests' -noLogo` passed **38/38** after a zero-warning/error rebuild. It includes complete command inventory positives beside missing/duplicate actual Handle and missing-adapter negatives, mandatory missing/duplicate/drifted capability rejection inside BuildAsync, extra contract-version configuration rejection, wildcard/exact tenant-scope drift, legacy normalized aliases and ambiguity, the actor fallback, and bounded convention resolution. Together with the other focused lanes above, **219 cases passed with zero failures or skips**. The matrix above maps each required scenario to these tests; runtime target assertions do not claim projection:query workload admission or catalog activation.


The root package-consumer boundary was also checked after removing the nonpackable host reference. `dotnet build tools/Hexalith.EventStore.CatalogConstruction/Hexalith.EventStore.CatalogConstruction.csproj -c Debug -p:UseHexalithProjectReferences=false --artifacts-path /tmp/story-5-12-root-package-check -m:1 -v quiet` exited 1 with zero warnings and two CS0234 errors: the pinned published Tenants.Contracts and Tenants.Server packages do not yet contain their new `Routing` namespaces. Full output is retained at `/tmp/story-5-12-root-package.log`. There are no references to Tenants executable-only query/dispatcher namespaces in the root composition. The remaining failure is the actual unreleased packable owner API boundary; release/pin updates remain unperformed and out of this implementation scope.

### Parent source acceptance audit (2026-10-26)

The parent inspected the baseline diff and the separate Tenants changes, checked the generated profile and owner definitions, and matched the frozen matrix to the 219 executed focused cases. Local implementation tasks are complete. The existing package/CI task and broad affected verification remain open; independent source review proceeds without implying those gates passed. Sprint status stays in-progress, and no Git history or dependency mutation is authorized.

### Review correction plan and KEEP instructions (2026-10-26)

Trigger: R3B3/R3B4/R3B5 showed that independent, partial reflection is not proof of executable command dispatch. The plan now requires the existing dispatcher discovery rules to be shared internally with offline verification (or reused through an existing SDK seam), including accepted state/return/envelope/cancellation signatures, overload preference, contract/CLR aliases, actual domain and complete hosted aggregate discovery. Do not add an unnecessary public API or copy a second divergent dispatcher algorithm. Preserve public constructor compatibility and runtime behavior. Private/internal refactoring and friend access for the nonpackable construction assembly are allowed where needed; no new package or dependency version is authorized.

KEEP: retain all working v1 codec/fixture bytes and digests; retain the approved v2 metadata, 29-route Development catalog and root c156ce0b68bab43f561805db9cd02714d123026d388f65c764d7b6fa4e3b7308 unless a demonstrated semantic correction requires changing it; retain all 17 owner policies and exact payload/subject semantics; preserve source/package separation, existing published pins, source-version fixes and the packable Tenants capability seam. Preserve all unrelated committed query/provenance work and prior tooling corrections. Do not stage, commit, push, branch, publish, provision, activate, update dependencies or initialize submodules. Do not load skills under references.

Remaining review correction tasks:

- [ ] R3B1/R3B2: Make Development configuration intake bounded before allocation/parsing, bind the exact retained bytes, and translate conversion failures into value-free catalog diagnostics. Test invalid conversion and oversized source files through the real CLI beside passing controls; failed commands must preserve existing output.
- [ ] R3B3/R3B4/R3B5: Reuse effective runtime aggregate/Handle discovery for complete hosted inventory, valid signatures, cancellation-overload preference, exact aliases and domain parity. Add negatives for invalid Handle shape, undeclared hosted aggregate, changed/extra/missing alias/domain, and a positive supported cancellation overload. Demonstrate actual dispatch agreement without changing dispatch behavior.
- [ ] R3B6: In the Tenants owning host tests, obtain actual registered query handler instances and compare every executable Domain/QueryType and attached declaration to the packaged capability enrollment. A declaration/runtime mismatch and missing registration must fail. Keep root construction free of executable-host references.
- [ ] R3B7/R3B8: Validate the authenticated-subject descriptor relationship; deep snapshot policy collections and registry route matching metadata so caller mutation cannot alter startup-validated routing or schemas. Add focused mismatch/mutation controls without enabling admission or trusting extensions.
- [ ] R3B9/R3V1: Extend the real Tenants operational-metadata HTTP test to verify actual DI enrollment, both callable projection names, exact legacy secondary outputs, and absence of invented named capabilities. Verify the registration/Bind omission would fail the assertion.
- [ ] R3B10: Extend the independent fixture generator and checked-in fixtures with schema-v2 declaration, route/idempotency facet and root vectors covering aliases, actor type, delegated context, outputs and resolver tenant scope. Test exact bytes/digests independently of production generation; leave v1 vectors unchanged.

Run focused affected checks for these corrections, not broad suites; parent broad verification follows the next review. Record exact commands, nonzero test counts and matrix coverage. Keep source implementation reviewable while package/CI completion remains blocked by the recorded release dependency. The original baseline and frozen approved intent are unchanged.
