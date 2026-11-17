"""Behavioral proof for the complete finite pre-production/pre-token source gate."""

import json
import os
import subprocess
import tempfile
import unittest
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
MAINTENANCE = "release/parties-sdk-3.130.2"


class ReleaseSourceBranchTests(unittest.TestCase):
    def setUp(self):
        self.temporary = tempfile.TemporaryDirectory(prefix="release-source-")
        self.addCleanup(self.temporary.cleanup)
        self.work = Path(self.temporary.name)
        gh = self.work / "gh"
        gh.write_text("#!/usr/bin/env python3\nfrom release_publication_fixture import gh_main\ngh_main()\n")
        gh.chmod(0o755)
        self.refs = {"main": "a" * 40, MAINTENANCE: "c" * 40}
        self.sha = self.refs["main"]
        self.path = self.work / "state.json"
        self.env = {key: value for key, value in os.environ.items()
                    if not key.startswith(("GITHUB_", "DISPATCH_", "REPOSITORY"))}
        self.env.update({"PATH": str(self.work) + os.pathsep + os.environ["PATH"],
                         "PYTHONPATH": str(ROOT / "tools/tests"),
                         "PUBLICATION_FIXTURE_STATE": str(self.path),
                         "REPOSITORY": "Hexalith/Hexalith.EventStore", "DISPATCH_SHA": self.sha,
                         "GITHUB_OUTPUT": str(self.work / "output")})

    def invoke(self, branch="main", fault=None, script=None):
        self.sha = self.refs.get(branch, "d" * 40)
        self.env["DISPATCH_SHA"] = self.sha
        self.env["DISPATCH_REF"] = "refs/heads/" + branch
        run = {"head_sha": self.sha, "head_branch": branch, "event": "push",
               "status": "completed", "conclusion": "success"}
        state = {"refs": self.refs.copy(), "requests": [], "runs": {
            name: [run.copy()] for name in ("ci.yml", "commitlint.yml", "integration.yml")}}
        if fault == "stale": state["refs"][branch] = "b" * 40
        if fault == "bad-sha": self.env["DISPATCH_SHA"] = "not-a-sha"
        if fault == "repository": self.env["REPOSITORY"] = "Other/Repository"
        if fault in state["runs"]: state["runs"][fault] = []
        if fault in ("head_sha", "head_branch", "event", "status", "conclusion"):
            for runs in state["runs"].values(): runs[0][fault] = "wrong"
        self.path.write_text(json.dumps(state))
        return subprocess.run(["bash", str(script or ROOT / "scripts/verify-release-source.sh")],
                              env=self.env, capture_output=True, text=True)

    def test_only_main_and_exact_maintenance_pass(self):
        for branch in ("main", MAINTENANCE):
            result = self.invoke(branch)
            self.assertEqual(result.returncode, 0, result.stderr)
        for branch in ("release/parties-sdk-3.130.3", "release/other", "other", "v3.130.2"):
            self.assertNotEqual(self.invoke(branch).returncode, 0)

    def test_wrong_main_query_mutation_is_detected_by_distinct_live_heads(self):
        mutant = self.work / "wrong-main-query.sh"
        original = (ROOT / "scripts/verify-release-source.sh").read_text()
        mutated = original.replace('git/ref/heads/${source_branch}', 'git/ref/heads/main')
        self.assertNotEqual(original, mutated)
        mutant.write_text(mutated)
        self.assertEqual(self.invoke("main", script=mutant).returncode, 0)
        self.assertNotEqual(self.invoke(MAINTENANCE, script=mutant).returncode, 0)

    def test_unknown_refs_are_not_invented_by_fixture(self):
        self.path.write_text(json.dumps({"refs": self.refs, "requests": []}))
        result = subprocess.run([str(self.work / "gh"), "api", "repos/Hexalith/Hexalith.EventStore/git/ref/heads/unknown", "--jq", ".object.sha"],
                                env=self.env, capture_output=True, text=True)
        self.assertNotEqual(result.returncode, 0)

    def test_every_source_property_and_required_workflow_fails_closed(self):
        for branch in ("main", MAINTENANCE):
            for fault in ("stale", "bad-sha", "repository", "head_sha", "head_branch", "event",
                          "status", "conclusion", "ci.yml", "commitlint.yml"):
                with self.subTest(branch=branch, fault=fault):
                    self.env["REPOSITORY"] = "Hexalith/Hexalith.EventStore"
                    self.env["DISPATCH_SHA"] = self.sha
                    self.assertNotEqual(self.invoke(branch, fault).returncode, 0)
        self.env["REPOSITORY"] = "Hexalith/Hexalith.EventStore"
        self.env["DISPATCH_SHA"] = self.sha
        self.assertNotEqual(self.invoke(MAINTENANCE, "integration.yml").returncode, 0)
        self.assertEqual(self.invoke("main", "integration.yml").returncode, 0)


if __name__ == "__main__":
    unittest.main()
