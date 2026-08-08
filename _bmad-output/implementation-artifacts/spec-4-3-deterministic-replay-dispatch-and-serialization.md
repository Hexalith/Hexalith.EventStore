---
title: 'Story 4.3: Deterministic Replay Dispatch And Serialization'
type: 'bugfix'
created: '2026-08-07'
status: 'done'
review_loop_iteration: 3
story_key: '4-3-deterministic-replay-dispatch-and-serialization'
baseline_commit: 'bb94d93e9b84132cff83a38fba84f25455820d31'
context:
  - '{project-root}/_bmad-output/project-context.md'
  - '{project-root}/_bmad-output/implementation-artifacts/epic-4-context.md'
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** Two independent defects let replay silently apply the wrong event or bind an empty one. (1) Both `Apply`-method resolvers key their dispatch dictionaries by **short** type name while the persister stores the **full** name, so the exact-match lookup *always* misses and every replayed event falls through an unanchored `EndsWith` fallback that takes the first match from an unordered dictionary — `Billing.SubOrderCreated` can bind `Apply(OrderCreated)`, nondeterministically across processes, with no diagnostic. (2) Payload binding uses `JsonSerializerOptions.Default` (PascalCase, case-**sensitive**) on the command, project, and pub/sub paths but Web options (case-insensitive) on the rehydrate path; since no `AddJsonOptions` exists anywhere, a normal camelCase API client's command payload binds **zero** properties and yields a default-constructed command that the `?? throw` guard cannot catch.

**Approach:** Collapse the two copy-pasted resolvers into one shared resolver that registers each event type under **both** its full name and its short name, resolves by exact match first, then a longest boundary-anchored suffix match where both `.` and `+` are name boundaries (`+` is how `Type.FullName` renders nested types), and throws a typed diagnostic when more than one candidate matches. Promote the existing `DomainProcessorStateRehydrator.SerializerOptions` into `Hexalith.EventStore.Contracts` as the single payload-binding options object and route every event/command payload read through it.

## Boundaries & Constraints

**Always:**

- Resolution order is: exact full-name key → exact short-name key → longest boundary-anchored suffix scan. A candidate `k` matches stored name `n` only when `n == k` or the character immediately before a trailing `k` in `n` is `.` or `+` (CLR namespace / nested-type boundaries). When multiple keys anchor, the longest key wins; two or more candidates under that winning key (or under an exact key) are an error.
- Two or more surviving candidates is an error with a typed exception naming the stored event type and every candidate — never a silent pick. Zero candidates keeps each path's **existing** not-found behavior unchanged.
- Exactly one resolver implementation exists when done. Deleting one copy and leaving the other is a failed implementation.
- The shared payload options keep `PropertyNameCaseInsensitive = true`. This is a **backward-compatibility requirement**, not style: PascalCase payload bytes are already at rest and must stay readable.
- Widening a reader from case-sensitive to case-insensitive is always safe. Narrowing anything is not.
- Payload protection is byte-level and wraps JSON binding; preserve the existing unprotect → bind and protect → persist ordering.
- **This change is readers-only. No payload writer changes** (human decision, 2026-08-07, superseding the earlier "convert the writer" decision — see Spec Change Log 1). Zero persisted bytes and zero wire bytes change. Widening every reader to case-insensitive is what fixes the defect; the write side is deliberately left alone.

**Ask First:**

- Any need to **modify** `EventStoreAggregateTests.cs:808-825` or `EventStoreProjectionTests.cs:195-211` to make them pass. Both inputs carry a real `.` boundary and must stay green untouched; needing to edit them means the resolution rule was built wrong. Halt rather than adjust the test.
- Any temptation to touch a payload **writer** for consistency. There are two, they disagree today, and unifying them is explicitly out of scope for this story.

**Never:**

- Do not touch these options objects — each owns a distinct persisted or presentational format: `EventStorePayloadProtectionMetadataCarrier.cs:42` (protection metadata stored beside protected records; re-casing breaks stored round-trips), `QueryCursorCodec.cs:23`, `Admin.Cli/Formatting/JsonDefaults.cs`, any `Admin.*` or `ReadModelBatch*` options, and Dapr-owned `DaprClient.JsonSerializerOptions`.
- Do not change `RestApiControllerEmitter.cs:236`. It already emits bare Web options, so it is behaviourally identical to the shared object; touching it forces generated-controller churn for no behaviour change.
- Do not change the pub/sub subscription registry (`EventStoreDomainEventsServiceCollectionExtensions.cs:102-105`) or `EventStoreDomainEventProcessor`'s unknown-type drop policy. That registry is FQN-only with no suffix hazard.
- Do not normalize `EventStoreProjection.cs:204-211`'s bare `InvalidOperationException`, and do not fix the silent skip at `EventStoreProjection.cs:89-92`. Both are real but out of scope.
- No new package, no `Guid.TryParse`/`Guid.NewGuid()` for EventStore identifiers, no payload contents in logs or exception messages (type names only).
- Do not touch projection **handler** routing (`DomainProjectionDispatcher`) or the stable dispatch id — verified free of type-name matching.
- **Do not change either payload writer**: `src/Hexalith.EventStore.Server/Events/EventPersister.cs:71` or `src/Hexalith.EventStore.Contracts/Results/DomainServiceWireResult.cs:29`. Do not change `src/Hexalith.EventStore.Testing/Fakes/FakeEventPersister.cs:61` either — it must keep matching the writers it fakes.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|----------|--------------|---------------------------|----------------|
| Exact full-name hit | Stored `A.ItemAdded`; state has `Apply(A.ItemAdded)` | Binds `Apply(ItemAdded)` via the FQN key; no suffix scan runs | N/A |
| Suffix collision | Stored `A.SubItemAdded`; state has `Apply(ItemAdded)` **and** `Apply(SubItemAdded)` | Binds `Apply(SubItemAdded)` | N/A |
| Unanchored near-miss | Stored `A.SubItemAdded`; state has only `Apply(ItemAdded)` | No match — `SubItemAdded` does not end with `.ItemAdded` | Path's existing not-found behavior |
| Legacy short name | Stored `ItemAdded` (no namespace); one `Apply(ItemAdded)` | Binds it — short-name compat retained | N/A |
| Ambiguous short name | Stored `ItemAdded`; state has `Apply(A.ItemAdded)` and `Apply(B.ItemAdded)` | Throws | Typed ambiguity exception listing both candidate full names |
| camelCase payload | `{"name":"x"}` bound to a record with property `Name` | `Name == "x"` on command, rehydrate, project and pub/sub paths | N/A |
| PascalCase payload at rest | `{"Name":"x"}` (what both writers still emit) | `Name == "x"` on all four paths | N/A |
| Nested-type stored name | Stored `A.Order+ItemAdded`; state has `Apply(ItemAdded)` | Binds it — `+` is a type-nesting boundary, same as `.` | N/A |
| Assembly-qualified stored name | Stored `A.ItemAdded, MyAsm, Version=1.0.0.0` | Binds `Apply(ItemAdded)` — assembly qualification stripped before matching | N/A |
| Two candidates, different depth | Stored `X.B.A.Foo`; state has `Apply(A.Foo)` and `Apply(B.A.Foo)` | Binds `Apply(B.A.Foo)` — longest anchored match wins | N/A |
| Two candidates, same suffix key | Stored `Outer.Foo`; state has `Apply(EsFixA.Foo)` and `Apply(EsFixB.Foo)` (same short/suffix key `Foo`) | Throws | Typed ambiguity — two types share the anchored suffix key (distinct equal-length suffix keys cannot both anchor the same stored name) |

</frozen-after-approval>

## Code Map

Verified at `bb94d93e`. Both defects confirmed still fully open — `DomainProcessorStateRehydrator.cs` last changed 2026-05-20, and the commits touching the other files since 2026-07-04 changed markers, ULID validation and cancellation, not resolution or serialization.

**Resolver copy 1 — primary change site**

- `src/Hexalith.EventStore.Client/Handlers/DomainProcessorStateRehydrator.cs`
  - `:15-36` `DiscoverApplyMethods` — `ConcurrentDictionary<Type, Dictionary<string, MethodInfo>>` cache at `:13`; inner dict is `StringComparer.Ordinal`, keys **short names only** at `:31-32`.
  - `:327-340` `TryResolveApplyMethod` — hazard is `eventTypeName.EndsWith(kvp.Key, StringComparison.Ordinal)` at `:332`, first-match `break` at `:334`.
  - `:322-326` XML doc already claims a "single-candidate `EndsWith` fallback" — **factually false today**; fix it.
  - `:343` `internal static JsonSerializerOptions SerializerOptions { get; } = new(JsonSerializerDefaults.Web);` — the promotion candidate. Its doc already claims cross-path consistency that does not exist.

**Resolver copy 2 — same change site**

- `src/Hexalith.EventStore.Client/Aggregates/EventStoreProjection.cs`
  - `:165-188` `GetOrBuildApplyMethods` — `_applyCache` at `:23`, short names only at `:183-184`.
  - `:195-202` — the "duplicate" is **not** a method; it is a character-identical inline block inside `private static void ApplyEventByName(...)`, hazard at `:197`, `break` at `:199`. (The planning record calls it a duplicate method; it is not.)

**Reuse — mirror these, do not reinvent**

- `src/Hexalith.EventStore.Client/Aggregates/EventStoreAggregate.cs` — the *command* resolver, already correct. `:223-231` `ExtractShortTypeName` does a real boundary split; `:124-128`/`:139-144` already throw on colliding keys; `:137-147` already registers a second alias key. Read-only.
- `src/Hexalith.EventStore.Client/Aggregates/MissingApplyMethodException.cs:20-50` — shape for the new sibling exception (carries `StateType`/`EventTypeName`/`MessageId`/`AggregateId`).

**Why exact match always misses (read-only evidence)**

- `src/Hexalith.EventStore.Server/Events/EventPersister.cs:66-68` stores `GetType().FullName ?? Name`; same at `DomainServices/DaprDomainServiceInvoker.cs:383`. `src/Hexalith.EventStore.DomainService/AdminOperationalIndexMetadata.cs:148-164` confirms FQN is the system-wide event-type vocabulary.

**The two payload WRITERS — read-only, do not touch (loop-1 correction)**

The first attempt converted `EventPersister.cs:71` believing it was the only writer. It is not, and it is not even the one that runs in the deployed topology:

- `src/Hexalith.EventStore.Contracts/Results/DomainServiceWireResult.cs:29` — `SerializeToUtf8Bytes(payload, payload.GetType())`, **no options**. Called by `src/Hexalith.EventStore.DomainService/DomainServiceRequestRouter.cs:49,56` for every domain-service result.
- `src/Hexalith.EventStore.Server/DomainServices/DaprDomainServiceInvoker.cs:192-198` wraps those bytes as `SerializedEventPayload` (`:385`). `DaprDomainServiceInvoker` is the **only** registered `IDomainServiceInvoker` (`Server/Configuration/ServiceCollectionExtensions.cs:61`).
- `src/Hexalith.EventStore.Server/Events/EventPersister.cs:70-71` therefore takes the `serialized.PayloadBytes` **pass-through** branch on the real path — anything done to the `else` branch at `:71` never runs in the deployed topology.
- `src/Hexalith.EventStore.Testing/Fakes/FakeEventPersister.cs:61` — third default-options writer; must keep matching the writers it fakes.

Net: both writers emit PascalCase today and keep doing so. Readers must therefore be case-insensitive — which is exactly the fix.

**Serializer change sites — payload binds using `JsonSerializerOptions.Default`**

- `src/Hexalith.EventStore.Client/Aggregates/EventStoreAggregate.cs:195` (command; the live empty-payload bug — `Deserialize` returns non-null so the `?? throw` at `:196` never fires)
- `src/Hexalith.EventStore.Client/Aggregates/EventStoreProjection.cs:216` and `:226` (project — same file already reads camelCase envelope keys at `:138`/`:215`)
- `src/Hexalith.EventStore.Client/Subscriptions/EventStoreDomainEventProcessor.cs:147` (pub/sub)
(Writers are excluded — see the writers block above.)

**Already correct — verify only**

- `src/Hexalith.EventStore.Client/Aggregates/AggregateReplayer.cs:29,148,213` and `DomainProcessorStateRehydrator.cs:55,98,132,240,280,283,299` already flow the Web options. These are the *only* current consumers.

**Tests that constrain the change**

- `tests/Hexalith.EventStore.Client.Tests/Aggregates/EventStoreAggregateTests.cs:808-825` and `tests/Hexalith.EventStore.Client.Tests/Aggregates/EventStoreProjectionTests.cs:195-211` assert suffix matching **succeeds** for `"MyNamespace.ItemAdded"`. Both inputs have a real `.` boundary, so both must stay green unchanged — treat any need to edit them as a signal the rule was implemented wrong.
- Casing lock-ins that must not regress: `tests/Hexalith.EventStore.Client.Tests/Aggregates/AggregateReplayerTests.cs:110,142` (camelCase `count`/`isTerminated`), `tests/Hexalith.EventStore.Server.Tests/DomainServices/DaprSerializationRoundTripTests.cs:42-52`, `tests/Hexalith.EventStore.Contracts.Tests/Commands/SubmitCommandResponseTests.cs:20`.
- Reusable collision fixtures already exist: `WidgetCreated`/`GadgetCreated` at `tests/Hexalith.EventStore.DomainService.Tests/Fixtures/WidgetDomain.cs:21,24`; `TestEvent`/`LargeTestEvent` at `tests/Hexalith.EventStore.Server.Tests/DomainServices/DaprDomainServiceInvokerTests.cs:664,666`. **No test anywhere covers a suffix collision** — that is the gap.
- Conventions (verified): xUnit v3 with implicit `using Xunit`, **Shouldly only** (`EventStoreAggregateTests.cs` has 110 raw `Assert.` calls — do not copy that), NSubstitute, **bare `await`** (CA2007 suppressed at `tests/Directory.Build.props:10`), `Member_Scenario_Expectation` naming.

## Tasks & Acceptance

**Execution:**

- [x] `src/Hexalith.EventStore.Client/Aggregates/ApplyMethodResolver.cs` -- NEW `internal static` class owning registration + resolution -- one implementation both call sites share, so a third copy cannot drift back in. It must: register each event type under **both** `FullName` and `Name`; resolve exact FQN → exact short name → **longest** anchored suffix; treat both `.` and `+` as name boundaries (`+` is how `Type.FullName` renders nested types, which the old unanchored `EndsWith` matched); strip assembly qualification before matching, mirroring `EventStoreAggregate.ExtractShortTypeName:223-231`; skip generic-definition and by-ref `Apply` overloads so a key of `T` cannot be registered; and fail loudly rather than last-writer-wins when two discovered methods claim the same `FullName` (a base plus a `new`-hiding override does this).
- [x] `src/Hexalith.EventStore.Client/Aggregates/AmbiguousApplyMethodException.cs` -- NEW public exception mirroring `MissingApplyMethodException` -- turns the silent wrong-bind into a diagnosable failure. De-duplicate and ordinally sort the candidate names so the message is byte-stable across runs.
- [x] `src/Hexalith.EventStore.Client/Handlers/DomainProcessorStateRehydrator.cs` -- adopt the shared resolver; delete `TryResolveApplyMethod`; correct the false XML doc at `:322-326`; forward `SerializerOptions` to the shared Contracts object -- removes the always-missing exact lookup.
- [x] `src/Hexalith.EventStore.Client/Aggregates/EventStoreProjection.cs` -- adopt the shared resolver in place of the inline block at `:195-202`; route `:216`/`:226` through the shared options -- kills the copy-paste and the case-sensitive project-path bind.
- [x] `src/Hexalith.EventStore.Contracts/Serialization/EventStorePayloadSerialization.cs` -- NEW `public static JsonSerializerOptions Options` = `new(JsonSerializerDefaults.Web)`, made read-only in the initializer -- Contracts is the only home all reader paths already reference (`Server → Client → Contracts`); read-only prevents drift by mutation.
- [x] `src/Hexalith.EventStore.Client/Aggregates/EventStoreAggregate.cs` -- pass the shared options at `:195` -- fixes the live empty-command-payload bug.
- [x] `src/Hexalith.EventStore.Client/Subscriptions/EventStoreDomainEventProcessor.cs` -- pass the shared options at `:147` -- last case-sensitive reader.
- [x] `src/Hexalith.EventStore.Client/Aggregates/AggregateReplayer.cs` -- catch `AmbiguousApplyMethodException` around the resolve call and map it to `AggregateReconstructionResult.Failed(...)` -- `Replay` returns a categorized result by contract; letting the exception escape turns a diagnosable failure into a 500 on the replay endpoint. The resolver still throws; only this path translates.
- [x] `tests/Hexalith.EventStore.Client.Tests/Aggregates/ApplyMethodResolverTests.cs` -- NEW; cover **every** I/O Matrix resolution row on both the rehydrate and projection paths -- the CP-9 acceptance test that has never existed. Include the ambiguity-symmetry case: the runtime-type entry point must not return `null` where the name-based one throws, or `EventStoreProjection.Project` silently drops the event.
- [x] `tests/Hexalith.EventStore.Client.Tests/Serialization/PayloadSerializationConsistencyTests.cs` -- NEW; camelCase and PascalCase bind correctly on all four reader paths, plus a source guardrail that every reader names the shared instance -- see Design Notes for the four ways the first attempt's guardrail was vacuous. It must fail if any reader is reverted to default options.

**Acceptance Criteria:**

- Given two event types where one CLR short name is a suffix of the other, when either is replayed by its stored full name on the aggregate or projection path, then the correct `Apply` runs and the result does not depend on dictionary enumeration order.
- Given an event whose short name maps to two candidate `Apply` methods, when resolution runs, then an `AmbiguousApplyMethodException` naming both candidates is thrown rather than one being chosen.
- Given the existing suffix-fallback tests at `EventStoreAggregateTests.cs:808-825` and `EventStoreProjectionTests.cs:195-211`, when the suite runs, then both pass **unmodified**.
- Given a payload serialized in either camelCase or PascalCase, when it is bound on the command, rehydrate, project or pub/sub path, then every property populates and no path yields a default-constructed object.
- Given the whole change, when `grep -rn "EndsWith" src/Hexalith.EventStore.Client/` runs, then the only event-type suffix comparison in the project is inside `ApplyMethodResolver.cs` and it is boundary-anchored. Checking only the two original files is **not** sufficient — the logic moves, so a grep scoped to where it used to live would pass vacuously.
- Given both writers are out of scope, when the change is complete, then `git diff` reports no modification to `EventPersister.cs`, `DomainServiceWireResult.cs`, or `FakeEventPersister.cs`.

## Spec Change Log

### 1 — 2026-08-07, loop 1 (`bad_spec`)

**Trigger.** All three review layers independently found that the Code Map's writer inventory was wrong. `EventPersister.cs:70-71` only serializes the *non-*`ISerializedEventPayload` branch; on the deployed DAPR topology payloads are already serialized upstream at `DomainServiceWireResult.cs:29` with no options and persisted verbatim. Converting `EventPersister.cs:71` therefore produced **two casings at rest in one stream** (in-process camelCase, domain-service PascalCase) while the frozen text claimed casing changed uniformly.

**Amended.** Scope narrowed to **readers only** by human decision, superseding the earlier "convert the writer" approval, which had been given on the incomplete one-writer inventory. Both writers and the fake are now in **Never**. The writer inventory was added to the Code Map. Resolution semantics gained the cases review exposed (nested `+`, assembly-qualified names, longest-match, duplicate `FullName`, generic/by-ref overloads). `AggregateReplayer` now translates ambiguity to a categorized failure. AC5 was rewritten because it had become vacuous once the suffix logic moved out of the two files it greps.

**Known-bad state avoided.** A mixed-casing event stream, plus a green build whose central claim — one shared definition across all payload paths — was false on the only topology that actually runs.

**Frozen-block changes, human-authorised 2026-08-07.** The writer-scope narrowing and the four added I/O Matrix rows (nested `+` boundary, assembly-qualified stripping, longest-anchored-match wins, equal-length tie throws) were both put to the human and ratified. No other frozen content changed.

**KEEP (must survive re-derivation).**

- The single shared resolver collapsing both copies. Resolution order exact FQN → exact short → anchored suffix was right.
- `AmbiguousApplyMethodException` mirroring `MissingApplyMethodException`'s shape and carrying `StateType`/`EventTypeName`/`MessageId`/`AggregateId`.
- The `ApplyMethodResolverTests` structure: per-path coverage across the rehydrate, projection and aggregate entry points, driven off the I/O Matrix rows.
- `EventStoreAggregateTests.cs:808-825`, `EventStoreProjectionTests.cs:195-211` and `AggregateReplayerTests.cs` stayed byte-identical. Keep that.
- Mutation-checking new guards before declaring them green (removing the `.` anchor must fail tests; dropping an options argument must fail the guardrail). Keep that practice and report it.

### 2 — 2026-08-08, code-review loop (`frozen Always/Intent alignment`)

**Trigger.** Review found Always and Intent still described only a `.`-anchored suffix with no longest-match rule, while the ratified I/O matrix and `ApplyMethodResolver` already used `.`/`+` boundaries and longest-anchored wins.

**Amended.** Human chose to amend frozen Always and Intent to match the matrix/resolver. Design Notes sketch, equal-depth matrix wording, and related test labels were corrected in the same pass. `AggregateReplayer` now forwards `request.AggregateId` into ambiguity diagnostics.

## Design Notes

Registration rule — FQN keys are unique by construction, so they always register. A short name registers only while unambiguous; the second type claiming the same short name marks it ambiguous instead of overwriting (today's silent last-writer-wins). Ambiguity is then reported at *resolution* time, not registration time, so an aggregate whose events collide only by short name still works when addressed by full name.

```csharp
// resolve order — exact FQN → exact short → longest boundary-anchored suffix ('.' or '+')
// SuffixKeys is ordered longest-first; the first anchored hit is the unique longest match.
// Multi-candidate sets under that key (or under an exact key) throw AmbiguousApplyMethodException.
if (byFullName.TryGetValue(name, out ApplyMethodCandidates? exact)) { return Single(exact); }
if (byShortName.TryGetValue(name, out ApplyMethodCandidates? shortHit)) { return Single(shortHit); }
foreach (string key in suffixKeysLongestFirst) {
    if (IsBoundaryAnchoredSuffix(name, key)) {   // preceding char is '.' or '+'
        return Single(GetSuffixCandidates(key));
    }
}
```

Readers only widen — case-insensitive accepts both casings — so no sequencing hazard exists and no persisted or wire byte changes. That is the whole reason the writers stay untouched.

**The source guardrail must not repeat loop 1's four failures.** It was written to catch a fifth path drifting later, and caught nothing:

1. Its predicate was `arguments.Contains("SerializerOptions")` — and the literal `JsonSerializerOptions.Default`, the exact defect this story fixes, *contains* that substring. Match `EventStorePayloadSerialization.Options` explicitly.
2. Its coverage control was `inspectedCalls >= fileCount`, satisfiable by one file contributing every call. Assert **per file** that each yields at least one inspected call.
3. Its token list missed the non-generic `.Deserialize(` form and `JsonSerializer.Serialize(`, while `.Deserialize<` double-counted the generic form.
4. Its balanced-paren scanner was neither string- nor comment-aware, so a parenthesis inside a literal or an XML `<see cref="JsonSerializer.Deserialize"/>` desynced it.

A guardrail that cannot fail is worse than none — it reports safety it does not provide. Before declaring it green, break each reader deliberately and confirm the guardrail goes red.

## Verification

**Commands:**

- `dotnet build Hexalith.EventStore.slnx --configuration Release -p:UseHexalithProjectReferences=false` -- expected: 0 warnings, 0 errors (`TreatWarningsAsErrors=true`).
- `dotnet test tests/Hexalith.EventStore.Client.Tests/Hexalith.EventStore.Client.Tests.csproj --configuration Release -p:UseHexalithProjectReferences=false` -- expected: all pass, including the two pre-existing suffix tests unmodified.
- `dotnet test tests/Hexalith.EventStore.Server.Tests/Hexalith.EventStore.Server.Tests.csproj --configuration Release -p:UseHexalithProjectReferences=false` -- expected: no regression vs. baseline; record exact pass/fail/skip.
- `dotnet test tests/Hexalith.EventStore.Contracts.Tests/Hexalith.EventStore.Contracts.Tests.csproj --configuration Release -p:UseHexalithProjectReferences=false` -- expected: casing round-trip assertions still pass.
- `dotnet test tests/Hexalith.EventStore.DomainService.Tests/Hexalith.EventStore.DomainService.Tests.csproj --configuration Release -p:UseHexalithProjectReferences=false` -- expected: projection dispatch unaffected.
- `dotnet test tests/Hexalith.EventStore.Sample.Tests/Hexalith.EventStore.Sample.Tests.csproj --configuration Release -p:UseHexalithProjectReferences=false` -- expected: end-to-end Counter replay unaffected.

Run test projects individually — never solution-level `dotnet test`. `--filter FullyQualifiedName~<Class>` works (VSTest bridge) for the inner loop.

**Manual checks (if no CLI):**

- Confirm exactly one `EndsWith`-based apply resolver remains in `src/Hexalith.EventStore.Client` and that both former sites call it.

## Suggested Review Order

**Resolution semantics — start here**

- Entry point: the whole resolution contract in one place, exact → short → anchored suffix.
  [`ApplyMethodResolver.cs:134`](../../src/Hexalith.EventStore.Client/Aggregates/ApplyMethodResolver.cs#L134)

- The only event-type suffix comparison left in the project; `.` and `+` both count as boundaries.
  [`ApplyMethodResolver.cs:205`](../../src/Hexalith.EventStore.Client/Aggregates/ApplyMethodResolver.cs#L205)

- Bracket-depth-aware; applied to registered key and stored name alike so matching stays consistent.
  [`ApplyMethodResolver.cs:228`](../../src/Hexalith.EventStore.Client/Aggregates/ApplyMethodResolver.cs#L228)

- Runtime-type overload; exact CLR identity first, then a strict assignability guard on any name fallback.
  [`ApplyMethodResolver.cs:179`](../../src/Hexalith.EventStore.Client/Aggregates/ApplyMethodResolver.cs#L179)

- Registers FQN and short name; skips generic-definition and by-ref overloads.
  [`ApplyMethodResolver.cs:60`](../../src/Hexalith.EventStore.Client/Aggregates/ApplyMethodResolver.cs#L60)

- One suffix registry populated unconditionally, so a lookup cannot disagree with itself.
  [`ApplyMethodTable.cs:59`](../../src/Hexalith.EventStore.Client/Aggregates/ApplyMethodTable.cs#L59)

- Ambiguity is a hard failure naming every candidate; de-duplicated and ordinally sorted.
  [`AmbiguousApplyMethodException.cs:89`](../../src/Hexalith.EventStore.Client/Aggregates/AmbiguousApplyMethodException.cs#L89)

**Call sites that lost their private copy**

- Replaced the character-identical inline `EndsWith` block that used to live here.
  [`EventStoreProjection.cs:175`](../../src/Hexalith.EventStore.Client/Aggregates/EventStoreProjection.cs#L175)

- Discovery now forwards to the shared table; the old cache and resolver are gone.
  [`DomainProcessorStateRehydrator.cs:20`](../../src/Hexalith.EventStore.Client/Handlers/DomainProcessorStateRehydrator.cs#L20)

- Envelope path feeds message and aggregate identity into the ambiguity diagnostic.
  [`DomainProcessorStateRehydrator.cs:215`](../../src/Hexalith.EventStore.Client/Handlers/DomainProcessorStateRehydrator.cs#L215)

- Ambiguity becomes a categorized failure; `Replay` returns a result by contract, never throws.
  [`AggregateReplayer.cs:125`](../../src/Hexalith.EventStore.Client/Aggregates/AggregateReplayer.cs#L125)

**Shared payload options — readers only**

- New public seam in Contracts, frozen at construction so it cannot drift by mutation.
  [`EventStorePayloadSerialization.cs:34`](../../src/Hexalith.EventStore.Contracts/Serialization/EventStorePayloadSerialization.cs#L34)

- The live bug: a camelCase command payload used to bind zero properties and not throw.
  [`EventStoreAggregate.cs:196`](../../src/Hexalith.EventStore.Client/Aggregates/EventStoreAggregate.cs#L196)

- Last case-sensitive reader on the pub/sub path.
  [`EventStoreDomainEventProcessor.cs:148`](../../src/Hexalith.EventStore.Client/Subscriptions/EventStoreDomainEventProcessor.cs#L148)

**Tests and docs**

- Every I/O Matrix row, exercised on the resolver, rehydrate, projection and replay paths.
  [`ApplyMethodResolverTests.cs:1`](../../tests/Hexalith.EventStore.Client.Tests/Aggregates/ApplyMethodResolverTests.cs#L1)

- Casing theories per path, plus a source guardrail with nine self-tests for its own scanner.
  [`PayloadSerializationConsistencyTests.cs:1`](../../tests/Hexalith.EventStore.Client.Tests/Serialization/PayloadSerializationConsistencyTests.cs#L1)

- Resolution order and operator remediation, replacing the stale "EndsWith / exact" claim.
  [`event-versioning.md:137`](../../docs/concepts/event-versioning.md#L137)

### Review Findings

- [x] [Review][Patch] Amend frozen Always/Intent to match ratified matrix (`.`/`+` boundaries + longest-anchored wins) [`_bmad-output/implementation-artifacts/spec-4-3-deterministic-replay-dispatch-and-serialization.md`] — decided: amend
- [x] [Review][Patch] Replay ambiguity resolve omits AggregateId [`src/Hexalith.EventStore.Client/Aggregates/AggregateReplayer.cs:125`]
- [x] [Review][Patch] Design Notes resolve sketch still shows `.`-only EndsWith with no longest-key selection [`_bmad-output/implementation-artifacts/spec-4-3-deterministic-replay-dispatch-and-serialization.md:171`]
- [x] [Review][Patch] Equal-depth matrix row / test label claim a longest-match length tie that the resolver asserts is unreachable; coverage is short-name/suffix-key ambiguity under `Foo` [`tests/Hexalith.EventStore.Client.Tests/Aggregates/ApplyMethodResolverTests.cs:312`]
- [x] [Review][Patch] PayloadSerializationConsistencyTests section still says “all four reader paths” while the guardrail treats AggregateReplayer as a fifth [`tests/Hexalith.EventStore.Client.Tests/Serialization/PayloadSerializationConsistencyTests.cs`]
- [x] [Review][Defer] Typed rehydrate MissingApplyMethodException uses short CLR name [`src/Hexalith.EventStore.Client/Handlers/DomainProcessorStateRehydrator.cs:192`] — deferred, pre-existing
