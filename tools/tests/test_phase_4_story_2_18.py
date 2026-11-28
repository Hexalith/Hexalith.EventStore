"""Registration, source binding and refusal checks for the native query proof."""

import base64
import copy
import hashlib
import json
from pathlib import Path
import secrets
import socket
import subprocess
import sys
import tempfile
from types import SimpleNamespace
import unittest
from unittest import mock
import xml.etree.ElementTree as ET

import test_phase_4_transition as T
import test_phase_4_story_completion as W
import phase_4_story_2_18 as A
import phase_4_story_completion as S
import phase_4_transition as C


class ConditionalQueryProofTests(unittest.TestCase):
    def setUp(self):
        scratch = tempfile.TemporaryDirectory()
        self.addCleanup(scratch.cleanup)
        self.root = Path(scratch.name)

    def claim(self, target):
        return {"story_key": A.STORY_KEY, "story_id": "2.18", "spec_path": A.SPEC, "target": target,
                "scope": "slice", "nfr_ids": ["NFR8", "NFR12"], "high_risk_nfr_ids": ["NFR12"],
                "coverage_sha256": "a" * 64}

    def test_only_the_exact_supporting_slice_registers_for_both_targets(self):
        for target in ("spec", "tracker"):
            claim = self.claim(target)
            self.assertIs(S.adapter_for(claim), A)
            for field, wrong in {"story_id": "2.19", "story_key": "2-18-other", "spec_path": A.SPEC.replace("spec-", ""),
                                 "scope": "story", "nfr_ids": ["NFR12"], "high_risk_nfr_ids": []}.items():
                with self.subTest(target=target, field=field):
                    self.assertIsNone(S.adapter_for({**claim, field: wrong}))

    def report(self):
        result = ET.Element("assemblies")
        assembly = ET.SubElement(result, "assembly", {"name": str(self.root / A.ASSEMBLY), "total": "6", "passed": "6",
                                                       "failed": "0", "skipped": "0", "not-run": "0", "errors": "0"})
        collection = ET.SubElement(assembly, "collection")
        for component, declarative, legacy in (
            ("pubsub", False, False), ("in-memory-events", False, False),
            ("in-memory-events", True, False), ("pubsub", False, True),
            ("in-memory-events", False, True), ("in-memory-events", True, True),
        ):
            display = f'{A.SELECTOR}(component: \\"{component}\\", declarative: {declarative}, legacyFinite: {legacy})'
            ET.SubElement(collection, "test", {"name": display, "type": A.TEST_TYPE, "method": A.METHOD, "result": "Pass"})
        return result

    def cases(self, report):
        path = self.root / "native.xml"
        ET.ElementTree(report).write(path)
        return A.passing_cases(path, self.root / A.ASSEMBLY)

    def test_exact_xunit_cases_normalize_independently_of_execution_order(self):
        report = self.report()
        reordered = copy.deepcopy(report)
        collection = reordered.find(".//collection")
        collection[:] = list(reversed(collection[:]))
        cases = self.cases(report)
        self.assertEqual(cases, self.cases(reordered))
        self.assertEqual(6, len(cases))
        self.assertTrue(all(row["result"] == "PASS" for row in cases))

    def test_evidence_schema_requires_the_producers_exact_case_order_and_membership(self):
        schema = json.loads((T.ROOT / "tools/phase-4-transition.schema.json").read_text())
        cases = self.cases(self.report())
        constraint = schema["$defs"]["story_2_18_evidence"]["properties"]["cases"]
        self.assertEqual({"const": cases}, constraint)
        alternate = copy.deepcopy(cases)
        alternate[2]["component"] = "pubsub"
        self.assertNotEqual(constraint["const"], alternate)

    def test_green_totals_cannot_hide_unrelated_duplicate_missing_or_skipped_cases(self):
        for change in ("unrelated", "duplicate", "missing", "skipped", "other_assembly", "assembly_error"):
            with self.subTest(change=change):
                report = self.report()
                collection = report.find(".//collection")
                if change == "unrelated":
                    collection[0].set("method", "AnotherPassingTest")
                elif change == "duplicate":
                    collection[5].set("name", collection[0].get("name"))
                elif change == "missing":
                    collection.remove(collection[0])
                elif change == "skipped":
                    collection[0].set("result", "Skip")
                elif change == "other_assembly":
                    report[0].set("name", str(self.root / "other.dll"))
                else:
                    report[0].set("errors", "1")
                with self.assertRaisesRegex(C.Refusal, "Story 2.18") as caught:
                    self.cases(report)
                self.assertEqual("STORY_EVIDENCE_FAILED", caught.exception.code)

    def test_wrong_runtime_does_not_reach_build_or_test(self):
        profile = self.root / A.CANDIDATE_PROFILE
        profile.parent.mkdir(parents=True)
        profile.write_bytes((T.ROOT / A.CANDIDATE_PROFILE).read_bytes())
        wrong = self.root / secrets.token_hex(8)
        wrong.write_bytes(b"not daprd")
        wrong.chmod(0o700)
        for path, reason in ((wrong, "differs from the pinned runtime"), (self.root / "missing", "prerequisite unavailable")):
            with self.subTest(path=path), mock.patch.dict("os.environ", {"QUALIFICATION_DAPRD": str(path)}):
                with self.assertRaisesRegex(C.Refusal, reason) as caught:
                    A.runtime_inputs(self.root)
                self.assertEqual("STORY_EVIDENCE_FAILED", caught.exception.code)

    def test_approval_subject_changes_when_the_authenticated_tree_changes(self):
        for root_tree in ("c" * 40, "d" * 40):
            api = SimpleNamespace(get=lambda path: {"sha": T.HEAD, "tree": {"sha": root_tree}})
            self.assertEqual(C.digest(C.canonical({"adapter": A.ADAPTER, "root_tree": root_tree})),
                             A.subject_at(api, T.HEAD))

    def test_git_snapshot_timeout_refuses_with_the_evidence_limit_code(self):
        control = SimpleNamespace(root=self.root, head=T.HEAD, story=self.claim("spec"), story_evidence=None)
        with mock.patch.object(A.Source, "git", side_effect=subprocess.TimeoutExpired("git", 60)):
            with self.assertRaisesRegex(C.Refusal, "Git snapshot exceeded its time limit") as caught:
                A.evaluate(control)
        self.assertEqual("STORY_EVIDENCE_LIMIT", caught.exception.code)
        self.assertIsNone(control.story_evidence)

    def test_invalid_nuget_zip_refuses_with_the_evidence_failure_code(self):
        package = self.root / "sample" / "1.0.0"
        package.mkdir(parents=True)
        content = b"not a zip archive"
        (package / "sample.1.0.0.nupkg").write_bytes(content)
        (package / "sample.1.0.0.nupkg.sha512").write_bytes(base64.b64encode(hashlib.sha512(content).digest()))
        tree = "c" * 40
        manifest = {"head": T.HEAD, "root_tree": tree, "submodules": [{"commit": "d" * 40}]}
        control = SimpleNamespace(root=self.root, story=self.claim("spec"), subject=A.subject_digest(tree),
                                  binding={}, story_evidence=None)
        with mock.patch.object(A, "inventory", return_value=manifest), \
             mock.patch.object(A, "run", side_effect=lambda root, _: A.Source.dependency_inventory(root)):
            with self.assertRaisesRegex(C.Refusal, "restored NuGet archive is invalid") as caught:
                A.evaluate(control)
        self.assertEqual("STORY_EVIDENCE_FAILED", caught.exception.code)
        self.assertIsNone(control.story_evidence)

    def test_fixture_docker_refuses_a_different_image_and_a_replaced_socket(self):
        docker = self.root / "fake-docker"
        docker.write_text(f"#!{sys.executable} -I\nimport json, sys\nprint(json.dumps(sys.argv[1:]))\n")
        docker.chmod(0o700)
        endpoint = self.root / "d.sock"
        host = "unix://" + str(endpoint)
        owner = "a" * 32
        with socket.socket(socket.AF_UNIX) as original, socket.socket(socket.AF_UNIX) as replacement:
            original.bind(str(endpoint))
            bound = endpoint.lstat()
            wrapper = A.write_fixture_docker(self.root, docker, host, (bound.st_dev, bound.st_ino), owner) / "docker"
            startup = ["--detach", "--rm", "--pull", "never", "--name", "catalog-redis-fixture", "--label",
                       "hexalith.route-catalog-fixture=fixture", "--publish", "127.0.0.1::6379", "--tmpfs", "/data"]
            command = [str(wrapper), "--host", host, "run"]
            accepted = subprocess.run([*command, *startup, A.REDIS_IMAGE, "redis-server", "--save", "", "--appendonly", "no"],
                                      check=True, capture_output=True, text=True)
            self.assertEqual(["--label", A.DOCKER_OWNER_LABEL + "=" + owner], json.loads(accepted.stdout)[3:5])

            # Supplying the pinned string as a flag value cannot authorize a different actual image.
            wrong_image = subprocess.run([*command, *startup, "--label", A.REDIS_IMAGE, "redis:latest", "redis-server"],
                                         capture_output=True, text=True)
            self.assertNotEqual(0, wrong_image.returncode)
            self.assertIn("requires the pinned Redis image", wrong_image.stderr)
            self.assertEqual("", wrong_image.stdout)

            alias = subprocess.run([*command[:3], "container", "run", "redis:latest"], capture_output=True, text=True)
            self.assertNotEqual(0, alias.returncode)
            self.assertIn("refuses an unexpected Docker command", alias.stderr)
            self.assertEqual("", alias.stdout)

            next_endpoint = self.root / "replacement.sock"
            replacement.bind(str(next_endpoint))
            next_endpoint.replace(endpoint)
            changed = subprocess.run([*command, *startup, A.REDIS_IMAGE, "redis-server"], capture_output=True, text=True)
            self.assertNotEqual(0, changed.returncode)
            self.assertIn("Docker socket changed", changed.stderr)
            self.assertEqual("", changed.stdout)

    def private_checkout(self, repository, files, gitlinks=()):
        repository.mkdir(parents=True, exist_ok=True)
        A.Source.write_index(repository, [])
        entries = list(gitlinks)
        for path, content in files.items():
            destination = repository / path
            destination.parent.mkdir(parents=True, exist_ok=True)
            destination.write_bytes(content)
            entries.append({"path": path, "mode": "100644", "git_sha": A.Source.store_object(repository, "blob", content)})
        A.Source.write_index(repository, entries)
        tree = A.Source.git(repository, "write-tree").decode().strip()
        head = A.Source.store_object(
            repository, "commit",
            (f"tree {tree}\nauthor Fixture <fixture@example.test> 0 +0000\n"
             "committer Fixture <fixture@example.test> 0 +0000\n\nNative evidence fixture\n").encode(),
        )
        (repository / ".git/HEAD").write_text(head + "\n")
        return head, tree, entries

    def test_live_inventory_uses_authenticated_root_and_exact_builds_gitlink(self):
        builds = self.root / A.BUILDS
        builds_head, builds_tree, _ = self.private_checkout(builds, {"Props/Directory.Packages.props": b"<Project />\n"})
        files = dict.fromkeys((*A.CONTROL_FILES, A.SPEC, A.PROJECT, A.TEST_SOURCE, A.CANDIDATE_PROFILE, "global.json",
                               "nuget.config", "Directory.Build.props", "Directory.Build.targets",
                               "Directory.Packages.props"), b"fixture\n")
        files[".gitmodules"] = (f'[submodule "Builds"]\npath = {A.BUILDS}\n'
                                'url = https://github.com/Hexalith/Hexalith.Builds.git\n').encode()
        gitlink = {"path": A.BUILDS, "mode": "160000", "git_sha": builds_head}
        head, tree, entries = self.private_checkout(self.root, files, [gitlink])
        documents = {
            "/git/commits/" + head: {"sha": head, "tree": {"sha": tree}},
            "/git/trees/" + tree + "?recursive=1": {
                "sha": tree, "truncated": False,
                "tree": [{"path": row["path"], "mode": row["mode"], "sha": row["git_sha"],
                          "type": "commit" if row["mode"] == "160000" else "blob"} for row in entries],
            },
        }
        control = SimpleNamespace(root=self.root, head=head, api=SimpleNamespace(get=documents.__getitem__))
        manifest = A.inventory(control)
        self.assertEqual((head, tree, builds_head), (manifest["head"], manifest["root_tree"], manifest["submodules"][0]["commit"]))
        A.assert_stable(self.root, manifest)
        control.story = self.claim("spec")
        control.subject = A.subject_digest("c" * 40)
        with mock.patch.object(A, "run") as run:
            with self.assertRaisesRegex(C.Refusal, "build tree differs from the authenticated approval subject"):
                A.evaluate(control)
            run.assert_not_called()

        (self.root / A.TEST_SOURCE).write_bytes(b"different conditional test\n")
        with self.assertRaisesRegex(C.Refusal, "source differs from bound Git blob"):
            A.inventory(control)
        (self.root / A.TEST_SOURCE).write_bytes(files[A.TEST_SOURCE])

        other_builds_head = A.Source.store_object(
            builds, "commit", (f"tree {builds_tree}\n\nOther revision with identical files\n").encode(),
        )
        (builds / ".git/HEAD").write_text(other_builds_head + "\n")
        with self.assertRaisesRegex(C.Refusal, "checkout revision differs from the bound revision"):
            A.inventory(control)
        with self.assertRaisesRegex(C.Refusal, "checkout revision differs from the bound revision"):
            A.assert_stable(self.root, manifest)


class GuardedCompletionRefusals(unittest.TestCase):
    def setUp(self):
        self.writer = W.StoryCompletionTests(methodName="runTest")
        self.writer.setUp()
        self.addCleanup(self.writer.doCleanups)
        self.fixture = self.writer.f
        for path in A.CONTROL_FILES:
            destination = self.fixture.root / path
            destination.write_bytes((T.ROOT / path).read_bytes())
            self.fixture.blob(T.HEAD, path, destination.read_bytes())
        for commit, tree in ((T.HEAD, "c" * 40), (T.BEFORE, "d" * 40)):
            self.fixture.put("/git/commits/" + commit, {"sha": commit, "tree": {"sha": tree}})

    def select(self, target, approved_at="2026-01-02T00:00:00Z"):
        self.writer.select("2.18", target, path=A.SPEC)
        self.fixture.approve(created=approved_at, roles=["product-owner", "test-owner"])
        subject = A.subject_at(SimpleNamespace(get=self.fixture.api_document), T.HEAD)
        for reference in self.fixture.request["approvals"]:
            comment = self.fixture.api_document("/issues/comments/" + str(reference["comment_id"]))
            body = json.loads(comment["body"])
            body["subject_sha256"] = subject
            comment["body"] = C.canonical(body).decode()
            reference["body_sha256"] = C.digest(comment["body"].encode())

    def test_both_writers_require_seal_and_fresh_source_bound_proof(self):
        for target in ("spec", "tracker"):
            with self.subTest(target=target):
                self.select(target)
                self.writer.refused("SEAL_MISSING", target=target)
                missing_source = self.writer.evaluation("SOURCE_MISMATCH")
                self.assertTrue(set(A.CONTROL_FILES) <= {entry["path"] for entry in missing_source["binding"]["inputs"]})
                forged = {**missing_source, "result": "PASS", "codes": [],
                          "story_evidence": {"result": "PASS", "http_status_sequence": [200, 304, 200]}}
                self.fixture.artifact(candidate=forged)
                rejection = self.writer.refused("SOURCE_MISMATCH", target=target, authenticated=True)
                self.assertIn("checkout revision differs", rejection["message"])

    def test_source_only_push_requires_fresh_authenticated_approval(self):
        self.select("spec", approved_at="2026-01-01T23:59:59Z")
        for item in self.fixture.request["subjects"]:
            self.fixture.blob(T.BEFORE, item["path"], (self.fixture.root / item["path"]).read_bytes())
        self.writer.evaluation("APPROVAL_WINDOW")

    def test_post_proof_head_refusal_clears_only_story_2_18_evidence(self):
        self.select("spec")
        control = C.Control(self.fixture.root, self.fixture.request, None)
        evidence = {"result": "PASS"}

        def record_proof():
            control.story_evidence = evidence

        for conditional, retained in ((True, None), (False, evidence)):
            with self.subTest(conditional=conditional):
                control.conditional_story = conditional
                with mock.patch.object(control, "prepare"), mock.patch.object(control, "check_approvals"), \
                     mock.patch.object(control, "prerequisites", side_effect=record_proof), \
                     mock.patch.object(control, "current_head", side_effect=C.Refusal("SOURCE_MISMATCH", "main advanced")):
                    with self.assertRaises(C.Refusal) as caught:
                        control.evaluate()
                self.assertIs(retained, control.result("evaluate", [caught.exception.code])["story_evidence"])


if __name__ == "__main__":
    unittest.main()
