# Story 3.21 — offline remediation assessment and proposed evidence contract

2026-11-13. **Owner approved the offline investigation, evidence criteria and delivery requirements;
exact implementation inputs remain conditional and execution is not admitted.**
Platform 4.17 and EventStore 3.21 remain **in-progress**. Candidate-02 remains technical
**FAIL (20/24)**, all five historical guarantees **REJECTED/open**, selected broker **null**,
and production eligibility **FAIL**. Its independently verified integrity and credited cleanup
remain separate results. No new remedy has been demonstrated by this assessment.

The [parent reconciliation](../../_bmad-output/implementation-artifacts/spec-3-21-production-dapr-runtime-pin-and-durable-broker-qualification.md#platform-result-reconciliation--2026-11-13)
and published [Platform intake](https://github.com/Hexalith/Hexalith.Platform/blob/44756d1db8a38ac305cbb5247ae9cd52985b1d76/docs/implementation/story-4-17-next-candidate-intake.md)
govern the observations. Review baselines are Platform
`44756d1db8a38ac305cbb5247ae9cd52985b1d76` and EventStore
`5f4db84502c7c54b316dd9429dccd12ae881e759`, both observed on local and remote main.
They do not replace the executed candidate-02 identities in the parent reconciliation.

## Kafka pressure: unresolved supported mechanism, then an evidence-contract change

The unchanged [`pressure_rejection`](../../tools/dapr_broker_qualification.py) accepts HTTP
400–598 only when Dapr's `google.rpc.ErrorInfo` has domain `dapr.io`, reason
`DAPR_PUBSUB_PUBLISH_MESSAGE`, topic `qualification.backpressure`, and exact error
`kafka server: The throttling quota has been exceeded`. Generic 500s and timeouts do not pass.

The Kafka 4.3.1 [tag object](https://api.github.com/repos/apache/kafka/git/tags/a07059eb9b5bac1bfdbb1e74313f2fae4ca20fd9)
resolves to `26b251a451ce941d3d7a55e6487bcb7f16b5ad48`. Its
[Produce quota branches](https://github.com/apache/kafka/blob/26b251a451ce941d3d7a55e6487bcb7f16b5ad48/core/src/main/scala/kafka/server/KafkaApis.scala#L487-L527)
throttle the channel for bandwidth/request quota excess while preserving partition results.
The inspected [strict controller quota](https://github.com/apache/kafka/blob/26b251a451ce941d3d7a55e6487bcb7f16b5ad48/server/src/main/java/org/apache/kafka/server/quota/StrictControllerMutationQuota.java#L55-L64)
can throw error 89 for administrative mutations, including
[topic creation](https://github.com/apache/kafka/blob/26b251a451ce941d3d7a55e6487bcb7f16b5ad48/core/src/main/scala/kafka/server/ControllerApis.scala#L362-L387).
That is not rejection of the real Dapr Produce request to the predeclared pressure topic.

**Demonstrated source behavior:** the current byte-rate fault cannot produce the required
quota rejection through that Produce path. Candidate-02 fixed acknowledged loss to zero,
but still supplied no accepted rejection. **Unresolved dependency:** no supported alternative
Produce-pressure mechanism reaching this exact Dapr error was identified in the inspected
sources. This bounded finding does not establish impossibility for every future implementation.
Request quotas, administrative quota failures, size limits, consumer tuning and deadlines are
not demonstrated substitutes.

There is a second, independent admission dependency. `validate_observations` in
[`tools/dapr_broker_qualification.py`](../../tools/dapr_broker_qualification.py) requires a
successful `docker exec` receipt containing `producer_byte_rate=1024` and client `qualification`.
The [producer](../../tools/qualify-dapr-brokers.py) applies/removes that fault, while the
[Platform input schema](../../tools/dapr_platform_broker_configuration.py) admits only consume
retry and group-rebalance tuning for Kafka. A configuration-only successor cannot replace it.

**Proposed work, in order:** EventStore identifies an exact supported rejection mechanism and
source path first. Only if feasible, specify producer application/removal and independent
receipt verification for the replacement fault, preserving `pressure_rejection`, all pass rules
and the full 24-vector matrix. Any future changed harness/profile needs new exact source/input
identities and preserves the old locks and failed packets. No implementation or repinning occurs here.

**Proposed acceptance:** Given an exact supported tuple and real Dapr publication, when the
command-bound producer-pressure fault is applied, then native broker evidence and the correlated
HTTP receipt prove the exact accepted rejection on `qualification.backpressure`. Given the same
capture, when the separate one-second cancellation request runs against the paused broker, then
its synthetic 599 and independently checked elapsed/wall-clock bound at most ten seconds prove
the existing cancellation condition; that request is not the attributable pressure rejection.
Retain the producer's pressure-publish request timeouts (Kafka twelve seconds, RabbitMQ five),
ten-second recovery/readback window after consumer restart, and verifier timing consistency checks.
All acknowledged IDs must have persisted effects at that bound, retention preserves the
observation, uncertain IDs remain separately reconciled, and removal/recovery is evidenced.
Until that mechanism exists, retain the engineering dependency and FAIL; do not advertise a
remedy or ask Platform to retry the present fault. A predicate change would be a separate owner
decision outside this proposal.

## Kafka rotation: minimum evidence to adjudicate causality

The two historical captures persisted a stale ID after the deny ACL; candidate-02 returned an
attributable authorization denial and had no stale effect within its bound. Candidate-01's
apparent PASS remains diagnostic-only because integrity was rejected. The published intake
retains command indexes, times, hashes and readback counts. Identical logical ACL lists and
unchanged rotation code do not establish the authorizer state or identity at each Produce.
Longer historical deny-to-request intervals contradict the simple claim that the later PASS
merely waited longer; they do not exclude a propagation fault.

Minimum evidence is a joined record for **each** historical failure and the verified PASS:

| Evidence | Required binding and adjudication use |
| --- | --- |
| Packet and worker | Original run/attempt, exact ZIP/entry inventory, producer/verifier/configuration/runtime/image identities, command indexes and timestamp basis; retain candidate-01's invalid-integrity label. |
| Actual publisher | Old and new process identities (worker, PID/start, endpoint, argv and loaded component/configuration hashes), startup/restart receipts and credential-generation fingerprints. Both publishers use the same app ID, so app ID alone cannot distinguish them. Do not retain private keys or secret values. |
| Effective principal | Broker-observed authenticated principal of the **actual stale Produce connection**, its listener/security protocol, client/request and connection identifiers, and certificate subject/fingerprint mapped to the loaded old generation. `User:CN=client` in the revoke command or certificate file is an expectation, not this observation. Retain rotated-principal evidence as the positive control. |
| Installed ACL state | Successful add-deny command receipts and exact list before/after, principal/host/operation/resource-pattern/permission, wildcard/prefix matches, superuser/default-allow/principal-mapping configuration and relevant broker/controller identity. Include the old allows and rotated allows. An administrative list alone proves no applied state for a separate request. |
| Applied authorization | On the serving broker, bind the actual Produce authorization decision and matching rule/state to the installed deny and stale connection/request. Retain metadata-application ordering or an equivalent supported state witness where needed to distinguish installed from applied. A fixed sleep or matching ACL-list hash is insufficient. |
| HTTP publication and persistence | Exact message ID/payload hash, old endpoint, request/response/ErrorInfo and time bounds, retries, native acceptance/error and offsets if available; persisted Dapr receipt/effect readback for old and new IDs with observation bounds. Historical stale effect 1 remains failure; bounded absence is not permanent absence. New-generation effect must remain exactly 1. |

Kafka's pinned [authorization loop](https://github.com/apache/kafka/blob/26b251a451ce941d3d7a55e6487bcb7f16b5ad48/metadata/src/main/java/org/apache/kafka/metadata/authorizer/StandardAuthorizer.java#L139-L159)
uses the current authorizer data for each action. Its
[stock audit message](https://github.com/apache/kafka/blob/26b251a451ce941d3d7a55e6487bcb7f16b5ad48/metadata/src/main/java/org/apache/kafka/metadata/authorizer/StandardAuthorizerData.java#L225-L309)
can show principal, operation, resource, request type and matching rule. **Proposed observation,
not delivered instrumentation:** combine supported audit/request/connection evidence to establish
the join above. The audit text alone has no publication ID, connection/correlation ID or metadata
offset; prove adequate correlation and sanitization before claiming the evidence contract is
implementable. No custom authorizer or private runtime is proposed.

There is also an admission dependency: the current Platform parser admits only two Kafka tuning
fields, and independent effective-input verification reconstructs exact broker properties and
startup grammar. First determine whether existing supported logs can establish the join. Any
new logging configuration, launch flags or capture paths need an explicit EventStore producer,
profile/configuration admission and independent verification plan as applicable; ad hoc logger
settings would drift the accepted inputs. This instrumentation contract must be reviewed before
implementation, even if the underlying logging feature is supported.

The discriminating questions are: did the old request use the expected old principal; did it
reach the intended broker/topic through the recorded old publisher; and had that broker applied
the matching deny before authorizing it? Principal mismatch, publisher misbinding and ACL
application timing remain **hypotheses**. The existing mTLS model provides no demonstrated SASL
reauthentication remedy.

**Proposed acceptance:** Given the original captures, when EventStore performs the joined audit,
then each causal conclusion cites the observations that support it; missing principal/applied-state
evidence yields an explicit historically unadjudicable result. A future instrumented diagnostic
can test hypotheses but cannot manufacture missing historical observations. Given a separately
approved diagnostic, when the old session is evaluated, then the unchanged attributable-denial
rule, controlled restart, exactly one new effect and **zero stale effects within the existing
three-second observation window** are each independently evidenced; retain the five-second
stale HTTP request timeout. Cold old-generation denial is an additional diagnostic question still unproved; its
separately specified broker-refusal or startup-failure observation cannot substitute for the
existing old-session HTTP predicate. No repeat count
or additional execution is authorized by this contract; a lone PASS does not close the history.

### Approved offline follow-through: retained-packet recheck

Following the owner's approval, the three original cached ZIPs were checked again against the
published SHA-256 commitments. All matched. The scan covered retained non-source JSON/log/text
outputs under each packet root, excluding copied inputs and execution-tool source:

| Capture | Command receipts | Text files scanned | Stale HTTP status | New effects |
| --- | --- | --- | --- | --- |
| Historical standalone | 1113 | 105 | 204 | 1 |
| Historical fixture | 1105 | 105 | 204 | 1 |
| Candidate-02 | 1113 | 108 | 500, attributable authorization denial | 1 |

None of those scanned files contained `Principal =`, `StandardAuthorizerData`, `correlationId=`
or `connectionId=`. This is a bounded marker search, not proof that every possible encoding of
identity is absent. It supplies no joined publisher/principal/applied-ACL witness and cannot
resolve the historical cause. Under the approved evidence criteria the cause remains
**unadjudicable on the available evidence**, the verified PASS remains one capture, and the
historical guarantee remains **REJECTED/open**. No infrastructure or new verifier packet ran.

## RabbitMQ: supported publish-path delivery and proposed acceptance

Read-only upstream verification on 2026-11-13 found the following exact identities:

| Delivery | Verified identity / implication |
| --- | --- |
| Latest [Dapr v1.19.1](https://github.com/dapr/dapr/releases/tag/v1.19.1), published October 31 | `cfb5ee090dda7cfc26145c7703605d831350aa2a`; its [go.mod](https://github.com/dapr/dapr/blob/cfb5ee090dda7cfc26145c7703605d831350aa2a/go.mod) embeds components-contrib v1.19.0. |
| Latest [components v1.19.0](https://github.com/dapr/components-contrib/releases/tag/v1.19.0), published October 22 | `1d331c389f11baaa1d664022b6b6c2f59811ce11`; [released RabbitMQ source](https://github.com/dapr/components-contrib/blob/1d331c389f11baaa1d664022b6b6c2f59811ce11/pubsub/rabbitmq/rabbitmq.go#L282-L334) logs failed confirmation and returns nil. |
| NACK source fix / backport | [`78808be217cc4aea768acbedd0161ab9db04a0b4`](https://github.com/dapr/components-contrib/commit/78808be217cc4aea768acbedd0161ab9db04a0b4), October 30; [#4748](https://github.com/dapr/components-contrib/pull/4748) merged November 6 as `ac89e0966ca5d9ff4dce4ae2ef0045c33e2660ee`. Neither latest release supplies it. |
| Current main / release-1.19 | [`440b15349565bb15f7a88636477864e5a9f8cc72`](https://github.com/dapr/components-contrib/blob/440b15349565bb15f7a88636477864e5a9f8cc72/pubsub/rabbitmq/rabbitmq.go#L282-L345) / [`46cf4f95b0c84ecb4014b6f79103a1b1ca9c5fbc`](https://github.com/dapr/components-contrib/blob/46cf4f95b0c84ecb4014b6f79103a1b1ca9c5fbc/pubsub/rabbitmq/rabbitmq.go#L282-L345). Both contain the NACK fix; both still retry, return nil when cancellation interrupts retry waits, discard reconnect errors, wait for confirmation without a context and publish with `mandatory=false`. No return handler was found in the inspected component file. |

| Failed vector | Required supported fix and proposed acceptance |
| --- | --- |
| Pressure | Propagate broker NACK through confirmation, retries and cancellation/deadlines to the caller, preserving a broker-attributable error. Given measured effective reject-publish/max-length-4 state on the exact queue, when pressure is applied, then the unchanged predicate receives the exact error `did not receive confirmation of publishing`, cancellation stays bounded and acknowledged loss is zero in persisted readback. The source-only `publishSync` fix alone is insufficient. |
| Rotation | Preserve and surface revoked-credential/reconnect `Exception (403)…ACCESS_REFUSED` through the complete publish path. Given the old publisher after revocation, when it publishes, then the unchanged Dapr denial predicate passes, controlled restart and new-generation effect 1 remain proved, and stale readback is bounded. Retain the cold-generation question. Discarded errors/cancellation success are demonstrated code paths; identifying which caused the historical 599 remains a hypothesis. |
| Missing binding | Supply supported mandatory publishing **and correlated return handling completed before success**, with confirm/return ordering and concurrent requests handled. Given a bound positive control and measured binding absence before/after, when the unbound ID is published, then HTTP 400–598, allowed effect 1 and bindingAbsent satisfy the unchanged rule, with ID-based persisted readback. NACK handling alone and alternate routing do not prove this rejection. |

The pressure cancellation/readback criteria above also govern RabbitMQ: a distinct paused-broker
request must return synthetic 599 with measured bound at most ten seconds; the pressure request
must supply its own broker-attributable rejection. Rotation additionally retains the five-second
stale HTTP timeout and zero stale effects in the three-second observation window. These proposed
acceptance statements restate existing conditions; they introduce no longer windows or relaxed errors.

RabbitMQ documents [reject-publish NACKs](https://www.rabbitmq.com/docs/maxlength) and
[mandatory returns before positive confirms for unroutable messages](https://www.rabbitmq.com/docs/confirms).
These semantics explain the required paths; they do not demonstrate a Dapr remedy.

**Delivery gate:** EventStore must identify an official supported Dapr release embedding the
necessary component changes, verify exact runtime/component/binary/image identities and admit
the full lock independently. Builds then owns accepted catalog/CLI/runner alignment; Platform
owns a separately accepted exact configuration and full-matrix execution. A merged component
commit, isolated unit test or passing native AMQP client cannot establish the real Dapr path.
No supported complete delivery was identified; runtime pins remain unchanged.

## Scope submitted for owner decision

1. **EventStore 3.21:** accept the bounded pressure feasibility investigation and evidence-contract
   scope. If no supported rejecting Produce path can be identified, retain the dependency and FAIL;
   no configuration-only remedy is available to implement under the current predicate.
2. **EventStore 3.21 / Platform 4.17:** accept the minimum rotation join and decide whether missing
   historical observations make causality unadjudicable. Decide the exact supported instrumentation
   and cold-generation diagnostic scope before implementation; execution needs its own later decision.
3. **EventStore / upstream / Builds:** accept the three RabbitMQ delivery requirements and their
   end-to-end acceptance criteria; identify a supported released tuple before proposing pin adoption.
4. **Platform 4.17 custody owner:** select destination, retention/access rules, named transfer operator
   and independent verifier in time for historical packet preservation before
   **2026-12-08T23:05:01Z** / **2026-12-08T23:08:16Z**, and candidate-02 before
   **2026-12-13T07:40:43Z**. Artifact `14018214306` needs preservation
   of the exact sanitized ZIP and full inventory, not just assessment links. The Platform intake owns
   that decision and transfer acceptance. No transfer or custody acceptance occurs here.

The workstreams are independent. A supported-input diagnostic may investigate an accepted bounded
question while predicting unresolved vectors as FAIL; it need not wait for all remediation.
Unresolved remedies block qualification claims, and unsupported inputs block execution admission.
Once the relevant question, evidence dependencies and implementation scope are accepted, Platform can prepare exact source,
lock/configuration hashes, worker/tools, hypotheses, predicted failures, independent verifier calls
and cleanup/preservation requirements for a separate execution decision. All 24 vectors remain;
diagnostic capture may truthfully fail. AD-26, Story 4.9 inventory, staging 4.14–4.16 and exact
shared-host/restore proof remain separate prerequisites. No candidate, broker or execution is selected.

## Owner approval — 2026-11-13

The owner's **“approve”** following the completed assessment accepts the bounded pressure
investigation and conditional contract scope, minimum rotation evidence criteria, RabbitMQ
delivery requirements and per-archive custody acceptance criteria. These are accepted requirements
for the offline work; they do not establish a supported pressure fault, implementable correlation
instrumentation, upstream acceptance/delivery or Builds adoption. Conditional changes still need
those concrete technical inputs. No guarantee, predicate, runtime pin or parent status changes.

The missing historical principal/applied-state observations leave causality unadjudicable under
the approved criteria. Further offline source/evidence work may narrow the hypotheses. A future
diagnostic still needs its exact supported observation/capture/admission subject and separate
execution decision; a repeat PASS cannot fill missing historical observations.

Platform's custody destination, retention/access/deletion terms, named operator and independent
verifier have not been supplied. An information request is pending; no transfer is admitted or
performed without those concrete inputs. Preserve the December 8 historical and December 13
candidate-02 deadlines above. Production eligibility remains FAIL and broker selection null.

## Application of recommendations — 2026-11-14

The owner then directed **“apply recommended”**. The concrete
[Kafka bounded investigation](story-3-21-kafka-bounded-investigation.md) and
[two RabbitMQ upstream work packages](story-3-21-rabbitmq-upstream-work-packages.md) apply that
scope. Pressure source review stops without an identified supported rejecting mechanism. Rotation
now has a precise supported logging candidate and source change map, with three required joins
still unresolved; it is not represented as delivered instrumentation. RabbitMQ drafts preserve
the real upstream issue history and unchanged acceptance predicates; no issue has been posted.

Platform locally staged all four original ZIPs with one full checksum/inventory manifest.
Independent local verification passed for 2,215 archive entries and 2,059 packet artifact hashes;
candidate-01 remains diagnostic-only. Local staged ZIP names now share inodes with the original
cache after disk-space recovery; they are not independent storage. No accepted off-site
destination/allocation/access reference was identified in the inspected handoffs, so transfer
and off-site acceptance remain open. Retention holds while guarantees remain open, followed by
policy review; no new service is provisioned.

No producer, verifier, profile, runtime pin, 24-vector inventory or qualification predicate changed.
Both parent stories remain in-progress, all five historical guarantees REJECTED/open, technical
qualification FAIL (20/24), selection null and production eligibility FAIL.
