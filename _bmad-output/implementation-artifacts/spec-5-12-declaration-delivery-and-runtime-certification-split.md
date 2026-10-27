---
title: '5.12 Separate declaration delivery from runtime certification'
type: 'feature'
created: '2026-10-27'
status: 'done'
baseline_commit: 'b1dd186ad49aba1c0c21d452c75fc7769feba1da'
route: 'dispatch'
review_loop_iteration: 0
parent_story_id: '5.12'
parent_story_key: '5-12-route-and-idempotency-catalog-schema-codec-and-envelope'
context:
  - AGENTS.md
  - _bmad-output/implementation-artifacts/spec-5-12-route-and-idempotency-catalog-schema-codec-and-envelope.md
---

<frozen-after-approval reason="owner approved option 2 with apply recommendation on 2026-10-27">

## Intent

**Problem:** Development catalog construction currently rejects the runtime's open query fallback even after every declared owner target and policy has passed verification. Story 5.12 consequently mixes declaration delivery with runtime enforcement that belongs beside 5.13 activation.

**Approach:** Separate deterministic declaration construction/checking from strict runtime certification. Align the architecture, 5.12/5.13 acceptance and 2.14/2.15 prerequisites in the same change. The owner explicitly approved this recommendation; no additional scope approval is needed.

## Boundaries & Constraints

**Always:** Derive all 29 entries and 17 policies from existing compiled owners, validate actual declared targets/configuration/inventories, and preserve canonical bytes/digests and failure diagnostics. Keep strict certification rejected for the current runtime. Missing handler metadata must remain an explicit 5.13 requirement: preserve the declared target or reject, never silently select another target. Preserve original spec baselines, history, prior corrections and package gates. Parent story remains in-progress while package/validation delivery is unresolved.

**Never:** Change runtime routing/authorization, activate/deploy, provision secrets, publish/update packages, mutate Git history or submodules, delete fallback probes, add a bypass boolean, or describe declaration success as complete runtime parity/readiness. This bounded amendment may finish independently of the parent story's package gate.

## I/O & Edge-Case Matrix

| Scenario | Input/state | Expected behavior | Failure |
| --- | --- | --- | --- |
| Declared Development inventory | Real compiled owners and checked-in configuration | Repeated generation/check succeeds and equals retained bytes | None; explicit runtime-unverified diagnostic |
| Declaration or output drift | Missing/ambiguous capabilities, invalid config, changed catalog bytes | Declaration construction/check rejects and preserves existing output | Existing bounded diagnostic or development-catalog-drift |
| Strict runtime certification | Current unbounded query/projection/actor fallback | Reject through retained completeness path and CLI; never write catalog | unrepresented-unbounded-query-fallback |
| Malformed file input | Oversized configuration, invalid conversion or file path | Bounded failure without values or partial output | Existing size/configuration/file diagnostic |

</frozen-after-approval>

## Code Map

- `tools/Hexalith.EventStore.CatalogConstruction/DevelopmentCatalogProfile.cs` — reuse all compiled-owner inventory/configuration/resolver checks. Existing `BuildAsync` is strict; extract declaration-only construction into a clearly named method and retain the strict wrapper/gate.
- `tools/routing-catalog.cs` — `development`/`check-development` currently call strict construction. Route them to declaration construction, expose an explicit strict runtime check, and retain bounded file reads/atomic output/exit codes.
- `tests/Hexalith.EventStore.Client.Tests/Routing/DevelopmentRouteCatalogBuilderTests.cs` — existing isolated real-CLI subprocess lane already covers generation, bounded input failures and output preservation; update its Development controls and add drift/strict-check controls.
- `tests/Hexalith.EventStore.Server.Tests/DomainServices/DevelopmentRouteCatalogParityTests.cs` — compare repeated declaration construction with retained bytes and prove invalid capabilities/config fail through both declaration and strict paths. Keep actual gateway fallback probes unchanged.
- `_bmad-output/planning-artifacts/architecture.md`, its architecture `.memlog.md`, `epics.md`, and Epic 2/5 context files — align ownership/prerequisites; update current text and append historical decisions.
- Parent 5.12 spec and `spec-2-14-canonical-tenant-boundary.md` — owner authorized frozen-scope amendments; preserve all unrelated migration/assurance requirements and keep their existing statuses.

## Tasks & Acceptance

- [x] `DevelopmentCatalogProfile.cs`, `tools/routing-catalog.cs` — separate declaration construction from strict certification using existing validation, with distinct CLI outcomes and truthful success diagnostics.
- [x] Server/Client tests above — prove repeated exact-byte generation, check success, byte drift rejection, capability/configuration rejection on declaration paths, strict rejection and output preservation beside successful controls.
- [x] `deploy/dapr/eventstore-routing-catalog.md` — document declaration success, exact strict check command, retained candidate status and package/readiness boundaries; preserve JSON bytes.
- [x] Architecture/epics/context files above and parent/2.14 specs — consistently assign exhaustive runtime parity/enforcement to 5.13 and narrow 2.14/2.15's 5.12 prerequisite to declaration/schema/codec delivery, subject to their remaining gates. Explicitly require metadata-outage rejection/target stability and cross-host agreement in 5.13. No automatic story completion/unblocking.
- [x] Focused Debug/source project builds and executable test lanes; direct CLI repeat/check/drift/strict controls; `git diff --check` and committed-file policy — record exact commands and results. Existing package failures remain separately recorded; do not repeat unrelated broad suites.

**Acceptance Criteria:**
- Given real owner declarations, when construction/checking runs, then all declared handler/policy checks still execute and produce the unchanged 29-route candidate with an explicit runtime-unverified message.
- Given undeclared runtime targets or unavailable handler metadata, when readiness is assessed, then strict completeness stays failed and the accepted 5.13 work explicitly owns closing those outcomes.
- Given downstream planning, when dependencies are read, then they distinguish accepted declarations from activated runtime authority and retain package, migration and assurance prerequisites.

## Implementation Notes

No unresolved intent choices, irreversible actions or new production API are required. The public extraction is confined to the existing nonpackable developer construction library. This bounded spec records implementation/review of the approved amendment; parent 5.12 completion is not inferred.

`BuildDeclarationsAsync` retains every compiled-owner capability, handler inventory, policy, configuration and resolver validation from the original construction path. Existing `BuildAsync` wraps that method and retains `ValidateQueryFallbackBoundary`, so it still rejects the current runtime after declaration validation. CLI `development` and `check-development` use declaration construction; `check-development-runtime ROOT` invokes the strict path and has no output argument. Declaration success reports: `Development declarations verified; runtime certification, secret availability and catalog activation are unverified.` Bounded reads, atomic replacement, diagnostics and exit codes are preserved.

The amendment uses `parent_story_id`/`parent_story_key` to keep its status independent of the full story. Parent 5.12 and its sprint row remain in-progress; 2.14 stays draft/backlog. Original baselines, historical verification and package failures remain authoritative.

## Spec Change Log

- 2026-10-27: Review corrections add normalized configuration/output collision rejection and cancellation observation, narrow per-generation proof claims, clarify strict-check boundaries, and make 5.13 request/cache/outage acceptance explicit. Completed parent declaration tasks are separated from the open package/CI gate. All original bytes, validations and runtime behavior are preserved.

- 2026-10-27: Implemented the approved declaration/strict-certification extraction and CLI modes, focused regression controls, architecture/epic/dependency alignment and catalog status documentation. No runtime or package dependency changes; the retained catalog and golden bytes are unchanged.

## Review Triage Log

Three context-free layers reviewed the 106,453-byte amendment diff. Edge-case review returned no findings; verification-gap review found no gaps. Every blind-hunter finding is classified individually below before routing. The fixes are direct code guards or artifact wording corrections within the approved amendment; they add no production API or runtime policy.

| Finding | Verdict | Evidence and route |
| --- | --- | --- |
| B1 | medium | The new generation wording overstates per-invocation executable query verification: Tenants capability enrollment returns packaged declarations, while its owning host tests independently resolve actual handlers. Narrow the new artifact wording and identify that owning-test boundary; no receipt framework or root dependency on the executable. Patch. |
| B2 | medium | A valid temporary Development profile can supply either configuration file as OUTPUT; construction reads it before atomic replacement, so successful generation overwrites an input. Reject normalized input/output path overlap with a bounded diagnostic and prove both configuration inputs survive. Patch. |
| B3 | medium | The declaration entry passes cancellation to a resolver whose static-registration branches never observe it. An already-cancelled invocation can therefore return a catalog. Observe cancellation before construction and completion, with a cancelled-token negative beside existing valid controls. Patch. |
| B4 | low | The strict wrapper retains an unconditional known-boundary rejection, rather than inspecting a live runtime. Clarify its XML summary so developer callers understand the current check. Direct documentation patch. |
| B5 | low | The strict CLI takes ROOT only and checks the compiled profile, not a running deployment or an independently supplied retained catalog. State those exact boundaries in the catalog documentation. Direct documentation patch. |
| B6 | low | Combined-log assertions do not independently pin stream placement, but the current code and direct CLI evidence verify digest-only stdout and the qualification on stderr. No stream regression exists; additional helper/tuple plumbing would harden an unlikely future regression. Reject this low-priority expansion. |
| B7 | medium | New 5.13 criteria put unknown-target rejection in readiness/topology wording, which can leave later requests underspecified. Require request-level denial before dispatch even after readiness succeeded. Direct acceptance-artifact patch. |
| B8 | medium | The new metadata cases name absence/failure but leave stale positive caches across commit/rollback implicit. Name that case in the existing generation/parity qualification; no current runtime change. Direct acceptance-artifact patch. |
| B9 | low | The activation wording can be read as promising dispatch during outages although the rule permits rejection. Clarify shared generation/target identity and acceptable local rejection, without mixed-generation readiness. Direct architecture-artifact patch. |
| B10 | low | Parent checkboxes still list declaration generation/tool separation as unfinished despite current evidence. Mark local declaration tasks complete and leave package/CI delivery explicitly open; retain in-progress parent status. Direct tracking-artifact patch. |

## Verification

Build individual Client and Server test projects with `-c Debug -p:UseHexalithProjectReferences=true -m:1 -v quiet`, then invoke built test DLLs with single-dash `-class`/`-method`. Run declaration CLI modes and strict certification serially. Preserve golden/candidate hashes and record no CI success without an actual CI run.

### Implementation evidence (2026-10-27)

Source boundary: EventStore `b1dd186ad49aba1c0c21d452c75fc7769feba1da` and unchanged Tenants `d5b8ed2067083f93e89b9981461421d50d541b19`, plus this amendment's working-tree changes. All commands ran from `/home/administrator/projects/hexalith/eventstore`, serially for builds/test execution/CLI. The recorded isolated Aspire baseline in the parent spec was reused; no host or AppHost code changed and no runtime startup was required for the offline amendment.

Both builds exited 0 with zero warnings/errors. The three executable invocations exited 0: **92 passed, zero failures, zero skips**.

| Exact build command | Exact test invocation | Result |
| --- | --- | --- |
| `dotnet build tests/Hexalith.EventStore.Client.Tests/Hexalith.EventStore.Client.Tests.csproj -c Debug -p:UseHexalithProjectReferences=true -m:1 -v quiet` | `dotnet tests/Hexalith.EventStore.Client.Tests/bin/Debug/net10.0/Hexalith.EventStore.Client.Tests.dll -class '*Routing.DevelopmentRouteCatalogBuilderTests' -noLogo` | 52 passed |
| `dotnet build tests/Hexalith.EventStore.Server.Tests/Hexalith.EventStore.Server.Tests.csproj -c Debug -p:UseHexalithProjectReferences=true -m:1 -v quiet` | `dotnet tests/Hexalith.EventStore.Server.Tests/bin/Debug/net10.0/Hexalith.EventStore.Server.Tests.dll -class '*DevelopmentRouteCatalogParityTests' -noLogo` | 36 passed |
| Uses the preceding Server build | `dotnet tests/Hexalith.EventStore.Server.Tests/bin/Debug/net10.0/Hexalith.EventStore.Server.Tests.dll -method '*CatalogCompletenessCannotCertifyGatewayActorFallback' -noLogo` | 4 passed; actual probes unchanged |

The Server controls repeatedly construct all 29 routes and 17 policies from compiled owners and compare exact retained bytes. Missing/duplicate/changed capabilities and invalid effective configuration reject through both declaration and strict APIs beside passing declaration controls. The Client lane executes isolated real CLI subprocesses for repeated Development generation, successful checking, byte-drift rejection, strict rejection, invalid configuration conversion, oversized base/Development files and invalid file arguments, asserting bounded diagnostics and preserved output. Its existing generic construction/validation, canonical-input and atomic-output controls also pass.

Direct CLI commands below were executed exactly as listed. The first four exited 0; both generated outputs equal the retained catalog byte-for-byte. The drift file was prepared as retained bytes plus one newline. Its check exited 2 with exactly `Route catalog rejected: $: development-catalog-drift` and preserved the drifted file. Strict certification exited 2 with exactly `Route catalog rejected: $.developmentProfile: unrepresented-unbounded-query-fallback`; retained and temporary output bytes were unchanged.

```text
dotnet run tools/routing-catalog.cs -c Debug -p:UseHexalithProjectReferences=true -- development /home/administrator/projects/hexalith/eventstore /tmp/story-5-12-split-development-1.json
dotnet run tools/routing-catalog.cs -c Debug -p:UseHexalithProjectReferences=true -- development /home/administrator/projects/hexalith/eventstore /tmp/story-5-12-split-development-2.json
dotnet run tools/routing-catalog.cs -c Debug -p:UseHexalithProjectReferences=true -- check-development /home/administrator/projects/hexalith/eventstore /home/administrator/projects/hexalith/eventstore/deploy/dapr/eventstore-routing-catalog.json
dotnet run tools/routing-catalog.cs -c Debug -p:UseHexalithProjectReferences=true -- validate /home/administrator/projects/hexalith/eventstore/deploy/dapr/eventstore-routing-catalog.json c156ce0b68bab43f561805db9cd02714d123026d388f65c764d7b6fa4e3b7308
dotnet run tools/routing-catalog.cs -c Debug -p:UseHexalithProjectReferences=true -- check-development /home/administrator/projects/hexalith/eventstore /tmp/story-5-12-split-development-drift.json
dotnet run tools/routing-catalog.cs -c Debug -p:UseHexalithProjectReferences=true -- check-development-runtime /home/administrator/projects/hexalith/eventstore
```

Hashes remain: retained candidate `c156ce0b68bab43f561805db9cd02714d123026d388f65c764d7b6fa4e3b7308`; v1 golden `d30010c9b1017f29c5ae9da9a39289d8865bfce91bd153b6000beaad0248f4c0`; independent v2 golden `c8b28c28e017154f6ecf6f69a60e89448a9df7d4a4161809f4aeda8c398ec049`.

`git diff --check` exited 0. `bash scripts/check-committed-files.sh` exited 0: `check-committed-files: tracked files pass (9178 checked).` Logs are `/tmp/story-5-12-split-{client,server}-build.log`, `/tmp/story-5-12-split-{client,server}-tests.log` and `/tmp/story-5-12-split-gateway-tests.log`; exact CLI arguments, outputs and byte-preservation/hash results are in `/tmp/story-5-12-split-cli-results.json`.

No broad suite, package-mode build or CI run was repeated. The parent spec's unreleased EventStore/Tenants package failures and unrelated broad Tenants documentation-test failure remain open and separately recorded. This evidence establishes the bounded amendment's declaration delivery, not runtime authority, metadata-outage enforcement, activation, production readiness, secret availability or OQ8 authority. Parent 5.12 remains in-progress and 2.14 remains draft/pending. Implementation is ready for parent review.


### Final parent review and verification (2026-10-27)

All three independent review layers completed. The ten individual blind-review findings are classified above: B1–B5 and B7–B10 were corrected with direct code/artifact patches; B6 was rejected as low-priority optional test hardening. Edge-case and verification-gap layers reported no findings. No new deferred-work entry or unresolved review finding remains for this amendment. Parent inspected the full original amendment diff and the final code/test/planning corrections.

The parent reran both exact Debug/source build commands and all three executable commands in the implementation table on the final patched source. Both builds exited 0 with zero warnings/errors. Client passed 52, Server declaration parity passed 37 (including cancelled construction), and unchanged gateway fallback probes passed 4: **93 passed, zero failures/skips**. Client's real CLI test now also rejects both direct and dot-segment-normalized output/configuration collisions, preserving both input files beside successful generation controls. The pre-fix collision was reproduced against both files in temporary profiles; those exact results are retained in `/tmp/story-5-12-split-output-repro.json`.

Parent repeated all six direct CLI controls using `/tmp/story-5-12-split-parent-repeat-{1,2}.json` and `/tmp/story-5-12-split-parent-drift.json`. Declaration generation twice, check-development and validate exited 0; drift and strict checking exited 2 with their exact expected diagnostics. Repeated outputs equal the retained catalog, and drift/retained bytes are preserved. Digest-only stdout and runtime-unverified stderr remain separate in the recorded CLI results. Candidate SHA-256 remains `c156ce0b68bab43f561805db9cd02714d123026d388f65c764d7b6fa4e3b7308`; golden fixture files are unchanged.

Exact final commands, expected/actual exits, stdout/stderr, per-command log paths and preservation assertions are retained in `/tmp/story-5-12-split-parent-final-results.json`. Logs use `/tmp/story-5-12-split-parent-final-{client,server}-{build,tests}.log` and `/tmp/story-5-12-split-parent-final-gateway-tests.log`. `git diff --check` and `bash scripts/check-committed-files.sh` both exited 0.

This bounded amendment is complete. Parent 5.12 and its sprint row remain in-progress for their recorded package/validation delivery gates; 2.14 remains draft/pending and 5.13 remains backlog with explicit runtime enforcement/exhaustive-parity obligations. Runtime certification is still rejected. No package-mode CI or broader suite was rerun, and no runtime authority, deployment or production readiness is claimed. The approved no-Git-mutation constraint was preserved; no commit or push was made.
