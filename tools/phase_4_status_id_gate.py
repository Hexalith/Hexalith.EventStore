"""Authenticate Story 2.15's compiled Release cases for the generated command status link.

The fixed owner inventory covers 65 grammar cases, two generator source assertions,
71 generated action cases, 20 Sample runtime cases and eight Server catalog cases.
When the API and Test owners change this inventory, capture fresh Release TRXs and
update CASES, the schema's passing-case lists and the reporter regression together.
"""

import json
from pathlib import PurePosixPath
import re
import xml.etree.ElementTree as ET

import phase_4_tenant_gate as T
import phase_4_transition as C

GATE = "G-STATUS-ID"
PATH = "tools/phase_4_status_id_gate.py"
ADAPTER = "generated-command-status-id-v1"
DECLARATION = "444d0155057749a0337aa7ce7044b1203b5f6468125c3eeb747a7bbdfae278ba"
WORKFLOW = ".github/workflows/ci.yml"
TRX_NAMESPACE = "http://microsoft.com/schemas/VisualStudio/TeamTest/2010"
JOBS = T.JOBS["eventstore_ci"]
ARTIFACT_JOBS = T.ARTIFACT_JOBS["eventstore_ci"]

CONTRACTS = "Hexalith.EventStore.Contracts.Tests"
GENERATORS = "Hexalith.EventStore.RestApi.Generators.Tests"
SAMPLE = "Hexalith.EventStore.Sample.Tests"
SERVER = "Hexalith.EventStore.Server.Tests"
GRAMMAR = CONTRACTS + ".Identity.MessageIdGrammarTests"
SOURCE = GENERATORS + ".RestApiControllerGenerationTests"
RUNTIME = GENERATORS + ".RestApiGeneratedControllerErrorSemanticsTests"
SAMPLE_RUNTIME = SAMPLE + ".SampleApi.SampleApiGeneratedControllerRuntimeTests"
CATALOG = SERVER + ".Routing.GeneratedCatalogDeclarationTests"
REPORTS = {"contracts-test-results": (CONTRACTS,), "blocking-test-results": (GENERATORS, SAMPLE, SERVER)}

ULID = "01ARZ3NDEKTSV4RRFFQ69G5FAV"
OTHER = "01BX5ZZKBKACTAV9WEVGEMMVRZ"
LOWER = ULID.lower()
LEGACY = "legacy-status-1"
GUID = "3f2504e0-4f89-11d3-9a0c-0305e82c3301"
ULID_BOUNDS = ("0" * 26, "7" + "Z" * 25)
VALID_V1 = (LEGACY, "a", "A-b-9", "Mixed-Case-Legacy-42", ULID, LOWER, GUID)
INVALID_V1 = ("-legacy", "legacy-", "-", "legacy status", " legacy", "legacy_status", "legacy.status",
              "legacy/status", "legacy%2Fstatus", "légacy", "ａbc", "legacy\n")
INVALID_V2 = (LOWER, ULID[:-1] + "v", *(ULID[:-1] + ch for ch in "ILOU"), "8" + "Z" * 25, "Z" * 26,
              ULID[:-1], ULID + "X", " " + ULID[:-1], ULID[:-1] + " ", "01ARZ3NDEK-SV4RRFFQ69G5FAV", LEGACY, GUID)


def arguments(parameters, rows):
    # The xUnit TRX reporter escapes its display text a second time, including quotes and backslashes.
    return {json.dumps("(" + text + ")", ensure_ascii=False)[1:-1] for text in T.theory(parameters, rows)}


def values(parameter, items):
    return arguments(parameter, ((value,) for value in items))


def facts(*names):
    return {name: {""} for name in names}


PAIR = ((1, LEGACY), (2, ULID))
VALID_IDS = [(1, value) for value in VALID_V1] + [(2, value) for value in (ULID, OTHER, *ULID_BOUNDS)]
INVALID_IDS = [(1, value) for value in INVALID_V1] + [(2, value) for value in INVALID_V2]
CASES = {
    CONTRACTS: {GRAMMAR: {
        **facts("LegacyIdentifierAtMaximumLengthIsValidUnderVersionOne",
                "LegacyIdentifierLongerThanMaximumIsInvalidUnderVersionOne", "GeneratedSortableIdentifierIsValidUnderVersionTwo"),
        "LegacyIdentifierIsValidUnderVersionOne": values("messageId", (*VALID_V1, "Z", "7", "a--b")),
        "MalformedIdentifierIsInvalidUnderVersionOne": values("messageId", (*INVALID_V1, None, "", " ", "-legacy-",
                                                                          "legacy ", "legacy:status", "legacy\t1", "١")),
        "CanonicalUppercaseUlidIsValidUnderVersionTwo": values("messageId", (ULID, OTHER, *ULID_BOUNDS,
                                                                           "0123456789ABCDEFGHJKMNPQRS", "0TVWXYZ0000000000000000000")),
        "NonCanonicalOrLegacyIdentifierIsInvalidUnderVersionTwo": values("messageId", (*INVALID_V2, None, "", " ",
                "I" + ULID[1:], "O" + ULID[1:], ULID[:-1] + "٢")),
        "UndeclaredVersionAcceptsNoIdentifier": values("version", (0, 3, -1, -2147483648, 2147483647)),
    }},
    GENERATORS: {SOURCE: facts("Run_HappyPathControllerSource_GeneratedControllerCompilesAndUsesGatewayBoundary",
                              "Run_ReferencedCommandContract_GeneratesRouteTenantPostAction"),
                 RUNTIME: {
        "CommandStatus_MessageIdValidForDeclaredVersion_EmitsAbsoluteLocationFromMessageIdOnly":
            arguments("version messageId", VALID_IDS) | {" [v1 length 128]"},
        "CommandStatus_MessageIdInvalidForDeclaredVersion_ReturnsAcceptedWithoutLocation":
            arguments("version messageId", INVALID_IDS) | {" [v1 length 129]"},
        "CommandStatus_MessageIdMissingWithValidCorrelationIdOnly_ReturnsAcceptedWithoutLocation":
            arguments("version messageId", [(v, value) for v, blank in ((1, " "), (2, "   ")) for value in (None, "", blank)]),
        "CommandStatus_ValidMessageIdDiffersFromCorrelationId_LocationUsesMessageIdOnly":
            arguments("version messageId correlationId", [(1, LEGACY, ULID), (1, ULID, LEGACY), (2, ULID, OTHER), (2, OTHER, ULID)]),
        "CommandStatus_VersionUndeclaredOrAmbiguous_ReturnsAcceptedWithoutLocation":
            arguments("published version", [(False, None), *((True, value) for value in (None, "1", "2", True, 0, 3, -1, 2147483647))])
            | {f" [published {number}]" for number in ("Int64 1", "Int64 2", "Double 1", "Int16 1", "Byte 2")},
        "CommandStatus_CallerClaimsAnotherVersion_DeclaredVersionDecides":
            arguments("declared messageId claimed expectLocation", [(1, LEGACY, "2", True), (2, LEGACY, "1", False), (2, LOWER, "1", False)]),
        "CommandStatus_ValidMessageIdWithoutConfiguredStatusBase_ReturnsAcceptedWithoutLocation": arguments("version messageId", PAIR),
        "CommandStatus_GatewayFailureWithDeclaredVersion_ReturnsProblemWithoutLocation": arguments("version messageId", PAIR),
    }},
    SAMPLE: {SAMPLE_RUNTIME: {
        **facts("CounterQuery_WhenRouteTenantIsMissing_ReturnsBadRequestBeforeGatewayCall",
                "CounterQuery_WhenGatewayReturnsSuccess_ForwardsPayloadAndMetadataHeaders",
                "CounterQuery_WhenGatewayReturnsSuccessWithoutMetadata_OmitsFreshnessAndPagingHeaders",
                "CounterQuery_WhenGatewayReturnsNotModified_ForwardsIfNoneMatchAndStrongETag",
                "CounterQuery_WhenGatewayReturnsNotModifiedWithoutStrongETag_ReturnsBadGatewayProblem",
                "CounterQuery_WhenGatewayReturnsNotModifiedWithoutETag_ReturnsBadGatewayProblem",
                "IncrementCounter_WhenBodyMatchesRoute_SubmitsGatewayCommandAndReturnsAccepted",
                "IncrementCounter_WhenStatusBaseUnconfigured_SubmitsGatewayCommandAndOmitsLocation",
                "CounterCommands_WhenBodiesMatchRoute_SubmitExpectedGatewayCommands",
                "CounterCommands_WhenGatewayReturnsCorrelationIdOnly_ReturnAcceptedWithoutLocation",
                "CounterCommands_WhenBodyIsNull_ReturnBadRequestBeforeGatewayCall",
                "CounterCommands_WhenRouteAndBodyAggregateMismatch_ReturnBadRequestBeforeGatewayCall",
                "CounterQuery_WhenGatewayThrowsForbidden_ReturnsSafeProblemDetails",
                "CounterCommands_WhenGatewayThrowsForbidden_ReturnSafeProblemDetails"),
        "IncrementCounter_WhenMessageIdIsBlankOrInvalidForDeclaredVersion_ReturnsAcceptedWithoutLocation":
            values("messageId", ("", " ", "-invalid-for-v1", "invalid for v1")),
        "IncrementCounter_WhenMessageIdDiffersFromCorrelationId_LocationUsesDeclaredV1MessageIdOnly": values("messageId", (LEGACY, LOWER)),
    }},
    SERVER: {CATALOG: {
        **facts("EveryCatalogCommandDeclaresExactlyOneMessageIdVersion",
                "GeneratedCommandPublishesOnlyTheCompiledDeclarationMessageIdVersionForTheAction",
                "GeneratedCommandPublishesNoMessageIdVersionWhenValidationRefusesOrForQueries"),
        "GeneratedActionComparesFullCompiledDeclarationIncludingMessageIdVersion": values("drift", (False, True)),
        "ExactCatalogTenantComesOnlyFromTheGeneratedAction": arguments("exactTenant partitionTenant accepted", (
            ("tenant-a", "tenant-a", True), ("system", "system", False), ("tenant-a", "tenant-b", False))),
    }},
}


def validate_claim(claim, head):
    C.closed(claim, ("adapter", "adapter_sha256", "run"))
    C.require(claim["adapter"] == ADAPTER and C.sha(claim["adapter_sha256"]),
              "INPUT_INVALID", "invalid G-STATUS-ID verifier", 2)
    pin = claim["run"]
    C.closed(pin, ("repository", "run_id", "attempt", "head_sha", "workflow", "workflow_sha256", "artifacts"))
    C.require(pin["repository"] == C.REPOSITORY and pin["head_sha"] == head and pin["workflow"] == WORKFLOW
              and C.sha(pin["workflow_sha256"]) and C.positive(pin["run_id"]) and C.positive(pin["attempt"]),
              "RUN_INVALID", "wrong G-STATUS-ID repository, workflow, revision or attempt")
    C.closed(pin["artifacts"], ARTIFACT_JOBS)
    for item in pin["artifacts"].values():
        C.closed(item, ("id", "sha256"))
        C.require(C.positive(item["id"]) and C.sha(item["sha256"]), "INPUT_INVALID", "invalid status evidence artifact pin", 2)

    ids = [item["id"] for item in pin["artifacts"].values()]
    C.require(len(set(ids)) == len(ids), "ARTIFACT_INVALID", "status evidence artifacts share an ID")


def check_declaration(row, claim, paths):
    # The registry controls the assurance level. The matrix separately records prior attestations and seals.
    declaration = {key: value for key, value in row.items() if key not in ("required_level", "achieved_level", "attestations", "seal")}
    C.require(C.digest(C.canonical(declaration)) == DECLARATION, "MATRIX_INVALID", "fixed G-STATUS-ID declaration differs")
    C.require(claim["adapter_sha256"] == paths[PATH] and claim["run"]["workflow_sha256"] == paths[WORKFLOW],
              "SOURCE_MISMATCH", "G-STATUS-ID verifier or CI workflow differs from approval")


def check_cases(assembly, content):
    def require(condition, message):
        C.require(condition, "BUSINESS_PREREQUISITE_MISSING", f"{GATE} {assembly}: {message}")

    try:
        xml = content.decode("utf-8-sig")
    except UnicodeDecodeError as error:
        raise C.Refusal("ARTIFACT_INVALID", "expected a UTF-8 xUnit report for " + assembly) from error
    require("\x00" not in xml and "<!DOCTYPE" not in xml.upper() and "<!ENTITY" not in xml.upper(), "unsafe TRX")
    try:
        root = ET.fromstring(xml)
    except (ValueError, LookupError, ET.ParseError) as error:
        raise C.Refusal("ARTIFACT_INVALID", "malformed TRX for " + assembly) from error
    ns = "{" + TRX_NAMESPACE + "}"
    require(root.tag == ns + "TestRun" and all(node.tag.startswith(ns) for node in root.iter()),
            "wrong TRX namespace or test run")
    require(len(root.findall(ns + "TestDefinitions")) == len(root.findall(ns + "Results")) == 1,
            "ambiguous TRX definitions or results")
    assembly_path = re.compile(r"(?:/[^/]+)*/tests/" + re.escape(assembly) + r"/bin/Release/net11\.0/"
                               + re.escape(assembly) + r"\.dll")
    storage_paths = set()
    expected = {cls + "." + method + suffix for cls, methods in CASES[assembly].items()
                for method, suffixes in methods.items() for suffix in suffixes}
    definitions = set()
    scoped = {}
    for unit in root.findall("./{*}TestDefinitions/{*}UnitTest"):
        uid = unit.get("id")
        methods = unit.findall("{*}TestMethod")
        require(uid and uid not in definitions and len(methods) == 1, "ambiguous test definition")
        method = methods[0]
        cls, name = method.get("className"), method.get("name")
        definitions.add(uid)
        storage = unit.get("storage", "")
        storage_paths.add(storage)
        require(storage == method.get("codeBase") and ".." not in PurePosixPath(storage).parts
                and assembly_path.fullmatch(storage) and len(storage_paths) == 1, "wrong or mixed Release assembly paths")
        if cls not in CASES[assembly] or (cls == SOURCE and name not in CASES[assembly][cls]):
            continue
        if cls == RUNTIME and not (name or "").startswith("CommandStatus_"):
            continue
        label = unit.get("name")
        require(name in CASES[assembly][cls] and label in {cls + "." + name + s for s in CASES[assembly][cls][name]},
                "unexpected compiled case " + str(label))
        execution = unit.findall("{*}Execution")
        require(len(execution) == 1 and execution[0].get("id"), "missing execution for " + label)
        scoped[uid] = (label, execution[0].get("id"))

    required_executions = {execution for _, execution in scoped.values()}
    passed = set()
    seen = set()
    executions = set()
    for result in root.findall("./{*}Results/{*}UnitTestResult"):
        uid = result.get("testId")
        require(uid in definitions, "unbound TRX result")
        if uid not in scoped:
            require(result.get("executionId") not in required_executions, "required execution ID reused by an unrelated result")
            require(result.get("testName") not in expected, "required result belongs to a different compiled test")
            continue
        label, execution = scoped[uid]
        require(result.get("executionId") == execution and result.get("testName") == label, "unlinked result for " + label)
        require(label not in passed and execution not in executions, "duplicate result for " + label)
        require(result.get("outcome") == "Passed", "unsuccessful or skipped " + label)
        passed.add(label)
        executions.add(execution)
        seen.add(uid)
    missing = expected - passed
    if missing:
        raise C.Refusal("BUSINESS_PREREQUISITE_MISSING", f"{GATE} missing required case {min(missing)}")
    require(seen == scoped.keys(), "compiled case lacks a result")
    return {"sha256": C.digest(content), "passing_cases": sorted(passed)}


def current_attempt(control):
    T.latest_attempt(control.api, GATE, control.request["status_qualification"]["run"], require_run_success=False)


def reconfirm(control):
    pin = control.request["status_qualification"]["run"]
    inventory = control.api.pages(f"/actions/runs/{pin['run_id']}/artifacts?per_page=100", "artifacts")
    for name in ARTIFACT_JOBS:
        T.live_artifact(inventory, name, pin["artifacts"][name], pin)
    current_attempt(control)


def verify(control):
    claim = control.request["status_qualification"]
    pin = claim["run"]
    api = control.api
    jobs = T.authenticate_workflow_jobs(api, GATE, pin, C.REPOSITORY, WORKFLOW, JOBS, ARTIFACT_JOBS, require_run_success=False)
    C.require(len({job["id"] for job in jobs.values()}) == len(JOBS)
              and len({job["check_run_url"] for job in jobs.values()}) == len(JOBS),
              "CI_UNAUTHENTICATED", "G-STATUS-ID requires two distinct Actions jobs and checks")
    inventory = api.pages(f"/actions/runs/{pin['run_id']}/artifacts?per_page=100", "artifacts")
    reports, producers = {}, {}
    for artifact, assemblies in REPORTS.items():
        job = jobs[ARTIFACT_JOBS[artifact]]
        files = T.artifact_files(api, inventory, artifact, pin["artifacts"][artifact], pin, job,
                                 [a + ".trx" for a in assemblies], project_paths=True)
        producers[artifact] = {"job": job["name"], "job_id": job["id"], "check_run_url": job["check_run_url"]}
        for assembly in assemblies:
            filename = assembly + ".trx"
            reports[filename] = check_cases(assembly, files[filename])
    current_attempt(control)
    return {"claim": claim, "declaration_sha256": DECLARATION, "producers": producers, "reports": reports}
