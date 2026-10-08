# Story 6.6 — runtime-options callable admission, 2026-10-08

This is a dormant M1 prerequisite under the implementation spec and trusted-code
amendment. The full spec and all frontmatter context inputs were loaded before
implementation. Frozen intent, baseline
`1329b35e52852952ecb2c94aabf100674e9691e3`, in-progress status, M1–M8 tasks,
O01–O20 obligations and activation fences are unchanged. The run started on clean
`main` at `9542d3c9f48bf9ce1c57f2ef68904703eaba56cc`; concurrent stream-reader/test and planning-document
changes were preserved and are excluded from this prerequisite's owned paths.
No Git history, dependency pins, deployment, publication or retained data changed.

## Implemented boundary

`EventImplementationBinding` now admits each runtime-options getter as one
explicit callable. A retained primary implementation defaults to a getter from
the same exact loaded Assembly object. A separately retained getter requires its
explicit `EventManagedArtifactExecutionBinding`; same-name/hash/identity from
Default is insufficient. Both retained bindings must share the registry's
capability-loss scope. Ended getter evidence refuses later descriptor admission
and calls. The direct file-only compatibility mode remains a local supplied-code
claim and establishes no immutable executed image or complete catalog.

`RegisteredEventVersionValidation` and `RegisteredCurrentEventDeserializer`
forward separately retained getter evidence and the original cancellation token.
Getter admission checks cancellation and observed loss before invocation and
immediately after return, before hashing or parsing returned options. A callback
that cancels or records loss and returns malformed JSON therefore cannot replace
the required cancellation or capability outcome with a JSON parse failure.
Existing zero-argument local validation remains callable. All changes are internal;
no production registration or public event/wire contract changed.

Real emitted assemblies exercise implicit same-image getters, separately retained
getters, same-identity Default substitution, changed/deleted sources, disposal and
foreign loss scopes. Actual validator/deserializer execution proves forwarding,
getter call counts, exact returned CLR type and pre-cancel zero-getter/handler
calls. Service controls cover schema, identity and deserializer options at the
malformed-output cancellation/loss boundary while retaining original source bytes
and returning the private buffer budget to zero.

These controls admit trusted supplied callables; they cannot undo effects from
code that violates policy. Complete authoritative catalogs/peer pins, immutable
framework/native execution and process observation qualification remain separate.

## Verification

Exact commands, owned-source hashes, separate external paths and retained log
hashes are in [commands-and-source.json](runtime-options-2026-10-08/commands-and-source.json).

| Lane | Result | Evidence |
| --- | --- | --- |
| Debug/source Client test-project build | Exit 0, zero warnings/errors | [build](runtime-options-2026-10-08/client-build.log) |
| Five focused Client event classes | Exit 0, 89 passed, zero failed/skipped | [focused](runtime-options-2026-10-08/focused-tests.log) |
| Complete Client assembly | Exit 0, 1,428 passed, zero failed/skipped | [full](runtime-options-2026-10-08/client-full.log) |
| Required Release/package solution build | Exit 0, zero warnings/errors | [build](runtime-options-2026-10-08/release-build.log) |
| Isolated Debug/source options guards | Exit 0, 15-control baseline, eight compiling mutations killed | [receipt](runtime-options-2026-10-08/source-guards/result.json) |
| Isolated Release/package options guards | Exit 0, 15-control baseline, eight compiling mutations killed | [receipt](runtime-options-2026-10-08/package-guards/result.json) |
| Historical approval preflight `--mutations` | Exit 0, historical approval unchanged, 20 obligations/47 follow-ups open | [log](runtime-options-2026-10-08/historical-preflight.log) |
| Current amendment preflight `--mutations` | Exit 0, 22 controls, all 20 obligations open | [log](runtime-options-2026-10-08/current-preflight.log) |
| Workflow `actionlint` | Exit 0, no diagnostics | [log](runtime-options-2026-10-08/workflow-lint.log) |
| Deferred-work checker | Exit 0, existing advisories retained | [log](runtime-options-2026-10-08/deferred-check.log) |

Every isolated mutation compiled before its named test failed. Five admission
mutations produce Shouldly assertion failures; three wrapper-forwarding mutations
fail their positive actual-wrapper controls with the expected retained-object
refusal. This distinguishes missing forwarding from a noncompiling mutant. Build
and execution share a 60-second deadline per lane. The additive CI lane uses
Release/package dependencies and existing pinned actions/root-only submodule
preparation. Hosted CI execution and package publication were not observed.

The first Aspire start remained pending and was interrupted through its owned
CLI. The no-build start then succeeded. `aspire describe` showed EventStore,
Admin, Sample and Tenants waiting for security. The owned AppHost was stopped
before runtime edits/builds; the immediate cleanup `aspire ps` returned `[]`.
The final inspection found no running EventStore AppHost; an unrelated Parties
AppHost started later and was preserved. No live Dapr recovery or production
qualification is claimed.

## Remaining implementation and inputs

M1 still requires complete reviewed catalogs and serving-peer pins, full executed
artifact bindings and qualified observations. M2–M7 still require the exact Dapr
logical claim/carrier/signing trust and source binding, concrete control owners
and atomic participants, shared consumer integration and crash/cancellation
qualification. At the start of these checks the retained design candidate was
unapproved; no semantic model was adopted by this callback prerequisite. After these checks,
the owner selected the proposed model as the basis for the next implementation;
its exact choices and authority record belong to that separate handoff. Further
dormant preparation is possible; these choices are actual protocol inputs.

The exact command `test -f deploy/dapr/production-profile.yaml` returned exit 1
with no output. Production ratification, fleet/broker and required live Dapr
evidence remain absent. This passing local prerequisite closes no parent task or
obligation and does not complete Story 6.6.
