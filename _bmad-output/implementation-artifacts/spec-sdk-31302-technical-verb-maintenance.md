---
title: 'Publish the bounded technical-verb maintenance fix'
type: 'bugfix'
created: '2026-11-01'
status: 'in-review'
route: 'dispatch'
baseline_commit: '0a635322ac79b2a0aab0701ba4b604355ae3e0ba'
review_loop_iteration: 1
---

<frozen-after-approval reason="operator explicitly authorizes separate prerequisites, release and no human checkpoint">

## Intent

**Problem:** Public SDK 3.130.1 rejects Root's real POST command grants because technical topology validation treats GET descendants as reachable by POST. The qualified source cannot currently publish a maintenance patch without adopting unrelated feature main.

**Approach:** Validate captured method evidence before intersecting actual endpoint methods with grant verbs. Publish only the qualified source plus this correction as 3.130.2 through an exact protected `release/parties-sdk-3.130.2` maintenance branch and the existing production identity.

## Boundaries & Constraints

**Always:** Preserve all authentication, policy, digest, auxiliary binding, unknown/empty/duplicate-method and grant guards. Keep existing path semantics. Base the patch on qualified executor `0a635322ac79b2a0aab0701ba4b604355ae3e0ba`, whose production tree equals published source `1de42b706f52af0da6513f0b7663002606d17156`. Retain main controls and all normal checks. Add only exact maintenance branch protection and production deployment policy; retain reviewer and disabled administrator bypass. Maintenance publication requires current exact-source push CI, commitlint and full Integration. Verify every official archive and source after publication.

**Never:** Adopt Works/HA/resume/Ops feature main; modify original producer outputs; initialize nested submodules; invent witnesses or broaden Root grants; patch generated publisher scripts; accept arbitrary refs, reservations, stored NuGet keys or substitute qualified receipts. The canonical publisher permits only repository `Hexalith/Hexalith.EventStore`, branch `release/parties-sdk-3.130.2`, version `3.130.2`, production and `ci.yml`; other non-main tuples fail closed. Do not grant historical compatibility.

## I/O & Edge-Case Matrix

| State | Required result |
| --- | --- |
| Real POST command parent with GET status child | Both independently bound operations validate under their own verbs. |
| Empty, unknown, duplicate or contradictory captured methods | Refuse before filtering. |
| Reachable missing witness, foreign code/owner/policy | Refuse unchanged. |
| Current green exact maintenance source | Allow protected publication of only 3.130.2. |
| Other ref/version, stale head, wrong/missing source proof | Refuse before production or publication authority. |

</frozen-after-approval>

## Code Map

- `RouteCatalogTopologyValidator.ValidateTechnicalReceiver`: retain path reachability, but perform COMPLETE intrinsic validation for every path-reachable admitted receiver BEFORE HTTP verb intersection. Preserve HTTP owner/code/policy/anonymous/witness-to-auxiliary checks, NativeActor kind/method/verb/digest/auxiliary checks, DaprCallback kind/method/verb/digest/auxiliary checks, and existing legacy receiver guards. Only the current ACL-grant-to-HTTP-witness matching is per-grant after intersection. The old post-filter blocks are an explicit known-bad state, not a template.
- Each captured HTTP endpoint Methods must exactly equal its own Http.Verbs. DevelopmentTechnicalHttpTransport.Auxiliary intentionally aggregates separate admitted endpoints at the same exact path after checking operation/owner/code identity; therefore the COMPLETE union of captured methods from the entire receiver inventory for that exact path/operation/owner/code must exactly equal independently bound auxiliary verbs, independently of current-grant reachability. Preserve this genuine split-endpoint union semantics (GET and POST may be separate endpoints). A GET-only captured operation without its POST sibling cannot hide independently bound GET POST. Empty, unknown, duplicate and contradictory endpoint evidence still fails closed before exclusion. Add both a genuine split-endpoint positive and the absent-sibling subset refusal.
- Preserve real MVC/schema3 POST `/api/v1/commands` plus human GET status acceptance with existing `status/*` semantics. Do not falsely qualify the workload-status endpoint, import newer path-trie helpers, or change ACLs/witnesses.
- `/tmp/joint-blind-probe-_kfn13f3` and `/tmp/sdk132-edge-probe-_2zek4xz` retain actual review counterexamples. Durable controls must isolate the EXCLUDED child: POST-only grant with unrelated GET HTTP owner/code/policy/anonymous drift; NativeActor and DaprCallback contradictory methods and intrinsic evidence; complete-set subset drift. Included-root mutations or an additional GET grant cannot substitute for these cases.
- `/tmp/parties-topology-diagnostic-85ca5575870e` contains actual eight observations/activation/Probe. Preserve private inputs; use task-owned copies and candidate Contracts only. The before-fix gateway refusal and unchanged all-eight-valid correction remain required.
- `scripts/verify-release-source.sh` and `release_publication_fixture.py`: exact current branch proof, with DISTINCT actual main and finite-branch SHAs in both shell and wrapper fixtures. Unknown refs must refuse. A task-private mutation querying main instead of the selected branch must fail the finite-branch control while actual main remains valid.
- `tools/verify-release-candidate.mjs` (new): before protected production and OIDC, use actual pinned native commit-analyzer/core version semantics over real committed ancestry after verified reachable `v3.130.1`; require the real candidate to be patch `3.130.2`. Do not duplicate Conventional Commit parsing, invent a version/tag, invoke credential/publish lifecycle hooks, or request preproduction write permissions. Reject unsupported analyzer configuration, missing/unreachable/wrong base, no release, minor/major, wrong source or any other candidate. Tests use actual analyzer and owned real Git fixtures with distinct branch heads; synthetic fixtures are not live qualification.
- `release.yml`: checkout complete history and qualify the finite candidate in the read-only preproduction job before the production job can begin; keep exact source checks and revalidation before token and actual verify/publish. Main retains existing behavior. Preserve exact regular release ref ordering and both `channel:false`; document this finite branch's lifecycle and no arbitrary future branch/version authority.
- Canonical Builds is parent-owned at `/tmp/builds-eventstore-maintenance-publication`, baseline `22a578b576a515d2af214fe81859447fffc97981`, corrected pin `30ea43a56330a9828ed0bb2e837289231d8630d1`. Seven paths: `Github/publish-containers/{publication_preflight.py,publish-containers.sh,README.md,tests/test_publication_preflight.py,tests/test_publish_script_contract.py}`, `.github/workflows/eventstore-maintenance-publisher.yml`, and its spec. Do not edit this workspace or generated helpers. Exercise the ACTUAL canonical shell entrypoint as well as Python phases; its old baseline counterfactual must fail. Only shell and Python preflight execution bytes intentionally change; preserve the other four action/validator/smoke helper blobs exactly. Parent proof: `/tmp/builds-eventstore-maintenance-shell-counterfactual.json`; local wrapper probes use `HEXALITH_BUILDS_SOURCE` to read exact committed canonical bytes.
- Loop0 source/diffs/claims and positive/red evidence are archived at `/tmp/es31302-loop0-archive`; reuse KEEP behavior, never blindly restore the flawed old implementation.

## FileScope

- `src/Hexalith.EventStore.Contracts/Routing/RouteCatalogTopologyValidator.cs`
- `tests/Hexalith.EventStore.Server.Tests/Routing/TechnicalTopologyVerbIsolationTests.cs`
- `.github/workflows/{ci,commitlint,integration,codeql,dependency-review,advisory-tests,payload-protection,payload-protection-lifecycle,release}.yml`
- `.releaserc.json`
- `scripts/{verify-release-source,validate-publication-preflight}.sh`
- `tools/verify-release-candidate.mjs`
- `tools/tests/{test_release_source_branch,test_release_publication_authority,release_publication_fixture,test_release_candidate}.py`
- `tests/Hexalith.EventStore.Contracts.Tests/Packaging/{ReleasePackageManifestTests,TrustedPublishingReleaseTests,ContainerPublishingGovernanceTests}.cs`
- `docs/{ci,ci-secrets-checklist}.md`
- `_bmad-output/implementation-artifacts/deferred-work.md` (append only review deferrals)
- This spec; the seven separately owned canonical Builds paths above.

## Tasks & Acceptance

- [x] Rederive complete intrinsic-before-filter validation and exact method-set equality; add discriminating excluded-child controls for every receiver kind, retaining the real MVC positive.
- [x] Restore finite normal workflow triggers and immutable exact source checks; test distinct main/finite heads and an actual wrong-main-query mutation at shell and wrapper boundaries.
- [x] Qualify the actual native-analyzer candidate before production/OIDC with read-only permissions and complete history; test patch positive and no-release/minor/major/base/ref/configuration refusals without publication authority.
- [x] Pin the exact corrected canonical publisher; test real shell plus Python verify/publish behavior and retain four unchanged helper-blob identities, all destination/frozen-phase/production constraints.
- [x] Update executable packaging guards and finite-branch lifecycle docs without adopting broader feature main.
- [x] Run only affected local checks, unchanged eight-host probe and normal lint/hooks; freeze a new joint code/claims packet for three independent layers.

Given the unchanged actual eight-host capture, when corrected Contracts validates it, then all eight remain valid without changed ACLs or witnesses. Given an excluded receiver with malformed/unbound intrinsic evidence or a hidden method subset, when qualification runs, then it refuses before filtering. Given a candidate other than actual patch 3.130.2 or incomplete/wrong-branch proof, when preproduction qualification runs, then production and OIDC authority remain unavailable. Actual publication and subsequent public Root runtime proof remain delivery gates after review.

## Implementation Notes

Single cohesive goal: deliver the required qualified SDK correction. External branch/config/publication effects require dispatch review. Operator authorization supersedes human checkpoints; no user intent gaps remain. All fourteen official package indexes report 3.130.1 latest and 3.130.2 absent in `/tmp/es31302-maintenance-version-availability.json`; recheck before publication. Original Root inputs remain untouched. Deep search ran in `finite_verb_audit`; publisher inspection is parent-owned. This is implementation for the already rendered step-03 workflow: implement directly without re-rendering or delegating again. Local wrapper tests may set `HEXALITH_BUILDS_SOURCE=/tmp/builds-eventstore-maintenance-publication` to read the exact provisional publisher commit; do not alter that owner workspace. Use the existing independent SDK Builds snapshot a22 for compilation. Real captured Root probe inputs remain private outside the repository; copy only to task-owned scratch and reference candidate Contracts there, never original producer DLLs for mutation.

## Spec Change Log

- Parent-authorized ref correction: actual semantic-release 25.0.9 with real reachable tags rejects maintenance `3.130.x` as an empty range. Use exact regular release ref `release/parties-sdk-3.130.2` first, and explicit `channel:false` on both it and main. `/tmp/es31302-semantic-release-branch-proof.json` proves patch acceptance without an invented tag. Canonical publisher still permits only version 3.130.2.


### Review loop 1 — complete intrinsic and publication boundaries

All three layers completed; thirteen individual rows are retained below, with B9 refuted and twelve demonstrated outcomes routed to bad_spec. The nonfrozen plan omitted complete intrinsic-before-filter validation, full actual shell coverage, distinct live refs and real candidate qualification before authority. Amendments above prevent those known-bad states. KEEP: actual POST parent/GET child acceptance without ACL/witness changes; all original authentication/digest/policy/auxiliary/empty/unknown-method guards; all exact-source/CI/production/OIDC/destination/frozen-phase checks; finite regular ref release/parties-sdk-3.130.2 and version3.130.2 only; canonical owner changes only; no generated-script edits; all existing passing controls and actual8host unchanged bytes; original published1de and newer unrelated main preserved.


- Loop1 actual-source clarification: unchanged admin observations contain separately admitted method endpoints whose canonical auxiliary transport is their union; strict singleton equality refused a genuine host. The unchanged producer at DevelopmentTechnicalHttpTransport.Auxiliary lines82–95 grounds complete operation-union equality. KEEP every intrinsic endpoint check and the hidden-subset negative; do not broaden methods or modify observations.

- Author final check: the regression checkout of the corrected canonical publisher must fetch complete history because its actual-shell counterfactual reads baseline22a, three commits behind2e88. The CI checkout now uses depth0; no executable helper or source semantics changed.

## Review Triage Log

| Finding | Verdict | Route | Evidence |
| --- | --- | --- | --- |
| B1 | high | bad_spec | The canonical container shell still requires main; actual publishCmd pushes NuGet before invoking it, so the finite maintenance path stops after partial publication. |
| B2 | medium | bad_spec | The passing SDK wrapper tests cover verify/publish and Python container preflight, not the actual shell source guard; reproduce and cover actual shell entry point. |
| B3 | medium | bad_spec | The new blob-equality assertion locks publish-containers.sh to the main-only baseline, rejecting the necessary finite correction; retain all unrelated helper identity guarantees and behavioral refusal controls. |
| B4 | high | bad_spec | Read code shows HTTP owner/code/policy/anonymous checks only after intersection; independent probe accepted drift on the excluded GET child where baseline refused. |
| B5 | high | bad_spec | The new auxiliary comparison checks only endpoint subset; GET against independently bound GET POST permits a bound POST to disappear; actual probe accepted. |
| B6 | high | bad_spec | NativeActor method/verb/digest/auxiliary validation remains after filtering; actual probe accepted GET metadata with PUT native transport. |
| B7 | high | bad_spec | DaprCallback intrinsic validation remains after filtering; actual probe accepted GET metadata for a POST callback with invalid binding. |
| B8 | medium | bad_spec | Existing adverse rows mutate the included POST root and also grant GET to its child; they do not discriminate exclusion-induced omissions demonstrated by probes. |
| B9 | false | reject | The exact regular release ref and ordering are intentional and tested against actual semantic-release/real tags; 3.130.x is an empty range. Current default main retains its own unchanged configuration. Both channels remain false and finite publisher blocks non132 versions. Future unsupported config merges are outside current source selection; document finite branch lifecycle clearly. |
| B10 | medium | bad_spec | The source shell gate does not determine candidate version before production/OIDC; exact132 check currently executes later in semantic-release wrapper. Qualify the real candidate before authority using pinned native analyzer semantics, preserving read-only preproduction permissions. |
| E1 | high | bad_spec | Independent temporary probe confirms NativeActor/DaprCallback contradictory verbs bypass post-filter intrinsic guards; same demonstrated defect as B6/B7. |
| E2 | high | bad_spec | Claim violation is demonstrated by the same actual callback probe (GET methods/POST callback, invalid digests/no auxiliary accepted), not a hypothetical. |
| G1 | medium | bad_spec | Pre-verified gap: mutating source script to always query main leaves both shell tests and all7 wrapper tests green because fixture returns one SHA for every ref. Add distinct branch heads and wrong-ref mutation control. |


### Loop 1 final review — bounded patches and preexisting deferrals

All three layers are final. Each of the twelve findings remains individually recorded; patch route retains loop iteration1, with no intent gap or new bad_spec loop. Parent owns canonical B6/B9 and its successor execution pin.

| Finding | Verdict | Route | Evidence |
| --- | --- | --- | --- |
| Loop1-B1 | medium | patch | Actual producer aggregates over all captured same-identity endpoints; the new operationMethods uses only current grant reachable endpoints and rejects valid split concrete grants. Use complete receiver inventory; durable discriminating positive. |
| Loop1-B2 | medium | defer | Reviewer reran identical case against0a635,1de,current: allaccept. The witness verbs.Any predicate is unchanged; record preexisting defect without attributing it to this maintenance change. |
| Loop1-B3 | medium | patch | Native cosmiconfig metadata .config/config.json can redirect resolution while the new qualifier reads .releaserc.json. Real probe qualified patch while native effective config rejected branch/changed release rules. Bind to actual pinned native resolution and reject drift before authority. |
| Loop1-B4 | medium | patch | Real scratch main sibling tag132 yields native target range>=131<132; manually built context still qualifies132. Use actual pinned native branch normalization/range. |
| Loop1-B5 | medium | patch | Merged-source-only tags omit globally existing candidate tag132 on unrelated history. Add global collision refusal and distinguishing tests; same demonstrated branch/tag context defect. |
| Loop1-B6 | medium | patch | Canonical maint proof stores/rechecks onlyci.yml; maint wrapper requiresCI/Lint/Integration. Freeze all3 exact-branch source receipts and recheck them at every phase; main behavior unchanged. |
| Loop1-B7 | medium | defer | New focused class doesnot starta host, but running finite receiver/callback/enrollment tests alreadyexist and changedbehavior is pure topology predicate. Real captured8host replay and completeRouting518 cover modifiedcallee; fullSDKIntegration and actualRoot persisted/live gate remain mandatory. Additional specific split-verb realhost matrix is a preexisting coverage improvement, not a demonstrated regression or reason to duplicate harnesses. |
| Loop1-B8 | low | patch | New candidate checks execute in existing governance Node22 while release24 is testedlocally. DirectliteralNode24alignment is bounded and removes named runtime mismatch without new branches/guards. |
| Loop1-B9 | medium | patch | New focused workflow onlylints tip. Ownerinstructionsrequireeveryassistantcommit exactvalidation; local3commits alreadyvalidated, but blockingCI shouldvalidate complete new22a..head range withpinned21.2.2. |
| Loop1-B10 | low | patch | Candidate source/base/type/version currentlyconsoleonly. Directlyretain actualstructured qualification/configdigest/nativeidentities in existing releaseevidence, without addingauthority orpublicAPI. |
| Loop1-E1 | medium | patch | Independent nativehelperprobe sameasB4 confirms candidate crossesproduction/OIDC beforeactualnative rangecheckrejects. |
| Loop1-G1 | medium | patch | Preverifiedgap: changingpreproductionstepif to main stillpasses2wiringtests. Addactualcondition-driven maintenance workflowharness/invalidcandidate production-sentinel control andrequiremutationfail. |

## Verification

Focused real-controller tests and safety negatives; unchanged actual Root observation probe red/green; release-source and canonical publisher boundary controls; impacted packaging guards, normal pinned commitlint/hooks and actionlint. Required remote checks and full CI/Integration must be green on exact maintenance source. Public package proof and subsequent actual Root persisted runtime qualification remain separate gates.


### Loop 0 local implementation verification (2026-11-01; superseded)

- Server and Contracts test projects build in Debug with the independent a22
  Builds snapshot: zero warnings/errors. The real MVC/schema3 regression and
  adverse controls cover method isolation, malformed/duplicate/contradictory
  methods, missing witnesses/auxiliary, foreign owner/code/policy and grant verbs.
- The additional independent-evidence control first reproduced acceptance when
  endpoint Methods and Http.Verbs both changed from POST to GET while retained
  auxiliary methods remained POST. Method preflight now rejects this before
  filtering and retains the existing full transport binding checks.
- The unchanged eight captured Root observations reproduced the gateway refusal
  against the published Contracts assembly; switching only the copied probe's
  Contracts reference to candidate output validates all eight. Private inputs
  and original producer outputs remain untouched and outside the repository.
- All 495 Server routing tests pass without skips, including all 17 focused
  technical-verb controls. The three impacted packaging classes pass 203 tests
  without skips. The local
  HTTP boundary/source suite passes 11 tests against exact publisher d64f2ec,
  including both phases, every required maintenance proof and wrong tuples.
- All nine changed workflows pass actionlint; shell syntax, Git diff whitespace,
  tracked-file hook guard and pinned commitlint pass. npm verifies 499 registry
  signatures; the semantic-release success fixture passes both histories.
- Local implementation pins d64f2ec throughout; its joint review, normal remote
  push/qualification and the protected maintenance release/14 official archive
  verification remain parent-owned gates. This local verification does not
  substitute for exact maintenance CI, full Integration or production proof.

## Delivery Gates After Review

- [x] Joint three-layer review and bounded patch closure; exact canonical publisher30ea remotely qualified in run41267580527 (138 tests and full commit range lint).
- [ ] Normal SDK lint/hooks and protected maintenance PR/merge; exact branch CI and full Integration.
- [ ] Protected production release and fourteen official ID/version/source archive checks.

Given successful protected release, when official archives download, then all fourteen IDs/version/source match the actual maintenance source. These delivery gates remain pending and are not local verification claims.


### Review loop 1 local implementation verification (2026-11-01)

- Rederived complete intrinsic validation before HTTP-grant intersection. Each
  HTTP endpoint matches its own complete captured verbs, while the complete
  admitted exact path/operation/owner/code method union matches the producer's
  aggregated auxiliary binding. The actual split-action positive and the
  missing-sibling GET-versus-GET-POST refusal both pass.
- Debug Server build with the independent a22 package snapshot passes with zero
  warnings/errors. All 40 focused technical-verb tests pass without skips,
  including the actual MVC/schema-three command/status pair and excluded-child
  HTTP owner/code/policy/anonymous/auxiliary drift, native actor and callback
  kind/method/verb/digest/auxiliary controls. The earlier broader routing pass
  covered 517 tests before the split-action clarification; it is supplementary,
  not a claim of another full run on the final source.
- The three affected packaging classes pass 203 tests without skips. All 19
  Python release-boundary test cases pass: four source-ref tests, nine wrapper
  and actual canonical-shell tests, and six native-analyzer tests over owned
  real Git histories. Main and finite heads differ. Wrong-main-query mutations
  fail finite source qualification while main remains valid; unknown fixture
  refs refuse. Actual canonical shell reaches an intercepted first-write
  boundary; its unmodified old baseline refuses the same maintenance tuple.
- Native qualification uses installed lockfile-pinned commit-analyzer 13.0.1
  and semantic-release 25.0.9 Git/version helpers without release lifecycle
  hooks. Real fixtures cover patch acceptance and no-release/minor/major,
  missing/wrong/unreachable/later base, shallow history, wrong source/ref,
  unsupported analyzer and competing configuration refusals. These fixtures
  are regression evidence, not live maintenance qualification.
- The unchanged private eight-host capture reproduces the published baseline's
  gateway refusal and validates all eight using only the copied probe's final
  candidate Contracts reference. Original and copied input bytes remain equal;
  the executed candidate DLL matches the build output. Inputs and detailed
  receipts remain task-owned private scratch outside the repository.
- All nine changed workflows pass actionlint. Shell syntax, Node syntax, Python
  compilation, Git whitespace, tracked-file guard, the normal pre-commit hook,
  and pinned strict commitlint pass. The semantic-release success fixture passes
  both isolated histories. Loop 1 logs use `/tmp/es31302-rederive-*` filenames;
  loop 0 logs and archived packets remain preserved.
- The final source pins corrected canonical publisher
  `2e88abc4d91e12c7dbf7b44e2b8dda96847dbc1a`; four unrelated executed helper
  blobs retain their baseline identities. No canonical owner workspace,
  generated publisher helper, producer output, observation, ACL or witness was
  edited. Joint review, exact-source remote CI/commitlint/full Integration,
  protected production publication, fourteen official archive checks, and the
  subsequent actual Root persisted runtime proof remain delivery gates.

### Author final affected check

Final already-built Routing namespace: 518 passed, zero errors/failures/skips/not-run, exit0; `/tmp/es31302-loop1-final-routing-result.json` and `/tmp/es31302-loop1-final-routing.log`. No rebuild or wider project run occurred. The canonical regression checkout uses full history for its actual baseline-shell counterfactual; actionlint passed the changed CI file.


### Loop 1 bounded review patches

The patch route retains all thirteen original and twelve loop-one triage rows,
all existing exact-3.130.2/main/safety/eight-host positives, and both recorded
preexisting deferrals. The witness predicate and additional running-host matrix
remain deferred in `deferred-work.md`; this patch adds neither fix nor a second
host harness.

- Complete split-method unions now use the entire receiver inventory. The
  concrete GET/POST grants on different values of one route pattern form a
  discriminating positive; the absent-sibling negative remains required.
- Qualification binds actual pinned native cosmiconfig resolution to committed
  configuration. Pure native `normalize.release` uses already-fetched branch
  tag inventories and enforces accepted type/range; global candidate tag
  collisions refuse even on unrelated history. No native branch fetch, note
  update or user-ref mutation runs.
- A condition-driven workflow harness executes the actual qualifier, proves an
  invalid candidate cannot enter the production sentinel, and detects changing
  the maintenance step condition to main. Governance directly uses Node 24.
- Existing artifact uploads retain structured qualification JSON, effective
  configuration SHA256 and pinned native identities before production and at
  pre-OIDC revalidation, without granting authority or adding a public API.
- SDK workflows, wrapper, affected guards and current docs consume canonical
  publisher `30ea43a56330a9828ed0bb2e837289231d8630d1`, including frozen
  CI/commitlint/Integration proofs, container revalidation and full-range lint.
  The four unrelated baseline helper comparisons remain unchanged. Canonical
  owner files and generated helpers remain untouched.


Affected patch checks: focused receiver class 41 passed; native/workflow
qualification 11 passed on Node 24.22.1; canonical-30ea wrapper/shell boundaries
10 passed; selected packaging/workflow/pin guards 12 passed. Both affected Debug
builds report zero warnings/errors. Actionlint passes only the changed CI and
release workflows; shell/Node/Python syntax and Git whitespace pass. Logs use
`/tmp/es31302-loop1-patch-*`. No broader verification, additional host harness,
private producer replay, canonical edit, ref mutation or publication ran in
this patch pass; parent performs independent full verification.


### Author verification of the bounded loop 1 patches

The parent classified the twelve loop1 rows as ten bounded patches and two
preexisting deferrals. All patch groups are closed without a new review loop;
Loop1-B2 and Loop1-B7 remain explicitly deferred above and in deferred-work.md.
The frozen intent remains byte-identical to the reviewed packet.

- Independent focused execution passes 41 receiver, 11 native/workflow, ten
  canonical wrapper/shell, and twelve selected packaging controls (74 total,
  zero failures/skips). Native checks use Node24.22.1. The already-built final
  Routing namespace passes 519 tests, zero failures/skips.
- A fresh task-private replay of all eight original captured hosts passes
  against the final Contracts binary. Original/copied input hashes remain
  unchanged; executed DLL hash matches candidate output
  `a921d7c4247a6927b11e99332ccc07c45dc7479d55b69972d1455704bac960e9`.
  This is pure validation of captured actual topology, not a new live run or
  a substitute for the mandatory later public Root persisted/runtime gate.
- Publisher30ea is remotely green: run41267580527, job124733497060, complete
  138 Python tests and pinned full22a..30ea commitlint range. Its four other
  executed helper bytes remain baseline-identical.
- Author logs use `/tmp/es31302-postpatch-author-*`; the new actual-input
  receipt is `/tmp/es31302-postpatch-actual-topology/receipt.json`. Previous
  frozen packets, source archives, private observations and public archives
  remain preserved. Production/native host source and original producers
  remain untouched apart from the bounded Contracts validator correction
  in this isolated SDK workspace.

Exact protected SDK branch CI/Integration, production release and fourteen
official archive checks remain required before reporting publication.
