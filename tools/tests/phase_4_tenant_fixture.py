"""Synthetic Actions objects and receipts; all business checks run in the real adapter."""

import base64
import copy
import hashlib
import io
import json
from pathlib import Path
import urllib.error
import xml.etree.ElementTree as ET
import zipfile

import phase_4_tenant_gate as A
import phase_4_transport_fixture as HTTP

TENANTS = "Hexalith/Hexalith.Tenants"
TENANTS_HEAD = "d" * 40
BUILDS = "e" * 40
TREE = "f" * 40
START = "2020-01-01T00:00:00Z"
CREATED = "2020-01-01T00:05:00Z"
END = "2020-01-01T00:15:00Z"
DIGEST = "a" * 64
HOSTS = ("eventstore", "eventstore-admin", "tenants", "tenants-api")
SERVICES = ("Hexalith.Tenants", "Hexalith.Tenants.Api", "Hexalith.Tenants.UI", "Hexalith.Tenants.AppHost")


def prefix(repo):
    return "https://api.github.com/repos/" + repo


class Transport:
    def __init__(self, state):
        self.state = state
        self.auth = {}

    def open(self, request, timeout=20):
        self.state["observed"].append(request.full_url)
        self.auth[request.full_url] = request.get_header("Authorization")
        record = self.state["responses"].get(request.full_url)
        if record and "sequence" in record:
            record = record["sequence"].pop(0)
        if not record:
            raise urllib.error.HTTPError(request.full_url, 404, "missing fixture response", {}, None)
        body = base64.b64decode(record["binary"]) if "binary" in record else json.dumps(record["json"]).encode()
        return HTTP.Response(body, record.get("headers"))


def trx(filename):
    root = ET.Element("TestRun")
    definitions = ET.SubElement(root, "TestDefinitions")
    results = ET.SubElement(root, "Results")
    assemblies = {"aspire-results.trx": "Hexalith.Tenants.IntegrationTests",
                  "authenticated-rest-rest.trx": "Hexalith.Tenants.IntegrationTests",
                  "live-sidecar-results.trx": "Hexalith.EventStore.Server.LiveSidecar.Tests",
                  "published-compatibility.trx": "Hexalith.EventStore.PublishedCompatibility.Tests"}
    assembly = assemblies.get(filename, filename.removesuffix(".trx"))
    path = f"/fixture/tests/{assembly}/bin/Release/net11.0/{assembly}.dll"
    for cls, (minimum, methods) in A.CASES[filename].items():
        methods = methods.copy()
        if cls == "Hexalith.EventStore.Server.Tests.Controllers.WorkloadGatewayControllerTests":
            methods["Bound_command_requires_exact_signed_requester_and_domain"] = 1
        rows = [method for method, count in methods.items() for _ in range(count)]
        rows.extend(["AnotherTest"] * max(0, minimum - len(rows)))
        cases = {key: iter(sorted(values)) for key, values in A.REQUIRED_ARGUMENTS.items()}
        for index, method in enumerate(rows):
            args = next(cases[method]) if method in cases else f"row: {index}"
            label = f"{cls}.{method}({args})"
            uid = f"{cls}-{index}"
            unit = ET.SubElement(definitions, "UnitTest", id=uid, storage=path)
            ET.SubElement(unit, "TestMethod", className=cls, name=method, codeBase=path)
            ET.SubElement(results, "UnitTestResult", testId=uid, testName=label, outcome="Passed")
    return ET.tostring(root)


class Qualification:
    def __init__(self, fixture):
        import test_phase_4_transition as T
        self.f = f = fixture
        self.files = {}
        self.logs = {}
        rows = json.loads((f.root / T.MATRIX).read_text())["gates"]
        f.row = next(row for row in rows if row["gate"] == "G-TENANT")
        for path in (*f.row["subject_paths"], A.PATH):
            dest = f.root / path
            dest.parent.mkdir(parents=True, exist_ok=True)
            # Keep simulated subjects small enough for the subprocess HTTP fixture.
            if not dest.exists():
                dest.write_bytes((T.ROOT / path).read_bytes() if path == A.PATH or path.endswith(".yml")
                                 else ("fixture subject " + path).encode())
            f.blob(T.HEAD, path, dest.read_bytes())
        f.request.update(gate="G-TENANT", subjects=[f.file(p) for p in f.row["subject_paths"]], evidence=[], predecessors=[])
        f.put("/activity?ref=refs%2Fheads%2Fmain&per_page=100", [f.activity(T.HEAD, "0" * 40, "2026-01-01T00:00:00Z")])
        self.claim = {"adapter": "tenant-boundary-v1", "adapter_sha256": f.file(A.PATH)["sha256"], "tenants_head": TENANTS_HEAD,
                      "builds_head": BUILDS, "sdk_version": "8.2.1", "mcpcli_version": "6.3.1", "runs": {}}
        f.request["tenant_qualification"] = self.claim
        self.put(TENANTS, "/git/commits/" + TENANTS_HEAD, {"sha": TENANTS_HEAD, "tree": {"sha": TREE}})
        self.put(TENANTS, "/git/trees/" + TREE + "?recursive=1", {"sha": TREE, "truncated": False, "tree": [
            {"path": "references/Hexalith." + name, "mode": "160000", "type": "commit", "sha": sha}
            for name, sha in (("EventStore", T.HEAD), ("Builds", BUILDS))]})
        self.producer = {"id": "Hexalith.EventStore.Aspire", "version": "8.2.1", "revision": T.HEAD,
                         "sha512": base64.b64encode(bytes(range(64))).decode()}
        self.runs = self.claim["runs"]
        for i, (lane, (repo, workflow)) in enumerate(A.LANES.items(), 1):
            self.install_lane(i, lane, repo, workflow)
        f.approve()

    def install_lane(self, i, lane, repo, workflow, report_files=None):
        import test_phase_4_transition as T
        f = self.f
        run_id = 1000 + i
        head = T.HEAD if repo == T.REPO else TENANTS_HEAD
        content = (f.root / workflow).read_bytes() if repo == T.REPO else ("workflow fixture " + workflow).encode()
        if repo == TENANTS:
            self.put(repo, "/contents/" + workflow + "?ref=" + head, {
                "type": "file", "path": workflow, "encoding": "base64", "content": base64.b64encode(content).decode(),
                "size": len(content), "sha": hashlib.sha1(b"blob " + str(len(content)).encode() + b"\0" + content).hexdigest()})
        pin = {"repository": repo, "head_sha": head, "workflow": workflow, "workflow_sha256": T.hash_bytes(content),
               "run_id": run_id, "attempt": 1, "artifacts": {}}
        self.runs[lane] = pin
        success = {"status": "completed", "conclusion": "success"}
        record = {**success, "id": run_id, "run_attempt": 1, "head_sha": head, "head_branch": "main", "event": "push",
                  "repository": {"full_name": repo}, "head_repository": {"full_name": repo}, "path": workflow,
                  "workflow_id": i, "check_suite_id": i}
        self.put(repo, f"/actions/runs/{run_id}", copy.deepcopy(record))
        self.put(repo, f"/actions/runs/{run_id}/attempts/1", record)
        self.put(repo, f"/actions/workflows/{i}", {"id": i, "path": workflow, "state": "active"})
        action = {"id": 15368, "slug": "github-actions"}
        self.put(repo, f"/check-suites/{i}", {**success, "id": i, "head_sha": head, "app": action})
        jobs, checks = [], []
        required = A.JOBS[lane].copy()
        if lane == "tenants_source":
            required["Actual source catalog owner profile"] = (
                "Record the actual selected SDK source and producer version",
                "Prove fresh backend ownership before provisioning private owner material",
                "Require bootstrap bearer validation and bound workload refusals",
                "Require actual cohort binding, owned bootstrap, persisted effects and refusal")
        for j, (job, steps) in enumerate(required.items(), i * 100):
            check_url = prefix(repo) + f"/check-runs/{j}"
            checkout_name = {"live-sidecar": "Checkout repository", "published-compatibility": "Checkout exact executor and comparison tags"}.get(
                job, "Run actions/checkout@" + "1" * 40)
            jobs.append({**success, "id": j, "run_attempt": 1, "run_id": run_id, "head_sha": head, "name": job,
                         "check_run_url": check_url, "started_at": START, "completed_at": END,
                         "steps": [{**success, "name": checkout_name, "number": 3, "started_at": START,
                                    "completed_at": START}, *[{**success, "name": s} for s in steps]]})
            self.logs[j] = self.log_lines(START, ["##[group]Run actions/checkout@" + "1" * 40, "with:",
                          "  repository: " + repo, "##[endgroup]", "Syncing repository: " + repo,
                          "[command]/usr/bin/git log -1 --format=%H", head])
            self.set_log(repo, j, self.logs[j])
            checks.append({**success, "id": j, "name": job, "url": check_url, "app": action,
                           "head_sha": head, "check_suite": {"id": i}})
        self.put(repo, f"/actions/runs/{run_id}/attempts/1/jobs?per_page=100", {"total_count": len(jobs), "jobs": jobs})
        self.put(repo, f"/check-suites/{i}/check-runs?filter=all&per_page=100", {"total_count": len(checks), "check_runs": checks})
        self.put(repo, f"/actions/runs/{run_id}/artifacts?per_page=100", {"artifacts": [], "total_count": 0})
        for j, name in enumerate(A.ARTIFACT_JOBS[lane], i * 100):
            files = report_files[name] if report_files else {file: trx(file) for file in A.REPORTS.get(name, ())}
            if name == A.SOURCE_PROFILE:
                files.update(self.receipts(T.HEAD, run_id))
            if name == A.REST_RESULTS:
                manifests = {host: {"runtimeTarget": ".NETCoreApp,Version=v11.0", "manifestSha256": DIGEST,
                             "packages": {"Hexalith.EventStore.Contracts": "8.2.1", "Hexalith.McpCli.Abstractions": "6.3.1"}}
                             for host in SERVICES}
                manifests["Hexalith.Tenants.AppHost"]["packages"][self.producer["id"]] = "8.2.1"
                files[A.PACKAGE] = T.canonical({"sdkSource": T.HEAD, "sdkPackageProducer": self.producer, "runtimeManifests": manifests})
            self.files[name] = files
            self.publish(lane, name, j)

    def put(self, repo, endpoint, document):
        self.f.state["responses"][prefix(repo) + endpoint] = {"json": document}

    def document(self, lane, endpoint):
        return self.f.state["responses"][prefix(A.LANES[lane][0]) + endpoint]["json"]

    def receipts(self, sdk, run_id):
        runtime = {"sdkSource": sdk, "sdkProducerVersion": "8.2.1", "AttemptId": "b" * 32, "RootDigest": DIGEST,
                   "activationBindingDigest": DIGEST, "Epoch": 1, "generation": 1, "operations": 24, "realRestReads": 7,
                   "outstandingLeases": 0, **{key: True for key in A.OUTCOMES}, "requiredHosts": [
                   {"HostId": host, "AppId": host, "InventoryDigest": DIGEST, "TopologyDigest": DIGEST,
                    "AuthenticationFingerprint": DIGEST, **({"ExecutableDigest": DIGEST} if host == "tenants" else {})} for host in HOSTS]}
        backend = {"schema": "tenants.catalog-profile.v1", "identity": {"repository": TENANTS, "runId": str(run_id),
                   "runAttempt": "1", "job": "development-catalog"}, "sdkSource": sdk, "sdkProducerVersion": "8.2.1",
                   "sdkPackageProducer": self.producer, "buildsSource": BUILDS, "profile": "catalog-owner", "database": 5,
                   "endpoint": "127.0.0.1:6379", "materialProvisionedWithFreshBackend": True,
                   "privateDirectory": "/job/tenants-source-catalog-abc", "initializationReceipt": "/job/hexalith-dapr-initialization.json",
                   "initializationSha256": DIGEST, "components": {key: DIGEST for key in
                   ("statestore.yaml", "pubsub.yaml", "configstore.yaml", "secrets.json")},
                   "backend": {"containerId": DIGEST, "imageId": "sha256:" + DIGEST, "redisRunId": "a" * 40,
                               "created": CREATED, "initialDatabaseSize": 0, "volumes": [{"name": DIGEST, "created": CREATED}]}}
        return {A.BACKEND: json.dumps(backend).encode(), A.RUNTIME: json.dumps(runtime).encode()}

    def publish(self, lane, name, artifact_id):
        pin = self.runs[lane]
        buf = io.BytesIO()
        with zipfile.ZipFile(buf, "w", compression=zipfile.ZIP_DEFLATED) as archive:
            for path, content in self.files[name].items():
                archive.writestr(path, content)
        data = buf.getvalue()
        sha = hashlib.sha256(data).hexdigest()
        pin["artifacts"][name] = {"id": artifact_id, "sha256": sha}
        repo, run_id = pin["repository"], pin["run_id"]
        listing = self.document(lane, f"/actions/runs/{run_id}/artifacts?per_page=100")
        listing["artifacts"] = [a for a in listing["artifacts"] if a["name"] != name]
        listing["artifacts"].append({"name": name, "id": artifact_id, "expired": False, "digest": "sha256:" + sha,
                                    "workflow_run": {"id": run_id, "head_sha": pin["head_sha"]}, "created_at": CREATED,
                                    "expires_at": "2099-01-01T00:00:00Z", "size_in_bytes": len(data)})
        listing["total_count"] = len(listing["artifacts"])
        self.f.state["responses"][prefix(repo) + f"/actions/artifacts/{artifact_id}/zip"] = {"binary": base64.b64encode(data).decode()}
        jobname = A.ARTIFACT_JOBS[lane][name]
        job = next(j for j in self.document(lane, f"/actions/runs/{run_id}/attempts/1/jobs?per_page=100")["jobs"] if j["name"] == jobname)
        upload = A.UPLOAD_STEPS[name]
        if not any(s["name"] == upload for s in job["steps"]):
            job["steps"].append({"name": upload, "started_at": CREATED, "completed_at": CREATED,
                                 "status": "completed", "conclusion": "success"})
        lines = ["##[group]Run actions/upload-artifact@" + "2" * 40, "with:", "  name: " + name]
        if name == "tenants-source-catalog-owner-profile":
            lines.append("  path: /work/tenants-source-backend.json\n/work/tenants-source-catalog-result.json")
        lines += ["##[endgroup]", f"Artifact {name} has been successfully uploaded! Final size is {len(data)} bytes. Artifact ID is {artifact_id}"]
        self.set_log(repo, job["id"], self.logs[job["id"]] + self.log_lines(CREATED, lines))

    @staticmethod
    def log_lines(stamp, lines):
        return "".join(stamp + " " + line + "\n" for line in lines)

    def set_log(self, repo, job_id, content):
        self.f.state["responses"][prefix(repo) + f"/actions/jobs/{job_id}/logs"] = {
            "binary": base64.b64encode(content.encode()).decode()}
