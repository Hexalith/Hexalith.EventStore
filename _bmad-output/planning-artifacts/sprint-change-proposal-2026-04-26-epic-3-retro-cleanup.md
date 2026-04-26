# Sprint Change Proposal - Epic 3 Retrospective Carry-Over Fixes

**Date:** 2026-04-26
**Author:** Bob (Scrum Master)
**Project Lead:** Jerome
**Trigger:** Epic 3 retrospective (`epic-3-retro-2026-04-26.md`) action items R3-A1..R3-A8
**Mode:** Batch
**Scope Classification:** **Moderate** - backlog reorganization, one bookkeeping correction, and live verification follow-up. No PRD redefinition or architecture replan.

---

## 1. Issue Summary

Epic 3 delivered a coherent Command REST API and error experience, but the retrospective found several carry-over items that should not be allowed to drift into Epic 4.

| ID | Finding | Evidence | Status |
|---|---|---|---|
| R3-A1 | Replay path still validates correlation IDs as GUIDs, violating the Epic 3 ULID rule | `src/Hexalith.EventStore/Controllers/ReplayController.cs:78`, `:84` | Needs fix story |
| R3-A2 | `EventEnvelope.AggregateType` is still populated from `identity.Domain` in both real and fake persisters | `src/Hexalith.EventStore.Server/Events/EventPersister.cs:83`, `src/Hexalith.EventStore.Testing/Fakes/FakeEventPersister.cs:61` | Existing Epic 1 story covers core fix; reconfirm scope |
| R3-A3 | `CommandStatus.IsTerminal()` remains private duplicated controller logic | `CommandStatusController.cs:171`, `AdminTraceQueryController.cs:223` | Existing Epic 2 story covers fix |
| R3-A4 | Story 3.6 file status says `review` while sprint-status says `done` | `_bmad-output/implementation-artifacts/3-6-openapi-specification-and-swagger-ui.md:3` | Bookkeeping correction applied by this proposal |
| R3-A6 | Tier 3 assertions may still expect pre-Story-3.5 error contracts | Story 3.5 notes explicitly call this out as out of scope | Needs verification/fix story |
| R3-A7 | Live Aspire verification was missing during retro; current AppHost is running but command bootstrap logs show 500s | Aspire MCP resources healthy; structured logs show `/api/v1/commands` 500 and dead-letter publication for tenant bootstrap | Needs verification/fix story |
| R3-A8 | Prior-retro action follow-through is not a story-creation gate | R2 items remained backlog while Epic 3 closed | Process update |

**Current live-check update.** After the retro, Aspire MCP detected a running AppHost. Core resources are running and healthy, and these endpoints returned 200:

- `http://localhost:8080/swagger/index.html`
- `http://localhost:8080/openapi/v1.json`
- `http://localhost:8080/problems/validation-error`

However, structured logs and traces show command-bootstrap failures:

- `Hexalith.Tenants.Bootstrap.TenantBootstrapHostedService` logged a 500 response from `POST /api/v1/commands`.
- `GlobalExceptionHandler` logged unhandled exceptions on `/api/v1/commands`.
- EventStore traces repeatedly report `GetConfiguration` errors with `configuration stores not configured`.

So R3-A7 is not "start Aspire" anymore. It is now "finish live command-surface verification and diagnose the current command POST failures."

---

## 2. Checklist Execution Summary

| Checklist Section | Status | Notes |
|---|---|---|
| 1. Trigger and context | Done | Trigger is Epic 3 retro carry-over findings with concrete source locations |
| 2. Epic impact | Done | Epic 4 is affected because Pub/Sub/backpressure depends on correct replay, event metadata, and live command behavior |
| 3. Artifact conflicts | Done | PRD remains valid; epics/sprint-status/story files need updates; architecture needs no replan |
| 4. Path forward | Done | Direct adjustment plus verification story cluster is the best path |
| 5. Proposal components | Done | Detailed stories and handoff are below |
| 6. Final review/handoff | Action-needed | This proposal is drafted and status entries are added; implementation still requires dev execution |

---

## 3. Impact Analysis

### Epic Impact

- **Epic 3 (`done`)**: remains closed. Story 3.6 bookkeeping is corrected to `Status: done`.
- **Epic 4 (`done` in sprint-status, but next in roadmap sequence for this retro context)**: affected by R3-A1, R3-A2, and R3-A7 because Pub/Sub delivery, backpressure, and replay recovery depend on a reliable command API surface and correct event metadata.
- **Future admin / operations epics**: affected by R3-A2 because admin storage, type catalog, and Pub/Sub diagnostics consume event metadata.

### Story Impact

New standalone post-Epic-3 cleanup stories:

| New Story Key | Title | Source Action | Priority |
|---|---|---|---|
| `post-epic-3-r3a1-replay-ulid-validation` | Replace replay GUID validation with ULID/non-whitespace rule | R3-A1 | High |
| `post-epic-3-r3a6-tier3-error-contract-update` | Update Tier 3 assertions for Story 3.5 ProblemDetails changes | R3-A6 | Medium |
| `post-epic-3-r3a7-live-command-surface-verification` | Verify running Aspire command surface and diagnose current command POST failures | R3-A7 | High |
| `post-epic-3-r3a8-retro-follow-through-gate` | Add prior-retro action audit to story-creation process | R3-A8 | Medium |

Existing cleanup stories remain authoritative:

- `post-epic-1-r1a1-aggregatetype-pipeline` remains the core R3-A2 fix.
- `post-epic-2-r2a2-commandstatus-isterminal-extension` remains the core R3-A3 fix.
- Existing post-Epic-1 and post-Epic-2 cleanup stories should not be duplicated; the new proposal explicitly points back to them.

### Artifact Conflicts

- **PRD:** No change. The PRD already requires command replay, dead-letter traceability, event metadata, and Pub/Sub/backpressure behavior.
- **Epics:** No current story acceptance criteria need amendment; the Epic 3 ULID rule already exists in `epics.md`.
- **Architecture:** No replan. R3-A2 is implementation drift against the existing event metadata model.
- **UX / Error docs:** No content change required unless Tier 3 verification finds stale expectations.
- **Sprint status:** Add post-Epic-3 cleanup keys and update `last_updated`.
- **Story 3.6 file:** Correct `Status: review` -> `Status: done`.

---

## 4. Recommended Approach

**Direct adjustment with a verification story cluster.**

The findings are specific and local. No rollback is useful because Epic 3's API contract is broadly correct. No MVP review is needed because the PRD goals remain achievable. The right course is to add targeted cleanup work, correct the story-status drift now, and treat live command-surface failures as a first-class verification story.

**Sequence:**

1. Apply this proposal's planning/status edits.
2. Fix R3-A1 before any replay or dead-letter recovery scenario is used as evidence.
3. Run R3-A7 live verification and diagnose current `/api/v1/commands` 500s.
4. Keep R3-A2 and R3-A3 routed to the already-existing post-Epic-1 / post-Epic-2 stories.
5. Run R3-A6 before the next full integration-test gate.
6. Add R3-A8 to the story-creation process before more stories are generated.

**Risk level:** Medium until R3-A7 is resolved, because the running topology currently reports command POST failures despite healthy resources.

---

## 5. Detailed Change Proposals

### Proposal 1 - `post-epic-3-r3a1-replay-ulid-validation`

**Problem.** `ReplayController` still performs:

```csharp
if (!Guid.TryParse(correlationId, out _))
```

and returns:

```text
Correlation ID '{correlationId}' is not a valid GUID format.
```

That conflicts with Story 3.2 and Story 3.5, which explicitly forbid `Guid.TryParse` for command identifiers.

**Edits:**

```text
File: src/Hexalith.EventStore/Controllers/ReplayController.cs

OLD:
if (!Guid.TryParse(correlationId, out _)) {
    ...
    $"Correlation ID '{correlationId}' is not a valid GUID format."
}

NEW:
if (string.IsNullOrWhiteSpace(correlationId)) {
    ...
    "Correlation ID is required."
}

Optional stricter path:
Use Hexalith.Commons.UniqueIds ULID validation if the API contract wants ULID-only.
Do not use Guid.TryParse.
```

**Tests:**

- Valid ULID path parameter is accepted by replay validation.
- Empty or whitespace path parameter returns 400.
- Regression test proves a ULID that is not a GUID-shaped value is not rejected by the replay controller.
- `rg "Guid.TryParse\\(correlationId" src/Hexalith.EventStore` returns no replay/status command-ID validation hits.

**Done when:** Replay accepts valid ULID identifiers and the 400 text no longer says "GUID".

---

### Proposal 2 - R3-A2 Routing: AggregateType Pipeline

**Problem.** Both the real persister and testing fake still write `AggregateType: identity.Domain`.

**Authoritative existing story:** `post-epic-1-r1a1-aggregatetype-pipeline`.

**Adjustment.** Do not create a duplicate post-Epic-3 story. Instead, tighten the existing story acceptance criteria during implementation:

```text
Acceptance criteria addition:
- EventPersister and FakeEventPersister must both reject identity.Domain as the AggregateType source.
- Tests must assert the real aggregate type, not merely "not unknown".
- Any fake used by downstream tests must match real EventPersister semantics.
```

**Done when:** Real and fake persisters populate `EventEnvelope.AggregateType` from the registered aggregate type, never from `identity.Domain`.

---

### Proposal 3 - R3-A3 Routing: Shared `CommandStatus.IsTerminal()`

**Problem.** `CommandStatusController` and `AdminTraceQueryController` each keep private terminal-status logic.

**Authoritative existing story:** `post-epic-2-r2a2-commandstatus-isterminal-extension`.

**Adjustment.** Do not duplicate. Keep the existing post-Epic-2 story and ensure it includes both controllers.

**Done when:**

- `CommandStatus.IsTerminal()` or equivalent shared extension exists in Contracts.
- Both controllers consume it.
- All 8 enum values are covered by unit tests.

---

### Proposal 4 - R3-A4 Story 3.6 Bookkeeping Correction

**Problem.** Story file says `Status: review`, while `sprint-status.yaml` marks the story done and the Dev Agent Record says implementation completed.

**Edit applied by this proposal:**

```text
File: _bmad-output/implementation-artifacts/3-6-openapi-specification-and-swagger-ui.md

OLD:
Status: review

NEW:
Status: done
```

**Done when:** Story file and sprint-status agree.

---

### Proposal 5 - `post-epic-3-r3a6-tier3-error-contract-update`

**Problem.** Story 3.5 completed the intended client-facing ProblemDetails changes, but its notes say Tier 3 tests that assert 401 response fields, correlation IDs, tenant IDs, or type URIs may still expect the old behavior.

**Scope:**

- Search `tests/Hexalith.EventStore.IntegrationTests` for generic RFC ProblemDetails URI expectations.
- Update stale assertions to the current `ProblemTypeUris` contract.
- Verify 401/503 responses do not expect pre-pipeline `correlationId` or `tenantId`.
- Verify 409 responses do not expect removed internal extensions.

**Done when:** Full integration tests no longer fail due to stale Story 3.5 error-contract expectations.

---

### Proposal 6 - `post-epic-3-r3a7-live-command-surface-verification`

**Problem.** Aspire is now running and resources report healthy, but live logs show command POST failures.

**Current evidence:**

- EventStore resource: running, healthy, `http://localhost:8080`.
- Swagger UI, OpenAPI JSON, and local problem docs return 200.
- Structured logs show `/api/v1/commands` 500 responses during tenant bootstrap.
- Dead-letter publication occurred for `BootstrapGlobalAdmin`.
- EventStore traces repeatedly show `GetConfiguration` errors with `configuration stores not configured`.

**Scope:**

1. Use Aspire MCP to inspect `eventstore`, `tenants`, `eventstore-dapr-cli`, `tenants-dapr-cli`, `statestore`, and `pubsub`.
2. Validate the public docs endpoints:
   - `/swagger/index.html`
   - `/openapi/v1.json`
   - `/problems/validation-error`
3. Submit or reproduce a command request using the dev auth mode available in the current AppHost.
4. Diagnose why tenant bootstrap command POSTs return 500.
5. Decide whether the configuration-store trace errors are expected in this topology or a real missing component.
6. Record findings in the story Dev Agent Record and update the Epic 3 readiness note if the command surface is not clean.

**Done when:**

- Command POST path has a known pass/fail result with evidence.
- If failing, a root cause is filed or fixed.
- Aspire resource/log/trace evidence is recorded, not inferred.

---

### Proposal 7 - `post-epic-3-r3a8-retro-follow-through-gate`

**Problem.** Epic 2 retro actions were visible, but some remained backlog while Epic 3 closed. Story creation needs an explicit prior-retro gate.

**Edit target:** story-creation skill/template or `CLAUDE.md` if no local template is available.

**Proposed checklist item:**

```text
Prior Retro Follow-Through:
- [ ] Review the latest completed epic retrospective.
- [ ] List any carry-over actions that affect this story.
- [ ] Confirm whether each action is completed, superseded, or intentionally deferred.
- [ ] If the story depends on an open retro action, either include it in scope or document the risk.
```

**Done when:** New story creation includes the prior-retro check before acceptance criteria are finalized.

---

## 6. Sprint Status Changes

Add a new post-Epic-3 cleanup section:

```yaml
# Post-Epic-3 Retro Cleanup (sprint-change-proposal-2026-04-26-epic-3-retro-cleanup.md)
# Standalone follow-up stories from Epic 3 retrospective.
# R3-A2 is covered by post-epic-1-r1a1-aggregatetype-pipeline.
# R3-A3 is covered by post-epic-2-r2a2-commandstatus-isterminal-extension.
# R3-A4 was corrected directly in the Story 3.6 file.
post-epic-3-r3a1-replay-ulid-validation: backlog
post-epic-3-r3a6-tier3-error-contract-update: backlog
post-epic-3-r3a7-live-command-surface-verification: backlog
post-epic-3-r3a8-retro-follow-through-gate: backlog
```

---

## 7. Implementation Handoff

**Scope:** Moderate.

**Development team:**

- Implement R3-A1.
- Execute R3-A6.
- Execute R3-A7 and either fix or file root cause for live command POST failures.

**Scrum Master / process owner:**

- Implement R3-A8 in the story-creation process.
- Ensure R3-A2 and R3-A3 stay routed to the existing backlog stories rather than duplicating work.

**Project Lead:**

- Confirm whether the command bootstrap 500s are acceptable during current development startup or must block Epic 4 evidence.

---

## 8. Approval and Routing

This proposal is ready for implementation routing.

Recommended approval scope:

1. Accept the new post-Epic-3 cleanup section in `sprint-status.yaml`.
2. Accept the direct Story 3.6 status correction.
3. Execute R3-A1 and R3-A7 before using Epic 3 as evidence for Epic 4 Pub/Sub/backpressure behavior.
4. Execute R3-A6 before the next full integration-test gate.
5. Execute R3-A8 before creating additional story files from the backlog.

