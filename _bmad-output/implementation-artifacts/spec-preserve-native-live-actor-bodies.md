---
title: Preserve native large actor bodies in live admission fixtures
status: done
baseline_commit: ad383a96394307154b0026643245fdbb9a24dd4e
created: 2026-10-31
---

## Intent

Hosted integration 41201936726 reported six NamedProjection real-sidecar failures after the strict admission fixture introduced a global 1 MiB buffering cap. That cap rejected native binary actor calls and ordinary large JSON replay requests before their existing production guards could execute.

Actor partitions and root-stamped calls already select their immutable snapshot without reading the body, so they do not need fixture buffering. Unstamped JSON still needs exact replay after model binding; retain disk-spilling buffering and preserve the native server request-size limit. Keep real admission, signed capabilities, leases, authorization, persistence, typed-page refusals and all replay cost assertions unchanged.

## File Scope

Allowed files for this story:

- `tests/Hexalith.EventStore.Server.LiveSidecar.Tests/Fixtures/LiveAdmissionCatalogStartupFilter.cs`
- `tests/Hexalith.EventStore.Server.LiveSidecar.Tests/Fixtures/LiveAdmissionCatalogStartupFilterTests.cs`
- `_bmad-output/implementation-artifacts/spec-preserve-native-live-actor-bodies.md`

## Verification Results

The Release build passed with zero warnings and errors. All 18 strict authority and middleware checks passed without skips, including 2 MiB actor, stamped and unstamped bodies plus byte-exact JSON replay.

The four unchanged real-sidecar method families passed 17/17 in 740.417 seconds, with zero failures, errors, skips or unrun cases. This includes all six hosted failures: the 10,000-event replay recovery, both 9,999/10,000 current matrices, both 9,000/9,999 source-guard streams, and the oversized typed-page refusal before effects. No production limit, cost budget, assertion, case or authorization rule changed.

The first repair excluded only actor/stamped requests. Its preserved failed run proved that the same cap also rejected unstamped replay JSON. The final repair leaves Kestrel's native request-size bound authoritative for that JSON.

The live run executed these existing methods:

- ReplayRecovery_GenericHandlerReusesOneOriginalManifestAcrossThreeLostDurableResponses
- SourceGuard_CurrentMatrixUsesExactlyElevenMetadataReadsAndPreservesEveryOwningSurface
- SourceGuard_RealActorHandlerAndStores_ProveTailThenReadOnlyCurrent
- SourceGuard_RealActorOversizedEnvelopeReturnsTypedPageFailureBeforeAnyEffects

Evidence logs and XML are retained under `/tmp/es-large-body-fixture-*`:

- `es-large-body-fixture-build-v2.log` SHA256 `23c8d1a00e310299014c050d17a7affda5cfb3f2eb4ce4d3864a6ab18af7c09b`

- `es-large-body-fixture-focused-v2.xml` SHA256 `ad6f43c671060b6afaeb35513351f50921591fe432a2e672b2461b621ea24c42`

- `es-large-body-fixture-live-replay-v2.xml` SHA256 `61cd66b977459b67336ca6159829a18e0ca8a188d53e290b1a316aafd421c262`
