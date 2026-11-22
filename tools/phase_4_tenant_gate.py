"""Fixed Story 2.14 evidence adapter for the G-TENANT decision."""

import base64
from collections import Counter
import datetime as dt
import io
import json
from pathlib import PurePosixPath
import re
import xml.etree.ElementTree as ET
import zipfile
import zlib

import phase_4_transition as C

ADAPTER = "tenant-boundary-v1"
PATH = "tools/phase_4_tenant_gate.py"
DECLARATION = "a10d493ac0d5840f30fbf7ecf983f54c3d26c50152dc6dbca96e6b94d557762f"
LANES = {
    "eventstore_ci": (C.REPOSITORY, ".github/workflows/ci.yml"),
    "eventstore_integration": (C.REPOSITORY, ".github/workflows/integration.yml"),
    "tenants_source": (C.TENANTS, ".github/workflows/source-reference.yml"),
    "tenants_package": (C.TENANTS, ".github/workflows/ci.yml"),
}
BROAD = "ci / build-and-test"
SOURCE = "Actual source catalog owner profile"
REST = "Blocking authenticated live routes (rest)"
JOBS = {
    "eventstore_ci": {BROAD: ("Unit tests (Tier 1, Microsoft.Testing.Platform)",),
                      "ci / contracts": ("Run Contracts tests",)},
    "eventstore_integration": {
        "live-sidecar": ("Verify canonical tenant and generated explicit-scope boundaries", "Run Live-Sidecar Integration Tests"),
        "published-compatibility": ("Run isolated published qualification and ownership regressions",)},
    "tenants_source": {SOURCE: ("Record the actual selected SDK source and producer version",
                                "Prove fresh backend ownership before provisioning private owner material",
                                "Require bootstrap bearer validation and bound workload refusals",
                                "Require actual cohort binding, owned bootstrap, persisted effects and refusal"),
                       "Projection provenance": ("Verify real-handler projection provenance",)},
    "tenants_package": {
        "Published catalog HTTP (shipping package defaults)": ("Require every generated HTTP case and catalog refusal control",),
        "Blocking HTTP contracts": ("Verify generated HTTP ETags and production REST consumers",),
        "Server profile boundary (shipping package defaults)": ("Require the current v2 owner-source runtime assertions",),
        BROAD: ("Unit tests (Tier 1, Microsoft.Testing.Platform)", "Integration tests (Tier 2, Microsoft.Testing.Platform)"),
        "Blocking authenticated live routes (persisted)": ("Verify all hosted topology, background delivery, and persisted-state regressions without skips",),
        "Blocking authenticated live routes (anonymous-ui)": ("Preserve anonymous UI denial coverage",),
        "Blocking authenticated live routes (authenticated-ui)": ("Verify authenticated UI, measured responsive modes and contextual audit Back",),
        REST: ("Verify the built package graph and provision the catalog on the fresh backend",
               "Verify cold bootstrap, six REST reads, global-owner audit, public system denial and cursor recovery"),
        "ci / aspire-tests": ("Run Aspire hook", "Require executed Aspire tests without skips")},
}
SOURCE_PROFILE = "tenants-source-catalog-owner-profile"
REST_RESULTS = "authenticated-route-test-results-rest"
ARTIFACT_JOBS = {
    "eventstore_ci": {"blocking-test-results": BROAD, "contracts-test-results": "ci / contracts"},
    "eventstore_integration": {"live-sidecar-test-results": "live-sidecar",
                               "published-compatibility-results": "published-compatibility"},
    "tenants_source": {SOURCE_PROFILE: SOURCE},
    "tenants_package": {REST_RESULTS: REST, "aspire-test-results": "ci / aspire-tests"},
}

# Fixed compiled test identities and distinct passing theory-row minima. The
# class floors also cover the full generated HTTP and authenticated REST suites.
# Reports outside these assemblies do not count towards a case.
CASES = {
    "Hexalith.EventStore.Contracts.Tests.trx": {
        "Hexalith.EventStore.Contracts.Tests.Identity.TenantCanonicalizerTests": (22, {
            "ExplicitTenantIsCanonicalized": 3, "InvalidOrReservedTenantIsRejected": 9,
            "TenantLongerThan64CharactersIsRejected": 1, "DistinctTenantGrantsRemainDistinctAfterCanonicalization": 1,
            "DuplicateOrMalformedGrantsAreRejected": 3, "PlatformScopeHasDisjointGrammar": 5})},
    "Hexalith.EventStore.RestApi.Generators.Tests.trx": {
        "Hexalith.EventStore.RestApi.Generators.Tests.RestApiGeneratedControllerErrorSemanticsTests": (62, {
            "GeneratedActionsSupplyTheResolvedPartitionTenantToExactCatalogValidation": 6,
            "ClaimsModeWithoutExplicitTenantRejectsBeforeCatalogAndGateway": 1,
            "GeneratedBodyAggregateMismatchRejectsBeforeCatalogAndGateway": 3})},
    "Hexalith.EventStore.Server.Tests.trx": {
        "Hexalith.EventStore.Server.Tests.Routing.GeneratedCatalogScopePreflightTests": (33, {
            "InvalidScopeOrGrantsNeverAcquireCatalogAuthority": 30, "CanonicalExplicitScopeStillUsesCatalogAuthority": 3}),
        "Hexalith.EventStore.Server.Tests.Controllers.PlatformScopeGrantPreflightTests": (10, {
            "InvalidPlatformGrantsNeverReachCatalogOrDispatch": 6,
            "ExplicitPlatformScopeStillRequiresCatalogWithEmptyOrDistinctTenantGrants": 4}),
        "Hexalith.EventStore.Server.Tests.Controllers.WorkloadGatewayControllerTests": (2, {
            "Bound_command_requires_exact_signed_requester_and_domain": 1,
            "Submit_rejects_cross_tenant_or_caller_supplied_origin_before_dispatch": 1}),
        "Hexalith.EventStore.Server.Tests.SignalR.ProjectionChangedHubTests": (5, {
            "ProjectionAdmission_MalformedOrDuplicateGrant_RejectsBeforeValidationOrMembership": 24,
            "ProjectionAdmission_ValidDistinctGrants_ForwardsCanonicalExplicitTenant": 4})},
    "Hexalith.EventStore.Client.Tests.trx": {
        "Hexalith.EventStore.Client.Tests.Gateway.EventStoreGatewayWorkloadClientTests": (17, {
            "MixedCaseStatusTenantIsCanonicalBeforeCredentialsAndTransport": 1,
            "InvalidStatusTenantNeverAcquiresCredentials": 9, "Unsupported_or_conflicting_stream_scope_refuses_before_credentials": 7})},
    "Hexalith.EventStore.Admin.Server.Tests.trx": {
        "Hexalith.EventStore.Admin.Server.Tests.Authorization.AdminTenantAuthorizationFilterTests": (3, {
            "CatalogedGlobalListRequiresExplicitPlatformScopeAndReadPermission": 2,
            "CatalogedGlobalListRejectsTenantActionArgumentBeforeAction": 1})},
    "Hexalith.EventStore.Sample.Tests.trx": {
        "Hexalith.EventStore.Sample.Tests.SampleApi.SampleApiGeneratedControllerRuntimeTests": (1, {
            "CounterQuery_WhenRouteTenantIsMissing_ReturnsBadRequestBeforeGatewayCall": 1})},
    "published-compatibility.trx": {
        "Hexalith.EventStore.PublishedCompatibility.Tests.PublishedScenarioTests": (1, {
            "Published31282QualificationMatrixAsync": 1})},
    "live-sidecar-results.trx": {
        "Hexalith.EventStore.Server.LiveSidecar.Tests.Integration.RouteCatalogHostedLiveTests": (1, {
            "PublicDevelopmentTransportAuthenticatesPeersAndPersistsActorsAcrossNativeRestart": 1})},
    "aspire-results.trx": {
        "Hexalith.Tenants.IntegrationTests.TenantsApiGeneratedControllerTests": (67, {
            "TenantDetail_generated_route_submits_canonical_tenant_scoped_query": 2,
            "Generated_read_refuses_missing_invalid_or_extra_scope_before_gateway": 6,
            "TenantDetail_generated_route_refuses_invalid_or_unauthorized_grants_before_gateway": 5,
            "Public_system_tenant_audit_route_is_denied_before_gateway": 3,
            "Global_audit_generated_route_uses_cataloged_scope_and_retained_owner_binding": 1})},
    "authenticated-rest-rest.trx": {"Hexalith.Tenants.IntegrationTests.AuthenticatedTenantsRouteTests": (9, {
        "CommittedNativeRemovalRefusesARetainedGrantingProjectionThroughProductionReaders": 1,
        "AuthenticatedOwnerRecoveryPersistsExactSnapshotsAndPermissionRefusalDoesNotAppendEvents": 1,
        "InvalidProtectedCursorHasRealSentinelAndRecoversPageOneThroughProductionGateway": 1,
        "AnonymousRestRequestCannotReadCommandSeededTenant": 1,
        "PopulatedTwentyRowPageHydratesWithoutActorQuotaFailure": 1,
        "SixAuthenticatedDaprRestReadsMatchPersistedState": 1, "ConditionalDetailReadRecognizesPersistedAuthority": 1,
        "AuthenticatedWrongPeerCannotInvokeTheReadReceiver": 1, "AuthenticatedReadCallerCannotInvokeADisallowedVerb": 1})},
}
# These theory arguments distinguish the positive canonicalization case from
# a lowercase-only pass and the individual denial paths from one another.
SCOPE_SCENARIOS = {
    "Route": ("missing malformed reserved duplicate-route tenant-header platform-header",
              "malformed-grant reserved-grant duplicate-grant unauthorized anonymous"),
    "Claims": ("missing malformed reserved duplicate-header platform-header",
               "malformed-grant reserved-grant duplicate-grant unauthorized anonymous"),
    "Platform": ("missing malformed duplicate-header tenant-header",
                 "malformed-grant reserved-grant duplicate-grant anonymous"),
    "System": ("reserved", ""),
}
def theory(names, rows):
    """xUnit displays strings quoted, booleans title-cased and missing values as null."""
    def display(value):
        return str(value) if isinstance(value, bool) else json.dumps(value, ensure_ascii=False)
    return {", ".join(f"{name}: {display(value)}" for name, value in zip(names.split(), row, strict=True)) for row in rows}


REQUIRED_ARGUMENTS = {
    "CatalogedGlobalListRequiresExplicitPlatformScopeAndReadPermission": {"permitted: True", "permitted: False"},
    "CanonicalExplicitScopeStillUsesCatalogAuthority": {"source: Route", "source: Platform", "source: Claims"},
    "GeneratedActionsSupplyTheResolvedPartitionTenantToExactCatalogValidation": theory("mode conflicting", (
        (mode, conflict) for mode in ("Route", "Claims", "System") for conflict in (False, True))),
    "GeneratedBodyAggregateMismatchRejectsBeforeCatalogAndGateway": theory("mode", [(v,) for v in ("Route", "Claims", "System")]),
    "InvalidPlatformGrantsNeverReachCatalogOrDispatch": theory("query extraGrant", (
        (query, grant) for query in (True, False) for grant in ("SYSTEM", " acme", "acme"))),
    "ExplicitPlatformScopeStillRequiresCatalogWithEmptyOrDistinctTenantGrants": theory("query tenantGrants", (
        (query, grants) for query in (True, False) for grants in (True, False))),
    "ProjectionAdmission_ValidDistinctGrants_ForwardsCanonicalExplicitTenant": theory("invocation", (
        (v,) for v in ("join", "scoped-join", "validate", "reconcile"))),
    "InvalidOrReservedTenantIsRejected": theory("input", ((v,) for v in (None, "", " Acme", "acme ", "-acme", "acme-", "acme:other", "systEM", "é"))),
    "DuplicateOrMalformedGrantsAreRejected": theory("first second", [("Acme", "acme"), ("ACME", "bad grant"), ("acme", "system")]),
    "PlatformScopeHasDisjointGrammar": theory("value valid", [("platform:tenants", True), ("platform:global-administrators", True),
        ("platform:Tenants", False), ("platform:bad--scope", False), ("system", False)]),
    "Public_system_tenant_audit_route_is_denied_before_gateway": theory("stream", [(None,), ("tenant",), ("global-administrators",)]),
    "Generated_read_refuses_missing_invalid_or_extra_scope_before_gateway": theory("path tenantHeader platformHeader", [
        ("/api/tenants", None, None), ("/api/tenants/tenant_alpha", None, None), ("/api/tenants/System", None, None),
        ("/api/tenants/tenant-alpha", "Tenant-Alpha", None), ("/api/tenants/tenant-alpha", "tenant-beta", None),
        ("/api/tenants/tenant-alpha", None, "platform:tenants")]),
    "Unsupported_or_conflicting_stream_scope_refuses_before_credentials": theory("scope domain header", [
        ("platform:global-administrators", "tenants", None), ("platform:tenants", "global-administrators", None),
        ("platform:other", "other", None), *((scope, domain, header) for scope, domain in
        (("platform:tenants", "tenants"), ("tenant-a", "timesheets")) for header in ("X-Hexalith-Tenant", "X-Hexalith-Scope"))]),
    "InvalidStatusTenantNeverAcquiresCredentials": {
        *theory("tenant", ((v,) for v in ("SYSTEM", "system", "platform:tenants", "ac_me", " acme", "acme-", "-acme", "acme "))),
        'tenant: "' + "a" * 50 + '"···'},
    "InvalidScopeOrGrantsNeverAcquireCatalogAuthority": {
        f'source: {scope}, scenario: "{scenario}", expected: {status}'
        for scope, groups in SCOPE_SCENARIOS.items() for status, group in zip((400, 403), groups)
        for scenario in group.split()},
    "ProjectionAdmission_MalformedOrDuplicateGrant_RejectsBeforeValidationOrMembership": {
        f'invocation: "{operation}", invalidGrant: "{grant}"'
        for operation in ("join", "scoped-join", "validate", "reconcile")
        for grant in ("", " ", "bad!", "system", "ACME", "acme")},
    "ExplicitTenantIsCanonicalized": {f'input: "{value}", expected: "{value.lower()}"'
                                     for value in ("Acme", "A", "ACME-123")},
    "TenantDetail_generated_route_submits_canonical_tenant_scoped_query": {
        'routeTenant: "Tenant-Alpha", tenantGrant: "TENANT-ALPHA"',
        'routeTenant: "tenant-alpha", tenantGrant: "tenant-alpha"'},
    "TenantDetail_generated_route_refuses_invalid_or_unauthorized_grants_before_gateway": {
        "firstGrant: null, secondGrant: null", 'firstGrant: "tenant-beta", secondGrant: null',
        *(f'firstGrant: "tenant-alpha", secondGrant: "{second}"' for second in ("tenant_beta", "TENANT-ALPHA", "System"))},
}
REPORTS = {
    "blocking-test-results": tuple(f for f in CASES if f.startswith("Hexalith.EventStore.") and ".Contracts." not in f),
    "contracts-test-results": ("Hexalith.EventStore.Contracts.Tests.trx",),
    "live-sidecar-test-results": ("live-sidecar-results.trx",),
    "aspire-test-results": ("aspire-results.trx",),
    "published-compatibility-results": ("published-compatibility.trx",),
    REST_RESULTS: ("authenticated-rest-rest.trx",),
}
BACKEND = "tenants-source-backend.json"
RUNTIME = "tenants-source-catalog-result.json"
PACKAGE = "package-provenance.json"
OUTCOMES = ("ownedBootstrapTerminalAndProjectedMembership", "conditionalRead", "distinctAuditOwnerReadback",
            "nativeLegacyHistories", "sharedLegacyTopicReadback", "publicSystemDenied",
            "actualAdminUiProxyAndOwnerRead", "refusedDescendant", "persistedTenant")
VERSION = re.compile(r"(?:0|[1-9][0-9]*)\.(?:0|[1-9][0-9]*)\.(?:0|[1-9][0-9]*)")
MAX_ARCHIVE = 64 * C.MAX_BYTES
MAX_REPORT = 48 * C.MAX_BYTES


def evidence(condition, check):
    C.require(condition, "BUSINESS_PREREQUISITE_MISSING", "G-TENANT " + check)


def validate_claim(claim, eventstore_head):
    C.closed(claim, ("adapter", "adapter_sha256", "tenants_head", "builds_head", "sdk_version", "mcpcli_version", "runs"))
    C.require(claim["adapter"] == ADAPTER and C.sha(claim["adapter_sha256"]) and C.sha(claim["tenants_head"], 40)
              and C.sha(claim["builds_head"], 40) and all(isinstance(claim[k], str) and VERSION.fullmatch(claim[k])
              for k in ("sdk_version", "mcpcli_version")), "INPUT_INVALID", "invalid tenant qualification identity", 2)
    C.closed(claim["runs"], LANES)
    for lane, (repo, workflow) in LANES.items():
        run = claim["runs"][lane]
        C.closed(run, ("repository", "run_id", "attempt", "head_sha", "workflow", "workflow_sha256", "artifacts"))
        C.require(run["repository"] == repo and run["workflow"] == workflow
                  and run["head_sha"] == (eventstore_head if repo == C.REPOSITORY else claim["tenants_head"])
                  and C.sha(run["workflow_sha256"]) and C.positive(run["run_id"]) and C.positive(run["attempt"]),
                  "RUN_INVALID", "wrong repository, workflow, revision or attempt in " + lane)
        C.closed(run["artifacts"], ARTIFACT_JOBS[lane])
        for item in run["artifacts"].values():
            C.closed(item, ("id", "sha256"))
            C.require(C.positive(item["id"]) and C.sha(item["sha256"]), "INPUT_INVALID", "invalid artifact pin", 2)


def check_declaration(row, claim, paths):
    fields = ("command", "trigger", "subject_paths", "evidence_identity", "pass_condition", "evaluator_roles")
    C.require(C.digest(C.canonical({k: row[k] for k in fields})) == DECLARATION,
              "MATRIX_INVALID", "G-TENANT business declaration differs from the fixed adapter")
    C.require(claim["adapter_sha256"] == paths[PATH], "SOURCE_MISMATCH", "G-TENANT adapter differs from approval")


def completed(record):
    return record.get("status") == "completed" and record.get("conclusion") == "success"


UPLOAD_STEPS = {
    "blocking-test-results": "Upload test and coverage evidence",
    "contracts-test-results": "Upload Contracts test results",
    "live-sidecar-test-results": "Upload live-sidecar test results",
    "published-compatibility-results": "Upload measured outcomes and bounded command logs",
    SOURCE_PROFILE: "Retain only credential-free source qualification results",
    REST_RESULTS: "Retain test results for every live-route matrix suite",
    "aspire-test-results": "Upload test results",
}


def finished(record, require_run_success):
    if require_run_success:
        return completed(record)
    # A job-scoped gate can credit its successful checks after the owner run finishes.
    return record.get("status") == "completed" and record.get("conclusion") in (
        "success", "failure", "cancelled", "timed_out", "neutral", "skipped", "action_required", "stale")


def latest_attempt(api, lane, pin, *, require_run_success=True):
    latest = api.get(f"/actions/runs/{pin['run_id']}")
    C.require(latest.get("id") == pin["run_id"] and latest.get("run_attempt") == pin["attempt"]
              and latest.get("head_sha") == pin["head_sha"] and finished(latest, require_run_success),
              "RUN_NOT_SUCCESSFUL", "superseded, changed or unsuccessful " + lane)


def step_output(log, step, action):
    """Select one runner action in the successful step's authenticated UTC interval."""
    C.require(completed(step), "CI_UNAUTHENTICATED", "step did not succeed: " + step["name"])
    start = C.timestamp(step.get("started_at")).strftime("%Y-%m-%dT%H:%M:%S").encode()
    end = C.timestamp(step.get("completed_at")).strftime("%Y-%m-%dT%H:%M:%S").encode()
    # Actions exposes whole seconds in the REST jobs API and subsecond runner
    # timestamps in logs. A new runner step terminates the selected action.
    begin = re.compile(rb"##\[group\]Run " + action)
    output = []
    selected = found = False
    size = 0
    for line in io.BytesIO(log):
        line = line.rstrip(b"\r\n")
        timed = re.match(rb"^(\d{4}-\d\d-\d\dT\d\d:\d\d:\d\d)(?:\.\d+)?Z (.*)$", line)
        if timed:
            instant, message = timed.groups()
            if instant < start:
                continue
            if instant > end:
                break
            if message.startswith(b"##[group]Run ") or message == b"Post job cleanup.":
                selected = bool(begin.fullmatch(message))
                if selected:
                    C.require(not found, "CI_UNAUTHENTICATED", "ambiguous runner action for " + step["name"])
                    found = True
        else:
            message = line  # Continuation of a multiline runner input.
        if selected:
            size += len(message) + 1
            C.require(size <= 4 * C.MAX_BYTES, "CI_UNAUTHENTICATED", "oversized action log for " + step["name"])
            output.append(message)
    C.require(found and b"##[endgroup]" in output, "CI_UNAUTHENTICATED", "missing runner action for " + step["name"])
    return b"\n".join(output).decode("utf-8")


def verify_job_log(api, job, pin, uploads, branches):
    log, _ = api.fetch(api.prefix + f"/actions/jobs/{job['id']}/logs", binary=True, binary_limit=MAX_ARCHIVE)
    steps = job["steps"]
    # Locate the runner's actual root checkout, allowing setup before it and
    # additional checkouts into subdirectories (e.g. the reusable Builds job).
    root_checkouts = []
    for match in re.finditer(rb"(?m)^(\d{4}-[0-9T:-]{14})[.0-9]*Z ##\[group\]Run actions/checkout@([0-9a-f]{40})\r?$", log):
        end = log.find(b"##[endgroup]", match.end(), match.end() + C.MAX_BYTES)
        C.require(end != -1, "CI_UNAUTHENTICATED", "unreadable checkout header")
        header = log[match.end():end]
        if re.findall(rb"(?m)Z   path: (.+?)\r?$", header) not in ([], [b"."]):
            continue
        root_checkouts.append((match[1].decode(), match[2].decode()))
    C.require(len(root_checkouts) == 1, "CI_UNAUTHENTICATED", "missing or ambiguous root checkout in " + job["name"])
    instant, action_sha = root_checkouts[0]
    checkout_names = {"live-sidecar": "Checkout repository", "published-compatibility": "Checkout exact executor and comparison tags"}
    checkout_name = checkout_names.get(job["name"], "Run actions/checkout@" + action_sha)
    candidates = [s for s in steps if s.get("name") == checkout_name and completed(s)
                  and s.get("started_at", "")[:19] <= instant <= s.get("completed_at", "")[:19]]
    C.require(len(candidates) == 1, "CI_UNAUTHENTICATED", "missing successful checkout step in " + job["name"])
    output = step_output(log, candidates[0], b"actions/checkout@" + action_sha.encode())
    header, content = output.split("##[endgroup]", 1)
    heads = re.findall(r"(?m)^\[command\]/usr/bin/git log -1 --format=%H\n([0-9a-f]{40})$", content)
    C.require(re.findall(r"(?m)^  repository: (.+)$", header) == [pin["repository"]]
              and ("Syncing repository: " + pin["repository"]) in content.splitlines()
              and heads == [pin["head_sha"]], "SOURCE_MISMATCH",
              "actual checkout differs from qualified source in " + job["name"])
    for name in uploads:
        steps_for_upload = [s for s in steps if s.get("name") == UPLOAD_STEPS[name]]
        C.require(len(steps_for_upload) == 1, "ARTIFACT_INVALID", "missing producer upload for " + name)
        output = step_output(log, steps_for_upload[0], rb"actions/upload-artifact@[0-9a-f]{40}")
        header, content = output.split("##[endgroup]", 1)
        receipts = re.findall(r"(?m)^Artifact (.+?) has been successfully uploaded! Final size is ([0-9]+) bytes\. Artifact ID is ([0-9]+)$", content)
        C.require(re.findall(r"(?m)^  name: (.+)$", header) == [name] and len(receipts) == 1
                  and receipts[0][0] == name and receipts[0][2] == str(pin["artifacts"][name]["id"]),
                  "ARTIFACT_INVALID", "job upload does not identify pinned artifact " + name)
        if name == SOURCE_PROFILE:
            paths = re.findall(r"(?m)^  path: (/[^\n]+)/" + re.escape(BACKEND) + r"\n\1/" + re.escape(RUNTIME) + "$", header)
            C.require(len(paths) == 1, "ARTIFACT_INVALID", "source job did not upload both owner receipts")
        job.setdefault("verified_upload_sizes", {})[name] = int(receipts[0][1])
    # Some attempts omit their PR association. An original runner observation
    # of GitHub's immutable base variable can then establish the qualifying ref.
    base_steps = [s for s in steps if s.get("name") == "Run printenv GITHUB_BASE_REF"]
    if not base_steps:
        return False
    C.require(len(base_steps) == 1, "RUN_INVALID", "ambiguous original PR base evidence")
    output = step_output(log, base_steps[0], rb"printenv GITHUB_BASE_REF")
    header, content = output.split("##[endgroup]", 1)
    # The default shell is invoked for exactly the runner-displayed command.
    # Refuse a second script line or an override of the original variable.
    script = header.splitlines()
    C.require(len(script) >= 3 and script[1].replace("\x1b[36;1m", "").replace("\x1b[0m", "").strip() == "printenv GITHUB_BASE_REF"
              and script[2].startswith("shell: ") and not any(line.startswith("  GITHUB_BASE_REF:") for line in script),
              "RUN_INVALID", "PR base step is not a direct read of GitHub's original target")
    C.require(content.strip() in branches, "RUN_INVALID", "ineligible original PR base")
    return True


def authenticate_jobs(api, lane, pin):
    repository, workflow = LANES[lane]
    return authenticate_workflow_jobs(api, lane, pin, repository, workflow, JOBS[lane], ARTIFACT_JOBS[lane])


def authenticate_workflow_jobs(api, lane, pin, repository, workflow, required_jobs, artifact_jobs, *, require_run_success=True):
    """Authenticate an owner's fixed workflow, actual job checkouts, steps and uploads."""
    run_id, attempt, head = pin["run_id"], pin["attempt"], pin["head_sha"]
    base = f"/actions/runs/{run_id}"
    latest_attempt(api, lane, pin, require_run_success=require_run_success)
    record = api.get(base + f"/attempts/{attempt}")
    branches = ("main", "release/parties-sdk-3.130.2") if repository == C.REPOSITORY else ("main",)
    event = record.get("event")
    if event == "pull_request":
        prs = record.get("pull_requests")
        C.require(isinstance(prs, list), "RUN_INVALID", "missing PR inventory in " + lane)
        matching = []
        for pr in prs:
            source, target = pr.get("head", {}), pr.get("base", {})
            if source.get("sha") == head and source.get("ref") == record.get("head_branch"):
                # Inline workflow-run pull requests expose numeric repo IDs.
                def owns(side):
                    repo = side.get("repo", {})
                    if repo.get("full_name") is not None:
                        return repo["full_name"] == repository
                    return C.positive(repo.get("id")) and repo["id"] == record["repository"].get("id")
                if owns(source) and owns(target):
                    matching.append(target.get("ref"))
        trigger = not prs or len(prs) == len(matching) == 1 and matching[0] in branches
    else:
        trigger = (event == "push" or event == "workflow_dispatch" and repository == C.REPOSITORY)
        trigger = trigger and record.get("head_branch") in branches
    C.require(record.get("id") == run_id and record.get("run_attempt") == attempt and record.get("head_sha") == head
              and record.get("repository", {}).get("full_name") == repository
              and record.get("head_repository", {}).get("full_name") == repository and trigger,
              "RUN_INVALID", "wrong run identity in " + lane)
    C.require(finished(record, require_run_success), "RUN_NOT_SUCCESSFUL", lane + " did not qualify")
    C.require(record.get("path") == workflow and C.positive(record.get("workflow_id")),
              "WORKFLOW_MISMATCH", "wrong workflow in " + lane)
    metadata = api.get("/actions/workflows/" + str(record["workflow_id"]))
    C.require(metadata.get("id") == record["workflow_id"] and metadata.get("state") == "active"
              and metadata.get("path") == workflow and C.digest(api.file(head, workflow)) == pin["workflow_sha256"],
              "WORKFLOW_MISMATCH", "workflow identity or bytes changed in " + lane)
    suite_id = record.get("check_suite_id")
    C.require(C.positive(suite_id), "CI_UNAUTHENTICATED", "missing check suite for " + lane)
    suite = api.get(f"/check-suites/{suite_id}")
    C.require(suite.get("id") == suite_id and suite.get("head_sha") == head and finished(suite, require_run_success)
              and suite.get("app", {}).get("id") == C.ACTIONS_APP_ID and suite["app"].get("slug") == "github-actions",
              "CI_UNAUTHENTICATED", "wrong Actions check suite in " + lane)
    checks = api.pages(f"/check-suites/{suite_id}/check-runs?filter=all&per_page=100", "check_runs")
    jobs = api.pages(base + f"/attempts/{attempt}/jobs?per_page=100", "jobs")
    verified = {}
    logged_base = False
    for name, steps in required_jobs.items():
        matches = [j for j in jobs if j.get("name") == name]
        C.require(len(matches) == 1 and completed(matches[0]), "CI_UNAUTHENTICATED", "missing successful job " + name)
        job = matches[0]
        C.require(job.get("run_id") == run_id and job.get("run_attempt") == attempt and job.get("head_sha") == head
                  and C.positive(job.get("id")), "CI_UNAUTHENTICATED", "wrong attempt for job " + name)
        checks_for_job = [c for c in checks if c.get("name") == name and C.positive(c.get("id"))
                          and c.get("url") == api.prefix + "/check-runs/" + str(c["id"]) == job.get("check_run_url")
                          and c.get("check_suite", {}).get("id") == suite_id and c.get("head_sha") == head
                          and c.get("app", {}).get("id") == C.ACTIONS_APP_ID and completed(c)]
        C.require(len(checks_for_job) == 1, "CI_UNAUTHENTICATED", "unlinked job " + name)
        for step in steps:
            found = [s for s in job.get("steps", []) if s.get("name") == step]
            C.require(len(found) == 1 and completed(found[0]), "BUSINESS_PREREQUISITE_MISSING",
                      lane + " missing successful step " + step)
        uploads = [artifact for artifact, producer in artifact_jobs.items() if producer == name]
        base_proven = verify_job_log(api, job, pin, uploads, branches)
        logged_base = logged_base or base_proven
        verified[name] = job
    C.require(event != "pull_request" or record["pull_requests"] or logged_base,
              "RUN_INVALID", "original PR target unavailable for " + lane)
    return verified


def gitlinks(api, head):
    commit = api.get("/git/commits/" + head)
    tree = commit.get("tree", {}).get("sha")
    C.require(commit.get("sha") == head and C.sha(tree, 40), "SOURCE_MISMATCH", "unverified Tenants commit")
    record = api.get("/git/trees/" + tree + "?recursive=1")
    C.require(record.get("sha") == tree and record.get("truncated") is False and isinstance(record.get("tree"), list),
              "SOURCE_MISMATCH", "incomplete Tenants source tree")
    links = {}
    for name in ("EventStore", "Builds"):
        path = "references/Hexalith." + name
        matches = [x for x in record["tree"] if x.get("path") == path]
        C.require(len(matches) == 1 and matches[0].get("mode") == "160000" and matches[0].get("type") == "commit"
                  and C.sha(matches[0].get("sha"), 40), "SOURCE_MISMATCH", "invalid Tenants gitlink " + path)
        links[name] = matches[0]["sha"]
    return links


def timestamp(value):
    C.require(isinstance(value, str) and re.fullmatch(r"[0-9]{4}-[0-9]{2}-[0-9]{2}T[0-9:]{8}(?:\.[0-9]{1,9})?Z", value),
              "BUSINESS_PREREQUISITE_MISSING", "invalid evidence timestamp")
    try:
        return dt.datetime.fromisoformat(value[:-1] + "+00:00")
    except ValueError as error:
        raise C.Refusal("BUSINESS_PREREQUISITE_MISSING", "invalid evidence timestamp") from error


def live_artifact(inventory, name, pin, run):
    matches = [a for a in inventory if a.get("name") == name and a.get("id") == pin["id"]]
    C.require(len(matches) == 1, "ARTIFACT_INVALID", "missing or ambiguous " + name)
    a = matches[0]
    C.require(a.get("expired") is False and a.get("digest") == "sha256:" + pin["sha256"]
              and C.positive(a.get("size_in_bytes")) and a["size_in_bytes"] <= MAX_ARCHIVE
              and a.get("workflow_run", {}).get("id") == run["run_id"] and a["workflow_run"].get("head_sha") == run["head_sha"]
              and timestamp(a.get("expires_at")) > dt.datetime.now(dt.timezone.utc),
              "ARTIFACT_INVALID", "wrong artifact identity, source or expiry for " + name)
    return a


def artifact_files(api, inventory, name, pin, run, job, filenames, *, project_paths=False):
    a = live_artifact(inventory, name, pin, run)
    start, end = timestamp(job.get("started_at")), timestamp(job.get("completed_at"))
    C.require(a["size_in_bytes"] == job["verified_upload_sizes"][name] and start <= timestamp(a.get("created_at")) <= end,
              "ARTIFACT_INVALID", "wrong artifact size or attempt interval for " + name)
    data, _ = api.fetch(api.prefix + f"/actions/artifacts/{pin['id']}/zip", binary=True, binary_limit=MAX_ARCHIVE)
    C.require(len(data) == a["size_in_bytes"] and C.digest(data) == pin["sha256"],
              "ARTIFACT_INVALID", "downloaded artifact differs from pin: " + name)
    try:
        with zipfile.ZipFile(io.BytesIO(data)) as archive:
            entries = archive.infolist()
            C.require(0 < len(entries) <= 16000 and len({e.filename for e in entries}) == len(entries),
                      "ARTIFACT_INVALID", "ambiguous artifact members: " + name)
            files = {}
            for entry in entries:
                path = entry.filename
                C.require(C.M.repository_path(path.rstrip("/")) and not entry.flag_bits & 1 and
                          (entry.external_attr >> 16) & 0o170000 not in (0o120000, 0o060000),
                          "ARTIFACT_INVALID", "unsafe archive member")
                basename = PurePosixPath(path).name
                if entry.is_dir() or basename not in filenames:
                    continue
                if project_paths:
                    C.require(path == basename.removesuffix(".trx") + "/" + basename,
                              "ARTIFACT_INVALID", "wrong Release report path: " + path)
                C.require(basename not in files and 0 < entry.file_size <= (C.MAX_BYTES if basename.endswith(".json") else MAX_REPORT),
                          "ARTIFACT_INVALID", "duplicate or oversized " + basename)
                files[basename] = archive.read(entry)
            C.require(set(files) == set(filenames), "ARTIFACT_INVALID", "missing required report in " + name)
            if name == SOURCE_PROFILE:
                C.require({e.filename for e in entries} == set(filenames), "ARTIFACT_INVALID", "wrong owner receipt archive")
            return files
    except (zipfile.BadZipFile, OSError, RuntimeError, NotImplementedError, zlib.error) as error:
        raise C.Refusal("ARTIFACT_INVALID", "malformed archive " + name) from error


def check_cases(filename, content):
    # xUnit TRX rows link the individual theory results to their compiled
    # class/method through testId. A passing aggregate count cannot prove a case.
    evidence(b"<!DOCTYPE" not in content.upper() and b"<!ENTITY" not in content.upper(), "unsafe TRX " + filename)
    try:
        root = ET.fromstring(content)
    except (ET.ParseError, LookupError, ValueError) as error:
        raise C.Refusal("ARTIFACT_INVALID", "malformed TRX " + filename) from error
    evidence(root.tag.rsplit("}", 1)[-1] == "TestRun", "missing TRX test run " + filename)
    special = {"authenticated-rest-rest.trx": "Hexalith.Tenants.IntegrationTests",
               "aspire-results.trx": "Hexalith.Tenants.IntegrationTests",
               "live-sidecar-results.trx": "Hexalith.EventStore.Server.LiveSidecar.Tests",
               "published-compatibility.trx": "Hexalith.EventStore.PublishedCompatibility.Tests"}
    assembly = special.get(filename, filename.removesuffix(".trx"))
    assembly_path = re.compile(r"(?:/[^/]+)*/tests/" + re.escape(assembly) + "/bin/Release/net[0-9]+[.][0-9]+/"
                               + re.escape(assembly) + "[.]dll")
    methods = {}
    for unit in root.findall("./{*}TestDefinitions/{*}UnitTest"):
        method = unit.find("{*}TestMethod")
        identifier = unit.get("id")
        evidence(identifier and identifier not in methods and method is not None, "ambiguous TRX definition in " + filename)
        cls = method.get("className")
        if cls in CASES[filename]:
            storage = unit.get("storage")
            evidence(isinstance(storage, str) and storage == method.get("codeBase") and ".." not in PurePosixPath(storage).parts
                     and assembly_path.fullmatch(storage), "wrong Release assembly for " + cls)
        methods[identifier] = (cls, method.get("name"))
    counts = Counter()
    classes = Counter()
    seen = set()
    required_labels = set()
    arguments = set()
    for result in root.findall("./{*}Results/{*}UnitTestResult"):
        identifier = result.get("testId")
        evidence(identifier in methods, "unbound TRX result in " + filename)
        cls, method = methods[identifier]
        if cls not in CASES[filename]:
            continue
        label = result.get("testName")
        evidence(label and identifier not in seen and result.get("outcome") == "Passed",
                 f"missing, skipped, duplicate or unsuccessful {cls}.{method}")
        seen.add(identifier)
        if method in CASES[filename][cls][1]:
            name = cls + "." + method
            evidence(label == name or (label.startswith(name + "(") and label.endswith(")")),
                     "TRX row differs from compiled method " + name)
            evidence((cls, label) not in required_labels, "duplicate required case " + label)
            required_labels.add((cls, label))
            if method in REQUIRED_ARGUMENTS:
                arguments.add((cls, method, label[len(name) + 1:-1].replace(r'\"', '"')))
        classes[cls] += 1
        counts[(cls, method)] += 1
    for cls, (minimum, required) in CASES[filename].items():
        for method, required_rows in required.items():
            evidence(counts[(cls, method)] >= required_rows, f"missing required result {cls}.{method}")
            for args in REQUIRED_ARGUMENTS.get(method, ()):
                evidence((cls, method, args) in arguments, f"missing required case {cls}.{method}({args})")
        evidence(classes[cls] >= minimum, f"fewer than {minimum} passing cases in {cls}")


def document(data):
    try:
        obj = C.parse(data)
    except C.Refusal as error:
        raise C.Refusal("ARTIFACT_INVALID", "malformed business receipt") from error
    C.require(isinstance(obj, dict), "ARTIFACT_INVALID", "business receipt must be an object")
    return obj


def owner_receipts(files, claim, source_job):
    backend, runtime = document(files[BACKEND]), document(files[RUNTIME])
    run = claim["runs"]["tenants_source"]
    sdk = claim["runs"]["eventstore_ci"]["head_sha"]
    producer = backend.get("sdkPackageProducer", {})
    evidence(backend.get("schema") == "tenants.catalog-profile.v1" and backend.get("identity") == {
        "repository": C.TENANTS, "runId": str(run["run_id"]), "runAttempt": str(run["attempt"]), "job": "development-catalog"},
        "backend receipt belongs to a different source job")
    evidence(backend.get("buildsSource") == claim["builds_head"]
             and backend.get("sdkSource") == runtime.get("sdkSource") == sdk == producer.get("revision")
             and producer.get("id") == "Hexalith.EventStore.Aspire"
             and producer.get("version") == backend.get("sdkProducerVersion") == runtime.get("sdkProducerVersion") == claim["sdk_version"],
             "source receipt producer, version or Builds mismatch")
    checksum = producer.get("sha512")
    evidence(isinstance(checksum, str) and re.fullmatch(r"[A-Za-z0-9+/]{85}[AQgw]==", checksum)
             and len(base64.b64decode(checksum, validate=True)) == 64, "invalid SDK package SHA-512")
    evidence(backend.get("profile") == "catalog-owner" and type(backend.get("database")) is int and backend["database"] == 5
             and backend.get("endpoint") == "127.0.0.1:6379" and backend.get("materialProvisionedWithFreshBackend") is True
             and C.sha(backend.get("initializationSha256")), "missing fresh catalog-owner provisioning proof")
    private, receipt = backend.get("privateDirectory"), backend.get("initializationReceipt")
    evidence(isinstance(private, str) and isinstance(receipt, str) and PurePosixPath(private).is_absolute()
             and ".." not in PurePosixPath(private).parts and PurePosixPath(private).name.startswith("tenants-source-catalog-")
             and PurePosixPath(receipt) == PurePosixPath(private).parent / "hexalith-dapr-initialization.json",
             "missing job-owned initialization receipt identity")
    components = backend.get("components")
    evidence(isinstance(components, dict) and {"statestore.yaml", "pubsub.yaml", "configstore.yaml", "secrets.json"} <= components.keys()
             and all(C.sha(v) for v in components.values()), "incomplete component receipt")
    actual = backend.get("backend", {})
    evidence(C.sha(actual.get("containerId")) and isinstance(actual.get("imageId"), str)
             and re.fullmatch("sha256:[0-9a-f]{64}", actual["imageId"]) and C.sha(actual.get("redisRunId"), 40)
             and type(actual.get("initialDatabaseSize")) is int and actual["initialDatabaseSize"] == 0,
             "backend was not initially empty or lacks a Redis identity")
    start, end = timestamp(source_job["started_at"]), timestamp(source_job["completed_at"])
    volumes = actual.get("volumes")
    evidence(isinstance(volumes, list) and volumes and start <= timestamp(actual.get("created")) <= end
             and all(isinstance(v, dict) and C.sha(v.get("name")) and start <= timestamp(v.get("created")) <= end for v in volumes)
             and len({v["name"] for v in volumes}) == len(volumes), "backend container or volume was not fresh in this job")
    evidence(C.sha(runtime.get("AttemptId"), 32) and C.sha(runtime.get("RootDigest")) and C.sha(runtime.get("activationBindingDigest"))
             and C.positive(runtime.get("Epoch")) and C.positive(runtime.get("generation")), "incomplete activation receipt")
    hosts = runtime.get("requiredHosts")
    evidence(isinstance(hosts, list) and len(hosts) == 4 and all(isinstance(h, dict) for h in hosts)
             and {h.get("HostId") for h in hosts} == {"eventstore", "eventstore-admin", "tenants", "tenants-api"}
             and all(h.get("AppId") == h["HostId"] and all(C.sha(h.get(k)) for k in (
                 "InventoryDigest", "TopologyDigest", "AuthenticationFingerprint")) and
                 (h["HostId"] != "tenants" or C.sha(h.get("ExecutableDigest"))) for h in hosts),
             "incomplete four-host activation")
    for key in OUTCOMES:
        evidence(runtime.get(key) is True, "source runtime failed " + key)
    for key, number in (("operations", 24), ("realRestReads", 7), ("outstandingLeases", 0)):
        evidence(type(runtime.get(key)) is int and runtime[key] == number, "source runtime failed " + key)
    return producer


def package_cohort(content, claim, producer):
    package = document(content)
    evidence(package.get("sdkSource") == producer["revision"] and package.get("sdkPackageProducer") == producer,
             "package lane did not resolve the source receipt producer")
    manifests = package.get("runtimeManifests")
    expected_hosts = {"Hexalith.Tenants", "Hexalith.Tenants.Api", "Hexalith.Tenants.UI", "Hexalith.Tenants.AppHost"}
    evidence(isinstance(manifests, dict) and set(manifests) == expected_hosts, "incomplete Release host manifests")
    for name, manifest in manifests.items():
        expected = {"Hexalith.EventStore.Contracts": claim["sdk_version"], "Hexalith.McpCli.Abstractions": claim["mcpcli_version"]}
        if name == "Hexalith.Tenants.AppHost":
            expected[producer["id"]] = claim["sdk_version"]
        evidence(isinstance(manifest, dict) and isinstance(manifest.get("runtimeTarget"), str)
                 and manifest["runtimeTarget"] and C.sha(manifest.get("manifestSha256")) and manifest.get("packages") == expected,
                 "resolved package cohort differs for " + name)


def verify(control):
    claim = control.request["tenant_qualification"]
    tenants = C.GitHub(C.TENANTS)
    links = gitlinks(tenants, claim["tenants_head"])
    C.require(links == {"EventStore": control.head, "Builds": claim["builds_head"]}, "SOURCE_MISMATCH",
              "Tenants SDK or Builds gitlink differs from the approved qualification")
    for lane in LANES:
        pin = claim["runs"][lane]
        api = control.api if pin["repository"] == C.REPOSITORY else tenants
        jobs = authenticate_jobs(api, lane, pin)
        inventory = api.pages(f"/actions/runs/{pin['run_id']}/artifacts?per_page=100", "artifacts")
        for name, job in ARTIFACT_JOBS[lane].items():
            reports = REPORTS.get(name, ())
            filenames = (BACKEND, RUNTIME) if name == SOURCE_PROFILE else (*reports, PACKAGE) if name == REST_RESULTS else reports
            files = artifact_files(api, inventory, name, pin["artifacts"][name], pin, jobs[job], filenames)
            for report in reports:
                check_cases(report, files[report])
            if name == SOURCE_PROFILE:
                producer = owner_receipts(files, claim, jobs[job])
            if name == REST_RESULTS:
                package_cohort(files[PACKAGE], claim, producer)
    for lane, pin in claim["runs"].items():
        api = control.api if pin["repository"] == C.REPOSITORY else tenants
        latest_attempt(api, lane, pin)
