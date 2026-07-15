---
created: 2026-07-15
story_id: "1.9"
story_key: 1-9-tenants-query-and-read-model-adoption
status: done
split_from: 1-6-sample-and-tenants-domain-centric-adoption
crosswalk: ../planning-artifacts/story-id-migration-2026-07-15.md
---

# Story 1.9: Tenants Query And Read-Model Adoption

Status: done

## Review Scope

The parent implementation/spec records Tenants adoption of `IDomainQueryHandler`,
`IReadModelStore`, `ReadModelWritePolicy`, and `IQueryCursorCodec`, including scoped tests.
Review must verify tenant isolation, RBAC/audit behavior, cursor scope, paging, conflicts,
persisted lifecycle state, and Story 1.2 provenance without ETag-derived aliases.

## Completion Gate

`done` requires the Tenants maintainer-approved PR/commit, exact Tenants SHA, accepted
scope, source/package mode, focused validation results, and persisted production-path
evidence. Until those external-authority facts are recorded, this child remains `review`
and does not authorize a submodule change.

Historical evidence: `spec-1-6-sample-and-tenants-domain-centric-adoption.md` and the
Story 1.6 implementation/review record.

## Review Findings

- [x] [Review][Patch] Record the accepted Tenants SHA and historical completion evidence [_bmad-output/implementation-artifacts/1-9-tenants-query-and-read-model-adoption.md:23] — APPLIED: the user accepted latest SHA `56c506c18a4c72f5fee1005948f2f9e08c2a8a5b` and authorized reuse of the historical persisted-path proofs.
- [x] [Review][Patch] Add lifecycle-state and precedence coverage for Tenants freshness mapping [references/Hexalith.Tenants/src/Hexalith.Tenants.UI/Services/Gateways/TenantQueryGateway.cs:799] — APPLIED: 14 theory cases cover current/stale precedence, rebuilding, degraded, unavailable, local-only, invalid lifecycle, and 304 behavior.
- [x] [Review][Patch] Cover provenance fail-closed behavior across membership, global-administrator, and audit branches [references/Hexalith.Tenants/src/Hexalith.Tenants.UI/Services/Gateways/TenantQueryGateway.cs:182] — APPLIED: 9 theory cases cover unknown, handler-computed, and invalid provenance across all three changed branches.

## Accepted Completion Evidence

- **Maintainer-approved commit:** On 2026-07-15 the user accepted latest Tenants commit `56c506c18a4c72f5fee1005948f2f9e08c2a8a5b`; the reviewed commit is clean and equals `origin/main`. The accepted delta from the review baseline `28630b94a7b4931dcd6796eb50ad1c21b092055d` changes four Tenants UI/test files. The current EventStore root now records the accepted SHA through a concurrent external update; this review made no parent submodule-pointer edit.
- **Accepted scope:** Story 1.9 reuses the parent Story 1.6 query/read-model adoption evidence and Story 1.2's persisted provenance evidence. The current delta closes consumer freshness/provenance coverage and strengthens the interactive-UI composition guard; it does not authorize a parent submodule pointer update.
- **Package-mode validation:** `dotnet test tests/Hexalith.Tenants.UI.Tests/Hexalith.Tenants.UI.Tests.csproj --configuration Release -m:1` — passed 904/904.
- **Source-mode validation:** `dotnet test tests/Hexalith.Tenants.UI.Tests/Hexalith.Tenants.UI.Tests.csproj --configuration Debug -m:1 -p:UseHexalithProjectReferences=true -p:HexalithMemoriesFromSource=false` — passed 904/904 with EventStore source references. The broader all-source attempt was correctly blocked by Memories' uninitialized nested `references/Hexalith.EventStore`; nested submodules were not initialized, and the documented package fallback was used.
- **Persisted production-path evidence (accepted reuse):** `spec-1-6-sample-and-tenants-domain-centric-adoption.md` records Tenants integration/configuration/projection/query validation, including persisted detail, audit, index, freshness, and invalid-identity behavior. `2-8-query-response-provenance-contract-and-route-aware-gateway-etag.md`, now adopted by Story 1.2, records the 2/2 persisted `IReadModelFreshness` production-carrier proof and the 1/1 live gateway route proving projection-backed ETag/304 and handler-computed evidence neutralization.
- **Review result:** Four adversarial layers completed with no failures. Three findings were resolved and patched; 15 overlapping, by-design, or unreachable claims were dismissed; no deferred or unresolved high/medium findings remain.
