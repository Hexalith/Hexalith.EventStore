---
title: Architecture Update 2026-10-07b Review - Technology Reality
date: 2026-10-07
lens: technology-reality
subject: architecture.md working tree
assurance: tool-persona evidence only
---

# Architecture Update 2026-10-07b Review - Technology Reality

> **Evidence class: `tool-persona`.** This review is evidence only. It is not an approval, a ratification, or an attestation. It records no assurance level for any gate and changes no gate result. The owner holds every role.

- **Subject:** `_bmad-output/planning-artifacts/architecture.md` working tree, SHA-256 `d3e5fcc5d0f6961c3976c31546793774f77e24993f704f8a7da663d012cb0b2a` (571 lines). The base is `HEAD` `4e4ee858`, whose spine bytes are unchanged since `48ef7171`.
- **Scope:** only the text this run changed (`git diff HEAD -- architecture.md`). That covers AD-5 **Append race**, AD-10 **JWT contract**, AD-11 **Compatibility authority**, AD-12 **Gate validators**, AD-17 **MessageId contract version**, AD-24 **Secret contract and rotation**, the AD-26 Rule, **Ratification** and **Production proof**, AD-27, AD-28, AD-33, AD-35, the Stack intro line and Code coverage row, and the changed Implementation Status rows. Owner decisions are read from the memlog after "Update run 2026-10-07b opened".
- **Method:** read-only. No build, test, Git mutation, or submodule update was run. Upstream checks used Dapr runtime source at tag `v1.18.4` and on branches `release-1.18` and `v1.19.0-rc.1`; components-contrib on `release-1.18`; docs.dapr.io, including the v1.18 docs; docs.github.com; the GitHub Releases and Rulesets APIs; and the NuGet flat-container API. All lookups were made on 2026-10-07.

## Verdict

The version and repository facts in the changed text hold. The Builds gitlink, every Stack pin, the committed fallback policy, the framework-route mappings, the tenant comparisons, the actor-interface signatures, and the release inventory all check out. Three committed decisions still rest on an unchecked or wrong technology premise:

1. AD-5 and AD-26 bind the actor-state **key prefix** into the digest. In Dapr, `keyPrefix` never applies to actor state, and actor records carry no namespace. The premise came from a false claim in this morning's technology review.
2. AD-24 lists exactly two internal proofs to move off the AD-25 digest ring. Committed code already HMACs three proofs with that ring, and the interim retirement clause protects only one of them.
3. AD-28's exit condition depends on qualifying a runtime control that does not exist in Dapr 1.18.4 or 1.19.0-rc.1, so the condition cannot be met as written. Its likely false positives, service-invocation ACLs and the API allowlist, would remove a safety control.

There are **0 critical, 3 high, 5 medium and 4 low** findings.

## Answers to the requested questions

### Q1 - Several `actorStateStore: true` components, one per app ID

- **Permitted on Dapr 1.18.** The "single actor state store" limit applies per sidecar, after scope filtering, not per namespace.
  - The sidecar drops every component not scoped to its app ID before it loads it (`pkg/runtime/authorizer/authorizer.go:124-129`, `namespaceComponentAuthorizer` -> `comp.IsAppScoped(a.id)`, `v1.18.4`).
  - Only components that survive the filter reach `AddStateStoreActor`. That function rejects a second actor store **inside that sidecar's component store** with `detected duplicate actor state store` (`pkg/runtime/compstore/statestore.go:30-36`, `v1.18.4`).
  - The docs sentence "Only a single state store component can be used as the state store for all actors" (https://docs.dapr.io/developing-applications/building-blocks/state-management/state-management-overview/) is therefore per app. A blog claim of "one per namespace" (oneuptime.com, 2026-03-31) is not supported by the source.
  - An app that hosts actors needs one: `v1.18.4` `pkg/actors/actors.go:198` logs "Actor state store not configured - actor hosting disabled ..., but invocation enabled". Operations (`DeadLetterDrainActor`, `src/Hexalith.EventStore.Operations/Program.cs:29-30`) and DomainService reminder hosts (`ReminderActor`, `src/Hexalith.EventStore.DomainService/EventStoreReminderServiceCollectionExtensions.cs:104-114`) therefore each need an actor state store, as AD-5 says.
- **`keyPrefix` does not touch actor state.**
  - Actor keys are built from the sidecar's own app ID only: `key.ConstructComposite(s.appID, actorType, actorID, key)` (`pkg/actors/state/state.go:106,210,270-271`, `v1.18.4`). No `keyPrefix` strategy applies.
  - The `keyPrefix` strategies (`appid`, `namespace`, `name`, `none`, custom) apply only to state-API keys (`pkg/components/state/state_config.go:67-95`).
  - Every state-API get, bulk, save, delete, and transaction path runs `checkKeyIllegal`, which rejects any key or prefix containing `||` (`state_config.go:68,130-132`; call sites `pkg/api/http/http.go:532,638,945,1030,1537,1563`, `pkg/api/grpc/grpc.go:605,672,751,838,890,960`).
  - So no state-API caller, under any `keyPrefix`, can write a 3-separator actor key. Another app ID cannot address this app's actor keys through the actor API either, because the key carries the caller's own app ID.
- **The real partition is app ID x physical table, not namespace and not prefix.**
  - Dapr v1.18 docs: "No namespace information is written as part of the actor record, and hence separate state stores are required for each namespace" (https://v1-18.docs.dapr.io/developing-applications/building-blocks/actors/namespaced-actors/). The docs' examples separate stores by `tableName`, `keyPrefixPath`, or `redisDB`, not by Dapr `keyPrefix`.
  - A same-app-ID sidecar in another namespace or environment on the same table is the Dapr-level second writer. So is any direct database connection.
- **"Key prefix bound into the digest" therefore means nothing for actor state.** What is meaningful is:
  - the app ID;
  - namespace uniqueness of that app ID per physical target;
  - the component's physical target: connection database and schema, `tableName`, and `metadataTableName`.
  
  See H1.

### Q2 - Can actor invocation be restricted to the hosting app ID?

- **Service-invocation `accessControl` does not apply to actor invocation.**
  - In `v1.18.4`, `CallLocal`/`CallLocalStream` run `callLocalValidateACL` (`pkg/api/grpc/daprinternal.go:57,126,391-416`).
  - `CallActor`/`CallActorStream` run only `callActorValidateWorkflowACL` (`:299,363`). That check returns immediately for any actor type that is not a workflow or activity actor (`pkg/api/grpc/workflow_acl.go:35-36`).
  - The docs scope ACLs to "operations ... via service invocation" (https://docs.dapr.io/operations/configuration/invoke-allowlist/).
- **The only actor-side policy is `WorkflowAccessPolicy`** (v1alpha1 CRD, `pkg/apis/workflowaccesspolicy`, https://docs.dapr.io/operations/security/workflow-access-policy/). It covers workflow and activity actor types only. `v1.19.0-rc.1` has the same `CallActor` code and no other access-policy API.
- **The API allowlist is coarse.** `spec.api.allowed/denied` works per building block and version, not per actor type (https://docs.dapr.io/operations/configuration/api-allowlist/). It can deny the whole `actors` API to an app that hosts no actors. It cannot do that for Operations or reminder-hosting domain services, which AD-5 names as actor hosts.
- **Kubernetes NetworkPolicy on the sidecar internal port is also too coarse.** That port also carries the service invocation that Admin Server, Tenants API, and Sample API use into `eventstore` (`deploy/dapr/accesscontrol.yaml:32-44`).
- **Conclusion:** on Dapr 1.18 the AD-28 clause "until AD-34 qualifies that the profile restricts actor invocation ... to their hosting app ID" cannot be met. It makes the signed execution context permanent in practice. That outcome is safe. The risk is that Story 5.7's qualification row records a control that does not cover `CallActor`. See H3.

## Findings

### High

#### H1 - AD-5 and AD-26 bind a parameter that does not partition actor state, and omit the ones that do

- **Location:**
  - AD-5 **Append race** `:150` ("with its key prefix bound into the AD-26 digest").
  - AD-26 **Production proof** `:345` ("PostgreSQL and broker components with every actor-state component and its key prefix").
  - AD-26 Rule `:341`, one `state.postgresql` v1 component per actor-hosting app ID.
- **Evidence:**
  - Q1 above: actor keys use only the app ID (`state.go:270-271`), `keyPrefix` applies only to state-API keys, and state-API keys cannot contain `||` (`state_config.go:130-132`). Actor records carry no namespace (v1.18 namespaced-actors doc).
  - **Provenance of the error.** The clause traces to `review-update-2026-10-07-technology-reality.md` H3. That review claimed "Even with the default `appid` prefix, the `eventstore` app's own state-API key `AggregateActor||<id>||<k>` becomes the actor key" and recommended binding `keyPrefix`. The `checkKeyIllegal` source above refutes that claim, and this review corrects it.
  - **A further PostgreSQL v1 trap for per-app components on one database:**
    - The v1 migration level is stored in the metadata table under the fixed key `migrations` (components-contrib `release-1.18` `state/postgresql/v1/migrations.go:34-38`). Both table names default (`tableName` `state`, `metadataTableName` `dapr_metadata`; `common/component/postgresql/v1/metadata.go:28-29`).
    - A second component that shares the database and metadata table, but has its own `tableName`, reads the shared migration level and skips creating its state table.
    - The `last-cleanup` key is shared too (`common/component/postgresql/v1/postgresql.go:160-168`).
  - **Where per-app scoping really helps.** The v1 Query API selects from the whole table, with no key-prefix filter (`postgresql_query.go:222-225`). Every app scoped to an actor-state component can therefore read every app's actor rows. Per-app scoping is the right control for that **read-disclosure** path, not for second-writer exclusion.
- **Closing wording:**
  - In AD-5, replace "with its key prefix bound into the AD-26 digest" with: "with its app ID, namespace, and physical target (database and schema, `tableName`, and a `metadataTableName` unique to that table) bound into the AD-26 digest, and no other component, namespace, or environment with the same app ID targeting that table. Dapr `keyPrefix` never applies to actor state."
  - In the AD-26 Production proof, replace "every actor-state component and its key prefix" with "every actor-state component with its app ID, namespace, and physical target".
  - Optionally add to AD-5: "Per-app scoping is required because any app scoped to an actor-state component can read every row through the state Query API."

#### H2 - AD-24 names two internal proofs. Three digest-ring HMACs already exist, and the interim retirement guard covers one

- **Location:**
  - AD-24 **Secret contract and rotation** `:309` ("Internal proofs, namely the trusted-effect gateway proof and the AD-28 execution context, use dedicated keys ... never the AD-25 digest ring. A digest-key generation retires only after every registered consumer, including any trusted-effect gateway proof not yet moved to its dedicated key, shows no live reference").
  - Memlog decision #6: "today TrustedEffectGatewayProof HMACs with the IdempotencyDigestKeyRing active version".
- **Evidence:** committed code HMACs three internal proofs with the AD-25 ring:
  - `src/Hexalith.EventStore.Server/Commands/TrustedEffectGatewayProof.cs:11,20-26,49-59` (named).
  - `src/Hexalith.EventStore.Server/Commands/IdempotencyExecutionContextProtector.cs:14-22,213-223`. Domain `execution-fence-v1`, keyed by `RentKeyMaterial(context.DigestKeyVersion)`. This is the existing AD-5 fenced context, which AD-28 `:357` makes the execution context for mutating aggregate methods. "The AD-28 execution context" therefore covers it implicitly. The interim retirement clause does not, and fence capabilities carry the digest-key version.
  - `src/Hexalith.EventStore.Server/Commands/TrustedEffectErasureCapability.cs:10-12,29-34,55-65` ("Signs lifecycle purge decisions for exact actor partitions and inventories"). It is **not named**, so under a closed "namely" list it may stay on the ring.
  - No Implementation Status row owns the key migration.
- **Risk:** after the move, retiring a digest-key generation passes the interim guard while erasure capabilities or fence capabilities signed under it are still outstanding. Verification then fails closed and blocks tenant purge or fenced execution.
- **Closing wording:**
  - "Internal proofs, namely the trusted-effect gateway proof, the trusted-effect erasure capability, the AD-5 fenced execution context (the AD-28 execution context for mutating methods), and any later internal proof, use dedicated keys inventoried here, never the AD-25 digest ring. A digest-key generation retires only after every registered consumer, including any internal proof not yet moved to its dedicated key, shows no live reference."
  - Add an owner row, for example Story 5.12 or a Security-owned story, for moving the three committed proofs and versioning `IdempotencyExecutionContext`.

#### H3 - The AD-28 exit condition depends on a Dapr control that does not exist, and the likely stand-ins would wrongly remove the execution context

- **Location:**
  - AD-28 `:357` ("Until AD-34 qualifies, as a Story 3.17 inventory row for that profile and runtime, that the profile restricts actor invocation and channel-only routes to their hosting app ID ...").
  - Implementation Status row `:544` (Story 5.7 owns "AD-34 qualification of actor-invocation restriction").
- **Evidence:** Q2 above: `CallActor` bypasses `accessControl` (`daprinternal.go:299` vs `:57`), and `WorkflowAccessPolicy` is limited to workflow actors (`workflow_acl.go:35-36`); both hold in `v1.18.4` and `v1.19.0-rc.1`. The API allowlist cannot be applied to actor-hosting peers.
  - Story 5.7 (working-tree `epics.md:4521-4524`) also adds service-invocation ACL deny rules for channel-only routes. Those rules close the service-invocation path to `/actors/...` and `/dapr/subscribe`, **not** the `CallActor` path. A qualification row built from ACL tests would therefore record a false "restricted".
- **Closing wording:** add to AD-28: "Service-invocation `accessControl`, the API allowlist, and `WorkflowAccessPolicy` do not restrict invocation of a user-defined actor type, and Dapr 1.18 provides no control that does. A qualifying row must show an observed mTLS cross-app `CallActor` to each hosted actor type denied by the runtime. Until a runtime provides that, the execution context is the standing requirement." Mirror the evidence requirement in the Story 5.7 AC when epics are next edited.

### Medium

#### M1 - The ratification subject includes the status marker that ratification flips

- **Location:** AD-26 **Ratification** `:343`. The subject runs "from its heading through its last non-blank line". The heading `:337` carries `[ASSUMPTION]`, and `:343` says "The target stays `[ASSUMPTION]` until ...". The Invariants intro `:111` also says only the AD-26 target carries `[ASSUMPTION]`.
- **Evidence:**
  - The subject is computable today. The 10 lines (1 AD-5 paragraph + 9 AD-26 lines), LF-terminated, are 6,327 bytes with SHA-256 `16ab54f4e6a84dca2330df8646d0aabb48904765b06c8a93bca05f88bb2bc436`. The file is `eol=lf` with no CR and no trailing whitespace. This digest is informational, not a pin.
  - Recording the target as adopted edits the heading, and probably the "stays `[ASSUMPTION]`" sentence. Both are **inside** the subject, so the post-ratification digest differs from the one the record bound.
  - The spine says only edits **outside** the subject leave a record valid. It also restarts the AD-11 24-hour window on any subject edit.
- **Closing wording:** add: "The subject excludes the AD-26 heading's status marker. Replacing `[ASSUMPTION]` with `[ADOPTED]` in the heading, and removing the 'stays `[ASSUMPTION]`' clause, after valid records exist is the only subject edit that neither invalidates them nor restarts the window." Alternatively, define the subject with the marker normalized.

#### M2 - Admin tenant checks also default a missing tenant, which the changed AD-27 row omits

- **Location:** Implementation Status, Canonical tenant boundary `:552` ("Admin Server's tenant checks compare tenants without canonicalizing").
- **Evidence:** the comparison claims are true:
  - `ClaimsTenantValidator.cs:45` (`Ordinal`);
  - Admin Server `AdminTenantAuthorizationFilter.cs:43` (`StringComparer.Ordinal`);
  - `DaprStreamQueryService.cs:160` (`OrdinalIgnoreCase`).
  
  Both `AdminTenantAuthorizationFilter` copies also replace a missing `tenantId` with the caller's first tenant claim: `src/Hexalith.EventStore.Admin.Server/Authorization/AdminTenantAuthorizationFilter.cs:32-38` and `src/Hexalith.EventStore/Authorization/AdminTenantAuthorizationFilter.cs:34-40`. AD-27 `:351` requires "exactly one explicit tenant" and lists defaulted tenants under Prevents. Working-tree Story 2.14 (`epics.md:2071-2074`) covers comparison only.
- **Closing wording:** "... the gateway `ClaimsTenantValidator` and Admin Server's tenant checks compare tenants without canonicalizing, and both `AdminTenantAuthorizationFilter` copies default a missing tenant to the caller's first grant ...". Story 2.14 then removes the defaulting.

#### M3 - AD-27 and AD-33 rely on an operation vocabulary that has no code and no owner

- **Location:**
  - AD-33 `:387` ("the AD-10 human-bearer operation claim or the AD-36 workload operation ... declared in the `Contracts` route declaration").
  - AD-27 `:351` ("authorized externally only through the AD-10 human-bearer operation claim").
- **Evidence:**
  - Only the workload claim exists: `eventstore:operation` (`src/Hexalith.EventStore.ServiceDefaults/Authentication/EventStoreWorkloadAuthenticationDefaults.cs:46`), with its vocabulary in ServiceDefaults (`EventStoreWorkloadOperations.cs:9-27`).
  - No human-bearer operation claim type exists, and no `epics.md` story mentions one.
  - `Contracts` references only `Hexalith.Commons.UniqueIds` (`src/Hexalith.EventStore.Contracts/*.csproj:9-10`). ServiceDefaults references no Contracts. A `Contracts` route declaration therefore cannot name a workload operation without moving or duplicating that vocabulary. Memlog Adv M1 is deferred to the Story 5.12 spec freeze.
- **Closing wording:** add an owner, Story 5.11 or 5.12, for defining the human-bearer operation claim type and moving both operation vocabularies to `Contracts`. Alternatively, state in AD-33 that the vocabularies are string-identical by guard until that move.

#### M4 - AD-5 retires the documented "domain services have zero state access (D4)" posture without saying so

- **Location:** AD-5 `:150` ("domain services that use typed reminders has its own `actorStateStore: true` component") and AD-26 Rule `:341`.
- **Evidence:**
  - `deploy/dapr/statestore-postgresql.yaml:9-14,30-33` says "DO NOT add new domain services to scopes ... Domain services have zero state store access (D4)". The AppHost `statestore.yaml:15-20` and `docs/guides/deployment-azure-container-apps.md:17,94,398` say the same.
  - Dapr 1.18.4 disables actor hosting without an actor state store (`actors.go:198`), so reminder-hosting domain services need one. The spine is right and the repository guidance is stale.
  - Under AD-24, each such app's sidecar now needs PostgreSQL credential custody.
- **Closing wording:** add to AD-5: "This supersedes the D4 zero-state posture for actor-hosting domain services only. Their actor-state component and its credentials are inventoried under AD-24 and AD-26." Story 5.7 updates the template headers and the guide.

#### M5 - The changed owner rows and the ratification subject name stories that exist only in the uncommitted epics

- **Location:**
  - AD-26 **Production proof** `:345`, inside the ratification subject: Stories 3.21, 5.12, 5.13, 7.22.
  - Implementation Status `:540-563`: Stories 3.21, 5.12, 5.13, 5.14, 7.22, and the "as extended on 2026-10-07" Story 2.14.
- **Evidence:**
  - `git show HEAD:_bmad-output/planning-artifacts/epics.md` has 0 headings for Stories 3.21, 5.12, 5.13, 5.14 and 7.22. They exist only in the working tree (`epics.md:3237,4727,4763,4799,6650`) and in uncommitted `sprint-status.yaml:178,250-252,300`.
  - The routing proposal at `HEAD` already names them, because it was absorbed into `af2892e8`.
- **Closing wording:** none for the spine. Commit the spine with the epics and sprint-status changes, or before them, in one change, so the subject digest never binds stories that are absent from `epics.md`. (`concurrent-bmad-loop-git` risk.)

### Low

- **L1 - AD-12 seal mechanics need GitHub-specific care.**
  - **Location:** `:218`.
  - **Evidence:**
    - Required checks skipped by path or branch filters "stay in a 'Pending' state and block merging". A job skipped by a conditional "reports 'Success'" (https://docs.github.com/en/pull-requests/collaborating-with-pull-requests/collaborating-on-repositories-with-code-quality-features/troubleshooting-required-status-checks).
    - Ruleset `14479516` (`Protect`) gives two users `bypass_mode: always` (`gh api repos/Hexalith/Hexalith.EventStore/rulesets/14479516`), so "required" never stops a transition from landing.
    - `release-available`, `production-promoted`, and consumer-removal transitions are records, not necessarily PR merges.
  - **Closing wording:** "The transition workflow runs on every pull request to `main` and skips its evaluation job by condition, never by path filter. A seal validator requires the run's presence and never relies on merge blocking. A transition not realized as a pull request is triggered through a protected environment."
- **L2 - Stale downstream reference.** Working-tree Story 5.7 still says the result "neither adopts nor rejects the AD-28 `[ASSUMPTION]`" (`epics.md:4524`), but this run resolved that assumption. Route it to the Story 9.3 repin or the next correct-course.
- **L3 - AD-35 "admin operations terminating at Admin Server" is not enforced by topology.**
  - **Location:** `:403`.
  - **Evidence:** the gateway also serves `[Authorize]` admin controllers on its public edge (`src/Hexalith.EventStore/Controllers/AdminStorageCommandController.cs:28-29` `api/v1/admin/storage`; `AdminStreamQueryController.cs:36-38`). Admin Server calls those routes (`DaprProjectionCommandService.cs:63-96`).
  - **Closing wording:** "the McpCli inventory guard rejects any gateway `api/v1/admin/*` target."
- **L4 - Informational.** Dapr `v1.19.0-rc.1` (2026-10-01) and `v1.18.5-rc.2` (2026-10-06) are published. `1.18.4` is still the latest stable release (GitHub Releases API), so the unchanged DAPR runtime row stays accurate. The AD-5 minor-version re-proof clause fires at 1.19 GA.

## Verified true (no finding)

- **Builds gitlink and Stack pins.**
  - The Builds gitlink at `HEAD` is `397c94a4` (`git ls-tree HEAD references/Hexalith.Builds`), and the submodule worktree is at the same commit.
  - At `397c94a4`, `Props/Directory.Packages.props` pins:
    - `Microsoft.Testing.Extensions.CodeCoverage` `18.12.0` (`:267`; the NuGet latest is `18.12.0`);
    - `Aspire.Hosting` `13.6.0` (`:135`; NuGet has `13.6.1`, as the Stack row says);
    - Dapr .NET SDK `1.18.10` (`:161-168`);
    - MediatR `14.2.0` (`:194`) and FluentValidation `12.1.1` (`:173`);
    - OpenTelemetry `1.19.1`, with instrumentation `1.19.0` (`:290-297`);
    - xunit.v3 `4.0.1` (`:343`), Shouldly `4.3.0` (`:318`), and NSubstitute `6.2.0` (`:283`);
    - FrontComposer `4.5.0` (`:10`) and Fluent UI `5.0.0` (`:250-251`).
  - Between `ba4ca78c` and `397c94a4`, CodeCoverage is the only change that touches a Stack row.
  - `global.json` is SDK `10.0.401`/`latestPatch`, and ASP.NET Core packages are `10.0.12`.
- **TrustedEffectGatewayProof.** It HMACs with `IdempotencyDigestKeyRing.ActiveVersion` (`TrustedEffectGatewayProof.cs:20-26`).
- **DomainService fallback policy.** It is committed in `c4d5455a`, which is on `origin/main` (`EventStoreDomainServiceSecurityExtensions.cs:79-81`). It is the only `FallbackPolicy` in `src`.
- **Framework routes.**
  - The gateway maps `MapSubscribeHandler`/`MapActorsHandlers` without a policy (`src/Hexalith.EventStore/Program.cs:47-48`), and so does Operations (`src/Hexalith.EventStore.Operations/Program.cs:43-44`). Operations has no authentication or authorization middleware.
  - The DomainService SDK applies `RequireEventStoreSidecarChannel()` (`EventStoreReminderEndpointExtensions.cs:32`).
- **Operations dead-letter authorization.** It uses caller app ID plus a non-empty bearer that is never validated (`DeadLetterOperationsEndpointExtensions.cs:227-234`).
- **State components and ACLs.**
  - `deploy/dapr/statestore-postgresql.yaml:34-36` scopes `eventstore` and `eventstore-admin`, with no `keyPrefix`.
  - The AppHost store adds `tenants` with `keyPrefix: none` (`src/Hexalith.EventStore.AppHost/DaprComponents/statestore.yaml:41-53`).
  - Admin reads the EventStore-written index `admin:stream-activity:all` (`DaprStreamQueryService.cs:146`, `DaprStreamActivityTracker.cs:20`). That is cross-app state that AD-5 now moves to a separately scoped component.
  - The production ACL grants `eventstore-admin` `/**` (`deploy/dapr/accesscontrol.yaml:42-44`).
- **Package references.** ServiceDefaults references no Contracts project (`Hexalith.EventStore.ServiceDefaults.csproj:17-24`).
- **Actor interfaces.**
  - `IAggregateActor.GetEventsAsync(long)` (`IAggregateActor.cs:44`) and `IETagActor.RegenerateAsync()` (`IETagActor.cs:22`) take no execution context.
  - Both interfaces are `public` in `Hexalith.EventStore.Server`, which is a released package (`tools/release-packages.json:12-13`). So the AD-28 change is an NFR12 surface change, as the memlog says.
  - The unfenced `ProcessCommandAsync(CommandEnvelope)` is still present (`:35`).
- **Other cross-references.**
  - `RestTenantSource.System` exists (`src/Hexalith.EventStore.Contracts/Rest/RestTenantSource.cs:15`).
  - PRD NFR1 carries the anonymous-endpoint rule (`prd.md:361`).
  - The routing proposal has the Group 5 staged table (`sprint-change-proposal-2026-10-07-architecture-routing.md:402-415`).
  - `ReleaseEvidenceCodec` is absent from EventStore `src`/`tools` and from Builds at `397c94a4`.
  - `JwtTrustedEffectDelegationVerifier` exists outside the contract (`src/Hexalith.EventStore/Authentication/JwtTrustedEffectDelegationVerifier.cs:15`), as AD-10 says.
- **Dapr component support.** `state.postgresql` v1 is stable, supports `actorStateStore`, and has no plan to deprecate it (https://docs.dapr.io/reference/components-reference/supported-state-stores/setup-postgresql-v1/).
