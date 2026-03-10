[<- Back to Hexalith.EventStore](../../README.md)

# Event Envelope & Metadata

Every domain event in Hexalith.EventStore is wrapped in an **event envelope** — a container that pairs your business fact (the payload) with metadata that provides traceability, ordering, multi-tenancy, and correlation.

This page explains the complete envelope structure, shows you a real example from the Counter domain, and describes how EventStore populates each metadata field automatically. By the end, you will know exactly what gets persisted when your domain service produces an event, how to interpret envelopes when consuming events downstream, and why the envelope design makes specific trade-offs in favor of consistency, traceability, and durability.

> **Prerequisites:**
>
> - [Architecture Overview](architecture-overview.md) — DAPR topology and building blocks
> - [Command Lifecycle Deep Dive](command-lifecycle.md) — the 5-step processing pipeline that produces event envelopes

## What Is an Event Envelope?

An event envelope wraps every domain event with metadata that the EventStore populates automatically. You write only the domain event payload — the business fact, like "counter incremented by 1" — and the EventStore adds the 11 metadata fields that provide traceability, ordering, multi-tenancy, and correlation.

Think of the envelope like a physical letter: your domain event is the letter inside, and the metadata fields are the postmark, return address, tracking number, and recipient address printed on the outside. Just as a postal system needs those markings to route, track, and deliver the letter, EventStore needs the metadata to persist events in the right stream, maintain strict ordering, isolate tenants, and enable distributed tracing across services.

Once an envelope is persisted, it is immutable. The metadata and payload form a permanent record of what happened, when, to which aggregate, and who caused it. This immutability is a core property of event sourcing — you never update or delete persisted events, you only append new ones. The complete history of every aggregate is preserved as an ordered sequence of immutable envelopes, and the current state is always reconstructable by replaying that sequence.

You never construct an EventEnvelope yourself — the EventStore builds it during the persist step of the AggregateActor pipeline. Your domain service's `Handle` method returns plain event objects (implementing `IEventPayload`), and EventStore takes care of wrapping, numbering, timestamping, and persisting them. This means you can write and test your domain logic in complete isolation — your `Handle` method knows nothing about envelopes, metadata, or persistence infrastructure.

You encounter EventEnvelopes in three main scenarios:

- **Event subscribers** — when building projections or read models that react to published events
- **Integration tests** — when asserting on the events that were persisted after a command
- **Debugging** — when inspecting event streams directly in the DAPR state store

In each case, the envelope gives you the full context of each event: who caused it, when it happened, which aggregate stream it belongs to, and where it fits in the sequence. You read the metadata to understand the event's provenance without needing to deserialize the payload. This is one of the key advantages of the envelope design — you can filter, route, trace, and audit events purely from metadata, deferring the more expensive payload deserialization until you actually need the business data.

## Anatomy of an Event Envelope

An event envelope is organized into three layers:

- **Metadata** — 11 typed fields that identify, order, and trace the event
- **Payload** — the serialized domain event as an opaque byte array
- **Extensions** — an open metadata bag for domain-specific needs

The metadata layer is the most important for day-to-day development. It tells you which aggregate produced the event, which tenant it belongs to, where it fits in the event stream, and how to trace it back to the originating HTTP request and command.

The payload carries your actual business data — the domain event itself, serialized to bytes. EventStore never inspects the payload, which means you can evolve your event schemas independently of the envelope structure.

The extensions provide an escape hatch for domain-specific metadata that EventStore does not need to understand. They pass through the system untouched, giving you a safe place to attach custom context without modifying the core envelope schema.

### Metadata Fields

The 11 metadata fields fall into three logical groups:

- **Identity fields** — `aggregateId`, `tenantId`, `domain`: locate the event in the multi-tenant, multi-domain aggregate topology. These three fields uniquely identify which aggregate instance produced the event and which tenant owns it.

- **Ordering and timing fields** — `sequenceNumber`, `timestamp`: position the event in its aggregate stream and on the global timeline. The sequence number provides strict ordering within one aggregate's stream; the timestamp provides a wall-clock reference for cross-aggregate and cross-service event correlation.

- **Tracing and provenance fields** — `correlationId`, `causationId`, `userId`, `domainServiceVersion`, `eventTypeName`, `serializationFormat`: trace the event back to the request, command, user, and service version that produced it. These fields power distributed tracing, audit logging, and version-aware deserialization.

| Field | Type | Description | Example |
|-------|------|-------------|---------|
| `aggregateId` | string | Full canonical identity (`{tenant}:{domain}:{id}`) | `"demo:counter:counter-1"` |
| `tenantId` | string | Tenant isolation key (lowercase, validated) | `"demo"` |
| `domain` | string | Domain service namespace (lowercase, validated) | `"counter"` |
| `sequenceNumber` | long | Strictly ordered per aggregate stream, starts at 1, gapless | `1` |
| `timestamp` | DateTimeOffset | Server-clock event creation time | `"2026-03-01T10:30:00Z"` |
| `correlationId` | string | Request-level tracing — same for all events from one command | `"550e8400-e29b-41d4-a716-446655440000"` |
| `causationId` | string | ID of the command that caused this event | `"a1b2c3d4-e5f6-7890-abcd-ef1234567890"` |
| `userId` | string | Authenticated user identity (from JWT `sub` claim) | `"user@example.com"` |
| `domainServiceVersion` | string | Version of the domain service that produced the event | `"v1"` |
| `eventTypeName` | string | Fully qualified event type for deserialization | `"CounterIncremented"` |
| `serializationFormat` | string | Payload encoding format | `"json"` |

You'll notice `aggregateId` contains the full composite identity `{tenant}:{domain}:{id}` — not just the local aggregate ID. This is by design: the canonical form enables single-field state store key derivation, while the separate `tenantId` and `domain` fields exist for efficient filtering and querying without string parsing. See [Identity Scheme](identity-scheme.md) for the complete identity mapping.

The `sequenceNumber` is validated at construction time — it must be >= 1, and the system throws an `ArgumentOutOfRangeException` if that constraint is violated. This guarantees that every event stream starts at 1 and contains no gaps. When you replay an aggregate's event stream to reconstruct state, the gapless sequence lets you detect missing or out-of-order events immediately. If your state reconstruction reads events 1, 2, 4 (skipping 3), you know something is wrong — the gapless guarantee means event 3 must exist, and its absence indicates a data integrity issue rather than a normal gap.

The sequence number is scoped to a single aggregate stream — it is not a global counter. Two different aggregates (for example, `counter-1` and `counter-2`) each maintain independent sequence numbers starting at 1. This per-aggregate scoping is what enables horizontal scaling: each aggregate's actor manages its own sequence independently, with no cross-aggregate coordination required.

The `correlationId` and `causationId` work together as a tracing pair:

- The **correlation ID** stays the same across all events produced by a single HTTP request. If one command produces three events, all three share the same correlation ID. This is what you use for request-level tracing in your observability platform — filter by correlation ID to see every event that resulted from a single API call.

- The **causation ID** links each individual event back to the specific command that caused it. In a system where commands can trigger downstream commands (saga or process manager patterns), the causation chain lets you walk backwards from any event to the exact command that produced it.

Together, they enable both request-level tracing ("show me everything that happened from this API call") and causal tracing ("which command produced this specific event?"). In your observability platform (such as Jaeger, Zipkin, or Application Insights), the correlation ID maps to the OpenTelemetry trace context, making it easy to connect event persistence spans to the original HTTP request span.

The `timestamp` uses the server clock at persistence time, not the client's clock or the time the command was received. This ensures that timestamps across all events in a system are comparable and ordered consistently, even when commands arrive from clients with skewed clocks. Note that the timestamp is informational — the `sequenceNumber` is the authoritative ordering mechanism within an aggregate stream, not the timestamp. Two events with sequence numbers 3 and 4 are guaranteed to be in that order, even if their timestamps are identical (which can happen when a command produces multiple events in a single persist operation).

The timestamp is most useful for cross-aggregate queries and operational monitoring, where you need to understand the chronological flow of events across different aggregate streams that do not share a common sequence.

The `userId` captures the authenticated identity from the JWT `sub` claim at the Command API gateway. This means every event is attributable to a specific user, which is essential for audit trails and security forensics. Because the user ID is extracted from the validated JWT — not from a user-supplied field — it cannot be spoofed by the calling application.

In multi-tenant scenarios, the combination of `tenantId`, `domain`, and `userId` gives you a complete picture of data lineage: which tenant's data was affected, which domain service processed it, and which authenticated user initiated the operation.

The `domainServiceVersion` records which version of your domain service produced the event. This becomes critical when you deploy new versions of your domain logic — if a new version changes the shape of an event payload, the version field tells consumers which serialization format to expect during deserialization. Combined with `eventTypeName`, this pair forms the complete type identity of any event payload, enabling version-aware upcasting and schema migration strategies.

### Payload

The payload is an opaque byte array containing the serialized domain event. EventStore does not inspect or validate your payload — it stores and forwards the raw bytes without any schema coupling. This means EventStore can persist events of any shape without knowing anything about your domain types.

By default, EventStore serializes your event payload using `System.Text.Json`, calling `JsonSerializer.SerializeToUtf8Bytes(payload, payload.GetType())`. The serialization happens during the AggregateActor's persist step — you return plain .NET objects from your `Handle` method, and EventStore converts them to bytes automatically. The payload type is determined at runtime from the actual .NET type of your event object, so polymorphic event hierarchies work without additional configuration.

If your event implements `ISerializedEventPayload`, EventStore uses your pre-serialized bytes directly, skipping redundant serialization. This is useful when you need custom serialization (for example, Protobuf or MessagePack for performance-critical paths) or when your event arrives from an external system already in byte form.

Because EventStore treats the payload as opaque bytes, it imposes no constraints on your event schema. You can use nested objects, arrays, enums, or any other structure that `System.Text.Json` can serialize. The only requirement is that your event type implements the `IEventPayload` marker interface — this is how EventStore discovers event types during assembly scanning.

There is also a special marker interface `IRejectionEvent` (which extends `IEventPayload`) for events that represent command rejections — for example, `CounterIncrementRejected` when a business rule prevents the operation. Rejection events are persisted with the same envelope structure as success events, giving you a complete audit trail of both successful and rejected operations. This is an important distinction from systems that only persist success events — in Hexalith, the event stream is a complete history of everything that happened, including operations that were intentionally rejected by business rules.

The `eventTypeName` distinguishes between success events and rejection events at the metadata level, so subscribers can filter or route based on event type without deserializing the payload.

### Extensions

The extensions field is a `Dictionary<string, string>` open metadata bag for domain-specific needs. EventStore passes extensions through without interpretation — it stores and forwards them alongside the metadata and payload without validation or transformation.

Use extensions for custom tracing, feature flags, or domain-specific routing hints. For example: `{"x-priority": "high", "x-region": "eu-west"}`.

Extensions are normalized to an empty read-only dictionary on construction if null, so you never need to null-check them when reading an envelope. This normalization happens in both the Contracts and Server envelope types, giving you a consistent, safe API regardless of which envelope type you encounter.

A typical use case: if your system routes events differently based on business priority or geographic region, you can set those values as extensions on the originating command, and they flow through to every event envelope produced by that command. Downstream subscribers can then read the extensions to make routing or processing decisions without coupling to the core metadata schema.

Extensions are string-to-string only — they do not support nested structures or non-string value types. This is intentional: keeping extensions flat makes them trivially serializable, efficiently queryable, and resistant to schema drift. If you need complex metadata, serialize it to a string value (for example, a JSON string as the value of a single extension key).

By convention, extension keys use lowercase with a prefix (e.g., `x-priority`, `x-region`) to avoid collisions with any future metadata fields. EventStore does not enforce a naming convention on extensions, but following this pattern makes it clear which metadata is core (the 11 envelope fields) and which is domain-specific (extensions).

## A Real Event Envelope

The following example shows a complete event envelope for a `CounterIncremented` event from the Counter sample. This is the flat Server envelope format — the JSON wire representation used for DAPR state store persistence and pub/sub publishing. All field names use camelCase, which is the wire format convention throughout Hexalith.EventStore.

In this scenario, a user sent an `IncrementCounter` command to the Counter sample's API. The AggregateActor processed the command, the domain service's `Handle` method returned a `CounterIncremented` event, and EventStore wrapped that event in the envelope shown below:

```json
{
  "aggregateId": "demo:counter:counter-1",
  "tenantId": "demo",
  "domain": "counter",
  "sequenceNumber": 1,
  "timestamp": "2026-03-01T10:30:00+00:00",
  "correlationId": "550e8400-e29b-41d4-a716-446655440000",
  "causationId": "a1b2c3d4-e5f6-7890-abcd-ef1234567890",
  "userId": "user@example.com",
  "domainServiceVersion": "v1",
  "eventTypeName": "CounterIncremented",
  "serializationFormat": "json",
  "payload": "eyJhbW91bnQiOjF9",
  "extensions": {
    "x-priority": "normal"
  }
}
```

The `payload` is a base64-encoded byte array. Decoded, this payload contains: `{"amount": 1}` — the serialized `CounterIncremented` event. The base64 encoding is the standard JSON representation of the underlying `byte[]` field — this is what you see when inspecting events in the DAPR state store or when consuming events from pub/sub.

Note three things in this example:

- **Sequence ordering:** The `sequenceNumber` is `1` because this is the first event in the aggregate's stream. Subsequent events for the same aggregate increment strictly from here — 2, 3, 4, and so on. If this counter is incremented three times, you get a gapless sequence of three envelopes numbered 1, 2, 3.

- **Tracing pair:** The `correlationId` and `causationId` form a distributed tracing pair. The correlation ID ties all events from a single HTTP request together — useful for end-to-end request tracing in your observability platform. Query your traces by correlation ID to see every event that resulted from a single API call. The causation ID links each event back to the specific command that caused it — useful for understanding causal chains when one command triggers downstream processing. In a distributed system with sagas or process managers, the causation chain lets you walk backwards from any event to the originating command.

- **Opaque payload:** The payload is opaque bytes. EventStore persisted the raw serialized content without knowing or caring what is inside. This is why EventStore can handle any domain event type without code changes — the envelope structure is the same whether you are storing counter increments, order placements, or user registrations.

This JSON shape is exactly what you see when you inspect events in the DAPR state store or when you receive events on a pub/sub topic. The flat structure with all fields at the top level is the Server envelope format — the wire representation used for persistence and publishing.

If this same command had produced multiple events (for example, both `CounterIncremented` and `CounterThresholdReached`), each event would get its own envelope with a unique `sequenceNumber` but shared `correlationId` and `causationId` values. This lets you reconstruct the full set of events produced by a single command by querying for all envelopes with the same causation ID.

## Two Envelope Types

There are two `EventEnvelope` record types in the codebase — this is by design, and the distinction matters when you consume events downstream.

**Contracts EventEnvelope** (`Hexalith.EventStore.Contracts.Events.EventEnvelope`) is a composed structure with a separate `EventMetadata` record. This is the public API type that domain service developers reference. The composed structure makes it easy to work with metadata and payload independently — for example, you can pass just the `Metadata` to a logging method without touching the payload. When you write projections, test assertions, or event handlers inside the EventStore ecosystem, you work with this type.

**Server EventEnvelope** (`Hexalith.EventStore.Server.Events.EventEnvelope`) is a flat structure with all 11 fields inlined alongside the payload and extensions. This is the internal storage/wire type used for DAPR state store persistence and pub/sub publishing. The flat structure maps directly to JSON without nesting, which simplifies DAPR's built-in serialization. This is the shape that arrives on pub/sub topics — if you build a subscriber or projection outside the EventStore ecosystem, you deserialize from the flat Server envelope format.

The reason for having two representations is separation of concerns: the Contracts type is optimized for developer ergonomics (grouped metadata, read-only extensions), while the Server type is optimized for storage and wire efficiency (flat, no nesting, mutable extensions for serialization compatibility).

Both types implement the same `ToString()` payload redaction behavior, so you get consistent security guarantees regardless of which representation you are working with.

Both types also expose a computed `AggregateIdentity` property that parses the canonical `aggregateId` string back into its three component parts (`TenantId`, `Domain`, `AggregateId`). This is useful when you need to extract identity information programmatically — for example, when routing events to tenant-specific handlers or when building state store key patterns from an envelope's identity.

The Contracts types look like this:

```csharp
// Contracts.Events namespace — the type you reference as a domain service developer
public record EventMetadata(
    string AggregateId, string TenantId, string Domain,
    long SequenceNumber, DateTimeOffset Timestamp,
    string CorrelationId, string CausationId, string UserId,
    string DomainServiceVersion, string EventTypeName, string SerializationFormat);

public record EventEnvelope(EventMetadata Metadata, byte[] Payload, IReadOnlyDictionary<string, string>? Extensions);
```

As a domain service developer, you only interact with the Contracts types. The Server envelope is an internal implementation detail that you never need to reference directly.

Neither envelope type carries JSON serialization attributes (such as `[JsonPropertyName]`) — DAPR handles serialization automatically using its built-in System.Text.Json configuration. This means the wire format follows `System.Text.Json` defaults: camelCase property names, no null value omission, and standard type converters for `DateTimeOffset`, `long`, and `byte[]` (base64).

If you build a subscriber that consumes events from DAPR pub/sub, the event data payload inside the CloudEvents wrapper is the flat Server envelope — all 11 metadata fields plus payload and extensions appear as top-level JSON properties, exactly like the JSON example shown earlier in this page. You do not need to reference the Server package — define a matching record in your own code or use dynamic deserialization.

This means external services (even non-.NET ones) can consume Hexalith events by defining their own data type that matches the flat JSON structure. The CloudEvents wrapper provides standard metadata for routing and filtering, and the inner data payload is the flat envelope you already know from the JSON example above.

When consuming events from pub/sub, remember that you receive the flat Server envelope shape — not the nested Contracts shape. All 11 metadata fields, plus `payload` (base64-encoded) and `extensions`, appear as top-level properties in the JSON data payload within the CloudEvents wrapper. If you are writing a .NET subscriber, you can create a simple record type matching this flat structure for deserialization. If you are writing in another language, map the JSON properties to your language's equivalent types.

## How EventStore Populates Metadata

Your domain service returns `DomainResult.Success(events)` from a `Handle` method — a list of plain domain event objects with no metadata attached. EventStore takes those raw events and wraps each one in an EventEnvelope during the AggregateActor's persist step (Step 5 of the pipeline, as described in the [Command Lifecycle Deep Dive](command-lifecycle.md)). EventStore populates all metadata fields — you never set metadata manually.

This is a deliberate design choice. By centralizing metadata population in the AggregateActor, EventStore guarantees that every event in the system has consistent, trustworthy metadata. The actor derives each field from sources it controls — the validated command, the actor's own sequence state, and the server clock — rather than accepting developer-supplied values that could be incorrect or malicious.

The flow looks like this: the AggregateActor receives a validated command, calls your domain service's `Handle` method, receives back a list of events, and then for each event:

1. Assigns the next sequence number (incrementing from `AggregateMetadata.CurrentSequence`)
2. Captures the server timestamp
3. Copies tracing IDs (`correlationId`, `causationId`) from the command
4. Copies the `userId` from the command's authenticated identity
5. Resolves `eventTypeName` from the .NET type of the event
6. Serializes the event payload to bytes
7. Assembles the complete envelope with all metadata, payload, and extensions

All of this happens atomically within the actor's turn-based concurrency model, which guarantees that no two commands for the same aggregate can interleave their persist operations. This atomicity is what makes sequence numbers gapless and strictly ordered — there is no race condition where two concurrent commands could claim the same sequence number.

Here is where each metadata field comes from:

| Field | Source |
|-------|--------|
| `aggregateId` | From AggregateIdentity (canonical form `{tenant}:{domain}:{id}`) |
| `tenantId` | From the incoming command's tenant |
| `domain` | From the incoming command's domain |
| `sequenceNumber` | Auto-incremented from AggregateMetadata.CurrentSequence |
| `timestamp` | Server clock at persistence time |
| `correlationId` | From the incoming command's correlation ID |
| `causationId` | From the incoming command's causation ID |
| `userId` | From the incoming command's user ID (JWT `sub` claim) |
| `domainServiceVersion` | Extracted from the command envelope's `domain-service-version` extension key; defaults to `"v1"` if absent (format: `v{number}`, regex: `^v[0-9]+$`) |
| `eventTypeName` | From the .NET type name of the event |
| `serializationFormat` | Defaults to `"json"` (System.Text.Json) |

This design ensures metadata consistency — no domain service can produce events with incorrect sequence numbers, missing tenants, or forged user identities. The metadata is authoritative because EventStore controls it end-to-end.

The `sequenceNumber` deserves special attention: it is auto-incremented from `AggregateMetadata.CurrentSequence`, which is a watermark stored alongside the event stream in the DAPR state store. The `AggregateMetadata` record tracks the current sequence watermark, the last modification time, and an optional ETag for optimistic concurrency. The AggregateActor reads the current watermark, increments it for each new event, and persists the updated watermark atomically with the events. This is how EventStore guarantees gapless, strictly ordered sequence numbers even under concurrent command processing.

Note that the `domainServiceVersion` is extracted from the incoming command envelope's `domain-service-version` extension key. If the extension is absent or empty, it defaults to `"v1"`. The version format is `v{number}` (regex: `^v[0-9]+$`) — for example, `"v1"`, `"v2"`, `"v10"`. This design makes versioning a deployment and routing concern: clients specify which domain service version should handle each command, and EventStore records which version actually processed it.

The `eventTypeName` is resolved from the .NET type name of the event object at serialization time. For example, if your `Handle` method returns a `CounterIncremented` instance, the `eventTypeName` will be `"CounterIncremented"`. This type name is essential for deserialization — when EventStore or a consumer reads an event from the store, it uses the `eventTypeName` to determine which .NET type to deserialize the payload bytes into.

The `serializationFormat` currently defaults to `"json"` for all events serialized with the built-in `System.Text.Json` serializer. If you use `ISerializedEventPayload` to provide pre-serialized bytes in a different format, the serialization format field reflects whatever format you specify.

This field is critical for forward compatibility — if a future version of EventStore supports additional serialization formats (such as Protobuf or Avro), consumers can use this field to select the correct deserializer. Because the format is recorded per-event, you can even migrate serialization formats incrementally: new events use the new format while old events retain their original format, and consumers handle both based on the `serializationFormat` value.

## CloudEvents Integration

When events are published to DAPR pub/sub, they are wrapped in CloudEvents 1.0 format. DAPR handles the CloudEvents wrapping natively — you do not need to construct CloudEvents yourself. Hexalith adds three CloudEvents metadata attributes that enable subscribers to filter and route events efficiently:

- **`cloudevent.type`** — the event type name (e.g., `CounterIncremented`). Subscribers can filter on this attribute to receive only specific event types. For example, a projection that builds a counter summary read model might subscribe only to `CounterIncremented` and `CounterDecremented` events, ignoring everything else.

- **`cloudevent.source`** — `hexalith-eventstore/{tenantId}/{domain}` (e.g., `hexalith-eventstore/demo/counter`). This identifies which tenant and domain produced the event, enabling topic-level and source-level filtering. In a multi-tenant system, subscribers can use the source to process events from specific tenants independently.

- **`cloudevent.id`** — `{correlationId}:{sequenceNumber}` (globally unique per event). This composite ID ensures idempotent delivery — subscribers can detect and skip duplicate events by checking whether they have already processed a given `cloudevent.id`. The combination of correlation ID and sequence number guarantees uniqueness across all aggregates and tenants.

Events follow the [CloudEvents 1.0 specification](https://cloudevents.io/), making them consumable by any CloudEvents-compatible subscriber — including non-.NET services, third-party event routers, and cloud-native event meshes.

The CloudEvents standard ensures that Hexalith events integrate naturally into heterogeneous architectures where not every service runs on .NET. A Python microservice, a Go event processor, or a serverless function can all subscribe to Hexalith event topics and consume events using their platform's CloudEvents SDK.

The three metadata attributes above are the only Hexalith-specific additions — everything else follows the CloudEvents specification exactly as DAPR implements it. DAPR adds its own standard CloudEvents attributes (such as `specversion`, `datacontenttype`, and `time`), and the Hexalith attributes supplement those with event-sourcing-specific context.

## Security Considerations

Both EventEnvelope types implement `ToString()` to redact the payload with `[REDACTED]`. This prevents accidental payload logging in structured logs or exception messages — a key safeguard when event payloads contain sensitive business data. Without this protection, a simple `logger.LogInformation("Processing event: {Event}", envelope)` call could leak customer PII, financial data, or other sensitive information into your log aggregation system.

The redaction applies to any code path that calls `ToString()` implicitly, including string interpolation, exception messages, and structured logging frameworks that serialize objects by calling their string representation. The metadata fields (aggregate ID, tenant, domain, timestamps, correlation IDs, etc.) remain visible in logs — only the payload bytes are replaced. This gives you full traceability without exposing business data.

For example, if an exception occurs during event persistence, the exception message will show the envelope's metadata (aggregate, sequence, tenant) to help you diagnose the issue, but the actual business data in the payload will appear as `[REDACTED]`. This is especially important in production environments where logs may be stored in shared observability platforms accessible to operations teams who should not see raw business data.

Extensions are sanitized at the Command API entry point before they reach the processing pipeline. This prevents injection of malicious metadata through the extensions bag — for example, an attacker cannot inject extensions that override internal routing decisions or impersonate system-level metadata. The sanitization strips any extension keys that match reserved prefixes and validates that all values are safe strings.

Together, payload redaction and extension sanitization ensure that the event envelope is safe for both logging and processing. You can log envelopes freely for debugging without worrying about data leaks, and you can trust that the extensions on any envelope have passed through the API gateway's validation layer.

These security measures are built into the envelope types themselves — they are not opt-in behaviors that developers need to remember to enable. This "secure by default" approach means that even if a developer writes careless logging code, the system protects sensitive data automatically.

## Connecting the Dots

In the [Command Lifecycle Deep Dive](command-lifecycle.md), you saw the AggregateActor persist events in Step 5. This page explained exactly what gets persisted — the envelope structure that wraps every domain event with the metadata that makes event sourcing work. You now know the complete anatomy of an envelope, where each metadata field comes from, and how the two envelope types relate to each other.

Four design principles run through the envelope design:

- **Metadata ownership:** EventStore owns all 11 metadata fields — domain services produce only the business fact (payload). This separation means your domain logic stays focused on business rules without worrying about infrastructure concerns like sequence numbering, tenant isolation, or tracing. It also means metadata is consistent system-wide — every event follows the same structure regardless of which domain service produced it.

- **Schema ignorance:** EventStore treats the payload as opaque bytes — it works with any domain event type without schema coupling. You can add new event types, change event shapes, or use different serialization formats without modifying EventStore itself. This is what makes EventStore a general-purpose event sourcing infrastructure — it is not coupled to any specific domain model.

- **Traceability:** Every event carries correlation and causation IDs, enabling end-to-end distributed tracing from HTTP request to persisted event. When debugging production issues, these IDs let you reconstruct the complete chain of commands and events that led to a particular state. Combined with OpenTelemetry instrumentation, the tracing fields give you full observability into event flows across service boundaries.

- **Durability:** The envelope schema is the most durable contract in the system. Once events are persisted, the metadata structure cannot change without migrating every event stream. This is why the schema was designed with exactly 11 carefully chosen metadata fields — each one earned its place by serving a fundamental need (identity, ordering, tracing, versioning). The extensions bag exists precisely for needs that emerge after the schema is finalized — allowing you to attach new metadata to events without altering the core envelope structure.

Together, these four principles mean that as a domain service developer, you focus entirely on your business logic. You write `Handle` methods that return events, and EventStore handles everything else — the envelope, the metadata, the persistence, and the distribution. The envelope is the contract between your domain logic and the rest of the infrastructure.

Understanding the envelope structure is fundamental to working effectively with Hexalith.EventStore. Whether you are building projections, debugging production issues, writing integration tests, or designing new domain services, the envelope is the common language that connects all parts of the system.

When your event schema evolves over time, the `eventTypeName` and `domainServiceVersion` fields enable version-aware deserialization — consumers can detect which version of an event they are reading and apply appropriate upcasting or migration logic. See [Event Versioning & Schema Evolution](event-versioning.md) for the full versioning strategy, safe/unsafe change classifications, and upcasting patterns.

## Next Steps

- **Next:** [Identity Scheme](identity-scheme.md) — understand how tenant, domain, and aggregate IDs map to actors, streams, and topics
- **Related:** [Command Lifecycle Deep Dive](command-lifecycle.md) — the end-to-end command processing pipeline that produces event envelopes
- **Related:** [Architecture Overview](architecture-overview.md) — the DAPR topology and building blocks that underpin envelope persistence and distribution
- **Sample:** The [Counter domain sample](../../samples/Hexalith.EventStore.Sample/) demonstrates all concepts from this page in working code
