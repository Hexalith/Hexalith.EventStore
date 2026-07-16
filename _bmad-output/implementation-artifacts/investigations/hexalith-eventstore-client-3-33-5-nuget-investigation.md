# Investigation: Hexalith.EventStore.Client 3.33.5 NuGet Availability

## Hand-off Brief

1. **What happened.** User reported that `Hexalith.EventStore.Client` version `3.33.5` was missing from NuGet; direct NuGet APIs and a clean `dotnet add package` restore now show it is available.
2. **Where the case stands.** The missing-package premise is refuted for nuget.org as of 2026-07-04 19:44 UTC; the most likely explanation is NuGet propagation/search lag, a local package-source configuration issue, or a cache/proxy issue outside this repository.
3. **What's needed next.** If a consumer still cannot restore it, collect the exact restore command, `nuget.config`, and NuGet error output from that environment.

## Case Info

| Field | Value |
| ----- | ----- |
| Ticket | N/A |
| Date opened | 2026-07-04 |
| Status | Concluded |
| System | Local repo `/home/administrator/projects/hexalith/eventstore`, .NET SDK 10.0.301 |
| Evidence sources | NuGet flat-container API, NuGet search API, temporary `dotnet add package`, release manifest, release config, package project |

## Problem Statement

User-reported description: "identified that Hexalith.EventStore.Client 3.33.5 is missing in nuget".

## Evidence Inventory

| Source | Status | Notes |
| ------ | ------ | ----- |
| NuGet flat-container index | Available | Listed `3.33.5` for `hexalith.eventstore.client`. |
| NuGet package blob HEAD | Available | Returned HTTP 200 with `content-length: 101520` and `last-modified: Sat, 04 Jul 2026 19:31:39 GMT`. |
| NuGet search API | Available | Returned `Hexalith.EventStore.Client` with latest `version: 3.33.5`. |
| Clean restore test | Available | `dotnet add package Hexalith.EventStore.Client --version 3.33.5 --source https://api.nuget.org/v3/index.json` restored successfully in a temp class library. |
| Release manifest | Available | `tools/release-packages.json:8` includes `Hexalith.EventStore.Client`; `tools/release-packages.json:9` points to the Client project. |
| Release command | Available | `.releaserc.json:11` validates packages before release; `.releaserc.json:12` pushes `Hexalith.EventStore.*.nupkg` to nuget.org. |

## Investigation Backlog

| # | Path to Explore | Priority | Status | Notes |
| - | --------------- | -------- | ------ | ----- |
| 1 | Verify public package availability | High | Done | NuGet API and restore both confirmed availability. |
| 2 | Check local release inventory | Medium | Done | Client is included in release manifest and publish glob. |
| 3 | Investigate consumer restore environment | Medium | Blocked | Requires consumer `nuget.config`, restore command, and exact error. |

## Timeline of Events

| Time | Event | Source | Confidence |
| ---- | ----- | ------ | ---------- |
| 2026-07-04 19:31:39 UTC | NuGet CDN reports the `3.33.5` `.nupkg` blob was last modified. | `curl -I` against flat-container package URL | Confirmed |
| 2026-07-04 19:44 UTC | Flat-container package blob returned HTTP 200. | `curl -fsSI` against flat-container package URL | Confirmed |
| 2026-07-04 current session | Clean temp restore added `Hexalith.EventStore.Client` `3.33.5` successfully. | `dotnet add package` temp-project command | Confirmed |

## Confirmed Findings

### Finding 1: The release manifest includes the Client package

**Evidence:** `tools/release-packages.json:8`, `tools/release-packages.json:9`

**Detail:** The manifest entry includes package id `Hexalith.EventStore.Client` and points to `src/Hexalith.EventStore.Client/Hexalith.EventStore.Client.csproj`.

### Finding 2: The release pipeline validates and pushes EventStore packages

**Evidence:** `.releaserc.json:11`, `.releaserc.json:12`

**Detail:** Semantic-release packs and validates manifest packages, then pushes `./nupkgs/Hexalith.EventStore.*.nupkg` to `https://api.nuget.org/v3/index.json`.

### Finding 3: The Client project is packable by default and has package metadata

**Evidence:** `src/Hexalith.EventStore.Client/Hexalith.EventStore.Client.csproj:1`, `src/Hexalith.EventStore.Client/Hexalith.EventStore.Client.csproj:4`

**Detail:** The project uses `Microsoft.NET.Sdk`; the repository package defaults make SDK projects packable unless overridden, and the project supplies the package description.

### Finding 4: NuGet currently serves the 3.33.5 Client package

**Evidence:** `curl -fsSL https://api.nuget.org/v3-flatcontainer/hexalith.eventstore.client/index.json`; `curl -fsSI https://api.nuget.org/v3-flatcontainer/hexalith.eventstore.client/3.33.5/hexalith.eventstore.client.3.33.5.nupkg`; temp `dotnet add package` command output.

**Detail:** The index lists `3.33.5`, the `.nupkg` blob returns HTTP 200, and a clean .NET project can restore the package from nuget.org.

## Deduced Conclusions

### Deduction 1: The package is not missing from nuget.org now

**Based on:** Finding 4.

**Reasoning:** A package version that is listed by the flat-container index, returned by NuGet search, downloadable as a `.nupkg`, and restorable by `dotnet add package` from nuget.org is available to standard NuGet clients.

**Conclusion:** The reported missing state is no longer true for nuget.org as of this investigation.

### Deduction 2: No repository packaging fix is indicated

**Based on:** Findings 1, 2, 3, and 4.

**Reasoning:** The Client package is in the release manifest, the release config pushes it, NuGet serves it, and restore works. A repository change would not address a now-available package or a consumer-side source/cache issue.

**Conclusion:** Do not patch release code for this report without fresh failing evidence from a restore environment.

## Hypothesized Paths

### Hypothesis 1: NuGet propagation/search lag caused the user-visible gap

**Status:** Open

**Theory:** The package was recently published and initially visible through some NuGet surfaces later than others.

**Supporting indicators:** NuGet CDN reports `last-modified: Sat, 04 Jul 2026 19:31:39 GMT`, shortly before this investigation.

**Would confirm:** A timestamped earlier query showing `3.33.5` absent from search or the UI while the push had already completed.

**Would refute:** A consumer failure after the API and restore checks above, using nuget.org directly with no proxy/cache.

**Resolution:** Not enough historical NuGet-side data in this session to prove the earlier transient state.

### Hypothesis 2: A consumer environment is using a stale cache or non-nuget.org source

**Status:** Open

**Theory:** The failing consumer may be resolving through a private feed, proxy, lock file, package cache, or source mapping that does not yet contain `3.33.5`.

**Supporting indicators:** Current direct nuget.org restore succeeds.

**Would confirm:** Consumer `nuget.config`, restore logs, or source mapping showing the package does not resolve from nuget.org.

**Would refute:** A failing restore using only `--source https://api.nuget.org/v3/index.json` on a clean machine/cache.

**Resolution:** Requires external consumer evidence.

## Missing Evidence

| Gap | Impact | How to Obtain |
| --- | ------ | ------------- |
| Exact consumer restore error | Determines whether failure is cache, source, framework, lock file, or package absence. | Capture `dotnet restore -v minimal` or failing CI log. |
| Consumer `nuget.config` and package source mapping | Determines whether nuget.org is actually used. | Inspect repo/user/machine `nuget.config` files. |
| Timestamp of the initial missing observation | Determines whether this was transient NuGet propagation lag. | Compare with NuGet CDN `last-modified` timestamp. |

## Source Code Trace

| Element | Detail |
| ------- | ------ |
| Error origin | No repository error found. |
| Trigger | User observed package/version missing in NuGet. |
| Condition | Current nuget.org APIs and restore path serve the package. |
| Related files | `tools/release-packages.json`, `.releaserc.json`, `src/Hexalith.EventStore.Client/Hexalith.EventStore.Client.csproj` |

## Conclusion

**Confidence:** High

`Hexalith.EventStore.Client` `3.33.5` is available on nuget.org now. The repository release inventory includes the Client package, the release config pushes matching packages, the NuGet flat-container API lists/downloads the version, the NuGet search API returns it as latest, and a clean `dotnet add package` restore succeeds.

## Recommended Next Steps

### Fix direction

No repository code fix is indicated. For any still-failing consumer, force nuget.org directly and bypass local source ambiguity:

```bash
dotnet nuget locals all --clear
dotnet restore --source https://api.nuget.org/v3/index.json
```

### Diagnostic

If restore still fails, collect the failing command, full error output, and all active `nuget.config` files so source mapping and private-feed caches can be checked.

## Reproduction Plan

1. Create a temporary .NET 10 class library.
2. Run `dotnet add package Hexalith.EventStore.Client --version 3.33.5 --source https://api.nuget.org/v3/index.json`.
3. Expected result: package reference is added and restore succeeds.

## Side Findings

- The generated package nuspec references repository commit `aaac942b430d316c678bb2425232efa43b48ec9e`, while the local tag `v3.33.5` points at `b779298a4aaf1399018d3293ad8bc384f471f388`. This does not affect package availability, but it is worth checking separately if exact source-link provenance matters.
