---
title: 'Close five deferred typed Gateway query generator findings'
type: 'bugfix'
created: '2026-11-22'
status: 'in-review'
route: 'oneshot'
review_loop_iteration: 0
context: []
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

Resolve the five related findings BH1–BH5 in the deferred-work ledger from the Story 9.2 guarded-write review. Typed Gateway query extensions use the existing generated-namespace resolver, including `RootNamespace` and sanitized assembly-name fallback. The generator reports a declaration-level error for missing or null result metadata, result types that cannot be generic arguments, results that cannot appear in a public signature, and static request classes. A bad declaration must leave other valid bindings available for compilation.

Keep existing wire mapping, REST generation, request discovery, and referenced-assembly enrollment. Add focused source and referenced-contract regressions, run the generator suite and review the result. Record the five dispositions and publish to `main` after verification.

</frozen-after-approval>

## Implementation Notes

- Source baseline was `d810b7a9a4db5454783a5eb776d60b708264ea45`. EventStore and all ten direct references were synchronized with upstream `main` before work began. FrontComposer was fetched with a shallow checkout. Seven reference revisions advanced and are recorded separately from the generator changes.
- The pre-edit `aspire start --apphost src/Hexalith.EventStore.AppHost/Hexalith.EventStore.AppHost.csproj --non-interactive` built with zero warnings and errors, then exited with code 2 because `/home/agent/.aspire` is read-only. `aspire describe` reported no running AppHost.
- `RestApiGenerator` supplies the shared namespace to `TypedGatewayQueryEmitter`. HESREST015 reports an invalid declaration at its source location when available. The emitter checks result visibility through containing types, closed generic arguments, and array elements, then emits valid bindings independently.
- `dotnet build tests/Hexalith.EventStore.RestApi.Generators.Tests/Hexalith.EventStore.RestApi.Generators.Tests.csproj --configuration Debug -p:UseHexalithProjectReferences=true -warnaserror -m:1 --nologo` passed. The built `net11.0` test assembly passed 17/17 focused typed-query cases and 291/291 cases in the full generator suite with zero skips.
- `dotnet build samples/Hexalith.EventStore.Sample.Api.GeneratedCodeQuality/Hexalith.EventStore.Sample.Api.GeneratedCodeQuality.csproj --configuration Debug -p:UseHexalithProjectReferences=true -warnaserror -m:1 --nologo` passed with zero warnings and errors. `git diff --check` passed. The pinned commitlint CLI 21.2.4 validated both commit messages with zero warnings and errors.
- The five generator findings are resolved in `deferred-work.md`. The spec stays `in-review` because `python3 -B tools/validate-phase-4-story-completion.py --spec _bmad-output/implementation-artifacts/spec-five-typed-gateway-query-generator-fixes.md --target spec --write` returned `STORY_IDENTITY_MISSING` for this standalone backlog batch.

## Review Triage Log

| Finding | Verdict and evidence | Disposition |
| --- | --- | --- |
| Reserved namespace segment | Medium. A generated declaration using `__arglist` requires an escape. | Escaped sanitized namespace segments and added a compilation test. |
| Invalid reference hid a same-named source query | Medium. The previous dictionary selected a candidate by display name before validation. | Validate candidates independently and prefer valid source declarations. A mixed compilation covers the collision. |
| Partial request reported on the wrong file | Low. A partial type can declare its attribute on any part. | Use the attribute location and assert its source file. |
| Missing argument lacked coverage | Low. A null constant and an incomplete constructor call enter different Roslyn paths. | Added an incomplete attribute test with a valid sibling. |
| Referenced invalid and valid declarations lacked joint coverage | Low. The all-invalid fixture only proved that the emitter omitted empty output. | Added a referenced assembly with both declarations and checked the compiled valid binding. |
