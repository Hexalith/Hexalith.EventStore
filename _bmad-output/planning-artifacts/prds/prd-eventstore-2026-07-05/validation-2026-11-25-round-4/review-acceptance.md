# Product acceptance review · round 4

Reviewed 2026-11-25 on the working tree of EventStore `main` at `517fedfe1357e2157e17d1d8621637a31cd0e961`. [PRD](../../../../../_bmad-output/planning-artifacts/prd.md) SHA-256 `8ba4e8c044653fbcb17230d4886c9bef256f37ef81acc3cbb3bd27218fb20923`. [Epic plan](../../../../../_bmad-output/planning-artifacts/epics.md) SHA-256 `3297793cbc74cb9e4ec42c98fba5d87385c90ae1565ae2c2f2f43641dc7a3aee`. Both digests were recomputed and match. Line numbers refer to these working files.

## Verdict

**Pass, with one medium finding.** All three assigned round-3 findings are resolved. The wording was corrected by commit `8627d32cb` on 21 November, and today's edits did not regress it. The PRD, epic stories, frozen specs and shipped code now agree on three behaviors. Production refuses symmetric JWT signing with either flag value. An accepted generated command keeps HTTP 202 and `Retry-After: 1` and omits only an unusable `Location`. A tenant-scoped route requires exactly one canonical tenant, while a cataloged platform operation runs without a tenant. Today's FR26-C4 edit accurately records the Admin correlation defect. However, it assigns the fix to Story 5.4, which the tracker and spec record as done, and no selectable follow-up item exists, so FR26 has no schedulable path to closure. Two low findings concern acceptance text that is less precise than the shipped behavior it closes. Deferred Admin operations show no drift: FR34-C4, NFR15, §8.3 and Story 7.4 still agree.

## Disposition of round-3 findings in this lens

| Round-3 finding | Disposition | Current evidence |
| --- | --- | --- |
| H2 · Story 5.3 permits a Production configuration the PRD rejects | Resolved | Epic Story 5.3 at lines 4490–4492 says: "In Production, symmetric mode fails startup with either flag value". The override scenario at 4495–4498 is now "enabled in Staging". The criterion also agrees with PRD §6.5 at 345, NFR3 at 407, the glossary at 213 ("never reaches Production"), the epic inventory at 124 and [frozen spec 5.3, lines 33 and 42](../../../../../_bmad-output/implementation-artifacts/spec-5-3-production-authentication-guards-and-secret-stripping.md). Shipped validator: [JwtBearerAuthenticationContract.cs, lines 129–143](../../../../../src/Hexalith.EventStore.ServiceDefaults/Authentication/JwtBearerAuthenticationContract.cs). Gateway regressions at [EventStoreAuthenticationOptionsTests.cs, lines 159, 178 and 196](../../../../../tests/Hexalith.EventStore.Server.Tests/Authentication/EventStoreAuthenticationOptionsTests.cs) cover Production with the override, Staging with it and Staging without it, and they pass. |
| H4 · An accepted command can be reported to the caller as rejected | Resolved | The "safe rejection" wording is gone from the PRD (0 hits). UJ2 at 199 says: "The accepted response keeps HTTP 202 and `Retry-After: 1`". FR12 at 290 says to "ignore caller-supplied versions" and omits `Location` only for an unusable base or declaration or a missing version-valid `MessageId`. FR12-C3 at 447 and §8.2 at 501 agree. Epic Story 2.15 at 2184–2185 reads: "caller-supplied versions are ignored. A valid endpoint declaration and response identity still select the link". This matches [frozen spec 2.15, lines 31 and 39–45](../../../../../_bmad-output/implementation-artifacts/spec-2-15-messageid-only-command-status-identity.md) and the generator at [RestApiControllerEmitter.cs, lines 295–303](../../../../../src/Hexalith.EventStore.RestApi.Generators/RestApiControllerEmitter.cs), which sets `Retry-After` and returns `Accepted` whether or not `Location` is set. |
| M1 · The tenant instruction omits cataloged platform operations | Resolved | UJ2 at 199 begins "For a tenant-scoped route" and adds the platform path: "requires its declared permission and credential checks and rejects a supplied tenant". §8.2 at 500 says "On tenant-scoped routes" and "A cataloged platform operation admits its declared platform scope and credentials … and rejects any request tenant". The wording agrees with NFR2 at 406, FR12-C2 at 446, [spec 2.14, line 40](../../../../../_bmad-output/implementation-artifacts/spec-2-14-canonical-tenant-boundary.md), and the shipped filter at [GeneratedRouteCatalogFilter.cs, lines 83–94 and 163–170](../../../../../src/Hexalith.EventStore.ServiceDefaults/Routing/GeneratedRouteCatalogFilter.cs). That filter rejects mixed scopes and requires exactly one declared `eventstore:permission`. The gate text is the subject of low finding L1 below. |

Counts: 3 resolved, 0 partially resolved, 0 open.

## New findings

### Medium · FR26-C4's open correlation fix has no schedulable owner

**Location.** PRD FR26-C4 at 452; epics Story 5.4 reconciliation at 4535 and ledger at 356; [sprint tracker, line 208](../../../../../_bmad-output/implementation-artifacts/sprint-status.yaml); [spec 5.4, line 5](../../../../../_bmad-output/implementation-artifacts/spec-5-4-admin-surface-safety-hygiene.md); architecture line 602 (route: consistent with the PRD).

**What is wrong.** Today's edit keeps the clause open "until Story 5.4 binds the AD-32 host fix and a host-level regression". The same epic paragraph opens "Story 5.4 is done", the tracker records `5-4-admin-surface-safety-hygiene: done`, and the spec has `status: 'done'` with no correlation criterion. The update run decided to keep Story 5.4 done and route the fix as "a bounded FR26-C4 follow-up", but that follow-up exists only in the memlog. `deferred-work.md` has no entry for this middleware; DW-543 covers the gateway predicate and is done. Work selection reads the tracker and ledger, so no one picks up the fix, and FR26, an MVP requirement, cannot close. The defect is confirmed in the code: [CorrelationIdMiddleware.cs, lines 15–20](../../../../../src/Hexalith.EventStore.Admin.Server.Host/Middleware/CorrelationIdMiddleware.cs) accepts only `Guid.TryParse` values and otherwise mints `Guid.NewGuid()`, and the middleware is registered at [Program.cs, line 54](../../../../../src/Hexalith.EventStore.Admin.Server.Host/Program.cs). The existing host regression also pins the defect. [MiddlewareOrderTests.cs, line 41](../../../../../tests/Hexalith.EventStore.Admin.Server.Host.Tests/MiddlewareOrderTests.cs) asserts `Guid.TryParse(...).ShouldBeTrue()` on the response header and passes today.

**Correction.** Record the bounded follow-up once where work is selected, for example as one deferred-work entry naming the middleware, the AD-32 grammar and the replacement of `MiddlewareOrderTests.cs:41`. Point FR26-C4 and the Story 5.4 reconciliation to it in place of "Story 5.4 binds", and keep Story 5.4 done.

### Low · G-TENANT and Story 2.14 omit the platform-path refusals that UJ2 and §8.2 now require

**Location.** PRD G-TENANT at 739; epics Story 2.14 platform criterion at 2134–2137. Compare UJ2 at 199, §8.2 at 500 and [spec 2.14, line 40](../../../../../_bmad-output/implementation-artifacts/spec-2-14-canonical-tenant-boundary.md): "Reject mixed scopes or invalid credentials".

**What is wrong.** The corrected journey and constraint require a cataloged platform operation to pass its declared permission check and reject any request tenant. The G-TENANT evidence column asks only for "a distinct authenticated platform scope". Story 2.14's criterion asks only that "no path synthesizes or forwards `system`". A reviewer could therefore accept G-TENANT without the mixed-scope and missing-permission negatives. The impact is limited to closure evidence. The shipped filter enforces both refusals, and tests cover them: the platform `tenant-header` case in [GeneratedCatalogScopePreflightTests.cs, line 53](../../../../../tests/Hexalith.EventStore.Server.Tests/Routing/GeneratedCatalogScopePreflightTests.cs) and `missing-permission` in [PlatformCommandModelValidationTests.cs, line 135](../../../../../tests/Hexalith.EventStore.Server.Tests/Routing/PlatformCommandModelValidationTests.cs).

**Correction.** Add the following to the G-TENANT evidence column and Story 2.14's platform-operation criterion: "a supplied tenant or mixed scope and a missing declared permission are refused before downstream work".

### Low · Story 5.4's correlation criterion does not state the AD-32 grammar that FR26-C4 now names

**Location.** Epics Story 5.4 at 4554–4557; PRD FR26-C4 at 452 ("the AD-32 host fix"); architecture AD-32 at line 414 (route); [CorrelationIdGrammar.cs](../../../../../src/Hexalith.EventStore.Contracts/Identity/CorrelationIdGrammar.cs).

**What is wrong.** The criterion preserves "an accepted bounded non-whitespace correlation token under the documented compatibility rule". It lists "GUID-only legacy input" among inputs that are otherwise replaced. AD-32 accepts 1–128 ASCII alphanumeric or hyphen characters, preserves valid values downstream including ULIDs, and never validates correlation as a ULID or GUID. GUID text is therefore valid. "Non-whitespace" admits characters that AD-32 rejects. The GUID entry can be read as requiring replacement of values that the gateway's [CorrelationIdMiddleware](../../../../../src/Hexalith.EventStore/Middleware/CorrelationIdMiddleware.cs) preserves through `CorrelationIdGrammar`. Whoever implements the fix could therefore introduce a third grammar.

**Correction.** Restate the criterion as follows. Preserve exactly one 1–128-character ASCII alphanumeric-or-hyphen value, including ULID and GUID text, using `CorrelationIdGrammar`. Replace blank, duplicated, oversized or malformed input with a ULID-safe value.

## Mechanical checks

- **Digests and HEAD.** `sha256sum` of both working files matched the assigned digests. HEAD is `517fedfe1`. The working tree differs from HEAD only in `prd.md` (+6/−4 lines in five hunks), `epics.md`, `.memlog.md` and the 4.16 spec.
- **Definitions.** I parsed the 56 FR/NFR rows of the PRD table and the 56 lines of the epic inventory. All 56 are byte-identical, including FR12, FR26, FR34, NFR2, NFR3 and NFR15.
- **Clause rows in this lens.** FR4-C3, FR12-C2, FR12-C3, FR12-C4, FR26-C4 and FR34-C4 have the same primary and supporting stories in PRD §7.1 (lines 433–462) and the epic ledger (337–366). All six are `pending` in the epic ledger.
- **Provenance.** The diff of `8627d32cb` contains each of the four corrected phrases: the UJ2 rewrite, "In Production, symmetric mode fails startup", "caller-supplied versions are ignored" and "cataloged platform operation admits".
- **Residual wording scans.**
  - "safe rejection": 0 hits in the PRD. The epic hits at 4944 and 6823 concern other stories.
  - "exactly one tenant": PRD lines 199, 406 and 500 are all scoped to tenant routes. The only epic hit is the NFR2 inventory line.
  - Symmetric and break-glass wording: 4 PRD hits and 11 epic hits. All agree that Production refuses symmetric signing.
- **Lifecycle states.** The tracker records 2.14 `in-progress`, 2.15 `review`, 2.18 `backlog`, 5.3 `done`, 5.4 `done`, 5.11 `backlog` and 7.4 `backlog`. These match the PRD gate rows and epic reconciliations, except for the Story 5.4 ownership conflict in the medium finding.
- **Shipped behavior.** I ran the prebuilt Release test assemblies in place, with no rebuild. They were built at 22:09–22:15 today, after the last change to the relevant sources (23 November).
  - `RestApi.Generators.Tests` with `-method "*CommandStatus_*"`: 71 passed and 0 failed.
  - `Server.Tests` with `EventStoreAuthenticationOptionsTests` and `GeneratedCatalogScopePreflightTests`: 63 passed and 0 failed.
  - `Admin.Server.Host.Tests` with `MiddlewareOrderTests` and `AdminServerAuthenticationOptionsTests`: 8 passed and 0 failed. This includes the GUID assertion at `MiddlewareOrderTests.cs:41`.
- **Code reads.**
  - Generator accept path: emitter lines 295–303.
  - Platform and tenant scope: filter lines 83–121 and 163–170; emitter `ResolveTenant` at 428–478.
  - Declared-version source: filter lines 191–192.
  - Admin and gateway correlation middleware.
  - JWT validator branches: lines 129–143.
  - Admin host validator delegation to the shared contract.
- **Deferred Admin.** No drift found. The PRD (FR34-C4 at 462, NFR15 at 419, §8.3 at 514) agrees with Story 7.4 at 5866 and 5891–5899. The UI still renders "Submit Deferred Request" at [Backups.razor, lines 277 and 315](../../../../../src/Hexalith.EventStore.Admin.UI/Pages/Backups.razor) and [Compaction.razor, line 203](../../../../../src/Hexalith.EventStore.Admin.UI/Pages/Compaction.razor). This is consistent with the backlog status of Story 7.4 at 5872.
- **Observation, not a finding.** The Admin host option tests cover Production with and without the override but have no Staging positive case. The host delegates to the shared validator, and NFR3 assigns per-host negatives to Story 5.11 under G-AUTH-HOSTS. The PRD therefore makes no false claim.
