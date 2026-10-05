# Story 6.6 dependency-loader decision

**Status:** Proposed for owner review. The local graph verifier is not startup attestation and grants no readiness.

## Evidence and requested decision

[AD-13 §5](../../spec-event-versioning-upcasting.md) requires the exact resolved managed and native dependency graph for every implementation, handler, registration adapter, filter, receipt provider and readback codec. The gateway pins the complete manifest and fingerprint. A serving process must detect or prevent later unmanifested implementation loads. The current default .NET load context and the local `EventDependencyClosureVerifier` can compare a supplied graph with pinned G rows, but they cannot prove that the supplied graph is complete or lock future loads.

**Requested approval:** Choose a controlled loading boundary for event-evolution implementations and cataloged consumers, then authorize a qualification spike before migration of existing registrations. The recommended candidate is a dedicated `AssemblyLoadContext` with explicit managed and native resolution from content-addressed, hash-checked files and a closed shared-framework allowlist. The host would admit the gateway-pinned manifest before loading any catalog route, resolve all declared roots and edges, and invalidate readiness before invoking a newly observed or unmanifested dependency.

That candidate still needs a separate enforceable policy for explicit `NativeLibrary.Load`, reflection-driven loads and code that can escape the controlled context. Merely recording `AssemblyLoad` events or scanning metadata references is an audit, not a lock. The spike must show how those paths are prevented or cause fail-closed capability loss before invocation. If it cannot, M1 readiness stays false.

The [Microsoft dependency-loading overview](https://learn.microsoft.com/dotnet/core/dependency-loading/overview) describes the separate managed and native loading paths; the [native loading algorithm](https://learn.microsoft.com/dotnet/core/dependency-loading/loading-unmanaged) and [per-assembly resolver contract](https://learn.microsoft.com/dotnet/api/system.runtime.interopservices.nativelibrary.setdllimportresolver) are inputs to the spike, not evidence that the proposed boundary is already complete.

## Qualification before migration

- Inventory every D/V/A/E/F/S implementation and every cataloged handler, adapter, filter, receipt provider and codec; prove the gateway and all serving peers use identical pinned manifest bytes and fingerprint.
- Exercise managed, native, reflection and late-load attempts, changed files, missing rows, ambiguous loader identities and nonlocal catalog rows. Every unmanifested executing dependency must refuse capability before callbacks or effects.
- Measure startup and call-path cost under the 64 MiB manifest and 65,536-row limits; retain exact dependency bytes and options for the required history lifetime. Preserve existing unversioned package and domain registration behavior until the new boundary is qualified.

**If declined or the spike cannot enforce closure:** Retain the current loader and keep M1 registry readiness and event-evolution activation fenced. The local pinned-graph verifier remains useful only for supplied-graph checks.
