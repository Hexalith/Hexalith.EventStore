# Typed reminder reconciliation

This is the AD-20 R6 producer seam. A domain module translates its committed
events into typed reminder intents. EventStore persists the scheduler state,
arms and cancels Dapr actor reminders, and submits due reminders through the
trusted-effect receipt path described in [Trusted effect submission](trusted-effects.md).
Platform owns the production Scheduler, its availability, and its backup policy.

Streams are authoritative. The discovery index only finds candidates. Every
decision to arm, submit, or cancel re-folds the target stream through the
domain's intent source first.

## Public contract

| Package | Type | Owner | Purpose |
| --- | --- | --- | --- |
| Contracts | `ReminderIntent` | Domain | Tenant, target domain and aggregate, UTC due instant, catalog kind, payload type, opaque payload, source domain/aggregate/sequence, and schedule revision |
| Contracts | `ReminderTarget` | Domain | The stream whose intents are converged |
| Contracts | `ReminderIdentityCodec` | EventStore | Version-one actor, schedule-token, and reminder-name codec |
| Contracts | `ReminderDisposition` | EventStore | Audited outcome: `Registered`, `Submitted`, `Retrying`, `Stale`, `Cancelled`, `Denied`, `Quarantined` |
| Contracts | `ReminderConvergenceResult` | EventStore | Armed, submitted, and cancelled counts, plus the item's retained unresolved and quarantined totals |
| Client | `IReminderIntentSource` | Domain | Re-folds current intents from the stream and translates a due intent into a command type and payload |
| Client | `IReminderRegistrar` | EventStore | `ConvergeAsync(target)`; call it after committing an event that changes a schedule |
| Client | `IReminderDelegationTokenProvider` | Platform | Issues the workload delegation for one submission; EventStore ships no implementation |
| DomainService | `AddEventStoreReminders<TSource>()` | EventStore | Registers the runtime |
| DomainService | `MapEventStoreReminders()` | EventStore | Maps the Dapr actor routes unless the host already mapped them |
| DomainService | `ReminderActor`, `IReminderActor` | EventStore | The actor that owns one item's reminders |
| DomainService | `EventStoreReminderOptions` | Host | Options bound from `EventStore:Reminders` |

Both `IReminderIntentSource` members must be deterministic and clock-free.
An instance can live as long as a reminder actor activation, so it must read
the stream on every call rather than cache it. EventStore reads the clock only
at its own edge. EventStore persists an
intent's identifiers and a SHA-256 digest of its payload, never the payload.
It also never interprets a domain decision.

The contract also binds its callers:

- Call `ConvergeAsync` at least once after every committed event that changes
  a target's schedule, and retry it when it throws, for example by letting a
  domain-event handler fail so its delivery is redelivered. A convergence that
  throws may have persisted nothing, and then no reconciliation pass can find
  the item.
- The intent source must stop reporting an intent once its target has handled
  the submitted command. Otherwise every convergence resubmits it and replays
  the receipt.
- `(source sequence, kind, target)` must be unique among a target's current
  intents. Intents that share it share one effect identity, and both are
  quarantined as `effect-collision`.
- Aggregate identifiers must be unique across every domain that shares one
  reminder actor type, because the AD-11 actor tuple omits the domain. A second
  domain with the same aggregate identifier is quarantined as `actor-collision`.

## Identity codec

`ReminderIdentityCodec` version 1 uses the AD-26 tuple rules of
`EffectIdentityCodec`. Each text field is canonical NFC UTF-8 preceded by a
four-byte big-endian length. Each integer is signed eight-byte big-endian. The
SHA-256 digest is rendered with `EffectIdentityCodec.RenderDigest` as 52
uppercase Crockford Base32 characters.

| Value | Tuple | Form |
| --- | --- | --- |
| Actor identifier | `("reminder-actor", tenant, item)` | `wra-<52>` |
| Schedule token | `("schedule", tenant, item, due UTC ticks, schedule revision)` | `wrs-<52>` |
| Reminder name | closed kind map plus schedule token | `date-wrs-<52>` for `works.date-resume.v1`, `expiry-wrs-<52>` for `works.expiry.v1` |

The item is the target aggregate identifier. The tenant must already be
canonical lowercase and is refused rather than folded. The due instant must
carry a zero UTC offset. No other `EffectKindCatalog` kind has a reminder name.

Golden vectors, calculated independently of the codec:

| Input | Value |
| --- | --- |
| Actor `tenant-a`, `item-1` | `wra-HZT1ANXRJ95M9MPNEVPJYRE4QS912QRKTCMEZWE0EBFQK015G1WG` |
| Token `tenant-a`, `item-1`, `2026-10-01T09:30:00Z`, revision 1 | `wrs-AED0KV4P0SF4RAQGXRJ8AZJEJ1NE6TVF5GWFHD0Z3HRMVRSQA8D0` |
| Date-resume name for the same witness | `date-wrs-AED0KV4P0SF4RAQGXRJ8AZJEJ1NE6TVF5GWFHD0Z3HRMVRSQA8D0` |

`ReminderIdentityCodec.Rederives` checks that a stored full tuple re-derives
both the actor identifier and the reminder name. A mismatch is a collision or
corrupted evidence. It is quarantined and never executed.

## Registration

`ConvergeAsync(target)` runs inside the item's serialized reminder actor turn:

1. Re-fold the target's current intents.
2. Quarantine malformed intents by digest. Examples are a target mismatch, an
   unsupported kind, a non-UTC due instant, a negative revision, a non-positive
   source sequence, a missing payload, or a non-canonical identity. Malformed
   evidence is never dropped.
3. Quarantine two different intents that map to one reminder name as
   `witness-collision`, and a stored witness whose name now carries different
   evidence. Quarantine intents with different names that share one effect
   identity as `effect-collision`. None of them is submitted.
4. Write the tenant registry and the tenant candidate index by compare-and-swap,
   before anything is scheduled. This runs whenever the item holds work, so an
   index restored from an older backup regains its candidates; it writes
   nothing when the candidate is present.
5. Persist the item's witnesses by compare-and-swap. A lost race fails closed:
   nothing is scheduled and the work is reported unresolved. If the item had no
   persisted state yet, for example when the index is full or its update budget
   is exhausted, convergence throws instead, so the caller retries.
6. Cancel obsolete reminders and submit due ones through the callback path. A
   `Retrying` witness inside its backoff window is left to its armed backoff
   reminder. Arm a future witness as a periodic reminder only when it is not
   yet `Armed` or the Scheduler no longer holds its reminder.
7. When the item holds nothing more, erase its state and remove its index entry
   last.

Duplicate registration is idempotent. A new schedule revision or due instant
produces a new name, and the obsolete reminder is cancelled. A Scheduler
failure leaves the witness `Pending`, indexed, and unresolved until a later
convergence re-arms it.

## Callback admission and submission

Callback admission runs in this order:

1. **App channel.** The request must carry a `dapr-api-token` header equal to
   `APP_API_TOKEN`. The token is required outside Development, and the filter
   fails closed without it. The filter guards every path in which an `actors`
   segment is followed by the actor type name, wherever it appears, so a host
   path base cannot hide it. That covers method calls and reminder callbacks. A
   denial returns an empty `401` and logs a reason code only.
2. **Stored identity.** The stored full tuple must re-derive the actor
   identifier and the reminder name. A mismatch is quarantined.
3. **Purpose.** The name prefix already matched the stored kind. That kind also
   needs a configured purpose. Without one, the callback is `Denied`, the work
   is retained, and a backoff reminder is re-armed.
4. **Currency.** The intent source must still report the exact witness. A
   superseded witness is audited as `Stale`. Nothing is submitted for that
   witness. The item re-converges with the intents just re-folded, indexing and
   persisting replacements while the stale witness and its Scheduler reminder
   are still held. Cancellation then releases the stale witness. If
   more than one current intent carries the name with different evidence, or
   another current intent shares the effect identity, the witness is
   quarantined before any submission.

A callback without a persisted witness mutates and discloses nothing. The
scheduler is asked to cancel it.

Submission builds `EffectIdentity(tenant, source domain, source aggregate,
source sequence, kind, target domain, target aggregate, 0)`. Both `MessageId`
and `IdempotencyKey` are `wrk-<EffectId>`. The causation identifier is the
reminder name. The workload comes from `EventStoreReminderOptions.Workload`.
A translation failure is quarantined, not retried.

A durable receipt (`Success`, `Rejection`, or `NoOp`) releases the witness in
this order:

1. Write the `Submitted` audit record with the effect identifier.
2. Cancel the Scheduler reminder successfully.
3. Delete the pending witness.
4. Re-fold the stream before removing the index entry. Retain discovery when
   the stream still reports intents or the fold is unavailable.

If the audit write or Scheduler cancellation fails, the witness stays and
counts as unresolved. A later retry replays the same receipt. An exception,
a mismatched receipt, or a missing submitter or
delegation keeps the witness as `Retrying`. It re-arms a backoff reminder that
doubles from `RetryInitialDelay` up to `RetryMaxDelay`, and the work counts as
unresolved. A callback never reports failure by throwing. Resubmitting the same
witness after a restart returns the same effect identifier with `Replayed = true`.

If the final stream fold returns null or throws after a receipt or stale
disposition is settled, the discovery candidate stays and counts as unresolved.
Readiness remains `Degraded` until a later convergence can re-fold the stream.

Timing belongs to the Scheduler. The callback authenticates origin and witness;
it does not re-check the due instant.

## Reconciliation

`ReminderReconciler` is a hosted service. Its first pass starts with the host,
and later passes run every `ReconciliationInterval`. After an incomplete pass,
the next one runs after `RetryInitialDelay`. Each pass reads the tenant
registry and every tenant candidate document, then converges every candidate
from its stream. A lost firing is reissued when due. A deleted Scheduler
reminder is re-armed when still in the future. One unreadable tenant or stream
never blocks the others; the pass is recorded as incomplete. A candidate
document stored under one tenant's key that names another tenant is refused.

## Host composition

```csharp
var builder = WebApplication.CreateBuilder(args);
builder.AddEventStoreDomainService();
// ActorTypeName is required and must be unique to this application; it can also come from configuration.
// Purposes has no default: bind one for every kind this host submits, or every callback is denied.
builder.Services.AddEventStoreReminders<MyReminderIntentSource>(options =>
{
    options.ActorTypeName = "OrderReminderActor";
    options.Purposes["works.date-resume.v1"] = "order-date-resume";
});
builder.Services.AddSingleton<IReminderDelegationTokenProvider, PlatformDelegationProvider>();
// The base address is this app's own Dapr sidecar; the invocation handler targets the gateway app ID.
builder.Services.AddHttpClient<ITrustedEffectSubmitter, HttpTrustedEffectSubmitter>(
        client => client.BaseAddress = new Uri(
            $"http://localhost:{builder.Configuration["DAPR_HTTP_PORT"] ?? "3500"}"))
    .AddEventStoreDaprServiceInvocation("eventstore", builder.Configuration["DAPR_API_TOKEN"]);
var app = builder.Build();
app.UseEventStoreDomainService();
app.Run();
```

`UseEventStoreDomainService` maps the Dapr actor routes only when reminders are
registered. It skips them when the host already mapped the actor handlers. A
host that maps the actor handlers itself must call `MapActorsHandlers` before
`UseEventStoreDomainService`; mapping them afterwards maps the actor routes a
second time and makes them ambiguous. The app-channel filter is installed by
the registration itself, so it also guards a host that maps the actor handlers
on its own.

| Key under `EventStore:Reminders` | Default | Meaning |
| --- | --- | --- |
| `ActorTypeName` | none; required | Dapr actor type, unique to this application because actor types are global under Dapr placement. It also scopes every persisted key, so changing it abandons existing state |
| `StateStoreName` | `statestore` | State store for witnesses, index, and dispositions |
| `Workload` | `DAPR_APP_ID`, then the application name | Workload named in the trusted-effect context |
| `Purposes:<kind>` | none | Named delegated purpose per kind. A kind without one is denied |
| `ReconciliationEnabled` | `true` | Runs the periodic reconciler |
| `ReconciliationInterval` | `00:05:00` | Interval between complete passes |
| `RetryInitialDelay` | `00:00:30` | First retry delay, and the delay after an incomplete pass |
| `RetryMaxDelay` | `00:15:00` | Longest retry delay, and the period of every armed reminder. It and the two other delays must not exceed 4294967294 milliseconds |
| `MaxCandidatesPerTenant` | `10000` | A full tenant index fails registration closed |
| `IndexWriteAttempts` | `8` | Compare-and-swap budget for one index update |

The host must also configure `APP_API_TOKEN` outside Development, and its
sidecar must present the same token. On Azure Container Apps the platform
injects the token and its managed sidecar sends it, so operators do not set
their own.

## Persisted state

Every key starts with `eventstore:reminders:v1:{ActorTypeName}`.

| Key suffix | Record | Contents |
| --- | --- | --- |
| `:control:tenants` | Tenant registry | Canonical tenants that hold candidates |
| `:tenant:{tenant}:candidates` | Candidate index | Domain, aggregate, and actor identifier per candidate |
| `:item:{wra-id}` | Item state | Tenant, domain, aggregate, write version, witnesses, and quarantine evidence |
| `:item:{wra-id}:disposition:{subject}` | Audit disposition | Latest disposition per reminder name or evidence digest, with reason code, effect identifier, target disposition, replay flag, and attempts |

A witness holds the reminder name, schedule token, kind, due instant,
revision, source coordinates, payload type, payload digest, status
(`Pending`, `Armed`, `Retrying`, or `Quarantined`), attempts, and last reason
code. Logs carry actor identifiers, reminder names, effect identifiers, reason
codes, and counts only: never tenant or aggregate identifiers, payloads,
tokens, or exception messages.

## Operator runbook

### Readiness

The `eventstore-reminders-unresolved` check carries the `ready` tag. For
reminder work it reports `Degraded`, never `Unhealthy`, so `/ready` stays `200`
and one tenant cannot pull the service out of rotation. Its data holds
`passCompleted`, `incompleteScans`, `unresolvedItems`, `unresolved`,
`quarantined`, and `callbackTokenConfigured`.

It is `Unhealthy`, as the gateway is, only when `APP_API_TOKEN` is missing
outside Development. Every reminder actor call is then refused with `401`.
Configure the token on the app and its sidecar.

It is `Degraded` in any of these cases:

| Description | Cause | Action |
| --- | --- | --- |
| No reconciliation pass has completed yet | The host just started, or reconciliation is disabled | Wait one pass, or enable reconciliation |
| The last pass was incomplete | A tenant index, candidate, or stream could not be read | Check the `200211` and `200212` logs; the next pass retries after `RetryInitialDelay` |
| Quarantined evidence awaits disposition | A collision, a tampered tuple, malformed evidence, or a failed translation | Follow the quarantine steps below |
| Work is retained without a durable outcome | Arming failed, or submission was uncertain, denied, or unavailable | Check the last reason code on the witness; the work retries automatically |

The view is per host. Each host rebuilds it from durable state on its first
pass after a restart.

### Retained and denied work

`Retrying` witnesses carry their last reason code. The codes are
`submission-uncertain`, `receipt-mismatch`, `submitter-unavailable`,
`delegation-unavailable`, `delegation-failed`, `purpose-unconfigured`,
`workload-unconfigured`, `source-unavailable`, `audit-unavailable`,
`cancel-failed`, `domain-invalid`, and `arm-failed`. Fix the cause; the next firing or pass resubmits under the same effect identifier. A
target that already holds the receipt replays it, so retries never create a
second logical effect.

### Quarantine

Quarantine reason codes are `tuple-mismatch`, `witness-collision`,
`effect-collision`, `actor-collision`, `translation-failed`, `translation-invalid`,
`effect-identity-invalid`, `domain-invalid`, `arm-failed`, and the malformed-intent codes `intent-missing`,
`target-mismatch`, `kind-unsupported`, `due-not-utc`, `revision-invalid`,
`source-sequence-invalid`, `payload-invalid`, and `identity-invalid`. Malformed
or duplicate restored state uses `stored-entry-invalid`,
`stored-entry-duplicate`, or `stored-quarantine-invalid`.

1. Find the item from the `200207` log, which carries the `wra-` actor
   identifier and the subject. The subject is the reminder name or a 52-character
   evidence digest.
2. Read `:item:{wra-id}` and its `:disposition:{subject}` record. Compare the
   stored tuple with the domain stream; the payload is only in the stream.
3. Fix the producing domain code or data through the domain's own commands.
   EventStore does not repair domain streams.
4. Dispose of the quarantine through the audited Platform operator path, which
   Story 4.16 delivers. Until then, only synthetic environments may remove the
   quarantined witness or record directly from the item state. The next
   convergence re-derives everything from the stream.

Quarantined evidence stays in the item state and keeps readiness `Degraded`
until it is disposed of. Later firings and convergence never submit it.

## Production gate

Production admission stays closed. Every proof so far uses synthetic data, as
AD-28 requires. Before any real data is admitted:

- The Platform Workload Delegation Service must implement
  `IReminderDelegationTokenProvider`.
- The trusted-effect authority rules must name each reminder workload, purpose,
  target domain, and command type.
- The Platform append-only audit backend must receive reminder dispositions.
- The mTLS and ACL caller path must be attested. Only the Scheduler, through the
  sidecar, may invoke the reminder actor type.
- The accountable data owner must approve the reminder state as a durable type.
- A restore drill must prove the order of stream, index, item state,
  dispositions, and target receipts, with Scheduler backup owned by Platform.

Story 4.16 owns these proofs.

## Evidence handed to Story 4.15

Works adopts this seam in Story 4.15:

- Implement `IReminderIntentSource` for `DateResume` and `Expiry` from the
  Work Item stream.
- Call `IReminderRegistrar.ConvergeAsync` from the committed-event handlers that
  change a schedule.
- Set `ActorTypeName` to `WorkItemReminderActor` and configure both purposes.
- Retire `DateReminderActor`, `DateReminderReconciler`, and the startup-only
  `ReminderReconciliationService` only after the 4.15 to 4.16 parity proof.

Producer evidence:

| Proof | Command or test |
| --- | --- |
| Codec golden vectors, collision detection, closed kind map, unchanged AD-26 vectors | `Hexalith.EventStore.Contracts.Tests -class '*Reminder*'` |
| Register/reschedule, callback replay, stale/forged admission, recovery, restore/HA over persisted fake-store state | `Hexalith.EventStore.DomainService.Tests -class '*Reminder*'` |
| Redis/Dapr registration, direct callback, restart replay, Scheduler re-arm | `Hexalith.EventStore.Server.LiveSidecar.Tests -class '*ReminderRecoveryLiveSidecarTests*'` |
| Package-only consumers with no Works types | `EVENTSTORE_PACKAGE_CONTRACT_DIR=<dir> Hexalith.EventStore.Contracts.Tests -method '*PackagedReminderApi*'` |

The release record names the public package version, source SHA, and
package-only results. 4.15 must consume that version or a later one.
