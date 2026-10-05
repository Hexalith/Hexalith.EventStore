# Story 6.6 provider decision: PostgreSQL actor transaction capture

**Status:** Proposed for owner review. No provider extension or runtime activation is approved by this record.

## Evidence and requested decision

The [PostgreSQL v1 feasibility probe](postgresql-v1-feasibility.md) found that the pinned stock Dapr provider normalizes the event envelope through `jsonb` and cannot retrieve a complete earlier committed head and ETag image after another append. [AD-13 §§3–4](../../spec-event-versioning-upcasting.md) requires exact raw bytes, a complete same-save mutation image, and authenticated historical committed-generation lookup. A separate application journal after `SaveStateAsync` would add a second commit boundary and cannot supply that proof.

**Requested approval:** Authorize design and implementation of a maintained PostgreSQL v1 state-provider variant for the pinned Dapr runtime, with transaction capture and bounded committed-generation lookup as specified below. Approval would permit source and local qualification work. It would not approve deployment, production credentials, V2 writes, retained-data migration, or readiness; those retain their separate AD-13 and AD-26 gates.

Dapr's [state-store component model](https://docs.dapr.io/operations/components/setup-state-store/) and [Go state-store interface](https://v1-18.docs.dapr.io/developing-applications/develop-components/pluggable-components/pluggable-components-sdks/pluggable-components-go/go-state-store/) locate transactional state writes at the provider boundary. This proposal does not choose an app-side journal or a PostgreSQL v2 migration.

## Proposed provider contract

1. The provider captures the exact bytes it receives for every event envelope and sidecar, plus the complete actor save participant set: addressed keys, before and after images, logical version, ETag, backend identity, provider generation and fence. It records an immutable generation image **inside the same PostgreSQL transaction** as the actor-state mutation. If capture cannot be completed, the actor-state transaction aborts.
2. The provider owns a retained journal separate from the application-owned `hexalith_eventstore_control` schema. It exposes bounded lookup of a committed generation and addressed raw event range. A lookup returns original `bytea` bytes, exact member presence, complete old/new mutation images and the current addressed head/ETag. It never reconstructs a historical generation from today's head or from a caller-supplied staged list.
3. The trusted EventStore adapter authenticates the backend, journal generation, fence, key range, byte hashes and complete participant images before it issues the exact AD-13 purpose-10 raw-readback and purpose-11 commit-receipt carriers. Signing keys are bound to the configured provider trust anchor; caller JSON and application metadata rows cannot create provider authority.
4. Retention covers each event's readable lifetime and every open backup, rollback, replay, delivery or incident obligation. Deletion requires authenticated closure of all such obligations and tenant-scoped erasure authority. Failed lookup, expired evidence, changed bytes or incomplete retention yields `RawSourceUnavailable` or `ActorCommitEvidenceHold`, without typed fallback.
5. The lookup enforces AD-13's 1–256 event page, 128 MiB raw-page, 64 MiB readable-page and 1 MiB proof limits before whole-body or typed materialization. It preserves the existing actor key and MessageId, and does not change the application-owned metadata adapter or add a post-save write.

## Qualification before use

- Pin and review the provider source, Dapr runtime build, journal DDL, backend identity, TLS/OpenBao roles and signing-key lifecycle. Demonstrate the new provider still satisfies existing actor state, ETag and transactional behavior.
- With two hosts on one real backend, prove exact raw-byte and historical-generation lookup after later appends; multi-key atomicity; lost acknowledgments; crash before, during and after commit; owner takeover; tampering; cancellation; bounded reads and memory; and retained-key rotation/revocation. Inspect stored rows, ETags, images and receipts, not only API status.
- Keep all Story 6.6 M2/M8 and slice activation gates open until these checks, the production profile, and the remaining AD-13 obligations pass.

**If declined:** Keep the stock provider and the current fail-closed V2/readiness fences. The existing typed reads and local PostgreSQL control-kernel tests do not satisfy authenticated event readback.
