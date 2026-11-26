---
title: 'Story 7.1 Delivery Contract and Poison Handling'
type: 'feature'
created: '2026-11-26'
status: 'in-review'
story_key: '7-1-delivery-contract-and-poison-handling'
route: 'dispatch'
review_loop_iteration: 0
baseline_commit: 'ed66aef9516a3426a2a66e95c343be2a0d6284dd'
context:
  - '{project-root}/_bmad-output/planning-artifacts/epics.md'
  - '{project-root}/_bmad-output/planning-artifacts/architecture.md'
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem.** Subscriber redelivery can repeat handler effects, and rejection or exhausted retries can acknowledge a delivery without preserving recoverable evidence.

**Approach.** Enforce Story 7.1 end to end with scoped marker admission, durable capture before acknowledgement, and authorized recovery evidence. Cover FR34-C1 and NFR6, with supporting NFR2, NFR5, and NFR16.

## Boundaries & Constraints

**Always.** Delivery is at least once and unordered. CloudEvent `id` equals persisted `MessageId`. External deduplication uses the received `(source, id)` per subscription. EventStore uses persisted `MessageId` within the admitted subscription. Sequence is aggregate-local. Operations owns poison retention and replay under AD-31. Keep producer drain, immutable events, and existing projection sequence guards intact.

**Never.** Expose payloads, protected bytes, credentials, claims, raw errors, or hidden tenant evidence. Release ownership on elapsed time or unknown effects alone. Enable production recovery without AD-31 gates, including durable audit, qualified Platform topology, and immutable release identity. This scope includes module code, contracts, docs and isolated verification. Production rollout and Admin UI are outside scope.

## I/O & Edge-Case Matrix

| Given | When | Then | Refusal |
|---|---|---|---|
| Completed marker | Exact redelivery | Acknowledge with zero effects | Conflicting content stays retryable or enters proven poison capture |
| Owned or ambiguous marker | Duplicate, cancellation, handler fault, lost write, or restart | Confirmed no-effect may release by fence. Proven dispatch reconciles to completion | Unknown effect stays pending or durably quarantined. No redispatch without proof |
| Development marker store | Capacity, retention or concurrent acquisition is reached | Compact eligible entries deterministically and enforce count | Retry under pressure while active and ambiguous entries are protected |
| Terminal rejection or configured attempt/age exhaustion | Capture or redelivery runs | Ack after persisted, scope-and-content-matched capture. Reuse one logical record | Failure, timeout, conflict and unknown remain retryable unless durable quarantine is proven |
| Visible poison record | Authorized list/count or audited retry, skip, archive | Scoped metadata, age and opaque cursor. Stable action identity and persisted outcome | Deny before lookup. Unavailable evidence stays unavailable. Accepted is pending |

</frozen-after-approval>

## Code Map

- Client `Subscriptions/{EventStoreDomainEventProcessor,EventStoreDomainEventsOptions,*MarkerStore,EventStorePayloadFingerprint}.cs` owns dispatch, subscription scope and fenced marker admission. Legacy rows without content proof remain retryable.
- `src/Hexalith.EventStore.DomainService/EventStoreDomainEventsEndpointExtensions.cs` binds the generic CloudEvent callback. `DomainRouteCatalogSidecarMetadata.cs` checks subscription metadata. Preserve channel and catalog admission.
- Operations `Endpoints/DeadLetterOperationsEndpointExtensions.cs` acknowledges proven capture through the actor's item and index transaction. The Admin controller and Dapr adapters expose scoped evidence using Client `IQueryCursorCodec` and `QueryCursorScope`.
- `src/Hexalith.EventStore.Server/Events/EventPublisher.cs` already sets stable source and ID. Named projection live tests exercise dispatcher, persist and checkpoint seams.

## Tasks & Acceptance

**Execution**

- [x] `src/Hexalith.EventStore.Client/Subscriptions/` Add content-bound admission with qualified create-if-absent, fenced transitions and strong readback. Reconcile legacy identity from authoritative evidence or refuse ambiguity. Bound development retention and capacity.
- [x] `src/Hexalith.EventStore.Operations/` Persist attempt and age evidence and idempotent poison capture with scoped readback. Retain safe message/correlation/causation IDs, tenant/domain/aggregate, sequence, original time/topic, attempts, first/last failure and bounded reason/retryability.
- [x] `src/Hexalith.EventStore.DomainService/EventStoreDomainEventsEndpointExtensions.cs` Bind capture-before-ack to validated delivery and configured exhaustion. Require explicit catalog policy for skipped outcomes. Keep upcast and handler semantics on replay.
- [x] `src/Hexalith.EventStore.Admin.Server/Controllers/AdminDeadLettersController.cs` Extend DTOs compatibly. Align Operations adapters with authorized counts, oldest age, scoped cursors and idempotent audited action status. Reconcile retry via normal admission. Skip/archive preserve the source event.
- [x] `deploy/dapr/` and `src/Hexalith.EventStore.AppHost/DaprComponents/` Align declared topics, credentials, retry, retention and catalog bindings. Enforce application age limits without broker expiry that can drop unretained events.
- [x] `docs/concepts/event-envelope.md`, `docs/reference/stream-replay-api.md` and `docs/guides/{dapr-component-reference,dapr-faq,disaster-recovery}.md` Align public identity, bounded marker retention, acknowledgement and recovery guidance. Supply Story 5.9's received identity example.
- [ ] `tests/Hexalith.EventStore.IntegrationTests/ContractTests/PubSubDeliveryProofTests.cs` Extend broker evidence with durable effects, markers, poison transfer and restart readback. Extend focused Client, DomainService, Server, Operations, Admin and structured topology tests for the matrix.

**Acceptance criteria**

- Given two aggregates with matching correlation and sequence and distinct persisted IDs, when both CloudEvents and one repeat reach the subscriber, then received IDs match persisted IDs and exactly two effects persist.
- Given duplicate, late, gapped or conflicting delivery, when the production handler/dispatcher runs, then persisted effects, stream head, counts and checkpoints obey their scoped single-delivery baseline.
- Given the fault and authorization matrix, when the exact broker and state profile run, then persisted readback proves acknowledgement, recovery and tenant isolation with no loss or duplicated effect.

## Implementation Notes

- Added content-bound marker fences and bounded development retention. Historical marker rows without content proof fail closed. The Dapr qualification probe distinguishes an explicit first-write rejection, including the observed Redis 1.19.1 response, from a transport or storage failure.
- Added an isolated Operations transport, scope-bound capture and pagination, and stable recovery action evidence. The live Operations smoke exercised the HTTP endpoint and durable actor index. Its duplicate observation left the attempt budget unchanged, the next failure retained one record, and restarting the host and sidecar preserved exact redelivery and conflict outcomes.
- Production callback and recovery wiring remain gated by the exact hosted broker and state profile, catalog and credential activation, immutable release identity, and Story 7.3 audit. The existing AppHost broker fixture halted during readiness with missing route-catalog configuration and inactive projection writer protocol. The isolated subscriber broker smoke verifies the identity example and persisted effects across restart. The combined Platform delivery, poison-transfer, authorization, and recovery matrix remains open.
- The callback verifies the admitted CloudEvent component, topic, type, source, and persisted ID before dispatch or poison observation. The explicit skip policy must agree with the committed subscription catalog. A replay uses the same processor admission and evolution path. Released durable markers remain bound to their original fingerprint, with a new fence for an exact retry.
- Operations reports unavailable indexed evidence without returning a partial count or exposing the underlying error. Resubmitting the same pending retry rearms a failed scheduler registration. A different action cannot replace one of the items while its previous action is pending.
- Dapr transport retries use exponential backoff with no finite inbound exhaustion. EventStore applies the separate attempt and age limits and acknowledges after handling or verified capture. This avoids Dapr 1.19.1's final broker disposition for bounded inbound exhaustion without a dead-letter destination.
- Pub/sub component templates in the AppHost, `deploy/dapr` and samples no longer declare the `deadLetterTopic` key that no supported component reads. Their comments state that broker dead-letter features are not poison evidence. A structured topology test rejects broker expiry, overflow, delivery-count dead-lettering and component or subscription dead-letter routes. Production Operations subscription, ACL, credential and catalog activation stay withheld under AD-31.
- Review pass 1 found that Dapr's CloudEvents middleware never forwards `pubsubname`, so every SDK callback delivery that checks CloudEvent metadata was refused with 503. `UseEventStoreDomainService()` now copies the structured `pubsubname` into the forwarded header and replaces any caller-supplied value. A custom pipeline must forward it itself.
- Callback refusals now log a bounded reason and the admitted `MessageId`. Operations counts each refused observation by mismatch category. Admin calls without an action ID get a fresh ID per call, so a retry after a failed campaign starts a new action.
- The Admin backend contract is covered for summary count and oldest age, single-grant scoping, denial before lookup, unavailable evidence, stable action identity, persisted pending, completed and failed states, and legacy DTO payloads. Operations action evidence records the authenticated subject; the AD-29 mutation audit remains Story 7.3's gate.

## Spec Change Log

## Review Triage Log

Review pass 1 (2026-11-26) covered `5bb02b913`..worktree: the Story 7.1 commit `a40242b3d` plus follow-up edits, excluding three unrelated commits after the frontmatter baseline.

| ID | Finding | Verdict | Route | Evidence |
|---|---|---|---|---|
| BH-1 | Handler fault leaves the marker stuck; operator retry cannot recover | false | rejected | Frozen row 2 keeps unknown effects pending or quarantined with no redispatch without proof; a failed retry persists `failed` and skip/archive remain. |
| BH-2 | Derived action identity blocks a re-retry after failure | medium | patch P2 | `ActionOperationId` hashes tenant/actor/operation/messages when HTTP sets `RequestedBy`; Admin overloads without `actionId` therefore get the stored `failed` result. |
| BH-3 | Non-Development non-durable store refuses silently | medium | patch P1 | `HandleDeliveryAsync` returns 500 with no signal; the refusal is intended, its silence is not. |
| BH-4 | No production sink, so terminal rejections redeliver forever | false | rejected | Frozen row 4 keeps failures retryable unless durable quarantine is proven; production capture is withheld by the AD-31 Never clause. |
| BH-5 | Callback refusals have no diagnostics | medium | patch P1 | The endpoint has no logger; operators see bare 500/503. |
| BH-6 | Cross-process setting mismatch fails closed silently | medium | patch P1 | `ObserveSubscriberDeliveryAsync` returns `Conflict` for route/topic/app/attempt-limit mismatch with no reason. |
| BH-7 | Fingerprint includes untrusted envelope fields | false | rejected | The processor refuses any envelope carrying `Effective*`, `RegistryFingerprint` or `IsAdapted` before admission, so such a redelivery cannot succeed under any fingerprint. |
| BH-8 | Attempt budget counts in-flight duplicates; ambiguous dev markers stay | low | rejected | Reaching ten redeliveries during one handler is rare and yields a record retried as `Duplicate`; telling in-flight from orphaned needs a lease TTL the intent forbids. |
| BH-9a | Capability mismatch consumes the attempt budget during rollout | false | rejected | Configured attempt exhaustion then capture is frozen row 4; without a sink delivery keeps retrying. |
| BH-9b | `event-versioning.md` advises `maxRetries` plus a dead-letter topic | medium | patch P5 | `event-versioning.md:86-88` contradicts the no-route policy and can move deliveries outside Operations capture. |
| BH-10a | Stale subscriber dead-letter route docs outside the spec list | medium | patch P5 | `configuration-reference.md:78`, `deployment-azure-container-apps.md:371` and `drain-failure-reason-codes.md:17` still describe subscriber routes; producer drain references stay valid. |
| BH-10b | New settings undocumented | low | patch P5 | No `EventStore:SubscriberDelivery:*` or Operations isolated-recovery entries exist in `configuration-reference.md`. |
| BH-10c | Qualification `subscriptions.yaml` declares `deadLetterTopic` | maybe-false | defer D3 | Pre-existing broker-qualification profile; harm depends on its resiliency policy. |
| BH-10d | Topology test skips the bicep sample | low | rejected | The bicep key was removed; a non-YAML parser for one sample is more than a direct correction. |
| BH-11 | Raw capture and unretainable deliveries retry forever | false | rejected | Acknowledging unretained deliveries is the problem statement; frozen row 4 keeps them retryable. |
| BH-12 | Existing Operations recovery switched off | false | rejected | Never clause: no production recovery without AD-31; retained records stay retained. |
| BH-13a | One unavailable record makes list/count 503 for every tenant | false | rejected | A missing item has no knowable tenant and frozen row 5 keeps unavailable evidence unavailable; a partial count would violate it. |
| BH-13b | Every list/count call scans all records | maybe-false | defer D2 | Would be medium for a polled count; needs measurement. |
| BH-14a | Detailed count lacks its `ProducesResponseType` | low | patch P6 | Direct attribute correction. |
| BH-14b | `CancellationToken` is not the last parameter | false | rejected | CA1068 is not enforced (warnings-as-errors build is clean); no named harm. |
| BH-14c | No read-only action status endpoint | false | rejected | Resubmitting the same pending action deliberately rearms the reminder; 202 reports persisted state. |
| BH-14d | Admin `ActionId` validation is looser than Operations | low | patch P7 | Admin forwards values Operations rejects; direct alignment. |
| BH-14e | ReadOnly count access undocumented | low | patch P5 | Docs omit the role change. |
| BH-15 | Ephemeral Data Protection keys invalidate cursors after restart | low | rejected | A bounded 400 on the next page in a single-instance profile; a persisted ring needs cross-assembly registration. |
| BH-16a | No test pins the observed Redis 1.19.1 rejection | medium | patch P8 | Only the negative path is tested; a parser regression would fail every durable subscriber closed unnoticed. |
| BH-16b | Probe depends on exact Dapr text and is not cached on failure | low | rejected | Fail-closed qualification is intended and Dapr is pinned; caching adds state. |
| BH-17 | Hot path adds about ten round trips without throughput evidence | maybe-false | defer D1 | Would be medium against NFR2; needs qualified-broker latency. |
| BH-18 | Spec status ahead of evidence | n/a | rejected | The fix edits this build's spec. |
| BH-19a | Proof component hardcodes `localhost:6379` | false | rejected | Matches `statestore.yaml`, which documents why the env form fails. |
| BH-19b | Whole options bound from configuration | false | rejected | Configurable topic and route are intended; markers are per subscription. |
| BH-19c | Validation error does not name the setting | low | patch P9 | Direct message correction. |
| BH-19d | Stray blank lines before closing braces | low | patch P10 | Cosmetic deletion. |
| EC-1 | Orphaned `InProgress` marker has no TTL or takeover | false | rejected | Never clause forbids releasing ownership on elapsed time alone. |
| EC-2 | `ReleaseFenceAsync` failure is swallowed | low | rejected | Pre-existing; needs a store fault exactly at release, and propagation would not unstick the marker. |
| EC-3 | Production host without a sink returns 503 forever | false | rejected | As BH-4. |
| EC-4 | Remark says plain `UseCloudEvents()` suffices | medium | patch P5 | Non-Development delivery needs forwarded `cloudevent.*` headers that only `UseEventStoreDomainService()` configures. |
| EC-5 | Route without a leading slash conflicts with Operations | low | patch P3 | The delivery carries the raw route while Operations compares `/` plus the method; skip admission already trims it. |
| EC-6 | Subscriber and Operations attempt limits differ | medium | patch P1 | Fail-closed cross-check is intended; silence is not. |
| EC-7 | Unretainable subscriber capture returns `Conflict` | false | rejected | As BH-11. |
| EC-8 | Pre-check observation over `MaxBodyBytes + 8192` throws | low | rejected | Isolated Development only, for payloads of roughly 780 KB or more; a fingerprint-only observation contract belongs to the AD-31 production sink. |
| EC-9 | Re-retry without `ActionId` returns the cached failure | medium | patch P2 | As BH-2. |
| EC-10 | Unattributed direct-actor action rewrites a pending item | low | rejected | HTTP always sets `RequestedBy`; only the legacy direct actor path can mix, and markers deduplicate replays. |
| EC-11 | Capacity fills with ambiguous markers | false | rejected | Frozen row 3 protects active and ambiguous entries and retries under pressure. |
| EC-12 | Pre-v2 legacy marker stays a content conflict | false | rejected | Code Map: legacy rows without content proof remain retryable. |
| EC-13 | Scope change hides earlier markers | false | rejected | Always clause: deduplication is per admitted subscription. |
| EC-14 | Expired delivery during a concurrent attempt is captured as age exceeded | low | patch P4 | Expiry overrides `RetryableInProgress` and `RetryableContentConflict`, recording misleading poison evidence; direct condition fix. |
| EC-15 | Attempt evidence stays after success | low | rejected | Small records in the isolated profile; removal needs a success call on the public sink API, and retention is Operations-owned under AD-31. |
| EC-16 | Persisted `ReplayRequested` items exhaust while recovery is off | low | rejected | Exhausted items stay retryable once recovery is enabled; a drain guard adds a branch for legacy rows. |
| EC-17 | Permanent capture outcomes now return 503 | false | rejected | As BH-11. |
| EC-18 | One missing item makes list/count 503 | false | rejected | As BH-13a. |
| EC-19 | Retry cannot reconcile an orphaned fence | false | rejected | As BH-1. |
| EC-20 | Configured exhaustion has no production effect | false | rejected | As BH-4. |
| EC-21 | Retention bound covers only completed markers | false | rejected | As EC-11. |
| VG-1 | Forwarded CloudEvent headers never produced by a real pipeline in tests | medium | patch P11 | Pre-verified gap. |
| VG-2 | Non-Development durable-admission gate untested | medium | patch P12 | Pre-verified gap. |
| VG-3 | Delivery-age dispatch guard hidden by the fixture | medium | patch P13 | Pre-verified gap. |
| VG-4 | Replay handshake untested on both sides | medium | patch P14 | Pre-verified gap. |
| VG-5 | Attributed exhaustion, `failed` and `action-pending` untested | medium | patch P15 | Pre-verified gap. |
| VG-6 | Returned list cursor never sent back | medium | patch P16 | Pre-verified gap. |
| VG-7 | Development sink to Operations wire contract untested | medium | patch P17 | Pre-verified gap. |
| VG-8 | `EventStore:SubscriberDelivery` binding untested | medium | patch P18 | Pre-verified gap. |
| VG-9 | Pre-processing retained check untested | medium | patch P19 | Pre-verified gap. |
| VG-O1 | Re-retry after a failed retry cannot start a new replay | medium | patch P2 | As BH-2. |
| VG-O2 | Some deliveries can never be acknowledged outside Development | false | rejected | As BH-4. |
| VG-O3 | Raw capture route loops on unrecoverable inputs | false | rejected | As BH-11. |

## Verification

- Run affected test projects individually, including AppHost topology tests. Run the focused live-sidecar marker and named projection lanes and the broker contract lane with cleanup. Run `dotnet build Hexalith.EventStore.slnx -c Release`.
- Story 3.21's hosted broker admission and Story 7.3 audit remain open. Keep production closure pending until the exact Platform profile and AD-31 evidence pass.
- `dotnet build Hexalith.EventStore.slnx -c Release -m:3` passed in a disposable standard build layout with zero warnings and errors. The final run included the Redis probe fix and used the worktree over `df6a96ab2a5bae24fe5951eee5429bda223cc675`. The shared artifacts layout collided on two projects named `Hexalith.EventStore.Sample.Tests`.
- After the final broker assertion edit, the Release build of `tests/Hexalith.EventStore.IntegrationTests/Hexalith.EventStore.IntegrationTests.csproj` passed with zero warnings and errors.
- Focused runs passed 75 Client subscription, 22 DomainService, 116 Operations, 96 Server, 14 AppHost topology, and three live-sidecar marker/projection tests. Full Admin Server passed 839 with 13 existing skips. The broader Server run reported a published Tenants fixture SHA mismatch. The broader AppHost run stalled and was stopped.
- The disposable Redis/Dapr broker check received the documented example source and both IDs, verified two total effects after an exact redelivery, and preserved them across subscriber restart. The disposable Operations check verified the persisted actor index, attempt budget, restart reconciliation and conflicting-content refusal.
- Follow-up checks passed 75 Client subscription, 26 DomainService callback, 120 Operations, 67 focused Admin, 22 Server configuration and callback, and nine AppHost catalog topology cases. The full Admin Server run passed 840 with 13 existing skips. The private Redis live-sidecar run passed released-fence reconciliation, duplicate and out-of-order named projection delivery, and projection gap closure with persisted checkpoint readback.
- The private rerun of `dotnet tests/Hexalith.EventStore.IntegrationTests/bin/Debug/net11.0/Hexalith.EventStore.IntegrationTests.dll -class '*PubSubDeliveryProofTests'` exited 1. All three tests failed at fixture initialization with readiness HTTP 503, route catalog `configuration-missing`, and a projection writer-protocol activation timeout. The job removed all three of its containers. The broker matrix remains open.
- Follow-up after the template and Admin changes: full Admin Server passed 873 with 13 existing skips, Admin Abstractions passed 513, Admin UI dead-letter passed 69, and Server Configuration, DaprComponents and Security passed 1,403 of 1,404. The one failure, `SecretsProtectionTests.TrackedReusableContent_DoesNotContainUsableSecrets`, reports the untouched `tests/Hexalith.EventStore.DomainService.Tests/DurableTasks/DurableTaskStepLookupFixture.cs:42`. Release builds of the three changed test projects passed with zero warnings.
- The broker lane rerun failed all three tests at the same fixture initialization: readiness 503 with route catalog "No activated catalog agrees with this host's profile, inventory, authentication and topology" and projection writer protocol v2 inactive. No lane processes remained afterward.
- Review pass 1 follow-up, on the tree with concurrent commits `528696d93`, `ed058ec8a` and `8a3536ba5` fast-forwarded in: Client passed 2,490 of 2,492. Its two `DevelopmentRouteCatalogBuilderTests` failures compare the generated development catalog with the committed fixture built against released owners; this story does not change catalog bytes. DomainService passed 1,565 (9 existing skips), Operations 132, Admin Server 881 (13 existing skips), Admin Abstractions 513 and AppHost 409. Server Configuration, DaprComponents and Security passed 1,403 of 1,404; the remaining failure is the existing `SecretsProtectionTests` finding in `DurableTaskStepLookupFixture.cs:42` from `a11d584a5`. `dotnet build Hexalith.EventStore.slnx -c Release -m:3` passed with zero warnings and errors. The live-sidecar marker and projection lanes were not rerun because this pass changed no code they exercise; the broker lane remains blocked at fixture readiness.
- `python3 -B tools/validate-phase-4-story-completion.py --spec _bmad-output/implementation-artifacts/spec-7-1-delivery-contract-and-poison-handling.md --target spec --write` refused on 2026-11-26 with `ADAPTER_UNAVAILABLE` for NFR2 and NFR16 and requires a fresh authenticated approval and seal, so the spec stays `in-review`.
- The read-only `python3 -B tools/validate-phase-4-story-completion.py --spec _bmad-output/implementation-artifacts/spec-7-1-delivery-contract-and-poison-handling.md --target spec` reported `SOURCE_MISMATCH` because of concurrent Story 3.21 edits to the planning epics. Running the same diagnostic with the committed planning sources in a disposable copy reported `ADAPTER_UNAVAILABLE` for NFR2 and NFR16 and required a fresh authenticated approval and seal.
