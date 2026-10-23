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
BEXEC = "22a578b576a515d2af214fe81859447fffc97981"
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
                     "scripts/validate-release-secrets.sh"):
            target = self.work / name
            target.parent.mkdir(exist_ok=True)
            shutil.copyfile(ROOT / name, target)
        self.site = self.work / "site"
        self.site.mkdir()
        (self.site / "sitecustomize.py").write_text("from release_publication_fixture import install\ninstall()\n")
        self.state_path = self.work / "state.json"
        self.sha = "a" * 40
        run = {"id": 123, "head_sha": self.sha, "head_branch": "main", "event": "push",
               "status": "completed", "conclusion": "success"}
        self.state = {"source_sha": self.sha, "live_sha": self.sha, "version": "99.0.0",
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
        self.state["live_sha"] = "b" * 40
        self.save()
        self.assertIn("source-no-longer-current", self.invoke("verify", False).stderr)

    def test_workflow_has_no_dispatch_inputs_and_requires_two_push_proofs(self):
        workflow = (ROOT / ".github/workflows/release.yml").read_text()
        self.assertNotIn("inputs:", workflow)
        self.assertIn("for required_workflow in ci.yml commitlint.yml", workflow)
        self.assertIn("HEXALITH_RELEASE_REQUIRE_AUTHORITY: 'false'", workflow)
        self.assertNotIn("release-version", workflow)
        self.assertIn("environment: production", workflow)
        self.assertLess(
            workflow.index("Require current main with exact-source push proofs"),
            workflow.index("  release:"))


if __name__ == "__main__":
    unittest.main()
