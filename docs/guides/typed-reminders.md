# Typed reminder reconciliation

This is the AD-20 R6 producer seam. A domain module translates its committed
events into typed reminder intents. EventStore persists the scheduler state,
arms and cancels Dapr actor reminders, and submits due reminders through the
trusted-effect receipt path described in [Trusted effect submission](trusted-effects.md).
Platform owns the production Scheduler, its availability, and its backup policy.

Streams are authoritative for current intents and submissions. The discovery
index only finds candidates. Registration and current-witness callbacks
re-fold the target stream through the domain's intent source. Orphaned,
already-quarantined, repaired-quarantine, and structurally invalid callbacks
can cancel reminders from persisted evidence without a stream fold.

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
- `(source domain, source aggregate, source sequence, kind, target)` must be
  unique among a target's current intents. Intents that share it share one
  effect identity, and both are quarantined as `effect-collision`. Two current
  intents with different evidence under one name are recorded as
  `witness-collision` even when that name also shares an effect identity.
  Convergence records a changed stored witness whose name participates in an
  effect collision as `effect-collision`; callback admission records its changed
  evidence as `witness-collision`.
- Retain the source coordinates when retrying the same logical submission.
  For the same kind and target, a distinct logical submission needs distinct
  committed source-event coordinates throughout the lifetime of the stream.
  Changing only the due instant or schedule revision retains the previous
  effect identity and replays its receipt or conflicts with changed command
  semantics.
- Distinct current witnesses must derive distinct reminder names. The name
  binds the tenant, target aggregate, kind, due instant, and schedule revision.
  Change the schedule witness when an existing intent's source coordinates,
  payload type, or payload change; different evidence under the same name
  is quarantined as `witness-collision`.
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
   identity as `effect-collision`. Two current intents with different evidence
   under one name are recorded as `witness-collision` even when that name also
   shares an effect identity. A changed stored witness whose name participates
   in an effect collision is recorded as `effect-collision` during convergence.
   None of them is submitted.
4. Write the tenant registry and the tenant candidate index by compare-and-swap,
   before anything is scheduled. This runs whenever the item holds work, so an
   index restored from an older backup regains its candidates; it writes
   nothing when the candidate is present.
5. Audit obsolete and newly quarantined witnesses, then cancel their Scheduler
   reminders before persisting the updated witnesses. An obsolete witness stays
   until its audit and cancellation both succeed. A failed quarantine audit
   retains the executable witness for retry.
6. Persist the item's witnesses by compare-and-swap. A lost race fails closed:
   the refused write leaves durable state unchanged and the work is reported
   unresolved. Earlier Scheduler cancellations may already have succeeded;
   recovery converges the retained work again with the same effect identity.
   If the item had no
   persisted state yet, for example when the index is full or its update budget
   is exhausted, convergence throws instead, so the caller retries.
7. Submit due witnesses through the callback path. A `Retrying` witness inside
   its backoff window is left to its armed backoff
   reminder. Arm a future witness as a periodic reminder only when it is not
   yet `Armed` or the Scheduler no longer holds its reminder.
8. When the item holds nothing more, erase its state and re-fold the stream.
   Remove its index entry last only when the fold reports no current intents.

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
   identifier and the reminder name. Loading the item validates this first and
   quarantines a mismatch as `stored-entry-invalid`. The callback repeats the
   check as defense in depth.
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

A callback without a persisted witness writes no callback disposition and
discloses nothing. Loading the item may first persist the deterministic
quarantine of corrupt stored entries. The Scheduler is asked to cancel the
orphaned reminder.

Submission builds `EffectIdentity(tenant, source domain, source aggregate,
source sequence, kind, target domain, target aggregate, 0)`. Both `MessageId`
and `IdempotencyKey` are `wrk-<EffectId>`. Delegation and submission use that
same stable logical-effect identifier as causation, so a same-source reschedule
can replay a receipt committed before an uncertain response. The reminder name
remains the schedule witness for registration and callback admission. Changed
command semantics under the same effect identity still conflict with its receipt.
The workload comes from `EventStoreReminderOptions.Workload`.
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
and later passes normally run every `ReconciliationInterval`. A pass with an
incomplete scan or a candidate convergence that throws waits the minimum of
`RetryInitialDelay` and `ReconciliationInterval` before the next pass.
Each pass reads the tenant registry and every tenant candidate document,
then converges every candidate
from its stream. A lost firing is reissued when due. A deleted Scheduler
reminder is re-armed when still in the future. One unreadable tenant or failed
candidate never blocks the others; these failures make the pass incomplete.
Convergence can instead return retained `Unresolved` or `Quarantined` outcomes,
which degrade readiness without making the scan incomplete. For example, a
null stream fold with durable work retains its witnesses and discovery,
returns `Unresolved`, and keeps the normal interval. A candidate
document stored under one tenant's key that names another tenant is refused.
A candidate document holding `MaxCandidatesPerTenant` entries also makes the
pass incomplete and readiness `Degraded`: a rejected first registration may
have no durable item state for the pass to discover. Existing candidates still
converge, and a later pass clears the condition once the index has capacity.
Capacity alone keeps the normal `ReconciliationInterval`; it does not put the
host on the shorter retry cadence.

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
// The submission proves this domain service's own workload identity; the Dapr handler stays innermost (AD-18).
builder.Services.AddHttpClient<ITrustedEffectSubmitter, HttpTrustedEffectSubmitter>(
        client => client.BaseAddress = new Uri(
            $"http://localhost:{builder.Configuration["DAPR_HTTP_PORT"] ?? "3500"}"))
    .AddEventStoreTrustedEffectWorkloadAssertion()
    .AddEventStoreDaprServiceInvocation("eventstore", builder.Configuration["DAPR_API_TOKEN"]);
var app = builder.Build();
app.UseEventStoreDomainService();
app.Run();
```

`UseEventStoreDomainService` maps the Dapr actor routes only when reminders are
registered. It skips them when the host already mapped the actor handlers. A
host that maps the actor handlers itself must call
`MapActorsHandlers().RequireEventStoreSidecarChannel()` before
`UseEventStoreDomainService`; mapping them afterwards maps the actor routes a
second time and makes them ambiguous, and mapping them without the
sidecar-channel policy fails startup. The app-channel filter is installed by
the registration itself, so it also guards a host that maps the actor handlers
on its own.

### Trusted-effect submission credentials

Every due reminder is submitted to `POST /api/v1/trusted-effects`, an internal
call that EventStore admits only from an authenticated workload (Story 5.5).
`AddEventStoreTrustedEffectWorkloadAssertion()` attaches this domain service's
own short-lived assertion: caller (`azp`) = the domain service, audience
`eventstore`, operation `eventstore:trusted-effect`. Without it every
submission receives `401`, nothing is admitted, and the reminder stays
retained. Provision three things for each submitting domain service:

1. **Allow-list it on EventStore.** Add its workload identity to
   `Authentication:DaprInternal:AllowedCallers`. In an Aspire AppHost, call
   `eventStore.WithEventStoreTrustedEffectSubmitter("<app-id>")`.
2. **Give it a credential from the trusted issuer.** In symmetric
   `Development` mode it signs with the shared `Authentication:JwtBearer`
   key it already uses, and its identity is
   `Authentication:WorkloadIssuer:Workload` (default
   `EventStore:DomainService:AppId`). In authority mode it needs its own
   confidential, service-account-only client whose `azp` is its app ID, set
   as `Authentication:WorkloadIssuer:ClientId` and `ClientSecret` (in an
   AppHost, `WithEventStoreWorkloadClientCredentials`). That client may
   request only the optional `eventstore-audience.eventstore` and
   `eventstore-operation.eventstore.trusted-effect` scopes.
3. **Keep the token short-lived.** The token lifetime must not exceed
   EventStore's `Authentication:DaprInternal:MaximumLifetimeSeconds`
   (default 300 seconds).

The local AppHost topology has no trusted-effect submitter, so it allow-lists
no internal caller.

| Key under `EventStore:Reminders` | Default | Meaning |
| --- | --- | --- |
| `ActorTypeName` | none; required | Dapr actor type, unique to this application because actor types are global under Dapr placement. It also scopes every persisted key, so changing it abandons existing state |
| `StateStoreName` | `statestore` | State store for witnesses, index, and dispositions |
| `Workload` | `DAPR_APP_ID`, then the application name | Workload named in the trusted-effect context |
| `Purposes:<kind>` | none | Named delegated purpose per kind. A kind without one is denied |
| `ReconciliationEnabled` | `true` | Runs the periodic reconciler |
| `ReconciliationInterval` | `00:05:00` | Normal interval, including capacity-only incompleteness and retained unresolved outcomes |
| `RetryInitialDelay` | `00:00:30` | First submission retry delay; incomplete scans or failed candidate convergence wait the minimum of this and `ReconciliationInterval` |
| `RetryMaxDelay` | `00:15:00` | Longest retry delay, and the period of every armed reminder. It and the two other delays must not exceed 4294967294 milliseconds |
| `MaxCandidatesPerTenant` | `10000` | A full tenant index fails registration closed and degrades readiness while keeping the normal scan interval |
| `IndexWriteAttempts` | `8` | Compare-and-swap budget for one index update |

Options validation requires a non-blank `StateStoreName` and an `ActorTypeName`
matching `[A-Za-z][A-Za-z0-9_-]{0,63}`. `IndexWriteAttempts` must be 1–100 and
`MaxCandidatesPerTenant` at least 1. `ReconciliationInterval` and
`RetryInitialDelay` must be positive, and `RetryInitialDelay` must not exceed
`RetryMaxDelay`. All three delays must stay within 4294967294 milliseconds.
`Purposes` accepts only `works.date-resume.v1` and `works.expiry.v1`, each with
a non-blank value; a purpose for any other kind fails startup validation.

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
| A tenant index is full or over capacity | A refused first registration may have no durable item state to discover | Free capacity and redeliver refused registrations; existing candidates still converge at `ReconciliationInterval` |
| The last pass had an incomplete scan or a candidate convergence threw | An index or candidate could not be read or validated, or convergence failed | Check the `200211` and `200212` reason codes; the next pass waits the minimum of `RetryInitialDelay` and `ReconciliationInterval` |
| Quarantined evidence awaits disposition | A collision, a tampered tuple, malformed evidence, or a failed translation | Follow the quarantine steps below |
| Work is retained without a durable outcome | Arming failed, submission was uncertain, denied, or unavailable, or a null stream fold retained durable work | Check the witness's `LastReasonCode` where present, or event `200214` and retained discovery; retained outcomes keep the normal reconciliation interval |

The view is per host. Each host rebuilds it from durable state on its first
pass after a restart.

### Fail-closed registration and discovery

Events `200214` (state change), `200211` (candidate), and `200212` (scan)
carry the bounded `ReasonCode` separately from `ExceptionType`. The index is
only a discovery aid; preserve malformed evidence while comparing it with the
authoritative streams.

| Reason code | Action |
| --- | --- |
| `state-conflict` | Retry convergence against the winning durable state. Investigate repeated competing writes; target receipts retain the same effect identity |
| `index-conflict` | Restore state-store availability or resolve CAS contention, then redeliver any refused first registration |
| `index-capacity` | Free candidate capacity or review the configured limit, then redeliver refused registrations. Readiness stays Degraded and capacity-only scans keep the normal interval |
| `index-registry-invalid` | Preserve the present registry with its null tenant collection and repair it through the audited operator path |
| `index-candidates-invalid` | Preserve the present tenant document with its null candidate collection and repair it through the audited operator path |
| `index-tenant-mismatch` | Compare the candidate document's stored tenant with its key and repair the mismatch; scanning never crosses tenants |
| `candidate-missing` | Preserve the malformed candidate row and recover its coordinates from the stream and retained evidence |
| `actor-id-mismatch` | Compare the candidate's tenant and aggregate with its derived actor identifier before operator repair; do not discard the only stored coordinates |

Story 4.16 supplies the audited production repair path. Direct repair is
limited to synthetic environments until that gate is satisfied. A malformed
discovery document keeps the pass incomplete; registration also refuses to
overwrite its null collection.

A `Stale` disposition with reason `witness-not-current` records that the
stream no longer reports the fired witness. It is an audited no-op for that
witness; replacement convergence proceeds before the obsolete reminder is
cancelled. Compare the current stream and replacement witness when checking
this expected reschedule outcome.

### Retained and denied work

`Pending` witnesses use `arm-failed` when the Scheduler could not arm them.
Fix the Scheduler; the next convergence retries arming.

`Retrying` witnesses carry their last reason code. The codes are
`submission-uncertain`, `receipt-mismatch`, `submitter-unavailable`,
`delegation-unavailable`, `delegation-failed`, `purpose-unconfigured`,
`workload-unconfigured`, `source-unavailable`, `audit-unavailable`,
`cancel-failed`.

A null fold during convergence can retain an unchanged witness without
setting its `LastReasonCode`; inspect event `200214` for `source-unavailable`.
Cleanup can also retain only the discovery candidate after the target receipt,
audit, Scheduler cancellation, and witness release succeeded. In that case
there is no witness `LastReasonCode` to inspect. Correlate event `200214` with
the retained candidate, disposition, and target receipt, restore stream-read
availability, and converge the candidate again. A successful empty fold then
releases discovery; preserve the candidate until that check succeeds.

Submission retries re-fold the stream and use the same effect identifier while
the intent remains current. A target that already holds the receipt replays it,
so these retries never create a second logical effect. Retained work can also
await an audit write, cancellation of an obsolete or stale reminder, or a
quarantine transition. Obsolete and stale witnesses retry retirement without
submitting their commands; quarantine transitions retry their audit and
quarantine. A witness with a durable target receipt may resubmit the same effect
to replay that receipt while retrying its audit or cancellation cleanup.
Quarantined witnesses are never submitted. Check the stored witness and its
disposition to identify the pending operation, then fix its dependency; the next
firing or convergence retries that operation.

### Quarantine

Quarantine reason codes are `witness-collision`,
`effect-collision`, `actor-collision`, `translation-failed`, `translation-invalid`,
`effect-identity-invalid`, `domain-invalid`, and the malformed-intent codes
`intent-missing`, `target-mismatch`, `kind-unsupported`, `due-not-utc`, `revision-invalid`,
`source-sequence-invalid`, `payload-invalid`, and `identity-invalid`. Malformed
or duplicate restored state uses `stored-entry-invalid`,
`stored-entry-duplicate`, or `stored-quarantine-invalid`.
The callback's `tuple-mismatch` branch is defense in depth after load-time
validation. A failed `domain-invalid` quarantine audit retains the witness as
`Retrying` with reason `audit-unavailable` until its audit can be written.

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
- The mTLS and ACL caller paths must be attested. Only the Scheduler, through
  the sidecar, may invoke reminder callbacks. Trusted domain committed-event
  handlers and the reconciler must be admitted to `ConvergeAsync` through the
  authorized actor invocation path. Story 4.16 owns the production policy proof
  for both paths.
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
