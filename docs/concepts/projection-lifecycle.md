[← Back to Hexalith.EventStore](../../README.md)

# Projection Lifecycle

For the production `/project/v2` duplicate, ordering, reservation, completion, retention, and reconciliation boundary, see [Projection delivery guarantees](projection-delivery.md).

`QueryResponseMetadata.Lifecycle` preserves projection operation state without collapsing it into the legacy `IsStale` and `IsDegraded` Booleans. The stable values are `Unknown`, `Current`, `Stale`, `Rebuilding`, `Degraded`, `Unavailable`, and `LocalOnly`.

Lifecycle is authoritative only when `QueryResponseMetadata.Provenance` is `ProjectionBacked`. Handler-computed, missing, invalid, cached, or contradictory evidence resolves to `Unknown`. EventStore never infers lifecycle from an ETag, HTTP success or `304`, payload fields, SignalR, or compatibility Booleans.

| Lifecycle | Authoritative source | Default consumer behavior |
| --- | --- | --- |
| `Current` | Persisted projection timestamp within the current or legacy aging band | May enable an otherwise-authorized mutation |
| `Stale` | Persisted projection timestamp beyond the stale threshold | Show last-known data with a warning; disable mutation |
| `Rebuilding` | Producer-confirmed active rebuild | Show in-progress state; disable mutation |
| `Degraded` | Producer-confirmed serviceable output with an affected dependency or capability | Explain the consequence; disable mutation unless a consumer documents an exception |
| `Unavailable` | Producer-confirmed authoritative storage or projection failure | Show a safe retry/support action; disable mutation |
| `LocalOnly` | Explicit non-authoritative local fallback | Label local-only; never claim projection-confirmed success |
| `Unknown` | No qualifying authoritative evidence | Show neutral unknown state; disable mutation |

The canonical header is `X-Hexalith-Projection-Lifecycle`. A projection-backed `200` or `304` emits one exact enum name only when its authoritative lifecycle is non-`Unknown`. Responses with `Unknown` lifecycle and non-projection responses omit it. A cached projection result clears lifecycle because time-sensitive `Current` evidence cannot be replayed safely; a bodyless `304` can recover lifecycle only from its authoritative header.

Legacy compatibility is one-way. `Current` maps to `IsStale = false`, `Stale` maps to `IsStale = true`, and operational states do not fabricate stale/current evidence. `Degraded` maps to the additive `IsDegraded` view. Neither Boolean creates a lifecycle value.

`ProjectionLifecyclePolicy.IsProjectionConfirmed` and `CanMutate` expose the EventStore-owned default policy. Consumer UI adoption, accessible text/status presentation, localization, and any documented mutation exception remain consumer-owned. Parties-style states map directly by the same names; `LocalOnly` remains visibly non-authoritative.
