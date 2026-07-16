# Reviewer Gate — Technology And Currentness Lens (2026-07-15 Update)

Verdict: **changes required**. AD-19's implemented protocol and AD-21's ownership choice fit the codebase, but the updated spine is not current enough to hand off unchanged: it reports a superseded .NET security patch level, describes a FrontComposer source/package boundary that does not yet exist in EventStore, and contains two additional local-reality mismatches.

## Scope And Evidence

Reviewed `architecture.md`, with emphasis on AD-19, AD-21, the two Mermaid dependency/topology views, Structural Seed, and every Stack row.

Local evidence:

- `global.json`, `Directory.Build.props`, root `Directory.Packages.props`, and `references/Hexalith.Builds/Props/Directory.Packages.props`
- `src/Hexalith.EventStore.Admin.UI/*`, AppHost/Aspire resource wiring, and topology/security tests
- v2 projection contracts, `DomainProjectionDispatcher`, `NamedProjectionDispatchCoordinator`, `ProjectionDispatchOptions`, and focused/live-sidecar tests
- root-declared `references/Hexalith.FrontComposer` source at `0a84e818b0ce220f291510ad094340f7296bb488` (`v3.2.2-8-g0a84e818`), including Shell/Contracts.UI projects and navigation APIs

Primary upstream evidence:

- [.NET 10.0.10 release notes](https://github.com/dotnet/core/blob/main/release-notes/10.0/10.0.10/10.0.10.md) — released 2026-07-14, includes SDK 10.0.302 and ASP.NET Core 10.0.10, and contains security fixes.
- [Aspire 13.4 release notes](https://aspire.dev/whats-new/aspire-13-4/) and official NuGet registry indexes for [Aspire.Hosting](https://api.nuget.org/v3-flatcontainer/aspire.hosting/index.json), [Keycloak](https://api.nuget.org/v3-flatcontainer/aspire.hosting.keycloak/index.json), [Kubernetes](https://api.nuget.org/v3-flatcontainer/aspire.hosting.kubernetes/index.json), and [CommunityToolkit Dapr](https://api.nuget.org/v3-flatcontainer/communitytoolkit.aspire.hosting.dapr/index.json).
- Official NuGet registry indexes for [Dapr.Client](https://api.nuget.org/v3-flatcontainer/dapr.client/index.json), [MediatR](https://api.nuget.org/v3-flatcontainer/mediatr/index.json), [FluentValidation](https://api.nuget.org/v3-flatcontainer/fluentvalidation/index.json), [Roslyn](https://api.nuget.org/v3-flatcontainer/microsoft.codeanalysis.csharp/index.json), [Fluent UI Blazor](https://api.nuget.org/v3-flatcontainer/microsoft.fluentui.aspnetcore.components/index.json), [OpenTelemetry hosting](https://api.nuget.org/v3-flatcontainer/opentelemetry.extensions.hosting/index.json), [OpenTelemetry runtime instrumentation](https://api.nuget.org/v3-flatcontainer/opentelemetry.instrumentation.runtime/index.json), [xUnit v3](https://api.nuget.org/v3-flatcontainer/xunit.v3/index.json), [Shouldly](https://api.nuget.org/v3-flatcontainer/shouldly/index.json), [NSubstitute](https://api.nuget.org/v3-flatcontainer/nsubstitute/index.json), [Hexalith.Commons.UniqueIds](https://api.nuget.org/v3-flatcontainer/hexalith.commons.uniqueids/index.json), [FrontComposer.Shell](https://api.nuget.org/v3-flatcontainer/hexalith.frontcomposer.shell/index.json), and [FrontComposer.Contracts.UI](https://api.nuget.org/v3-flatcontainer/hexalith.frontcomposer.contracts.ui/index.json).

No currentness or fit assertion in this review is based only on model training data.

## High Findings

### H1 — The .NET/ASP.NET rows are one security servicing release behind

**Evidence:** The spine and repository pin SDK `10.0.302` and ASP.NET Core/SignalR `10.0.9`. Microsoft released .NET `10.0.10` on 2026-07-14 with SDK `10.0.302`; the official notes explicitly identify security and non-security fixes and list the 10.0.10 ASP.NET/SignalR packages. `global.json` uses `rollForward: latestPatch`, so an installed 10.0.302 is admissible, but the stated `10.0.302` Stack value and the central `10.0.9` package pins are no longer the current serviced baseline.

**Impact:** The spine's claim that named technology is verified-current is false on its own update date, and implementers could treat a security-superseded patch as the approved build baseline.

**Disposition:** **Discuss/update upstream pins, then autofix the Stack.** Prefer SDK `10.0.302` and ASP.NET/SignalR `10.0.10`. If the repository deliberately holds back, the spine must say this is a reviewed lag with a revisit condition; it must not present the old patch as current.

### H2 — AD-21 is a sound target, but its Stack/dependency rendering asserts an unimplemented and unpinned FrontComposer boundary

**Evidence:** The ownership decision fits brownfield reality: `src/Hexalith.EventStore.Admin.UI` exists; AppHost resource, DAPR app id, and container repository are all `eventstore-admin-ui`; the host already uses typed client services and Fluent UI. Current FrontComposer source provides `FrontComposerShell`, `Hexalith.FrontComposer.Contracts.UI`, declarative `FrontComposerNavEntry`, and route-based active navigation, so one **Event Store Admin** entry plus deep-link selection is technically compatible.

However, EventStore `Admin.UI.csproj` currently references neither FrontComposer package/project. `Directory.Build.props` defaults cross-repository dependencies to NuGet and has no FrontComposer source-switch wiring. Imported central props pin `Hexalith.FrontComposer.Shell` to `3.1.1` but contain **no** `Hexalith.FrontComposer.Contracts.UI` `PackageVersion`; the checked-out source is already beyond published `3.2.2`. Therefore the Stack row “Root-declared source in Debug; centrally pinned NuGet packages in Release” is not current repository reality, and the dependency diagram visually presents the target dependency as if it already exists.

**Impact:** Story 7.14 cannot implement the exact handoff in Release/package mode without first selecting compatible Shell/Contracts.UI versions and adding the missing central/package-vs-source mechanics. Two implementers could choose source HEAD, Shell 3.1.1 plus Contracts.UI 3.1.1, or current 3.2.2 incompatibly.

**Disposition:** **Discuss, then amend.** Keep AD-21's owner/resource decision. Add a binding rule or explicit prerequisite that pins Shell and Contracts.UI to the same compatible FrontComposer release, adds both package pins, and defines the source/package switch before the UI migration consumes them. Label the diagram as target-state or distinguish current from planned edges. Do not call the current boundary implemented.

## Medium Findings

### M1 — `Hexalith.Commons.UniqueIds 2.26.0` does not match evaluated central package reality

**Evidence:** The imported `references/Hexalith.Builds/Props/Directory.Packages.props` sets `HexalithCommonsVersion` to `2.28.1`, which is the package version used when source mode is off. Root `Directory.Packages.props` has only a compatibility fallback of `2.28.0` if the imported property is empty. The official NuGet registry also lists `2.28.1`; nothing in the current evaluation selects `2.26.0`.

**Impact:** The Stack contains a factual local-reality error.

**Disposition:** **Autofix** to `2.28.1` (or state source commit identity if the row is meant to describe source mode).

### M2 — AD-19 freezes 32 as though it were the configured ceiling, while code makes 32 only the default

**Evidence:** AD-19 says the response contains at most “the configured maximum of 32 outcomes.” The implementation does enforce the exact v2 envelope, enum ordinals, exact admitted-route reconciliation, ordinal route ordering, async/cancellation behavior, and checkpoint advancement only after durable completion. But `ProjectionDispatchOptions.DefaultMaxOutcomes` is 32 while `MaxOutcomes` is configurable up to 4096; no committed appsettings value fixes it at 32.

**Impact:** The architecture can be read as a hard interoperability/security bound while two deployments can legally choose different larger values. This is a real code/spine divergence at the protocol admission boundary.

**Disposition:** **Discuss.** Either enforce 32 as a hard invariant in code/configuration, or change AD-19 to say “default 32, configured through `ProjectionDispatchOptions.MaxOutcomes`, hard ceiling 4096” and bind cross-service configuration parity. The remainder of AD-19 passes current-reality review.

## Low Findings

### L1 — Two package rows accurately mirror local pins but are no longer upstream-current

- `OpenTelemetry.Instrumentation.Runtime` has stable `1.16.0`; the repo/spine still uses `1.15.1` while the other selected OpenTelemetry packages are already `1.16.0`.
- `NSubstitute 6.0.0` GA is published; the repo/spine still uses `6.0.0-rc.1`.

**Disposition:** **Defer or update together with central props.** These are not architecture-fit failures, but the Stack should not imply that all pins are current. The CommunityToolkit Dapr, Aspire Keycloak/Kubernetes, and Fluent UI V5 entries are visibly prerelease dependencies; their exact pins exist and fit current code, but prerelease acceptance should remain an explicit release risk rather than an unstated assumption.

## Verified Without Finding

- AD-19's named `(Domain, ProjectionType)` routing, `/project/v2`, `Version = 2`, stable status ordinals, ordinal ordering, exact-one-outcome validation, partial sibling durability, legacy adapter, cancellation propagation, and fail-closed checkpoint reconciliation are present in production code and focused/live-sidecar evidence.
- AD-21's decision to evolve the existing Admin UI and retain `eventstore-admin-ui` is compatible with current AppHost, DAPR ACL, container, and UI client boundaries. FrontComposer's current source API can represent the single module navigation/deep-link behavior; the gap is dependency/version integration, not conceptual fit.
- Aspire.Hosting `13.4.6`, Keycloak/Kubernetes `13.4.6-preview.1.26319.6`, Dapr .NET SDK `1.18.4`, MediatR `14.2.0`, FluentValidation `12.1.1`, Roslyn `5.6.0`, Fluent UI V5 `5.0.0-rc.4-26180.1`, main OpenTelemetry packages `1.16.0`, xUnit v3 `3.2.2`, and Shouldly `4.3.0` all exist in their official registries and fit the local references. Dapr `1.19` and xUnit `4` are prerelease lines, so the selected stable majors remain appropriate.

## Gate Recommendation

Do not finalize the updated spine as technology-current until H1 and H2 are reconciled. M1 is a direct safe correction. M2 needs one explicit architectural choice because it changes whether 32 is a protocol invariant or merely a default. The remaining pin refreshes may be deferred with owners and revisit conditions.

## Re-review 2026-07-15

Verdict: **pass with a non-blocking implementation-reality caveat; no critical or high findings remain.**

- **H1 resolved.** AD-11 and Stack now distinguish repository seed (`10.0.302` / `10.0.9`) from the required current security baseline (`10.0.302` / `10.0.10`) and prohibit another implementation or release slice before the coordinated move. This accurately represents both current repository state and Microsoft's 2026-07-14 security release.
- **H2 resolved at architecture altitude.** AD-21 now fixes matching `3.2.2` Shell/Contracts.UI dependencies and one Debug-source/Release-package boundary; both `3.2.2` packages exist in the official registry and current FrontComposer APIs fit the selected shell/navigation model. The repository has not implemented that target yet: the checked-out source is `v3.2.2-8-g0a84e818`, imported central props still pin Shell `3.1.1` and omit Contracts.UI, and `Admin.UI` has no FrontComposer reference. This is now bounded Story 7.14 implementation work rather than an open architectural choice. The Stack/dependency view should be read as target state until that story lands.
- **M1 resolved.** `Hexalith.Commons.UniqueIds 2.28.1` now matches evaluated central props and the official package registry.
- **M2 resolved.** AD-19 now correctly makes `ProjectionDispatchOptions.MaxOutcomes` the validated positive bound and identifies `32` as its default, rather than freezing 32 as the wire ceiling. The local additional validation ceiling (`4096`) remains implementation seed. The new normalized result is explicitly versioned and does not mutate the verified `/project/v2` envelope.
- The earlier low-priority OpenTelemetry runtime and NSubstitute refresh opportunities remain accurately disclosed in this review and do not create a critical/high architecture-currentness defect.
