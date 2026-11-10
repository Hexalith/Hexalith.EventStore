---
title: 'Close five deferred REST contract and generator defects'
type: 'bugfix'
created: '2026-11-10'
status: 'done'
route: 'oneshot'
review_loop_iteration: 0
context: []
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

Close DW-66, DW-67, DW-68, DW-69 and DW-72 from `deferred-work.md` as one focused REST metadata and generated endpoint hardening change. Runtime query-binding attributes and generator diagnostics must reject `None` with a non-null entity value and reject leading/trailing whitespace in aggregate/entity route or constant bindings. Preserve valid literal values exactly, including internal spaces, and preserve omitted/null entity values with `None`.

Reject undefined assembly or route tenant-source enum values with a stable generator error instead of silently selecting claims behavior. Preserve every defined tenant mode and existing inheritance behavior; this does not implement the separately owned tenant-boundary migration. Generated command actions must declare the canonical 1 MiB body limit. Query `ArgumentException` failures must return a fixed safe 400 detail without exposing exception text.

Use existing Contracts and generator test infrastructure, including compiled controller metadata and execution. Record verification and an independent review, mark exactly these five ledger entries resolved, then commit and push to `main` as requested. Do not change dependency pins, submodules, deployment settings, release inventory or unrelated deferred items.

</frozen-after-approval>

## Implementation Notes

- Investigation confirmed all five defects in the current source. The existing REST hardening backlog S1/S3/S5/S6 supplies the intended policies; rejecting padded bindings avoids mutating literal identifiers. No new public runtime API, migration or deployment is required. The small implementation uses the one-shot route.
- Baseline: `aspire start --apphost src/Hexalith.EventStore.AppHost/Hexalith.EventStore.AppHost.csproj --non-interactive` succeeded; `aspire describe` inspected resource state and `aspire wait sample-api --apphost src/Hexalith.EventStore.AppHost/Hexalith.EventStore.AppHost.csproj --timeout 30 --non-interactive` reported healthy. Stop the owned run before rebuilding.
- Source baseline: `9ef50d5fd6d8728a89f41228fc66e90118a59a85`.

- Implemented all five fixes with regression coverage. Moved `RestQueryBindingSource` into its own file to retain one type per C# file; its public identity and values are unchanged. Preserved Roslyn null constants so omitted/explicit-null entity values agree with runtime construction.
- Verification: Debug source-reference builds of generator tests and Contracts tests succeeded with zero warnings/errors. Direct xUnit runs passed all 178 generator tests and all 53 tests in `Hexalith.EventStore.Contracts.Tests.Rest.*`, with no skips.
- Initial validation exposed test-fixture mistakes (query metadata must be static, framework body-size metadata is exposed through `IRequestSizeLimitMetadata`) which were corrected. `dotnet test --project tests/Hexalith.EventStore.RestApi.Generators.Tests/Hexalith.EventStore.RestApi.Generators.Tests.csproj --configuration Debug -p:UseHexalithProjectReferences=true -m:1 --no-restore --nologo` forwarded `-m:1` to MTP and ran zero tests; used the documented built-assembly fallback successfully.

- Sample conformance: `dotnet build samples/Hexalith.EventStore.Sample.Api.GeneratedCodeQuality/Hexalith.EventStore.Sample.Api.GeneratedCodeQuality.csproj --configuration Debug -p:UseHexalithProjectReferences=true -warnaserror -m:1 --nologo` passed with zero warnings/errors, compiling the real generated controller as ordinary code under repository analyzers.
- Exact focused commands: `dotnet build tests/Hexalith.EventStore.Contracts.Tests/Hexalith.EventStore.Contracts.Tests.csproj --configuration Debug -p:UseHexalithProjectReferences=true -m:1 --nologo`; `dotnet tests/Hexalith.EventStore.Contracts.Tests/bin/Debug/net10.0/Hexalith.EventStore.Contracts.Tests.dll -class 'Hexalith.EventStore.Contracts.Tests.Rest.*'` → 53/53. `dotnet build tests/Hexalith.EventStore.RestApi.Generators.Tests/Hexalith.EventStore.RestApi.Generators.Tests.csproj --configuration Debug -p:UseHexalithProjectReferences=true -m:1 --no-restore --nologo`; `dotnet tests/Hexalith.EventStore.RestApi.Generators.Tests/bin/Debug/net10.0/Hexalith.EventStore.RestApi.Generators.Tests.dll` → 178/178.
- Commit-message preflight: repository-pinned `@commitlint/cli@21.2.4`, `node_modules/.bin/commitlint --edit /tmp/eventstore-five-rest-commit-message.txt --verbose` → 0 problems, 0 warnings. `git diff --check` passed. Root-declared submodules were clean, had no pending upstream commits, and each recorded HEAD was contained by its origin/main. `git fetch origin` confirmed main had no divergence before publication.

- The owned Aspire baseline was stopped successfully before any source changes or builds.
- Independent blind review produced five low findings, all addressed below; it found no confirmed behavioral defect. Added executed binding and tenant-resolution regressions, referenced-contract cases and Unicode padding coverage. HESREST014 now names the offending assembly or fully qualified contract; assembly identity participates in the incremental options comparison.

## Review Triage Log

| Finding | Verdict and evidence | Disposition |
| --- | --- | --- |
| HESREST014 omitted the offending assembly/contract identity. | low: the previous message only contained the numeric value; referenced consumers could not identify its owner. Existing generator diagnostics use Location.None. | Patched to include the assembly name or fully qualified contract; source and referenced regressions assert the owner. |
| Defined tenant-mode compilation tests did not establish inheritance/override behavior. | low verification gap: the implementation preserves GetTenantSource, but compilation alone cannot prove which tenant is sent. | Added executing query tests for inherited Route/Claims, differing explicit overrides, and retained assembly Inherit behavior. |
| Undefined route tenant-source regression omitted metadata references. | low verification gap: referenced discovery feeds the same emitter but had no direct regression. | Added an API-scoped referenced-contract case asserting HESREST014, contract identity and no controller. |
| Positive binding tests did not assert forwarded IDs or referenced omitted/null values. | low verification gap: generation alone could miss literal mutation. | Added executing query assertions for exact internal-space literals and omitted/explicit-null EntityId, plus positive referenced compilations. |
| Generator padding cases omitted runtime-covered Unicode whitespace. | low verification gap: both validators use Trim, but ASCII-only cases would not pin agreement. | Added tab and nonbreaking-space cases across aggregate/entity route and constant bindings, for source and referenced contracts. |

- Final verification after review patches: generator Debug/source build succeeded with zero warnings/errors; its full built-assembly run passed 191/191, no skips. The focused Contracts lane remained 53/53, and the Sample generated-code conformance build passed. All five review findings are addressed; no new deferred item was necessary. Scope is focused validation, not a claim that the full solution or hosted CI ran locally.
