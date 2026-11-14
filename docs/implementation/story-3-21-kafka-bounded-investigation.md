# Story 3.21 — bounded Kafka investigation and observation candidate

2026-11-14, Europe/Paris. Applies the owner's **“apply recommended”** direction. This is a
completed bounded source investigation and a concrete diagnostic candidate, not an implemented
fault, instrumentation delivery or execution approval. The
[evidence contract](story-3-21-broker-remediation-evidence-contract.md) remains governing.

Both parent stories remain in-progress, all five historical guarantees REJECTED/open, technical
qualification FAIL (20/24), broker selection null and production eligibility FAIL. All sources
below are pinned Kafka 4.3.1, `26b251a451ce941d3d7a55e6487bcb7f16b5ad48`.

## Pressure: bounded review completed, replacement implementation stopped

The [Produce handler](https://github.com/apache/kafka/blob/26b251a451ce941d3d7a55e6487bcb7f16b5ad48/core/src/main/scala/kafka/server/KafkaApis.scala#L487-L527)
throttles bandwidth/request quota excess without replacing successful partition results with
`THROTTLING_QUOTA_EXCEEDED`. The
[strict controller quota](https://github.com/apache/kafka/blob/26b251a451ce941d3d7a55e6487bcb7f16b5ad48/server/src/main/java/org/apache/kafka/server/quota/StrictControllerMutationQuota.java#L55-L64)
and its administrative callers demonstrate a different path. No supported producer-pressure
mechanism reaching the unchanged exact Dapr rejection was identified. This finding is bounded
to inspected sources, not a universal assertion about every Kafka version or fault.

**Stop rule applied:** do not implement a replacement pressure fault or tune the current quota
again without identifying that supported Produce rejection path. The existing verifier's
`validate_observations` quota-receipt check and immutable profile remain unchanged. Pressure
stays FAIL with acknowledged loss zero on candidate-02; there is no remedy to advertise.

**Prepared upstream question, not sent:**

> Does supported stock Kafka provide a producer-pressure mechanism that returns protocol error
> 89 from Produce on an already-declared topic, reaching the Dapr publish path as the exact
> error `kafka server: The throttling quota has been exceeded`? Please identify the supported
> configuration, released version, source path and reproducible evidence. The inspected 4.3.1
> bandwidth/request quotas throttle; a controller-mutation rejection is not this publication.

An answer identifying such a mechanism can reopen implementation planning. An answer confirming
its absence leaves the explicit dependency and unchanged predicate; it does not authorize a
different error, a private broker/runtime or reduced qualification matrix.

## Rotation: smallest supported logging candidate

Kafka supports dynamic broker logger controls through
[`kafka-configs.sh`, entity `broker-loggers`](https://github.com/apache/kafka/blob/26b251a451ce941d3d7a55e6487bcb7f16b5ad48/core/src/main/scala/kafka/admin/ConfigCommand.scala#L207-L221).
The proposed diagnostic would use the producer's existing broker administration helper to describe
the prior logger state, apply these existing logger levels before rotation, describe effective
state, and restore the prior overrides after capturing the scenario:

```text
kafka.request.logger=DEBUG
kafka.authorizer.logger=DEBUG
org.apache.kafka.metadata.authorizer.StandardAuthorizerData=TRACE
```

This avoids a new launch mount, image or observability service. The broker-loggers entity is a
supported source capability; **none of these commands has been executed here**. Any invocation
must retain the producer's measured broker/container/admin identity, not run against a shared or
production target. Current administration grammar can recognize `kafka-configs.sh`; recognition
alone does not admit new diagnostics, configuration or retention behavior.

| Supported observation | Exact source capability | Limit still requiring a supported method |
| --- | --- | --- |
| Completed request | [RequestChannel](https://github.com/apache/kafka/blob/26b251a451ce941d3d7a55e6487bcb7f16b5ad48/core/src/main/scala/kafka/network/RequestChannel.scala#L258-L270) and [JSON conversion](https://github.com/apache/kafka/blob/26b251a451ce941d3d7a55e6487bcb7f16b5ad48/server/src/main/java/org/apache/kafka/network/RequestConvertToJson.java#L754-L799) supply request header/client ID/correlation ID, response, connection, listener/security protocol and session principal. | A native connection is not a publisher PID, credential-generation fingerprint or HTTP message ID. |
| Authenticated principal | [SocketServer](https://github.com/apache/kafka/blob/26b251a451ce941d3d7a55e6487bcb7f16b5ad48/core/src/main/scala/kafka/network/SocketServer.scala#L1022-L1028) constructs request context from the authenticated channel principal. [Connection IDs](https://github.com/apache/kafka/blob/26b251a451ce941d3d7a55e6487bcb7f16b5ad48/clients/src/main/java/org/apache/kafka/common/network/ServerConnectionId.java#L118-L127) encode endpoint tuples. | Measure process/socket ownership and any Docker port translation; app/client ID or endpoint timing alone is insufficient. |
| Local ACL application | [StandardAuthorizerData](https://github.com/apache/kafka/blob/26b251a451ce941d3d7a55e6487bcb7f16b5ad48/metadata/src/main/java/org/apache/kafka/metadata/authorizer/StandardAuthorizerData.java#L184-L198) logs ACL UUID/content after assigning its local cache. | This record has no metadata offset or reference to a particular request. Preserve installed ACL receipts and serving broker identity too. |
| Authorization result/rule | [Audit text](https://github.com/apache/kafka/blob/26b251a451ce941d3d7a55e6487bcb7f16b5ad48/metadata/src/main/java/org/apache/kafka/metadata/authorizer/StandardAuthorizerData.java#L252-L305) supplies principal, operation, resource, result and matching rule; DEBUG includes allowed operations. | Audit text has no connection/client/correlation ID. A unique audit-to-request association is not demonstrated. |
| Produce body | [Produce logging](https://github.com/apache/kafka/blob/26b251a451ce941d3d7a55e6487bcb7f16b5ad48/server/src/main/java/org/apache/kafka/network/RequestConvertToJson.java#L529) disables record serialization; [record conversion](https://github.com/apache/kafka/blob/26b251a451ce941d3d7a55e6487bcb7f16b5ad48/generator/src/main/java/org/apache/kafka/message/JsonConverterGenerator.java#L384-L400) retains byte size. | The CloudEvent/message ID is absent. Neither body length nor timing proves stale HTTP ID → Produce, especially with retries/concurrency. |

**Feasibility conclusion:** the logger candidate supplies useful missing observations, but the
process/socket, HTTP-ID/request and audit/request joins remain unresolved. Do not call logging
alone a demonstrated remedy or implement a verifier that silently infers those joins.

## Concrete prospective change map and evidence requirements

| Existing owning file | Minimum prospective change | Admission / acceptance requirement |
| --- | --- | --- |
| `tools/qualify-dapr-brokers.py`, rotation scenario and sidecar records | Record actual old/new process start/PID/endpoint, loaded-resource hashes and public credential-generation fingerprints; describe/apply/describe/restore logger overrides around the scenario. | No secret/private-key values. Bind every administration receipt to the measured owned broker and retain prior/effective/restored logger state. No invocation is admitted by this document. |
| `tools/qualify-dapr-brokers.py`, diagnostics/finish | Retain complete targeted `kafka-request.log`, `kafka-authorizer.log` and required ACL-application records before owned cleanup, including rotated log segments if present. | The current `docker logs --tail 150` is insufficient. Retain byte hashes, ordering/time basis, start/end coverage and an explicit truncation/missing-file result; incomplete coverage cannot support a causal claim. |
| `tools/dapr_broker_qualification.py` | Validate identities, logger receipts, coverage and any supported joins independently. Distinguish an absent causal witness from a contradicted claim. | Absent/ambiguous joins remain unadjudicable; false claims fail verification. Existing vector predicates and historical packet admission stay unchanged. |
| Existing `tools/tests` qualification controls and a separately identified diagnostic source/profile | Exercise wrong publisher/foreign principal, pre-application ACL, ambiguous/reused connection or request IDs, retries, missing/truncated logs and false joins. | Exact profile/source inventory admission is needed for the completed implementation. Keep old locks immutable and runtime/tool pins unchanged. Do not fork the producer or add a tracing service. |

Retain the outstanding **cold-old-generation** control explicitly in that prospective scenario
and test map. Its exact restart/refusal or startup-failure observation needs separate diagnostic
admission and cannot replace the existing live-old-session HTTP denial predicate. No cold-generation
result is asserted here, and no additional run is authorized by carrying the question forward.

The [default logger configuration](https://github.com/apache/kafka/blob/26b251a451ce941d3d7a55e6487bcb7f16b5ad48/config/log4j2.yaml#L53-L85)
provides the targeted file appenders. Resolve the actual in-container paths and retention segments
on the exact approved image/worker before execution; source file names alone are not observed
capture availability.

## Useful next diagnostic question and stop conditions

The narrow question is whether supported observations can bind the stale publisher, effective
principal and applied deny to the actual publication without relying on timing alone. The next
offline design must identify how the three unresolved joins can be made unambiguous with existing
worker/broker capabilities. If that cannot be specified, retain the blocker instead of adding
logging volume or custom correlation infrastructure.

A later bounded experiment may investigate an explicitly unresolved observation method, predicting
that causal adjudication could remain unavailable. It must name that uncertainty, exact supported
source/profile/configuration/worker/tools, full 24-vector scope, expected remaining failures,
independent verifier calls, preservation and cleanup. No successful pressure/RabbitMQ outcome or
historical-cause conclusion may be predicted without evidence. An exact execution decision is
still required; no run is dispatched here.
