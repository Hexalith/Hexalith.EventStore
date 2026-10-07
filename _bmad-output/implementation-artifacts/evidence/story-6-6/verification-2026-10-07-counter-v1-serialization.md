# Story 6.6: source-derived Counter V1 serializer declarations

The root-owned Counter domain now supplies the concrete bounded serializer
declarations previously absent from the sample. Its host calls
`AddCounterEventSerialization` after the normal domain-service registration.
The existing router selects the keyed bounded producer for Counter success,
rejection, termination and no-op results. Greeting and undeclared external
domains retain their existing compatibility behavior. No V2 admission or
readiness fence changes.

## Source and bounds

The five Counter application events are sealed empty marker records. Their
existing default `JsonSerializer.SerializeToUtf8Bytes(payload, payload.GetType())`
image is exactly `{}` (`7b7d`). The declarations retain each exact CLR full-name
alias and `json` format with a two-byte output ceiling. Specialized fixed-byte
writers use no general serializer, custom converters, mutable JSON options or
variable-sized token buffer. A source-inventory test refuses additional event
types or data members until these declarations are revised.

Counter's explicit `AggregateTerminated` adapter retains the framework's exact
V1 alias and PascalCase payload shape. The aggregate type is fixed to
`CounterAggregate`. `AggregateIdentity` validates the emitted identifier before
writing, admitting at most 256 ASCII characters from its existing identifier
grammar; none require JSON escaping. The exact maximum is 51 prefix bytes plus
256 identifier bytes plus two suffix bytes: **309 bytes**. Its one 256-byte stack
token buffer is cleared in `finally`. A foreign aggregate route, invalid
identifier or undeclared result payload is refused.

[The machine-readable declarations](counter-v1-serialization-2026-10-07/serializer-declarations.json)
record these source-derived types, aliases, byte shapes and bounds.
[The input capture](counter-v1-serialization-2026-10-07/inputs.json) hashes the
actual writer, router, identity, domain, test and legacy serializer inputs at
the observed source revision. This inventory is a local application serializer
declaration, not a sealed event-evolution registry, authenticated dependency
closure, immutable artifact execution binding, gateway capability pin or
negotiated writer readiness record.

## Verification

The following Debug/source build was run after the final test changes:

```sh
dotnet build tests/Hexalith.EventStore.Sample.Tests/Hexalith.EventStore.Sample.Tests.csproj --configuration Debug -m:1 -warnaserror -p:UseHexalithProjectReferences=true
```

It exited zero with **zero warnings and zero errors**. That build result is a
session-tool observation; the final test commands, output, XML and before/after
managed-image/runtime/source hashes are retained separately below. Each captured
test subprocess had a 60-second timeout, and all recorded source and image hashes
stayed unchanged during its execution.

| Check | Result | Capture |
| --- | --- | --- |
| `CounterEventSerializationTests` in the rebuilt Debug assembly | 17 passed; zero failures, skips or errors | [command/hashes](counter-v1-serialization-2026-10-07/focused.json), [log](counter-v1-serialization-2026-10-07/focused.log), [XML](counter-v1-serialization-2026-10-07/focused.xml) |
| Full rebuilt Sample assembly | 175 passed; zero failures, skips or errors | [command/hashes](counter-v1-serialization-2026-10-07/sample-full.json), [log](counter-v1-serialization-2026-10-07/sample-full.log), [XML](counter-v1-serialization-2026-10-07/sample-full.xml) |

The focused controls compare every real Counter command outcome with the existing
legacy serializer, independently assert `{}` marker bytes and the exact 309-byte
termination image, reject foreign/invalid/undeclared results, preserve cancellation
at the domain boundary, admit 1,000 two-byte events while refusing 1,001, and retain
the independent Greeting wire behavior. V1 metadata remains absent exactly as in
the old route; no stable identity triplet is advertised without negotiation.

Before runtime edits, the explicitly selected EventStore AppHost was started and
inspected through Aspire. Resources reached Running/Healthy apart from the finished
Tenants executable. The owned startup instances were stopped using the explicitly
selected `aspire stop` before builds. These are uncaptured local Development
observations and establish no event-evolution or production qualification.

## Remaining scope

This completes the sample's concrete bounded V1 serializer declaration and
registration slice only. It does not replace every domain's compatibility
fallback, register an authenticated shared evolution reader, supply all D/V/A/E/F/S
and transitive catalog artifacts, qualify loader observations or Dapr control
transactions, or close migration/fleet/package/production obligations. The
PostgreSQL/Dapr logical-byte work is a separate evidence lane. All M1–M8 parent
tasks and V2/proof-dependent activation fences remain open.
