[← Back to Hexalith.EventStore](../../README.md)

# Event payload versioning and upcasting

An event is immutable after EventStore persists it. Declare a new payload version and provide JSON upcasters when current code needs a different shape. EventStore retains the original bytes and identity; the domain service transforms a copy before deserializing it for command rehydration, replay, projections, or subscriptions.

## Declare and write a version

```csharp
using Hexalith.EventStore.Contracts.Events;

[EventPayloadVersion(2)]
public sealed record CounterIncremented(int IncrementedBy) : IEventPayload;
```

An event without the attribute, or with `[EventPayloadVersion(1)]`, remains version 1 and has no stored `PayloadVersion` field. A version 2 event is written with `PayloadVersion = 2` and metadata version 1. Valid declared versions are 1 through 1024. Versioned event payloads must be JSON objects. A custom `ISerializedEventPayload` must declare the same version as its event type and provide JSON bytes.

The server stores and forwards the declared version without upcasting. Stream and admin views display stored events. Neither the original payload bytes nor its event identity change during a read.

## Changes that need no new version

Changes that preserve the meaning and JSON shape consumed by every deployed reader, such as internal refactoring, can keep the current version. An added property needs both directions of compatibility: old readers must safely ignore it in new events, and new readers must supply a safe default when reading old stored events that lack it. Rename a property, change its type or meaning, or remove a property only with a new version and a complete upcaster chain. Check every Apply, projection, and subscription consumer before deciding that a shape change is safe.

## Write a pure upcaster

Each `IEventPayloadUpcaster` transforms one version to the next. Register one step for every historical version up to the current declaration. The SDK discovers public and non-public upcasters with parameterless constructors from the assemblies it scans for aggregate and projection types or subscriber contracts. You can also register one explicitly with `AddEventPayloadUpcaster<T>()`. Discovery and explicit registration of the same type count once.

A projection handler that consumes `ProjectionEventDto` directly and has no typed `Apply` method can register its consumed event type with `AddKnownEventPayload<T>()`, so the registry validates and upcasts it before the handler runs.

```csharp
using System.Text.Json.Nodes;
using Hexalith.EventStore.Client.Events;

internal sealed class CounterIncrementedV1ToV2 : IEventPayloadUpcaster
{
    public string EventTypeName => typeof(CounterIncremented).FullName!;
    public int FromVersion => 1;

    public JsonObject Upcast(JsonObject payload)
    {
        payload["IncrementedBy"] = 1;
        return payload;
    }
}
```

An upcaster must be deterministic and use no clock, random values, I/O, or scoped service. It receives a JSON object with case-insensitive property lookup. A version 1 event needs a 1→2 step before a version 2 CLR type can read it; a version 3 type also needs a 2→3 step. Startup rejects duplicate, incomplete, invalid, or dangling chains. A bad stored version, malformed JSON, or failing upcaster produces a typed read error before a handler or checkpoint advances.

## Rename an event

Set `TargetEventTypeName` on the step that changes the name. The target must resolve to a registered Apply, projection, or subscriber event type. The step emits that name at `FromVersion + 1`:

```csharp
public string EventTypeName => "Old.Contracts.CounterRaised";
public int FromVersion => 1;
public string TargetEventTypeName => typeof(CounterIncremented).FullName!;
```

A later step uses the target name. Keep historical names in the upcaster chain even if the old CLR type has been retired. The SDK resolves full, short, and anchored alias names and runs a registered rename before treating an old CLR type as terminal.

## Apply method resolution

The SDK resolves `Apply(TEvent)` by an exact CLR full name, then an exact short name, then the longest boundary-anchored suffix of a stored name. A short-name collision can still resolve through exact full names. For known `IEventPayload` types, ambiguous resolution raises `EventPayloadEvolutionException` and replay reports `UnsupportedVersion`. Other Apply types can raise `AmbiguousApplyMethodException`. Use distinct full names in the stored stream and avoid ambiguous aliases. An upcaster rename can route an old stored name to one unambiguous current type. Subscription reads accept exact full names and registered historical upcaster aliases; an unrelated event with the same short name stays unknown.

## Domain service version routing

The resolver checks registrations in this order for the requested domain service version:

1. exact static registration keyed by `tenant:domain:version`
2. exact static registration keyed by `tenant|domain|version`
3. pipe wildcard static registration keyed by `*|domain|version`
4. sanitized wildcard static registration keyed by `wildcard_{domain}_{version}`
5. opt-in DAPR config-store lookup when `ConfigStoreName` is non-empty
6. convention fallback: `AppId = domain`, `MethodName = "process"`

See the [configuration reference](../guides/configuration-reference.md#domain-services) for supported key formats and deployment settings.

For a staged rollout, register the old and new domain service versions at the same time and route each caller to its intended version. Retain the old route until callers have moved. A rollback can select that route only while the old service can read every event it may encounter; after version 2 events are written, roll back to a release that understands payload versions and has the required upcasters. Do not send version 2 writes to an older server replica.

## Deploy in order

1. Deploy the EventStore server release that supports payload versions to **every server replica**. Older replicas can discard a new `PayloadVersion` permanently or reject its write.
2. Deploy the matching SDK release to **every consumer replica**: domain services, projections, and subscriber hosts.
3. Deploy the new event declaration and complete upcaster chain. A consumer replica still on the previous release refuses a versioned event and retries until it is upgraded.

A subscription that cannot upcast returns HTTP 503 and leaves the event uncompleted. Configure Dapr resiliency `maxRetries` and a dead-letter topic so a persistent incompatibility is visible and can be redelivered after correction. Do not acknowledge an unreadable known event as successful.

An unreadable known event raises `EventPayloadEvolutionException`. Replay reports `UnsupportedVersion` for version or upcaster failures; a malformed current-version payload found during replay deserialization reports `DeserializationFailed`. Live `/process` surfaces an uncaught evolution error as HTTP 500. Projection dispatch stops before invoking the handler or advancing its checkpoint, and `/project/v2` surfaces the failure as HTTP 500. A shared rebuild accumulate failure returns `Indeterminate` with reason `HandlerFailure` and does not advance. Deploy a corrected consumer and retry the event.

## Identity and compatibility

New writes must have valid tenant, domain, aggregate ID, aggregate type, event type name, ULID message ID, correlation ID, causation ID, and positive sequence. Reads enforce the same grammar, while also accepting historical 36-character GUID message IDs. Correlation and causation IDs contain 1–128 ASCII letters, digits, or hyphens. Invalid stored identity fails before application.

Metadata version 2, `EventContractType`, and the first-attempt effective/proof fields remain unsupported. Existing version 1 envelopes remain unstamped and retain their original bytes.

See [Event Envelope & Metadata](event-envelope.md) for the stored envelope and [Command Lifecycle](command-lifecycle.md) for state rehydration.
