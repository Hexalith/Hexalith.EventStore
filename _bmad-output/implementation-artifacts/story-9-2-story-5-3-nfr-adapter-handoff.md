# Story 9.2: Story 5.3 NFR adapter handoff

This bounded delivery concerns the first story-owned completion adapter. It grants no live completion, all-host NFR3 closure, whole-NFR closure, readiness or parent Story 9.2 completion. Historical Story 5.3 labels remain historical. Parent Story 9.2 remains `in-progress`.

## Source and pre-freeze investigation

The canonical source revision observed before implementation is `eecc0e4da70a0d64aaeff4fb75cdc06f94eb7563`. The workspace already contained the uncommitted completion-guard slice; those edits are preserved. The new adapter implementation is also uncommitted. A revision identifier alone therefore does not identify the worktree bytes. Final verification records the new adapter/control input digests separately.

Authoritative scope: PRD §11.2 maps Story 5.3 to NFR3 and NFR4; its NFR3 row limits 5.3 evidence to the bound hosts. The original spec binds the EventStore gateway, Admin Server Host, sample API, relevant UI token channels and local authentication composition. Tenants/generated/future JWT-host conformance remains Story 5.11. PRD NFR3 and the existing shared contract require Production to reject symmetric mode even when the override is set; the older Story 5.3 epics wording permits the override more broadly. This delivery retains the stronger implemented posture and records the wording discrepancy.

The old spec's review pass 8 explicitly deferred scanner defects. Source inspection before freezing this slice confirmed YAML credential sequences are skipped, credential-constructor matching is qualification/type incomplete, XML elements omit CDATA, and space-separated CLI secret flags and private octet JWK material lack recognition. There is no NFR4 waiver. Existing green tests and historical `done` cannot replace executable checks for those gaps.

## Independent genuine business checks

All commands ran under `run-with-temp`; disposable copies, root-declared dependency copies and build outputs were beneath its private `$TMPDIR`. Nothing initialized or updated a submodule, and no nested dependency was copied by recursive Git operations. Useful logs and reproducible local scripts remain ignored scratch in `_bmad-output/implementation-artifacts/evidence/story-9-2-story-5-3-adapter/`.

Each project build used these exact arguments, with `PROJECT` replaced by its owning test project path:

```bash
dotnet build PROJECT --configuration Release -m:1 -p:UseNuGetDeps=true -p:NuGetAudit=false -p:MinVerVersionOverride=1.0.0
```

The five builds succeeded with zero warnings/errors. These are focused package-mode checks using the pinned dependency versions, not the full unmodified Release gate. The audit and MinVer environment pins do not change repository configuration.

Each compiled test assembly ran with `dotnet ASSEMBLY -noLogo -noColor` followed by fixed repeated `-class` arguments:

| Assembly / classes | Result |
| --- | --- |
| Admin.Server.Host.Tests: HostBootstrapTests | 34 passed, zero failures/skips |
| Server.Tests: Authentication.EventStoreAuthenticationOptionsTests, Authentication.ConfigureJwtBearerOptionsTests, Authentication.ProductionAuthorityAuthenticationTests, HealthChecks.DefaultHealthEndpointResponseWriterTests, Security.SecretsProtectionTests | 347 total; 346 passed, one failed; zero skips |
| AppHost.Tests: Configuration.LocalAuthenticationCredentialsTests, Configuration.KeycloakRealmTemplateTests, Configuration.AppHostAuthenticationModelTests, Configuration.HexalithEventStoreJwtAuthenticationTests | 60 total; 56 passed, four failed because the initial isolated package-mode snapshot lacked Tenants host project sources; zero skips |
| Admin.UI.Tests: Services.AdminApiAccessTokenProviderRoleTests | 50 passed, zero failures/skips |
| Sample.Tests: SampleApi.SampleApiHostMiddlewareTests, BlazorUI.EventStoreApiAccessTokenProviderTests | 43 passed, zero failures/skips |

Class names above are prefixed with their assembly's full namespace, for example `Hexalith.EventStore.Server.Tests.Authentication.EventStoreAuthenticationOptionsTests`.

The server failure is `SecretsProtectionTests.TrackedReusableContent_DoesNotContainUsableSecrets`. It reports three existing fixture locations: `tests/Hexalith.EventStore.Testing.Integration.Tests/GeneratedApiSmokePreflightDiagnosticsTests.cs:126`, `tools/tests/test_phase_4_gate_decision.py:39`, and `tools/tests/test_phase_4_transition.py:750`. These are scanner findings requiring triage; this report does not assert that their synthetic values are operational credentials. They cannot be dismissed merely to produce a passing completion result.

The AppHost environment prerequisite was independently resolved by copying the existing root-declared Tenants tracked source into another private workspace. With those sources present, the complete `Configuration.AppHostAuthenticationModelTests` class passed **7/7**, including the four previously failing cases. This establishes only bound-host composition behavior. Any adapter execution using those sources must bind their inventory and pinned gitlink; it establishes no Tenants authentication conformance.

Exact orchestration commands and logs:

- `run-with-temp python3 -B _bmad-output/implementation-artifacts/evidence/story-9-2-story-5-3-adapter/verify-existing-lanes.py` — performs the four non-Admin-host rows above; all child work was awaited before scratch cleanup.
- `run-with-temp python3 -B _bmad-output/implementation-artifacts/evidence/story-9-2-story-5-3-adapter/verify-apphost-with-tenants.py` — AppHost model prerequisite rerun, 7/7.
- Initial Admin host check used an inline Python disposable-copy job with the exact build/test argv above; `admin-build.log` and `admin-host-tests.log` preserve the argv and 34/34 summary.

These are fresh business test executions. No synthetic GitHub approval or saved PASS report supplied their result. They do not establish the complete Story 5.3 denominator: full Release, integration/Keycloak and other original-spec channels remain independently required where the adapter declares them.

## Full diagnostic before review corrections

Exact command:

```bash
run-with-temp python3 -B tools/validate-story-5-3-nfr.py --logs _bmad-output/implementation-artifacts/evidence/story-9-2-story-5-3-adapter/validator-final-1
```

Exit **1**. The preserved business report is independently **FAIL**. Its SHA-256 is `5fd29e82a3b38a2e6583d8c8b4af3cdef4056b3c393fb4ee83c9ded31548a6f0`; its complete 19,228-file source inventory digest is `e379cb0474c5c383cd30cfdf0bf4a5a5f2d485a4960f8279da8d5d9e3197bbe0`. This standalone inventory has `root_tree: null` and confers no platform authority.

The scanner's actual positive assignment control detected the runtime credential. All six mandatory format probes failed detection: YAML sequence, qualified NetworkCredential, qualified ClientSecretCredential, XML CDATA, space-separated CLI secret and private octet JWK. The complete claim therefore fails independently of infrastructure.

| Fixed lane | Genuine result |
| --- | --- |
| Complete Admin host regression | 70/70 passed |
| Sample API/UI | 48/48 passed |
| AppHost credentials/model | 60/60 passed |
| Admin UI token channels | 52/52 passed |
| Load token | 6/6 passed |
| Smoke/preflight script | 56/56 passed |
| Packaging | 2 passed, 52 failed: scratch Git lacked repository origin/HEAD/history required by existing tests |
| Provider | 8 passed, 2 failed: omitted FrontComposer Pact JSON input |
| Integration | Runner exited before XML: private PATH omitted existing Dapr CLI |
| Server and full solution build | MSB3030: private primary cache omitted pinned Tenants.Contracts 7.0.0 files selected through PackageDownload |

The source stability check additionally refused `SOURCE_MISMATCH`: another process advanced HEAD to `97c02f7bf99a694528a2b15f0b3b0a6e5fb10e9d` and added `DaprReadModelStoreDecoratorTests.cs` to index membership during execution. This implementation made no Git mutation. The original implementation baseline remains unchanged; the external work is preserved. Saved business diagnostics cannot authorize the changed source.

Review corrections address the adapter-owned scratch/provenance/cache/path defects rather than mislabelling them as missing host infrastructure. In particular, the host already has `/usr/local/bin/dapr` and a 1.19.1 runtime. These earlier results are retained as investigation evidence and will be superseded by verification of the corrected adapter.

## Final corrected adapter verification

The complete fixed plan was rerun from a frozen disposable copy so unrelated worktree/index changes could not change an in-flight diagnostic. Exact command:

```bash
run-with-temp python3 -B _bmad-output/implementation-artifacts/evidence/story-9-2-story-5-3-adapter/run-frozen-validator.py
```

The retained local driver binds current source and tool inputs, materializes the verified private Git objects and inputs, then calls the isolated fixed entry point `python3 -I -B tools/validate-story-5-3-nfr.py --root PRIVATE_COPY --bound`. It awaits all child work. Its source is retained with the logs; no saved report supplies evidence. Output directory: `corrected-diagnostic-5/` under the ignored evidence directory above. The corresponding `corrected-diagnostic-5.log` preserves the exact result, counts, diagnostics and hashes.

Observed Git HEAD: `97c02f7bf99a694528a2b15f0b3b0a6e5fb10e9d`; original baseline: `eecc0e4da70a0d64aaeff4fb75cdc06f94eb7563`. This implementation is uncommitted. The standalone report has `root_tree: null` and supplies no live assurance, approval or seal.

- Canonical source/runtime inventory binding: `a05e58a5e1c97a334bde47ab788b9d2323fae7e1f4a3dfb159c5b9dfb8104d0d` (19,232 source files and 12,633 runtime input records).
- Saved expanded inventory file SHA-256: `ba333fc8e28549115861ba43e42b537f7d84b0dde3a7b24ec745d8361176602f`. This differs from the canonical binding because machine-specific runtime source paths are excluded from the canonical identity.
- Report SHA-256: `c6d6c2f2771506a75e217fabd9ffc95b9a665a38e42da30260dccf786141b638`.
- Fixed plan SHA-256: `b3fb8169b66b70b24248a8312b5a5b67c7ff773409437f115b0d758b68bdeea7`.
- Verified private package-input digest: `a1de836fb866ed291b243260cd8bde2e94bcc2bbd2d383d1816b1c241ff8c6f4`.
- Toolchain-input digest: `de0d197f32e813c21f0053d4a3b71eab5ef06481a0e35f4c94a463eb9510b1de`.

Pinned root-declared source inputs (no initialization/update was performed locally):

- `references/Hexalith.Builds`: `e2bbbd0893f586e4d447ef8358ff747ea3d34435`.
- `references/Hexalith.Tenants`: `7c1747f93fbe988ca8aaa87d636bb6a39563e6b8`.
- `references/Hexalith.FrontComposer`: `0d065fceb2bfca9999307cb864a3f93392fb3400`.

Exit **1**, business result **FAIL**, with the complete denominator retained. All nine selected project builds and the full Release solution build passed. These builds keep the explicit environment pins `NuGetAudit=false`, `MinVerVersionOverride=1.0.0`, `UseNuGetDeps=true`; they do not replace the unmodified repository CI gate.

| Fixed business lane | Final result |
| --- | --- |
| gateway-auth-probes-secrets | 346/347 passed; 1 failed; 0 skipped |
| admin-host-regression | 70/70 passed; 0 failed; 0 skipped |
| sample-api-ui | 48/48 passed; 0 failed; 0 skipped |
| apphost-credentials-model | 60/60 passed; 0 failed; 0 skipped |
| admin-ui-token-channels | 52/52 passed; 0 failed; 0 skipped |
| load-token | 6/6 passed; 0 failed; 0 skipped |
| packaging-auth-contract | 54/54 passed; 0 failed; 0 skipped |
| provider-ephemeral-token | 10/10 passed; 0 failed; 0 skipped |
| script-smoke-preflight | 56/56 passed; 0 failed; 0 skipped |
| integration-auth | No completed XML report; refused at its fixed execution/output bound |
| scanner-capabilities | One genuine positive detection; all six required historical formats missed; lane FAIL |

The integration log reached 1054660 bytes against the fixed 1 MiB bound. Captured support-safe warnings include `channel-token-missing` and `activation_authority_failed`. This is insufficient integration evidence, not a passing or narrowed lane. The SDK/Dapr binaries were present and bound; the earlier private PATH/provenance/cache/input defects have been corrected. The completed diagnostic's own nonpersistent integration container was removed after its recorded creator process exited and its name was confirmed in this run's log. Existing Dapr/MCP containers were preserved.

The tracked scan still reports the three original fixture findings: `tests/Hexalith.EventStore.Testing.Integration.Tests/GeneratedApiSmokePreflightDiagnosticsTests.cs:126`, `tools/tests/test_phase_4_gate_decision.py:39`, and `tools/tests/test_phase_4_transition.py:750`. These are the validator's emitted locations. The new adapter no longer adds its own ambiguous `KEY` finding: its canonical identity constant is explicitly named `STORY_IDENTITY`. No scanner rule, exclusion, coverage inventory or failing expectation was weakened.

Final targeted verification (**367 tests passed, zero skipped**, plus 18 explicit schema cases):

- `run-with-temp python3 -B -m unittest discover -s tools/tests -p 'test_phase_4_story_5_3.py' -v` — 23/23, including genuine source-linked scanner execution and genuine bound-validator failure/report forwarding.
- `run-with-temp python3 -B -m unittest discover -s tools/tests -p 'test_phase_4_story_completion.py' -v` — 32/32; both spec and tracker targets, missing/invalid approvals/seals, stale inputs and saved-report refusals.
- `run-with-temp python3 -B -m unittest discover -s tools/tests -p 'test_phase_4_transition.py' -v` — 38/38. The final shared transition module matches the module that passed this run byte-for-byte; predecessor, approval and completed-run controls retain their meaning.
- `run-with-temp python3 -B -m unittest discover -s tools/tests -p 'test_phase_4_gate_decision.py' -v` — 16/16.
- `run-with-temp python3 -B -m unittest discover -s tools/tests -p 'test_phase_4_gate_risk_matrix.py' -v` — 147/147.
- `run-with-temp python3 -B -m unittest discover -s tools/tests -p 'test_oq8_governing_design.py' -v` — 111/111.
- `run-with-temp python3 -B tools/tests/check_phase_4_story_5_3_schema.py` — 18/18 using already-installed jsonschema. Missing tooling fails explicitly; no new dependency was installed. Contract fixtures never confer business authority.
- `run-with-temp actionlint .github/workflows/phase-4-high-risk-nfr-story-done.yml .github/workflows/phase-4-transition-fixtures.yml` — passed.
- `git diff --check` — passed.

Three review lenses completed; every finding is triaged in the owning spec. One verification prerequisite remains deferred: a genuine successful guarded write with a concurrent business-source edit, for both targets. Add it before claiming any genuine complete positive consumption; today's missing NFR evidence cannot reach that branch, and synthetic business PASS is not an acceptable substitute. The final writer already rechecks the complete inventory immediately before replacement.

No delivery CI link, owner approval or live seal exists for this uncommitted work. Synthetic platform fixtures establish verifier behavior only. Dedicated workflow declarations now materialize exact root gitlinks and SDK prerequisites; missing Dapr/runtime prerequisites still refuse, and no workflow or settings mutation was dispatched.

## Recommended next slice

Repair the five known scanner grammar categories (six concrete probes) with runtime-generated rejection/control cases, triage the three tracked-scan findings, and complete the real authentication/Keycloak integration lane within its fixed bounds. Reconcile the older epics symmetric-override wording with the stronger PRD contract without weakening Production behavior. Add the deferred guarded concurrent-source-write tests once genuine complete evidence is available, and prepare the dedicated CI runtime prerequisites before obtaining a fresh completed run and time-separated owner approvals. Keep parent Story 9.2 `in-progress`; Tenants/generated/future-host conformance, other adapters, readiness and Stories 3.19/3.20 remain separately owned.

Final lifecycle verification: `python3 -B tools/validate-phase-4-story-completion.py --spec _bmad-output/implementation-artifacts/spec-9-2-story-5-3-nfr-completion-adapter.md --target spec --write` returned ordinary-slice PASS and wrote only this bounded slice label. The same command with `--target tracker` (without `--write`) refused parent completion with `BUSINESS_PREREQUISITE_MISSING`; parent 9.2 remains in-progress. `run-with-temp bash scripts/check-committed-files.sh` passed for 10,576 checked files. Protected files and all initially preserved tracker/customization/historical-spec bytes match the task-start snapshots. All verification children were awaited; no CI delivery or live assurance was created.
