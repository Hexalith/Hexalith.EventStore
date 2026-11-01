"""Run the EventStore wrapper against the approved Builds publication engine.

Semantic Release selects the version. The production environment approves
publication. A typed reservation or one-use issue authority is refused.
"""

import json
import os
import shutil
import subprocess
import tempfile
import unittest
import uuid
from pathlib import Path


ROOT = Path(__file__).resolve().parents[2]
BEXEC = "30ea43a56330a9828ed0bb2e837289231d8630d1"
CONTRACT_FILES = (
    "publish-containers.sh", "oci_registry_validator.py", "publication_preflight.py",
    "smoke-container-platforms.sh", "smoke_container_platforms.py",
)


class ReleasePublicationAuthorityTests(unittest.TestCase):
    def setUp(self):
        self.temporary = tempfile.TemporaryDirectory(prefix="publication-authority-")
        self.addCleanup(self.temporary.cleanup)
        self.work = Path(self.temporary.name)
        builds = Path(os.environ.get("HEXALITH_BUILDS_SOURCE", ROOT / "references/Hexalith.Builds"))
        self.contract = self.work / ".hexalith/release"
        self.contract.mkdir(parents=True)
        for name in CONTRACT_FILES:
            data = subprocess.check_output([
                "git", "-C", str(builds), "show", f"{BEXEC}:Github/publish-containers/{name}"])
            (self.contract / name).write_bytes(data)
            (self.contract / name).chmod(0o755)
        for name in ("tools/release-packages.json", "scripts/validate-publication-preflight.sh",
                     "scripts/validate-release-secrets.sh", "scripts/verify-release-source.sh"):
            target = self.work / name
            target.parent.mkdir(exist_ok=True)
            shutil.copyfile(ROOT / name, target)
        self.site = self.work / "site"
        self.site.mkdir()
        (self.site / "sitecustomize.py").write_text("from release_publication_fixture import install\ninstall()\n")
        fake_bin = self.work / "bin"
        fake_bin.mkdir()
        gh = fake_bin / "gh"
        gh.write_text("#!/usr/bin/env python3\nfrom release_publication_fixture import gh_main\ngh_main()\n")
        gh.chmod(0o755)
        self.fake_bin = fake_bin
        self.state_path = self.work / "state.json"
        self.sha = "a" * 40
        run = {"id": 123, "head_sha": self.sha, "head_branch": "main", "event": "push",
               "status": "completed", "conclusion": "success"}
        self.state = {"source_sha": self.sha, "refs": {"main": self.sha, "release/parties-sdk-3.130.2": "c" * 40}, "version": "99.0.0",
                      "comments": [], "requests": [],
                      "runs": {name: [run.copy()] for name in ("ci.yml", "commitlint.yml")}}
        self.save()
        self.env = {key: value for key, value in os.environ.items()
                    if not key.startswith(("GITHUB_", "HEXALITH_RELEASE_", "HEXALITH_PUBLICATION_"))}
        self.env.update({
            "GITHUB_WORKSPACE": str(self.work), "GITHUB_REPOSITORY": "Hexalith/Hexalith.EventStore",
            "GITHUB_SHA": self.sha, "GITHUB_WORKFLOW_SHA": self.sha, "GITHUB_REF": "refs/heads/main",
            "GITHUB_RUN_ID": "42", "GITHUB_RUN_ATTEMPT": "1", "GITHUB_RUN_NUMBER": "9",
            "GITHUB_EVENT_NAME": "workflow_dispatch", "GITHUB_ACTOR": "release-owner",
            "GITHUB_TRIGGERING_ACTOR": "release-owner",
            "GITHUB_WORKFLOW_REF": "Hexalith/Hexalith.EventStore/.github/workflows/release.yml@refs/heads/main",
            "GITHUB_TOKEN": uuid.uuid4().hex, "HEXALITH_ZOT_USERNAME": uuid.uuid4().hex,
            "HEXALITH_ZOT_API_KEY": uuid.uuid4().hex,
            "HEXALITH_BUILDS_EXECUTION_SHA": BEXEC, "HEXALITH_RELEASE_ENVIRONMENT": "production",
            "HEXALITH_RELEASE_SOURCE_BRANCH": "main", "HEXALITH_RELEASE_SOURCE_CI_WORKFLOW": "ci.yml",
            "HEXALITH_RELEASE_PACKAGE_MANIFEST": "tools/release-packages.json",
            "HEXALITH_RELEASE_EXPECTED_PACKAGE_COUNT": "14", "HEXALITH_RELEASE_REQUIRE_AUTHORITY": "false",
            "NUGET_TRUSTED_PUBLISHING_KEY": uuid.uuid4().hex,
            "HEXALITH_CONTAINER_PROJECTS": "src/Hexalith.EventStore/Hexalith.EventStore.csproj|eventstore",
            "PUBLICATION_FIXTURE_STATE": str(self.state_path),
            "PATH": str(self.fake_bin) + os.pathsep + os.environ["PATH"],
            "PYTHONPATH": os.pathsep.join((str(self.site), str(ROOT / "tools/tests"))),
        })

    def save(self):
        self.state_path.write_text(json.dumps(self.state))

    def refresh(self):
        self.state = json.loads(self.state_path.read_text())

    def invoke(self, phase, success=True, version=None):
        result = subprocess.run(["bash", "scripts/validate-publication-preflight.sh",
                                 version or self.state["version"], phase], cwd=self.work,
                                env=self.env, capture_output=True, text=True)
        self.refresh()
        if success:
            self.assertEqual(result.returncode, 0, result.stdout + result.stderr)
        else:
            self.assertNotEqual(result.returncode, 0, result.stdout + result.stderr)
        return result

    def begin_publish(self):
        self.state["self_tag"] = True
        self.save()

    def test_semantic_release_version_publishes_without_a_typed_reservation(self):
        self.invoke("verify")
        self.assertEqual(self.state["comments"], [])
        self.begin_publish()
        self.invoke("publish")
        self.assertEqual(self.state["comments"], [])
        self.assertFalse(any(item["method"] == "POST" for item in self.state["requests"]))

    def test_typed_reservation_and_enabled_authority_refuse(self):
        self.env["HEXALITH_RELEASE_RESERVED_VERSION"] = self.state["version"]
        self.assertIn("must be empty", self.invoke("verify", False).stderr)
        del self.env["HEXALITH_RELEASE_RESERVED_VERSION"]
        self.env["HEXALITH_RELEASE_REQUIRE_AUTHORITY"] = "true"
        self.assertIn("must be exactly false", self.invoke("verify", False).stderr)
        self.assertFalse((self.work / "nupkgs").exists())

    def test_missing_ci_proof_and_stale_main_refuse(self):
        self.state["runs"]["ci.yml"] = []
        self.save()
        self.assertIn("source-ci-not-successful", self.invoke("verify", False).stderr)
        self.state["runs"]["ci.yml"] = [{
            "id": 123, "head_sha": self.sha, "head_branch": "main", "event": "push",
            "status": "completed", "conclusion": "success"}]
        self.state["refs"][self.env["HEXALITH_RELEASE_SOURCE_BRANCH"]] = "b" * 40
        self.save()
        self.assertIn("source-no-longer-current", self.invoke("verify", False).stderr)

    def maintenance(self):
        self.state.pop("self_tag", None)
        container_evidence = self.work / ("evidence-" + uuid.uuid4().hex)
        self.env["HEXALITH_CONTAINER_EVIDENCE_DIRECTORY"] = str(container_evidence)
        self.env["HEXALITH_RELEASE_EVIDENCE_DIRECTORY"] = str(container_evidence / "preflight")
        branch = "release/parties-sdk-3.130.2"
        self.sha = self.state["refs"][branch]
        self.state["source_sha"] = self.sha
        self.env["GITHUB_SHA"] = self.sha
        self.env["GITHUB_WORKFLOW_SHA"] = self.sha
        self.state["version"] = "3.130.2"
        self.env["HEXALITH_RELEASE_SOURCE_BRANCH"] = branch
        self.env["GITHUB_REF"] = "refs/heads/" + branch
        self.env["GITHUB_WORKFLOW_REF"] = "Hexalith/Hexalith.EventStore/.github/workflows/release.yml@refs/heads/" + branch
        for name in ("ci.yml", "commitlint.yml", "integration.yml"):
            self.state["runs"][name] = [{"id": 123, "head_sha": self.sha,
                "head_branch": branch, "event": "push", "status": "completed", "conclusion": "success"}]
        self.save()

    def test_exact_maintenance_verify_and_publish_use_actual_pinned_engine(self):
        self.maintenance()
        self.invoke("verify")
        frozen = json.loads((Path(self.env["HEXALITH_RELEASE_EVIDENCE_DIRECTORY"]) / "publication-identity.json").read_text())
        self.assertEqual(set(frozen["source"]["required_workflow_runs"]), {"ci.yml", "commitlint.yml", "integration.yml"})
        self.begin_publish()
        self.invoke("publish")
        self.assertEqual(self.state["comments"], [])
        self.assertFalse(any(item["method"] == "POST" for item in self.state["requests"]))

    def test_maintenance_refuses_each_missing_proof_in_both_phases(self):
        for phase in ("verify", "publish"):
            for name in ("ci.yml", "commitlint.yml", "integration.yml"):
                with self.subTest(phase=phase, workflow=name):
                    self.maintenance()
                    self.invoke("verify")
                    self.begin_publish()
                    self.state["runs"][name] = []
                    self.save()
                    self.assertIn(name, self.invoke(phase, False).stderr)

    def test_maintenance_refuses_stale_head_and_foreign_tuple(self):
        for phase in ("verify", "publish"):
            for fault in ("stale", "branch", "version", "repository", "workflow", "environment", "reservation"):
                with self.subTest(phase=phase, fault=fault):
                    self.setUp()
                    self.maintenance()
                    self.invoke("verify")
                    self.begin_publish()
                    if fault == "stale": self.state["refs"][self.env["HEXALITH_RELEASE_SOURCE_BRANCH"]] = "b" * 40
                    if fault == "branch": self.env["HEXALITH_RELEASE_SOURCE_BRANCH"] = "release/other"
                    if fault == "version": self.state["version"] = "3.130.3"
                    if fault == "repository": self.env["GITHUB_REPOSITORY"] = "Other/Repository"
                    if fault == "workflow": self.env["HEXALITH_RELEASE_SOURCE_CI_WORKFLOW"] = "other.yml"
                    if fault == "environment": self.env["HEXALITH_RELEASE_ENVIRONMENT"] = "staging"
                    if fault == "reservation": self.env["HEXALITH_RELEASE_RESERVED_VERSION"] = "3.130.2"
                    self.save()
                    self.invoke(phase, False)

    def test_wrong_main_query_mutation_fails_wrapper_with_distinct_heads(self):
        script = self.work / "scripts/verify-release-source.sh"
        original = script.read_text()
        script.write_text(original.replace('git/ref/heads/${source_branch}', 'git/ref/heads/main'))
        self.assertNotEqual(script.read_text(), original)
        self.invoke("verify")
        self.maintenance()
        self.assertIn("current selected branch tip", self.invoke("verify", False).stderr)

    def shell_boundary(self):
        project = self.work / "src/Hexalith.EventStore/Hexalith.EventStore.csproj"
        project.parent.mkdir(parents=True, exist_ok=True)
        project.write_text("<Project />")
        docker = self.fake_bin / "docker"
        docker.write_text("#!/usr/bin/env bash\necho publication-boundary-reached >&2\nexit 73\n")
        docker.chmod(0o755)
        return subprocess.run(["bash", str(self.contract / "publish-containers.sh"), self.state["version"]],
            cwd=self.work, env=self.env, capture_output=True, text=True)

    def test_actual_canonical_shell_accepts_finite_source_before_first_write(self):
        self.maintenance()
        self.invoke("verify")
        self.begin_publish()
        self.invoke("publish")
        result = self.shell_boundary()
        self.assertEqual(result.returncode, 73, result.stdout + result.stderr)
        self.assertIn("publication-boundary-reached", result.stderr)
        builds = Path(os.environ.get("HEXALITH_BUILDS_SOURCE", ROOT / "references/Hexalith.Builds"))
        baseline = subprocess.check_output(["git", "-C", str(builds), "show",
            "22a578b576a515d2af214fe81859447fffc97981:Github/publish-containers/publish-containers.sh"])
        (self.contract / "publish-containers.sh").write_bytes(baseline)
        result = self.shell_boundary()
        self.assertNotEqual(result.returncode, 73)
        self.assertIn("Release source branch must be exactly main", result.stderr)

    def test_actual_canonical_shell_revalidates_all_frozen_maintenance_proofs(self):
        for name in ("ci.yml", "commitlint.yml", "integration.yml"):
            with self.subTest(workflow=name):
                self.maintenance()
                self.invoke("verify")
                self.begin_publish()
                self.invoke("publish")
                self.state["runs"][name] = []
                self.save()
                result = self.shell_boundary()
                self.assertNotEqual(result.returncode, 73)
                self.assertIn(name, result.stderr)
                self.assertNotIn("publication-boundary-reached", result.stderr)

    def test_workflow_uses_shared_source_proof_before_production_and_token(self):
        workflow = (ROOT / ".github/workflows/release.yml").read_text()
        self.assertNotIn("inputs:", workflow)
        self.assertEqual(workflow.count("bash scripts/verify-release-source.sh"), 2)
        self.assertIn("HEXALITH_RELEASE_REQUIRE_AUTHORITY: 'false'", workflow)
        self.assertIn("environment: production", workflow)
        self.assertLess(workflow.index("Require current approved branch with exact-source push proofs"), workflow.index("  release:"))
        self.assertLess(workflow.index("Revalidate current source before Semantic Release"), workflow.index("Exchange GitHub OIDC"))


if __name__ == "__main__":
    unittest.main()
