# Sprint Change Proposal — External Generated API Command-Status Location Policy

- **Date:** 2026-07-07
- **Author:** Winston (System Architect) — via Correct-Course workflow
- **Change type:** Architectural policy definition (contract-first) + one enforcement story
- **Scope classification:** Moderate (backlog reorganization — new invariant + new story; no live story reworked)
- **Mode:** Incremental
- **Status:** Approved

---

## Section 1 — Issue Summary

The REST controller source generator (`Hexalith.EventStore.RestApi.Generators`) emits, on every generated command's `202 Accepted` response, a hard-coded **relative** status `Location` header:

```csharp
// RestApiControllerEmitter.AppendCommandAction
Response.Headers["Retry-After"] = "1";
Response.Headers["Location"] = "/api/v1/commands/status/" + Uri.EscapeDataString(__hexalithResponse.CorrelationId);
return Accepted(__hexalithResponse);
```

This surfaced during the Story 2.2 (generator emission) and Story 2.3 (Sample external API host proof) follow-up reviews, and was carried forward as an unresolved deferred-work item and an open `sprint-status.yaml` action (Epic 2, owner Winston): *"Define the external generated API command-status Location policy."*

**Three concrete defects, all corroborated by tests and the deferred-work ledger:**

1. **Dangling route (404).** A dedicated external API host (e.g. `Sample.Api`) maps only generated controllers plus default endpoints. It never maps a command-status endpoint, so a client that polls that relative URL against the external host gets `404`. Confirmed by `SampleApiGeneratedControllerRuntimeTests` and the spec-2-2 / spec-2-3 deferred-work entries.
2. **Wrong authority + relative form.** The `/api/v1/commands/status/{id}` route is actually served by the **platform gateway** host (`CommandStatusController`, `[Route("api/v1/commands/status")]` → `[HttpGet("{correlationId}")]`). The platform's own `CommandsController.Submit` deliberately builds an **absolute** URI (`{Request.Scheme}://{Request.Host}/…`, "RFC 7231: Location header should be absolute URI"). The generated host copies neither the absolute form nor owns the target route — a double mismatch.
3. **Status-key ambiguity.** The generated `Location` keys by `SubmitCommandResponse.CorrelationId` (the only tracking field on the response), while **FR27 (Epic 4)** re-keys command status/archive by `MessageId`. The policy must state which identifier the status resource is canonically keyed by and must not depend on today's coincidence that `CorrelationId` defaults to `MessageId` (`CommandsController.Submit` sets `CorrelationId = string.IsNullOrWhiteSpace(request.CorrelationId) ? request.MessageId : request.CorrelationId`).

**Governance frame.** AD-3 (gateway is the command/query policy boundary) and AD-4 (generated REST is a thin gateway delegator in dedicated hosts) both place the command-status resource with the **gateway**, not the external API host. The external host must not re-implement a status endpoint.

---

## Section 2 — Impact Analysis

### Epic impact
- **Epic 2 (External Integration Surfaces)** is the topical home. Stories 2.1–2.5 shipped; the retrospective is done. This change adds **one post-retro hardening story (2.6)**; it does not rework any shipped story.
- **Epic 4 (Event Correctness And Recovery)** owns FR27 command-status re-keying. It is a *downstream, transparent* dependency of the status key, **not a blocker** for this policy — the generator change works against today's `CorrelationId` key and the identifier value migrates transparently when Epic 4 re-keys.
- No other epic is invalidated; no new epic is required; no resequencing.

### Artifact conflicts
| Artifact | Impact |
|---|---|
| `architecture.md` | **New invariant AD-17** + one Consistency Conventions row. |
| `epics.md` | **New Story 2.6** under Epic 2. |
| `deferred-work.md` | Two command-status Location entries (spec-2-2, spec-2-3) marked scheduled. |
| `sprint-status.yaml` | Action item closed (`done`); Story 2.6 registered (`backlog`); reconciled with rest-generator-hardening backlog item S2. |
| `prd.md` | **N/A** — refines FR12 behavior; no new FR; MVP unaffected. |
| `ux.md` | **N/A** — no user-facing UI surface. |

> **Numbering note.** AD-16 was claimed concurrently (Health/Probe endpoints) while this proposal was drafted; this policy is therefore **AD-17**.

### Technical impact (enforcement, designed in the dev story — not this proposal)
Because the generator emits **compile-time** code, it cannot bake in a runtime gateway URL. The absolute, configurable, fail-closed `Location` must be resolved at **request time** from an injected runtime option/service:
- **Client (runtime seam):** a command-status Location option/builder (e.g. `CommandStatusLocationOptions` carrying the gateway status base URI, or an `ICommandStatusLocationBuilder`), registered in external API host DI.
- **Generators (emission):** `RestApiControllerEmitter.AppendCommandAction` emits `if (builder.TryBuild(statusKey, out var uri)) Response.Headers["Location"] = uri;` — absolute when configured, header omitted when not. The generated controller constructor gains the injected dependency alongside `IEventStoreGatewayClient`.
- **Generated hosts:** Sample (and Tenants) API host DI registers the option (configured and/or fail-closed demonstrations).
- **Tests:** `RestApiControllerGenerationTests` and `RestApiGeneratedControllerErrorSemanticsTests` replace the relative-string assertions with policy assertions.

### Story Rewrite Gate (mandatory)
No **active** story ACs, tasks, Dev Notes, or design assumptions are superseded — Stories 2.2 and 2.3 are **done/frozen**. The behavior they shipped (relative `Location` and its generator/runtime test assertions) **is** superseded; that supersession is carried explicitly by **new Story 2.6** and the named test edits, not by a silent change to a closed story. **Gate satisfied.**

---

## Section 3 — Recommended Approach

**Selected path: Hybrid — Direct Adjustment + contract-first invariant** (mirrors the AD-15 / Story-4.7 route-provenance precedent: define the invariant now, schedule enforcement as a story).

**Policy decisions (approved):**
1. **Location form:** absolute to a configured gateway command-status base, **fail-closed** — when unconfigured, emit **no** `Location` header (never a relative/dangling one).
2. **Status key:** the **single gateway-owned tracking field** on `SubmitCommandResponse` (today `CorrelationId`), single-sourced; any `Correlation → Message-id` migration is owned by FR27 / Epic 4 and is transparent to this policy.

**Rationale:** preserves the HTTP-native polling affordance for external consumers while being RFC 7231-correct, pointing at the host that actually serves the route (the gateway, per AD-3), and failing closed (AD-10 posture — never advertise a resource the host cannot serve). Decoupling the status key from a `CorrelationId == MessageId` assumption keeps the policy stable across Epic 4.

- **Effort:** Low–Medium. **Risk:** Low (fail-closed default; independent of Epic 4).
- **Alternatives considered:** (B-alt) *suppress the header entirely* — simpler, but drops the polling affordance; (C) *external host mounts a status pass-through controller* — keeps a single client authority but makes the external host serve a command surface, an AD-3/AD-4 tension with more moving parts. Rejected in favor of the absolute, gateway-authoritative, fail-closed policy.

---

## Section 4 — Detailed Change Proposals

### 4.1 `architecture.md` — new invariant AD-17 (adopted)

Added after AD-16, before *Consistency Conventions*:

> **AD-17 - Generated Command-Status Location Is Absolute, Gateway-Authoritative, And Fail-Closed**
> - **Binds:** FR11-FR15, FR27, NFR12-NFR14, NFR16
> - **Prevents:** generated external API hosts advertising a command-status `Location` they do not serve, that resolves against the wrong authority, or that pins the status key to a pre-FR27 identifier.
> - **Rule:** the `202 Accepted` command-status `Location` is a gateway-owned affordance, never an external-host-owned route (AD-3, AD-4). The generated host maps no command-status endpoint of its own.
>   1. Configured host → **absolute** `Location` `{gatewayStatusBase}/api/v1/commands/status/{statusKey}` (RFC 7231), resolved at request time from a runtime option.
>   2. Unconfigured host → **no** `Location` header; never a relative/dangling URL (fail-closed); the `202` body still carries the tracking key.
>   3. `statusKey` is the single gateway-owned tracking field on `SubmitCommandResponse` (today `CorrelationId`); no `CorrelationId == MessageId` assumption; FR27/Epic 4 re-keying is transparent.
>   4. `Location` only on the `202` success path; mapped gateway command failures emit none.
>   5. Guardrail evidence is generator-output plus runtime tests (AD-12).

Plus a *Consistency Conventions* row:

> | Command status | The generated `202` command-status `Location` is gateway-authoritative and fail-closed (AD-17): absolute to the configured gateway status base, or omitted. External API hosts map no command-status route. |

### 4.2 `epics.md` — new Story 2.6: Generated Command-Status Location Policy

Added after Story 2.5 (full ACs in `epics.md`). Requirements covered: FR12; governed by AD-3, AD-4, AD-17; forward-compatible with FR27. Key ACs:
- Configured host → absolute `Location` with single-sourced `statusKey`.
- Unconfigured host → `Retry-After` + no `Location`; no relative URL ever (fail-closed).
- Gateway failure → no `Location` (unchanged).
- Generator-output + runtime tests assert all three behaviors; the pre-existing relative-string assertions in `RestApiControllerGenerationTests` and `RestApiGeneratedControllerErrorSemanticsTests` are replaced.
- Sample host demonstrates configured and fail-closed behavior; closes the spec-2-2/spec-2-3 deferred entries.
- Independent of FR27 re-keying; tracked `backlog` as a post-retro follow-on.

### 4.3 `deferred-work.md` — two entries scheduled

The spec-2-2 (relative Location) and spec-2-3 (Sample host relative status location) entries each gain:

> `resolution: Policy defined as architecture invariant AD-17 … enforcement scheduled as Story 2.6. status: scheduled (2026-07-07).`

### 4.4 `sprint-status.yaml` — action closed, story registered, S2 reconciled

- Action *"Define the external generated API command-status Location policy"* → `status: done` with a note referencing AD-17, Story 2.6, and closure of the spec-2-2/spec-2-3 entries.
- `2-6-generated-command-status-location-policy: backlog` added under Epic 2.
- **Reconciliation:** Story 2.6 **absorbs** the rest-generator-hardening Second-Wave item **S2 (command-status Location)**, which was blocked on this policy — point S2 at Story 2.6 to avoid double-tracking.
- `last_updated` bumped.

---

## Section 5 — Implementation Handoff

**Classification: Moderate → Product Owner / Developer.**

| Owner | Responsibility |
|---|---|
| **Winston (System Architect)** | ✅ Policy defined (AD-17) and action item closed. |
| **John (PM) / PO** | Point rest-generator-hardening backlog item **S2** at Story 2.6 (single enforcement vehicle); confirm Epic 2 gains one `backlog` follow-on story. |
| **Author (Create-Story)** | Run `bmad-create-story` for Story 2.6 → `spec-2-6-generated-command-status-location-policy.md` with the code seam (Client runtime option/builder + Generators emission + host DI + tests) and AD-12 evidence plan. |
| **Amelia (Developer)** | Implement per Story 2.6: runtime seam, emitter change, Sample host wiring, replace the relative-string test assertions; close the two deferred entries on completion. |

**Success criteria:** generated command `202` emits an absolute gateway-authoritative `Location` when configured and **no** `Location` when unconfigured; no relative status URL is ever emitted; failure path emits no `Location`; generator-output + runtime tests assert all three; spec-2-2/spec-2-3 deferred entries closed.

**Out of scope here:** FR27 command-status re-keying (Epic 4); the other rest-generator-hardening items (S1 request-size limit, S3 safe query `ArgumentException` text, S4 rejection extension allowlist, S5 tenant-source handling, S6 RestQueryBinding reconciliation) remain under their existing action item.
