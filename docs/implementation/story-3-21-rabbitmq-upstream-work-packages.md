# Story 3.21 — prepared RabbitMQ upstream work packages

2026-11-14, Europe/Paris. **Local issue-ready drafts; no issue posted and no upstream acceptance
or delivery claimed.** The owner requested application of the pragmatic recommendations.
These two packages implement the preparation recommendation, not runtime changes.

Both parent stories remain in-progress, all five historical guarantees REJECTED/open, technical
qualification FAIL (20/24), broker selection null and production eligibility FAIL. The
[evidence contract](story-3-21-broker-remediation-evidence-contract.md) governs existing predicates.

## Current source and supported-delivery check

Read-only checks at 2026-11-13T23:06:50Z (November 14 in Europe/Paris) reconfirmed:

- Latest [Dapr v1.19.1](https://github.com/dapr/dapr/releases/tag/v1.19.1):
  `cfb5ee090dda7cfc26145c7703605d831350aa2a`; its
  [go.mod](https://github.com/dapr/dapr/blob/cfb5ee090dda7cfc26145c7703605d831350aa2a/go.mod)
  embeds components-contrib v1.19.0, `1d331c389f11baaa1d664022b6b6c2f59811ce11`.
- Current components main `440b15349565bb15f7a88636477864e5a9f8cc72` and release-1.19
  `46cf4f95b0c84ecb4014b6f79103a1b1ca9c5fbc` retain the incomplete
  [publish path](https://github.com/dapr/components-contrib/blob/46cf4f95b0c84ecb4014b6f79103a1b1ca9c5fbc/pubsub/rabbitmq/rabbitmq.go#L282-L345).
- NACK fixes `78808be217cc4aea768acbedd0161ab9db04a0b4` and backport
  `ac89e0966ca5d9ff4dce4ae2ef0045c33e2660ee` are source-only candidate inputs;
  the latest [component release](https://github.com/dapr/components-contrib/releases/tag/v1.19.0)
  predates them. No checked supported release supplies the complete remedies.

Deduplication: [issue #4697](https://github.com/dapr/components-contrib/issues/4697) describes
the original NACK defect **and mandatory/return handling**, but is closed/completed. Its unresolved
mandatory half needs a linked follow-up or reopening request. Adjacent
[#4596](https://github.com/dapr/components-contrib/issues/4596), closed/not-planned, concerns
permanent exchange-declaration retry classification, not revoked-credential reconnect propagation.
Open [#4724](https://github.com/dapr/components-contrib/issues/4724) concerns a consumer/shared-channel
race. Bounded issue searches found no exact cancellation-to-nil or publisher ACCESS_REFUSED issue;
that does not prove none exists. Recheck before filing to avoid duplicates.

## Package A — preserve publish failures through confirmation, cancellation and reconnect

**Suggested issue title:** RabbitMQ publish errors are lost during retry cancellation and reconnect

**Problem and evidence:** The released component logs NACK and returns success. The NACK backport
corrects `publishSync`, while `Publish` still returns nil when either retry wait is cancelled and
discards reconnect errors; confirmation waits through a context-free `Wait()`. These are observed
source paths. The exact contribution of each to the historical stale-credential timeout is a
hypothesis, not established causality.

Historical Dapr-path evidence: the measured reject-publish/max-length-4 policy produced no accepted
rejection and three acknowledged IDs missing at the readback bound. Rotation's stale publication
timed out with synthetic 599. New-generation publication worked. These are bounded results from
the retained candidate, not a fresh upstream reproduction or proof of permanent loss.

**Proposed change:** Preserve broker confirmation and reconnect errors through the component's
existing retry path. Make every confirmation/retry/reconnect wait respect cancellation/deadlines
and return non-success on cancellation. Recognize permanent authorization errors without consuming
the remaining budget in futile retries. Keep an already in-flight publication uncertain until
persisted readback; never manufacture a broker denial from a generic deadline.

Include the blocking `channelMutex` acquisition in `publishSync`, exchange declaration and
connection establishment in that same deadline review. A concurrent caller waiting behind a
stalled publication must not outlive its budget merely because it has not reached a retry timer.
The proposed change must bound or unblock those paths too; timer-only cancellation tests are insufficient.

**Implementation targets in components-contrib:** `pubsub/rabbitmq/rabbitmq.go`,
`pubsub/rabbitmq/rabbitmq_test.go` and existing broker integration/certification tests. Extend tests
at the public `Publish` boundary, not only `confirmationError`. No new retry service or private
Dapr fork is needed. The existing helper test and mock returning no confirmation do not establish
complete behavior.

**Reproduction recipe for separately authorized execution:** Fill the exact bounded queue with
publisher confirms enabled; publish uniquely identified messages through Dapr. Exercise cancellation
during confirmation, ordinary retry and reconnect waits. Keep an old publisher alive, rotate the
new generation, revoke the old credential and publish from both identified instances. Capture
native confirmations/authentication errors, Dapr responses and ID-based persisted readback.

**Acceptance criteria:**

- Given a broker NACK, when the Dapr publication finally fails, then the exact broker-attributable
  error `did not receive confirmation of publishing` survives all layers; the rejection is never
  acknowledged as success. Recovery that genuinely accepts a retry must retain its actual outcome.
- Given cancellation at each wait, when the call terminates, then it returns non-success within
  the existing bound and starts no further retries after observing cancellation. Cleanup is bounded;
  already in-flight effects remain uncertain and are reconciled separately.
- Given a stalled lock holder, exchange declaration or connection establishment, when another
  call reaches its deadline, then bounded termination still holds and no delayed publish is
  started afterward merely because the lock or connection later becomes available.
- Given revoked-credential reconnect returning `Exception (403)…ACCESS_REFUSED`, when publishing
  through Dapr, then that attributable broker error reaches the existing denial predicate. Generic
  timeout/cancellation is insufficient.
- Given the existing qualification faults, when independently evaluated, then the distinct
  paused-broker request returns synthetic 599 within the ten-second measured bound, acknowledged
  loss is zero within the ten-second recovery/readback window, controlled restart/new effect once
  are preserved, and no stale effect appears in the three-second rotation window. Keep current
  request timeouts; do not expand a bound to make the fix pass.
- Retain a separate cold-old-generation control, with its own approved broker-refusal/startup-failure
  observations. It cannot substitute for the existing old-session Dapr HTTP denial predicate.

## Package B — optional mandatory publishing with correctly correlated returns

**Suggested issue title:** Follow up #4697: support mandatory publishing and correlated unroutable returns

**Problem and evidence:** Current main and release-1.19 still publish with `mandatory=false` and
have no return handler in the inspected component. Candidate-02 measured binding absence before
and after; the unbound publication returned 204 beside a passing bound control. This unresolved
behavior is already described in #4697 and should be linked explicitly.

**Proposed change:** Add opt-in mandatory publishing, defaulting to current behavior for compatibility.
The exact metadata name belongs to upstream; a possible name is `publishMandatory`. Mandatory
mode should require publisher confirms or reject an incompatible configuration. Associate each
return with the correct publication attempt and channel generation, and resolve return/confirmation
ordering before reporting success. Keep correlation bounded and within existing publisher machinery.
Do not add a routing service, alternate exchange or application-side provider access.

Carry that returned failure through the existing public `Publish` path within the current HTTP
timeout. Once an unroutable return is conclusive, classify it as a final publish failure or use
bounded retry handling that preserves the error; do not spend the entire budget retrying it into
synthetic 599. The chosen component/runtime error classification must be verified to produce the
existing accepted HTTP 400–598 result; its name alone supplies no API-behavior proof.

**Implementation targets in components-contrib:** `pubsub/rabbitmq/{metadata.go,metadata.yaml,
metadata_test.go,rabbitmq.go,rabbitmq_test.go}` and existing broker integration/certification tests.

**Reproduction recipe for separately authorized execution:** Create an exchange and a routed control;
measure binding removal, enable mandatory mode, and publish a unique unbound ID. Capture native
`basic.return` / `312 NO_ROUTE`, subsequent confirmation, Dapr response and persisted readback.
RabbitMQ documents [return before positive confirm](https://www.rabbitmq.com/docs/confirms) and
[reject-publish NACKs](https://www.rabbitmq.com/docs/maxlength); neither mechanism alone proves the
other guarantee.

**Acceptance criteria:**

- Given a mandatory unroutable publication, when its return precedes ACK, then that exact call fails;
  ACK cannot overwrite the returned outcome, and the failure reaches Dapr within the existing
  HTTP timeout without being replaced by retry exhaustion/cancellation uncertainty.
- Given concurrent calls, retries, duplicate application IDs, cancellation or channel replacement,
  when returns arrive, then each remains bound to its actual attempt/channel; no stale return
  contaminates a later publication and no unbounded correlation state is retained.
- Given a routed positive control, when published, then success and exactly one persisted effect
  are observed. Given opt-out configuration, existing behavior remains compatible.
- Given the existing physically missing binding before/after, when evaluated through Dapr, then
  HTTP 400–598, `allowedEffects == 1` and `bindingAbsent` satisfy the unchanged predicate. Retain
  ID-based readback and uncertain outcomes; alternate routing cannot substitute for rejection.

## Concrete reproduction inputs and request steps

These are **unexecuted maintainer recipes**, not an admitted Platform run. Use a fresh owned TLS
RabbitMQ/Dapr fixture and its controlled consumer-stop/restart operations. The complete nonsecret
inputs are the immutable
[component](https://github.com/Hexalith/Hexalith.EventStore/blob/495dd24943031ec75e2822c6168ada3c8b9fdb82/deploy/dapr/qualification/rabbitmq.yaml)
and [subscriptions](https://github.com/Hexalith/Hexalith.EventStore/blob/495dd24943031ec75e2822c6168ada3c8b9fdb82/deploy/dapr/qualification/subscriptions.yaml),
not private packet files. Supply fixture-local secret references rather than credential values.

Relevant component metadata is `consumerID=qualification-consumer`, `durable=true`,
`deletedWhenUnused=false`, `autoAck=false`, `deliveryMode=2`, `publisherConfirm=true`,
`prefetchCount=1`, `reconnectWaitSeconds=1`, `concurrency=single`, `exchangeKind=fanout`,
`maxLen=4`; the subscriptions use `queueType=quorum`. Preserve the complete topic/scope catalog
and TLS/secret configuration from those files. Component name is `pubsub` and publisher app ID
is `qualification-publisher`.

For pressure, after the exact queue exists and the fixture consumer is controlled-stopped, bind
`qualification_rabbit_container` and `qualification_dapr_url` to that fixture's measured container
and loopback publisher endpoint. Apply/inspect the exact policy, then issue eight uniquely
identified publications within this fresh fixture:

```bash
docker exec "$qualification_rabbit_container" rabbitmqctl set_policy qualification-bounded \
  '^qualification-consumer-qualification\.backpressure$' \
  '{"overflow":"reject-publish","max-length":4}' --apply-to queues
docker exec "$qualification_rabbit_container" rabbitmqctl list_queues \
  name policy effective_policy_definition --formatter=json
for qualification_index in 1 2 3 4 5 6 7 8; do
  curl --silent --show-error --max-time 5 --write-out '\nHTTP %{http_code}\n' \
    --header 'Content-Type: application/json' \
    --data "{\"specversion\":\"1.0\",\"id\":\"pressure-$qualification_index\",\"source\":\"story-3-21\",\"type\":\"qualification\",\"datacontenttype\":\"application/json\",\"data\":{\"messageId\":\"pressure-$qualification_index\"}}" \
    "$qualification_dapr_url/v1.0/publish/pubsub/qualification.backpressure?metadata.rawPayload=false"
done
```

Retain each response and elapsed time, restart the consumer and read back all eight IDs through
the existing fixture within the unchanged ten-second window. Queue name is exactly
`qualification-consumer-qualification.backpressure`. Curl timeout/exit status is a client transport
observation, not a fabricated HTTP broker rejection; the qualification producer's synthetic 599
mapping remains separate. Repeat fault placement inside existing component tests for the lock,
exchange, connection, confirmation and retry waits; these calls alone do not cover every wait.

For rotation, first use the existing fixture to create `qualification-v2`, publish its secret
generation and controlled-restart the consumer/new publisher while keeping the old publisher alive.
Bind `qualification_old_publisher_url` and `qualification_new_publisher_url` to those measured
loopback endpoints. Retain the new control, revoke `qualification`, then test the old endpoint:

```bash
curl --silent --show-error --max-time 5 --write-out '\nHTTP %{http_code}\n' \
  --header 'Content-Type: application/json' \
  --data '{"specversion":"1.0","id":"rotation-new-1","source":"story-3-21","type":"qualification","data":{"messageId":"rotation-new-1"}}' \
  "$qualification_new_publisher_url/v1.0/publish/pubsub/qualification.events?metadata.rawPayload=false"
docker exec "$qualification_rabbit_container" rabbitmqctl delete_user qualification
curl --silent --show-error --max-time 5 --write-out '\nHTTP %{http_code}\n' \
  --header 'Content-Type: application/json' \
  --data '{"specversion":"1.0","id":"rotation-stale-1","source":"story-3-21","type":"qualification","data":{"messageId":"rotation-stale-1"}}' \
  "$qualification_old_publisher_url/v1.0/publish/pubsub/qualification.events?metadata.rawPayload=false"
```

Retain new-effect count 1, the exact stale broker-attributable response and three-second stale
readback; schedule the cold-generation control separately. For Package B, enable the proposed
mandatory mode **only after it exists in a supported component**. In a fresh fixture with the
bound `qualification.events` control and physically unbound `qualification.missing-binding`,
capture binding inventories immediately before and after this request:

```bash
docker exec "$qualification_rabbit_container" rabbitmqctl list_bindings \
  source_name destination_name --formatter=json
curl --silent --show-error --max-time 5 --write-out '\nHTTP %{http_code}\n' \
  --header 'Content-Type: application/json' \
  --data '{"specversion":"1.0","id":"unbound-1","source":"story-3-21","type":"qualification","data":{"messageId":"unbound-1"}}' \
  "$qualification_dapr_url/v1.0/publish/pubsub/qualification.missing-binding?metadata.rawPayload=false"
docker exec "$qualification_rabbit_container" rabbitmqctl list_bindings \
  source_name destination_name --formatter=json
```

Assert binding absence for the exact missing-binding topic and presence for the positive control;
retain the control's persisted effect and the unbound ID's native return/API/readback outcomes.
No broker, sidecar, policy, user or binding was changed while preparing these recipes.

## Delivery and next action

These drafts are ready for maintainer discussion, with original packet identities and source links
available through the owning evidence contract. Filing/reopening or sending them externally has
not occurred. Implementation and tests must precede an official supported Dapr release embedding
the accepted fixes; then EventStore admits exact runtime/component identities, Builds accepts its
catalog/CLI/runner alignment, and Platform prepares the separately accepted full-matrix execution.
Source merges, helper tests and native AMQP success alone confer no qualification or broker choice.
