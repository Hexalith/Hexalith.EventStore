---
title: 'Resolve five deferred generated API contract findings'
type: 'bugfix'
created: '2026-11-22'
status: 'in-review'
route: 'oneshot'
review_loop_iteration: 0
context: []
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

Resolve DW-47, DW-70, DW-103, and the typed Gateway review findings BH6 and VG1. A generated command must reconcile every domain route segment with its body, including tenant-named segments when the route does not supply the partition tenant. Generated command rejections must preserve the gateway's bounded rejection type and published corrective action. REST headers and JSON must share canonical query provenance names and fall back to `Unknown` for unrecognized values.

Generate typed Gateway bindings for accessible nested queries in source and directly referenced Contracts. Execute a generated binding through the real Gateway client to verify the verb, path, tenant, domain, aggregate, declared query type, JSON payload, and typed result. Keep explicit referenced REST enrollment and existing query metadata behavior. Verify and review the changes, record the five ledger dispositions, and publish to `main`.

</frozen-after-approval>

## Implementation Notes

- EventStore and its ten direct references matched `origin/main` before implementation. The EventStore baseline was `22c1e66a8481178be0f88b36efbf8f1f011cd750`. FrontComposer used a shallow checkout. Five upstream pin advances will be recorded separately.
- The command diagnostic and action emitter currently apply different tenant-segment rules for command body mapping. The typed emitter walks namespaces but stops at the first level of types. The existing Roslyn test harness supports emitting and executing compiled fixtures.
- The pre-edit Aspire AppHost built with zero warnings and errors. `aspire start --apphost src/Hexalith.EventStore.AppHost/Hexalith.EventStore.AppHost.csproj --non-interactive` returned 2 when Aspire tried to write to its read-only home directory. `aspire describe` confirmed the AppHost had exited.
- Command generation and execution now use the same tenant-source-aware route mapping. The shared controller fixture supplies a body value for its domain tenant segment. The tenant catalog assertions still distinguish that domain identifier from the partition tenant.
- Generated command responses forward bounded rejection event IDs and the applicable catalog corrective action for a matching canonical domain rejection. The Contracts provenance policy drives the gateway response header, generated response header, JSON converter and client parser.
- Typed query discovery descends into accessible nongeneric nested types and uses the same rules for source and globally available `.Contracts` references. The HTTP test executes the compiled generated binding through the real Gateway client and compares the complete JSON request and typed response.

- Debug source-reference builds passed with warnings treated as errors for the generator tests, sample generated-code conformance, Contracts tests, Client tests, and Server tests. The built generator suite passed 319 tests, the Contracts query lane passed 164, and the server query controller lane passed 145.
- The Client suite passed 2,438 tests. Two live batch-recovery cases skipped because they require isolated Dapr endpoints.
- The phase-4 completion writer refused `STORY_IDENTITY_MISSING` because this standalone batch has no canonical story key. The spec remains in review.

## Review Triage Log

| Finding | Verdict and disposition |
| --- | --- |
| Domain rejection detection accepted malformed matching URIs and noncanonical reason codes. | Medium. The adapter now requires compact canonical reason codes, a matching bounded URI, and a supported command status. Regressions cover empty, malformed, and extra URI segments. |
| The type filter dropped versioned kebab-case rejection IDs. | Medium. The filter now accepts hyphens and the HTTP cases cover a versioned ID and a nested CLR type. |
| A published corrective action could appear on a different rejection. | Medium. The gateway and adapter share catalog guidance selection. A valid type still survives a mismatched action. |
| Alias-only referenced nested types could produce uncompilable global bindings. | Medium. Only globally available references enroll. The alias-only consumer fixture compiles and emits no binding for the hidden declaration. |
| Gateway enum normalization could diverge from the shared provenance names. | Low. The gateway uses Contracts normalization, and the formatter uses the same function. |
| The positive rejection tests bypassed Gateway client deserialization. | Medium. Positive cases now send ProblemDetails JSON through the real client and execute the generated command controller. |
| Rejection coverage omitted the size limit and disallowed punctuation. | Low. Independent extension tests cover both and preserve applicable guidance. |
| Typed request checks could miss extra routing or freshness fields. | Medium. The HTTP test compares the complete JSON request. |
| Route overrides lacked diagnostic and executing command coverage. | Medium. Tests now cover an assembly Route default overridden to Claims, both mapped and unmapped parameters, and dispatch only on a matching body. |
| The catalog fixture's Claims route and body disagreed. | Medium. The fixture now binds the route and body to the same domain identifier before asserting partition-tenant resolution. |
| The ledger and spec were pending during review. | Low. The five verified ledger dispositions and this implementation record accompany the product commit. |
