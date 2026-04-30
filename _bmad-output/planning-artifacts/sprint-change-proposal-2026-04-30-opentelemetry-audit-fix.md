# Sprint Change Proposal - OpenTelemetry NuGet Audit Build Fix

**Date:** 2026-04-30
**Project:** Hexalith.EventStore
**Scope Classification:** Minor - direct development correction
**Trigger:** `aspire run --project src/Hexalith.EventStore.AppHost/Hexalith.EventStore.AppHost.csproj` failed during restore/build because NuGet audit warnings were treated as errors.

---

## 1. Issue Summary

The AppHost could not start after the Epic 11 retrospective because the build failed on NU1902 package vulnerability warnings. The failing packages were OpenTelemetry dependencies pinned centrally at 1.15.1:

- `OpenTelemetry.Api` 1.15.1, reported via transitive dependency
- `OpenTelemetry.Exporter.OpenTelemetryProtocol` 1.15.1

The error affected AppHost-referenced projects including:

- `src/Hexalith.EventStore.Admin.Server.Host`
- `Hexalith.Tenants/src/Hexalith.Tenants`
- `Hexalith.Tenants/src/Hexalith.Tenants.ServiceDefaults`

Because `Directory.Build.props` and `Hexalith.Tenants/Directory.Build.props` set `TreatWarningsAsErrors=true`, the warnings blocked `aspire run`.

---

## 2. Impact Analysis

**Epic impact.** No product epic or acceptance criteria changes are required. This is a dependency maintenance correction needed to restore the development/runtime validation path.

**Story impact.** No story scope changes are required. Epic 11 retrospective action R11-A3 remains valid: capture full AppHost projection proof. This correction unblocks the AppHost build portion of that action.

**Architecture impact.** No architecture change. The OpenTelemetry service-defaults integration remains the same; only package patch versions change.

**PRD impact.** None.

**UX impact.** None.

**Infrastructure and CI impact.** Central package manifests must use non-vulnerable OpenTelemetry patch versions so restore/build can run with NuGet audit and warnings-as-errors enabled.

---

## 3. Recommended Approach

Use **Option 1: Direct Adjustment**.

Update the central package pins for OpenTelemetry core hosting/exporter packages from 1.15.1 to 1.15.3 in both the root repository and `Hexalith.Tenants` package manifests:

- `Directory.Packages.props`
- `Hexalith.Tenants/Directory.Packages.props`

Do not disable NuGet audit and do not loosen `TreatWarningsAsErrors`. Suppressing the audit would restore the command but preserve the vulnerable dependency state.

---

## 4. Detailed Change Proposal

### Package Manifest Changes

**Files:**

- `Directory.Packages.props`
- `Hexalith.Tenants/Directory.Packages.props`

**Old:**

```xml
<PackageVersion Include="OpenTelemetry.Exporter.OpenTelemetryProtocol" Version="1.15.1" />
<PackageVersion Include="OpenTelemetry.Extensions.Hosting" Version="1.15.1" />
```

**New:**

```xml
<PackageVersion Include="OpenTelemetry.Exporter.OpenTelemetryProtocol" Version="1.15.3" />
<PackageVersion Include="OpenTelemetry.Extensions.Hosting" Version="1.15.3" />
```

**Rationale.** NuGet reports 1.15.3 as the current available stable package version for both packages. Updating `OpenTelemetry.Extensions.Hosting` also moves the transitive `OpenTelemetry.Api` dependency away from the vulnerable 1.15.1 resolution.

---

## 5. Validation Results

1. `aspire run --project src/Hexalith.EventStore.AppHost/Hexalith.EventStore.AppHost.csproj` initially failed on NU1902.
2. Package pins were updated to 1.15.3 in both central package manifests.
3. `dotnet clean src/Hexalith.EventStore.AppHost/Hexalith.EventStore.AppHost.csproj -v minimal` was run after one stale reference-assembly failure.
4. `aspire run --project src/Hexalith.EventStore.AppHost/Hexalith.EventStore.AppHost.csproj` then built and started the AppHost.
5. Aspire MCP confirmed a running AppHost for `D:\Hexalith.EventStore`.
6. Aspire MCP `list_resources` showed the main resources running and healthy, including `eventstore`, `sample`, `sample-blazor-ui`, `tenants`, `statestore`, `pubsub`, and DAPR sidecars.

**Additional runtime correction.** The follow-up Aspire run exposed tenant bootstrap replay failures after the OpenTelemetry build blocker was removed. The correction removed concurrent actor-state reads during event replay and added no-op replay handlers for persisted tenant rejection events. A fresh Aspire run showed the expected idempotent bootstrap behavior: EventStore invoked `tenants/process` successfully, persisted/published `GlobalAdminAlreadyBootstrappedRejection`, returned 409 to the bootstrap caller, and the tenants service logged that the global administrator is already registered.

---

## 6. Handoff

**Development team:** Keep the package pin update.

**QA / Dev:** Continue Epic 11 R11-A3 with full projection proof now that AppHost builds, starts, and the tenant bootstrap replay path is fixed.

**Scrum Master:** Treat Epic 11 R11-A9 as closed by this correction; keep R11-A1 through R11-A4 as the remaining high-priority projection-builder confidence items.

---

## Checklist Summary

| Checklist Item | Status | Notes |
|---|---|---|
| 1.1 Triggering story identified | Done | Epic 11 retrospective follow-up / AppHost validation |
| 1.2 Core problem defined | Done | NU1902 audit warnings blocked AppHost build |
| 1.3 Evidence gathered | Done | Aspire run output captured vulnerable package warnings |
| 2.x Epic impact assessed | Done | No epic scope change |
| 3.x Artifact impact assessed | Done | Package manifests only |
| 4.1 Direct adjustment evaluated | Viable | Low-risk patch version update |
| 4.2 Rollback evaluated | Not viable | No completed story rollback needed |
| 4.3 MVP review evaluated | Not applicable | No MVP scope impact |
| 5.x Proposal components complete | Done | This document |
| 6.x Final handoff | Done | Direct implementation completed |
