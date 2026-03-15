# Sprint Change Proposal — EventStore Documentation Refresh

**Date:** 2026-03-15
**Triggered by:** Jerome — repository documentation has drifted from the implemented EventStore surface area
**Author:** Jerome (facilitated by SM agent)
**Change scope:** Moderate
**Mode:** Batch (assumed for repository-wide documentation refresh)

---

## Section 1: Issue Summary

The EventStore documentation no longer tells a single coherent story.

Several parts of the repository describe different product surfaces:

- the **manual docs** still describe the core command pipeline correctly in many places, but under-document newly implemented **query**, **projection notification**, and **SignalR** capabilities;
- the **NuGet package guide** still says there are **5 published packages**, while the repository now contains a dedicated **`Hexalith.EventStore.SignalR`** package and matching tests/sample usage;
- the **roadmap and surrounding narrative** still treat query and projection-change workflows as future-facing in places, even though the codebase already exposes `api/v1/queries`, projection notification endpoints, and a SignalR hub/client path;
- the **planning artifacts** (`prd.md`, `epics.md`, `architecture.md`) have diverged in the opposite direction on some points — for example they describe a newer 14-field envelope and newer query contract model that the current shipping code does not yet expose.

This creates three user-facing risks:

1. developers can follow documentation that omits currently available capabilities;
2. readers can encounter contradictory statements about the package inventory and API surface;
3. maintainers lose trust in the docs because roadmap/planning docs and implementation docs are drifting independently.

**Concrete evidence discovered during analysis**

- `docs/reference/nuget-packages.md` still says **5** packages ship together.
- `docs/guides/upgrade-path.md` also says **5** packages ship together.
- `src/Hexalith.EventStore.SignalR/Hexalith.EventStore.SignalR.csproj` exists and is documented by its assembly/package description as a real package surface.
- `src/Hexalith.EventStore.CommandApi/Controllers/QueriesController.cs` exposes **`POST /api/v1/queries`**.
- `src/Hexalith.EventStore.CommandApi/Controllers/ProjectionNotificationController.cs` exposes **`POST /projections/changed`**.
- `src/Hexalith.EventStore.CommandApi/Program.cs` conditionally maps **`/hubs/projection-changes`**.
- `samples/Hexalith.EventStore.Sample.BlazorUI/` demonstrates the SignalR client and the three UI refresh patterns.
- `docs/community/roadmap.md` still frames parts of the query/projection surface as future work.
- `README.md` and several manual docs do not clearly surface the new query + SignalR story even though the code does.
- planning artifacts (`_bmad-output/planning-artifacts/*.md`) now contain assumptions that are ahead of the actual code in some areas, especially around envelope shape and query contract details.

---

## Section 2: Impact Analysis

### Epic Impact

| Epic                                         | Impact Level | Summary                                                                                                                            |
| -------------------------------------------- | ------------ | ---------------------------------------------------------------------------------------------------------------------------------- |
| **Documentation / DX surfaces**              | **High**     | README, getting-started docs, reference docs, and package docs need alignment with the actual repository surface.                  |
| **Query / projection feature communication** | **High**     | Query and projection-notification capabilities exist in code but are not documented consistently.                                  |
| **Planning artifacts**                       | **Moderate** | PRD, epics, and architecture need selective course-correction so they clearly distinguish implemented behavior from future intent. |
| **All other epics**                          | **Low**      | No code behavior change required; this is a documentation and planning-alignment effort.                                           |

### Story / Artifact Impact

| Artifact Type                                       | Impact   | Notes                                                                                                                                                         |
| --------------------------------------------------- | -------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| **README**                                          | High     | Must reflect the expanded package surface and the query/SignalR story.                                                                                        |
| **Getting started docs**                            | Moderate | Need runtime command consistency (`aspire run` guidance) and cross-links to query / Blazor sample flows where relevant.                                       |
| **Concept docs**                                    | High     | Architecture and command/event narrative should include the current query + projection notification capabilities without overstating what is not yet shipped. |
| **Reference docs**                                  | High     | Need explicit coverage for query endpoints, projection notifications, SignalR hub/client, and updated package inventory.                                      |
| **Guides**                                          | Moderate | Upgrade path and troubleshooting should reflect the current package count and runtime surfaces.                                                               |
| **Community roadmap**                               | Moderate | Must stop presenting already-implemented query/SignalR elements as purely future work.                                                                        |
| **Planning artifacts (PRD / epics / architecture)** | Moderate | Selective updates needed so implementation-facing docs and planning docs do not contradict one another.                                                       |

### Technical Impact

No application behavior changes are required.

This is a **documentation synchronization** change with these technical consequences only:

- refresh manual docs to match the implemented API/package surface;
- add or expand reference content for query/projection/SignalR features;
- clarify which capabilities are shipping now versus still roadmap/planning;
- align planning artifacts where they materially contradict the repository’s current state.

---

## Section 3: Recommended Approach

**Path:** Direct Adjustment (Option 1)

This is the right path because the problem is not architectural failure — it is repository documentation drift.

### Option review

- **Option 1: Direct adjustment** — **Viable**
    - Update the docs set in-place.
    - Add missing query/SignalR coverage.
    - Correct package counts, runtime instructions, and feature status messaging.
    - Selectively correct planning artifacts where they directly contradict shipped behavior.

- **Option 2: Potential rollback** — **Not viable**
    - Rolling back code to match the older docs would discard implemented functionality and make the repository less accurate.

- **Option 3: PRD MVP review** — **Not currently needed**
    - The issue is documentation accuracy, not MVP viability.
    - A selective planning-artifact correction is sufficient; no scope reduction or fundamental product rethink is required.

### Recommendation rationale

- **Effort:** Medium — several high-value markdown files need updates, but no code rework is required.
- **Risk:** Low/Medium — the main risk is introducing new inconsistency if only part of the docs set is refreshed.
- **Timeline impact:** Short-term docs work only; improves onboarding and maintainability immediately.
- **Sustainability:** High — once the docs are grouped around the actual product surfaces (command API, query API, projections, SignalR, package inventory), future maintenance becomes easier.

---

## Section 4: Detailed Change Proposals

### Proposal Group A — Public entry points

#### Proposal A1: README refresh

**Artifact:** `README.md`

**OLD:**

- Presents the command pipeline well but only lightly hints at projections.
- Does not clearly advertise the implemented query + SignalR story.
- Documentation navigation does not guide readers to query/projection/SignalR capabilities.

**NEW:**

- Keep the command-first narrative.
- Add a concise section describing the implemented query path (`/api/v1/queries`), projection invalidation, and SignalR client sample.
- Update the docs navigation to include the new/expanded reference material.
- Mention the Blazor sample UI as the demonstration of the three refresh patterns.

**Rationale:** The README is the repo’s front door. It should reflect the current product, not only the earlier MVP slice.

#### Proposal A2: Quickstart consistency update

**Artifacts:** `docs/getting-started/quickstart.md`, `docs/getting-started/first-domain-service.md`

**OLD:**

- Quickstart uses `dotnet run --project src/Hexalith.EventStore.AppHost` while repository guidance elsewhere centers on `aspire run`.
- The getting-started flow ends after the command path with little visibility into the newer query/SignalR sample capabilities.

**NEW:**

- Standardize the recommended local run command and explain when to use it.
- Preserve the command-first learning flow.
- Add a short “what to explore next” section pointing to query + SignalR sample capabilities rather than pretending they are absent.

**Rationale:** Onboarding should stay linear while still acknowledging the broader current surface area.

### Proposal Group B — Concept and reference docs

#### Proposal B1: Add current query/projection documentation

**Artifacts:**

- `docs/reference/command-api.md` (expand or split)
- new query-focused reference page if needed
- relevant concept pages such as `docs/concepts/architecture-overview.md`

**OLD:**

- The command API reference is detailed, but the implemented query API and projection notification flow are not represented as first-class docs.

**NEW:**

- Document `POST /api/v1/queries`.
- Document `POST /projections/changed`.
- Document the conditional SignalR hub path `/hubs/projection-changes` and its relationship to projection change broadcasting.
- Explain the implemented ETag-assisted query path at a user-facing level, without promising unimplemented roadmap behaviors.

**Rationale:** Readers should not have to reverse-engineer the query story from source files.

#### Proposal B2: Package guide correction

**Artifacts:** `docs/reference/nuget-packages.md`, `docs/guides/upgrade-path.md`

**OLD:**

- Says the project ships **5** packages.
- Does not include `Hexalith.EventStore.SignalR`.

**NEW:**

- Update the guide to **6** packages.
- Add `Hexalith.EventStore.SignalR` with purpose, intended consumers, and installation guidance.
- Update dependency and versioning language accordingly.

**Rationale:** Package inventory is a hard factual contract; this must be exact.

#### Proposal B3: Architecture and feature-status refresh

**Artifacts:** `docs/concepts/architecture-overview.md`, `docs/community/roadmap.md`

**OLD:**

- Architecture overview is command-centric and underplays the now-implemented query/projection/SignalR path.
- Roadmap language still treats some already-implemented query capabilities as future work.

**NEW:**

- Expand the architecture overview to show both command flow and projection/query notification flow.
- Reclassify roadmap items so it clearly distinguishes:
    - implemented now,
    - partially implemented / maturing,
    - still planned.

**Rationale:** Readers need a truthful picture of what exists today versus what is next.

### Proposal Group C — Planning artifact course-correction

#### Proposal C1: Selective PRD / Epics / Architecture alignment

**Artifacts:**

- `_bmad-output/planning-artifacts/prd.md`
- `_bmad-output/planning-artifacts/epics.md`
- `_bmad-output/planning-artifacts/architecture.md`

**OLD:**

- Some planning artifacts now describe a future contract that the current shipping code does not yet expose (for example, newer envelope and query contract assumptions).

**NEW:**

- Preserve future intent where appropriate.
- Clearly separate **implemented** behavior from **planned** behavior.
- Correct factual statements that currently read as if already shipped when they are not.
- Ensure package inventory and implemented API versioning/routes match the repository.

**Rationale:** Planning docs may be aspirational, but they should not masquerade as current implementation docs.

#### Proposal C2: Sprint status update after approval

**Artifact:** `_bmad-output/implementation-artifacts/sprint-status.yaml`

**OLD:**

- Does not yet reflect the approved documentation course-correction work.

**NEW:**

- Add or update the documentation-related work item(s) after approval so the planning trail remains accurate.

**Rationale:** The workflow requires the sprint tracking artifact to match approved changes.

---

## Section 5: Implementation Handoff

### Change Scope Classification: **Moderate**

This is broader than a typo pass, but it does not require a product replan.

### Handoff Plan

| Role                 | Responsibility                                         | Deliverables                                    |
| -------------------- | ------------------------------------------------------ | ----------------------------------------------- |
| **SM agent**         | Produce the Sprint Change Proposal and obtain approval | This proposal document                          |
| **Dev / Docs agent** | Update README and manual docs to match code            | Updated markdown files                          |
| **Dev / Docs agent** | Add or expand query/projection/SignalR docs            | New or expanded reference/concept pages         |
| **Dev / Docs agent** | Correct package inventory and upgrade guidance         | Updated package/version docs                    |
| **PO / Maintainer**  | Confirm roadmap language for implemented vs planned    | Approved wording for roadmap/planning alignment |
| **SM / Dev**         | Update `sprint-status.yaml` after approval             | Revised sprint tracking                         |

### Success Criteria

- [ ] README reflects the current product surface, including query/projection/SignalR entry points.
- [ ] `docs/reference/nuget-packages.md` lists the correct package inventory, including `Hexalith.EventStore.SignalR`.
- [ ] upgrade guidance no longer says the repository ships only 5 packages.
- [ ] the query API, projection notification endpoint, and SignalR hub/client story are documented in user-facing docs.
- [ ] architecture docs distinguish current implementation from planned roadmap items.
- [ ] roadmap wording no longer presents already-implemented query/projection capabilities as purely future work.
- [ ] planning artifacts no longer contradict the current repository on core shipped facts.
- [ ] sprint tracking is updated after approval.

---

**Generated:** 2026-03-15
**Status:** Pending approval — ready for batch documentation updates once approved
