# Story 2.13 qualification continuation — 2026-10-20

Run-start revision: `b16be63521e8b784fc68828e0267f892b8e45924`. The frozen
spec baseline remains `f2b480b68a8bd5369b3cdf550340e0fadfb2332b`. Historical
[October 11](qualification.md) and [October 19](qualification-2026-10-19.md)
packets remain unchanged. This is local qualification evidence, with no AD-12
CI seal, accepted exception, approved replacement profile, or NFR5 closure.

## User decision and implementation

The user directed: **“Enforce the serialized-byte limit in the approved story
spec.”** This resolves the prior key/value-versus-serialized ambiguity without
changing frozen intent. AD-8 now states the same rule. Its
[impact record](runs-2026-10-20/architecture-ad26-impact.json) confirms that the
AD-26 ratification subject is unchanged at
`efd33e1ee81d2b988ae890400a2c3f6bb09a4a36ab884c812958fbc57a8b0a0e`.
This contract instruction supplies no transport, issuer, exception, or
production approval.

`ProjectionChangedMetadataLimits` in Contracts owns the 16-entry/2,048-byte
ceilings. Size means the UTF-8 length of the compact default System.Text.Json
metadata object, including braces, quotes, colons, commas, escaped keys and
escaped values. This canonical representation is the validation rule; it does
not count arbitrary whitespace in an incoming request. Producer and broadcaster
clip in ordinal key order, while the callback rejects oversized input before
provenance/ETag/broadcast work. Both Direct and PubSub producers use the same
rule. Options may lower the ceilings; 2 bytes are required for the empty object.
Signals remain hints and the existing signal/detail signatures are unchanged.

The earlier behavior accepted a map with 16 entries, 2,032 key/value UTF-8 bytes
and **2,129 serialized bytes**. The independently retained
[pre-fix run](runs-2026-10-20/replica-restart.json) observed that gap while also
passing replica restart/rejoin/readback assertions (1 test, 146.283 seconds).
Its success means the characterization assertions passed; the receipt explicitly
records `SerializedMetadataCeilingQualified: false`. Its exact source/spec bytes
are retained in [the snapshot map](runs-2026-10-20/pre-fix-source-snapshot.json).
It is superseded evidence for the metadata contract.

## Selected profile and paths

Before implementation, the selected fixture paths were the existing
`SignalRDaprDistributionFixture.cs`, `SignalRDaprDistributionNode.cs`, and
`SignalRDaprDistributionRuntimeProofTests.cs`. The user clarification additionally
required the Contracts helper, notifier/options, callback validator, broadcaster,
and their focused tests. The Development-only proof producer shares that rule so
its reported metadata count describes the actual bounded broadcast. Touched
options, enums, validators, and proof records each have their own C# file.

Generated components remain temporary `pubsub.yaml`, `statestore.yaml`, and
exact-topic subscription YAML owned by the fixture. No additional deployment
component was selected, and no backplane package, transport selection, AppHost,
or deployment subscription changed.

The [profile](runs-2026-10-20/profile-identity.json) records SDK 10.0.402,
ASP.NET/.NET 10.0.13, Aspire CLI/Hosting 13.6.1, daprd 1.18.7, Dapr CLI 1.18.2,
Dapr .NET 1.18.10, SignalR client/backplane 10.0.13, StackExchange.Redis 3.4.5,
and xUnit v3 4.0.2. The existing Redis container reports 6.2.21 from `redis:6`;
local image IDs and placement/scheduler 1.18.7 identities are retained as
physical profile diagnostics. They are not production image authority.

Each candidate run uses two real product hosts and independent sidecars, shared
app ID `eventstore`, separate app-channel tokens, Redis Streams `pubsub.redis/v1`
with `{uuid}` consumer groups, and `state.redis/v1`. Application processes receive
no Redis backplane setting. Real clients identify their owning host. Publication
and logical readback use Dapr; physical provider inspection identifies the test
profile only. Development symmetric provenance is not a production issuer.

The baseline used `EnableKeycloak=false aspire run --apphost
src/Hexalith.EventStore.AppHost/Hexalith.EventStore.AppHost.csproj --detach
--non-interactive`, then `aspire wait eventstore --timeout 45` and `aspire describe
--format Json` with the same explicit AppHost. EventStore reached healthy;
`aspire stop` succeeded before code edits. Retained
[resource observations](runs-2026-10-20/baseline-describe.log) omit environment,
arguments, and dashboard credentials. No external service was provisioned and
no sysctl was changed.

## Verification and retained identities

The [packet index](runs-2026-10-20/index.json) binds commands, outputs, source,
spec, profile, components, redacted logs, runtime metadata, process/connection
identities, observed state versions and ETags, limitations, and disposition.
Each final command retains exact source/spec bytes; each scenario retains its
component and source bytes. Binary hashes are measured before and after tests.
These are local consistency bindings, not owner attestations or a CI seal.

<!-- RESULTS -->
| Check | Exact command receipt | Result / command UTC interval |
| --- | --- | --- |
| Full Debug source graph | [final-graph-build.json](runs-2026-10-20/final-graph-build.json) | Build passed, 0 warnings/errors; `2026-10-20T02:52:31.914648+00:00`–`2026-10-20T02:54:59.855623+00:00` |
| Focused Server build after audit | [post-audit-server-build.json](runs-2026-10-20/post-audit-server-build.json) | Build passed, 0 warnings/errors; `2026-10-20T02:59:49.439625+00:00`–`2026-10-20T03:00:34.711848+00:00` |
| Focused Integration build after audit | [post-audit-integration-build.json](runs-2026-10-20/post-audit-integration-build.json) | Build passed, 0 warnings/errors; `2026-10-20T03:00:39.566544+00:00`–`2026-10-20T03:00:43.966733+00:00` |
| Metadata and producer regression | [post-audit-metadata-tests.json](runs-2026-10-20/post-audit-metadata-tests.json) | 93 passed, 0 errors, 0 failed; 0.588 s; `2026-10-20T03:01:17.013243+00:00`–`2026-10-20T03:01:18.121478+00:00` |
| Two-host/sidecar runtime class | [retry-distribution-tests.json](runs-2026-10-20/retry-distribution-tests.json) | 2 passed, 0 errors, 0 failed; 292.087 s; `2026-10-20T03:05:33.177926+00:00`–`2026-10-20T03:10:25.520855+00:00` |
| Real projection composition | [final-real-projection.json](runs-2026-10-20/final-real-projection.json) | 1 passed, 0 errors, 0 failed; 40.974 s; `2026-10-20T03:10:25.679858+00:00`–`2026-10-20T03:11:06.947457+00:00` |
| Ordinary Direct regression | [final-ordinary-regression.json](runs-2026-10-20/final-ordinary-regression.json) | 1 passed, 0 errors, 0 failed; 30.446 s; `2026-10-20T03:11:07.193314+00:00`–`2026-10-20T03:11:37.909672+00:00` |
| Retained Redis baseline | [post-audit-redis-baseline.json](runs-2026-10-20/post-audit-redis-baseline.json) | 1 passed, 0 errors, 0 failed; 182.008 s; `2026-10-20T03:11:38.154013+00:00`–`2026-10-20T03:14:40.391436+00:00` |
<!-- END RESULTS -->

The initial `--no-restore` source-mode build failed with CS1704 because cached
package-mode assets imported a second Hexalith.Commons.UniqueIds assembly.
[Restoring the pinned source graph](runs-2026-10-20/focused-source-restore.json)
succeeded without a dependency change, and the
[restored focused build](runs-2026-10-20/focused-restored-build.json) passed with
0 warnings/errors. Failed and superseded attempts retain separate identities;
they are not substituted for final-source results.

The first final-source distribution attempt ran the strengthened restart scenario
successfully, but its second fixture failed before health because daprd could
not bind local gRPC port 39441 (`address already in use`). The
[failed receipt](runs-2026-10-20/post-audit-distribution-tests.json) and
[environment classification](runs-2026-10-20/distribution-startup-failure-classification.json)
remain separate. A fresh fixture with new ports reran the complete class with
unchanged sources and binaries; it passed both scenarios in 292.087 seconds.
No unrelated process was stopped to recover the port.

Neither the final real-projection run nor the ordinary Direct regression logged
the historical checkpoint-refusal/exhaustion or writer-protocol lease-cleanup
markers. That is confined to these runs and supplies no whole-ordering claim.

The retained Redis harness's [generated evidence](runs-2026-10-20/redis-baseline-evidence-2026-10-20-031440Z.md)
reports cross-host signal/scoped-detail delivery, clipped metadata, a disabled
backplane control, and its established unreachable-Redis fail-open behavior.
Its legacy command template and abbreviated Git display remain unchanged;
the command receipt above supplies the actual executable invocation, full
revision, final assembly hashes, and exact input bytes. This baseline is
transport evidence and supplies no Dapr-only production qualification.

The [closing observation](runs-2026-10-20/closing-observations.json) checks
retained process identities after disposal, temporary component directories,
unchanged final code/assembly bytes, and the existing inotify limit. No unrelated
process or external Redis container was stopped. The local verifier is
`python3 _bmad-output/implementation-artifacts/evidence/story-2-13/runs-2026-10-20/verify-packet.py`.
It checks retained bytes and run bindings; it supplies no owner or CI authority.

## Scenario meaning and remaining gates

The new replica scenario stops both B processes and observes its old connection
close. Host access fails under a three-second cancellation budget. A writes
synthetic state version 2 while B misses a hint. After restart with new process
identities, B reads version 2 through Dapr with a changed ETag. A replacement
connection inherits no groups, a wrong-tenant identity cannot rejoin, and an
authorized identity explicitly rejoins scoped groups. A previously unused topic
provides fresh signal/detail controls, excluding replayed outage notices. Raw
JSON-overhead, backslash, and non-ASCII oversize maps are denied on both hosts;
an exact 2,048-byte map remains deliverable. Each host must record the same
three distinct rejected CloudEvent IDs for that unique topic; repeated log
wrappers and retries count once. This proves all three malformed callbacks were
processed and rejected, beyond the bounded absence of a client notice.
Replayed hints leave persisted
state and its ETag unchanged. This is synthetic-state recovery evidence, not a
real projection completion or canonical UI freshness-state proof.

The existing synthetic scenario supplies same-named A/B projection groups,
persistent B clients on both hosts, authorized B controls, denied foreign-tenant
signal/detail reception, signed-provenance denials, repeated/reordered notices,
reauthorized reconnect, sidecar interruption, cancellation, and Information-level
metadata-value suppression. Its registered shipped wildcard still fails to
deliver concrete dynamic topics beside a passing exact-topic control.

The separate real-projection composition uses `IncrementCounter`, requires
projection-backed persisted readback on both host query endpoints, compares
strong ETags for equality, and checks tenant-B denials on both hosts. Detail
in the synthetic proof is separate from that real projection signal: no real
projection-triggered scoped-detail claim is made.

The three-way [operation matrix](qualification-2026-10-19.md#operation-comparison-and-accountable-gaps)
continues to apply, with the serialized metadata and replica restart rows
advanced by this packet. Remaining operations stay explicitly unresolved:

| Operation/gate | Gap and alternative | Accountable owner |
| --- | --- | --- |
| Dynamic replica fan-out | Shipped Redis Streams wildcard does not cover concrete tenant topics; qualify a dynamic subscription or alternate broker/topic design | SignalR/Architecture |
| Real projection detail | Sample projection emits a signal; compose scoped detail with the actual projection rather than synthetic publication | Projection/SignalR/Test |
| Production identity/profile | Development bound symmetric provenance is not a production binding-capable issuer; approve and qualify an issuer/profile | Security/Platform deployment |
| Freshness and completion | Synthetic Dapr reconciliation proves persisted state only; run canonical Stale/Degraded/Unavailable/Unknown and real completion behavior | UX/Projection/Test |
| Provider/network outage and backpressure | Replica/sidecar faults do not qualify provider-wide/network faults, retention, saturation, or combined retry budgets; use an isolated fault profile | Operations/Test |
| Conflicting identity | Wrong/missing grants are distinct from conflicting credential conformance; qualify the latter without inferring a grant from app identity | Security |
| Azure binding equivalence | No service profile or two-host connection proof; qualify trusted negotiation, membership and service limits before selection | SignalR/Security/Operations |
| Rollback and atomic transition | Disposable restart/teardown is not host/package/deployment rollback; prove the complete transition before retiring Redis | Platform deployment/Test |
| Cost, retention, throughput | No supported capacity or spend envelope is established; Story 2.16 owns NFR5 primary evidence | SignalR/Story 2.16 owner |
| Approved disposition | Required Architecture/Security/Operations/Test/transport-owner decisions are absent; no exception is inferred from unknown suitability | Named reviewing owners |

[Dapr's Redis documentation](https://docs.dapr.io/reference/components-reference/supported-pubsub/setup-redis-pubsub/)
describes consumer IDs and retry/queue/retention controls; those controls need
profile-specific qualification. The
[Azure SignalR binding](https://docs.dapr.io/reference/components-reference/supported-bindings/signalr/)
documents output `create` and group/user targeting. It does not establish the
existing application's connection and membership behavior.
[Microsoft's negotiation description](https://learn.microsoft.com/en-us/azure/azure-signalr/signalr-concept-client-negotiation)
requires a trusted server to redirect clients to the service. These documents
support a candidate design comparison, not an Azure suitability rejection.

## Disposition and Story 3.17 handoff

**Unresolved; retain the shipped Redis integration and story backlog.** No
backplane dependency is retired, no exception is accepted, and no production
profile, release/promotion, NFR5 closure, or story completion is asserted. The
metadata contract fix is narrower than the gated runtime transition.

Story 3.17 must inventory the retained EventStore/Gateway linked Redis graphs,
`ConfigureBackplane`, `BackplaneRedisConnectionString` and
`EVENTSTORE_SIGNALR_REDIS`, plus the shared metadata contract and this exact
unresolved qualification handoff. The exception register remains
`accepted_exceptions: []`. The pre-existing sprint state is preserved; it is not
completion or production authority.
