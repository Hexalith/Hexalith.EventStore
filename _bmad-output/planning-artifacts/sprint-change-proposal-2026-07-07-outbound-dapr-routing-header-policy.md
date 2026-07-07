# Sprint Change Proposal — Outbound DAPR Routing-Header Replacement Policy

- **Date:** 2026-07-07
- **Author:** Amelia (Developer)
- **Trigger:** Epic 2 retrospective open action item — *"Decide and enforce the outbound DAPR routing-header replacement policy for generated API host clients."*
- **Workflow:** `bmad-correct-course` (Incremental mode)
- **Change scope classification:** **Moderate** (backlog reorganization: new Story 2.7, Epic 2 reopened, new architecture invariant AD-18) → handoff to Product Owner / Developer.
- **Decisions taken (with user):** Blast radius = *centralize in the platform + guardrail*; story home = *new Story 2.7, scheduled now*.

---

## Section 1 — Issue Summary

### Problem statement

Outbound DAPR service invocation from EventStore host clients routes through a `DaprAppIdHandler`
`DelegatingHandler` that sets the sidecar **control-plane** headers `dapr-app-id` and
`dapr-api-token` with `HttpRequestHeaders.TryAddWithoutValidation(...)`. That call **appends** a
value; it does not **replace** an existing one. If the outgoing `HttpRequestMessage` already carries
a `dapr-app-id` or `dapr-api-token` — because a caller supplied it, or an upstream/inbound
header-forwarding handler copied it onto the request — the sidecar receives a **duplicate** header.

DAPR's resolution of a duplicated `dapr-app-id` is undefined; depending on which value the sidecar
reads, the service invocation can be routed to a **caller-chosen app id** (a trust-boundary /
routing-hijack risk), and a duplicated or stale `dapr-api-token` can leak or mis-send the sidecar
token. These are sidecar control-plane headers, not application headers: they must be set
authoritatively by the client transport, deterministically, from configuration, and must never be
influenced by untrusted caller input.

### Discovery context

The defect was surfaced during the **Story 2.3** (Sample External API Host Proof) code review and
recorded in the deferred-work ledger under `spec-2-3-sample-external-api-host-proof.md`. It was
carried into the **Epic 2 retrospective** as an explicit open action item owned by the Developer
role. The behavior **pre-dates** the Story 2.3 proof — it is a platform-pattern defect, not a
regression introduced by that story.

### Evidence

- **Four byte-identical copies** of the defective handler exist (the ledger named only the first two):
  - `samples/Hexalith.EventStore.Sample.Api/Services/DaprAppIdHandler.cs` *(named in ledger)*
  - `samples/Hexalith.EventStore.Sample.BlazorUI/Services/DaprAppIdHandler.cs` *(named in ledger)*
  - `src/Hexalith.EventStore.Admin.UI/Services/DaprAppIdHandler.cs` *(same defect, not named)*
  - `references/Hexalith.Tenants/src/Hexalith.Tenants.Api/Services/DaprAppIdHandler.cs` *(submodule copy)*
- Each uses:
  ```csharp
  _ = request.Headers.TryAddWithoutValidation("dapr-app-id", appId);
  if (!string.IsNullOrEmpty(apiToken))
      _ = request.Headers.TryAddWithoutValidation("dapr-api-token", apiToken);
  ```
- Registration (Sample.Api): `AddEventStoreGatewayClient(...).AddHttpMessageHandler<InboundBearerForwardingHandler>().AddHttpMessageHandler(() => new DaprAppIdHandler("eventstore", token))`.
- **Test gap:** `SampleApiGatewayHandlerTests` asserts only the single-value happy path
  (`GetValues("dapr-app-id").ShouldBe(["eventstore"])`). **No test seeds a pre-existing header** to
  prove replacement, so nothing prevents the append behavior from regressing.

### The decided policy

The outbound DAPR service-invocation handler **owns** both control-plane headers:

1. **Replace, never append** — remove any pre-existing `dapr-app-id`, then set the configured app id
   as the single value.
2. **Token stripping** — remove any pre-existing `dapr-api-token` unconditionally; set the configured
   token only when one is present (else the request carries none).
3. **Innermost handler** — the DAPR routing handler runs last in the gateway-client chain (after any
   inbound bearer/header-forwarding handler) so it has the final say over control-plane headers.
4. **Caller values are never routed** — no caller- or inbound-forwarded `dapr-app-id`/`dapr-api-token`
   can influence sidecar routing or the sidecar token.

Canonical implementation shape:
```csharp
request.Headers.Remove("dapr-app-id");
request.Headers.TryAddWithoutValidation("dapr-app-id", appId);
request.Headers.Remove("dapr-api-token");
if (apiToken is { Length: > 0 })
    request.Headers.TryAddWithoutValidation("dapr-api-token", apiToken);
```

---

## Section 2 — Impact Analysis

### Epic impact
- **Epic 2 (External Integration Surfaces)** — status `done`, but shipped a defective, duplicated
  outbound-DAPR pattern. Reopens to `in-progress` to absorb a new hardening **Story 2.7**.
- **Epic 3–7** — no epic invalidated or resequenced. **Epic 5 (Security & Tenant Isolation)** is
  *reinforced* by AD-18 (it complements Story 5.5, Internal and Domain-Service Trust Boundary), not
  blocked.

### Story impact
- **New Story 2.7 — Outbound DAPR Routing-Header Ownership** (full spec in Section 4).
- **Stories 2.3 / 2.4 (done)** — ACs are **not** rewritten. Story 2.7 supersedes only their
  host-local handler *implementation detail* (the per-host `DaprAppIdHandler` copies), which those
  stories legitimately delivered at the time. No active-story AC drift (see Story Rewrite Gate below).

### Artifact conflicts
| Artifact | Change |
| --- | --- |
| `architecture.md` | New invariant **AD-18** (Outbound Sidecar Control-Plane Headers Are Handler-Owned); Consistency-Conventions row; Capability→Architecture map (FR11–FR16, FR26/FR28/FR32 rows). |
| `epics.md` | Insert **Story 2.7** under Epic 2. |
| `sprint-status.yaml` | Add `2-6-…`; `epic-2: done → in-progress`; action item `open → in-progress` with decision note. |
| `deferred-work.md` | Reconcile the spec-2-3 `DaprAppIdHandler` entry → owned by AD-18 + Story 2.7; Tenants copy = coordinated follow-up. |
| `project-context.md` | Add one Framework rule pinning the replace-not-append policy for future agents. |
| PRD, UX | No change (MVP unaffected; no UI/UX surface). |

### Technical impact (implementation, executed by Story 2.7)
- **Platform home:** canonical `DaprServiceInvocationHandler` in **`Hexalith.EventStore.Client`**
  (`Handlers/` namespace), wired through `AddEventStoreGatewayClient(appId, apiToken?)`.
  > **Correction carried from the decision dialog:** the `Hexalith.EventStore.Gateway` package is the
  > *inbound* server-host composition (it `Compile`-includes the whole `Hexalith.EventStore` gateway
  > API host). The outbound service-invocation handler is a *client transport* concern, so its correct
  > platform home is the **Client** package — which already owns both `AddEventStoreGatewayClient` and
  > the `Handlers/` namespace and is the package Sample.Api / Sample.BlazorUI / Admin.UI reference.
  > Decision (centralize in the platform) is unchanged; only the exact package is made precise.
- **Delete** the three in-repo `DaprAppIdHandler` copies; hosts wire only the platform extension.
- **Guardrail test** fails if any host declares a local DAPR routing-header handler or uses
  `TryAddWithoutValidation` for `dapr-app-id` / `dapr-api-token`.
- **Replacement unit test** seeds pre-existing headers and asserts single authoritative value.
- **Tenants submodule** (`references/Hexalith.Tenants`) copy is a **coordinated follow-up requiring
  maintainer approval** — not modified under this repo's story (submodule-edit rule).

---

## Section 3 — Recommended Approach

**Selected path: Option 1 — Direct Adjustment (Hybrid).** Add Story 2.7 + AD-18, centralize the
handler in the Client package, delete the three copies, add the guardrail + replacement tests, and
coordinate the Tenants submodule copy separately.

- **Effort:** Low–Medium. **Risk:** Low.
- **Rejected alternatives:**
  - *Option 2 — Rollback:* not viable. Nothing to revert; the defect pre-dates Story 2.3 and reverting
    shipped stories does not simplify the fix.
  - *Option 3 — MVP review:* not viable. MVP scope is unaffected; this is correctness/security
    hardening within the existing plan.
  - *Blast radius "fix-in-place" / "minimal":* rejected during the decision dialog — they retain 3–4
    duplicated handlers, contradict the domain-centric no-boilerplate-in-hosts rule (AD-2), and leave
    the identical defect live in Admin.UI (and Tenants) for "minimal".

---

## Section 4 — Detailed Change Proposals

### 4.1 Architecture — new invariant AD-18 (`architecture.md`, after AD-17)

```
### AD-18 - Outbound Sidecar Control-Plane Headers Are Handler-Owned [ADOPTED]

- Binds: FR13, FR14, FR26, FR28, NFR-security (trust boundary)
- Prevents: a caller- or inbound-forwarded dapr-app-id / dapr-api-token duplicating or
  hijacking DAPR sidecar service-invocation routing, or leaking / mis-sending the sidecar token.
- Rule: Outbound DAPR service invocation from any host client sets the sidecar control-plane
  headers dapr-app-id and dapr-api-token authoritatively through a single platform-owned
  DelegatingHandler in Hexalith.EventStore.Client, wired by AddEventStoreGatewayClient.
  1. Replace, never append: the handler removes any pre-existing dapr-app-id, then sets the
     configured app id as the single value.
  2. The handler removes any pre-existing dapr-api-token and sets the configured token only when
     one is present; when no token is configured it strips any pre-existing dapr-api-token.
  3. The handler is the innermost (last-run) handler in the gateway-client chain, so it has the
     final say after any inbound bearer / header-forwarding handler.
  4. Caller- or inbound-forwarded values never influence sidecar routing or the sidecar token.
  5. Hosts must not define their own DAPR routing-header handler (AD-2). A structural guardrail
     test fails if a host declares a local DAPR routing-header handler or uses
     TryAddWithoutValidation for dapr-app-id / dapr-api-token.

AD-18 extends AD-3 (gateway is the command/query policy boundary) and AD-10 (security fails
closed), applied to the client-to-sidecar transport boundary, and is enforced through the
platform per AD-2 (domain modules stay domain-centric; no per-host transport boilerplate).
```

**Consistency Conventions** — new row:

| Concern | Convention |
| --- | --- |
| Sidecar control-plane headers | Outbound `dapr-app-id` / `dapr-api-token` are handler-owned, **replaced not appended**, set authoritatively from config by the single platform handler; caller/inbound-forwarded values are never routed (AD-18). |

**Capability → Architecture map** — append `AD-18` to the `FR11-FR16 External integration surfaces`
row and the `FR26, FR28, FR32 Security and tenant isolation` row.

### 4.2 Epics — new Story 2.7 (`epics.md`, under Epic 2, before Epic 3)

```
### Story 2.7: Outbound DAPR Routing-Header Ownership

Requirements covered: FR13, FR14 (hardening); security posture FR26/FR28. Trigger: Epic 2 retro
open action; defect from Story 2.3 review (deferred-work, spec-2-3). Governed by AD-18.

As a platform maintainer,
I want outbound DAPR service-invocation clients to own and replace the sidecar routing headers,
So that a caller- or inbound-supplied dapr-app-id / dapr-api-token can never duplicate or hijack
sidecar routing or leak a token.

Acceptance Criteria:

Given a platform-owned outbound DAPR service-invocation handler in Hexalith.EventStore.Client,
When it processes an outbound gateway request,
Then it removes any pre-existing dapr-app-id and sets the configured app id as the single value,
removes any pre-existing dapr-api-token and sets the configured token only when present (else
leaves none), and runs as the innermost handler in the gateway-client chain.

Given the handler is wired through AddEventStoreGatewayClient(appId, apiToken?),
When Sample.Api, Sample.BlazorUI, and Admin.UI build,
Then their three local DaprAppIdHandler copies are deleted and each host wires only the platform
extension.

Given a request already carries a conflicting dapr-app-id / dapr-api-token,
When the outbound handler runs,
Then the sidecar receives exactly one authoritative value and the injected value is discarded —
proven by a unit test that seeds pre-existing headers (single-value assertion), not only the
happy path.

Given the guardrail runs,
When a host declares a local DAPR routing-header handler or uses TryAddWithoutValidation for
dapr-app-id / dapr-api-token,
Then a structural test fails with a support-safe message.

Given the Tenants submodule carries an identical DaprAppIdHandler,
When this story completes,
Then the equivalent submodule change is recorded as a coordinated follow-up requiring maintainer
approval (Story 2.4 lineage) and is not silently modified here.

Given Release build and focused tests run,
Then all configured tests pass, including the new replacement and guardrail tests.
```

### 4.3 `sprint-status.yaml`
- Under `epic-2`: add `2-7-outbound-dapr-routing-header-ownership: backlog`.
- `epic-2: done` → `epic-2: in-progress`.
- Action item *"Decide and enforce the outbound DAPR routing-header replacement policy…"*:
  `status: open` → `status: in-progress`, note referencing this proposal (policy decided; AD-18) and
  Story 2.7 (enforcement).

### 4.4 `deferred-work.md`
- Annotate the spec-2-3 `DaprAppIdHandler` / `TryAddWithoutValidation` entry: reconciled 2026-07-07,
  owned by AD-18 + Story 2.7; Tenants submodule copy = coordinated maintainer-approval follow-up.

### 4.5 `project-context.md`
- Add one Framework-Specific rule: outbound DAPR routing headers are handler-owned and replaced,
  never appended; use the platform handler via `AddEventStoreGatewayClient`; never hand-roll a
  per-host `DaprAppIdHandler` (AD-18).

### Story Rewrite Gate (correct-course enforcement)

No **active** story ACs are superseded. Stories 2.3 and 2.4 are `done`; Story 2.7 is additive and
supersedes only their host-local handler *implementation* (documented in Section 2). A full new-story
rewrite for 2.6 is provided (4.2). The gate is satisfied — no silent AC drift, and the superseded
implementation detail is stated explicitly rather than left implicit.

---

## Section 5 — Implementation Handoff

- **Scope classification:** **Moderate** (backlog reorganization: new story, epic reopened, new
  invariant).
- **Primary handoff — Developer (Amelia):** implement Story 2.7 — canonical
  `DaprServiceInvocationHandler` in `Hexalith.EventStore.Client`, `AddEventStoreGatewayClient(appId,
  apiToken?)` wiring, delete the three in-repo copies, add replacement + guardrail tests, run focused
  Tier-1 + Release build.
- **Product Owner:** confirm Epic 2 reopen and Story 2.7 sequencing in the sprint plan.
- **Architect (Winston):** AD-18 authored here; confirm on next architecture review pass.
- **Coordinated follow-up (maintainer approval):** apply the equivalent replace-semantics fix to
  `references/Hexalith.Tenants/src/Hexalith.Tenants.Api/Services/DaprAppIdHandler.cs` in the Tenants
  submodule (Story 2.4 lineage) — tracked, not silently changed.

### Success criteria
1. A single platform-owned outbound DAPR handler exists; the three in-repo `DaprAppIdHandler` copies
   are gone; hosts wire only the platform extension.
2. A unit test seeds a pre-existing `dapr-app-id`/`dapr-api-token` and asserts a single authoritative
   value survives (append behavior cannot regress).
3. The guardrail test fails on any host-local DAPR routing-header handler or `TryAddWithoutValidation`
   of these headers.
4. AD-18 and Story 2.7 are recorded; the Tenants submodule fix is tracked as a coordinated follow-up.
5. Release build + configured focused tests pass.

---

*Produced by `bmad-correct-course` (Incremental mode), 2026-07-07.*
