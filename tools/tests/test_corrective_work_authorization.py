"""Actual CLI controls in disposable repositories; fixtures grant no live authority."""

import contextlib
import copy
import datetime
import hashlib
import json
import os
import re
from pathlib import Path
import runpy
import shutil
import subprocess
import sys
import tempfile
import unittest
from unittest import mock
from types import SimpleNamespace


ROOT = Path(__file__).resolve().parents[2]
FIXTURES = ROOT / "tools/tests/fixtures/corrective-work-authorization"
DIRECTORY = "_bmad-output/implementation-artifacts/evidence/corrective-work-authorizations"
RECORD = DIRECTORY + "/G-BASELINE/fixture-authorization.json"
REGISTRY = DIRECTORY + "/owner-role-registry.json"
SCRIPT = "tools/validate-corrective-work-authorization.py"
CONTRACT = "tools/corrective-work-authorization.schema.json"
PRD = "_bmad-output/planning-artifacts/prd.md"
ACTIONS = ["readiness", "release", "deployment", "production", "migration", "removal"]
EVIDENCE = "evidence/result.json"
EVIDENCE_VALIDATOR = "tools/check-fixture-evidence.py"
ISSUED = "2000-01-03T00:00:00Z"
APPROVED = "2000-01-04T00:00:00Z"


def canonical(value):
    return json.dumps(value, sort_keys=True, separators=(",", ":"), ensure_ascii=False).encode()


class Repository:
    def __init__(self, parent, initial=None):
        self.root = Path(parent) / "repo"
        self.root.mkdir()
        self.state_file = Path(parent) / "authority.json"
        self.env = {**os.environ, "GIT_CONFIG_GLOBAL": os.devnull, "GIT_CONFIG_NOSYSTEM": "1", "GIT_TERMINAL_PROMPT": "0"}
        self.git("init", "--quiet")
        self.git("config", "user.name", "Synthetic Fixture")
        self.git("config", "user.email", "fixture@example.invalid")
        self.git("config", "core.hooksPath", os.devnull)
        self.git("config", "core.filemode", "true")
        self.git("config", "core.autocrlf", "false")
        for name in (SCRIPT, CONTRACT):
            self.write(name, (ROOT / name).read_bytes())
        self.write(PRD, b"Synthetic PRD; never production approval.\n")
        self.write("failed-gate.json", canonical({"gate": "G-BASELINE", "result": "FAIL", "reason": "synthetic fixture"}))
        self.write(".gitignore", b"_ignored/\ntools/_ignored/\n")
        self.write("src/correction.txt", b"before\n")
        self.write("src/untouched.txt", b"unchanged\n")
        self.write("_bmad-output/planning-artifacts/architecture.md", b"Synthetic architecture.\n")
        link = self.root / "_bmad-output/planning-artifacts/architecture/architecture-fixture/ARCHITECTURE-SPINE.md"
        link.parent.mkdir(parents=True, exist_ok=True)
        link.symlink_to("../../architecture.md" if initial != "baseline-external-symlink" else "/outside-repository")
        self.write(EVIDENCE_VALIDATOR, b'import json,sys\nfrom pathlib import Path\nvalue=json.loads(Path(sys.argv[1]).read_text())\nprint("evidence-accepted" if value.get("ok") is True else "evidence-rejected")\nsys.exit(0 if value.get("ok") is True else 1)\n')
        if initial == "argument-validator":
            self.write(EVIDENCE_VALIDATOR, b'import argparse,json,sys\nfrom pathlib import Path\np=argparse.ArgumentParser()\np.add_argument("evidence")\np.add_argument("--token",required=True)\na=p.parse_args()\nvalid=json.loads(Path(a.evidence).read_text()).get("ok") is True and a.token=="approved"\nprint("accepted" if valid else "rejected")\nsys.exit(0 if valid else 1)\n')
        elif initial == "argparse-rejector":
            self.write(EVIDENCE_VALIDATOR, b'import argparse,sys\np=argparse.ArgumentParser()\np.add_argument("evidence")\np.parse_args()\nprint("actual evidence validation always rejects")\nsys.exit(7)\n')
        self.registry = {"schema": "hexalith.corrective-owner-registry.v1", "version": 1,
                         "repository": "Hexalith/Hexalith.EventStore", "created_at": "2000-01-01T00:00:00Z",
                         "authority_issue": 10, "approvals_issue": 11,
                         "authority_owner": {"github_id": 123, "github_login": "fixture-human"},
                         "owners": [{"github_id": 123, "github_login": "fixture-human", "roles": ["product-owner"]}]}
        if initial == "registry-unknown":
            self.registry["unknown"] = True
        elif initial == "registry-duplicate":
            self.registry["owners"].append(dict(self.registry["owners"][0]))
        elif initial == "gate-pass":
            self.write("failed-gate.json", canonical({"gate": "G-BASELINE", "result": "PASS", "reason": "synthetic fixture"}))
        elif initial == "gate-mismatch":
            self.write("failed-gate.json", canonical({"gate": "G-OQ8", "result": "FAIL", "reason": "synthetic fixture"}))
        self.write(REGISTRY, canonical(self.registry))
        if initial == "initialized-gitlink":
            source = self.root.parent / "module-source"
            self.git("init", "--quiet", str(source))
            (source / "tracked.txt").write_bytes(b"unchanged submodule source\n")
            self.git("-C", str(source), "add", "tracked.txt")
            self.git("-C", str(source), "-c", "user.name=Synthetic Fixture", "-c", "user.email=fixture@example.invalid", "-c", "core.hooksPath=" + os.devnull,
                     "commit", "--quiet", "-m", "test: seed corrective authorization fixture")
            self.git("-c", "protocol.file.allow=always", "submodule", "add", "--quiet", str(source), "vendor/linked")
        if initial == "baseline-gitlink":
            (self.root / "vendor/linked").mkdir(parents=True)
            self.git("update-index", "--add", "--cacheinfo", "160000," + "1" * 40 + ",vendor/linked")
        self.git("add", "--all")
        self.git("commit", "--quiet", "-m", "test: seed corrective authorization fixture")
        self.baseline = self.git("rev-parse", "HEAD").stdout.strip()
        self.module = runpy.run_path(str(self.root / SCRIPT))
        self.write("src/correction.txt", b"after\n")
        self.write(EVIDENCE, canonical({"ok": True}))
        self.document = {
            "schema": "hexalith.corrective-work-authorization.v1", "version": 1,
            "authorization_id": "fixture-authorization", "failed_gate": {"id": "G-BASELINE", "evidence": self.binding("failed-gate.json")},
            "prd": self.binding(PRD), "baseline_commit": self.baseline, "input_subject_sha256": "0" * 64,
            "derivation": "hexalith.corrective-subject.v1", "owner": {"github_id": 123, "github_login": "fixture-human", "role": "product-owner"},
            "owner_role_registry": self.binding(REGISTRY),
            "scope": {"allowed": [{"path": "src/correction.txt", "mutations": ["modify"]}, {"path": EVIDENCE, "mutations": ["add", "modify"]},
                                  {"path": RECORD, "mutations": ["add", "modify"]}],
                      "prohibited_paths": [PRD, REGISTRY, ".github/workflows/ci.yml", "docs/ci.md", "sealed/"], "prohibited_actions": ACTIONS},
            "expected_evidence": [{"file": self.binding(EVIDENCE), "validator": self.binding(EVIDENCE_VALIDATOR), "arguments": [], "expected_exit_code": 0}],
            "validator": self.binding(SCRIPT), "contract": self.binding(CONTRACT), "issued_at": ISSUED, "expires_at": "2099-01-01T00:00:00Z",
            "revoked": False, "corrective-work-only": True, **{action: False for action in ACTIONS},
            "authorization_sha256": "0" * 64, "postflight": None}
        self.resign()

    def git(self, *arguments):
        result = subprocess.run(["git", *arguments], cwd=self.root, env=self.env, capture_output=True, text=True)
        if result.returncode:
            raise RuntimeError(result.stdout + result.stderr)
        return result

    def write(self, name, data):
        path = self.root / name
        path.parent.mkdir(parents=True, exist_ok=True)
        path.write_bytes(data)

    def binding(self, name):
        return {"path": name, "sha256": hashlib.sha256((self.root / name).read_bytes()).hexdigest()}

    def comment(self, issue, identifier, payload, created=APPROVED):
        return {"id": identifier, "url": f"https://api.github.com/repos/Hexalith/Hexalith.EventStore/issues/comments/{identifier}",
                "issue_url": f"https://api.github.com/repos/Hexalith/Hexalith.EventStore/issues/{issue}",
                "html_url": f"https://github.com/Hexalith/Hexalith.EventStore/issues/{issue}#issuecomment-{identifier}",
                "user": {"id": 123, "login": "fixture-human", "type": "User"}, "author_association": "OWNER", "performed_via_github_app": None,
                "created_at": created, "updated_at": created, "body": canonical(payload).decode()}

    @staticmethod
    def url(issue, page=1):
        return f"https://api.github.com/repos/Hexalith/Hexalith.EventStore/issues/{issue}/comments?per_page=100&page={page}"

    def resign(self):
        self.document["input_subject_sha256"] = self.module["input_subject"](self.root, self.document)
        self.document["authorization_sha256"] = hashlib.sha256(canonical(self.module["projected_record"](self.document))).hexdigest()
        self.save()
        self.state = {"responses": {
            self.url(10): [self.comment(10, 101, {"schema": "hexalith.corrective-owner-registry-approval.v1", "subject_sha256": self.binding(REGISTRY)["sha256"]}, "2000-01-02T00:00:00Z")],
            self.url(11): [self.comment(11, 102, {"schema": "hexalith.corrective-work-approval.v1", "subject_sha256": self.document["authorization_sha256"]})]}}
        self.save_state()

    def save(self):
        self.write(RECORD, canonical(self.document) + b"\n")

    def save_state(self):
        self.state_file.write_bytes(canonical(self.state))

    def invoke(self, *extra, baseline=None, registry=False, direct=False, record=RECORD):
        args = ["--evaluate-registry"] if registry else [record, "--changed-paths-from", baseline or self.baseline, *extra]
        flags = ["-I", "-S", "-B"] + (["-" + "O" * sys.flags.optimize] if sys.flags.optimize else [])
        command = [sys.executable, *flags]
        if direct:
            command += [str(self.root / SCRIPT), *args]
        else:
            command += [str(FIXTURES / "authority_cli.py"), str(self.state_file), str(self.root / SCRIPT), *args]
        process = subprocess.run(command, cwd=self.root, env=self.env, capture_output=True, text=True, timeout=45)
        try:
            verdict = json.loads(process.stdout)
        except json.JSONDecodeError as error:
            raise AssertionError(process.stdout + process.stderr) from error
        return process, verdict


@contextlib.contextmanager
def repository(initial=None):
    with tempfile.TemporaryDirectory(prefix="corrective-authority-") as parent:
        yield Repository(parent, initial)


class CorrectiveWorkAuthorizationTests(unittest.TestCase):
    def assert_pass(self, result, phase):
        process, verdict = result
        self.assertEqual(process.returncode, 0, verdict)
        self.assertEqual(verdict["result"], "PASS")
        self.assertEqual(verdict["phase"], phase)
        self.assertTrue(verdict["corrective-work-only"])
        for flag in ACTIONS:
            self.assertIs(verdict[flag], False)
        return verdict

    def complete(self, repo, record=RECORD):
        process, proposal = repo.invoke("--postflight", "--write-postflight", record=record)
        self.assertEqual(process.returncode, 1, proposal)
        self.assertEqual(proposal["reason"], "missing-completion", proposal)
        payload = proposal["proposed_completion"]
        self.assertEqual(canonical(payload).decode(), proposal["proposed_completion_body"])
        self.assertEqual(hashlib.sha256(canonical(payload)).hexdigest(), proposal["proposed_completion_sha256"])
        self.assertIsNone(json.loads((repo.root / record).read_text())["postflight"])
        rows = repo.state["responses"][repo.url(11)]
        rows.append(repo.comment(11, 1000 + len(rows), payload, "2000-01-05T00:00:00Z"))
        repo.save_state()
        return self.assert_pass(repo.invoke("--postflight", "--write-postflight", record=record), "postflight")

    def test_preflight_positive_control_across_git_layers(self):
        with repository() as repo:
            self.assert_pass(repo.invoke(), "preflight")
            repo.git("add", "src/correction.txt", EVIDENCE, RECORD)
            self.assert_pass(repo.invoke(), "preflight")
            repo.git("commit", "--quiet", "-m", "test: commit corrective authorization fixture")
            self.assert_pass(repo.invoke(), "preflight")

    def test_baseline_pinned_registry_issuer_accepts_member_association(self):
        with repository() as repo:
            for rows in repo.state["responses"].values():
                for row in rows:
                    row["author_association"] = "MEMBER"
            repo.save_state()
            self.assert_pass(repo.invoke(), "preflight")

    def test_postflight_positive_control_and_commit_stability(self):
        with repository() as repo:
            first = self.complete(repo)
            second = self.assert_pass(repo.invoke("--postflight"), "postflight")
            self.assertEqual(first["output_subject_sha256"], second["output_subject_sha256"])
            self.assert_pass(repo.invoke(), "postflight")
            repo.git("add", "--all")
            repo.git("commit", "--quiet", "-m", "test: commit corrective authorization fixture")
            committed = self.assert_pass(repo.invoke("--postflight"), "postflight")
            self.assertEqual(first["output_subject_sha256"], committed["output_subject_sha256"])
            self.assert_pass(repo.invoke(registry=True), "registry")

    def test_staged_record_postflight_positive_control(self):
        with repository() as repo:
            repo.git("add", "--all")
            self.complete(repo)
            self.assert_pass(repo.invoke("--postflight"), "postflight")

    def defer_outputs(self, repo):
        validator_path = "tools/future-evidence-validator.py"
        validator_bytes = (repo.root / EVIDENCE_VALIDATOR).read_bytes()
        repo.document["expected_evidence"][0]["file"]["sha256"] = None
        repo.document["expected_evidence"][0]["validator"] = {"path": validator_path, "sha256": hashlib.sha256(validator_bytes).hexdigest()}
        repo.document["scope"]["allowed"].append({"path": validator_path, "mutations": ["add"]})
        (repo.root / EVIDENCE).unlink()
        repo.resign()
        return validator_path, validator_bytes

    def test_absent_evidence_and_preapproved_future_validator_positive_control(self):
        with repository() as repo:
            validator_path, validator_bytes = self.defer_outputs(repo)
            self.assert_pass(repo.invoke(), "preflight")
            repo.write(validator_path, validator_bytes)
            repo.write(EVIDENCE, canonical({"ok": True, "derived_after_handoff": True}))
            self.complete(repo)
            self.assert_pass(repo.invoke("--postflight"), "postflight")

    def test_scoped_delete_rename_and_mode_positive_control(self):
        for operation in ("delete", "rename", "mode"):
            with self.subTest(operation=operation), repository() as repo:
                repo.document["scope"]["allowed"][0]["mutations"] = ["modify", "delete", "mode"]
                if operation == "delete":
                    (repo.root / "src/correction.txt").unlink()
                elif operation == "rename":
                    (repo.root / "src/correction.txt").rename(repo.root / "src/renamed.txt")
                    repo.document["scope"]["allowed"].append({"path": "src/renamed.txt", "mutations": ["add"]})
                else:
                    (repo.root / "src/correction.txt").chmod(0o755)
                repo.resign()
                self.assert_pass(repo.invoke(), "preflight")

    def test_actual_bootstrap_isolates_imports_and_survives_optimized_python(self):
        with repository() as repo:
            repo.write("tools/json.py", b'raise RuntimeError("untrusted import executed")\n')
            # A missing record is evaluated before any network read. The executable
            # bootstrap must remove the script directory before importing json.
            (repo.root / RECORD).unlink()
            process = subprocess.run([sys.executable, "-O", str(repo.root / SCRIPT), RECORD, "--changed-paths-from", repo.baseline],
                                     cwd=repo.root, capture_output=True, text=True, timeout=45)
            self.assertNotEqual(process.returncode, 0)
            self.assertEqual(json.loads(process.stdout)["reason"], "missing-input")
            self.assertNotIn("untrusted import", process.stderr)

    def test_missing_live_authority_is_content_bound_failure(self):
        with repository() as repo:
            shutil.rmtree(repo.root / DIRECTORY)
            process, verdict = repo.invoke(registry=True, direct=True)
            self.assertEqual(process.returncode, 1)
            self.assertEqual(verdict["reason"], "missing-authority")
            self.assertEqual(verdict["source_commit"], repo.baseline)
            self.assertEqual(verdict["validator_sha256"], repo.binding(SCRIPT)["sha256"])

    def test_checked_in_rejection_fixtures(self):
        cases = json.loads((FIXTURES / "rejections.json").read_text())
        observed = []
        for case in cases:
            with self.subTest(case=case["id"]), repository(case.get("initial")) as repo:
                arguments, baseline = self.apply_case(repo, case)
                process, verdict = repo.invoke(*arguments, baseline=baseline)
                self.assertEqual(process.returncode, 1, verdict)
                self.assertEqual(verdict["result"], "FAIL")
                self.assertEqual(verdict["reason"], case["reason"], verdict)
                observed.append({"id": case["id"], "result": "FAIL", "reason": verdict["reason"]})
        self.assertEqual(len(observed), len(cases))
        self.assertEqual(observed, json.loads((FIXTURES / "observed-rejections.json").read_text()))

    def paginate(self, repo):
        rows = repo.state["responses"][repo.url(11)]
        discussion = {"user": {"id": 987, "login": "unrelated"}, "body": "ordinary discussion"}
        repo.state["responses"][repo.url(11)] = rows + [discussion] * (100 - len(rows))
        repo.state["responses"][repo.url(11, 2)] = []
        repo.save_state()

    def test_unchanged_gitlink_positive_control(self):
        for initial in ("baseline-gitlink", "initialized-gitlink"):
            with self.subTest(initial=initial), repository(initial) as repo:
                self.assert_pass(repo.invoke(), "preflight")
                self.complete(repo)
                self.assert_pass(repo.invoke("--postflight"), "postflight")
                if initial == "baseline-gitlink":
                    (repo.root / "vendor/linked").rmdir()
                    self.assert_pass(repo.invoke("--postflight"), "postflight")

    def test_paginated_authority_positive_control(self):
        with repository() as repo:
            self.paginate(repo)
            self.assert_pass(repo.invoke(), "preflight")

    def test_unrelated_copied_payloads_do_not_deny_authority(self):
        with repository() as repo:
            for issue in (10, 11):
                copied = copy.deepcopy(repo.state["responses"][repo.url(issue)][0])
                copied["user"] = {"id": 987, "login": "unrelated", "type": "User"}
                repo.state["responses"][repo.url(issue)].append(copied)
            payload = {"schema": "hexalith.corrective-work-revocation.v1", "subject_sha256": repo.document["authorization_sha256"]}
            copied = repo.comment(11, 555, payload)
            copied["user"] = {"id": 987, "login": "unrelated", "type": "User"}
            repo.state["responses"][repo.url(11)].append(copied)
            repo.save_state()
            self.assert_pass(repo.invoke(), "preflight")

    def test_two_precreated_records_complete_without_recursive_subjects(self):
        with repository() as repo:
            second_name = DIRECTORY + "/G-BASELINE/second-authorization.json"
            repo.document["scope"]["allowed"].append({"path": second_name, "mutations": ["add", "modify"]})
            repo.resign()
            second = copy.deepcopy(repo.document)
            second["authorization_id"] = "second-authorization"
            second["authorization_sha256"] = hashlib.sha256(canonical(repo.module["projected_record"](second))).hexdigest()
            repo.write(second_name, canonical(second) + b"\n")
            repo.state["responses"][repo.url(11)].append(repo.comment(11, 103, {"schema": "hexalith.corrective-work-approval.v1", "subject_sha256": second["authorization_sha256"]}))
            repo.save_state()
            first = self.complete(repo)
            self.complete(repo, second_name)
            again = self.assert_pass(repo.invoke("--postflight"), "postflight")
            self.assertEqual(first["output_subject_sha256"], again["output_subject_sha256"])
            repo.git("add", "--all")
            repo.git("commit", "--quiet", "-m", "test: commit corrective authorization fixture")
            self.assert_pass(repo.invoke(registry=True), "registry")

    def test_registry_reports_each_malformed_record_and_continues(self):
        cases = json.loads((FIXTURES / "malformed-records.json").read_text())
        with repository() as repo:
            for index, case in enumerate(cases):
                data = b"[" * case["nesting_depth"] + b"0" + b"]" * case["nesting_depth"] if "nesting_depth" in case else canonical(case["value"])
                repo.write(DIRECTORY + f"/G-BASELINE/malformed-{index}.json", data)
            repo.document["scope"]["allowed"].extend({"path": DIRECTORY + f"/G-BASELINE/malformed-{index}.json", "mutations": ["add"]} for index in range(len(cases)))
            repo.resign()
            self.complete(repo)
            process, verdict = repo.invoke(registry=True)
            self.assertEqual(process.returncode, 1, verdict)
            self.assertEqual(len(verdict["evaluations"]), len(cases) + 1)
            self.assertEqual(sum(item["result"] == "PASS" for item in verdict["evaluations"]), 1)
            failures = [item for item in verdict["evaluations"] if item["result"] == "FAIL"]
            expected = {DIRECTORY + f"/G-BASELINE/malformed-{index}.json": case["reason"] for index, case in enumerate(cases)}
            self.assertEqual({item["record"]: item["reason"] for item in failures}, expected)

    def test_nonempty_approved_arguments_control_actual_validator_acceptance(self):
        with repository("argument-validator") as repo:
            repo.document["expected_evidence"][0]["arguments"] = ["--token", "approved"]
            repo.resign()
            self.assert_pass(repo.invoke(), "preflight")
            verdict = self.complete(repo)
            self.assertEqual(verdict["evidence_results"][0]["command"],
                             ["python3", "-I", "-S", "-B", EVIDENCE_VALIDATOR, "./" + EVIDENCE, "--token", "approved"])

    def test_independent_versioned_subject_known_answers(self):
        vectors = json.loads((FIXTURES / "subject-vectors.json").read_text())
        module = runpy.run_path(str(ROOT / SCRIPT))
        document = vectors["immutable_core"]["authorization"]
        definitions = json.loads((ROOT / CONTRACT).read_text())["$defs"]
        canonical_case = vectors["canonicalization"]
        self.assertEqual(module["canonical"](canonical_case["value"]).decode(), canonical_case["canonical_utf8"])
        self.assertEqual(module["digest"](module["canonical"](canonical_case["value"])), canonical_case["sha256"])
        core = module["canonical"](module["projected_record"](document))
        self.assertEqual(core.decode(), vectors["immutable_core"]["canonical_utf8"])
        self.assertEqual(hashlib.sha256(core).hexdigest(), vectors["immutable_core"]["sha256"])
        subject = module["input_subject"]
        fake_git = mock.Mock(return_value=subprocess.CompletedProcess([], 0, stdout=(vectors["input_subject"]["baseline_tree"] + "\n").encode()))
        with mock.patch.dict(subject.__globals__, {"run_git": fake_git}):
            self.assertEqual(subject(Path("/synthetic"), document), vectors["input_subject"]["sha256"])
        fake_git.assert_called_once_with(Path("/synthetic"), "rev-parse", document["baseline_commit"] + "^{tree}")
        output = vectors["output_subject"]
        self.assertEqual(module["output_subject"](document, output["repository"], output["evidence_results"]), output["sha256"])
        # The canonical strings and digests are literal independently generated answers.
        for vector in (vectors["input_subject"], output):
            self.assertEqual(hashlib.sha256(vector["canonical_utf8"].encode()).hexdigest(), vector["sha256"])
        for case in vectors["multi_envelope_projection"]:
            self.assertEqual(module["blob_identity"](case["raw_json"].encode(), case["path"], definitions), case["sha256"])

    def test_evidence_command_portability_matches_known_output_subject(self):
        vectors = json.loads((FIXTURES / "subject-vectors.json").read_text())
        module = runpy.run_path(str(ROOT / SCRIPT))
        document = vectors["immutable_core"]["authorization"]
        execute = module["execute_evidence"]
        output = vectors["output_subject"]
        for executable in ("/developer/python/bin/python3", "/ci/toolcache/python3"):
            with self.subTest(executable=executable):
                fake_git = mock.Mock(return_value=subprocess.CompletedProcess([], 1, stdout=b"", stderr=b""))
                process = subprocess.CompletedProcess([], 0, stdout=b"accepted\n", stderr=b"")
                with mock.patch.dict(execute.__globals__, {"sys": SimpleNamespace(executable=executable), "run_git": fake_git,
                                                           "bound": lambda root, binding: b'{"ok":true}'}), mock.patch("subprocess.run", return_value=process) as invoked:
                    results = execute(Path("/synthetic"), document)
                self.assertEqual(invoked.call_args.args[0], [executable, *output["evidence_results"][0]["command"][1:]])
                self.assertEqual(results, output["evidence_results"])
                self.assertEqual(module["output_subject"](document, output["repository"], results), output["sha256"])

    def test_schema_pattern_and_runtime_formats_agree(self):
        module = runpy.run_path(str(ROOT / SCRIPT))
        definitions = json.loads((ROOT / CONTRACT).read_text())["$defs"]
        vectors = json.loads((FIXTURES / "subject-vectors.json").read_text())
        for case in vectors["formats"]:
            node = definitions[case["definition"]]["properties"][case["property"]]
            self.assertTrue(node["pattern"].startswith("^"))
            self.assertIn("$", node["pattern"])
            for expected, values in ((True, case["valid"]), (False, case["invalid"])):
                for value in values:
                    with self.subTest(property=case["property"], value=value):
                        schema_accepts = re.search(node["pattern"], value) is not None and node.get("minLength", 0) <= len(value) <= node.get("maxLength", 4096)
                        self.assertEqual(schema_accepts, expected)
                        if expected:
                            module["shape"](value, node, definitions)
                        else:
                            with self.assertRaises(module["Rejection"]):
                                module["shape"](value, node, definitions)

    def apply_case(self, repo, case):
        operation = case["operation"]
        arguments, baseline = [], None
        if operation == "record-field":
            target = repo.document
            keys = case["field"].split("/")
            for key in keys[:-1]:
                target = target[int(key)] if isinstance(target, list) else target[key]
            key = int(keys[-1]) if isinstance(target, list) else keys[-1]
            target[key] = case["value"]
            repo.resign()
        elif operation == "missing-record":
            (repo.root / RECORD).unlink()
        elif operation == "duplicate-json":
            data = (repo.root / RECORD).read_bytes()
            repo.write(RECORD, b'{"version":1,' + data[1:])
        elif operation == "nonfinite-json":
            data = (repo.root / RECORD).read_bytes().replace(b'"version":1', b'"version":NaN')
            repo.write(RECORD, data)
        elif operation == "auth-drift":
            repo.document["scope"]["allowed"][0]["mutations"].append("delete")
            repo.save()
        elif operation == "input-drift":
            repo.document["input_subject_sha256"] = "f" * 64
            repo.document["authorization_sha256"] = hashlib.sha256(canonical(repo.module["projected_record"](repo.document))).hexdigest()
            repo.save()
        elif operation == "wrong-baseline":
            baseline = "f" * 40
        elif operation == "unmerged-index":
            oid = repo.git("rev-parse", repo.baseline + ":src/correction.txt").stdout.strip()
            index_info = f"0 {'0' * 40}\tsrc/correction.txt\n100644 {oid} 1\tsrc/correction.txt\n100644 {oid} 2\tsrc/correction.txt\n"
            process = subprocess.run(["git", "update-index", "--index-info"], input=index_info, cwd=repo.root, env=repo.env, capture_output=True, text=True)
            self.assertEqual(process.returncode, 0, process.stderr)
        elif operation == "immutable-index":
            original = (repo.root / REGISTRY).read_bytes()
            repo.registry["owners"][0]["roles"].append("operator")
            repo.write(REGISTRY, canonical(repo.registry))
            repo.git("add", REGISTRY)
            repo.write(REGISTRY, original)
        elif operation == "immutable-drift":
            field = case["field"]
            path = repo.document[field]["path"]
            if field == "owner_role_registry":
                repo.registry["owners"][0]["roles"].append("operator")
                repo.write(path, canonical(repo.registry))
            elif field == "contract":
                contract = json.loads((repo.root / path).read_text())
                contract["title"] += " changed"
                repo.write(path, canonical(contract))
            else:
                repo.write(path, (repo.root / path).read_bytes() + b"\n# changed\n")
            if case.get("rebind"):
                repo.document[field] = repo.binding(path)
                repo.resign()
        elif operation == "receipt-field":
            row = repo.state["responses"][repo.url(case.get("issue", 11))][0]
            target = row
            keys = case["field"].split("/")
            for key in keys[:-1]:
                target = target[key]
            target[keys[-1]] = case["value"]
            repo.save_state()
        elif operation == "missing-approval":
            repo.state["responses"][repo.url(11)] = []
            repo.save_state()
        elif operation == "duplicate-approval":
            repo.state["responses"][repo.url(11)] *= 2
            repo.save_state()
        elif operation == "revocation":
            payload = {"schema": "hexalith.corrective-work-revocation.v1", "subject_sha256": repo.document["authorization_sha256"]}
            repo.state["responses"][repo.url(11)].append(repo.comment(11, 103, payload, "2000-01-05T00:00:00Z"))
            repo.save_state()
        elif operation == "http-failure":
            repo.state["http_failure"] = True
            repo.save_state()
        elif operation == "concurrent-drift":
            repo.state["concurrent_write"] = str(repo.root / "src/correction.txt")
            repo.save_state()
        elif operation == "out-of-scope":
            path = "extra.bin" if case.get("layer") == "untracked" else "src/untouched.txt"
            repo.write(path, b"unauthorized bytes\n")
            if case.get("layer") in ("index", "committed"):
                repo.git("add", path)
                if case["layer"] == "committed":
                    repo.git("commit", "--quiet", "-m", "test: commit corrective authorization fixture")
                repo.write(path, b"unchanged\n")
        elif operation == "delete":
            (repo.root / "src/correction.txt").unlink()
        elif operation == "mode":
            (repo.root / "src/correction.txt").chmod(0o755)
        elif operation in ("symlink", "hardlink", "fifo"):
            path = repo.root / "src/correction.txt"
            path.unlink()
            if operation == "symlink":
                path.symlink_to(repo.state_file)
            elif operation == "hardlink":
                os.link(repo.root / "src/untouched.txt", path)
            else:
                os.mkfifo(path)
        elif operation == "gitlink":
            repo.git("update-index", "--add", "--cacheinfo", "160000," + repo.baseline + ",vendor/linked")
        elif operation == "prohibited":
            repo.document["scope"]["prohibited_paths"].append("src/")
            repo.resign()
        elif operation == "duplicate-path":
            repo.document["scope"]["allowed"].append({"path": "src/correction.txt", "mutations": ["delete"]})
            repo.resign()
        elif operation == "ignored-evidence":
            repo.write("_ignored/result.json", canonical({"ok": True}))
            repo.document["expected_evidence"][0]["file"] = repo.binding("_ignored/result.json")
            repo.resign()
        elif operation == "wrong-validator":
            repo.document["expected_evidence"][0]["validator"] = repo.binding(PRD)
            repo.resign()
        elif operation == "evidence-failed":
            repo.write(EVIDENCE, canonical({"ok": False}))
            repo.document["expected_evidence"][0]["file"] = repo.binding(EVIDENCE)
            repo.resign()
            arguments = ["--postflight", "--write-postflight"]
        elif operation == "missing-evidence":
            (repo.root / EVIDENCE).unlink()
            arguments = ["--postflight", "--write-postflight"]
        elif operation == "postflight-missing":
            arguments = ["--postflight"]
        elif operation == "postflight-drift":
            self.complete(repo)
            repo.document = json.loads((repo.root / RECORD).read_text())
            if case["target"] == "source":
                repo.write("src/correction.txt", b"later change\n")
            elif case["target"] == "evidence":
                repo.write(EVIDENCE, canonical({"ok": True, "later": True}))
            elif case["target"] == "postflight":
                repo.document["postflight"]["output_subject_sha256"] = "f" * 64
                repo.save()
            elif case["target"] == "validator":
                repo.write(EVIDENCE_VALIDATOR, (repo.root / EVIDENCE_VALIDATOR).read_bytes() + b"\n# drift\n")
            elif case["target"] == "rewrite":
                arguments = ["--postflight", "--write-postflight"]
            if not arguments:
                arguments = ["--postflight"]
        elif operation == "deferred-output":
            validator_path, validator_bytes = self.defer_outputs(repo)
            if case["target"] == "unscoped-validator":
                repo.document["scope"]["allowed"] = repo.document["scope"]["allowed"][:-1]
                repo.resign()
            elif case["target"] == "unscoped-evidence":
                repo.document["scope"]["allowed"] = [entry for entry in repo.document["scope"]["allowed"] if entry["path"] != EVIDENCE]
                repo.resign()
            else:
                self.assert_pass(repo.invoke(), "preflight")
                repo.write(validator_path, validator_bytes + (b"\n# wrong approved bytes\n" if case["target"] == "validator-mismatch" else b""))
                repo.write(EVIDENCE, canonical({"ok": True}))
                arguments = ["--postflight", "--write-postflight"]
                if case["target"] == "evidence-drift":
                    self.complete(repo)
                    repo.write(EVIDENCE, canonical({"ok": True, "later": True}))
                    arguments = ["--postflight"]
        elif operation == "hidden-gitlink":
            repo.git("update-index", "--" + case["flag"], "vendor/linked")
        elif operation == "hidden-content":
            repo.git("update-index", "--" + case["flag"], "src/untouched.txt")
            repo.write("src/untouched.txt", b"hidden unauthorized bytes\n")
        elif operation == "hidden-mode":
            repo.git("config", "core.filemode", "false")
            if case.get("flag"):
                repo.git("update-index", "--" + case["flag"], "src/correction.txt")
            (repo.root / "src/correction.txt").chmod(0o755)
        elif operation == "shadow-drift":
            self.complete(repo)
            repo.document = json.loads((repo.root / RECORD).read_text())
            completed = (repo.root / "src/correction.txt").read_bytes()
            repo.write("src/correction.txt", b"divergent shadow\n")
            repo.git("add", "src/correction.txt")
            if case["layer"] == "committed":
                repo.git("commit", "--quiet", "-m", "test: commit corrective authorization fixture")
            repo.write("src/correction.txt", completed)
            if case["layer"] == "committed":
                repo.git("add", "src/correction.txt")
            arguments = ["--postflight"]
        elif operation == "reset-completion":
            self.complete(repo)
            repo.document = json.loads((repo.root / RECORD).read_text())
            if case.get("committed"):
                repo.git("add", "--all")
                repo.git("commit", "--quiet", "-m", "test: commit corrective authorization fixture")
            repo.document["postflight"] = None
            repo.save()
            if case.get("change", True):
                repo.write("src/correction.txt", b"rebound output\n")
            if case.get("commit_reset"):
                repo.git("add", "--all")
                repo.git("commit", "--quiet", "-m", "test: commit corrective authorization fixture")
            arguments = case.get("arguments", [])
        elif operation == "completion-receipt":
            self.complete(repo)
            rows = repo.state["responses"][repo.url(11)]
            row = rows[-1]
            if case["target"] == "duplicate":
                rows.append(dict(row))
            else:
                row[case["field"]] = case["value"]
            repo.save_state()
            arguments = ["--postflight"]
        elif operation == "prohibited-spelling":
            repo.document["scope"]["prohibited_paths"].append(case["value"])
            repo.resign()
        elif operation == "paginated-revocation":
            self.paginate(repo)
            payload = {"schema": "hexalith.corrective-work-revocation.v1", "subject_sha256": repo.document["authorization_sha256"]}
            repo.state["responses"][repo.url(11, 2)].append(repo.comment(11, 555, payload, "2000-01-05T00:00:00Z"))
            repo.save_state()
        elif operation == "late-revocation":
            repo.state[case["flag"]] = True
            repo.save_state()
            arguments = ["--postflight", "--write-postflight"]
        elif operation == "fixed-evidence-preflight":
            if case["target"] == "missing":
                (repo.root / EVIDENCE).unlink()
            else:
                repo.write(EVIDENCE, canonical({"ok": True, "different": True}))
        elif operation == "second-record-envelope":
            second = copy.deepcopy(repo.document)
            second["authorization_id"] = "unscoped-baseline-record"
            name = DIRECTORY + "/G-BASELINE/unscoped-baseline-record.json"
            repo.write(name, canonical(second))
            repo.git("add", "--all")
            repo.git("commit", "--quiet", "-m", "test: commit corrective authorization fixture")
            repo.baseline = repo.git("rev-parse", "HEAD").stdout.strip()
            repo.document["baseline_commit"] = repo.baseline
            repo.resign()
            original = (repo.root / name).read_bytes()
            second["authorization_sha256"] = "f" * 64
            repo.write(name, canonical(second))
            if case["layer"] in ("index", "committed"):
                repo.git("add", name)
                if case["layer"] == "committed":
                    repo.git("commit", "--quiet", "-m", "test: commit corrective authorization fixture")
                repo.write(name, original)
                if case["layer"] == "committed":
                    repo.git("add", name)
        elif operation == "option-evidence":
            repo.write("--help", canonical({"ok": False}))
            repo.document["expected_evidence"][0]["file"] = repo.binding("--help")
            repo.document["scope"]["allowed"].append({"path": "--help", "mutations": ["add"]})
            repo.resign()
            arguments = ["--postflight", "--write-postflight"]
        elif operation == "ignored-validator":
            name = "tools/_ignored/approved-validator.py"
            repo.write(name, (repo.root / EVIDENCE_VALIDATOR).read_bytes())
            repo.document["expected_evidence"][0]["validator"] = repo.binding(name)
            repo.document["scope"]["allowed"].append({"path": name, "mutations": ["add"]})
            repo.resign()
        elif operation == "deep-json":
            repo.write(RECORD, b"[" * case["nesting_depth"] + b"0" + b"]" * case["nesting_depth"])
        elif operation == "uninitialized-gitlink-content":
            if case.get("postflight"):
                self.complete(repo)
                arguments = ["--postflight"]
            repo.write("vendor/linked/concealed.bin", b"uninventoried content\n")
        elif operation == "authorized-mode-drift":
            repo.document["scope"]["allowed"][0]["mutations"].append("mode")
            repo.resign()
            self.complete(repo)
            (repo.root / "src/correction.txt").chmod(0o755)
            arguments = ["--postflight"]
        elif operation == "approved-arguments":
            repo.document["expected_evidence"][0]["arguments"] = ["--token", "denied"]
            repo.resign()
            self.assert_pass(repo.invoke(), "preflight")
            arguments = ["--postflight", "--write-postflight"]
        elif operation == "raw-record":
            repo.write(RECORD, canonical(case["value"]))
        elif operation == "new-record-core":
            self.complete(repo)
            second = copy.deepcopy(repo.document)
            second["authorization_id"] = "late-authorization"
            second["postflight"] = None
            second_name = DIRECTORY + "/G-BASELINE/late-authorization.json"
            repo.write(second_name, canonical(second))
            repo.document = json.loads((repo.root / RECORD).read_text())
            arguments = ["--postflight"]
        elif operation == "initial":
            pass
        else:
            raise AssertionError("unknown rejection fixture operation: " + operation)
        return arguments, baseline


if __name__ == "__main__":
    unittest.main()
