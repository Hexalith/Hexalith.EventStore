# Architecture Spine Validate Report — 2026-09-08

**Subject:** `_bmad-output/planning-artifacts/architecture.md` (symlinked as `ARCHITECTURE-SPINE.md`), `status: final`, `updated: 2026-08-29`, 25 ADs, 610 lines.
**Intent:** Validate (critique only; the spine was not changed).
**Gate composition:** `lint_spine.py` (deterministic) + rubric walker + the two configured floor lenses (technology currentness, adversarial divergence) + two ad-hoc lenses warranted by platform altitude (security/data-integrity, brownfield ratification + input reconciliation). Five parallel independent reviewers; full texts in this folder as `review-validate-2026-09-08-*.md`.

## Gate verdict

**The spine does not pass the gate as it stands.** Two critical divergences are corroborated by two or three lenses each and are *already visible in the source tree*, so the level below is diverging today, not hypothetically. Eleven high findings follow, most of them the same shape: an AD names a property ("attributable", "application-layer credential", "tenant authorization", "ProjectionVersion") without fixing the one rule two builders would need to agree on. The mechanical floor is clean: lint 0 findings, every FR1-FR37 and NFR1-NFR19 is bound and matches the PRD inventory, all 27 cited sources exist, and the prior AD-11/AD-22/AD-24 closures from the 2026-07-19 and 2026-08-16 gates still hold.

| Severity | Raw findings (5 lenses) | Consolidated clusters |
| --- | --- | --- |
| Critical | 2 | 2 |
| High | 16 | 10 |
| Medium | 20 | 8 |
| Low | 15 | 9 |

## Critical

### V-C1 — Production actor state-store provider is undecided while the AppHost default is the provider with a proven silent overwrite
- **Raised by:** Security C1, Adversarial H3, Rubric H1.
- **Spine:** `architecture.md:121` (AD-5 disclaims write-once enforcement), `:452-458` (AD-25 names `oq8-postgresql-v1` only as an *evidence profile*), `:606` (Deferred: Redis `state.redis` durable write at sequence 1 silently overwritten; "no behavior is inferred for another state-store provider"), `:486-513` (Stack has no state-store, broker, orchestrator, or environment row), `:396` (ACA disqualified, no conforming target named).
- **Tree:** `src/Hexalith.EventStore.AppHost/DaprComponents/statestore.yaml:26` is `state.redis`; `deploy/dapr/` ships both `statestore-postgresql.yaml` and `statestore-cosmosdb.yaml` as production `statestore` (the Cosmos one with an inline `masterKey`, already AD-24-nonconforming); `ProductionDaprComponentValidationTests.cs:201-209` asserts only name and `actorStateStore`.
- **Divergence:** Stories 4.14/4.15 prove admission on PostgreSQL; an Epic 7 deployment slice ships Cosmos or Redis, changes AppHost + YAML + tests together (AD-9), routes secrets via OpenBao (AD-24), and cites the PostgreSQL packet as its AD-12 evidence. Both compliant. The AD-25 fence is explicitly not a storage fence, so the second-writer window is open on whichever provider is picked.
- **Disposition:** discuss (owner decision), then autofix. Suggested: a Stack row "Actor state store (production): `state.postgresql` v1, `oq8-postgresql-v1` profile; `state.redis` is Development/AppHost-only; any other provider is non-conforming until it passes the Story 4.5 race capture and an AD-25 evidence profile", a matching Convention row, and a topology test asserting provider *type*. If the owner will not decide now, add an `## Open Questions` section (the spine has none) listing state store, broker, target, and environment matrix.

### V-C2 — Tenant identity has no canonical source or normalized form; the tree already holds three incompatible readings
- **Raised by:** Adversarial C1, Security H2, Rubric M7.
- **Spine:** `architecture.md:468` says where tenant identity *appears*, not where it comes from, who validates it, or its canonical string form; AD-10 `:151`, AD-25 `:406`, AD-7 `:133`, AD-8 `:139` all depend on it.
- **Tree:** `Contracts/Identity/AggregateIdentity.cs:28-29` lowercases; `ClaimsTenantValidator.cs:44-45` and `AdminTenantAuthorizationFilter.cs:46-49` compare Ordinal against `eventstore:tenant`; `Admin.Server/Services/DaprProjectionQueryService.cs:77-79,205-206` compares OrdinalIgnoreCase with a `TenantAllSentinel`; SignalR groups use the raw string (`ProjectionChangedHub.cs:79-82`); Admin.Server falls back to the caller's *first* tenant claim when the tenant is omitted (`AdminBackupsController.cs:538-547`, `AdminConsistencyController.cs:148-182`) while the gateway rejects; MCP defaults to session context; Admin.UI mints its own JWT with a `tenants` array from configuration; generated REST `RestTenantSource.System` emits the literal `"system"` unnormalized.
- **Divergence:** Story 7.2 (ordinal canonical claims) vs the Story 4.7 / 1.9 lineage (lowercase read-model addresses). A principal with claim `Acme` is authorized for `Acme`, stores under `acme`, subscribes to SignalR group `Acme`, and admission partitions on whichever string the adapter passes. Under NFR2, "omit tenant, get first grant" is compliant with the text as written.
- **Disposition:** autofix. One Convention row "Tenant identity": explicit in the request, never defaulted from grants/session/config; authorized once at the first application boundary via the single claim type `eventstore:tenant`; canonical form is the `AggregateIdentity` form produced by one platform normalizer in `Contracts`; every downstream hop carries the validated value and never re-derives it; `system` is reserved. Plus an AD-10 clause that authorization compares canonical identity only.

## High

### V-H1 — The "application-layer credential" for sidecar-to-application hops is unnamed; code has three answers, and domain-service hosts have none
- **Raised by:** Security H1.
- **Spine:** AD-10 `:149-151` requires it for internal, domain-service, projection-notification, and admin-computation endpoints and says DAPR ACLs are insufficient; AD-18 and the Conventions row cover outbound only.
- **Tree:** gateway trusts the `dapr-caller-app-id` header (`DaprInternalAuthenticationHandler.cs:11-27`); Operations checks `dapr-api-token` == `APP_API_TOKEN`; the DomainService SDK maps `/process`, `/project`, `/project/v2`, `/project/rebuild/*` with no authentication at all, relying on `accesscontrol.sample.yaml`. AD-16's deny evidence runs on the gateway host only.
- **Disposition:** autofix. AD-10 clause: sidecar-to-app hops authenticate with the DAPR app-channel token via a platform `ServiceDefaults` middleware installed by `UseEventStoreDomainService()`; a domain-service host without a configured token fails readiness outside Development; `dapr-caller-app-id` and ACLs are attribution and defense-in-depth only; AD-16 evidence runs on every host kind.

### V-H2 — Deny-by-default is optional in AD-16.3, and no host has a `FallbackPolicy`; a new endpoint without `[Authorize]` is fail-open by construction
- **Raised by:** Security H5.
- **Spine:** `:249` ("any host that *introduces* a global fallback policy..."), `:253`, `:473` presume a fallback that does not exist.
- **Tree:** `grep FallbackPolicy src/` returns nothing; gateway and generator rely on per-controller `[Authorize]`.
- **Disposition:** autofix. Replace AD-16.3 with a requirement: every EventStore host sets `FallbackPolicy = RequireAuthenticatedUser` through `ServiceDefaults`; anonymous endpoints are an explicit allowlist (three probes + static `/problems` catalog); AD-16 evidence adds a structural scan for endpoints outside the allowlist lacking authorization metadata.

### V-H3 — "Attributable" admin mutations have no identity model or audit-record contract, and the delegated-write subject at the gateway is unspecified
- **Raised by:** Security H4, Adversarial H5, Security M4 (CLI/MCP/Admin.UI unbound as clients; Admin.UI is a de facto HS256 token issuer).
- **Spine:** `:151` is the only sentence; `:609` defers *audit-record deletion* for a record the spine never defines; memlog line 29 recorded "audit authenticated user/tenant/action/outcome/correlation" but it never reached AD-10; `:78`/`:577` show delegated writes without saying whose identity crosses.
- **Tree:** Admin.Server forwards the operator's bearer token (good) but also takes `DecisionActorId` from the request body and falls back to `"anonymous"` (`DaprBackupCommandService.cs:255,358,379,491`); `AdminProjectionRebuildController.cs:403-415` uses the authenticated `NameIdentifier`. Story 7.3 (audit intent) and Story 5.5 (gateway trust boundary) can pick operator-pass-through vs service-principal + on-behalf-of and both comply; outcome is every delegated write denied or two audit trails naming different actors.
- **Disposition:** autofix. AD-10 clause: one audit record per admin mutation with authenticated `sub` (never body field, never placeholder), tenant, action, target, outcome, correlation, timestamp, delegating surface; Admin Server/CLI/MCP hold no standing gateway credential and present the operator's own token unchanged; an unresolvable principal is rejected; a service-principal path needs its own AD first.

### V-H4 — AD-25 digest-key custody is not bound to AD-24, the AD-24 and AD-25 rotation clocks conflict, and the digest-key/adapter/retention catalog has no deployment-wide owner
- **Raised by:** Security H3, Adversarial H2.
- **Spine:** AD-25 `:406-408,428-440`; AD-24 `:384-393,398` disclaims only pdenc-v2 KEK custody and is silent on the admission digest key; nothing says the active version, reader set, retention catalog, or adapter registry is one deployment-wide catalog.
- **Tree:** `ConfigurationIdempotencyDigestKeyProvider` (keys in app configuration) is selectable outside Development; `IdempotencyAdmissionOptions.ActiveDigestKeyVersion` / `ReaderDigestKeyVersions` are per-host; adapter registry keyed by bare `CommandType` (no domain); each adapter picks its own `DescriptorVersion` and `RetentionTier`.
- **Divergence:** Host A active=`v2` readers=`[v1]`, Host B active=`v1`: both pass the `:437` readiness rule, and the same key gets two "single prior authorities". AD-24's revoke-after-acknowledge makes old-alias records unreachable before AD-25 promotion completes, which is the fail-open duplicate execution AD-25 exists to prevent.
- **Disposition:** autofix. AD-24: the digest key ring is a `runtime-required` OpenBao logical secret; configuration source is Development-only. AD-25: revocation is gated by AD-25 retirement (zero records/tombstones/aliases/holds on the retired version); active/reader set, retention catalog, and adapter registry are one deployment-overlay-owned catalog validated at readiness; adapters register by `(Domain, CommandType)`.

### V-H5 — Correlation identity is load-bearing (AD-17 status key) but unowned, and no AD binds telemetry naming or propagation
- **Raised by:** Adversarial H1, Security M2 (no general telemetry-hygiene rule), Rubric M5 (observability silent).
- **Spine:** `:263`, `:466` name no header, validator, generator, or propagation rule; nothing covers ActivitySource/Meter naming or a general "no secrets/PII in telemetry" convention (leak rules are AD-25-local and cursor/ETag-local).
- **Tree:** gateway middleware accepts `[A-Za-z0-9-]{≤128}` and mints ULID; Admin host middleware accepts only `Guid.TryParse` and mints a GUID (the forbidden API per `:466`); ActivitySources `Hexalith.EventStore` vs `Hexalith.EventStore.Domain.{domain}`. Story 5.4 (in review) is about to document an Admin-local "compatibility rule" the gateway may reject, breaking Story 7.3 audit-to-status joins.
- **Disposition:** autofix. Identity row: `X-Correlation-ID`, one platform validator in `Contracts`, ULID minted on rejection, echoed and forwarded unchanged on every hop, recorded as `CorrelationId` in envelopes/status/audit/logs; `traceparent` is additive. New Telemetry row: ActivitySource/Meter prefixes, and a general rule that raw keys, secrets, cursors, ETags, and tenant payloads never enter logs, traces, metrics, or problem details.

### V-H6 — Erasure has two authorities (AD-7 read-model/checkpoint erasure vs AD-23 `IErasureStateProvider` key invalidation) and key invalidation stalls AD-20 rebuilds forever
- **Raised by:** Adversarial H6, Security M3 (three meanings, no tiering).
- **Spine:** `:133`, `:374`, `:408-410`, `:610` never order or couple the two mechanisms or say what a projection does with events it can no longer decrypt.
- **Divergence:** Story 8.5/8.7 erasure = key invalidation (plaintext read models untouched); Story 1.9 lineage erasure = read-model + checkpoint deletion (events untouched). Neither is complete; no single state says whether a scope is erased. After key invalidation, an AD-20 paged rebuild hits unreadable payloads, AD-19 normalizes to `Indeterminate/NotAdvanced`, and the projection stays `Rebuilding` forever.
- **Disposition:** discuss (cross-repo with Parties), then amend AD-7/AD-23: one erasure-state authority per domain; AD-7 erasure is an ordered step before `Invalidated`; delivery/rebuild treat erased-scope events as an explicit `Erased` outcome that advances the checkpoint.

### V-H7 — `ProjectionVersion` has a source (AD-15) but no meaning or owner; companion state-key schemes are restated per project
- **Raised by:** Adversarial H4.
- **Spine:** `:235`, `:198`, `:315`, `:133` never say what a `ProjectionVersion` is (aggregate sequence? projection checkpoint? global position?), who assigns it, or whether it is comparable across projections.
- **Tree:** `IReadModelFreshness.ProjectionVersion` is a free `string?`; Tenants uses prefix + aggregate sequence; EventStore checkpoints are projection-scoped; Admin restates the command-status key format locally.
- **Disposition:** discuss, then autofix in AD-15/AD-20: platform-defined monotonic version per `(tenant, domain, aggregate, projection)` rendered by one platform formatter; companion key schemes are owned by `Server` and consumed through seams, never restated.

### V-H8 — Poison/dead-letter delivery contract is silent, and `Hexalith.EventStore.Operations` (the dead-letter host that exists) is governed by nothing
- **Raised by:** Rubric H2, Brownfield H2.
- **Spine:** zero hits for poison/dead-letter; AD-8 `:135-140` fixes at-least-once/dedup but not retry bound, DLQ topic naming/tenant scoping, DLQ consumer ownership, or checkpoint behavior on poison. Structural Seed, both diagrams, AD-9, AD-11 `:159`, and AD-19 `:309` do not know the Operations host.
- **Tree:** `src/Hexalith.EventStore.Operations` (landed `cd52ef2c`, 2026-08-28): `ContainerRepository=eventstore-operations`, own DAPR actor writing state directly, subscribes `deadletter.work.events`, replays to default app-id `works` / route `work/events` (consumer-domain defaults inside the platform), guarded only by a DAPR app-channel token; absent from AppHost, ACL/scopes, `container-projects`, brownfield docs, epics, and sprint-status. Admin.Server already invokes it. Every subscription in `deploy/` and `samples/dapr-components` already sets `enableDeadLetter` with broker-specific mechanics.
- **Disposition:** discuss. Minimal: a Deferred row parking the contract under Story 7.1 with an interim rule (same `{tenant}-{domain}` scoping for DLQ topics; no checkpoint advances past an undelivered event), plus seed row, topology node, AD-11 declared-but-unreleased container repositories (`eventstore-admin`, `eventstore-operations`), and an AD-2/AD-19 clause that replay targets are configuration, never platform defaults naming a consumer domain.

### V-H9 — AD-19's normalized result is a phantom: `ProjectionDispatchResult`, `ProjectionDispatchResultEntry`, `ProjectionCheckpointAdvanceState` exist in zero files
- **Raised by:** Brownfield H1.
- **Spine:** `:290`, `:292`, `:480` bind exact type names and `Advanced/NotAdvanced` values. Code has `ProjectionDispatchResponse`/`ProjectionDispatchOutcome` and the checkpoint decision inside `NamedProjectionDispatchCoordinator`; `ProjectionDispatchStatus` values, `MaxOutcomes` 32, and `/project/v2` do match.
- **Disposition:** discuss. Either mark the shape PLANNED with an owning story (none exists; 6.3/6.4 nearest, both backlog) or re-ratify AD-19 around the code, keeping the matrix and dropping the phantom names.

### V-H10 — AD-11 hard-binds the ASP.NET/runtime security baseline at `10.0.11`; Microsoft published the `10.0.12` security servicing release today
- **Raised by:** Technology currentness H1.
- **Spine:** `:157`, `:502`. Repository catalog and `global.json` (SDK `10.0.400`) are still consistent with each other; only the spine's fixed patch number is behind the vendor, reopening the exact class of finding the 2026-08-16 gate closed. Propagation lag: releases.json / nuget still show 10.0.11 at fetch time.
- **Disposition:** autofix wording to a band-relative sentence with a dated literal; defer the catalog move to Story 3.16 under AD-11's refresh contract once packages and the paired SDK are validated.

## Medium (8 clusters)

- **V-M1 Stack/catalog drift and "which catalog wins" ambiguity** (Tech M1, M2; Security M1; Rubric L5). FrontComposer row says `4.1.1`; committed Builds gitlink `35c3d1e5` pins `4.3.0`; worktree `a32cb422` pins `4.4.0`; committed FrontComposer submodule is already `v4.4.0-2`. Third recurrence of this row drifting. "DAPR seed `1.18.0`" conflates CI runtime `1.18.2` (CLI `1.18.0`) with `deploy/README.md`'s `daprd:1.18.0`; upstream stable is `1.18.3`. The spine never says whether "the Builds catalog" means the committed gitlink, the worktree checkout, or the published package. Autofix: drop the FrontComposer literal or date it to the gitlink; state the DAPR pins separately; define catalog authority as the committed gitlink.
- **V-M2 Structural Seed omits real projects** (Rubric M2, Brownfield M1). `AppHost`, `SignalR`, `Testing`, `Testing.Integration`, `Operations`, `Admin.Server.Host`, `samples/Sample.Tests`, `samples/dapr-components`, `samples/deploy`. Three are release-inventory packages. Autofix rows are in both reviews.
- **V-M3 Planned work written as present state** (Brownfield M2, M3, M4; L1). AD-21 FrontComposer composition and `event-store-admin` identity: Admin.UI references no FrontComposer package, string appears nowhere, Stories 7.14/7.19/7.20 backlog. AD-24: no `openbao` component, no contract file, no AppHost provisioning, Story 7.6 backlog. AD-23: "Story 8.2 is blocked until the spec records named approvals" is stale, the spec is `approved-authorized`. AD-25 "active Stories 4.9-4.15": 4.9-4.14 done, 4.15 review. Autofix: status-qualify each, or strip lifecycle adjectives the tracker owns.
- **V-M4 AD Rules carry story scheduling, dated exceptions, and evidence procedure** (Rubric M1, L2, L4). AD-22 alone is 1095 words; `:337`, `:339-366` are dated exceptions and errata; this is what forced the 08-29 reopen and undermines `status: final`. Discuss: move to memlog/story specs, leave one sentence per exception.
- **V-M5 Diagrams grant Admin.Server direct state-store reads that AD-3 forbids** (Rubric M3). `:576`, `:78` vs `:109`. Autofix: an explicit AD-3 carve-out for support-safe read-only operational reads against scoped components.
- **V-M6 Conventions table duplicates ten ADs and the copies already drift** (Rubric M6). Autofix: keep only rows adding a convention absent from any AD; reduce the rest to "See AD-n".
- **V-M7 Run hygiene** (Rubric M4, Tech L3, Brownfield L2). `companions:` lists the spine itself (`:53`); memlog stops 2026-08-16 while the spine is `updated: 2026-08-29` (SDK moved to 10.0.400 with no memlog entry; the memlog still says 10.0.302); `epics.md:15` architecture digest `623bc23e…` no longer matches committed bytes `2b96a810…` (same drift the 08-29 proposal repaired); the 2026-09-07 proposal and Epic 3 retro are not in `sources:` (they confirm the spine, and the retro's AGG-8 shows AD-11's single `ReleaseEvidenceCodec` is unratified: identifier absent, two codec generations coexist).
- **V-M8 AD-25 naming and wiring** (Adversarial M1, M2, M3). "Admission" names two layers; the fence's wire carrier across the domain-service hop is unnamed; host-to-domain ownership is a convention (`appId ?? command.Domain`) and `(Domain, ProjectionType)` uniqueness is not bound across hosts. Autofix wording in the adversarial review.

## Low (9)

Rubric L1 ("**ADD,**" editing artifact at `:606`), L2 (erratum narrative inside AD-22), L3 (FR17/FR18 bound only by AD-1's blanket range); Tech L1 (CodeCoverage 18.11.0 on nuget), L2 (CommunityToolkit Dapr 13.5.1-beta.748); Security L1 (`Guid.TryParse` still in `Admin.Server.Host` correlation middleware, route to a story), L2 (deploy docs use tags where the convention says digest); Brownfield L3 (sprint-status guarded comment lines are AD-12 evidence mechanics, not an architecture invariant), L4 (`docs/brownfield/architecture.md` claims write-once events, which the Deferred row disclaims); Adversarial L1 (Cosmos inline `masterKey`, repository not spine).

## What passed

- Lint: 0 findings (no placeholders, duplicate IDs, missing Binds/Prevents/Rule, or unpinned Stack rows).
- Coverage: every FR1-FR37 and NFR1-NFR19 bound and matching the PRD inventory; Epic 5/6/7 stories all have a home except 7.1, 5.10, 7.2; every Deferred row has a real "why it can wait"; Stack correctly treated as seed.
- Currentness confirmed: OpenBao = `secretstores.hashicorp.vault` v1, Stable since DAPR 1.16, all named metadata fields including `version_id`; ACA managed DAPR still excludes OpenBao and lists the Configuration spec as unsupported; OCI annotations as stated; Aspire 13.5.3 current (no 13.6/14); FluentUI rc.5 (no GA); Dapr SDK 1.18.5; xunit.v3 4.0.0, Shouldly 4.3.0, NSubstitute 6.2.0, MediatR 14.2.0, FluentValidation 12.1.1, OTel 1.18.0, CodeAnalysis 5.9.0, NBomber 6.6.0/6.2.1, UniqueIds 2.30.0.
- Brownfield ratified: AD-2 host seams, AD-4/17/18 client types, AD-7 cursor/read-model seams, AD-14/15 metadata and provenance types, AD-16 probes, AD-11 14-package inventory and `container-projects`, AD-25 concepts substantially in code.
- Prior closures hold: AD-11/AD-22 release-identity graph; the 2026-07-19 adversarial rereview's remaining H4 (runtime-required secret version/cache/refresh) is closed by current AD-24 `:390-392`.
- The 2026-09-07 proposal's "no architecture change" claim verified true.

## Recommended next step

Roll the findings into an **Update** run (`/bmad-architecture update`): V-C1 needs an owner decision on the production provider before text; V-C2, V-H1 to V-H5, and V-M1/M2/M3/M5/M6 are autofixable from the wording in the lens reviews; V-H6 to V-H9 need a short discussion each. Keep AD IDs stable; the likely new IDs are AD-26 (production runtime envelope) and a Tenant-identity / Telemetry convention pair.
