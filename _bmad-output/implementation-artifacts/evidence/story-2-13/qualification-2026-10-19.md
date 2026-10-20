# Story 2.13 qualification continuation — 2026-10-19

This continuation starts at revision `dfe963ffd44c4df1d3b43891234e30231a9a28e8`.
The frozen spec baseline remains `f2b480b68a8bd5369b3cdf550340e0fadfb2332b`.
The historical [qualification packet](qualification.md) and its receipts are preserved.
This continuation inspects its task changes against the starting revision above;
it makes no review claim over the unrelated history since the frozen baseline.

The selected scope is the existing disposable exact-topic Development fixture and
its synthetic and real-projection proofs. Generated components remain temporary
`pubsub.redis/v1`, `state.redis/v1`, and exact-topic subscriptions owned by
`SignalRDaprDistributionFixture`; no additional deployment component is selected.
No production host, package, AppHost, or deployment transition is authorized by
this continuation, and no owner approval or exception is inferred.

Planned checks strengthen both-host denial, duplicate/reordered-hint readback,
metadata bounds, and retention of the exact component bytes and redacted logs.
The frozen spec says 2,048 **serialized** UTF-8 bytes; current AD-8 and the
validator/broadcaster count the UTF-8 bytes of **keys plus values**. Both counts
will be recorded separately. Existing 2,032-byte evidence establishes only the
latter boundary. This discrepancy needs an Architecture/contract-owner decision
before claiming the serialized ceiling is generally enforced.
For the historical ASCII map (`k00`–`k15`, 124-character values), the keys and
values total 2,032 bytes but compact JSON totals 2,129 bytes. The new positive
map uses 116-character values: 1,904 key/value bytes and 2,001 compact JSON bytes.
The positive map fits both ceilings; it does not prove that the receiver rejects
every JSON-oversized map whose keys and values fit the current AD-8 check.

## Baseline and operating envelope

`EnableKeycloak=false aspire run --apphost
src/Hexalith.EventStore.AppHost/Hexalith.EventStore.AppHost.csproj --detach
--non-interactive`, followed by `aspire wait eventstore --timeout 45 --apphost
src/Hexalith.EventStore.AppHost/Hexalith.EventStore.AppHost.csproj
--non-interactive` and `aspire describe ... --format Json`, observed EventStore,
its sidecar, sample, and sample sidecar Running/Healthy. `aspire stop --apphost
src/Hexalith.EventStore.AppHost/Hexalith.EventStore.AppHost.csproj
--non-interactive` returned exit 0 before edits. The initial default Keycloak
baseline started but its resources were still Waiting at observation; it is not
represented as a health pass. Dashboard login credentials are omitted.

This runner has SDK 10.0.402, ASP.NET/.NET runtime 10.0.13, Aspire CLI 13.6.1,
and daprd 1.18.7. The pre-existing placement/scheduler containers use
`daprio/dapr:1.18.7`; Redis is the pre-existing `redis:6` container. These are
new profile identities, not a re-use of the October 11 result. No external service
was provisioned, no shared provider fault was injected, and no sysctl was changed.
`fs.inotify.max_user_watches` was already 8388608 at entry; the older packet's
temporary setting is historical, not a setting this continuation owns.

The built dependency graphs identify Dapr.Client/Dapr.AspNetCore 1.18.10,
SignalR client/backplane 10.0.13, StackExchange.Redis 3.4.5, Aspire.Hosting 13.6.1,
and xunit.v3 4.0.2. The backend reports Redis 6.2.21, local image identity
`sha256:c35b83ce044bb6d148c484d36e059ad28e02d5714ba6731fb55b6421e2ed0ccf`;
the placement/scheduler local image identity is
`sha256:0ac4fadf0016be85065cdebab952a8350e468cf9283d26777b0307a14a10aecc`.
These local image identities are diagnostic profile bindings, not registry
publication or production-promotion evidence. The new candidate receipts do
not rerun the historical Redis-backplane baseline on these newer package pins.

The candidate keeps two real EventStore processes, two independent daprd
processes, shared app ID `eventstore`, distinct app-channel tokens, local hub
connections, and test-only exact-topic subscriptions. The application receives
no Redis backplane setting. Logical state reads/writes use Dapr; direct backend
tools and the Dapr metadata API serve setup/profile identification only.
The real-projection composition additionally starts the existing Aspire test
topology, including its EventStore/sidecar and sample domain service. Its
dedicated fixture and both additional hub hosts opt into PubSub, so actor
placement on any of the three EventStore processes cannot silently select
Direct. The two recorded hub hosts own the proof's client connections; the
ordinary Aspire regression retains its separate Direct-default fixture.

## Operation comparison and accountable gaps

The [historical matrix](qualification.md#operation-and-guarantee-comparison)
retains the three-way comparison. The following continuation explicitly assigns
the remaining operations. A passing test assertion is not an owner decision.

| Operation | Retained Redis | Dapr pub/sub candidate | Azure SignalR output binding | Gap owner and alternative |
| --- | --- | --- | --- | --- |
| Negotiation / transport | Self-hosted ASP.NET SignalR; retained baseline proof | Same self-hosted endpoints and real clients | Unrun; no service/tier/issuer profile | SignalR owner: qualify service negotiation or retain self-hosted negotiation |
| Authenticated join / targeting / rejoin | Existing hub contract and backplane | Same hub, local group targeting, fresh authorized rejoin | Documented group/user output targeting; membership unproven | Security and SignalR owners: prove membership/revocation on chosen service/profile |
| Replica fan-out | Existing direct backplane proof | Exact subscriptions plus per-sidecar consumer groups; dynamic concrete topics unresolved | Unrun two-host client proof | SignalR owner: design dynamic subscriptions or qualify another broker/topic design |
| Ordering / duplicate hints | No projection completion guarantee | Repeated/reordered hints must reconcile persisted state; no ordering/exactly-once claim | Unrun | Test and projection owners: real projection duplicate/completion and checkpoint evidence |
| Outage / bounded cancellation | Full provider outage unrun | Sidecar interruption and Dapr query cancellation only | Unrun | Operations and Test: host/provider/network faults and bounded retry/backpressure evidence |
| Freshness / completion | Signal is only a hint | Dapr/actor readback governs; no UI freshness-state proof | Unrun | UX/projection owners: canonical Stale/Degraded/Unavailable/Unknown behavior under unavailable readback |
| Tenant / group / credential denials | Existing guards retained | Both-host denials and signed provenance are test-only symmetric Development credentials | Client token issuance, group authorization, binding identity unrun | Security: production binding-capable issuer and conflicting/malformed identity conformance |
| Signal / detail / metadata | Existing wire contract retained | Synthetic scoped detail and separate real signal proof; no real projection-scoped detail | Wire/transport compatibility unrun | SignalR and Contracts owners: compose real detail and resolve serialized-versus-key/value byte ceiling |
| Logging / credential custody | Existing direct Redis configuration remains unresolved | Redacted bounded test logs; metadata-value suppression checked at Information | Dapr supports secret-store/Entra configuration; exact least-privilege profile unrun | Security and Operations: production custody, rotation, and redaction review |
| Capacity / retention / cost | Unmeasured here | Per-sidecar consumer groups and streams; throughput/retention/cardinality unmeasured | No selected service tier, quotas, or spend evidence | Story 2.16 owner supplies NFR5 primary evidence; transport owner binds supported limits |
| Rollback / deployment parity | Retained rollback target, no transition occurred | Disposable fixture teardown is not atomic runtime rollback | Unrun | Platform deployment and Test: both-host/profile compatibility and rollback before retiring Redis |
| Approved disposition | No accepted exception | No approved production profile | No approved profile or service access | Architecture, Security, Operations, Test, and transport owner: decide exact qualified scope or time-bounded exception |

Conflicting tenant-claim combinations remain **unrun**; separate tenant grants
are not treated as conflicting request scope. Neither component scopes nor the
shared app ID confer a tenant grant. No conflicting-identity conformance result
is inferred from wrong-tenant or missing-grant denials.

The [Dapr Azure SignalR documentation](https://docs.dapr.io/reference/components-reference/supported-bindings/signalr/)
documents an output `create` operation with group/user targeting and credential
configuration, including Entra ID. It does not establish the existing hub's
negotiation and membership behavior. [Microsoft's negotiation documentation](https://learn.microsoft.com/en-us/azure/azure-signalr/signalr-concept-client-negotiation)
describes trusted-server negotiation and service redirection; a compatible
binding-backed connection/membership design still needs evidence. This is a
design gap, not a claim that Azure SignalR cannot satisfy the requirement.

The [Dapr Redis component documentation](https://docs.dapr.io/reference/components-reference/supported-pubsub/setup-redis-pubsub/)
identifies consumer groups, queue/concurrency settings, timeouts and retention
options. The fixture retains its actual component bytes rather than assuming
those defaults establish a supported production envelope. [Dapr's metadata API](https://docs.dapr.io/reference/api/metadata_api/)
identifies the running runtime, components, and subscriptions; captured metadata
is deliberately limited to these value-free fields.

## Execution results

The current-source commands below invoke the built xUnit executable directly;
they do not use a Microsoft.Testing.Platform project filter. The retained
[profile identity](runs-2026-10-19/profile-identity.json) and
[baseline observation](runs-2026-10-19/baseline-resource-observation.json) provide
the value-free runtime context. The final index maps each command to its own
scenario manifest; an earlier pass is not substituted for a failed attempt.

| Check | Exact command / receipt | Result |
| --- | --- | --- |
| Focused full graph | `dotnet build tests/Hexalith.EventStore.IntegrationTests/Hexalith.EventStore.IntegrationTests.csproj -c Debug -m:1 --no-restore -p:UseHexalithProjectReferences=true -v:q`; [output](runs-2026-10-19/build-focused-full-graph.log) | Passed, 0 warnings/errors, 16.75 seconds, before the persistent-B observer additions; both unchanged product host graphs compiled. |
| Current test source | `dotnet build tests/Hexalith.EventStore.IntegrationTests/Hexalith.EventStore.IntegrationTests.csproj -c Debug -m:1 --no-restore -p:UseHexalithProjectReferences=true -p:BuildProjectReferences=false -p:UseSharedCompilation=false --disable-build-servers -v:minimal`; [output](runs-2026-10-19/build-current-source.log) | Passed, 0 warnings/errors, 3.66 seconds, against the already-built source references. |
| Synthetic distribution | `timeout 300s tests/Hexalith.EventStore.IntegrationTests/bin/Debug/net10.0/Hexalith.EventStore.IntegrationTests -class Hexalith.EventStore.IntegrationTests.ContractTests.SignalRDaprDistributionRuntimeProofTests`; [command receipt](runs-2026-10-19/synthetic-current-source-command.json) | 1 passed, 0 failed/errors, 133.829 seconds; command UTC 15:47:36.547128–15:49:50.572590. |
| Real projection composition | `timeout 420s tests/Hexalith.EventStore.IntegrationTests/bin/Debug/net10.0/Hexalith.EventStore.IntegrationTests -class Hexalith.EventStore.IntegrationTests.ContractTests.SignalRDaprProjectionDispatchProofTests`; [command receipt](runs-2026-10-19/real-projection-current-source-command.json) | 1 passed, 0 failed/errors, 30.141 seconds; command UTC 15:49:50.574223–15:50:21.017624. |
| Ordinary Direct regression | `timeout 420s tests/Hexalith.EventStore.IntegrationTests/bin/Debug/net10.0/Hexalith.EventStore.IntegrationTests -class Hexalith.EventStore.IntegrationTests.ContractTests.ValidProjectionRoundTripE2ETests`; [command receipt](runs-2026-10-19/ordinary-aspire-regression-command.json) | 1 passed, 0 failed/errors, 24.699 seconds, including collection cleanup; UTC 15:51:23.179364–15:51:48.133041. |
| Full solution | `timeout 600s dotnet build Hexalith.EventStore.slnx -c Debug -m:1 -p:UseHexalithProjectReferences=true -p:UseSharedCompilation=false --disable-build-servers -v:q`; [command receipt](runs-2026-10-19/solution-debug-build-command.json) | Passed, 0 warnings/errors, 2:20.33; UTC 15:52:28.841057–15:54:49.507338. |

The solution build regenerated `Hexalith.EventStore.dll` and
`Hexalith.EventStore.Gateway.dll` with different hashes; proof sources, the test
assembly and host dependency manifest remained unchanged. The two proof receipts
above remain valid for their recorded **pre-solution-build binaries**. A final
pair of uniquely named post-solution-build receipts binds the resulting host
binaries; the final attempt map distinguishes source identity from binary
identity rather than silently treating the earlier pass as the final artifact.

| Final built-binary confirmation | Receipt and matching manifest | Result |
| --- | --- | --- |
| Synthetic distribution after solution build | [command](runs-2026-10-19/synthetic-post-solution-build-command.json), [manifest](runs-2026-10-19/synthetic-distribution-20261019T1558084979342Z-03013efdef8e4a0698aba33b613b8207/manifest.json) | 1 passed, 0 failed/errors, 129.162 seconds; UTC 15:58:08.104497–16:00:17.629671. |
| Real projection after solution build | [command](runs-2026-10-19/real-projection-post-solution-build-command.json), [manifest](runs-2026-10-19/real-projection-distribution-20261019T1600417069432Z-6e3f2abe3ad542a7b2071db197716455/manifest.json) | 1 passed, 0 failed/errors, 36.035 seconds; UTC 16:00:17.633122–16:00:53.918456. |

These final receipts confirm unchanged sources and host/test binaries throughout
each run. Both final manifests match the binaries left by the successful
solution build. Earlier source passes, the ordinary Direct regression, and the
failed attempt retain their original identities and separate scope.

The [final attempt map](runs-2026-10-19/attempt-map-final.json) and
[continuation index](runs-2026-10-19/continuation-index.json) bind the exact source,
frozen spec intent, profile, artifacts, commands, UTC intervals, results, and
unapproved disposition. The [index checksum](runs-2026-10-19/continuation-index.sha256)
protects the retained index bytes; it is a local consistency receipt, not an
AD-12 CI seal or owner approval. `.gitattributes` preserves the generated packet
bytes across checkouts. `git diff --check` passed. No Story 2.13 host or sidecar
process remained after the final runs; no unrelated process or sysctl was changed.

The synthetic [current-source manifest](runs-2026-10-19/synthetic-distribution-20261019T1547368108854Z-a16dc3a87e974680853222d8065139d8/manifest.json)
binds independent hosts/sidecars and all four persistent A/B connection IDs.
Tenant B clients on **both** hosts first received their own signal and scoped
detail positive controls. They then received no foreign-tenant signal/detail
during A publication, repeated/reordered hints, authorized scoped rejoin, and
sidecar recovery. Both B clients held same-named tenant-B projection and scoped
groups, including the recovery projection. The observer counted two authorized
B replay hints after sidecar restart. Such duplicates are permitted hints; they
are not a second projection completion.

The same run registered the shipped wildcard on both sidecars, observed no
concrete dynamic-topic delivery during bounded windows, then delivered a valid
exact-topic control. Both hosts rejected anonymous/expired negotiation,
wrong-tenant/missing-grant joins, malformed tenant and forged scope joins, and
missing, mismatched, or expired provenance. Valid callbacks to the same groups
provided positive controls. Seventeen-entry and oversized multibyte metadata
were rejected on both hosts; retained logs contain the bounded validation and
credential reason codes. Valid detail carried 16 entries, 1,904 key/value UTF-8
bytes and 2,001 compact-JSON bytes. The synthetic logical Dapr state remained at
version 2 with unchanged ETag after repeated/reordered notices, rejoin and
recovery; unavailable-sidecar readback failed with a three-second cancellation
budget. The Information-level logs contained none of the metadata-value sentinel.

The real-projection [current-source manifest](runs-2026-10-19/real-projection-distribution-20261019T1550089359725Z-a31f454413a046d28117ea11ea81a0c5/manifest.json)
binds a unique tenant/aggregate, `IncrementCounter` completion with one event,
both A connection deliveries, and denied same-named tenant-B groups on both
hosts. Both host queries returned count 1, identical strong ETags, and
`ProjectionBacked` provenance in the header and body. The logical queries pass
through the Dapr actor boundary; the ETags are equality validators, not ordered
projection-progress tokens. Neither this run nor the ordinary regression logged
the historical checkpoint-refusal/exhaustion or writer-protocol lease-cleanup
markers. That observation is confined to these runs and is not whole-ordering,
checkpoint, or shared-runner-concurrency qualification. Local OTLP trace-export
connection-refused diagnostics remain in the real run log; telemetry delivery
and production observability were not qualified.

### Retained failed and superseded attempts

- The initial `--no-restore` source-mode build failed with CS1704 because cached
  package-mode assets also imported `Hexalith.Commons.UniqueIds`. Restoring the
  same source graph removed the mismatch; no dependency versions changed. The
  next build exposed an assertion-overload compile error in this change, which
  was corrected. Both [asset-mismatch](runs-2026-10-19/build-source-assets-mismatch.log)
  and [compile-error](runs-2026-10-19/build-initial-assertion-overload-error.log)
  outputs are retained.
- A later full focused build emitted no diagnostics and was canceled after
  3:44.73, reporting 0 warnings/errors. Only that owned build process was
  interrupted. The [canceled output](runs-2026-10-19/build-canceled-shared-server.log)
  and [4.52-second isolated fallback](runs-2026-10-19/build-focused-isolated.log)
  are separate from the final current-source compile. The stall's cause is not
  established; unrelated processes were not stopped.
- Earlier synthetic passes at 15:33 and 15:37 retain their own source hashes,
  components, logs and manifests. They predate persistent B clients on both
  hosts and the corrected replay observer and are **superseded-source** evidence,
  not substitutes for the current-source result. Their runner outputs are
  [initial](runs-2026-10-19/initial-synthetic-output.log) and
  [superseded](runs-2026-10-19/superseded-synthetic-output.log).
- The [15:45 command receipt](runs-2026-10-19/synthetic-command.json) binds the
  [failed observer output](runs-2026-10-19/synthetic-final.log), 1 failed test in
  24.462 seconds. That observer rejected all B-channel traffic at recovery
  without checking the notice's tenant. The corrected observer rejects every
  foreign tenant while counting authorized B duplicates. The passing rerun
  observed two authorized B replays and no foreign-tenant notice. The failed
  attempt has no success manifest and grants no qualification; its receipt/log
  remain unchanged. Its pre-correction source snapshot was not retained, so it
  is diagnostic evidence only.

## Disposition and Story 3.17 handoff

**Unresolved; completion and the atomic runtime transition remain gated.** The
original story stays backlog and its bytes remain unchanged; the pre-existing
sprint `in-progress` entry is also unchanged. The active spec records this
continuation without changing its frozen baseline or intent. No accepted
exception, production profile, release/promotion, NFR5 primary closure, or owner
approval is supplied by this packet.

Architecture/SignalR must resolve dynamic tenant/topic distribution and the
metadata ceiling; Security must provide the production binding-capable issuer
and conflicting-identity evidence; Operations/Test must complete real scoped
detail, host/provider/network outage, canonical freshness states, bounded
retry/backpressure and atomic transition/rollback evidence. Azure binding
negotiation/membership/replica equivalence remains unrun without an exact
approved service profile. Story 2.16 retains NFR5 capacity/cost/limit ownership.
Each remaining operation has its owner and alternative in the matrix above;
missing access or unknown suitability is not an accepted capability exception.

Story 3.17 must inventory and guard the retained
`SignalRServiceCollectionExtensions.ConfigureBackplane`, both EventStore and
Gateway package graphs/linked inputs, the `BackplaneRedisConnectionString` and
`EVENTSTORE_SIGNALR_REDIS` settings, and this unresolved evidence handoff. The
register still contains `accepted_exceptions: []`. Transport, Architecture,
Security, Operations and Test owners must separately approve an exact qualified
profile/disposition or a scoped exception before runtime policy changes. The
historical scoped proposal remains a proposal, not rollback or acceptance proof.
