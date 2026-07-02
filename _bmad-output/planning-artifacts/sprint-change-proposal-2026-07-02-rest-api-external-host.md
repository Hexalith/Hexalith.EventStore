# Sprint Change Proposal — REST API is External; UI Uses Client Libraries

Date: 2026-07-02
Author: Administrator (via correct-course)
Status: Approved
Supersedes (in part): `sprint-change-proposal-2026-06-21.md` Epic D design decision "generate controllers into the domain UI host"
Trigger: D.5 code review (`D-5-proof-sample-blazorui-queries`) surfaced an AC2 contract-duplication defect rooted in the "generate into the UI host" premise.

---

## 1. Issue Summary

Epic D (REST Controller Source Generator) was designed to **generate typed REST controllers into the domain UI host**, each delegating to `IEventStoreGatewayClient` (2026-06-21 §"Why this over alternatives": *"Not gateway-hosted controllers: generating into the domain UI host … the generated controllers are thin typed BFF facades"*).

Implementing the first adoption proof (D5, Sample BlazorUI) exposed that this premise does not hold cleanly:

- To feed the **syntax-based** generator inside the UI-host compilation, D5 **compile-linked** `GetCounterStatusQuery.cs` into `Sample.BlazorUI.dll`, creating a **second compiled identity** of the contract type — which AC2 and Guardrail #2 explicitly prohibit ("Do not copy `GetCounterStatusQuery` into the Blazor UI project as a second type").
- The clean alternative (a `ProjectReference` to the domain project so the generator discovers the contract from a referenced assembly) **fails to build** with `CS0436` — the UI host's implicit `Program` collides with the Sample host's implicit `Program` (both are `Sdk.Web` top-level-statement apps).
- In practice the Blazor **components never call the generated controller** — they call the gateway Client directly. The generated controller was exercised only by an external `curl` smoke, revealing that the natural consumer of a typed REST API is an **external application**, not the interactive UI that emitted it.

**Owner directive (change trigger):**
> The UI should use **Client libraries** that call the commands and query endpoints. The **REST API is for external applications.**

## 2. Impact Analysis

**Epic Impact — Epic D (REST Controller Source Generator):** core design decision inverted. The generator platform (D1–D4) is **unaffected and retained**; the *hosting target* and the *proof stories* (D5–D8) change.

**Story Impact:**
- **D5** (`in-progress`) — reframed from "UI hosts + adopts the generated controller" to "UI consumes the Client library; generated controller proven in an external API host." Resolves the two open review findings.
- **D6** (`ready-for-dev`) — reframed: UI commands via `IEventStoreGatewayClient.SubmitCommandAsync`; generated command controller lives in the external API host.
- **D7** (backlog) — Tenants.UI → Client libraries; generated Tenants controllers → external Tenants API host (submodule PR).
- **D8** (backlog) — docs/governance text corrected; guardrail inverted.
- **New** — a `Sample.Contracts` library + `Sample.Api` external host (prerequisite scaffold), and a proof story for the external host.

**Artifact Conflicts:** `sprint-change-proposal-2026-06-21.md` (Epic D goal + design decision + §4.3 doc edits), `CLAUDE.md` (Domain-Module Authoring rules — the planned D8 edits), `docs/brownfield/architecture.md` + `integration-architecture.md`.

**Technical Impact:** new projects (`Sample.Contracts`, `Sample.Api`) added to `.slnx` + AppHost; contract relocation (namespace preserved → no consumer churn); removal of the self-hosted controller + inbound JWT surface from `Sample.BlazorUI`; retirement of the hand-rolled `"EventStoreApi"` HttpClient once commands move to the Client library.

## 3. Recommended Approach

**Selected path: Direct Adjustment** — reframe Epic D's hosting target and proof stories; the generator platform stays. No rollback of D1–D4. New additive scaffold (`Sample.Contracts` + `Sample.Api`) chosen over "generator = platform-only" so the generator retains an in-repo, end-to-end external adoption proof (owner decision).

- **Effort:** Medium. D5 code correction is small; the scaffold + D6/D7/D8 reframes are the bulk.
- **Risk:** Low–Medium. Additive, non-breaking; the risky `ProjectReference`-to-a-Web-host pattern is eliminated by the contracts library.
- **Timeline:** absorbs into Epic D; D5 unblocks immediately.

## 4. Detailed Change Proposals

### CP-1 — Corrected architecture principle (supersedes 2026-06-21 §"Why this over alternatives")
> **Not UI-host-hosted controllers:** the generator emits typed controllers into a **dedicated external-facing API host** (per domain), each delegating to `IEventStoreGatewayClient`. The **interactive domain UI host consumes the platform Client libraries directly** (`IEventStoreGatewayClient` for commands and queries) and hosts **no** generated controllers. REST-annotated contracts live in a **contracts-only library** referenced by the domain service, the external-API host, and (for metadata) the UI host. This keeps the gateway domain-agnostic, keeps interactive UIs thin, and makes the typed REST API a deliberate, versioned **external** integration surface.

### CP-2 — New contracts-only library `Hexalith.EventStore.Sample.Contracts`
- Plain `Microsoft.NET.Sdk` class library (`IsPackable=false`), references only `Hexalith.EventStore.Contracts`.
- Move `GetCounterStatusQuery.cs` here (namespace `Hexalith.EventStore.Sample.Counter.Queries` **unchanged**).
- Referenced by `Sample` (domain), `Sample.BlazorUI` (metadata — replaces the `<Compile Link>`), `Sample.Api` (generator discovery). Add to `.slnx`. Fix the stale XML doc.

### CP-3 — New external-facing API host `Hexalith.EventStore.Sample.Api`
- `Sdk.Web`, `EnableContainer=true`, `ContainerRepository=sample-api`. References `Sample.Contracts`, `Client`, the generator analyzer (`OutputItemType=Analyzer` / `ReferenceOutputAssembly=false`), `ServiceDefaults`.
- Owns `RestApiAssemblyInfo.cs` (`[assembly: RestApi("api/{tenant}/counter", "counter", RestTenantSource.Route)]`), `AddControllers()`/`MapControllers()`, inbound JWT, and the `IEventStoreGatewayClient` registration via the DAPR app-id + bearer handlers. Two-line-style `Program.cs`; no Razor/SignalR/FluentUI.
- New AppHost resource `sample-api`; add to `.slnx`. AC9-style smoke (`GET /api/tenant-a/counter/counter-1` → 200 + ETag + 304) runs here.

### CP-4 — D5 rescope (`D-5-proof-sample-blazorui-queries`)
- **BlazorUI removes:** generator analyzer ref, `RestApiAssemblyInfo.cs`, the `<Compile Link>`, `AddControllers()`/`MapControllers()`, inbound JWT (`AddAuthentication/JwtBearer` + `UseAuthentication/UseAuthorization`).
- **BlazorUI adds:** `ProjectReference` to `Sample.Contracts`.
- **Keeps:** `EventStoreProjectionQueryClient` → `IEventStoreGatewayClient`, `CounterStatusResult`, and the two applied review patches (refresh-error visibility; 404 history-cap trim).
- ACs about analyzer ref / `[RestApi]` opt-in / `AddControllers`/`MapControllers` / inbound JWT / generated-source inspection **move to the `Sample.Api` proof story**. D5 ACs become: queries via the Client library; no controller/opt-in in the UI; count-0/ETag/304/support-safe preserved; build clean.
- **Resolves** D5 review Finding 1 (duplication — deleted by construction) and Finding 2 (endpoint unused by UI — now the intended design).

### CP-5 — D6 rescope (`D-6-proof-counter-commands`)
- **UI:** `CounterCommandForm` calls `IEventStoreGatewayClient.SubmitCommandAsync` (Client library) instead of raw `HttpClient` POST to `/api/v1/commands`; `messageId` becomes a ULID via `UniqueIdHelper.GenerateSortableUniqueStringId()` (fixes the ULID-not-GUID defect). Retire the named `"EventStoreApi"` HttpClient.
- **External host:** annotate Counter command(s) with `ICommandContract` + `[RestRoute]` in `Sample.Contracts`; generator emits the POST command controller into `Sample.Api` (202 + Location). Depends on CP-2/CP-3.

### CP-6 — D7 rescope (Tenants, submodule)
- Tenants contracts (commands + 6 queries, rich routes incl. `~/api/users/{userId}/tenants`) → contracts library.
- Generated controllers → external **Tenants API host**, not `Hexalith.Tenants.UI`. `Hexalith.Tenants.UI` consumes Client libraries.
- Hand-written `TenantsQueryController` deleted; its external REST role **replaced** by the generated controllers in the external Tenants API host (surface preserved). Separate PR in `Hexalith.Tenants`; submodule tests stay green.

### CP-7 — D8 docs/governance + Epic D table & sequencing
- Correct every "into the UI host" statement in `CLAUDE.md` and `docs/brownfield/*` to "into a dedicated external-facing API host; the interactive UI host consumes Client libraries and hosts no controllers."
- Published-package count math unchanged (`Sample.Contracts`/`Sample.Api` are `IsPackable=false`; analyzer 8→9 stands). Extend `DomainModuleAuthoringGuardrailTests` to assert **interactive UI hosts expose no MVC command/query controllers**.
- Epic D story table rewrite + sequencing: D1→D2→D3→D4 → **(Sample.Contracts + Sample.Api scaffold)** → D5 (UI queries via Client) / D5b (Sample.Api generated query controller + smoke) / D6 → D7 → D8.

## 5. Implementation Handoff

**Scope classification: Major** (inverts a core Epic D design decision; adds projects; cross-repo submodule; multiple stories) — but with an immediately implementable **Minor/Moderate slice** for the active story.

**Routing:**
- **PM / Architect** — fold CP-1/CP-7 into the planning artifacts (Epic D table, sequencing, brownfield docs); confirm the `Sample.Api` published/container posture.
- **Developer** — implement in order:
  1. **CP-2 + CP-3** scaffold (`Sample.Contracts`, `Sample.Api`) — prerequisite.
  2. **CP-4** (D5) — unblocks the active story; re-verify Release build + Sample tests + `Sample.Api` AC9 smoke.
  3. **CP-5** (D6), then **CP-6** (D7 submodule PR), then **CP-7 governance** (D8).

**Success criteria:** interactive UI hosts contain no generated/hand-written command/query controllers and consume Client libraries; generated controllers live in `Sample.Api` (and the external Tenants API host) and pass the AC9 smoke; contract types have a single compiled identity; guardrail tests enforce the inverted rule; Release build clean.

---

_Generated via the correct-course workflow (Incremental mode). All 7 change proposals approved by Administrator on 2026-07-02._
