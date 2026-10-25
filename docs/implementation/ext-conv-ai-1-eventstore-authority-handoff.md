# EXT-CONV-AI-1 EventStore authority handoff

This delivery implements the EventStore components described by [the approved specification](../../_bmad-output/implementation-artifacts/spec-ext-conv-ai-1-eventstore-authorities.md). It does not establish an installed Conversations catalogue, an accepted successor dependency target, or Live qualification. Installed targets, public-anchor hashes, credentials, storage bindings and inventory qualification remain **unverified**. No Live owner command or cohort attempt was executed; Agents Story 8.3 and RQ-1 Level 5 remain gated.

## Implemented providers

| Component | Delivered behavior |
| --- | --- |
| `CompleteCommandSource`, `CommandSourceAuthorityBinding` | A distinct complete native prefix, bounded to 10,000 events and 16 MiB of application event bytes, including an explicit persisted head-zero incarnation. The workload assertion binds the exact command, current state, source bytes, source actor, method and enrollment epoch. |
| `AggregateActor.CommandSource.cs` | Exact enrolled streams bypass snapshots and effective-event evolution, verify the complete immutable legacy JSON prefix, and capture readable event bytes with native stored-envelope digests. The actor checks current enrollment/expiry and its local metadata/native proof again before persistence. A persistence retry captures fresh authority; normal idempotency recovery preserves the original committed command outcome. |
| `CompleteCommandSourceVerifier` and SDK intake | Local verification against an already authenticated platform workload, with no source reads or originating actor re-entry. Mandatory domains reject missing evidence. The existing `CommandStateProof` / `VerifiedEffectiveEvents` evolution rejection remains. |
| `SourcePublicationAuthority` | Concrete namespace, operation and complete-command authority. It reads the independent enrollment/journal, independently observes authenticated source prefixes and their closed projector results, and compares separate source and receiver acknowledgement lookups. Caller observations, receipt strings and proof DTOs alone grant no permission. |
| `SourcePublicationAuthorityActor` | Durable actor-state enrollment, roster/index anchors, admitted originals, exact transition recovery, immutable source progress and immutable acknowledgement chains. Journal turns perform local workload/public-key/evidence validation and actor-state I/O; source/projector/receiver I/O happens outside the journal-owner turn. |
| Authenticated namespace/index actors | Method, tenant/domain/namespace, epoch and exact request bytes are signed. Mutations also bind the expected revision in those bytes. Raw legacy calls deny under the delivered provider. Both primary owners retain prospective bytes before the independent anchor advances and reconcile only exact independently retained originals. |
| Dapr clients and transports | `SourcePublicationPrimaryClient`, `SourcePublicationAuthorityClient` and the existing namespace/index/writer adapters carry exact workload bindings. Independent original lookup does not depend on the requesting namespace/index actor or its primary state. |
| Workload binding issuer | Explicit disabled-by-default complete-command and publication digest profiles in the existing RSA workload authority. Each grant pins audience, operation, tenant/domain, source or namespace, method and epoch; only the canonical SHA-256 request digest varies. No wildcard or unbound request substitutes for these profiles. |

An exact receiver/authority successor uses `SourcePublicationAuthorityClient.RenewEnrollmentAsync` with the predecessor epoch and exact enrollment digest. The registrar must have current independently approved successor evidence. The conditional transition preserves the namespace, finite initial inventory, original journals and signal identities; competing changed successors conflict. Exact retries reuse the original successor result. Command-source enrollment can grow monotonically. After the host composes the approved successor enrollment, new registrations use the current epoch. Receiver-only rollover at the same epoch preserves original prefix recovery only while current independent source/receiver lookup still confirms the exact original receipts. Changed protected revisions, missing lookup, revocation or poison deny reuse.

An expired original complete cut never authorizes reuse. A fresh authenticated complete cut can start a new reconciliation generation while retaining old journal records and the same source publication identities. Each new proof records its independently anchored primary predecessor version, so an unused expired issuance after response loss cannot strand the primary or skip a source prefix. Primary rollback without its original pending transition cannot lower the independent anchor. A changed incarnation or same-ID publication conflicts durably quarantine the index; a poisoned predecessor is never skipped or acknowledged.

## Explicit composition

The independent authority host registers:

```csharp
services.AddEventStoreSourcePublicationAuthorityJournal(
    installedAuthorityHostBinding,
    independentlyInstalledCurrentEvidence);
```

The primary/observer composition registers:

```csharp
services.AddEventStoreSourcePublicationAuthority(
    installedAuthorityHostBinding,
    acceptedExactEnrollments,
    independentlyInstalledCurrentEvidence,
    installedProducerObserverRegistrarReaderRecoveryIssuers,
    authenticatedNativeSourceReader,
    closedPublicationProjector,
    independentSourceAndReceiverAcknowledgementObserver);
```

The receiving domain-service SDK explicitly requires complete native evidence:

```csharp
services.AddEventStoreCompleteCommandSourceVerification(
    installedPlatformWorkloadId,
    requiredDomains);
```

These examples name required installed objects; they are not a deployable installation or a set of trusted defaults. Registration validates the supplied scope/evidence/issuer inputs and installs executable seams. It does not enroll a namespace, qualify inventory, or invoke a Live owner command. The registrar separately calls `EnrollAsync` for the exact accepted enrollment, then uses the authenticated `install-namespace` operation. Initial source and command-source lists use canonical actor-ID order. Exact command sources must be enrolled before use; adding an accepted source requires a monotonic registrar successor and composing that successor in the host. Enrolled streams cannot silently fall back when current authority disappears.

The existing `AddEventStoreSourcePublication` overload remains a seam-registration API. It does not independently supply the new production authority or qualify deployment coverage.

## Required trust, storage and ACL inputs

Before any installation can be called available, its owner must supply and qualify all of these actual inputs:

1. The exact tenant, source domain, feed and installation ID. Conversations uses `conversation/approved-deletion-v1` with the exact tenant/installation agreed by its runtime composition. No caller can substitute a foreign scope.
2. An independently authenticated complete finite legacy inventory, including created Conversations with no Agent Calls, plus evidence that **every** writer registers before its first append. A maximum sequence, a projection, a receipt label or an in-memory fixture does not establish completeness. All writer paths, recovery paths and cold activations need inventory/enforcement qualification.
3. Distinct enrolled producer, observer, registrar, reader and recovery workload identities; separate source and receiver lookup permission; the exact current receiver target and delivery-authority revision. Authentication uses the existing workload issuer contract and Dapr channel protection. The receiver adapter must obtain actual authenticated durable originals and may not turn command admission or a delivery status into a receipt.
4. Public issuer/key/algorithm/audience bindings and exact expiring grants. `AllowCompleteCommandSourceDigest` and `AllowSourcePublicationDigest` default to false. Their mandatory static bindings exclude only the dynamic digest: complete commands pin tenant, domain, `command-source`, `command-source-epoch`, `command-source-method=process`; publication pins tenant, domain, `publication-scope`, `publication-method`, `publication-epoch`. In this **uninstalled** command profile, `command-source` is the case-sensitive aggregate component (at most 256 characters), jointly scoped by the separate mandatory exact tenant/domain claims. It is not the colon-joined actor ID, which can be 386 characters. The journal and primary operations have separate audiences. The journal verifier requires installed local public signing keys and refuses metadata-network verification inside its owner turn.
5. An installed `ISourcePublicationAuthorityInstallationEvidence` verifier. It must authenticate current inventory, all-writer enforcement, role/receiver enrollment, storage identity, expiry/revocation and the independent current epoch from fresh evidence. This interface intentionally has no available production default. Static configuration and test boolean substitutes do not implement that trust decision.
6. Separate governed journal storage and epoch custody, actor placement and state-component bindings, private method ACLs, and restore/rollover procedures. Register `SourcePublicationAuthorityActor` in the independent authority host, not as an accidentally co-restored primary index. A storage-name string is not proof of storage independence. Primary restoration cannot erase the journal's original transition/high-water evidence. Restoring the authority itself requires independently governed current-epoch/continuity evidence before it may issue, reuse or recover authority; co-restoring primary state and an old authority epoch does not establish nonrollback behavior.
7. Operational capacity and retention qualification. The finite limits remain fail closed: at most 1,000 roster sources, 10,000 indexed publications, 100 receipts per advance, bounded pending transitions, and an authority journal capped at 30,000 records and **32 MiB of actual canonical serialized state**, including base64 expansion and enrollment/record overhead. Every persistence path, including enrollment renewal, preflights the same bounded serialization used by confirmation before staging. Source and acknowledgement observation share a finite 30-second primary-operation budget, propagate cancellation, and use bounded provider workers; noncooperative work cannot retain the actor turn after that budget. No automatic pruning or unqualified capacity/availability claim is delivered.

## Conversations integration boundary

Conversations still needs an `IConversationCommandSourceVerifier` adapter over the SDK's authenticated `CompleteCommandSourceContext.Current`. The adapter must require that context and fold the exact complete legacy prefix; it must not accept caller-carried evolution proof fields or call back into the originating actor to establish authority. Its domain host must opt into mandatory complete-source intake and use the exact accepted platform workload binding.

Its publication composition must bind the existing closed Conversations projector and authenticated native source reader, and implement `ISourcePublicationAcknowledgementObserver` using independently authenticated source and receiver lookup. Both results must name the exact immutable entry, original receipt ID, current accepted target/continuity binding and the same positive protected deletion revision. The EventStore journal retains the actual matched receipts as an immutable predecessor chain; exact recovery is available through `LookupAsync`, including `reconciliation-head`, `dispatch-head`, `reconciliation:<receipt-id>` and `dispatch-proof:<receipt-id>`. Unknown results stop progress.

A **created zero-Agent Conversation** is different from a provisionally registered **head-zero uncreated stream**. The tests retain both in the complete namespace. The inspected Conversations `EventStoreConversationTenantCatalogue` currently returns unavailable when a replay has `IsCreated=false`; it must not silently omit that registered stream and claim a complete catalogue. That consumer integration requires owner qualification of the creation/registration window and the typed unavailable behavior. A created Conversation with no Agent Calls must remain in the denominator. This EventStore delivery neither edits the sibling repository nor proves that consumer catalogue installed or qualified.

The dedicated service Party, worker/machine binding, approval issuer, delivery transport, separate conformance receiver, independent receiver lookup installation, retained attempt journal and accepted configuration/plan/profile/anchor hashes remain external deliveries. No installed hash, target, manifest, integration date or dependency acceptance was invented or changed here.

## HTTP request-size installation requirements

The complete-source HTTP request carries event payloads in both current state and the native-source carrier. Base64 expansion means the 16 MiB application-event budget is not a 16 MiB HTTP budget. The concrete invoker/handler/SDK endpoint test measured **44,739,133 HTTP body bytes for 16,776,192 application-event bytes**; its small-event control measured 2,629 body bytes for a two-byte event. These are in-process HTTP component measurements, not installed sidecar or Kestrel qualification. Installation must measure the full approved worst-case request, including command bytes, event count and metadata, then explicitly bound every hop above that size with an accepted margin. The measured near-limit request already exceeds both defaults below.

[Dapr's request-size documentation](https://docs.dapr.io/operations/configuration/increase-request-size/) specifies a 4 MB default and the current `--max-body-size` / `dapr.io/max-body-size` settings; the old HTTP request-size setting is deprecated. [Kestrel's request-size documentation](https://learn.microsoft.com/en-us/aspnet/core/fundamentals/servers/kestrel?view=aspnetcore-10.0#maximum-request-body-size) specifies a default `MaxRequestBodySize` of 30,000,000 bytes and requires overrides before the body is read. The owner must qualify the EventStore-side and receiver-side Dapr limits, the receiving host's limit and any proxy limits against the measured request envelope. No runtime configuration, default limit, installed hash or Live probe was changed here.

## Local verification

The final serialized local verification executes the following for each `lane` in `Server DomainService ServiceDefaults Client`:

```bash
dotnet build "tests/Hexalith.EventStore.${lane}.Tests/Hexalith.EventStore.${lane}.Tests.csproj" -c Debug -p:UseHexalithProjectReferences=true -m:1
dotnet "tests/Hexalith.EventStore.${lane}.Tests/bin/Debug/net10.0/Hexalith.EventStore.${lane}.Tests.dll" -class '*CommandSource*' -class '*SourcePublication*' -class '*DomainServiceWorkloadAssertion*' -class '*DaprDomainServiceInvoker*'
```

Final coordinating-owner run on 2026-10-25, after all review fixes, serialized in the lane order above against source observation `dee8497458bfc92e094ac66fb977b2eddabd6c93` and its current dependency graph. This observation is not an accepted installed target:

| Lane | Build | Selected cases | Failures / skips |
| --- | --- | ---: | --- |
| Server | Exit 0; 0 warnings, 0 errors | 116 | 0 / 0 |
| DomainService | Exit 0; 0 warnings, 0 errors | 23 | 0 / 0 |
| ServiceDefaults | Exit 0; 0 warnings, 0 errors | 4 | 0 / 0 |
| Client | Exit 0; 0 warnings, 0 errors | 143 | 0 / 0 |

All eight commands exited 0: **286 selected cases passed**. The ServiceDefaults cases exercise actual RSA signing-to-local-validator round trips for all three binding families, including disabled/revoked grant and exact-scope negatives. Server cases exercise the concrete provider and authenticated primary actors through serialized persisted state, competing registration, command reply loss/restart and consecutive heads, independent source/receiver lookup, receipt/anchor response loss, receiver and epoch successor registration, expired-cut fresh generations including lost unmirrored issuance, primary rollback, changed predecessors and durable no-skip poison. Existing Client and workload/invoker compatibility suites also pass. Final native logs and command receipts are `/tmp/ext-conv-ai-1-root-final-nfxx9t5q/{server,domainservice,servicedefaults,client}-{build,tests}.log` and `results.json`; the commands and counts here retain the repository proof summary.

These are component tests using serialized actor-state ownership, fault injection and synthetic trusted installation/observation ports. They do not qualify a production store, workload ACL, complete inventory or Live C1–C4 installation. No broad solution test, Live owner command, dependency acceptance or installation was run.

### Focused review-fix verification

The review-fix pass builds only the Server and DomainService test projects with the same Debug/project-reference/serialized build command above. Both builds exited 0 with zero warnings and errors. It then runs only the affected classes:

```bash
dotnet tests/Hexalith.EventStore.Server.Tests/bin/Debug/net10.0/Hexalith.EventStore.Server.Tests.dll -class '*SourcePublicationAuthorityTests' -class '*AggregateActorCommandSourceTests' -class '*InteractionProtectionPersistenceTests' -class '*DaprDomainServiceInvokerTests' -class '*CommandSourceAuthorityTests'
dotnet tests/Hexalith.EventStore.DomainService.Tests/bin/Debug/net10.0/Hexalith.EventStore.DomainService.Tests.dll -class '*CompleteCommandSourceTests'
```

Results: **109 Server and 23 DomainService cases passed; zero failures or skips**. The RSA source also compiles into the already configured Server CI lane via the existing linked-source pattern, while its standalone focused project remains. The sealed CI workflow was not edited. Additional cases cover acknowledged source/roster growth, late revocation/expiry before actual save, original-body verification before administrator normalization through the real JWT/app-channel SDK endpoint, independent journal rejection of fabricated or incomplete index vectors, uncertain observations without poison, finite cancellation/timeout with released actor turns, actual serialized journal capacity, maximum joint source identities, and processor context lifetime. Logs use `/tmp/ext-conv-ai-1-review-{server,domainservice}-{build,tests}.log`. The final coordinating-owner four-lane result above supersedes these intermediate counts. All review findings were patched; none was deferred. Additional root checks cover cancellation forwarding on the current graph.


### Shared-path regression verification

The coordinator also ran the built DomainService assembly without class filters, then the shared actor and authentication classes:

```bash
dotnet tests/Hexalith.EventStore.DomainService.Tests/bin/Debug/net10.0/Hexalith.EventStore.DomainService.Tests.dll
dotnet tests/Hexalith.EventStore.Server.Tests/bin/Debug/net10.0/Hexalith.EventStore.Server.Tests.dll -class '*AggregateActor*' -class '*Administrator*' -class '*Workload*' -class '*QueryCallerAuthority*'
```

Both commands exited 0. DomainService: 1,241 total, **1,240 passed**, zero failed, one existing opt-in test skipped (`ContentPersistenceQualificationTests.PersistedBytesSurviveSidecarProcessReplacementAndRejectCorruption`). Its `EVENTSTORE_CONTENT_LIVE=1` switch was not enabled. Actor/authentication: **488 passed**, zero failures/skips. Logs and exact commands are retained in `/tmp/ext-conv-ai-1-root-final-nfxx9t5q/{domainservice,server}-regressions.log` and `regressions.json`. The focused 286-case requirement has no skips. These broader counts overlap the focused classes and are not a unique-case aggregate.
