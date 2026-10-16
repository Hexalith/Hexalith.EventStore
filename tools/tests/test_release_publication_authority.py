"""Run the actual EventStore wrapper and exact approved Builds publication engine."""

import hashlib
import json
import os
import shutil
import subprocess
import tempfile
import unittest
import uuid
from datetime import datetime, timedelta, timezone
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
        for name in ("tools/release-publication-identity.py", "tools/release-packages.json",
                     "scripts/validate-publication-preflight.sh", "scripts/validate-release-secrets.sh"):
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
                      "runs": {name: [run.copy()] for name in ("ci.yml", "p1r-qualification.yml", "commitlint.yml")}}
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
            "HEXALITH_RELEASE_EXPECTED_PACKAGE_COUNT": "14", "HEXALITH_RELEASE_REQUIRE_AUTHORITY": "true",
            "HEXALITH_RELEASE_RESERVED_VERSION": self.state["version"],
            "HEXALITH_RELEASE_AUTHORITY_ISSUE_URL": "https://api.github.com/repos/Hexalith/Hexalith.EventStore/issues/123",
            "HEXALITH_RELEASE_AUTHORITY_OWNER": "github:release-owner",
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

    def candidate(self):
        self.invoke("candidate")
        self.candidate_path = self.work / ".hexalith/release-candidate/publication-identity.json"
        self.identity = json.loads(self.candidate_path.read_text())
        self.env["HEXALITH_RELEASE_CANDIDATE_SHA256"] = hashlib.sha256(self.candidate_path.read_bytes()).hexdigest()

    def authorize(self):
        now = datetime.now(timezone.utc) - timedelta(seconds=1)
        timestamp = now.isoformat()
        body = {"schema": "hexalith.release-publication-authority.v1", "role": "release-owner",
                "identity_sha256": self.env["HEXALITH_RELEASE_CANDIDATE_SHA256"],
                "rationale": "Synthetic local test only", "authorized_at": timestamp,
                "expires_at": (now + timedelta(hours=1)).isoformat(), "nonce": uuid.uuid4().hex}
        self.state["comments"] = [{"id": 77, "url": "https://api.github.com/repos/Hexalith/Hexalith.EventStore/issues/comments/77",
                                   "issue_url": self.env["HEXALITH_RELEASE_AUTHORITY_ISSUE_URL"],
                                   "user": {"login": "release-owner"}, "author_association": "OWNER",
                                   "created_at": timestamp, "updated_at": timestamp, "body": json.dumps(body)}]
        self.save()

    def mutate_authority(self, **changes):
        body = json.loads(self.state["comments"][0]["body"])
        body.update(changes)
        self.state["comments"][0]["body"] = json.dumps(body)
        self.save()

    def begin_publish(self):
        self.state["self_tag"] = True
        self.save()

    def test_candidate_exposes_exact_identity_without_authority_or_external_writes(self):
        output = self.work / "candidate-output"
        self.env["GITHUB_OUTPUT"] = str(output)
        self.candidate()
        self.assertEqual(self.identity["run"]["id"], "42")
        self.assertEqual(self.identity["builds"]["action_sha"], BEXEC)
        self.assertEqual(len(self.identity["packages"]["ids"]), 14)
        self.assertFalse(any(item["method"] != "GET" for item in self.state["requests"]))
        self.assertEqual(self.state["comments"], [])
        self.assertFalse((self.work / "nupkgs").exists())
        self.assertIn("candidate-run-attempt=1\n", output.read_text())

    def test_valid_authority_is_consumed_once_before_first_package_write_and_replay_refuses(self):
        self.candidate()
        self.authorize()
        self.invoke("verify")
        self.assertEqual(len(self.state["comments"]), 1)
        self.begin_publish()
        configuration = json.loads((ROOT / ".releaserc.json").read_text())
        command = next(plugin[1]["publishCmd"] for plugin in configuration["plugins"]
                       if isinstance(plugin, list) and plugin[0] == "@semantic-release/exec")
        # Execute the actual hook through the first-write boundary, substituting only
        # the external mutation with a local marker after successful live consumption.
        prefix = command.split(" && dotnet nuget push", 1)[0].replace("${nextRelease.version}", self.state["version"])
        marker = self.work / "first-package-write"
        publish = subprocess.run(["bash", "-c", prefix + ' && touch "$1"', "test", str(marker)],
                                 cwd=self.work, env=self.env, capture_output=True, text=True)
        self.assertEqual(publish.returncode, 0, publish.stderr)
        self.assertTrue(marker.exists())
        marker.unlink()
        self.refresh()
        self.assertEqual(len(self.state["comments"]), 2)
        receipt = json.loads(self.state["comments"][1]["body"])
        self.assertEqual(receipt["identity_sha256"], self.env["HEXALITH_RELEASE_CANDIDATE_SHA256"])
        self.assertEqual(receipt["run_attempt"], "1")
        # The real hook's && ordering cannot reach the first write unless publish preflight succeeds.
        self.assertLess(command.index("validate-publication-preflight.sh"), command.index("dotnet nuget push"))
        marker = self.work / "first-package-write"
        replay = subprocess.run(["bash", "-c", 'bash scripts/validate-publication-preflight.sh "$1" publish && touch "$2"',
                                 "test", self.state["version"], str(marker)], cwd=self.work, env=self.env, capture_output=True)
        self.assertNotEqual(replay.returncode, 0)
        self.assertFalse(marker.exists())
        self.refresh()
        self.assertEqual(len(self.state["comments"]), 2)

    def test_missing_reservation_issue_owner_and_disabled_authority_refuse(self):
        for name in ("HEXALITH_RELEASE_RESERVED_VERSION", "HEXALITH_RELEASE_AUTHORITY_ISSUE_URL",
                     "HEXALITH_RELEASE_AUTHORITY_OWNER"):
            with self.subTest(name=name):
                value = self.env.pop(name)
                self.invoke("verify", False)
                self.env[name] = value
        self.env["HEXALITH_RELEASE_REQUIRE_AUTHORITY"] = "false"
        self.invoke("verify", False)
        self.assertFalse((self.work / "nupkgs").exists())

    def test_missing_genuine_matching_comment_refuses(self):
        self.candidate()
        self.assertIn("authority-missing", self.invoke("verify", False).stderr)

    def test_wrong_owner_and_non_owner_comment_refuse(self):
        self.candidate()
        self.authorize()
        self.state["comments"][0]["user"]["login"] = "another-owner"
        self.save()
        self.assertIn("authority-wrong-role", self.invoke("verify", False).stderr)
        self.authorize()
        self.mutate_authority(role="observer")
        self.assertIn("authority-wrong-role", self.invoke("verify", False).stderr)

    def test_wrong_authority_identity_refuses(self):
        self.candidate()
        self.authorize()
        self.mutate_authority(identity_sha256="0" * 64)
        self.assertIn("authority-missing", self.invoke("verify", False).stderr)

    def test_wrong_version_source_run_attempt_and_contract_refuse_same_run_candidate(self):
        self.candidate()
        self.authorize()
        self.invoke("verify", False, version="99.0.1")
        for key in ("GITHUB_RUN_ID", "GITHUB_RUN_ATTEMPT", "GITHUB_WORKFLOW_SHA"):
            with self.subTest(key=key):
                value = self.env[key]
                self.env[key] = "2" if key != "GITHUB_WORKFLOW_SHA" else "b" * 40
                self.assertIn("candidate-mismatch", self.invoke("verify", False).stderr)
                self.env[key] = value
        self.env["GITHUB_SHA"] = "b" * 40
        self.assertIn("source-no-longer-current", self.invoke("verify", False).stderr)
        self.env["GITHUB_SHA"] = self.sha
        with (self.contract / "smoke-container-platforms.sh").open("a") as output:
            output.write("\n# changed\n")
        self.assertIn("candidate-mismatch", self.invoke("verify", False).stderr)

    def test_missing_or_tampered_candidate_hash_or_bytes_refuse(self):
        self.candidate()
        self.authorize()
        digest = self.env.pop("HEXALITH_RELEASE_CANDIDATE_SHA256")
        self.assertIn("candidate-missing", self.invoke("verify", False).stderr)
        self.env["HEXALITH_RELEASE_CANDIDATE_SHA256"] = digest
        self.candidate_path.write_text("{}")
        self.assertIn("candidate-mismatch", self.invoke("verify", False).stderr)

    def test_expired_authority_refuses_verify_and_publish(self):
        self.candidate()
        self.authorize()
        self.mutate_authority(expires_at=(datetime.now(timezone.utc) - timedelta(seconds=1)).isoformat())
        self.assertIn("authority-expired", self.invoke("verify", False).stderr)
        self.authorize()
        self.invoke("verify")
        self.mutate_authority(expires_at=(datetime.now(timezone.utc) - timedelta(seconds=1)).isoformat())
        self.begin_publish()
        self.assertIn("authority-expired", self.invoke("publish", False).stderr)
        self.assertEqual(len(self.state["comments"]), 1)

    def test_previously_consumed_authority_refuses_verify_and_publish(self):
        self.candidate()
        self.authorize()
        self.invoke("verify")
        self.begin_publish()
        self.invoke("publish")
        self.assertIn("authority-replayed", self.invoke("verify", False).stderr)
        self.assertIn("authority-replayed", self.invoke("publish", False).stderr)

    def test_consumed_nonce_or_identity_on_replacement_authority_refuses(self):
        self.candidate()
        self.authorize()
        body = json.loads(self.state["comments"][0]["body"])
        for changes in ({"nonce": body["nonce"], "identity_sha256": "f" * 64},
                        {"nonce": uuid.uuid4().hex, "identity_sha256": body["identity_sha256"]}):
            with self.subTest(changes=changes):
                self.state["comments"] = self.state["comments"][:1]
                self.state["comments"].append({"id": 999, "user": {"login": "github-actions[bot]"},
                    "body": json.dumps({"schema": "hexalith.release-publication-authority-consumption.v1",
                                        "authority_comment_id": 66, **changes})})
                self.save()
                self.assertIn("authority-replayed", self.invoke("verify", False).stderr)

    def test_untrusted_comment_cannot_forge_cross_comment_consumption(self):
        self.candidate()
        self.authorize()
        body = json.loads(self.state["comments"][0]["body"])
        self.state["comments"].append({"id": 999, "user": {"login": "issue-commenter"},
            "body": json.dumps({"schema": "hexalith.release-publication-authority-consumption.v1",
                                "authority_comment_id": 66, "nonce": body["nonce"],
                                "identity_sha256": body["identity_sha256"]})})
        self.save()
        self.invoke("verify")

    def test_stale_main_and_missing_failed_or_wrong_push_proofs_refuse(self):
        for workflow in ("ci.yml", "p1r-qualification.yml", "commitlint.yml"):
            original = self.state["runs"][workflow][0].copy()
            for field, value in (("conclusion", "failure"), ("event", "pull_request"),
                                 ("head_sha", "b" * 40), ("status", "in_progress")):
                with self.subTest(workflow=workflow, field=field):
                    self.state["runs"][workflow] = [{**original, field: value}]
                    self.save()
                    self.assertIn("source-ci-not-successful", self.invoke("candidate", False).stderr)
            self.state["runs"][workflow] = []
            self.save()
            self.assertIn("source-ci-not-successful", self.invoke("candidate", False).stderr)
            self.state["runs"][workflow] = [original]
        self.state["live_sha"] = "b" * 40
        self.save()
        self.assertIn("source-no-longer-current", self.invoke("candidate", False).stderr)

    def test_publish_rechecks_all_three_proofs(self):
        self.candidate()
        self.authorize()
        self.invoke("verify")
        self.begin_publish()
        for workflow in ("ci.yml", "p1r-qualification.yml", "commitlint.yml"):
            original = self.state["runs"][workflow]
            self.state["runs"][workflow] = []
            self.save()
            self.assertIn("source-ci-not-successful", self.invoke("publish", False).stderr)
            self.state["runs"][workflow] = original
        self.assertEqual(len(self.state["comments"]), 1)

    def test_unauthenticated_registry_is_blocker_even_for_historical_proposal(self):
        self.state["version"] = "3.122.1"
        self.env["HEXALITH_RELEASE_RESERVED_VERSION"] = self.state["version"]
        self.save()
        self.candidate()
        self.authorize()
        self.state["registry_status"] = 401
        self.save()
        self.assertIn("version-floor-unavailable", self.invoke("verify", False).stderr)
        self.assertEqual(len(self.state["comments"]), 1)

    def test_actual_workflow_source_step_refuses_bypass_and_requires_all_workflows(self):
        workflow = (ROOT / ".github/workflows/release.yml").read_text()
        step = workflow.split("      - name: Require current main with selected exact-source proof\n", 1)[1]
        script = step.split("        run: |\n", 1)[1].split("      - uses:", 1)[0]
        script = "\n".join(line[10:] if line.startswith("          ") else line for line in script.splitlines())
        output = self.work / "github-output"
        env = {**self.env, "BYPASS_VALIDATION": "true", "GITHUB_OUTPUT": str(output)}
        result = subprocess.run(["bash", "-c", script], cwd=self.work, env=env, capture_output=True)
        self.assertNotEqual(result.returncode, 0)
        self.assertFalse(output.exists())
        self.assertIn("for required_workflow in ci.yml p1r-qualification.yml commitlint.yml", script)
        self.assertIn("environment: production", workflow)
        self.assertLess(workflow.index("Upload read-only candidate identity"), workflow.index("  release:"))

    def test_actual_workflow_refuses_prior_candidate_attempt_before_artifact_download(self):
        workflow = (ROOT / ".github/workflows/release.yml").read_text()
        name = "Require candidate from current run attempt"
        step = workflow.split("      - name: " + name + "\n", 1)[1]
        script = step.split("        run: |\n", 1)[1].split("      - uses:", 1)[0]
        script = "\n".join(line[10:] if line.startswith("          ") else line for line in script.splitlines())
        for candidate_attempt in ("1", ""):
            result = subprocess.run(["bash", "-c", script], cwd=self.work,
                env={**self.env, "GITHUB_RUN_ATTEMPT": "2", "CANDIDATE_RUN_ATTEMPT": candidate_attempt},
                capture_output=True, text=True)
            self.assertNotEqual(result.returncode, 0)
            self.assertIn("Rerun all jobs and obtain fresh owner authority", result.stderr)
        result = subprocess.run(["bash", "-c", script], cwd=self.work,
            env={**self.env, "GITHUB_RUN_ATTEMPT": "2", "CANDIDATE_RUN_ATTEMPT": "2"},
            capture_output=True, text=True)
        self.assertEqual(result.returncode, 0, result.stderr)
        self.assertIn("candidate-run-attempt: ${{ steps.candidate.outputs.candidate-run-attempt }}", workflow)
        self.assertLess(workflow.index(name), workflow.index("Download same-run candidate identity"))


if __name__ == "__main__":
    unittest.main()
