# Payload and Snapshot Protection Hooks

EventStore exposes optional infrastructure hooks for protecting event payloads and snapshot state
before they are persisted, and for unprotecting them before they are published or replayed. This
guide covers the **provider-neutral hook contract** introduced in Story 22.7a:

- The shape of the protection metadata that EventStore stamps onto persisted events and snapshots.
- The default no-op behavior and how to register a custom protection provider.
- The fail-closed rules EventStore applies when it encounters legacy, malformed, or unknown
  protection metadata.

> **Out of scope for this guide.** Real encryption providers, key lifecycle (deletion, rotation,
> invalidation), missing-key behavior, crypto-shredding workflows, restored-backup safety, and
> full operational redaction across the Admin UI, CLI, MCP, ProblemDetails, and backup tooling are
> tracked in Stories 22.7b, 22.7c, and 22.7d. This page documents only the hook and metadata
> contract that those follow-up stories build on.

## Default behavior

Without an explicit registration, EventStore uses `NoOpEventPayloadProtectionService`. Every
event payload and snapshot is persisted and published exactly as-is, except that EventStore now
stamps explicit protection metadata onto the persisted envelope so downstream consumers can
distinguish "this row is explicitly unprotected" from a legacy row that predates Story 22.7a.

The no-op metadata record is:

```text
State            = Unprotected
MetadataVersion  = 1
Scheme           = null
KeyAlias         = null
ContentHint      = null
CompatibilityFlags = null
```

## Metadata shape

`EventStorePayloadProtectionMetadata` lives in `Hexalith.EventStore.Contracts.Security` and is a
small immutable record:

| Field | Description |
| --- | --- |
| `State` | `Unprotected`, `Protected`, or `ProviderOpaque`. |
| `MetadataVersion` | Schema version (currently `1`). Unknown future versions fail-closed to `ProviderOpaque`. |
| `Scheme` | Optional provider-neutral scheme identifier (e.g. `"aes-gcm-256"`). Required when `State == Protected`. |
| `KeyAlias` | Optional non-secret key reference. Treated sensitive-by-default by callers. |
| `ContentHint` | Optional content-type hint (e.g. `"application/json"`). |
| `CompatibilityFlags` | Optional small bounded `IReadOnlyDictionary<string, string>` for forward-compatible flags. |

The metadata never carries raw key material, plaintext, IVs/nonces, authentication tags,
provider-private blobs, state-store keys, or connection strings. The carrier validates against a
forbidden-substring/keyword list and length-bounds every field.

## Storage layout

- **Events.** EventStore stamps the JSON-serialized protection metadata into the persisted server
  `EventEnvelope.Extensions` dictionary under the reserved key `eventstore.protection`. Other
  extension entries are preserved unchanged.
- **Snapshots.** `SnapshotRecord` carries an optional `ProtectionMetadata` field of type
  `EventStorePayloadProtectionMetadata?`. Old snapshots that predate Story 22.7a deserialize with
  `null` here and are mapped to the **legacy** compatibility state on load.

The carrier exposes static helpers in `EventStorePayloadProtectionMetadataCarrier`:

- `Read(extensions)` returns the typed metadata for any envelope, falling back to the legacy
  compatibility record when no `eventstore.protection` entry exists.
- `Write(extensions, metadata)` returns a new dictionary with the metadata applied.
- `Read(serialized)` parses a serialized JSON string. Malformed JSON, unknown schema versions,
  forbidden secret-shaped fields, and missing required fields all map to
  `PayloadProtectionState.ProviderOpaque`, never to `Unprotected`.

## State machine

```text
                  ┌───────────────┐
                  │ Unprotected   │  ← no-op provider, legacy reads, post-unprotect
                  └──────▲────────┘
                         │
                         │ (Protect)
                         ▼
                  ┌───────────────┐
                  │   Protected   │  ← provider-emitted (Scheme required)
                  └──────▲────────┘
                         │
                         │ (malformed metadata, unknown version, forbidden shape)
                         ▼
                  ┌────────────────┐
                  │ ProviderOpaque │  ← carry-only; never decrypted or republished
                  └────────────────┘
```

### Fail-closed rules

- Missing metadata → **legacy** compatibility record (`State = Unprotected`, `CompatibilityFlags["legacy"] = "missing"`).
- Malformed JSON, unknown enum value, unknown schema version, missing required `State` or
  `Scheme` for `Protected` → `ProviderOpaque` with `CompatibilityFlags["reason"]`.
- Forbidden secret-shaped fields detected on read → `ProviderOpaque` with a `forbidden` reason.

EventStore never downgrades any of these states to `Unprotected` by inference.

## Hook boundaries

| Hook | Caller | When |
| --- | --- | --- |
| `ProtectEventPayloadAsync` | `EventPersister` | After payload bytes are computed, before `IActorStateManager.SetStateAsync`. |
| `UnprotectEventPayloadAsync` | `EventPublisher` | Once per stored envelope, before constructing the DAPR publish envelope. |
| `ProtectSnapshotAsync` | `SnapshotManager` | Before `IActorStateManager.SetStateAsync` writes the new snapshot. |
| `UnprotectSnapshotAsync` | `SnapshotManager` | After reading the snapshot from storage. |

All four hooks receive the caller's `CancellationToken`. `OperationCanceledException` propagates
without being mapped to an infrastructure failure.

`ProviderOpaque` records are **passed through verbatim**. EventStore never invokes
`UnprotectEventPayloadAsync` or `UnprotectSnapshotAsync` on opaque bytes, never publishes them in
plaintext, and never treats them as safely unprotected.

## Publish-time policy (22.7a)

EventStore continues to call `UnprotectEventPayloadAsync` before DAPR publication so existing
subscribers and domain services see plaintext payloads. The published envelope's
`eventstore.protection` extension carries the metadata returned by the provider, so subscribers
can distinguish protected-at-rest events without inspecting bytes.

Provider-opaque envelopes are published verbatim with the opaque metadata intact. A fully
fail-closed publish-time policy that refuses to publish `Protected` bytes when the provider
cannot unprotect them is **deferred to Story 22.7b** alongside missing-key behavior.

## Registering a custom provider

```csharp
public sealed class MyProtectionService : IEventPayloadProtectionService {
    public Task<PayloadProtectionResult> ProtectEventPayloadAsync(
        AggregateIdentity identity,
        IEventPayload eventPayload,
        string eventTypeName,
        byte[] payloadBytes,
        string serializationFormat,
        CancellationToken cancellationToken = default) {
        // Encrypt payloadBytes (provider-private) and return the cipher with a Protected metadata
        // record describing the scheme and a non-secret key alias.
        var protectedBytes = MyCrypto.Encrypt(payloadBytes, cancellationToken);
        var metadata = new EventStorePayloadProtectionMetadata(
            State: PayloadProtectionState.Protected,
            MetadataVersion: 1,
            Scheme: "myco-aead-v1",
            KeyAlias: $"tenant:{identity.TenantId}:event-payload",
            ContentHint: serializationFormat,
            CompatibilityFlags: null);
        return Task.FromResult(new PayloadProtectionResult(protectedBytes, serializationFormat, metadata));
    }

    public Task<PayloadProtectionResult> UnprotectEventPayloadAsync(
        AggregateIdentity identity,
        string eventTypeName,
        byte[] payloadBytes,
        string serializationFormat,
        CancellationToken cancellationToken = default) {
        // Decrypt and return Unprotected metadata so the published envelope advertises plaintext.
        var plain = MyCrypto.Decrypt(payloadBytes, cancellationToken);
        return Task.FromResult(new PayloadProtectionResult(
            plain,
            serializationFormat,
            EventStorePayloadProtectionMetadata.Unprotected()));
    }

    // Snapshot hooks: implement ProtectSnapshotAsync and UnprotectSnapshotAsync (typed) or
    // continue using ProtectSnapshotStateAsync / UnprotectSnapshotStateAsync (default delegating
    // implementations exist on the interface for backward compatibility).
}
```

Register the service before the default registration:

```csharp
services.AddSingleton<IEventPayloadProtectionService, MyProtectionService>();
services.AddEventStoreServer(builder.Configuration);
```

## Doing nothing changes nothing

If you do not register a custom protection service, EventStore behaves exactly as it did before
Story 22.7a — the only observable change is the presence of an explicit
`eventstore.protection` entry on newly persisted event envelopes describing the no-op state.
Existing rows that predate Story 22.7a remain readable and project to the legacy compatibility
record on load.
