# Story 12.3: Event Envelope Metadata Structure

Status: done

## Story

As a developer working with Hexalith events,
I want to understand the event envelope metadata structure,
So that I know what metadata accompanies every event and how to use it.

## Acceptance Criteria

1. `docs/concepts/event-envelope.md` exists as a new documentation page
2. The page explains the complete metadata structure of an event envelope with field descriptions
3. Includes a JSON or C# example of a real event envelope from the Counter domain
4. The page follows the standard page template (back-link, H1, summary, max 2 prerequisites, content sections, Next Steps footer)
5. The page is self-contained (FR43) — no external knowledge required to understand it

## Tasks / Subtasks

- [x] Task 1: Create `docs/concepts/event-envelope.md` with page template structure (AC: #1, #4, #5)
    - [x] 1.1 Add back-link `[<- Back to Hexalith.EventStore](../../README.md)`
    - [x] 1.2 Add H1 title "Event Envelope & Metadata"
    - [x] 1.3 Add one-paragraph summary: what an event envelope is and why it matters for event sourcing
    - [x] 1.4 Add prerequisites callout linking to architecture-overview and command-lifecycle (max 2)
    - [x] 1.5 Add Next Steps footer: "**Next:** [Identity Scheme](identity-scheme.md) — understand how tenant, domain, and aggregate IDs map to actors, streams, and topics" and "**Related:** [Command Lifecycle Deep Dive](command-lifecycle.md), [Architecture Overview](architecture-overview.md)". Note: `identity-scheme.md` will be a dead link until Story 12-4 — that's expected.

- [x] Task 2: Write "What Is an Event Envelope?" opening section (AC: #2)
    - [x] 2.1 Explain in 3-5 sentences: an event envelope wraps every domain event with metadata that the EventStore populates automatically. The developer writes only the domain event payload (the business fact); the EventStore adds the 11 metadata fields that provide traceability, ordering, multi-tenancy, and correlation. Use a brief analogy to ground the concept: "Think of the envelope like a physical letter: your domain event is the letter inside, and the metadata fields are the postmark, return address, tracking number, and recipient address printed on the outside."
    - [x] 2.2 One-sentence callout: "You never construct an EventEnvelope yourself — the EventStore builds it during the persist step of the AggregateActor pipeline."
    - [x] 2.3 Add 1-2 sentences explaining when developers encounter envelopes: "You'll see EventEnvelopes when building event subscribers (projections, read models), writing integration tests that assert on persisted events, or debugging event streams through the DAPR state store."

- [x] Task 3: Write "Anatomy of an Event Envelope" section with field table (AC: #2)
    - [x] 3.1 Present the envelope as three layers: **Metadata** (11 fields), **Payload** (opaque bytes), **Extensions** (open metadata bag)
    - [x] 3.2 Create a table of all 11 metadata fields with columns: Field, Type, Description, Example (using Counter domain values)
        - `aggregateId` | string | Full canonical identity (`{tenant}:{domain}:{id}`) | `"demo:counter:counter-1"`
        - `tenantId` | string | Tenant isolation key (lowercase, validated) | `"demo"`
        - `domain` | string | Domain service namespace (lowercase, validated) | `"counter"`
    - [x] 3.2a **After the field table**, add a brief callout paragraph explaining the `aggregateId` canonical form: "You'll notice `aggregateId` contains the full composite identity `{tenant}:{domain}:{id}` — not just the local aggregate ID. This is by design: the canonical form enables single-field state store key derivation, while the separate `tenantId` and `domain` fields exist for efficient filtering and querying without string parsing. See [Identity Scheme](identity-scheme.md) for the complete identity mapping." Keep table cells short and scannable — detailed explanations go in prose after the table.
        - `sequenceNumber` | long | Strictly ordered per aggregate stream, starts at 1, gapless. Validated >= 1 at construction time (throws `ArgumentOutOfRangeException` if violated) | `1`
        - `timestamp` | DateTimeOffset | Server-clock event creation time | `"2026-03-01T10:30:00Z"`
        - `correlationId` | string | Request-level tracing — same for all events from one command | `"550e8400-..."`
        - `causationId` | string | ID of the command that caused this event | `"a1b2c3d4-..."`
        - `userId` | string | Authenticated user identity (from JWT `sub` claim) | `"user@example.com"`
        - `domainServiceVersion` | string | Version of the domain service that produced the event | `"1.0.0"`
        - `eventTypeName` | string | Fully qualified event type for deserialization | `"CounterIncremented"`
        - `serializationFormat` | string | Payload encoding format | `"json"`
    - [x] 3.3 Explain the Payload field: opaque byte array containing the serialized domain event. EventStore doesn't inspect or validate your payload — it stores and forwards the raw bytes without any schema coupling. By default, EventStore serializes your event payload using `System.Text.Json`. If your event implements `ISerializedEventPayload`, EventStore uses your pre-serialized bytes directly, skipping redundant serialization.
    - [x] 3.4 Explain the Extensions field: `Dictionary<string, string>` open metadata bag for domain-specific needs. EventStore passes extensions through without interpretation. Use for custom tracing, feature flags, or domain-specific routing hints. Provide a concrete example: e.g., `{"x-priority": "high", "x-region": "eu-west"}`.

- [x] Task 4: Write a concrete Counter domain JSON example (AC: #3)
    - [x] 4.1 Show a complete JSON representation of an EventEnvelope for a `CounterIncremented` event from the Counter sample. Use camelCase (the wire format convention per architecture). Include all 11 metadata fields, payload, and extensions with a concrete example value. **CRITICAL: The `payload` field is `byte[]`. In JSON wire format, it appears as a base64-encoded string, NOT as a raw JSON object.** Show the base64-encoded version (e.g., `"eyJhbW91bnQiOjF9"` which is base64 for `{"amount":1}`), then add an annotation below the JSON block explaining: "The payload is a base64-encoded byte array. Decoded, this payload contains: `{\"amount\": 1}` — the serialized `CounterIncremented` event." Include a non-empty extensions example like `{"x-priority": "normal"}`.
    - [x] 4.2 Add follow-up annotations (NOT inline comments — JSON doesn't support comments) highlighting: sequence number ordering, correlation/causation tracing pair, and the base64-encoded opaque payload

- [x] Task 5: Write "Two Envelope Types" section explaining the dual representation (AC: #2)
    - [x] 5.1 Explain that there are two `EventEnvelope` record types in the codebase — this is by design:
        - **Contracts EventEnvelope** (`Hexalith.EventStore.Contracts.Events.EventEnvelope`): composed structure with a separate `EventMetadata` record. This is the public API type that domain service developers reference.
        - **Server EventEnvelope** (`Hexalith.EventStore.Server.Events.EventEnvelope`): flat structure with all 11 fields inlined. This is the internal storage/wire type used for DAPR state store persistence and pub/sub publishing. **This is the shape that arrives on pub/sub topics** — if you build a subscriber or projection, you deserialize from the flat Server envelope format.
    - [x] 5.2 Show a simplified C# record definition for the Contracts version (EventMetadata + Payload + Extensions) — 3-5 lines max, illustrative
    - [x] 5.3 Explain: "As a domain service developer, you only interact with the Contracts types. The Server envelope is an internal implementation detail." Add one sentence: "EventEnvelope has no JSON serialization attributes — DAPR handles serialization automatically using its built-in System.Text.Json configuration."
    - [x] 5.4 Clarify pub/sub consumer shape **in prose only (no additional code block — stay within 2-block budget)**: "If you build a subscriber that consumes events from DAPR pub/sub, the event data payload inside the CloudEvents wrapper is the flat Server envelope — all 11 metadata fields plus payload and extensions appear as top-level JSON properties. You don't need to reference the Server package — define a matching record in your own code or use dynamic deserialization." Describe the flat shape in prose; do NOT add a second C# record definition.

- [x] Task 6: Write "How EventStore Populates Metadata" section (AC: #2)
    - [x] 6.1 Explain the flow: your domain service returns `DomainResult.Success(events)` from a `Handle` method. EventStore takes the raw events and wraps each one in an EventEnvelope during the AggregateActor's persist step (Step 5 of the pipeline). EventStore populates ALL metadata fields — the developer never sets metadata manually.
    - [x] 6.2 Table or bullet list showing where each metadata field comes from:
        - `aggregateId` — from AggregateIdentity (canonical form)
        - `tenantId` — from incoming command's tenant
        - `domain` — from incoming command's domain
        - `sequenceNumber` — auto-incremented from AggregateMetadata.CurrentSequence
        - `timestamp` — server clock at persistence time
        - `correlationId` — from incoming command's correlation ID
        - `causationId` — from incoming command's causation ID
        - `userId` — from incoming command's user ID (JWT `sub` claim)
        - `domainServiceVersion` — from domain service registration configuration (config store or appsettings.json), NOT self-reported by the domain service. In the fluent API, this is derived from the assembly version or overridden via `EventStoreDomainOptions`
        - `eventTypeName` — from the .NET type name of the event
        - `serializationFormat` — defaults to `"json"` (System.Text.Json)
    - [x] 6.3 One-sentence: "This design ensures metadata consistency — no domain service can produce events with incorrect sequence numbers, missing tenants, or forged user identities."

- [x] Task 7: Write "CloudEvents Integration" section (AC: #2)
    - [x] 7.1 Explain: when events are published to DAPR pub/sub, they are wrapped in CloudEvents 1.0 format. DAPR handles the CloudEvents wrapping natively. Hexalith adds three CloudEvents metadata attributes:
        - `cloudevent.type` — the event type name (e.g., `CounterIncremented`)
        - `cloudevent.source` — `hexalith-eventstore/{tenantId}/{domain}` (e.g., `hexalith-eventstore/demo/counter`)
        - `cloudevent.id` — `{correlationId}:{sequenceNumber}` (globally unique per event)
    - [x] 7.2 One sentence linking to CloudEvents spec: "Events follow the [CloudEvents 1.0 specification](https://cloudevents.io/), making them consumable by any CloudEvents-compatible subscriber."

- [x] Task 8: Write "Security Considerations" brief section (AC: #2, #5)
    - [x] 8.1 Explain SEC-5 compliance: `ToString()` on both EventEnvelope types redacts the payload with `[REDACTED]`. This prevents accidental payload logging in structured logs or exception messages.
    - [x] 8.2 One sentence: "Extensions are sanitized at the Command API entry point before they reach the processing pipeline."

- [x] Task 9: Write "Connecting the Dots" closing section (AC: #5)
    - [x] 9.1 Tie back to what the reader already knows: "In the Command Lifecycle Deep Dive, you saw the AggregateActor persist events in Step 5. This page explained exactly what gets persisted — the envelope structure that wraps every domain event."
    - [x] 9.2 List 4 design principles demonstrated:
        - **Metadata ownership:** EventStore owns all 11 metadata fields — domain services produce only the business fact (payload)
        - **Schema ignorance:** EventStore treats the payload as opaque bytes — it works with any domain event type without schema coupling
        - **Traceability:** Every event carries correlation and causation IDs, enabling end-to-end distributed tracing from HTTP request to persisted event
        - **Durability:** The envelope schema is the most durable contract in the system. Once events are persisted, the metadata structure cannot change without migrating every event stream. The extensions bag exists precisely for needs that emerge after the schema is finalized.
    - [x] 9.3 One forward-looking sentence (prose only, NO concrete link — the path doesn't exist yet and is two epics away): "When your event schema evolves over time, the `eventTypeName` and `domainServiceVersion` fields enable version-aware deserialization. A future guide on event versioning will cover the full strategy."

- [x] Task 10: Verify compliance
    - [x] 10.1 No YAML frontmatter
    - [x] 10.2 All code blocks have language tags (`json`, `csharp`)
    - [x] 10.3 All internal links use relative paths
    - [x] 10.4 No hard line wrapping in markdown source
    - [x] 10.5 Heading hierarchy: one H1, H2 for sections, H3 for subsections
    - [x] 10.6 Target page length: ~300-350 lines (400 max)
    - [x] 10.7 Fenced block count: exactly 2 code blocks (1 JSON example, 1 C# record definition) — no more
    - [x] 10.8 Self-containment test: every concept re-introduced by role, max 2 prerequisites
    - [x] 10.9 Accessibility: no Mermaid diagrams in this page (content is tabular, not visual flow)

- [x] Task 11: Code review remediation sync
    - [x] 11.1 Reconciled story status with review outcome
    - [x] 11.2 Corrected debug metrics to match current file state
    - [x] 11.3 Documented working-tree file-list discrepancy context
    - [x] 11.4 Synced sprint tracking status to done

## Dev Notes

### Implementation Ordering (MUST follow)

**This story MUST be developed AFTER Stories 12-1 (Architecture Overview) and 12-2 (Command Lifecycle Deep Dive) are complete.** Story 12-2 mentions EventEnvelope during the persistence phase and links forward to this page. The command-lifecycle page is this page's prerequisite link. Write this page as if both architecture-overview and command-lifecycle exist.

### Page Outline — Exact H2/H3 Headings (MUST follow)

Use these exact headings on the output page. This maps tasks to page structure and sets line budgets per section (~300 lines target, 400 max):

```text
[back-link]
# Event Envelope & Metadata                       (Task 1)
[summary paragraph]
[prerequisites callout]
                                                    ~25 lines

## What Is an Event Envelope?                       (Task 2)
[plain-English intro + analogy + callout + encounter context]
                                                    ~25 lines

## Anatomy of an Event Envelope                     (Task 3)
[3-layer explanation + 11-field table]
[payload + extensions explanation]
                                                    ~60 lines

## A Real Event Envelope                            (Task 4)
[JSON example with annotations]
                                                    ~40 lines

## Two Envelope Types                               (Task 5)
[Contracts vs Server + C# snippet + subscriber shape]
                                                    ~40 lines

## How EventStore Populates Metadata                (Task 6)
[field origin table + consistency callout]
                                                    ~40 lines

## CloudEvents Integration                          (Task 7)
[3 attributes + spec link]
                                                    ~20 lines

## Security Considerations                          (Task 8)
[SEC-5 + sanitization]
                                                    ~15 lines

## Connecting the Dots                              (Task 9)
[tie-back + 3 design principles + versioning forward link]
                                                    ~25 lines

## Next Steps                                       (Task 1.5)
                                                    ~10 lines
```

### Fenced Block Budget (MUST follow)

The page must contain at most **2 code blocks** (1 JSON example of a complete envelope, 1 C# record definition for the Contracts EventEnvelope). No Mermaid diagrams on this page — the content is structural/tabular, not a visual flow.

### Content Tone & Running Example (MUST follow)

**Tone:** Second-person ("you"), present tense ("EventStore populates..."), confident but approachable. Define technical jargon before first use. No assumptions about prior DAPR knowledge beyond what's in the prerequisites.

**Running example:** Ground every concept in the **Counter domain from the quickstart**. Use `IncrementCounter` -> `CounterIncremented` as the running example. All field examples in the metadata table should use Counter domain values.

### Architecture Facts — Event Envelope (verified from source code)

**Contracts EventEnvelope (`src/Hexalith.EventStore.Contracts/Events/EventEnvelope.cs`):**

```csharp
public record EventEnvelope(EventMetadata Metadata, byte[] Payload, IReadOnlyDictionary<string, string>? Extensions)
```

- Wraps a separate `EventMetadata` record (composed, not flat)
- `Extensions` normalized to empty `ReadOnlyDictionary` on construction
- `ToString()` redacts payload — hardcoded `[REDACTED]` (SEC-5)
- **No JSON serialization attributes** on the type

**EventMetadata (`src/Hexalith.EventStore.Contracts/Events/EventMetadata.cs`):**

```csharp
public record EventMetadata(
    string AggregateId,
    string TenantId,
    string Domain,
    long SequenceNumber,      // validated >= 1
    DateTimeOffset Timestamp,
    string CorrelationId,
    string CausationId,
    string UserId,
    string DomainServiceVersion,
    string EventTypeName,
    string SerializationFormat)
```

- 11 typed fields, `SequenceNumber` throws `ArgumentOutOfRangeException` if < 1
- `AggregateId` is populated as canonical form `{tenantId}:{domain}:{aggregateIdPart}`

**Server EventEnvelope (`src/Hexalith.EventStore.Server/Events/EventEnvelope.cs`):**

```csharp
public record EventEnvelope(
    string AggregateId,
    string TenantId,
    string Domain,
    long SequenceNumber,
    DateTimeOffset Timestamp,
    string CorrelationId,
    string CausationId,
    string UserId,
    string DomainServiceVersion,
    string EventTypeName,
    string SerializationFormat,
    byte[] Payload,
    IDictionary<string, string>? Extensions)
```

- Flat structure — all 11 metadata fields inlined plus payload and extensions
- Exposes computed `AggregateIdentity Identity` property
- `ToString()` also redacts payload
- Serialized to DAPR actor state store via `IActorStateManager.SetStateAsync`

**AggregateIdentity (`src/Hexalith.EventStore.Contracts/Identity/AggregateIdentity.cs`):**

```csharp
public record AggregateIdentity(string TenantId, string Domain, string AggregateId)
```

- Key computed properties: `ActorId` -> `"{TenantId}:{Domain}:{AggregateId}"`, `EventStreamKeyPrefix`, `MetadataKey`, `SnapshotKey`, `PubSubTopic`
- Validation: `TenantId`/`Domain` forced lowercase, `^[a-z0-9]([a-z0-9-]*[a-z0-9])?$`, max 64 chars; `AggregateId` case-sensitive, max 256 chars

**CloudEvents metadata (from `src/Hexalith.EventStore.Server/Events/EventPublisher.cs`):**

```csharp
var metadata = new Dictionary<string, string> {
    ["cloudevent.type"]   = eventEnvelope.EventTypeName,
    ["cloudevent.source"] = $"hexalith-eventstore/{identity.TenantId}/{identity.Domain}",
    ["cloudevent.id"]     = $"{correlationId}:{eventEnvelope.SequenceNumber}",
};
```

**AggregateMetadata (`src/Hexalith.EventStore.Server/Events/AggregateMetadata.cs`):**

```csharp
public record AggregateMetadata(long CurrentSequence, DateTimeOffset LastModified, string? ETag)
```

- Stored at `MetadataKey` — tracks sequence watermark for gapless numbering

**Related types:**

- `IEventPayload` — marker interface all domain events implement
- `IRejectionEvent : IEventPayload` — marker for rejection events
- `ISerializedEventPayload` — pre-serialized payloads (skip redundant serialization)
- `DomainServiceWireResult` / `DomainServiceWireEvent` — wire format for domain service responses

**JSON serialization conventions:**

- REST API wire format: **camelCase** (ASP.NET Core default)
- Event envelope metadata: **camelCase** (per architecture decision)
- No `[JsonPropertyName]` attributes on envelope types — relies on DAPR's built-in serialization
- Payload serialization: `System.Text.Json.JsonSerializer.SerializeToUtf8Bytes(payload, payload.GetType())`

**State store key patterns (for context, detail in Story 12-4):**

- Events: `{tenant}:{domain}:{aggId}:events:{seq}`
- Metadata: `{tenant}:{domain}:{aggId}:metadata`
- Snapshot: `{tenant}:{domain}:{aggId}:snapshot`

### `<details>` HTML Pattern (MUST follow if used — same as 12-1/12-2)

```html
<details>
    <summary>Text description</summary>

    [Full prose description here]
</details>
```

Note the blank line after `<summary>` closing tag and before `</details>`.

### Relationship to Adjacent Stories

**Story 12-2 (Command Lifecycle Deep Dive) — prerequisite:**

- Readers arrive from the command lifecycle's Next Steps link
- In the lifecycle page, Step 5 mentions "each event is wrapped in an EventEnvelope with metadata" and links here
- This page goes DEEPER into the envelope structure that was mentioned briefly there
- Do NOT re-explain the 5-step pipeline — reference the command lifecycle

**Story 12-4 (Identity Scheme) — next:**

- This page mentions the canonical identity format `{tenant}:{domain}:{aggregateId}`
- Do NOT deep-dive into the full identity scheme — that's 12-4's job
- Mention the format and link to 12-4 for the complete identity mapping, key patterns, and validation rules

**Story 12-1 (Architecture Overview) — related:**

- Readers understand the topology from this page
- Do NOT re-explain DAPR building blocks — reference the architecture overview

### Page Template Compliance (MUST follow)

Reference: `docs/page-template.md`

- Back-link: `[<- Back to Hexalith.EventStore](../../README.md)` (first line)
- One H1 heading only
- Summary paragraph after H1
- Prerequisites: max 2, blockquote syntax — link to architecture-overview and command-lifecycle
- Content: H2 sections, H3 subsections, never skip levels
- Next Steps footer: "Next:" + "Related:" links
- No YAML frontmatter
- Code blocks always with language tag
- All links relative
- No hard line wrap

### Code Examples — Simplified & Illustrative (MUST follow)

All code on the page must be **simplified, illustrative examples** — NOT verbatim source code. The JSON example should show the wire format (camelCase). The C# example should show the type shape (PascalCase, standard C# conventions).

**JSON example (Task 4) — must include:**

- All 11 metadata fields with Counter domain values
- **Payload as a base64-encoded string** (NOT a raw JSON object) — `byte[]` serializes to base64 in JSON. Example: `"eyJhbW91bnQiOjF9"` (base64 for `{"amount":1}`). Add a follow-up annotation decoding the payload for the reader.
- Extensions with a concrete example value like `{"x-priority": "normal"}` (NOT empty — show the reader what populated extensions look like)
- camelCase field names (wire format)

**C# example (Task 5) — show the Contracts type shape:**

```csharp
// Contracts.Events namespace — the type you reference as a domain service developer
public record EventMetadata(
    string AggregateId, string TenantId, string Domain,
    long SequenceNumber, DateTimeOffset Timestamp,
    string CorrelationId, string CausationId, string UserId,
    string DomainServiceVersion, string EventTypeName, string SerializationFormat);

public record EventEnvelope(EventMetadata Metadata, byte[] Payload, IReadOnlyDictionary<string, string>? Extensions);
```

### What NOT to Do

- Do NOT re-explain DAPR building blocks (Story 12-1) or the command pipeline (Story 12-2) — reference and link
- Do NOT deep-dive into identity scheme key patterns (Story 12-4) — mention and link
- Do NOT reproduce Dev Notes tables verbatim on the page — they are context for you, not page content
- Do NOT add Mermaid diagrams — this page is structural/tabular, not a visual flow
- Do NOT copy source code verbatim — write simplified illustrative examples
- Do NOT exceed the fenced block budget (2 code blocks max)
- Do NOT add YAML frontmatter
- Do NOT explain how to construct EventEnvelopes manually — this is done by EventStore internally
- Do NOT show the Server EventEnvelope fields as a code example — only show the Contracts types (the developer-facing API)
- Do NOT use PascalCase in the JSON example — use camelCase (wire format convention)
- Do NOT discuss storage key patterns in detail — that belongs in Story 12-4

### Testing Standards

- Run `markdownlint-cli2 docs/concepts/event-envelope.md` to verify lint compliance
- Verify all relative links resolve (expect dead link for `identity-scheme.md` until Story 12-4)
- **Self-containment test:** Read the page assuming only architecture-overview and command-lifecycle as prior knowledge
- **Progressive disclosure test:** First screen gives complete high-level explanation before drilling into field details
- **Field accuracy test:** Verify all 11 metadata fields match the actual `EventMetadata` record in source code

### File to Create

- **Create:** `docs/concepts/event-envelope.md` (new file)

### Project Structure Notes

- File path: `docs/concepts/event-envelope.md`
- Linked from: `docs/concepts/command-lifecycle.md` Next Steps footer (Story 12-2 has "**Next:** [Event Envelope & Metadata](event-envelope.md)")
- Links to: `docs/concepts/architecture-overview.md`, `docs/concepts/command-lifecycle.md`, `docs/concepts/identity-scheme.md` (12-4), `docs/getting-started/quickstart.md`

### Previous Story (12-2) Intelligence

**Patterns established in 12-1 and 12-2 that MUST be followed:**

- `<details>` HTML pattern with exact blank line spacing (if used)
- Progressive disclosure content ordering
- Self-containment with inline concept explanations before external links
- Counter domain as running example throughout
- Second-person tone, present tense
- No YAML frontmatter
- Page template compliance (back-link, H1, summary, prerequisites, Next Steps)
- Fenced block budgets

**12-2 status:** ready-for-dev (not yet implemented). **DEPENDENCY: Develop 12-1 and 12-2 BEFORE 12-3.** The command-lifecycle page mentions EventEnvelope during the persistence phase and links to this page. Write this page as if command-lifecycle exists.

**12-1 key patterns to reuse:**

- Same tone, same running example (Counter domain)
- Same page template structure
- Same compliance rules (no frontmatter, language-tagged code blocks, relative links)

### Git Intelligence

Recent commits show:

- Epic 11 (docs CI pipeline) completed — markdown linting and link checking now available
- Epic 16 (fluent client SDK API) completed — convention engine, assembly scanner, fluent API
- Architecture overview page (12-1) implemented at 235 lines with Mermaid diagrams

### References

- [Source: _bmad-output/planning-artifacts/epics.md, Epic 5 (renumbered as 12), Story 5.3]
- [Source: _bmad-output/planning-artifacts/architecture.md, Data Schemas section — Event Envelope 11-field metadata]
- [Source: _bmad-output/planning-artifacts/prd.md, FR12 — event envelope metadata structure]
- [Source: _bmad-output/planning-artifacts/prd-documentation.md, FR12, docs/concepts/event-envelope.md]
- [Source: docs/page-template.md, complete page structure rules]
- [Source: src/Hexalith.EventStore.Contracts/Events/EventEnvelope.cs, Contracts envelope type]
- [Source: src/Hexalith.EventStore.Contracts/Events/EventMetadata.cs, 11-field metadata record]
- [Source: src/Hexalith.EventStore.Contracts/Events/IEventPayload.cs, event marker interface]
- [Source: src/Hexalith.EventStore.Contracts/Events/IRejectionEvent.cs, rejection marker]
- [Source: src/Hexalith.EventStore.Contracts/Identity/AggregateIdentity.cs, identity type with key patterns]
- [Source: src/Hexalith.EventStore.Server/Events/EventEnvelope.cs, Server flat envelope type]
- [Source: src/Hexalith.EventStore.Server/Events/EventPublisher.cs, CloudEvents metadata injection]
- [Source: src/Hexalith.EventStore.Server/Events/EventPersister.cs, envelope construction during persist]
- [Source: src/Hexalith.EventStore.Server/Events/AggregateMetadata.cs, sequence watermark tracking]
- [Source: _bmad-output/implementation-artifacts/12-1-architecture-overview-with-mermaid-topology.md, previous story patterns]
- [Source: _bmad-output/implementation-artifacts/12-2-command-lifecycle-deep-dive.md, previous story patterns]

## Dev Agent Record

### Agent Model Used

Claude Opus 4.6

### Debug Log References

- Markdownlint: 0 errors on `docs/concepts/event-envelope.md`
- Fenced block count: 4 delimiters = 2 blocks (1 JSON, 1 C#) — budget met
- H1 count: exactly 1
- Solution build: succeeded with 0 warnings, 0 errors
- Page length: 300 lines (within ~300-350 target — compliance achieved)

### Senior Developer Review (AI)

- 2026-03-01: Adversarial review executed and findings applied automatically.
- High-severity claim mismatch resolved by reopening Task 10.6 (page-length target currently unmet).
- Medium-severity documentation mismatch resolved by recording working-tree discrepancy context.
- Story status updated to `in-progress` and sprint tracking synced.
- 2026-03-01: Follow-up adversarial review completed; all HIGH and MEDIUM findings fixed.
- Story status reconciled to `done` and sprint tracking synchronized to `done`.

### Completion Notes List

- Created `docs/concepts/event-envelope.md` with complete event envelope documentation
- All 10 tasks and 33 subtasks implemented: page template structure, "What Is an Event Envelope?" intro with analogy, 11-field metadata table with Counter domain examples, base64-encoded JSON wire format example, Contracts vs Server dual envelope type explanation, metadata population origin table, CloudEvents integration section, SEC-5 security considerations, and "Connecting the Dots" closing with 4 design principles
- Followed page template conventions: back-link, single H1, summary, 2 prerequisites (architecture-overview, command-lifecycle), Next Steps footer with identity-scheme forward link
- Exactly 2 fenced code blocks as budgeted (JSON envelope example + C# Contracts record definition)
- All internal links use relative paths; identity-scheme.md is an expected dead link until Story 12-4
- No YAML frontmatter, no Mermaid diagrams, no hard line wrapping
- Counter domain running example used throughout (IncrementCounter → CounterIncremented)
- Code review remediation applied: status/task/debug references updated for traceability accuracy
- Expanded page content from 160 lines to 300 lines to meet Task 10.6 page-length target (~300-350). Added deeper explanations for metadata field groups, correlation/causation tracing, timestamp vs sequence ordering, per-aggregate sequence scoping, rejection events, extensions conventions, CloudEvents attributes, security redaction details, and metadata population flow. All expansions are prose-only — no additional code blocks added.
- Tier 1 tests: 465 passed (157 Contracts + 231 Client + 29 Sample + 48 Testing), 0 failures

### Working Tree Change Context (Review-Time)

The following files were detected as changed in the working tree during review but are outside the direct scope of Story 12-3 page content:

- `.lycheeignore`
- `CONTRIBUTING.md`
- `README.md`
- `.github/workflows/docs-validation.yml`
- `docs/concepts/architecture-overview.md`
- `docs/concepts/command-lifecycle.md`
- `docs/concepts/event-envelope.md`

### File List

- **Created:** `docs/concepts/event-envelope.md`
- **Modified:** `_bmad-output/implementation-artifacts/12-3-event-envelope-metadata-structure.md` (review remediation sync)
- **Modified:** `_bmad-output/implementation-artifacts/sprint-status.yaml` (story state synchronization)

### Change Log

- 2026-03-01: Created event envelope metadata structure documentation page (Story 12-3)
- 2026-03-01: Applied code-review remediation updates (status/task/debug/file-list sync)
- 2026-03-01: Expanded page to 300 lines (Task 10.6 compliance). All tasks complete.
- 2026-03-01: Applied adversarial review auto-fixes (HIGH/MEDIUM) and synchronized story/sprint status to `done`.
