---
title: '5.13 Route catalog activation and topology binding'
type: 'feature'
created: '2026-10-28'
status: 'in-review'
route: 'dispatch'
review_loop_iteration: 4
story_id: '5.13'
story_key: '5-13-route-catalog-activation-and-topology-binding'
planning_baseline: 'df11852aadcee814bba094c78a60dd2a1fdb934b'
baseline_commit: 'df11852aadcee814bba094c78a60dd2a1fdb934b'
context:
  - _bmad-output/implementation-artifacts/epic-5-context.md
  - _bmad-output/project-context.md
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** The catalog is construction-only. Resolvers, metadata caches and caller-selected actor/projection targets can disagree or dispatch undeclared work.

**Approach:** Make one committed catalog generation authoritative across hosts, admission and dispatch. Validate canonical bytes, target identity and topology through prepare/ready/commit/rollback; reject disagreement before downstream work.

**Owner decision (2026-10-28):** "apply recommended" selects EventStore runtime/SDK implementation and Development multi-host qualification now. Stories 5.6/5.7 and Platform production profile, trust and hosted qualification remain open. This delivery cannot complete the full parent story or grant production readiness.

## Boundaries & Constraints

**Always:** Preserve AD-33/AD-25, canonical codec compatibility and existing authorization. Bind root/facet digests, declaration identity, profile, exact required hosts and predecessor. Use Dapr for runtime coordination/persistence. Missing metadata preserves the committed target or rejects. Production readiness fails without an activated owner-bound production instance. Proof is tests plus CI recorded here.

**Never:** Infer activation from declaration success; silently fall back; weaken ACLs; invent production trust/profile authority; change stored identities; recreate removed evidence machinery. No deployment, publication, dependency update, submodule edit or Git mutation is authorized by this spec.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected behavior | Failure |
| --- | --- | --- | --- |
| Activation | Valid candidate, all required hosts ready | Commit one complete generation | Failed prepare/ready retains prior authority |
| Invalid candidate | Bad trust/digests, duplicate/missing/ambiguous entries, override or host drift | Every required host refuses readiness | No candidate dispatch |
| Request | Declared command/query/projection and matching policy | Use captured committed target | Undeclared target/override rejects before effects, including after readiness |
| Metadata/cache | Missing entry, read failure, cached absence or stale positive | Same committed target or rejection | No alternate handler/actor/projection |
| Transition/failure | Host failure during prepare, ready or after commit | Complete-generation agreement or refusal; recover prior complete generation | No mixed serving; Dapr readback proves state |
| Production | No activated production instance/profile | Readiness fails | Development fixtures grant no production authority |

</frozen-after-approval>

## Code Map

Path shorthand below: `C=src/Hexalith.EventStore.Contracts`, `H=src/Hexalith.EventStore`, `S=src/Hexalith.EventStore.Server`, `D=src/Hexalith.EventStore.ServiceDefaults`.

- `C/Routing/RouteCatalogCodec.cs` validates retained bytes/root; declarations already distinguish handler/actor targets, aliases and projection outputs. Reuse without redefining hashes.
- `H/Queries/HandlerAwareQueryRouter.cs`, `DaprDomainQueryHandlerRegistry.cs`, `DaprDomainQueryInvoker.cs`: metadata absence selects actors; cache is domain-only; invoker resolves independently.
- `S/Queries/QueryRouter.cs`, `DomainServices/DomainServiceResolver.cs`: caller-selected actors and convention/config fallback remain open. Admission and projection coordinators have independent inventories.
- `D/Extensions.cs`: readiness lacks catalog checks. DomainService SDK supplies common host wiring. AppHost is a fixture; Platform owns production composition.

## Tasks & Acceptance

**Execution:** Implement sequentially without helper agents or starting a nested build/review workflow; the parent owns review and tracking.
- [x] `C/Routing/` — add separate lifecycle/host-attestation contracts; preserve envelope v1/v2 vectors.
- [x] `D/Routing/` and `S/Routing/` — add immutable snapshots, trust validation and Dapr-backed activation authority; fence dispatch across commit/rollback, restart and concurrent attempts.
- [x] `H/Queries/` and `S/Queries/QueryRouter.cs` — select only committed targets; bind metadata caches to generation; reject direct actor/projection bypasses.
- [x] `S/Pipeline/SubmitCommandHandler.cs`, `S/Commands/CommandRouter.cs`, `IdempotencyAdmissionCoordinator.cs`, `IdempotencyIntentAdapterRegistry.cs`, `S/DomainServices/DaprDomainServiceInvoker.cs` — validate route and admission facet before effects or independent resolution.
- [x] `S/Projections/NamedProjectionDispatchCoordinator.cs`, `ProjectionUpdateOrchestrator.cs` and their partials — bind methods/outputs and capability caches; inventory auxiliary replay/reconstruction calls without inventing declarations.
- [x] `D/Extensions.cs`, `src/Hexalith.EventStore.DomainService/EventStoreDomainServiceExtensions.cs`, `src/Hexalith.EventStore.RestApi.Generators/` — wire required-host readiness, protected Dapr generation readback and generated-host declaration checks.
- [x] `src/Hexalith.EventStore.Aspire/`, `src/Hexalith.EventStore.AppHost/Program.cs`, `deploy/dapr/` — compare inventories/fingerprints; reject unfinished prerequisite topology. Qualify a separate Development fixture without weakening deployed ACLs.
- [x] `tests/Hexalith.EventStore.QueryRouting.Tests/`, `tests/Hexalith.EventStore.Server.Tests/`, `tests/Hexalith.EventStore.Contracts.Tests/Routing/`, `tests/Hexalith.EventStore.AppHost.Tests/`, `tests/Hexalith.EventStore.Server.LiveSidecar.Tests/` — exercise every matrix row with positive controls, actual gateway fallback probes and persisted Dapr readback. Keep the strict completeness mechanism; close its refusal only after enforcement/parity passes.

**Integration requirements after review:**
- Bind every existing receiver DTO to its exact inventoried auxiliary domain and, where the request selects projections/capabilities or tenant-scoped declarations, to those committed fields. Cover reconstruction, multipart/shared rebuild and owner/applied-receipt paths; unrecognized effect-bearing shapes fail closed. Preserve existing credential and provenance checks. Enforce mandatory delegated context at the receiver as well as the gateway.
- Generated actions must supply their already-resolved partition tenant to catalog validation for Route, Claims and System modes. Reuse the same tenant semantics as action dispatch, including route/body aggregate identifiers; do not reinterpret aggregate IDs as tenants or infer tenants from grants.
- Make rollback/abort inputs bind the exact lifecycle attempt/binding and expected epoch/phase. A delayed pending abort must never roll back a committed generation or cancel a replacement with the same envelope root. Persist immutable, non-reusable attempt identity across abort, commit and rollback so delayed ready/commit cannot select replacement content. Keep idempotent retries for the same live operation.
- Retain fences only for demonstrated ambiguous remote execution. A canceled local cache/capability inspection with no remote call must release. Audit the touched projection scopes so actual remote ambiguity still retains its durable fence; keep the no-expiry/proof-only recovery rule. Failed durable release must permit explicit retry. Keep receiver leases through IResult.ExecuteAsync, including streaming and cancellation.
- Use a committed command version when an override is absent; reject only an explicit conflicting override, retaining historical normalization/validation. Validate named dispatch cache misses before any metadata refresh. Require admission consumers in the exact required host set, include lease capacity in readiness, reject duplicate keyPrefix metadata, and evict obsolete generation cache entries.
- Add executed controls for stale receiver headers after commit/rollback with zero effects, concrete invoker matching/conflicting/absent versions, independent observed target drift, auxiliary receiver domain/projection scope, generated exact-tenant Claims/System/Route parity, async IResult lifetime, canceled local-only scopes versus remote ambiguity, failed release retry, immutable attempts/delayed aborts, admission-consumer omission, capacity and duplicate-prefix topology. Re-run the existing complete-source concrete invoker cases with explicit test-only catalog isolation and every prior assertion intact. Parent verification must include the affected DomainServices test namespace as well as the previously selected Server namespaces.

**Readiness and transport requirements after the second review:**
- Independently capture the installed admission adapters, enabled policy and available key-generation metadata for each local admission consumer; capture actual named projection fingerprints/handler inventories and actual auxiliary endpoint operation policies for each receiver. Include these nonsecret facts in host identity and compare each proposed admission/capability/auxiliary binding before prepare/readiness/readback/dispatch can claim agreement. Facts must come from actual registrations/configuration/providers, never from the proposed activation. Compare policy independently of envelope generation fields using Contracts-owned canonical helpers; preserve the v1/v2 wire format and all retained vectors. Missing required facts refuse.
- Prove valid, properly trusted candidates with incompatible admission adapter/key metadata, named capability fingerprint or auxiliary operation are refused during readiness, with persisted prior authority unchanged. Positive fixture observations must describe the actual configured registrations, key-generation availability and endpoint policies. Do not weaken owner trust or existing credential authorization to satisfy the tests; keep production owner qualification open.
- Reconstruction must use its relevant committed command/domain executable scope. An unrelated wildcard query/projection must not widen an exact tenant, and no relevant scope must refuse. Receiver auxiliary checks must compare the actual canonical endpoint operation as well as domain/projection/tenant.
- Protected catalog readback and prepare/ready/commit/rollback/reconcile form a control path, not data dispatch. Permit only the exact canonical methods and verbs to traverse the standard registered HTTP pipeline before activation and while Pending, preserving SDK header ownership, workload/channel authorization and exact target configuration. All ordinary or unknown invocation methods remain fenced. Exercise bootstrap, lifecycle coordination, rollback and reconciliation through that standard pipeline, including unauthorized/unknown-method negatives.
- Generated actions fail closed when hosting validation is missing. Preserve old controller behavior only through explicitly installed test callbacks in manual fixtures; add actual generated-host positives and absent-callback zero-effect refusal.
- Apply actual-remote-boundary retention to admission as well as projection helpers. Local boundary/proof errors and cancellation after an already completed actor call release their fences; actor task ambiguity still retains. Reuse the tested atomic remote enrollment and permanent execution-close rules. Preserve all old admission/state/security assertions.
- Known-quiescent own-lease cleanup must progress through CAS contention beyond the general 32-attempt update budget, with bounded backoff and no expiry/unsafe acknowledgment. Backend failure still preserves the fence and explicit release retry; no automatic proof-based clearance is invented. Add forced-conflict and concurrent acquire/release controls.
- Add bounded, nonsecret structured refusal diagnostics at lifecycle, receiver and readiness boundaries while preserving public responses. Add the two filed verification controls: concrete catalog-backed query invocation at an HTTP sink against a conflicting resolver, and activated actor-response projection mismatch refusal with a matching control.

**Runtime boundary requirements after the third review:**
- Guard the complete HTTP transport path: disable automatic redirects beneath the catalog guard so an admitted target cannot redirect around validation. Refuse raw Dapr actor invocation URLs through this general-purpose pipeline unless an exact catalog scope can be proven; preserve the supported typed actor routing paths and their declaration checks. Add real HTTP redirect and raw-actor zero-effect negatives with a declared normal invocation positive. Keep final SDK header ownership and exact control-plane exceptions.
- Public QueryRouter construction must require catalog authority; ordinary type-based DI and direct supported construction must not silently choose an unenforced legacy path. Preserve legacy protocol tests only with explicit internal/friend test seams. Prove public/type-based construction uses authority and undeclared actor queries have no effects.
- Separate known local proxy/client construction and validation from actual remote enrollment. CommandRouter's ordinary/fenced branches and projection replay gateway factories must release on local construction failure, while an actual failed/abandoned actor task retains. Do not weaken atomic enrollment for an opaque factory that may already have started remote work. Audit the touched wrappers and add local-factory versus remote-failure controls.
- RebuildProjectionAsync must authorize the requested output within the admitted tenant and selected committed executable target, including non-callable declared outputs. A declaration for another tenant or target must not permit checkpoint reads/writes. Add different-tenant/target zero-effect negatives and the matching output positive.
- Bind the authority's effective nonsecret state component, state key and normalized selected Dapr HTTP endpoint into independent host observation. Capture them from the same effective runtime configuration used by DaprRouteCatalogStateStore, never candidate data. Check that the loaded component metadata.name matches the selected state component. Each host can have its own expected local sidecar endpoint, but a host moving to another cell/endpoint must lose identity agreement even if the loaded documents are unchanged. Add state-key/component/endpoint drift controls, including previously valid cells, and unchanged-coordinate positives. Preserve the fail-closed behavior of custom test stores with explicit independent coordinate fixtures; do not introduce fallback identity or production trust.
- Every observed executable target must join an actual compiled declaration with the same RouteId and canonical declaration digest. Empty/missing local declarations cannot attest a target merely because the candidate supplies its digest. Add missing-declaration refusal and matching registration positive.
- At the SDK command receiver, reject an explicit conflicting domain-service-version extension using the same normalization/validation as the outbound command path. An absent override uses the committed version. Cover absent/matching/conflicting values before processor effects.
- At concrete HTTP query response decoding, compare a successful response ProjectionType with the captured committed declaration. Propagate RouteCatalogUnavailableException and catalog-bound 503 refusal through the gateway as 503 rather than converting it to generic query failure/500. Cover direct concrete invocation and nested gateway behavior, preserving existing failure sanitization and non-catalog protocol seams.
- Preserve gateway ProblemDetails media type for catalog 503 (application/problem+json). Follow the existing UX-DR2 pre-pipeline 503 convention, which deliberately omits correlationId; the review's requested correlation extension does not override that rule. Add an actual HTTP refusal response control.
- Track downstream SDK read-model state RPC ambiguity under the receiver lease, including DaprReadModelBatchStateAccessor mutation/transaction calls and cancellation. A dispatcher converting a transport exception into an Indeterminate response must not erase the demonstrated remote uncertainty. Enter/restore the active receiver scope while executing both endpoint work and deferred IResult work; instrument actual state RPC boundaries after local validation. Preserve layering (Client references Contracts, not ServiceDefaults), atomic enrollment, permanent execution-close, explicit release retry, and normal/local-only release. A minimal shared boundary abstraction may live in Contracts. Add concrete state-transport fault/cancellation controls that prove the durable receiver fence survives a normal Indeterminate HTTP response and blocks replacement readiness; prove successful state work and local-only rejection drain. Do not claim certainty for arbitrary custom handlers: their remote work must use the same explicit boundary contract.
- Add the pre-verified concrete DaprProjectionLifecycleGateway.ReadSnapshotAsync transport-fault control under activated catalog authority: QueryRouter's existing lifecycle-failure handling must not release the durable fence or permit replacement readiness. Include a successful completed lifecycle call control.

**Construction and transport requirements after the fourth review:**
- Apply fail-closed public construction consistently across every public catalog-enforced routing, admission, controller, metadata/resolver and invocation entry point introduced or modified here, not only QueryRouter. Inventory CommandRouter, SubmitCommandHandler, IdempotencyAdmissionCoordinator/IdempotencyIntentAdapterRegistry, ProjectionUpdateOrchestrator, DomainServiceResolver/DaprDomainServiceInvoker, QueriesController, HandlerAwareQueryRouter, DaprDomainQueryInvoker and DaprDomainQueryHandlerRegistry. Supported public construction must require nonnull authority and cannot silently select a nullable-authority legacy path. Keep intentional protocol isolation behind explicit internal/friend test constructors or an internal test seam; expose no public switch or sentinel that disables catalog enforcement. Preserve every original protocol/security/source assertion by changing only test composition. Runtime DI must use authority-required construction, preserving all existing internal collaborators; where a runtime core overload must remain internal (for example QueryRouter's internal invoker/source-guard collaborators), distinguish its required nonnull authority from the optional legacy test seam. Never drop source/provenance enforcement merely to select a public overload. Add public/direct and type-based DI missing-authority/undeclared zero-effect controls, plus declared positives across these construction families. This supersedes the earlier non-frozen public optional manual-construction description; it does not change the frozen intent.
- Preserve unrelated ordinary HTTP behavior, including its configured automatic redirect behavior. Do not mutate an ordinary client's primary transport to disable redirects globally. Isolate guarded Dapr invocation onto a redirect-disabled transport while retaining the ordinary transport and its configuration. Preserve effective TLS/certificate/proxy/credential/connection settings, lifetime/disposal ownership and final SDK delegating-handler/header ownership; do not silently substitute an unconfigured transport or replay credential handlers twice. Use supported factory/configuration surfaces for any additional transport. If explicit owner configuration is needed for a custom transport, fail closed for guarded invocation and document that requirement; ordinary HTTP must keep its configured behavior. Add actual ordinary redirect enabled/disabled controls, declared invocation success, undeclared redirected target refusal, and configured transport/header ownership controls. Never restore the demonstrated automatic invocation redirect bypass.
- Refuse Dapr's Basic-auth invocation form (Authorization Basic with username dapr-app-id) before transport, including before activation and during Pending. This delivery supports the canonical URL/header forms already used by its SDK; unsupported forms must fail closed. Preserve ordinary unrelated Basic authentication. Cover unknown targets/methods, caller root/epoch headers, and canonical invocation positives. Parent reproduced the bypass in `/tmp/story513-basic-auth-review-probe`: HTTP 204, one sink effect, zero authority calls, caller root forwarded. The pinned Dapr 1.18.7 parser recognizes this third form; do not infer invocation solely from the two existing forms.
- In NamedProjectionDispatchCoordinator, validate the request tenant and callable projection set against committed project/v2 declarations before any auxiliary rebuild/reconcile RPC, including the direct mapped replay action path that does not resolve the metadata catalog first and any multipart upload that precedes the final invocation. Reuse the existing Outputs policy and canonical auxiliary operation inventory; retain source/owner/header/capability checks. Add exact-tenant matching controls and mismatched tenant/output zero-HTTP-effect negatives.
- Preserve bounded catalog-unavailable 503 behavior for authoritative state API failures. A Dapr state HTTP 500 or unusable authority response must not escape acquisition as a generic gateway 500. Map the actual authority backend failure at the appropriate existing boundary to the closed nonsecret refusal category, preserving caller cancellation, durable ambiguous writes, CAS rejection handling and explicit release retry. Add actual gateway HTTP 503/ProblemDetails tests using the concrete authority state transport with failed read/mutation responses and a healthy control; never expose backend payload, coordinates, proof or secrets.
- Add the pre-verified legacy projection response gap: deliver a nonempty source event stream through the catalog-enabled ProjectionUpdateOrchestrator, decode a matching declared response and prove resulting projection state/checkpoint. A foreign output must have no projection-state/checkpoint effects. Exercise the actual response acceptance branch; empty-event or pre-HTTP tests do not cover it.
- Preserve the existing supported coexistence of named v2 and legacy v1 projection delivery at the same app/version/domain. Build the captured projection method plan from the tenant's actual committed declarations: named-only authority must never fall through to an undeclared legacy project call, legacy-only authority must not attempt named dispatch, and a supported mixed plan must execute its declared paths rather than becoming ambiguous solely because it has both canonical methods. Bind requested rebuild outputs to their actual scoped method. Continue refusing genuinely ambiguous app/version/handler choices; never invent a fallback target. Add nonempty named-only, legacy-only and mixed controls that record the invoked methods and resulting projection/checkpoint effects. This also covers the parent's observation that the current legacy callers still use literal project while ResolveCommittedProjection now treats differing methods as an ambiguous target.
- Skipping named dispatch must preserve inspection of already guarded legacy output scopes. In legacy-only plans, use the existing projection-specific source/provenance inspection before legacy delivery/rebuild effects; an owned guarded or unavailable scope must refuse rather than downgrade after catalog removal. Do not call a named-delivery method merely to inspect, and do not remove the source guard altogether because Named is false. Add an established-guard negative with zero legacy HTTP/state/checkpoint effects and an unguarded matching positive. Preserve all original AD-33 checks in compatible replay and actual runtime DI.
- GeneratedRouteCatalogFilter must report its bounded nonsecret refusal diagnostic when ValidateTenant rejects declaration, target or tenant validation, in addition to acquisition errors. Preserve the false/503 result and early validation behavior; add a hosted negative with no downstream effects and a bounded diagnostic positive.
- Resolve Docker's effective context/host precedence before fixture mutation and pin the validated local Unix endpoint for every owned Docker fixture command. DOCKER_CONTEXT overrides DOCKER_HOST; merely checking the latter is insufficient. Preserve exact ID/owner-label cleanup and random loopback ports. Apply the same pinned local selection to Redis/Sentry infrastructure without modifying global contexts/configuration. Prove conflicting environment selections refuse before any container mutation, and command execution/cleanup cannot switch to another endpoint after capture. Use owned temporary/fake tooling for negative tests; do not contact a remote daemon to prove refusal.
- Bound Sentry infrastructure subprocess waits and cancellation using the existing Redis helper's owned-process pattern, including process-tree cleanup on timeout. Preserve owned temporary credentials and pinned images. Add a deterministic stalled-tool control and a normal completion control without global process/context changes.
- Preserve conservative SDK uncertainty handling. The pinned Dapr SDK already translates definite Aborted ETag mismatches to false before the remote boundary catches anything; normal false conflicts release. An unclassified DaprException followed by an existence read does not prove the original mutation is quiescent and must keep its fence. Do not weaken this behavior to satisfy R4-B01. Keep all 18 P10 no-call/completed/post-start cancellation controls and all prior KEEP constraints.

**Acceptance Criteria:**
- Given the Development fixture host set, when exhaustive parity runs, then gateway, generated APIs, admission, domain/projection dispatchers, AppHost and ACLs agree on targets and fingerprints.
- Given a multi-host drill, when failures cross lifecycle phases, then Dapr readback and concurrent requests prove complete-generation agreement or refusal; results bind host set and digests.
- Given incomplete production prerequisites, when this delivery passes, then parent-story completion and production readiness remain unclaimed.

## Implementation Notes

The following describes iteration 1. Its successful behaviors must be preserved; the iteration-1 triage identifies defects to correct before acceptance.

Implemented separate lifecycle/host/quiescence contracts without changing the envelope v1/v2 codec or its canonical vectors. Common hosting registers a required Dapr authority, immutable snapshots, readiness, protected generation/lifecycle endpoints, MVC declaration checks and Dapr invocation fencing at the primary HTTP transport boundary. The SDK keeps final routing-header ownership and its application delegating-handler order. Runtime DI supplies the authority; optional manual-construction seams retain legacy unit/protocol behavior, including the historical Domain projection default when the controller has no authority.

One shared, non-app-prefixed Dapr CAS cell retains complete committed/previous/pending generations and exact host receipts. Prepare fences new dispatch; readiness/commit require durable dispatch leases to drain. Leases have unique identities, root and epoch and never expire into authority. Actor RPC ambiguity, Dapr HTTP transport failures and canceled projection scopes retain their fences. Admission includes its directory/migration/lifecycle RPCs and later execution validation/completion/recovery operations; returned executable sessions bind root and epoch, so commit or rollback cannot revive a stale session.

`ReconcileAsync` and the protected activation-policy reconciliation endpoint clear only one exact fence after an owner-supplied `IRouteCatalogQuiescenceVerifier` validates completion proof. No verifier is registered by default. There is no automatic timeout, client-completion shortcut or administrative boolean clear. The Development test owner signs exact fence/root/epoch receipts only after its independently observed server execution completes; the hosted cancellation drill proves forged/wrong-operation receipts and eventual completion alone cannot clear the fence.

Queries, commands/admission, named capabilities and legacy projection paths validate committed declarations/targets before effects or independent resolution. Named cache misses reject undeclared app/version/domain/method before metadata refresh; a committed legacy `project` remains legacy regardless of metadata. Metadata caches include root and monotonic epoch. Generated actions compare the complete compiled Contracts declaration via the shared codec. SDK receiver filters validate generation headers and exact request targets after existing workload/channel authorization; canonical POST overrides must install that filter through the SDK policy helper.

Independent host observers derive fingerprints from actual compiled registrations, executable target inventories, loaded component/ACL/sidecar inputs and authentication profiles. Restricted Development topology validation rejects unknown app-ID/method grants, broad verbs, default-open ACLs and component scope drift. The ordinary AppHost refuses unqualified activation; a separate fixture supplies the Development qualification. The strict catalog completeness refusal remains unchanged.

The three owned live cases use real Dapr persistence in uniquely owned disposable Redis containers on random loopback ports. Cleanup verifies the exact container ID and owner label and removes only that backend. The state-only case separately qualifies concurrent empty-cell CAS through actual sidecars and the real metadata registry/cache across prepare/commit/rollback. The two hosted cases run separate gateway controller/decorator, SDK domain and generated API hosts with protected own-sidecar generation reads, declared/undeclared paths, failure/restart phases and cancellation/reconciliation. The generated API calls its own sidecar: slim correctly refuses the authenticated cross-app hop; a disposable pinned Sentry 1.18.7 with temporary mTLS credentials and one exact API-to-gateway ACL grant passes. Gateway access validators and the projection actor invoker remain controlled test doubles in these catalog host tests; they are not real actor execution or production authorization qualification.

Test isolation is explicit. DomainService direct-delegate wire tests substitute only `IDomainRouteCatalogGuard`; the existing trust-boundary and signed-source HTTP fixtures use the same guard double while preserving their original credential/source assertions. Their anonymous health/readiness assertions now expect catalog refusal (503), while liveness remains 200. The existing source/lifecycle composed-router fixture receives a canonical snapshot authority double and preserves its original lifecycle/provenance/cost assertions. The pre-existing live actor/projection persistence fixture removes only the added catalog HTTP guard and constructs its named coordinator without authority; its business/security/state assertions remain intact. Those suites prove their original protocols, not catalog enforcement. Activated actual-HTTP and explicit authority guard tests provide the separate catalog proof.

Earlier local qualification runs, before backend isolation was corrected, used the machine's Redis at `127.0.0.1:6379` with `keyPrefix:none`. They wrote fixed `admin:query-types:counter` and `admin:query-types:catalog-test` metadata keys (the fixture wrote `["get-counter-status"]` and `["undeclared-old-handler"]`, respectively) and unique catalog bootstrap/authority cells. Their pre-test contents were not captured. Those shared keys have deliberately not been guessed, reset or deleted; this earlier side effect remains disclosed. Current catalog fixtures never access that shared backend.

The integration lane prepares the existing runtime-lock Sentry pin and an isolated checksum-verified matching `daprd`, plus the already-pinned Redis image used by published compatibility. No version/pin or global installed runtime changes are made. Local prerequisites and exact image pulls are documented in the Development fixture README.

Production prerequisites remain open: Stories 5.6/5.7 topology and hosted qualification, Platform owner-bound production profile/trust/host observer/quiescence proof verification and production activation. Development receipts grant no production authority. Parent-story completion, production readiness and strict completeness certification are not claimed.

## Spec Change Log

### Iteration 1 (2026-10-28)

- **Triggers:** B02/E01 auxiliary scoping, B03 generated partition tenancy, B06 phase/binding-safe rollback, B07 immutable attempt history and B10 remote-work-specific ambiguity retention.
- **Amendment:** The following integration requirements make the non-frozen plan explicit without changing approved intent. Re-derive the implementation, including the direct corrections and verification controls logged above.
- **Known-bad state avoided:** App/method-only auxiliary authorization; route-only generated tenant lookup; root-only phase-changing aborts; reused aborted attempts; permanent fences after local-only cancellation.
- **KEEP:** Preserve v1/v2 canonical codec and retained Development catalog bytes; actual Contracts declaration hashes; strict completeness refusal; Dapr CAS bootstrap and monotonic epochs; independent host observations and exact receipts; existing authorization and SDK header ownership; no-expiry ambiguous remote fences and proof-only reconciliation; generation-bound query/projection selection; owned Redis, pinned temporary Sentry/mTLS and isolated daprd fixtures; all original wire/source/security assertions and explicit test-only isolation; existing passing Development lifecycle/readback/cache coverage. Preserve all approved production/Git/dependency/submodule boundaries and the disclosed earlier shared Redis side effect.
- **Preservation source:** `/tmp/story513-rederive-b7reuwxc/previous-implementation.json` contains the prior implementation by repository-relative path. It is a reconstruction aid, not an accepted implementation: the defects above must be corrected during re-derivation. Raw logs and snapshots stay outside the repository.


### Iteration 2 (2026-10-28)

- **Trigger:** R2-B01/B02/B04: readiness attested admission, named capabilities and auxiliary authorization that were not part of independent installed-host observations.
- **Amendment:** The non-frozen plan now requires nonsecret actual policy/key-generation, named handler/fingerprint and auxiliary endpoint-policy observations and early candidate comparison. The concrete transport/retention/generated-host and verification corrections above are re-derived with it.
- **Known-bad state avoided:** Serving=true for a trusted but locally incompatible admission/capability/policy binding; inaccessible control-plane transitions; fallback success without generated validation; local-only admission fence retention.
- **KEEP:** Preserve every iteration-1 KEEP requirement and the verified iteration-2 corrections: exact epoch/phase/binding rollback, immutable attempts, scope-specific receivers and delegated context, generated action-resolved tenancy and actual MVC early-error handling, full IResult lifetime, atomic pre-factory remote enrollment, permanent closed execution even after release failure, bounded native-capture slots, no-expiry ambiguous fences and proof-only reconciliation, generation cache eviction and readiness capacity, original wire/security assertions, owned temporary infrastructure, pins and canonical bytes. Preserve the independent external status commit and all disclosed limitations/side effects.
- **Preservation source:** `/tmp/story513-rederive2-qgef33og/previous-implementation.json` records the prior implementation. Reconstruct its working behavior and correct the accepted findings; the snapshot is not an accepted final implementation. Restore source against current HEAD `be4aba873d09736abb4caacf1fae8ad870f0b3b6`, preserving its three independent committed paths; keep the immutable planning baseline unchanged.

### Iteration 3 (2026-10-28)

- **Triggers:** R3-B03/B06 and R3-E01: public construction can omit authority, host identity omits the effective authority coordinates, and SDK state RPC ambiguity is outside the receiver lease context.
- **Amendment:** The non-frozen plan now binds public construction and effective cell coordinates, and defines a shared actual-RPC boundary across the Contracts/Client/hosting layers. Re-derive these changes together with every accepted direct boundary correction and verification gap below.
- **Known-bad state avoided:** An ordinary public router that silently bypasses the catalog; hosts serving independently valid cells with identical attested identities; an Indeterminate SDK response that clears an unfinished state mutation's fence; redirect/raw-actor bypasses and cross-tenant rebuild output authorization.
- **KEEP:** Preserve all prior change-log constraints and verified working behavior, especially independent installed admission/capability/auxiliary facts, complete declaration digests, exact canonical control recipients/verbs through the standard SDK pipeline, receiver IResult lifetime, local-only release versus durable remote ambiguity, closed nonsecret refusal diagnostics, successful contention-aware release, exact rollback/attempt identity, cache eviction and every strengthened concrete-query/actor-response test. Preserve canonical wire vectors/catalog bytes, dependency pins, index/submodules, owned Redis/Sentry/daprd infrastructure, original security/source/protocol assertions, the external status commit and the disclosed earlier shared Redis side effect. Production/CI/parent acceptance remain open.
- **Preservation source:** `/tmp/story513-rederive3-h48hleuv/previous-implementation.json` records the 158 task-owned source paths before restoration. It is a reconstruction aid, not accepted code. Restore/reconstruct against current HEAD `be4aba873d09736abb4caacf1fae8ad870f0b3b6`, preserving its three independent committed paths and the immutable planning baseline. Historical verification below applies to the respective earlier revisions; new boundary behavior needs fresh executed evidence.

### Iteration 4 (2026-10-28)

- **Triggers:** R4-B02 and R4-B04: permissive public construction remains in other catalog-enforced entry points, and the invocation redirect fix mutates unrelated ordinary HTTP transports.
- **Amendment:** The non-frozen plan now requires consistent guarded public construction and isolated invocation redirect handling with ordinary transport/configuration semantics preserved. Re-derive these changes together with the accepted Basic-auth/auxiliary-scope/backend-response/diagnostic/fixture corrections and concrete projection-response verification below.
- **Known-bad state avoided:** Undeclared effects through a public nullable-authority constructor; ordinary HTTP redirects disabled by global catalog registration; Dapr Basic invocations treated as ordinary traffic; scoped auxiliary work dispatched before receiver refusal; generic 500 on authoritative store failure; local qualification touching the wrong Docker daemon or hanging without a bounded tool wait.
- **KEEP:** Every earlier change-log constraint remains binding. Preserve all 245 passing routing controls, especially P10's six local cancellation checks and 18 activated controls, the shared Contracts actual-RPC boundary, normal/Indeterminate receiver lifetime, atomic enrollment and execution-close, independent effective authority coordinates and declaration joins, all exact rollback/attempt/cache/admission/receiver/generated/control-path behaviors, original protocol/security/source assertions, canonical wire vectors/catalog bytes, empty index, dependency pins, submodules, external status commit, owned temporary infrastructure and the earlier shared Redis disclosure. No production trust, CI or parent acceptance is granted.
- **Preservation source:** `/tmp/story513-rederive4-scjtnwvg/previous-implementation.json` records 175 task-owned source paths. Reconstruct their verified behavior and correct the accepted findings; this is not an accepted final implementation. Restore against current HEAD `be4aba873d09736abb4caacf1fae8ad870f0b3b6`, preserving its independent three status paths and the immutable planning baseline. Earlier verification below applies to those earlier source revisions.

## Review Triage Log

### Iteration 1 — all three independent layers completed

| ID | Verdict | Route | Evidence |
| --- | --- | --- | --- |
| B01 | high | patch | DomainRouteCatalogGuard query case admits a declared RequiresDelegatedContext query without CallerAuthority; DomainQueryDispatcher verifies only present authority/digest claims, so direct authenticated calls can bypass the gateway requirement. |
| B02 | high | bad_spec | Auxiliary receiver default checks app/method only. Replay, owner-proof, applied-receipt, multipart and shared-rebuild DTOs carry domains/projection identities that this branch never binds to the owner inventory. |
| B03 | medium | bad_spec | GeneratedRouteCatalogFilter reads route tenant values unconditionally. The generator resolves Claims from eventstore:tenant and System as system; exact-tenant catalog routes therefore reject valid supported generated actions. |
| B04 | medium | patch | InvokeCoreAsync compares ExtractVersion default v1 against every committed target. Ordinary commands without an explicit extension cannot use committed v2+ targets. |
| B05 | medium | patch | TryDispatchCoreAsync calls catalogRefresher on a cache miss before ValidateCommittedCapability and bypasses the guarded ResolveCatalogEntryAsync path. The actual refresh call therefore precedes committed registration validation. |
| B06 | high | bad_spec | RollbackAsync matches only RootDigest and switches between pending abort and committed rollback based on current state. An in-flight pending abort can act after commit or cancel another proposal with identical canonical bytes but different lifecycle bindings. |
| B07 | high | bad_spec | PrepareAsync retains no aborted attempt identity. After abort, the same documented unique immutable AttemptId can name a new binding; delayed ReadyAsync/CommitAsync calls then address replacement content. |
| B08 | maybe-false | defer (unverified medium; process after loop) | Default reconciliation refuses without a verifier, and the approved Development fixture maps one independently held execution to one exact fence. Whether a future production owner can correlate concurrent/restarted operations through external instrumentation is unverified; qualify exact request-to-fence correlation before enabling that production verifier. |
| B09 | medium | patch | DisposeAsync marks _disposed before release completes; a durable-store release exception makes subsequent disposal skip an unreleased non-expiring fence. Resetting disposal state on release failure permits an explicit retry. |
| B10 | medium | bad_spec | RouteCatalogLeaseReleaser retains on token cancellation even when a named capability/cache-hit scope performed only local inspection. Such cancellations leave durable fences without any outstanding remote work. Ambiguity tracking must follow actual remote calls. |
| B11 | false | reject | The restricted Development topology validator checks grant bounds, not universal connectivity. A deny-only slim fixture is deliberately a refusal control; the separate mTLS live case proves the required executable cross-app grant. No mandatory caller-edge model or falsely successful required call is identified at this validator. |
| B12 | medium | patch | AcquireAsync refuses at 4096 outstanding leases, while ReadbackAsync Serving checks pending state alone. At that explicit cap, readiness can remain healthy while all new dispatches fail. |
| B13 | medium | patch | Activation binding requires every route/auxiliary/capability recipient but omits Idempotency.ConsumerAppId. A command-only catalog can omit its admission consumer, commit without its attestation, and leave that consumer unable to acquire authority. |
| B14 | low | patch | Generation/epoch cache keys accumulate in the singleton dictionary. TTL controls reuse but never removes entries; repeated activation/rollback grows obsolete entries. Expired/obsolete-entry eviction is a direct correction. |
| E01 | high | bad_spec (group with B02) | The unrecognized auxiliary DTO branch ignores its exact owning Domain. Existing per-endpoint domain/handler execution can consequently receive work outside the catalog inventory despite matching app/method. |
| E02 | high | patch | Endpoint filters return IResult before ASP.NET executes it. Disposing the receiver lease at next(context) completion permits an async streamed/custom result to keep executing after authority drains; hold the same lease through ExecuteAsync. |
| E03 | medium | patch | The topology metadata count selects entries with both keyPrefix and none, so one none plus one conflicting duplicate passes. Require exactly one keyPrefix entry with value none. |
| E04 | medium | patch (group with B12) | Readback Serving omits the same explicit lease-cap refusal used by AcquireAsync; this is the same capacity-readiness defect as B12. |
| V01 | medium | patch | Pre-verified regression gap: receiver root/epoch checks have only matching-header positives. Add missing/wrong/stale-root and stale-epoch refusal including same-root rollback, with current positives and zero handler effects. |
| V02 | medium | patch | Pre-verified regression gap: no concrete catalog-authority command invoker tests cover explicit conflicting version extensions or a committed matching HTTP request. Add real sink positives and zero-effect negatives. |
| V03 | medium | patch | Pre-verified regression gap: current observations derive targets from the same envelope. Add independently drifted executable targets with correctly recomputed host identity/trust, proving prepare/readiness refusal and unchanged authority. |
| V04 | medium | patch | The parent reproduced both CompleteSourceConcreteInvokerBindsSerializedHttpRequestThroughHandlerAndSdkReceiver cases failing with 503 (exit 1; /tmp/story513-parent-complete-source-regression.log). This old wire/source fixture needs explicit catalog isolation while preserving every original assertion, as the other original SDK fixtures already do. |
| P01 | low | patch | Parent audit: the active DomainServiceResolver branch runs before historical version lowercasing/format validation. Preserve the existing V1-to-v1 normalization contract before selecting a committed registration. |

All findings were classified individually before grouping. B02/E01 and B12/E04 share root causes. The non-frozen implementation plan omitted supported receiver/tenant/lifecycle/cancellation integrations; these bad-spec entries trigger automatic re-derivation. Patch/defer processing is held until the loop returns. Frozen intent remains unchanged.


### Iteration 2 — all three independent layers completed

| ID | Verdict | Route | Evidence |
| --- | --- | --- | --- |
| R2-B01 | medium | bad_spec | ObservedRouteCatalogHost fingerprints and compares declarations/targets/topology/authentication only. It never reads the installed adapter/key policy; existing AdmissionKeyPolicyMustMatchBeforeKeyOrDirectoryAccess activates before demonstrating the late mismatch. A sealed but incompatible admission candidate can therefore become ready. |
| R2-B02 | medium | bad_spec | ProjectionCapabilities are binding inputs but absent from RouteCatalogHostObservation. Any nonempty fingerprint passing the declaration join can be trusted and committed before runtime capability comparison rejects it. |
| R2-B03 | high | patch | ValidateAuxiliaryTenant unions command, query and projection routes and accepts an empty set. A wildcard non-command route can therefore widen exact-tenant reconstruction; scope reconstruction to relevant command execution and reject missing scope. |
| R2-B04 | medium | bad_spec | Auxiliary Operation is merely nonempty in activation binding; it is not independently observed or compared with actual endpoint authorization, and receiver calls omit the optional operation argument. Readiness can certify a false operation-policy binding even though original endpoint authorization still protects execution. |
| R2-B05 | medium | patch | Admission capability/proof catches retain for local boundary rejection before actor access; post-success cancellation also retains. The protector validates command identity locally before key/actor access, so these failures do not demonstrate outstanding remote execution. |
| R2-B06 | high | patch | The standard primary HTTP guard acquires dispatch authority for protected lifecycle/readback methods that are not Routes/AuxiliaryOperations. Before bootstrap and during Pending, the guard prevents the very control calls needed to activate, recover or reconcile. |
| R2-B07 | medium | patch | ReleaseAsync uses the same 32 immediate CAS-attempt limit as general updates. Sustained conflicts after completed work can throw from automatic disposal and leave its durable fence. Give known-quiescent own-lease release contention-aware progress without expiry or swallowing backend failure; prove conflicts beyond 32 and concurrent drains. |
| R2-B08 | low | reject — nonroutine and structural | An exact reconciliation retry after the fence is already removed receives refusal, while the reconciled state remains unchanged and ready/commit can progress. No duplicate effect or unsafe clearance occurs. Identical success acknowledgments are not promised; retaining new persistent receipt history for this rare response-loss/no-op case is more than a direct correction. |
| R2-B09 | low | reject — nonroutine and structural | Attempt/transition tombstones increase hot-cell size linearly. Qualified Development fixtures execute a bounded lifecycle and discard owned backends; a material long-lived history limit is not reached in ordinary use of this delivery. Replacing the journal with a cold transactional protocol adds state and complexity; production capacity remains unqualified. |
| R2-B10 | medium | patch | Lifecycle/receiver/readiness catches discard the cause of 503 refusals. Provide bounded nonsecret structured diagnostics and retain the original public status/authorization behavior, so operators can distinguish configuration/trust/topology/store failures. |
| R2-E01 | high | patch | Same independently verified control-plane deadlock as R2-B06: standard factory traffic cannot bootstrap or coordinate while Pending because it is incorrectly treated as data dispatch. |
| R2-E02 | high | patch | RouteCatalogGeneratedAction.ValidateTenant returns true when the hosting callback is absent. A real generated host missing filter wiring can forward work without its compiled declaration/tenant validation. Absence must refuse; manual positive fixtures must install an explicit test callback. |
| R2-E03 | medium | patch | Same local-only admission retention defect as R2-B05; the broad ValidateCapabilityAsync/ValidateExecutionAsync catches retain a fence for an invalid boundary before any actor call. |
| R2-V01 | medium | patch | Pre-verified regression gap: concrete DaprDomainQueryInvoker tests omit catalog authority, while catalog gateway/hosted fixtures mock that invoker. Add a real HTTP sink proving committed app/method selection despite a conflicting resolver. |
| R2-V02 | medium | patch | Pre-verified regression gap: activated actor query responses always use the declared projection. Add a mismatched response-identity negative and matching control, proving refusal before payload/lifecycle/ETag handling. |

Each finding was classified before grouping. R2-B01/B02/B04 share the incomplete independent-host-observation model and require a non-frozen plan correction. R2-B05/E03 and R2-B06/E01 share their respective retention and control-plane defects. The new bad-spec group triggers re-derivation; lower patch/defer processing remains held until the loop returns. B08 from iteration 1 remains the same unverified production-owner qualification item, not an accepted runtime bypass.

### Iteration 3 — all three independent layers completed

| ID | Verdict | Route | Evidence |
| --- | --- | --- | --- |
| R3-B01 | high | patch | Parent reran the real HTTP repro: the admitted POST follows a 307 beneath RouteCatalogInvocationHandler and reaches an undeclared app/method once (204). Default SocketsHttpHandler redirect handling bypasses the guard's sole target check. |
| R3-B02 | high | patch | The same executed repro sent PUT /v1.0/actors/UndeclaredActor/id/method/Unknown through the catalog transport and reached the sink (204). The non-invoke/no-header branch forwards this raw invocation without any lease or scope check. |
| R3-B03 | medium | bad_spec | The public QueryRouter constructor is documented for DI and always leaves _catalogAuthority null; its authority-aware overload is internal. Repository registration uses a safe explicit factory, but ordinary external type-based registration/direct public construction cannot supply the guard. |
| R3-B04 | medium | patch | Ordinary and fenced CommandRouter branches construct local proxies inside catch blocks that Retain unconditionally. A synchronous construction error before an actor method starts leaves a permanent fence although no remote work exists. |
| R3-B05 | high | patch | RebuildProjectionAsync first admits one tenant's executable projection, then searches every declaration in the domain for the requested output, ignoring tenant and selected target. Another tenant/target's output therefore reaches checkpoint access. |
| R3-B06 | high | bad_spec | ObservedRouteCatalogHost hashes retained documents but not the effective StateStore/StateKey/Dapr endpoint used by DaprRouteCatalogStateStore. Component metadata.name is not compared to StateStore. Hosts can reuse the same attested identity against separately valid old/new cells. |
| R3-B07 | high | patch | DomainRouteCatalogGuard's command case checks app/method and admission only. A directly authenticated receiver request with a conflicting explicit domain-service-version reaches processing, bypassing the outbound version check. |
| R3-B08 | medium | patch | DaprDomainQueryInvoker decodes and returns a successful QueryResult without comparing ProjectionType. HandlerAwareQueryRouter checks it later, but supported direct concrete invocation exposes a response inconsistent with its captured declaration. |
| R3-B09 | medium | patch | DaprDomainQueryInvoker's broad catch converts transport RouteCatalogUnavailableException into QueryExecutionFailure; a downstream 503 also becomes generic Failure. A nested refusal after outer acquisition therefore loses the catalog 503 behavior. |
| R3-B10 | low | patch | The actual HTTP repro returns application/json for catalog ProblemDetails. Correct the media type directly. The requested correlationId is rejected: existing DaprSidecarUnavailableHandler explicitly follows UX-DR2, omitting it for pre-pipeline 503. |
| R3-E01 | high | bad_spec | DomainRouteCatalogFilter holds a lease but installs no remote scope; DaprReadModelBatchStateAccessor state mutations are untracked. DomainProjectionDispatcher catches a batch-store fault into Indeterminate and returns normally, so receiver/sender release can clear authority while the state write remains ambiguous. |
| R3-E02 | high | patch | ObserveAsync checks actual declarations against the candidate and targets against candidate routes, but never joins each actual target to an actual compiled declaration. Empty Declarations with candidate-matching Targets can attest compiled identity that was never observed. |
| R3-E03 | medium | patch | DaprProjectionRebuildWriteGateway's replay methods call CreatePublicationProxy inside RouteCatalogRemoteCall's enrolled delegate. A known local factory exception therefore retains active leases. Move known local creation out; retain opaque delegates that may have started RPC work. |
| R3-V01 | medium | patch | Pre-verified gap: concrete DaprProjectionLifecycleGateway tests run without authority, while catalog cases mock lifecycle calls. Add an activated concrete lifecycle transport failure proving a retained durable fence despite QueryRouter swallowing the lifecycle error, and refusal of replacement readiness. |
| P10 | medium | patch — completed before next review | Parent source audit found already-canceled tokens could enter newly wrapped Client bulk/save/delete/conditional/reconciliation/create-conflict RPC boundaries and retain fences without remote work. Six pre-enrollment checks now release local cancellations; 18 executed activated-lease controls prove zero calls/effects, completed-work release and post-start cancellation retention. |

Each finding was classified individually before grouping. R3-B04/E03 share local proxy creation inside an ambiguity boundary; the other findings remain separate. R3-B03/B06/E01 require new construction/observation/boundary design outside the frozen intent and trigger re-derivation. Lower patch processing is held while that loop runs. Iteration-1 B08 remains the previously logged unverified production-owner qualification item, to defer once after loopbacks finish.

### Iteration 4 — all three independent layers completed

| ID | Verdict | Route | Evidence |
| --- | --- | --- | --- |
| R4-B01 | false | reject | The pinned Dapr 1.19.0 MakeSaveByteStateCallAsync/MakeSaveStateCallAsync catch RpcException Aborted and return false internally; the outer remote boundary therefore does not retain for a definite ETag rejection. The later generic-DaprException existence fallback cannot prove the original RPC quiescent, so retaining that uncertainty is required. Existing completed-false P10 controls drain normally. Source: pinned SDK DaprClientGrpc.cs, lines 883–899 and 1148–1164. |
| R4-B02 | medium | bad_spec | CommandRouter's public two-argument/default primary construction leaves catalogAuthority null and can invoke an actor without resolution. Default AddEventStoreServer registration is guarded, but supported public manual construction remains permissive. The same nullable-authority design appears in other public entry points; the plan must require consistent construction instead of fixing one class per pass. |
| R4-B03 | high | patch | Direct InvokeMappedOnlyStreamedRebuildAsync checks header/identity equality then reaches auxiliary InvokeAsync without ResolveCatalogEntryAsync. Auxiliary checks domain/capability but omit tenant/callable Outputs, so a matching capability from another tenant's declaration can reach HTTP before receiver rejection. |
| R4-B04 | medium | bad_spec | RouteCatalogStateHttpClientFilter wraps every factory client, and CreateTransport unconditionally sets its existing native primary handler AllowAutoRedirect=false. The ordinary no-invocation branch then uses that mutated transport, changing unrelated configured HTTP behavior. Separation of the effective transports/configuration is a design correction. |
| R4-B05 | false | reject — carried B11 | carried: the same restricted grant-bounds validator deliberately permits deny-only slim refusal controls; the separate mTLS drill proves executable required connectivity. This code does not promise a universal caller-edge model, and no accepted required call is falsely reported successful. |
| R4-B06 | low | reject — carried R2-B09 | carried: bounded ephemeral Development qualification does not encounter material long-lived attempt/transition journal growth. Cold transactional history is a structural fix for a rare future workload, not a direct correction. Production capacity remains unqualified. |
| R4-B07 | low | reject — carried R2-B08 | carried: an identical reconciliation retry after removal refuses while the exact state/effect remains safely reconciled. Identical success acknowledgment is not promised; adding persistent response receipts is more than a direct correction for this rare response-loss case. |
| R4-B08 | low | reject — nonroutine and structural | The qualified Development owner obtains exact fence IDs through strong authority-cell readback and its held-operation instrumentation; the executed reconciliation drill proves that route. Omitting a separate human-friendly inventory endpoint requires raw-cell inspection for rare local recovery, but does not prevent recovery in this delivery. A new recovery API is structural; production-owner correlation remains the separately deferred B08 qualification. |
| R4-B09 | medium | patch | GeneratedRouteCatalogFilter.ValidateTenant catches all declaration/target/tenant failures and returns false; the action then returns its early 503 without the acquisition catch or any refusal diagnostic. Operators cannot distinguish these real generated-host disagreements; retain refusal and emit the existing bounded diagnostic. |
| R4-B10 | high | patch | Redis StartAsync manually prefers DOCKER_HOST while subsequent Docker commands inherit DOCKER_CONTEXT. Docker's official CLI reference states the context overrides the host, so conflicting selections can pass the Unix check and mutate another daemon. Resolve and pin the effective validated local endpoint for fixture operations. |
| R4-B11 | medium | patch | Sentry RunAsync waits for infrastructure subprocesses with no cancellation, unlike the Redis helper. A stalled tool can hang local qualification and consume the CI job's outer 90-minute timeout. Bound the known started process and clean up that owned process on timeout. |
| R4-E01 | high | patch | Parent reran the Basic-auth probe: HTTP 204, sinkEffects=1, authorityCalls=0 and caller root forwarded. Pinned Dapr 1.18.7 findTargetIDAndMethod recognizes Basic dapr-app-id credentials, which the transport guard's ordinary branch misses. Refuse that unsupported invocation form before effects. |
| R4-V01 | medium | patch | Pre-verified regression gap: catalog-enabled legacy ReadProjectResponseAsync has no executed nonempty-event matching-output response/state/checkpoint test. Existing orchestrator tests omit authority; catalog cases stop before decoding. Add matching and foreign output cases at the concrete delivery boundary. |
| R4-V02 | medium | patch | Concrete DaprRouteCatalogStateStore.EnsureSuccessStatusCode emits HttpRequestException for a state API 500; pre-dispatch acquisition has no catalog translation, the sidecar handler requires connection-refusal/reset evidence, and GlobalExceptionHandler consequently returns generic 500. Preserve bounded catalog-unavailable 503 for this authoritative outage. |

Every finding was classified individually before grouping. The permissive public-construction family is one R4-B02 root; R4-B04 is a separate transport-coupling root. The remaining accepted findings have distinct causes and remain separate. Those two bad-spec entries trigger re-derivation; lower patch processing is held during the loop. No new deferred ledger entry has been appended yet; original B08 remains pending once loopbacks finish.

### Iteration 5 — redirect-fence review

| ID | Verdict | Route | Evidence |
| --- | --- | --- | --- |
| R5-B01 | medium | patch | `ClearRedirectAuthentication` removes `Authorization` on every followed redirect. Platform redirects keep that header when the host and scheme stay the same, so same-host ordinary clients lose credentials. |
| R5-B02 | high | patch | A redirect whose path matches `/v1.0/invoke/` is fenced even when the host changes, so an absolute redirect to another host can receive catalog root/epoch headers and the replayed body as a declared invocation. |
| R5-B03 | maybe-false | defer (unverified high) | Invoke matching is ordinal and actor matching is ignore-case. Whether daprd accepts `/V1.0/invoke/` and would then run beneath the guard is unverified. |
| R5-B04 | medium | patch | When the redirect-disabled ordinary copy cannot be created, ordinary redirecting requests throw `RouteCatalogUnavailableException` instead of staying on the owner transport. Full-body capture is required to replay a preserved method and is not itself the defect. |
| R5-B05 | false | reject | Auxiliary assignment can overwrite a route operation value, but ACL qualification never reads that value. The method key is unchanged, so the grant set is not replaced. |
| R5-B06 | false | reject | Lifecycle catches return an empty 503 on the control path. Gateway `application/problem+json` remains the exception-handler surface named by the spec; these are different responses by that split. |
| R5-B07 | low | reject | The catalog status note can lag runtime fail-closed dispatch. Everyday callers use the spec and tests, and rewriting that historical note is more than a direct correction. |
| R5-B08 | medium | defer | Exact message identity versus cross-tenant correlation `409` comes from the separate status commit after the planning baseline, not from catalog activation. |
| R5-B09 | false | reject | Runtime DI constructs `QueryRouter` through the internal core and passes `IProjectionSourceGuardDelivery`. The public actor-factory constructor has no source-guard argument to drop. |
| R5-B10 | false | reject | Observation replaces callback coordinates with the state store's own coordinates, then hashes those into host identity. A store that moves changes the attested identity; candidate coordinates are not the source. |
| R5-B11 | false | reject | `ReleaseAsync` retries compare-and-exchange without a 32-attempt ceiling. That is the required progress for known-quiescent cleanup; a false compare-and-exchange is contention, not a backend failure that must throw. |
| R5-B12 | low | reject | The historical probe paragraph remains, and the later closure section already records the passing crossover tests. The proposed fix is an edit to this spec. |
| R5-B13 | low | reject | Releasing a loopback reservation before the sidecar binds can theoretically lose the port. That window is not an everyday failure, and holding or retrying the bind is more than a direct correction. |
| R5-B14 | false | reject | `RouteCatalogDevelopmentSentry.Image` and `runtime-lock.json` `images.sentry.reference` are the same `sha256:b9fc166085289661f516150dc1c47ac3495b9e6d1e666079cb71cd2374ca7256`. |
| R5-B15 | false | reject | `IdempotencyIntentAdapterRegistry.ResolveAsync` is implemented and acquires the catalog lease. The interface default is unused by that registration, and `GetResult` runs on a thread-pool thread without a synchronization context. |
| R5-E01 | false | reject — carried R4-B05 | carried: absent policies still take the deny-only path that the restricted validator accepts as a refusal control. The mTLS drill remains the executable-grant proof. |
| R5-E02 | false | reject | Same unused operation-value overwrite as R5-B05. No ACL decision changes. |
| R5-E03 | maybe-false | defer (unverified medium) | `AbsolutePath` on a relative URI throws `InvalidOperationException` before a catalog refusal. `HttpClient` absolutizes against `BaseAddress` before the primary handler, so this path may be unreachable. |
| R5-E04 | false | reject | `ResolveCatalogEntryAsync` throws for an uncommitted named registration before `RefreshAsync`. A failed refresh returns null and does not call the receiver. |
| R5-E05 | false | reject | Dispatch matches the request fingerprint. Another contract version is selected only when it carries that same fingerprint, so a different capability is not admitted. |
| R5-E06 | false | reject | The receiver capability check has the same fingerprint requirement as R5-E05. |
| R5-E07 | medium | patch | `RouteCatalogProjectionPolicy.Outputs` dereferences `Execution!`. Schema v1 permits a null execution, so a committed v1 projection throws `NullReferenceException` instead of a catalog refusal. |
| R5-E08 | medium | patch | `QueryRouter.RouteQueryAsync` dereferences `Execution!` for the same v1 declaration and leaves the catalog 503 boundary. |
| R5-E09 | medium | patch | `HandlerAwareQueryRouter` has the same null-execution dereference. |
| R5-E10 | medium | patch | `DaprDomainQueryInvoker` has the same null-execution dereference. |
| R5-E11 | medium | patch | `DaprDomainQueryHandlerRegistry` has the same null-execution dereference. |
| R5-E12 | false | reject | With a catalog lease, `DaprDomainServiceInvoker` builds the registration from the committed target and does not invoke a resolver result after that lease ends. |
| R5-E13 | low | reject — carried R4-B06 | carried: attempt history growth stays outside ordinary Development qualification. A cold history protocol is structural, and production capacity remains unqualified. |
| R5-E14 | false | reject | The startup check requires the receiver guard for canonical routes whose methods are unspecified or include POST, which is the POST-override rule. A GET-only canonical route is not shown. |
| R5-E15 | false | reject | Same pre-refresh committed check as R5-E04. |
| R5-E16 | false | reject — carried R4-B05 | carried: the same absent-policy success is the deny-only control already rejected. |
| R5-E17 | medium | patch | Same `QueryRouter` null-execution `NullReferenceException` as R5-E08, which a gateway can surface as 500. |
| R5-V01 | medium | patch | Pre-verified: catalog-backed `Submit` and `SubmitBoundOwnerAuthorityAsync` never omit `ProjectionType` on a declared query whose projection differs from its domain. Restoring `request.Domain` would refuse `list-tenants`, `get-user-tenants`, and `ResolveTenantOwnerAuthority` while current tests stay green. |

Each finding was classified before grouping. R5-E07 through R5-E11 and R5-E17 are one null-execution dereference. R5-B01, R5-B02, R5-B04, and R5-V01 have separate causes. No intent gap or bad spec remains, so these patches proceed. R5-B03, R5-E03, and R5-B08 are deferred.

## Design Notes

Cross-package APIs require a dispatch workflow. Independent snapshot swaps cannot establish distributed activation safety. Historical 9.1 record tooling was intentionally removed; its absence is not a development blocker.

## Verification

- Build affected test projects individually with `dotnet build <project> -c Debug -p:UseHexalithProjectReferences=true -m:1`; run built xUnit DLLs using `-class` filters.
- Run package-mode Release validation and the owned live-sidecar drill after focused checks; record exact commands/counts, source SHA and CI link, separating blockers from passing evidence.
- Run `git diff --check` and `bash scripts/check-committed-files.sh`.


### Executed verification (2026-10-28)

Base source SHA remains `df11852aadcee814bba094c78a60dd2a1fdb934b`; implementation is an uncommitted working-tree change. No Git, submodule or dependency-version mutation was performed. CI link: unavailable because no CI execution exists for this working tree. Parent review and CI are still outstanding; this note does not claim final acceptance or parent-story completion.

Each affected project was built separately in Release/package mode with zero warnings/errors using these exact commands:

```sh
dotnet build tests/Hexalith.EventStore.Server.Tests/Hexalith.EventStore.Server.Tests.csproj -c Release -p:UseHexalithProjectReferences=false -p:UseNuGetDeps=true
dotnet build tests/Hexalith.EventStore.DomainService.Tests/Hexalith.EventStore.DomainService.Tests.csproj -c Release -p:UseHexalithProjectReferences=false -p:UseNuGetDeps=true
dotnet build tests/Hexalith.EventStore.Server.LiveSidecar.Tests/Hexalith.EventStore.Server.LiveSidecar.Tests.csproj -c Release -p:UseHexalithProjectReferences=false -p:UseNuGetDeps=true
dotnet build tests/Hexalith.EventStore.Contracts.Tests/Hexalith.EventStore.Contracts.Tests.csproj -c Release -p:UseHexalithProjectReferences=false -p:UseNuGetDeps=true
dotnet build tests/Hexalith.EventStore.QueryRouting.Tests/Hexalith.EventStore.QueryRouting.Tests.csproj -c Release -p:UseHexalithProjectReferences=false -p:UseNuGetDeps=true
dotnet build tests/Hexalith.EventStore.ServiceDefaults.Tests/Hexalith.EventStore.ServiceDefaults.Tests.csproj -c Release -p:UseHexalithProjectReferences=false -p:UseNuGetDeps=true
dotnet build tests/Hexalith.EventStore.AppHost.Tests/Hexalith.EventStore.AppHost.Tests.csproj -c Release -p:UseHexalithProjectReferences=false -p:UseNuGetDeps=true
dotnet build tests/Hexalith.EventStore.RestApi.Generators.Tests/Hexalith.EventStore.RestApi.Generators.Tests.csproj -c Release -p:UseHexalithProjectReferences=false -p:UseNuGetDeps=true
dotnet build tests/Hexalith.EventStore.Client.Tests/Hexalith.EventStore.Client.Tests.csproj -c Release -p:UseHexalithProjectReferences=false -p:UseNuGetDeps=true
```

Build logs are `/tmp/route-server-final-build.log`, `/tmp/route-domain-final-build.log`, `/tmp/route-live-final-release-build.log`, `/tmp/route-contracts-final-build.log`, `/tmp/route-query-final-build.log`, `/tmp/route-defaults-final-build.log`, `/tmp/route-apphost-final-build.log`, `/tmp/route-generator-final-build.log` and `/tmp/route-client-final-build.log`.

| Executed scope | Result | Log |
| --- | --- | --- |
| Server Commands/Queries/Pipeline/Projections/Routing/QueriesController and existing admission-actor class | 1,791 total; 1,774 passed; 17 existing skips; 0 failures | `/tmp/route-server-broad-final-tests.log` |
| Full DomainService suite, including unchanged credential/source/wire assertions | 1,277 total; 1,276 passed; 1 original skip; 0 failures | `/tmp/route-domain-final-tests.log` |
| Owned state/CAS/cache and slim/mTLS actual-host catalog drill, isolated Redis backends | 3 passed; 0 skipped/failed | `/tmp/route-final-live-tests.log` |
| Existing live command class | 2 passed; 0 skipped/failed | `/tmp/route-existing-live-final-command-tests.log` |
| Existing live named projection detail/index/checkpoint/retry control | 1 passed; 0 skipped/failed | `/tmp/route-existing-live-final-projection-tests.log` |
| Contracts routing, including v1/v2 canonical vectors | 142 passed | `/tmp/route-contracts-final-tests.log` |
| Full QueryRouting suite | 15 passed | `/tmp/route-query-final-tests.log` |
| Full ServiceDefaults suite | 16 passed | `/tmp/route-defaults-final-tests.log` |
| AppHost semantic topology and unqualified activation | 8 passed | `/tmp/route-apphost-final-tests.log` |
| Full REST generator suite | 124 passed | `/tmp/route-generator-final-tests.log` |
| Existing SDK Dapr handler/header-owner registration suites | 20 passed | `/tmp/route-client-routing-final-tests.log` |
| Real HTTP sink/order controls (also included in Server scope above) | 4 passed | `/tmp/route-http-transport-tests.log` |
| Existing broker input regression suite | 25 passed | `/tmp/route-ci-input-tests.log` |

Exact test commands:

```sh
dotnet tests/Hexalith.EventStore.Server.Tests/bin/Release/net10.0/Hexalith.EventStore.Server.Tests.dll -class '*Commands.*' -class '*Queries.*' -class '*Pipeline.*' -class '*Projections.*' -class '*Routing.*' -class '*Controllers.QueriesControllerTests' -class '*Actors.IdempotencyAdmissionActorTests'
dotnet tests/Hexalith.EventStore.DomainService.Tests/bin/Release/net10.0/Hexalith.EventStore.DomainService.Tests.dll
EVENTSTORE_ROUTE_CATALOG_DAPRD=/tmp/route-catalog-ci-preparation/route-catalog-tools/daprd dotnet tests/Hexalith.EventStore.Server.LiveSidecar.Tests/bin/Release/net10.0/Hexalith.EventStore.Server.LiveSidecar.Tests.dll -class '*RouteCatalogDevelopmentLiveTests' -class '*RouteCatalogHostedLiveTests'
dotnet tests/Hexalith.EventStore.Server.LiveSidecar.Tests/bin/Release/net10.0/Hexalith.EventStore.Server.LiveSidecar.Tests.dll -class '*Commands.CommandRoutingIntegrationTests'
dotnet tests/Hexalith.EventStore.Server.LiveSidecar.Tests/bin/Release/net10.0/Hexalith.EventStore.Server.LiveSidecar.Tests.dll -method '*NormalDelivery_PersistsIndependentDetailIndexCheckpointsAndConvergedRetryLedger'
dotnet tests/Hexalith.EventStore.Contracts.Tests/bin/Release/net10.0/Hexalith.EventStore.Contracts.Tests.dll -class '*Routing.*'
dotnet tests/Hexalith.EventStore.QueryRouting.Tests/bin/Release/net10.0/Hexalith.EventStore.QueryRouting.Tests.dll
dotnet tests/Hexalith.EventStore.ServiceDefaults.Tests/bin/Release/net10.0/Hexalith.EventStore.ServiceDefaults.Tests.dll
dotnet tests/Hexalith.EventStore.AppHost.Tests/bin/Release/net10.0/Hexalith.EventStore.AppHost.Tests.dll -class '*RouteCatalogTopologyTests'
dotnet tests/Hexalith.EventStore.RestApi.Generators.Tests/bin/Release/net10.0/Hexalith.EventStore.RestApi.Generators.Tests.dll
dotnet tests/Hexalith.EventStore.Client.Tests/bin/Release/net10.0/Hexalith.EventStore.Client.Tests.dll -class '*Handlers.DaprServiceInvocationHandlerTests' -class '*Registration.DaprServiceInvocationRegistrationTests'
dotnet tests/Hexalith.EventStore.Server.Tests/bin/Release/net10.0/Hexalith.EventStore.Server.Tests.dll -class '*CatalogHttpTransportTests'
python3 -m unittest discover -s tools/tests -p 'test_dapr_broker_qualification.py'
actionlint .github/workflows/integration.yml
git diff --check
bash scripts/check-committed-files.sh
```

The exact integration workflow preparation step was also executed locally with owned `/tmp/route-catalog-ci-preparation` scratch and `/tmp/route-catalog-ci-preparation.env` in place of the GitHub runner paths. Both existing image pulls succeeded; the isolated binary matched runtime-lock SHA-256 `e988a6f53abec017fc9ebcb4f89de9bed11bce005eb49bd8e85e8d3d6adbf488`. Its log is `/tmp/route-ci-preparation-check.log`. Actionlint, `git diff --check` and the committed-file guard pass (`/tmp/route-integration-workflow-check.log`, `/tmp/route-diff-check.log`, `/tmp/route-committed-files-check.log`; 9,319 tracked files checked). No catalog-owned Redis container remained after the live run.

Debug/source-focused checks had passed Contracts routing (142), QueryRouting (15), ServiceDefaults (16), generator (124), AppHost topology (7 before the independent caller-positive addition), SDK direct-delegate fixtures (99), and the original Server manual-constructor command/query subset (168). The pre-existing live eventful command source-mode run was blocked by `FileNotFoundException` for `Hexalith.Commons.UniqueIds, Version=3.128.4.0` while its output/deps carried assembly version `1.0.0.0`; exact exception evidence is `/tmp/route-existing-live-assembly-diagnostic.log`. Fresh Release/package restore/build and the same command class passed, as recorded above. No unrelated dependency or old business/security assertion was changed to hide that source-graph assembly mismatch.

### Parent implementation and matrix audit (2026-10-28)

All eight execution tasks are complete for the approved EventStore/SDK and separate Development fixture delivery. Parent-story acceptance still requires the production prerequisites listed above. The parent read the complete baseline diff including untracked files and all subsequent implementation deltas; no real index staging was used because the approved scope forbids Git mutation. The audited pre-review diff is `/tmp/story513-parent-audit-is71bo0i/final-pre-review.diff` (SHA-256 `857339b7f3882c1d0f466de9ea3451e058d661a4f9adc69905732320cd9c9a86`).

The parent independently reran all new Server routing classes: **60 passed, 0 skipped/failed/not-run**. Command: `dotnet tests/Hexalith.EventStore.Server.Tests/bin/Release/net10.0/Hexalith.EventStore.Server.Tests.dll -class '*Routing.*' -xml /tmp/story513-parent-routing-tests.xml`. Output: `/tmp/story513-parent-routing-tests.log`; the XML records every passing theory case. The existing live output records all three catalog cases executing with zero skips/failures.

| Frozen matrix row | Executed passing coverage |
| --- | --- |
| Activation | `EveryExactHostMustAttestTheSameCompleteGeneration`, `PrepareFencesNewDispatchAndCannotCommitUntilDurableLeasesDrain`; three-sidecar CAS/lifecycle and both hosted live cases |
| Invalid candidate | Five `InvalidCandidateCannotReplacePriorAuthority` cases, exact-host attestation, full generated declaration drift and semantic AppHost topology controls |
| Request | Command/admission positive and negative controls; actual gateway fallback/actor/projection override refusal; named/legacy projection guards; final HTTP sink target/header ownership; actual hosted query/projection guards |
| Metadata/cache | Four `ActualGatewayDecoratorPreservesCommittedHandlerAndActorTargetsAcrossMetadataStates` cases; named stale-capability and cache-miss controls; live metadata-registry transitions |
| Transition/failure | Prepare/ready failure, concurrent attempts, restart/rollback and monotonic epoch controls; command/admission/actor ambiguity fencing; all hosted cancellation/quiescence/reconciliation and protected Dapr readback cases |
| Production | `DeclarationsAloneCannotActivateOrServe`, `DevelopmentFixtureCannotGrantProductionReadiness`, `GeneralAppHostCannotActivateUnqualifiedPrerequisiteTopology` |

No covering test is skipped or disabled. The 18 old suite skips recorded above provide none of this matrix coverage. Package-mode checks pass; the separate Debug/source live dependency mismatch and absent working-tree CI remain limitations, not passing evidence.

### Iteration 2 re-derivation and final verification (2026-10-28)

The amended EventStore/SDK implementation and separate Development qualification are implemented and parent-audited. The frozen intent is unchanged. Production prerequisites, parent-story acceptance and a working-tree CI run remain open; these results grant no production readiness or strict completeness certification.

The immutable planning/implementation baseline remains `df11852aadcee814bba094c78a60dd2a1fdb934b`. Current HEAD is `be4aba873d09736abb4caacf1fae8ad870f0b3b6`: an independently observed external fast-forward changed only `docs/reference/command-api.md`, `Controllers/CommandStatusController.cs` and `Commands/CommandStatusControllerTests.cs`. Those independent changes were preserved. This implementation remains a working-tree change; the implementing and auditing agents performed no commit/ref, dependency-version or submodule mutation. CI link: unavailable because this working tree has not run in CI.

The re-derived implementation corrects the accepted findings while preserving the v1/v2 canonical bytes, existing authorization and SDK header ownership:

- Every supported receiver DTO binds its exact auxiliary domain and applicable tenant, projection and capability scope. Reconstruction, multipart/shared rebuild, owner proof and applied receipts are covered; unknown effect-bearing shapes refuse. Required delegated query context is enforced at the receiver before dispatch.
- Generated actions supply their existing resolved partition tenant for Route, Claims and System modes. Catalog validation follows the existing route/body identity checks and precedes gateway dispatch. Actual MVC tests verify that a missing validation handshake changes the executed result to 503, while legitimate early 400/403/500 results remain intact.
- Abort/rollback requests bind immutable attempt identity, full lifecycle digest, exact epoch and phase. Durable attempt history prevents reuse after terminal transitions; exact response-loss retries cannot cancel replacement content with the same envelope root.
- Actual remote boundaries enroll their complete captured lease set atomically before invoking a factory. First disposal permanently closes execution, including stale captured asynchronous contexts. Ambiguous or abandoned remote tasks retain non-expiring fences; eventual completion alone cannot clear them. Local-only cancellation releases, and a failed durable release permits disposal retry without reopening execution. Optional native capture keeps its existing bounded worker slot until the actor actually completes.
- Receivers hold authority through asynchronous `IResult.ExecuteAsync`, including streaming and cancellation. Current-header positives and zero-effect stale-root/stale-epoch negatives execute after commit and same-root rollback.
- Absent command overrides use the committed version; explicit overrides retain historical normalization/validation. Named cache misses validate committed registration before refresh. Admission consumers belong to the exact required host set, lease exhaustion fails readiness, duplicate key-prefix metadata refuses, and obsolete query-cache generations are evicted.

All nine affected projects were built separately in Release/package mode with zero warnings/errors. The commands were `dotnet build tests/Hexalith.EventStore.<Name>.Tests/Hexalith.EventStore.<Name>.Tests.csproj -c Release -p:UseHexalithProjectReferences=false -p:UseNuGetDeps=true -m:1`, with `<Name>` equal to `Server`, `DomainService`, `Server.LiveSidecar`, `Contracts`, `QueryRouting`, `ServiceDefaults`, `AppHost`, `RestApi.Generators` and `Client`. Final affected Server and LiveSidecar outputs were rebuilt after their last changes. Logs use `/tmp/story513-rederive-<name>-release-build.log`, with lowercase names and dots replaced by hyphens.

| Executed scope | Result | Log |
| --- | --- | --- |
| Parent final Server Commands/Queries/Pipeline/Projections/Routing/DomainServices/QueriesController/admission-actor lane, exact final source | 2,093 total; 2,076 passed; 17 existing skips; 0 failures/errors/not-run | `/tmp/story513-parent-final-server.log`; XML `/tmp/story513-parent-final-server.xml` |
| Final Routing lane, including lifecycle races, receiver current/stale headers, actual MVC and failed-release retry | 129 passed; 0 skipped/failed | `/tmp/story513-rederive-final-routing-tests.log`; XML `/tmp/story513-rederive-final-routing.xml`; independently repeated by parent |
| Full DomainService suite with original credential/source/wire assertions | 1,277 total; 1,276 passed; 1 original skip; 0 failures | `/tmp/story513-rederive-domainservice-release-tests.log` |
| Fresh final owned state/CAS/cache and slim/mTLS hosted catalog drill | 3 passed; 0 skipped/failed | `/tmp/story513-rederive-owned-live.log` |
| Existing live command and named projection persistence controls | 2 + 1 passed; 0 skipped/failed | `/tmp/story513-rederive-existing-live-command.log`, `/tmp/story513-rederive-existing-live-projection.log` |
| Contracts routing / full QueryRouting / full ServiceDefaults | 142 / 15 / 16 passed | `/tmp/story513-rederive-contracts-release-tests.log`, `/tmp/story513-rederive-queryrouting-release-tests.log`, `/tmp/story513-rederive-servicedefaults-release-tests.log` |
| AppHost topology, including duplicate prefix and unqualified production prerequisites | 9 passed | `/tmp/story513-rederive-apphost-release-tests.log` |
| Full REST generator suite, including actual generated Route/Claims/System tenant and body-ID controls | 133 passed | `/tmp/story513-rederive-restapi-generators-release-tests.log` |
| Existing SDK Dapr handler and registration suites | 20 passed | `/tmp/story513-rederive-client-release-tests.log` |
| Existing broker-input regression suite | 25 passed | `/tmp/story513-rederive-ci-input-tests.log` |

The final parent Server command was:

```sh
dotnet tests/Hexalith.EventStore.Server.Tests/bin/Release/net10.0/Hexalith.EventStore.Server.Tests.dll -class '*Commands.*' -class '*Queries.*' -class '*Pipeline.*' -class '*Projections.*' -class '*Routing.*' -class '*DomainServices.*' -class '*Controllers.QueriesControllerTests' -class '*Actors.IdempotencyAdmissionActorTests' -result-xml /tmp/story513-parent-final-server.xml > /tmp/story513-parent-final-server.log 2>&1
```

The other final test commands were:

```sh
dotnet tests/Hexalith.EventStore.Server.Tests/bin/Release/net10.0/Hexalith.EventStore.Server.Tests.dll -class '*Routing.*' -xml /tmp/story513-rederive-final-routing.xml
dotnet tests/Hexalith.EventStore.DomainService.Tests/bin/Release/net10.0/Hexalith.EventStore.DomainService.Tests.dll
EVENTSTORE_ROUTE_CATALOG_DAPRD=/tmp/route-catalog-ci-preparation/route-catalog-tools/daprd dotnet tests/Hexalith.EventStore.Server.LiveSidecar.Tests/bin/Release/net10.0/Hexalith.EventStore.Server.LiveSidecar.Tests.dll -class '*RouteCatalogDevelopmentLiveTests' -class '*RouteCatalogHostedLiveTests'
dotnet tests/Hexalith.EventStore.Server.LiveSidecar.Tests/bin/Release/net10.0/Hexalith.EventStore.Server.LiveSidecar.Tests.dll -class '*Commands.CommandRoutingIntegrationTests'
dotnet tests/Hexalith.EventStore.Server.LiveSidecar.Tests/bin/Release/net10.0/Hexalith.EventStore.Server.LiveSidecar.Tests.dll -method '*NormalDelivery_PersistsIndependentDetailIndexCheckpointsAndConvergedRetryLedger'
dotnet tests/Hexalith.EventStore.Contracts.Tests/bin/Release/net10.0/Hexalith.EventStore.Contracts.Tests.dll -class '*Routing.*'
dotnet tests/Hexalith.EventStore.QueryRouting.Tests/bin/Release/net10.0/Hexalith.EventStore.QueryRouting.Tests.dll
dotnet tests/Hexalith.EventStore.ServiceDefaults.Tests/bin/Release/net10.0/Hexalith.EventStore.ServiceDefaults.Tests.dll
dotnet tests/Hexalith.EventStore.AppHost.Tests/bin/Release/net10.0/Hexalith.EventStore.AppHost.Tests.dll -class '*RouteCatalogTopologyTests'
dotnet tests/Hexalith.EventStore.RestApi.Generators.Tests/bin/Release/net10.0/Hexalith.EventStore.RestApi.Generators.Tests.dll
dotnet tests/Hexalith.EventStore.Client.Tests/bin/Release/net10.0/Hexalith.EventStore.Client.Tests.dll -class '*Handlers.DaprServiceInvocationHandlerTests' -class '*Registration.DaprServiceInvocationRegistrationTests'
python3 -m unittest discover -s tools/tests -p 'test_dapr_broker_qualification.py'
actionlint .github/workflows/integration.yml
git diff --check
bash scripts/check-committed-files.sh
```

The final workflow lint, whitespace check and committed-file guard pass; 9,319 tracked files were checked. No catalog-owned Redis container remained after the fresh final live drill. The fixture still uses the existing checksum-verified isolated daprd and pinned temporary Sentry/Redis inputs; the disclosed iteration-1 shared-Redis side effect remains unchanged and was not guessed or repaired.

Earlier Debug/source validation built Server.Tests and generator tests without warnings/errors and passed 573 affected Server cases plus all 133 generator cases. Subsequent final changes were checked in the focused package lanes and the parent's complete final Server lane above. During re-derivation, broad diagnostics exposed an optional-capture timeout regression; it was corrected without weakening the original bounded-slot assertions. The two remote-enrollment race controls first failed against the pre-correction primitive (`/tmp/story513-rederive-remote-races-red.log`) and then passed in the final Routing lane. The 18 original suite skips supply none of this catalog matrix coverage; no new covering test is skipped or disabled.

All frozen matrix rows have executed passing controls: exact-host lifecycle and Dapr readback; invalid candidate/independently drifted target refusal; guarded command/query/projection/generated ingress and receiver scopes; metadata stability/cache eviction; commit/rollback/restart/concurrent/cancellation/disposal races; and Development versus absent production authority. B08 remains an unverified production-owner qualification item: request-to-fence correlation must be qualified before enabling a production quiescence verifier. No production verifier is registered by default, and Stories 5.6/5.7 plus the Platform profile/trust/observer/hosted qualification and production activation remain open.

### Parent re-derivation audit (2026-10-28)

The parent read the entire implementation diff through the preserved initial baseline and each subsequent frozen-source delta. Iteration-2 audit directories are `/tmp/story513-audit-e3x7e_mb`, `/tmp/story513-audit-wa2pd6i2`, `/tmp/story513-audit-bsedt_2i`, `/tmp/story513-audit-znt7vxnu` and `/tmp/story513-audit-x01kv9fb`; every chunk in each was read. Source remained frozen while the parent independently executed the final 129-case Routing lane and the final 2,093-case affected Server lane above. The eight execution tasks are complete for the approved Development delivery. All six frozen matrix rows retain executed positive and negative coverage, with no skipped covering case. The production/CI acceptance boundaries remain open.

All verified iteration-1 defects except the explicitly unverified production-owner B08 item have been corrected. B11 remains rejected for its documented refutation. Canonical vectors/catalog bytes, pinned versions, index and submodules remain unchanged. The independent external status commit is preserved and included in the baseline-relative diff without being claimed as this story's implementation.

Additional parent/implementation audit findings were corrected before the second independent review:

| ID | Verdict | Route | Evidence and correction |
| --- | --- | --- | --- |
| P02 | low | patch — fixed | The normalization edit duplicated a static helper validation. The helper is restored to its original single validation; the main resolver retains pre-selection normalization. |
| P03 | medium | patch — fixed | Actual MVC returned 200 after the filter set ActionExecutingContext.Result following next(). Standalone proof is `/tmp/story513-mvc-probe.log`. The filter now changes ActionExecutedContext.Result and preserves early errors; five actual HTTP/MVC controls pass. |
| P04 | low | patch — fixed | Initial stale-header controls lacked corresponding post-transition positives. Current-header positives now pass after both commit and same-root rollback, alongside zero-effect stale negatives. |
| P05 | high | patch — fixed | Remote factories ran before enrollment, and captured contexts could start effects after disposal. Both deterministic controls failed before correction (`/tmp/story513-rederive-remote-races-red.log`). Atomic enrollment now precedes factory invocation and refuses closed execution; both controls pass. |
| P06 | high | patch — fixed | Resetting the release-retry flag after a store error reopened execution. A permanent closed-for-execution flag now survives release failure; the extended control proves zero effects before a successful disposal retry. |
| P07 | medium | patch — fixed | A newly added cancellation wait freed bounded native-capture slots while actor tasks remained active. The wait was removed; actual-task enrollment retains abandoned execution fences while the original capture-slot invariant and full Named coordinator controls pass. |

The parent Routing command used `-result-xml /tmp/story513-parent-final-routing.xml`, with output `/tmp/story513-parent-final-routing.log`: **129 passed, no skips/failures/not-run**. The complete final Server XML records **2,076 passes and 17 original skips**; every Routing case passes within that broader run as well. Fresh final live output records **3 passes, no skips/failures**, and an exact owner-label Docker query finds no surviving catalog Redis container. These are local working-tree results; they do not replace CI or production-owner qualification.

### Second-review re-derivation verification (2026-10-28)

The second-review requirements are implemented for the approved EventStore/SDK and separate Development fixture delivery. The immutable planning baseline remains `df11852aadcee814bba094c78a60dd2a1fdb934b`; current HEAD remains `be4aba873d09736abb4caacf1fae8ad870f0b3b6`. The three independent status-change paths are preserved. This is an uncommitted working-tree implementation; no commit/ref, dependency-version, deployment, publication or submodule mutation was performed. CI link: unavailable because this working tree has not run in CI. Parent acceptance, production authority and strict completeness certification remain open.

The reconstructed implementation retains all prior lifecycle, receiver, canonical-byte, authorization, SDK header-owner, remote ambiguity, disposal retry and owned-infrastructure protections. The new corrections are:

- Host identity now includes independently captured installed admission policy/key-generation availability, actual named-handler capability fingerprints and actual receiver endpoint operation policies. Capture helpers read registered adapters, configured policy and disposable key providers, SDK handler registrations/options and effective endpoint metadata, without taking candidate content. Contracts supplies generation-independent static admission comparison; the v1/v2 envelope format and retained vectors are unchanged. Trusted incompatible adapter, key metadata, capability and operation candidates fail readiness while persisted prior authority remains intact; matching candidates then commit successfully. Missing observations, disabled admission or unavailable key generations refuse.
- Reconstruction binds only its relevant committed command/domain executable tenant scope, rejects absent scope, and compares the actual canonical auxiliary operation. Unrelated wildcard query/projection routes cannot widen it.
- Exact canonical control methods and verbs can use independently configured recipients through the standard HTTP factory pipeline before activation and while Pending. The final SDK handler still owns the routing headers. The owned hosted drill now performs bootstrap, prepare/ready/commit, protected readback, abort/rollback and proof reconciliation through that pipeline, with unauthorized and unknown-method negatives.
- Generated actions refuse absent hosting validation. Existing manual positive fixtures install explicit test callbacks. Actual emitted-controller HTTP tests prove both the positive callback and missing-callback zero-gateway-effect refusal.
- Admission retains fences at actual atomically enrolled actor calls. Local boundary/proof failures and cancellation after a completed actor call release; ambiguous actor completion retains. Known-quiescent own-lease release retries CAS contention with bounded backoff beyond 32 conflicts, while backend failure still preserves the fence and explicit disposal retry.
- Lifecycle, receiver and readiness boundaries emit closed, nonsecret structured refusal categories without exception messages, credentials, proofs or request bodies. Concrete catalog-backed query invocation reaches the committed HTTP sink despite a conflicting resolver. Activated actor projection mismatches refuse before payload parsing or subsequent lifecycle/ETag processing, with matching controls.

All nine affected projects built separately in Release/package mode with zero warnings/errors. The exact command was `dotnet build tests/Hexalith.EventStore.<Name>.Tests/Hexalith.EventStore.<Name>.Tests.csproj -c Release -p:UseHexalithProjectReferences=false -p:UseNuGetDeps=true -m:1`, for `Server`, `DomainService`, `Server.LiveSidecar`, `RestApi.Generators`, `Contracts`, `QueryRouting`, `ServiceDefaults`, `AppHost` and `Client`. Build logs are `/tmp/story513-r2-{server,domainservice,live,generator,contracts,queryrouting,servicedefaults,apphost,client}-release-build.log`. The final Debug/source Server build also passed with zero warnings/errors: `dotnet build tests/Hexalith.EventStore.Server.Tests/Hexalith.EventStore.Server.Tests.csproj -c Debug -p:UseHexalithProjectReferences=true -m:1`; log `/tmp/story513-r2-final-server-debug-build.log`.

| Executed scope | Result | Log |
| --- | --- | --- |
| Final affected Server Commands/Queries/Pipeline/Projections/Routing/DomainServices/QueriesController/admission-actor lane | 2,126 total; 2,109 passed; 17 original skips; 0 failures/errors/not-run | `/tmp/story513-r2-server-affected.log`; XML `/tmp/story513-r2-server-affected.xml` |
| Final Release Routing lane | 162 passed; 0 skipped/failed | `/tmp/story513-r2-routing-release.log`; XML `/tmp/story513-r2-routing-release.xml` |
| Final Debug/source Routing lane | 162 passed; 0 skipped/failed | `/tmp/story513-r2-final-routing-debug.log` |
| Full DomainService suite, preserving every original wire/source/credential assertion | 1,277 total; 1,276 passed; 1 original skip; 0 failures | `/tmp/story513-r2-domainservice-release-tests.log` |
| Owned Dapr state/CAS/cache and slim/mTLS hosted catalog drill | 3 passed; 0 skipped/failed | `/tmp/story513-r2-owned-live.log` |
| Existing live command and named-projection persistence controls | 2 + 1 passed; 0 skipped/failed | `/tmp/story513-r2-existing-live-command.log`, `/tmp/story513-r2-existing-live-projection.log` |
| Contracts routing / full QueryRouting / full ServiceDefaults | 142 / 15 / 16 passed | `/tmp/story513-r2-contracts-tests.log`, `/tmp/story513-r2-queryrouting-tests.log`, `/tmp/story513-r2-servicedefaults-tests.log` |
| AppHost topology / full REST generator / SDK Dapr header-owner controls | 9 / 135 / 20 passed | `/tmp/story513-r2-apphost-tests.log`, `/tmp/story513-r2-generator-release-tests.log`, `/tmp/story513-r2-client-tests.log` |
| Existing broker-input regression suite | 25 passed | `/tmp/story513-r2-ci-input-tests.log` |

Exact test commands:

```sh
dotnet tests/Hexalith.EventStore.Server.Tests/bin/Release/net10.0/Hexalith.EventStore.Server.Tests.dll -class '*Commands.*' -class '*Queries.*' -class '*Pipeline.*' -class '*Projections.*' -class '*Routing.*' -class '*DomainServices.*' -class '*Controllers.QueriesControllerTests' -class '*Actors.IdempotencyAdmissionActorTests' -result-xml /tmp/story513-r2-server-affected.xml
dotnet tests/Hexalith.EventStore.Server.Tests/bin/Release/net10.0/Hexalith.EventStore.Server.Tests.dll -class '*Routing.*' -result-xml /tmp/story513-r2-routing-release.xml
dotnet tests/Hexalith.EventStore.Server.Tests/bin/Debug/net10.0/Hexalith.EventStore.Server.Tests.dll -class '*Routing.*'
dotnet tests/Hexalith.EventStore.DomainService.Tests/bin/Release/net10.0/Hexalith.EventStore.DomainService.Tests.dll
EVENTSTORE_ROUTE_CATALOG_DAPRD=/tmp/route-catalog-ci-preparation/route-catalog-tools/daprd dotnet tests/Hexalith.EventStore.Server.LiveSidecar.Tests/bin/Release/net10.0/Hexalith.EventStore.Server.LiveSidecar.Tests.dll -class '*RouteCatalogDevelopmentLiveTests' -class '*RouteCatalogHostedLiveTests'
dotnet tests/Hexalith.EventStore.Server.LiveSidecar.Tests/bin/Release/net10.0/Hexalith.EventStore.Server.LiveSidecar.Tests.dll -class '*Commands.CommandRoutingIntegrationTests'
dotnet tests/Hexalith.EventStore.Server.LiveSidecar.Tests/bin/Release/net10.0/Hexalith.EventStore.Server.LiveSidecar.Tests.dll -method '*NormalDelivery_PersistsIndependentDetailIndexCheckpointsAndConvergedRetryLedger'
dotnet tests/Hexalith.EventStore.Contracts.Tests/bin/Release/net10.0/Hexalith.EventStore.Contracts.Tests.dll -class '*Routing.*'
dotnet tests/Hexalith.EventStore.QueryRouting.Tests/bin/Release/net10.0/Hexalith.EventStore.QueryRouting.Tests.dll
dotnet tests/Hexalith.EventStore.ServiceDefaults.Tests/bin/Release/net10.0/Hexalith.EventStore.ServiceDefaults.Tests.dll
dotnet tests/Hexalith.EventStore.AppHost.Tests/bin/Release/net10.0/Hexalith.EventStore.AppHost.Tests.dll -class '*RouteCatalogTopologyTests'
dotnet tests/Hexalith.EventStore.RestApi.Generators.Tests/bin/Release/net10.0/Hexalith.EventStore.RestApi.Generators.Tests.dll
dotnet tests/Hexalith.EventStore.Client.Tests/bin/Release/net10.0/Hexalith.EventStore.Client.Tests.dll -class '*Handlers.DaprServiceInvocationHandlerTests' -class '*Registration.DaprServiceInvocationRegistrationTests'
python3 -m unittest discover -s tools/tests -p 'test_dapr_broker_qualification.py'
actionlint .github/workflows/integration.yml
git diff --check
bash scripts/check-committed-files.sh
```

Workflow lint, whitespace and the committed-file guard pass (9,319 tracked files). No catalog-owned Redis container remains; the exact owner-label query output is empty in `/tmp/story513-r2-container-cleanup.log`. The isolated daprd still matches locked SHA-256 `e988a6f53abec017fc9ebcb4f89de9bed11bce005eb49bd8e85e8d3d6adbf488`. No canonical catalog bytes, pins, original business/security assertions or original skip conditions changed. No new covering test is skipped. The disclosed earlier shared-Redis side effect remains unchanged and was not guessed or repaired.

The ordinary AppHost baseline was started, described and stopped with the Aspire CLI before editing. Startup succeeded; `security` was Unhealthy and application resources remained Waiting. Logs are `/tmp/story513-r2-aspire-{start,described,stop}.log`. This is a baseline limitation, not passing topology evidence. The separate owned Development drill supplies this delivery's qualification. Stories 5.6/5.7 and Platform production profile/trust/observer/hosted qualification, activated production authority and B08 request-to-fence correlation qualification remain open. These local results do not replace CI or grant parent-story completion.

### Parent second-review implementation audit (2026-10-28)

The parent read every code delta in /tmp/story513-audit-se_71lgu (nine chunks), /tmp/story513-audit-8r7m1rlo (one) and /tmp/story513-audit-15dmp0oh (one), plus the completed verification section. The final delta strengthens two tests without changing production code. The concrete query target now has an independently attested non-default method; actor projection refusal uses valid JSON so another failure cannot mask a missing identity check. Both matching and refusing controls ran and passed.

| ID | Verdict | Route | Evidence and correction |
| --- | --- | --- | --- |
| P08 | medium | patch — fixed | A default query method could not detect reverting to QueryMethodName. The concrete invoker now uses an activated catalog-query-value target distinct from that constant, with the conflicting resolver untouched. |
| P09 | medium | patch — fixed | Malformed JSON could independently produce the asserted actor-response failure. Both responses now carry valid payloads; the foreign projection must refuse before the second lifecycle read, and the matching payload succeeds. |

The parent independently reran all final Routing classes after those corrections: **162 passed, zero skipped/failed/not-run**, 15.644 seconds. Exact command: `dotnet tests/Hexalith.EventStore.Server.Tests/bin/Release/net10.0/Hexalith.EventStore.Server.Tests.dll -class '*Routing.*' -result-xml /tmp/story513-parent-r2-routing.xml`; output /tmp/story513-parent-r2-routing.log. Its XML explicitly records every current matrix control, including trusted installed-policy drift, standard control transport, local admission rejection, contended release and both strengthened query cases. The earlier 2,126-case broad Server run covers the same production source; only the four query test controls and fixture documentation changed afterward. No broader repeat is needed to qualify those test-only changes.

All eight execution tasks are complete for the approved local Development delivery, and all six frozen matrix rows have executed passing, non-skipped controls. The parent inspected the recorded nine clean package builds and passing companion suites, live output and cleanup evidence. The README now documents independent installed facts, the explicit control-recipient option and SDK header-routed control URLs. Wire codecs/vectors and retained catalog bytes remain compatible; the codec has only the additive Contracts-owned static policy-digest helper. Dependency pins, index and submodules remain unchanged. Parent-story/CI/production acceptance and the unverified B08 production-owner item remain open.

### Third-review re-derivation implementation and verification (2026-10-28)

The approved EventStore/SDK and separate Development delivery has been reconstructed and the third-review boundary requirements implemented. The immutable planning baseline is `df11852aadcee814bba094c78a60dd2a1fdb934b`; current HEAD remains `be4aba873d09736abb4caacf1fae8ad870f0b3b6`. The independently committed status paths and the initial epic/sprint edits are preserved. This is an uncommitted working-tree implementation. No commit/ref/index, dependency-version, publication, deployment or submodule mutation was performed. CI link: unavailable because this working tree has not run in CI. Parent review/acceptance, strict completeness certification and production authority remain open.

The implementation retains all earlier verified lifecycle, canonical-byte, authorization, credential/header ownership, inventory, cache, exact tenant, fencing, reconciliation, disposal-retry and owned-infrastructure behavior. The new corrections are:

- The standard primary HTTP transport disables automatic redirects beneath the catalog guard and refuses raw Dapr actor invocation URLs before any HTTP sink effect. Normal declared invocation and exact protected control methods retain their existing SDK header ownership. The supported typed/weak actor proxy paths retain their router checks.
- The sole public `QueryRouter` constructor requires catalog authority. Direct public and ordinary type-based DI construction both refuse undeclared actor queries before actor/proxy effects. Legacy protocol seams are internal and available only to existing friend assemblies.
- Command ordinary/fenced/coordinated branches and projection replay methods construct known local clients/proxies before actual remote enrollment. The same audit corrects known admission and projection HTTP factories. Opaque factories still enroll the complete captured lease set atomically before invocation; actual failed/abandoned actor tasks retain their non-expiring fences.
- Rebuild output authorization joins the admitted tenant, domain and the selected committed app/method/version with the owning declaration's complete output inventory. A non-callable declared output is valid only within that scope. Other-tenant/other-target declarations cannot authorize checkpoint access.
- Effective state component, state key and normalized selected Dapr HTTP endpoint are captured once by `DaprRouteCatalogStateStore` from the same configuration used for its RPCs. Common host observation reads those effective coordinates and includes them in host identity; custom stores require explicit independent coordinate fixtures. The loaded component's `metadata.name` must match. Key/component/endpoint drift, separately valid cells with the old identity, and absent coordinate observations refuse. Every actual executable target also joins an actual compiled declaration with the same RouteId and canonical digest; candidate-supplied digests cannot fill an absent local declaration.
- SDK command receivers and outbound invocation share the Contracts-owned historical version normalization/validation. Absent/empty/whitespace overrides use the committed version; explicit matching versions pass and conflicting/invalid versions refuse before processing. Successful concrete HTTP query responses must match the captured declaration's projection identity. Catalog transport exceptions and catalog-bound receiver 503 responses propagate as catalog refusals through nested gateway dispatch. The actual HTTP exception response preserves `application/problem+json` and the UX-DR2 pre-pipeline convention without `correlationId`.
- A minimal Contracts-owned actual-RPC boundary connects Client state transports to the existing hosting lease primitive without a Client-to-ServiceDefaults reference. Endpoint work and deferred `IResult.ExecuteAsync` each enter/restore the receiver scope. SDK read-model reads, writes, deletes, reconciliation and transactions enroll after local validation; token cancellation known before the SDK call releases normally. Injected post-persistence transport faults/cancellation retain the durable receiver fence even when the actual dispatcher returns a normal Retryable/Indeterminate HTTP response. Completed state work and local-only validation/cancellation drain. Arbitrary custom handler remote work must use this same explicit boundary; no automatic certainty is claimed for other work.
- The activated concrete `DaprProjectionLifecycleGateway.ReadSnapshotAsync` HTTP transport-fault control proves that QueryRouter's existing lifecycle-error handling cannot clear its fence or permit replacement readiness. The matching successful completed-call control drains normally.

All nine affected test projects built separately in Release/package mode with zero warnings/errors. The command for each `<Name>` was:

```sh
dotnet build tests/Hexalith.EventStore.<Name>.Tests/Hexalith.EventStore.<Name>.Tests.csproj -c Release -p:UseHexalithProjectReferences=false -p:UseNuGetDeps=true -m:1
```

The executed names were `Server`, `DomainService`, `Server.LiveSidecar`, `RestApi.Generators`, `Contracts`, `QueryRouting`, `ServiceDefaults`, `AppHost` and `Client`. Logs are `/tmp/story513-r3-{server,domainservice,live,generator,contracts,queryrouting,servicedefaults,apphost,client}-release-build.log`. Debug/source Server validation also built cleanly with `dotnet build tests/Hexalith.EventStore.Server.Tests/Hexalith.EventStore.Server.Tests.csproj -c Debug -p:UseHexalithProjectReferences=true -m:1`; log `/tmp/story513-r3-final-server-debug-build.log`.

| Executed scope | Result | Log |
| --- | --- | --- |
| Final Release affected Server Commands/Queries/Pipeline/Projections/Routing/DomainServices/QueriesController/admission-actor lane | 2,191 total; 2,174 passed; 17 original skips; 0 failures/errors/not-run | `/tmp/story513-r3-server-affected-tests.log`; XML `/tmp/story513-r3-server-affected.xml` |
| Debug/source Routing lane | 227 passed; 0 skipped/failed/not-run | `/tmp/story513-r3-routing-debug.log`; XML `/tmp/story513-r3-routing-debug.xml` |
| Final Release Routing controls, included in the broader Server lane | 227 passed; 0 skipped/failed/not-run | `/tmp/story513-r3-server-affected.xml` |
| Full DomainService suite with original wire/source/credential assertions | 1,277 total; 1,276 passed; 1 original skip; 0 failures | `/tmp/story513-r3-domainservice-tests.log` |
| Owned Dapr state/CAS/cache and slim/mTLS hosted catalog drill | 3 passed; 0 skipped/failed | `/tmp/story513-r3-owned-live.log` |
| Existing live command and named projection persistence controls | 2 + 1 passed; 0 skipped/failed | `/tmp/story513-r3-existing-live-command.log`, `/tmp/story513-r3-existing-live-projection.log` |
| Contracts routing / full QueryRouting / full ServiceDefaults | 142 / 15 / 16 passed | `/tmp/story513-r3-{contracts,queryrouting,servicedefaults}-tests.log` |
| AppHost topology / full REST generator | 9 / 135 passed | `/tmp/story513-r3-{apphost,generator}-tests.log` |
| Client projection store/protocol controls | 748 total; 746 passed; 2 existing skips; 0 failures | `/tmp/story513-r3-client-projections-tests.log` |
| Existing SDK Dapr header-owner controls | 20 passed | `/tmp/story513-r3-client-headers-tests.log` |
| Existing broker-input regression suite | 25 passed | `/tmp/story513-r3-ci-input-tests.log` |

The 65 new routing cases comprise 20 transport/construction/factory/lifecycle/HTTP-response cases, 11 authority-coordinate/declaration cases, 16 actual HTTP receiver/state-boundary cases, seven SDK receiver-version cases, three rebuild output-scope cases and eight direct/nested concrete query response cases. All execute and pass; none is skipped. State controls use the actual SDK accessor and dispatcher with injected post-persistence state-transport faults and inspect both persisted state bytes and durable authority-cell leases. I/O faults preserve the existing Retryable dispatcher classification, while cancellation preserves Indeterminate. Both normal HTTP responses retain ambiguous fences. These deterministic controls do not assert certainty for arbitrary custom handlers. The existing exact source/wire/security assertions and original skip conditions remain intact.

Exact test commands:

```sh
dotnet tests/Hexalith.EventStore.Server.Tests/bin/Release/net10.0/Hexalith.EventStore.Server.Tests.dll -class '*Commands.*' -class '*Queries.*' -class '*Pipeline.*' -class '*Projections.*' -class '*Routing.*' -class '*DomainServices.*' -class '*Controllers.QueriesControllerTests' -class '*Actors.IdempotencyAdmissionActorTests' -result-xml /tmp/story513-r3-server-affected.xml
dotnet tests/Hexalith.EventStore.Server.Tests/bin/Debug/net10.0/Hexalith.EventStore.Server.Tests.dll -class '*Routing.*' -result-xml /tmp/story513-r3-routing-debug.xml
dotnet tests/Hexalith.EventStore.DomainService.Tests/bin/Release/net10.0/Hexalith.EventStore.DomainService.Tests.dll
EVENTSTORE_ROUTE_CATALOG_DAPRD=/tmp/route-catalog-ci-preparation/route-catalog-tools/daprd dotnet tests/Hexalith.EventStore.Server.LiveSidecar.Tests/bin/Release/net10.0/Hexalith.EventStore.Server.LiveSidecar.Tests.dll -class '*RouteCatalogDevelopmentLiveTests' -class '*RouteCatalogHostedLiveTests'
dotnet tests/Hexalith.EventStore.Server.LiveSidecar.Tests/bin/Release/net10.0/Hexalith.EventStore.Server.LiveSidecar.Tests.dll -class '*Commands.CommandRoutingIntegrationTests'
dotnet tests/Hexalith.EventStore.Server.LiveSidecar.Tests/bin/Release/net10.0/Hexalith.EventStore.Server.LiveSidecar.Tests.dll -method '*NormalDelivery_PersistsIndependentDetailIndexCheckpointsAndConvergedRetryLedger'
dotnet tests/Hexalith.EventStore.Contracts.Tests/bin/Release/net10.0/Hexalith.EventStore.Contracts.Tests.dll -class '*Routing.*'
dotnet tests/Hexalith.EventStore.QueryRouting.Tests/bin/Release/net10.0/Hexalith.EventStore.QueryRouting.Tests.dll
dotnet tests/Hexalith.EventStore.ServiceDefaults.Tests/bin/Release/net10.0/Hexalith.EventStore.ServiceDefaults.Tests.dll
dotnet tests/Hexalith.EventStore.AppHost.Tests/bin/Release/net10.0/Hexalith.EventStore.AppHost.Tests.dll -class '*RouteCatalogTopologyTests'
dotnet tests/Hexalith.EventStore.RestApi.Generators.Tests/bin/Release/net10.0/Hexalith.EventStore.RestApi.Generators.Tests.dll
dotnet tests/Hexalith.EventStore.Client.Tests/bin/Release/net10.0/Hexalith.EventStore.Client.Tests.dll -class '*Projections.*'
dotnet tests/Hexalith.EventStore.Client.Tests/bin/Release/net10.0/Hexalith.EventStore.Client.Tests.dll -class '*Handlers.DaprServiceInvocationHandlerTests' -class '*Registration.DaprServiceInvocationRegistrationTests'
python3 -m unittest discover -s tools/tests -p 'test_dapr_broker_qualification.py'
actionlint .github/workflows/integration.yml
git diff --check
bash scripts/check-committed-files.sh
```

The integration workflow lint, whitespace check and committed-file guard pass; 9,319 tracked files were checked. The isolated daprd still matches locked SHA-256 `e988a6f53abec017fc9ebcb4f89de9bed11bce005eb49bd8e85e8d3d6adbf488`. The exact catalog owner-label Docker query finds no surviving Redis fixture container (`/tmp/story513-r3-container-cleanup.log`). Canonical wire vectors/catalog bytes, dependency pins, index and submodules are unchanged. The earlier disclosed shared-Redis side effect is unchanged and was not guessed or repaired. The ordinary AppHost was started/described/stopped for the required pre-edit baseline; application resources were Waiting. That ordinary topology is not the separate Development drill's qualification and supplies no production readiness.

All six frozen matrix rows retain executed positive/negative coverage, including actual gateway fallback, metadata stability, stale receiver headers, exact control transport, Dapr persisted generation readback and failure/concurrency/cancellation phases. Stories 5.6/5.7 and the Platform production profile/trust/host-observer/hosted qualification and activation remain open. B08 remains the same unverified production-owner request-to-fence correlation qualification; no production verifier is registered by default. These local results do not replace CI, parent review or production-owner acceptance.

The post-edit ordinary AppHost restart also succeeded and was described and stopped through the Aspire CLI; logs are `/tmp/story513-r3-final-aspire-{start,describe,stop}.log`. The resource engine reported Running/Healthy application processes. That process status is not catalog activation or a production qualification; the separate owned Development drill and activated boundary tests supply the catalog evidence above.


### Parent verification after P10 (2026-10-28)

The parent read every incremental code-diff chunk against the previously audited full baseline: `/tmp/story513-audit-utc942io/delta-{01..07}.diff`, `/tmp/story513-audit-qnj73lfo/delta-01.diff` and `/tmp/story513-audit-_08ejx0m/delta-01.diff`. The final audited 178 non-planning changed-file path/content set has SHA-256 `3fde064f7f0890fcfa95a61f44e13401c7c2003e6524bd7d8f0eb32d9b1cfd01` (sorted UTF-8 path, NUL, content, NUL). HEAD remains `be4aba873d09736abb4caacf1fae8ad870f0b3b6`; the original planning baseline and frozen intent hash remain unchanged. The source revision includes P10 and has no source changes during this verification. All eight local execution tasks are complete, and all six frozen matrix rows have passing executed controls. Parent-story, CI and production acceptance remain open.

The parent repeated each of the nine listed Release/package builds individually with `-m:1`, using exactly `dotnet build tests/Hexalith.EventStore.<Project>.Tests/Hexalith.EventStore.<Project>.Tests.csproj -c Release -p:UseHexalithProjectReferences=false -p:UseNuGetDeps=true -m:1` for `Server`, `DomainService`, `Server.LiveSidecar`, `Contracts`, `QueryRouting`, `ServiceDefaults`, `AppHost`, `RestApi.Generators` and `Client`. All returned zero, with zero warnings/errors. Logs: `/tmp/story513-parent-r3-{server,domainservice,live,contracts,queryrouting,servicedefaults,apphost,generator,client}-release-build.log`.

| Repeated parent check | Result |
| --- | --- |
| Debug/source Routing | 245 passed; 0 failures/skips/not-run |
| Release affected Server, including DomainServices | 2,209 total; 2,192 passed; 17 original skips; 0 failures/not-run |
| Full DomainService | 1,277 total; 1,276 passed; 1 original skip; 0 failures/not-run |
| Owned state/CAS/cache and slim/mTLS hosted Development drill | 3 passed; 0 failures/skips/not-run |
| Existing live command / named projection persistence | 2 / 1 passed; 0 failures/skips/not-run |
| Contracts Routing / QueryRouting / ServiceDefaults | 142 / 15 / 16 passed |
| AppHost topology / full REST generator | 9 / 135 passed |
| Client Projections / SDK header-owner controls | 746 passed + 2 original skips / 20 passed |
| Broker-input regression suite | 25 passed |
| actionlint / whitespace / committed-file guard | Passed; 9,319 tracked files |

Parent commands and complete output are recorded in `/tmp/story513-parent-r3-<scope>.log`; each xUnit run additionally writes the corresponding `.xml`. Exact commands were the following (the additional result-xml argument is shown once for the Debug run and applies analogously to every Release run):

```sh
dotnet tests/Hexalith.EventStore.Server.Tests/bin/Debug/net10.0/Hexalith.EventStore.Server.Tests.dll -class '*Routing.*' -result-xml /tmp/story513-parent-r3-routing-debug.xml
dotnet tests/Hexalith.EventStore.Server.Tests/bin/Release/net10.0/Hexalith.EventStore.Server.Tests.dll -class '*Commands.*' -class '*Queries.*' -class '*Pipeline.*' -class '*Projections.*' -class '*Routing.*' -class '*DomainServices.*' -class '*Controllers.QueriesControllerTests' -class '*Actors.IdempotencyAdmissionActorTests'
dotnet tests/Hexalith.EventStore.DomainService.Tests/bin/Release/net10.0/Hexalith.EventStore.DomainService.Tests.dll
EVENTSTORE_ROUTE_CATALOG_DAPRD=/tmp/route-catalog-ci-preparation/route-catalog-tools/daprd dotnet tests/Hexalith.EventStore.Server.LiveSidecar.Tests/bin/Release/net10.0/Hexalith.EventStore.Server.LiveSidecar.Tests.dll -class '*RouteCatalogDevelopmentLiveTests' -class '*RouteCatalogHostedLiveTests'
dotnet tests/Hexalith.EventStore.Server.LiveSidecar.Tests/bin/Release/net10.0/Hexalith.EventStore.Server.LiveSidecar.Tests.dll -class '*Commands.CommandRoutingIntegrationTests'
dotnet tests/Hexalith.EventStore.Server.LiveSidecar.Tests/bin/Release/net10.0/Hexalith.EventStore.Server.LiveSidecar.Tests.dll -method '*NormalDelivery_PersistsIndependentDetailIndexCheckpointsAndConvergedRetryLedger'
dotnet tests/Hexalith.EventStore.Contracts.Tests/bin/Release/net10.0/Hexalith.EventStore.Contracts.Tests.dll -class '*Routing.*'
dotnet tests/Hexalith.EventStore.QueryRouting.Tests/bin/Release/net10.0/Hexalith.EventStore.QueryRouting.Tests.dll
dotnet tests/Hexalith.EventStore.ServiceDefaults.Tests/bin/Release/net10.0/Hexalith.EventStore.ServiceDefaults.Tests.dll
dotnet tests/Hexalith.EventStore.AppHost.Tests/bin/Release/net10.0/Hexalith.EventStore.AppHost.Tests.dll -class '*RouteCatalogTopologyTests'
dotnet tests/Hexalith.EventStore.RestApi.Generators.Tests/bin/Release/net10.0/Hexalith.EventStore.RestApi.Generators.Tests.dll
dotnet tests/Hexalith.EventStore.Client.Tests/bin/Release/net10.0/Hexalith.EventStore.Client.Tests.dll -class '*Projections.*'
dotnet tests/Hexalith.EventStore.Client.Tests/bin/Release/net10.0/Hexalith.EventStore.Client.Tests.dll -class '*Handlers.DaprServiceInvocationHandlerTests' -class '*Registration.DaprServiceInvocationRegistrationTests'
python3 -m unittest discover -s tools/tests -p 'test_dapr_broker_qualification.py'
actionlint .github/workflows/integration.yml
git diff --check
bash scripts/check-committed-files.sh
```

The exact-label Docker query `docker ps -a --filter label=hexalith.route-catalog-fixture --format '{{.ID}} {{.Names}} {{.Status}}'` returned no surviving fixture containers; evidence is `/tmp/story513-parent-r3-container-cleanup.log`. No shared Redis restoration was attempted; the disclosed earlier writes and unknown prior values remain unchanged. Canonical retained catalog bytes, dependency pins, submodules and the empty index are preserved. CI link is still unavailable for this uncommitted working tree. This evidence qualifies the approved local Development delivery only.

### Fourth-review implementation and executed verification (2026-10-28; acceptance blocked)

The iteration-4 implementation was reconstructed from its preservation source and corrected against the amended requirements. HEAD remains `be4aba873d09736abb4caacf1fae8ad870f0b3b6`; the immutable planning baseline remains `df11852aadcee814bba094c78a60dd2a1fdb934b`. Frozen intent and retained catalog bytes are unchanged. This is an uncommitted working tree. No commit/ref/index, dependency-version, deployment, publication or submodule mutation was performed. CI link: unavailable because this working tree has not run in CI. Parent-story/production acceptance and strict completeness certification remain open. The additional ordinary-redirect counterexample below blocks acceptance of this implementation despite the passing planned suites.

Implemented corrections include:

- All inventoried public construction families require nonnull catalog authority; old protocol compositions remain internal/friend-only. Runtime DI retains its installed collaborators. QueryRouter has a distinct internal required-authority core retaining its invoker, lifecycle gateway and source guard. The CatalogConstruction tool and existing AppHost configuration fixtures have explicit friend access to their construction-only protocol seam. The exact old five-argument DaprDomainServiceInvoker signature remains internal. Its old API-visibility-only test was intentionally migrated to assert that isolation and the required-authority public surface; every wire, source, credential, security and business assertion remains intact. Existing live protocol composition now explicitly calls its internal friend constructor instead of relying on hiding authority from ActivatorUtilities.
- Ordinary primary HTTP transports retain their configured redirect behavior and effective native configuration. Guarded invocation uses a separate owned redirect-disabled primary below the same final SDK header owner. Native settings include automatic/manual certificate mode, TLS, credentials, proxy and connection configuration. Unknown custom primaries require an explicit separately owned invocation factory; factory failure closes invocation while ordinary HTTP remains available. Owner-returned native handlers also have redirects disabled on that separate handler. Basic Dapr invocation usernames are refused case-insensitively before authority or transport in empty, active and Pending phases; unrelated ordinary Basic authentication remains available.
- Auxiliary named replay validates committed project/v2 tenant/callable outputs before direct mapped actions or multipart uploads, retains canonical operation/capability/source/header checks, and rejects a conflicting executable version. Projection method plans admit one actual app/version and its tenant-scoped canonical legacy/named methods, preserve mixed delivery, and select rebuild outputs by their owning method. Named-only rebuild reuses the existing stage/commit/verify promotion and checkpoint readback protocol. Legacy output scopes retain projection-specific source/provenance inspection even after named metadata removal; established/unavailable guards refuse before legacy HTTP/model/checkpoint effects. Compatible replay's prior guard checks remain present.
- Concrete authority backend read/mutation failures, non-caller HTTP timeouts and unusable state responses produce closed nonsecret catalog 503 responses. Caller cancellation, CAS conflicts, ambiguous-write fences and release retry remain intact. Generated declaration/target/tenant callback refusal emits the existing bounded diagnostic before downstream effects.
- Owned Redis/Sentry tooling captures Docker context/host precedence before mutation and pins a validated local Unix endpoint for execution and exact-ID/owner-label cleanup. Sentry uses the same captured Docker selection as Redis. Infrastructure subprocesses have bounded timeout/cancellation and owned process-tree cleanup. The catalog CI preparation step also validates and pins its endpoint before either existing image pull; four tests execute that actual Python selection prefix against owned fake tooling without contacting a daemon.

All nine affected test projects built separately in Release/package mode with zero warnings/errors, using:

```sh
dotnet build tests/Hexalith.EventStore.<Name>.Tests/Hexalith.EventStore.<Name>.Tests.csproj -c Release -p:UseHexalithProjectReferences=false -p:UseNuGetDeps=true -m:1
```

The executed names were `Server`, `DomainService`, `Server.LiveSidecar`, `RestApi.Generators`, `Contracts`, `QueryRouting`, `ServiceDefaults`, `AppHost` and `Client`. Logs are `/tmp/story513-r4-{server,domainservice,live,generator,contracts,queryrouting,servicedefaults,apphost,client}-release-build.log`. Final Debug/source Server build also passed cleanly with `dotnet build tests/Hexalith.EventStore.Server.Tests/Hexalith.EventStore.Server.Tests.csproj -c Debug -p:UseHexalithProjectReferences=true -m:1`, logged in `/tmp/story513-r4-final-server-debug-build.log`.

| Executed scope | Result | Log / XML prefix |
| --- | --- | --- |
| Final Debug/source Routing, including all original 245 cases | 324 passed; 0 failures/skips/not-run | `/tmp/story513-r4-final-routing-debug` |
| Final Release affected Server, including DomainServices | 2,288 total; 2,271 passed; 17 original skips; 0 failures/not-run | `/tmp/story513-r4-server-affected` |
| Full DomainService, original wire/source/credential assertions | 1,277 total; 1,276 passed; 1 original skip; 0 failures/not-run | `/tmp/story513-r4-domainservice` |
| Owned infrastructure fake-tool controls | 6 passed; 0 failures/skips | `/tmp/story513-r4-fixture-tools` |
| Owned state/CAS/cache and slim/mTLS hosted Development drill | 3 passed; 0 failures/skips | `/tmp/story513-r4-owned-live` |
| Existing command / named projection persistence controls | 2 / 1 passed; 0 failures/skips | `/tmp/story513-r4-existing-live-{command,projection}` |
| Contracts Routing / full QueryRouting / full ServiceDefaults | 142 / 15 / 16 passed | `/tmp/story513-r4-{contracts,queryrouting,servicedefaults}` |
| AppHost topology plus original resolver configuration tests | 13 passed; 0 failures/skips | `/tmp/story513-r4-apphost` |
| Full REST generator | 135 passed; 0 failures/skips | `/tmp/story513-r4-generator` |
| Client projection / SDK header-owner controls | 746 passed + 2 original skips / 20 passed | `/tmp/story513-r4-client-{projections,headers}` |
| Broker input / actual CI Docker-selection prefix controls | 25 / 4 passed | `/tmp/story513-r4-ci-input-tests.log`, `/tmp/story513-r4-ci-docker-selection-tests.log` |

Exact test commands (each xUnit run also uses `-result-xml /tmp/story513-r4-<scope>.xml`):

```sh
dotnet tests/Hexalith.EventStore.Server.Tests/bin/Debug/net10.0/Hexalith.EventStore.Server.Tests.dll -class '*Routing.*'
dotnet tests/Hexalith.EventStore.Server.Tests/bin/Release/net10.0/Hexalith.EventStore.Server.Tests.dll -class '*Commands.*' -class '*Queries.*' -class '*Pipeline.*' -class '*Projections.*' -class '*Routing.*' -class '*DomainServices.*' -class '*Controllers.QueriesControllerTests' -class '*Actors.IdempotencyAdmissionActorTests'
dotnet tests/Hexalith.EventStore.DomainService.Tests/bin/Release/net10.0/Hexalith.EventStore.DomainService.Tests.dll
dotnet tests/Hexalith.EventStore.Server.LiveSidecar.Tests/bin/Release/net10.0/Hexalith.EventStore.Server.LiveSidecar.Tests.dll -class '*RouteCatalogFixtureToolTests'
EVENTSTORE_ROUTE_CATALOG_DAPRD=/tmp/route-catalog-ci-preparation/route-catalog-tools/daprd dotnet tests/Hexalith.EventStore.Server.LiveSidecar.Tests/bin/Release/net10.0/Hexalith.EventStore.Server.LiveSidecar.Tests.dll -class '*RouteCatalogDevelopmentLiveTests' -class '*RouteCatalogHostedLiveTests'
dotnet tests/Hexalith.EventStore.Server.LiveSidecar.Tests/bin/Release/net10.0/Hexalith.EventStore.Server.LiveSidecar.Tests.dll -class '*Commands.CommandRoutingIntegrationTests'
dotnet tests/Hexalith.EventStore.Server.LiveSidecar.Tests/bin/Release/net10.0/Hexalith.EventStore.Server.LiveSidecar.Tests.dll -method '*NormalDelivery_PersistsIndependentDetailIndexCheckpointsAndConvergedRetryLedger'
dotnet tests/Hexalith.EventStore.Contracts.Tests/bin/Release/net10.0/Hexalith.EventStore.Contracts.Tests.dll -class '*Routing.*'
dotnet tests/Hexalith.EventStore.QueryRouting.Tests/bin/Release/net10.0/Hexalith.EventStore.QueryRouting.Tests.dll
dotnet tests/Hexalith.EventStore.ServiceDefaults.Tests/bin/Release/net10.0/Hexalith.EventStore.ServiceDefaults.Tests.dll
dotnet tests/Hexalith.EventStore.AppHost.Tests/bin/Release/net10.0/Hexalith.EventStore.AppHost.Tests.dll -class '*RouteCatalogTopologyTests' -class '*Configuration.EventStoreDomainServiceConfigurationTests'
dotnet tests/Hexalith.EventStore.RestApi.Generators.Tests/bin/Release/net10.0/Hexalith.EventStore.RestApi.Generators.Tests.dll
dotnet tests/Hexalith.EventStore.Client.Tests/bin/Release/net10.0/Hexalith.EventStore.Client.Tests.dll -class '*Projections.*'
dotnet tests/Hexalith.EventStore.Client.Tests/bin/Release/net10.0/Hexalith.EventStore.Client.Tests.dll -class '*Handlers.DaprServiceInvocationHandlerTests' -class '*Registration.DaprServiceInvocationRegistrationTests'
python3 -m unittest discover -s tools/tests -p 'test_dapr_broker_qualification.py'
python3 -m unittest discover -s tools/tests -p 'test_route_catalog_ci_tools.py'
actionlint .github/workflows/integration.yml
git diff --check
bash scripts/check-committed-files.sh
```

The read-only command `dotnet run tools/routing-catalog.cs -c Debug -p:UseHexalithProjectReferences=true -- check-development . deploy/dapr/eventstore-routing-catalog.json` compiles and passes after the constructor/friend change; log `/tmp/story513-r4-catalog-construction-check.log`. Retained catalog SHA-256/root remains `c156ce0b68bab43f561805db9cd02714d123026d388f65c764d7b6fa4e3b7308`, byte-for-byte equal to HEAD; no generation command ran. The isolated daprd still matches locked SHA-256 `e988a6f53abec017fc9ebcb4f89de9bed11bce005eb49bd8e85e8d3d6adbf488`.

The ordinary AppHost baseline and final source builds passed with one MSBuild worker. Both pre-edit and post-edit Aspire start/describe/stop cycles completed; the final ordinary topology showed application resources Waiting and parameter resources Running/Healthy. Logs are `/tmp/story513-r4-aspire-{start-retry,describe,stop}.log` and `/tmp/story513-r4-final-aspire-{start,describe,stop}.log`. This is not catalog activation or production qualification. Exact owner-label inspection at the validated/pinned local Unix Docker endpoint finds no remaining catalog Redis or Sentry extraction containers (`/tmp/story513-r4-container-cleanup.log`). No shared Redis restoration was attempted; the previously disclosed fixed-key writes and unknown prior values remain unchanged. The index, retained catalog bytes, frozen intent, dependency pins and submodules are preserved.

**Remaining confirmed acceptance blocker:** An initially ordinary request can receive a native automatic redirect into a Dapr invocation URL below the guard. The owned probe `dotnet run --project /tmp/story513-r4-ordinary-redirect-probe/Probe.csproj -c Debug` reports `status=204; effects=1; authorityCalls=0; forwardedRoot=caller-root` in `/tmp/story513-r4-ordinary-redirect-probe.log`. It starts a random-loopback HTTP sink, posts to `/ordinary`, returns a 307 to `/v1.0/invoke/undeclared/method/unknown`, and proves an undeclared sink effect without authority. The probe host is disposed. The current positive ordinary-redirect tests and negative invocation-redirect tests do not cover this crossover. A redirect-aware ordinary-transport design must preserve ordinary behavior while fencing each later Dapr hop; the passing planned suites do not close this boundary. Do not mark local implementation acceptance complete on the strength of their green results.

Production profile/trust/host-observer/hosted qualification and activation, Stories 5.6/5.7, CI and parent acceptance remain open. Original B08 remains the same unverified production-owner request-to-fence correlation qualification; no production verifier is registered by default.

### Redirect-fence closure (2026-10-29)

The ordinary-to-invocation redirect counterexample above is closed on the current working tree. Ordinary requests still follow the owner's redirect policy on a separate redirect-disabled transport. A later hop that is a Dapr invoke URL, a raw actor URL, or Basic `dapr-app-id` reenters the catalog guard. Declared targets proceed; undeclared targets, raw actors, and hops before activation or while pending do not reach the sink. Turning automatic redirects off still returns the redirect and does not follow it. The primary transport's `AllowAutoRedirect` setting is left unchanged.

Current-source verification, after that fence, all passed with zero failures, skips, or not-run cases:

| Scope | Result |
| --- | --- |
| Debug/source Server `*Routing.*` | 334 passed |
| Debug/source `CatalogTransportIsolationTests` | 29 passed |
| Debug/source AppHost `*RouteCatalogTopologyTests` | 9 passed |
| Debug/source owned live `RouteCatalogDevelopmentLiveTests` and `RouteCatalogHostedLiveTests`, with `EVENTSTORE_ROUTE_CATALOG_DAPRD` pointing at the pinned local `daprd` | 3 passed |

These runs use `UseHexalithProjectReferences=true` against this working tree. They close the local matrix rows, including complete-generation commit, invalid-candidate refusal, committed-target selection, metadata stability, and persisted Dapr readback across prepare, commit, and rollback. Strict completeness certification, parent-story completion, and production readiness remain unclaimed. Development fixtures still grant no production authority.
