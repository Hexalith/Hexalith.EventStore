# Story 6.6 dependency-loader decision

**Status:** The owner selected reviewed trusted application code on 2026-10-06.
The [approved loader amendment](../../story-6-6-trusted-code-amendment.md) now controls
the trust and loading policy. The dedicated-context candidate's failed qualification
remains historical evidence. The local graph verifier grants no readiness.

## Historical requirement and candidate

[AD-13 §5](../../spec-event-versioning-upcasting.md) requires the exact resolved managed and native dependency graph for every implementation, handler, registration adapter, filter, receipt provider and readback codec. The gateway pins the complete manifest and fingerprint. A serving process must detect or prevent later unmanifested implementation loads. The current default .NET load context and the local `EventDependencyClosureVerifier` can compare a supplied graph with pinned G rows, but they cannot prove that the supplied graph is complete or lock future loads.

The original candidate was a dedicated `AssemblyLoadContext` with explicit managed and native resolution from content-addressed, hash-checked files and a closed shared-framework allowlist. The host would admit the gateway-pinned manifest before loading any catalog route, resolve all declared roots and edges, and invalidate readiness before invoking a newly observed or unmanifested dependency.

That candidate still needs a separate enforceable policy for explicit `NativeLibrary.Load`, reflection-driven loads and code that can escape the controlled context. Merely recording `AssemblyLoad` events or scanning metadata references is an audit, not a lock. The spike must show how those paths are prevented or cause fail-closed capability loss before invocation. If it cannot, M1 readiness stays false.

The [Microsoft dependency-loading overview](https://learn.microsoft.com/dotnet/core/dependency-loading/overview) describes the separate managed and native loading paths; the [native loading algorithm](https://learn.microsoft.com/dotnet/core/dependency-loading/loading-unmanaged) and [per-assembly resolver contract](https://learn.microsoft.com/dotnet/api/system.runtime.interopservices.nativelibrary.setdllimportresolver) are inputs to the spike, not evidence that the proposed boundary is already complete.

## Local qualification — 2026-10-05

The [isolated qualification](dependency-loader-qualification-2026-10-05.md) passed its pinned positive and ordinary undeclared-load refusal controls. Explicit managed loads into Default and another context still executed an owned effect after the observer invalidated readiness. Explicit `NativeLibrary.Load(string)` executed an owned constructor and export without calling either native resolver hook, before the host rejected capability. Exact source/binary hashes, failed setup attempts and all six final observations are retained. This negative result applies to the dedicated-context-plus-observation candidate tested here.

The probe required an owner decision on explicit loads and nested effects. The
owner has now selected the reviewed trusted-code policy described below. The
failed before-effect controls are preserved; they are not reclassified as passes.

## Owner decision — 2026-10-06

The owner approved option 3 from [the comparison](dependency-loader-options-2026-10-06.md)
with “do recommended”. Trust covers the reviewed catalog and its dependency chain;
tenant input cannot introduce executing code. Verify immutable declared artifacts
and complete pins before admission. Prohibit undeclared explicit loading through
the reviewed code policy and qualify declared exceptions. Use observations to
detect violations and fence subsequent capability, without claiming confinement
or prevention of effects that already executed. The amendment replaces only the
conflicting stronger loader assurance; other activation gates remain in force.

## Qualification before migration

- Inventory every D/V/A/E/F/S implementation and every cataloged handler, adapter, filter, receipt provider and codec; prove the gateway and all serving peers use identical pinned manifest bytes and fingerprint.
- Refuse changed files, missing rows, ambiguous identities and incomplete declared graphs before admission. Demonstrate execution uses the same immutable checked artifacts. Exercise managed, native, reflection and late-load observations and subsequent capability loss under the approved policy; retain any effects that precede observation as limitations.
- Measure startup and call-path cost under the 64 MiB manifest and 65,536-row limits; retain exact dependency bytes and options for the required history lifetime. Preserve existing unversioned package and domain registration behavior until the new boundary is qualified.

**Until qualified:** Retain the current loader and keep M1 registry readiness and
event-evolution activation fenced. The local pinned-graph verifier remains useful
only for supplied-graph checks. Selection of a trust policy supplies neither an
authoritative catalog nor production qualification.
