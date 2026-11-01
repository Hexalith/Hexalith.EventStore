"""Native analyzer over owned real Git ancestry; these fixtures are not live proof."""
import json
import os
import subprocess
import tempfile
import unittest
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
BASE = "1de42b706f52af0da6513f0b7663002606d17156"
BRANCH = "release/parties-sdk-3.130.2"


class ReleaseCandidateTests(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory(prefix="native-release-candidate-")
        self.addCleanup(self.temp.cleanup)
        self.work = Path(self.temp.name)
        self.git("init", "-q")
        # Read original committed objects without modifying the source repository.
        common = subprocess.check_output(["git", "rev-parse", "--path-format=absolute", "--git-common-dir"], cwd=ROOT, text=True).strip()
        (self.work / ".git/objects/info/alternates").write_text(str(Path(common) / "objects") + "\n")
        self.git("config", "user.name", "Release Fixture")
        self.git("config", "user.email", "fixture@example.invalid")
        self.git("update-ref", "refs/heads/" + BRANCH, BASE)
        self.git("symbolic-ref", "HEAD", "refs/heads/" + BRANCH)
        self.git("read-tree", BASE)
        self.git("tag", "v3.130.1", BASE)
        self.config = json.loads((ROOT / ".releaserc.json").read_text())
        self.write_config()
        (self.work / "package.json").write_bytes((ROOT / "package.json").read_bytes())
        self.git("add", ".releaserc.json", "package.json")
        self.env = {key: value for key, value in os.environ.items()
                    if not key.startswith(("GITHUB_", "REPOSITORY", "DISPATCH_"))}
        self.env.update({"REPOSITORY": "Hexalith/Hexalith.EventStore", "DISPATCH_REF": "refs/heads/" + BRANCH})

    def git(self, *arguments, input=None):
        return subprocess.check_output(["git", *arguments], cwd=self.work, text=True, input=input, stderr=subprocess.PIPE).strip()

    def write_config(self):
        (self.work / ".releaserc.json").write_text(json.dumps(self.config))

    def candidate(self, message="fix: isolate technical receiver verbs"):
        # Even fixture commit subjects use the repository-pinned validator.
        subprocess.run([str(ROOT / "node_modules/.bin/commitlint"), "--config", str(ROOT / "commitlint.config.mjs")],
                       cwd=ROOT, input=message + "\n", text=True, check=True, capture_output=True)
        self.git("commit", "-q", "--allow-empty", "-m", message)
        head = self.git("rev-parse", "HEAD")
        self.env["DISPATCH_SHA"] = head
        # Actual main and maintenance heads differ in this real Git fixture.
        self.git("update-ref", "refs/heads/main", BASE)
        self.assertNotEqual(self.git("rev-parse", "refs/heads/main"), head)
        return head

    def invoke(self, success=False):
        result = subprocess.run(["node", str(ROOT / "tools/verify-release-candidate.mjs")],
                                cwd=self.work, env=self.env, capture_output=True, text=True)
        if success:
            self.assertEqual(result.returncode, 0, result.stdout + result.stderr)
        else:
            self.assertNotEqual(result.returncode, 0, result.stdout + result.stderr)
        self.assertFalse((self.work / "nupkgs").exists())
        self.assertFalse((self.work / ".hexalith").exists())
        return result

    def test_real_patch_candidate_uses_native_analyzer_and_core(self):
        head = self.candidate()
        result = json.loads(self.invoke(True).stdout)
        self.assertEqual({key: result[key] for key in ("source", "base", "type", "version", "branch")},
                         {"source": head, "base": BASE, "type": "patch", "version": "3.130.2", "branch": BRANCH})
        self.assertEqual(result["native"], {"semanticRelease": "25.0.9", "commitAnalyzer": "13.0.1", "cosmiconfig": "9.0.2"})
        self.assertEqual(result["configuration"]["path"], ".releaserc.json")
        self.assertRegex(result["configuration"]["sha256"], r"^[0-9a-f]{64}$")
        self.assertEqual(result["range"], ">=3.130.1")

    def test_no_release_minor_and_major_refuse_before_authority(self):
        for message in ("docs: explain technical receivers", "feat: extend receiver support", "fix!: change receiver contract"):
            with self.subTest(message=message):
                self.setUp()
                self.candidate(message)
                self.assertIn("must be patch 3.130.2", self.invoke().stderr)

    def test_wrong_source_and_unapproved_refs_refuse(self):
        self.candidate()
        for field, value in (("DISPATCH_SHA", BASE), ("DISPATCH_REF", "refs/heads/main"),
                             ("DISPATCH_REF", "refs/heads/release/other"), ("REPOSITORY", "Other/Repository")):
            with self.subTest(field=field, value=value):
                old = self.env[field]
                self.env[field] = value
                self.invoke()
                self.env[field] = old

    def test_missing_wrong_unreachable_and_later_bases_refuse(self):
        for fault in ("missing", "wrong", "unreachable", "later", "shallow"):
            with self.subTest(fault=fault):
                self.setUp()
                head = self.candidate()
                if fault == "missing": self.git("tag", "-d", "v3.130.1")
                if fault == "wrong": self.git("tag", "-f", "v3.130.1", head)
                if fault == "unreachable":
                    message = "fix: isolated fixture\n"
                    subprocess.run([str(ROOT / "node_modules/.bin/commitlint"), "--config", str(ROOT / "commitlint.config.mjs")],
                                   cwd=ROOT, input=message, text=True, check=True, capture_output=True)
                    orphan = self.git("commit-tree", self.git("write-tree"), input=message)
                    self.git("update-ref", "HEAD", orphan)
                    self.env["DISPATCH_SHA"] = orphan
                if fault == "later": self.git("tag", "v3.130.2", head)
                if fault == "shallow": (self.work / ".git/shallow").write_text(BASE + "\n")
                self.invoke()

    def test_unsupported_analyzer_configurations_refuse(self):
        for fault in ("analyzer-options", "extra-plugin", "extends", "exec-analyzer", "channel", "tag-format", "package-config", "alternate-config"):
            with self.subTest(fault=fault):
                self.setUp()
                if fault == "analyzer-options": self.config["plugins"][0] = ["@semantic-release/commit-analyzer", {"releaseRules": []}]
                if fault == "extra-plugin": self.config["plugins"].append("unapproved-analyzer")
                if fault == "extends": self.config["extends"] = "unapproved-config"
                if fault == "exec-analyzer": self.config["plugins"][2][1]["analyzeCommitsCmd"] = "echo patch"
                if fault == "channel": self.config["branches"][0]["channel"] = "next"
                if fault == "tag-format": self.config["tagFormat"] = "${version}"
                if fault == "package-config": (self.work / "package.json").write_text(json.dumps({"release": {"plugins": []}}))
                if fault == "alternate-config": (self.work / "release.config.js").write_text("throw new Error('must never execute');")
                self.write_config()
                self.git("add", ".releaserc.json", "package.json")
                self.candidate()
                self.assertIn("configuration", self.invoke().stderr)

    def test_native_cosmiconfig_redirect_refuses_before_hooks_or_authority(self):
        (self.work / ".config").mkdir()
        (self.work / ".config/config.json").write_text(json.dumps({"cosmiconfig": {
            "searchPlaces": ["alternate-release.json"], "mergeSearchPlaces": False}}))
        alternate = {"branches": ["main"], "plugins": [["@semantic-release/commit-analyzer", {
            "releaseRules": [{"type": "fix", "release": "minor"}]}]]}
        (self.work / "alternate-release.json").write_text(json.dumps(alternate))
        self.git("add", ".config/config.json", "alternate-release.json")
        self.candidate()
        self.assertIn("Native effective release configuration differs", self.invoke().stderr)

    def sibling_tag(self, unrelated=False):
        message = "fix: advance sibling release\n"
        subprocess.run([str(ROOT / "node_modules/.bin/commitlint"), "--config", str(ROOT / "commitlint.config.mjs")],
                       cwd=ROOT, input=message, text=True, check=True, capture_output=True)
        ancestry = [] if unrelated else ["-p", BASE]
        sibling = self.git("commit-tree", self.git("write-tree"), *ancestry, input=message)
        if not unrelated:
            self.git("update-ref", "refs/heads/main", sibling)
        self.git("tag", "v3.130.2", sibling)
        return sibling

    def test_native_branch_range_refuses_main_sibling_candidate_tag(self):
        self.candidate()
        self.sibling_tag()
        self.assertIn("outside native branch type/range: >=3.130.1 <3.130.2", self.invoke().stderr)

    def test_global_candidate_tag_on_unrelated_history_refuses(self):
        self.candidate()
        self.sibling_tag(unrelated=True)
        self.assertIn("global fetched tag inventory", self.invoke().stderr)

    def workflow(self):
        # Use the installed YAML parser against the actual workflow, rather than
        # interpreting indentation or asserting the qualifier text is present.
        script = """import {createRequire} from 'node:module';
const require=createRequire(process.argv[1]);
const {readFileSync}=require('node:fs');
process.stdout.write(JSON.stringify(require('js-yaml').load(readFileSync(process.argv[2],'utf8'))));"""
        return json.loads(subprocess.check_output(["node", "--input-type=module", "-e", script,
            str(ROOT / "package.json"), str(ROOT / ".github/workflows/release.yml")], text=True))

    def drive_preproduction(self, workflow):
        sentinel = self.work / "production-entered"
        sentinel.unlink(missing_ok=True)
        contexts = {"github.repository": self.env["REPOSITORY"], "github.ref": self.env["DISPATCH_REF"],
                    "github.sha": self.env["DISPATCH_SHA"]}
        result = None
        for step in workflow["jobs"]["verify-source"]["steps"]:
            command = step.get("run", "")
            if "node tools/verify-release-candidate.mjs" not in command:
                continue  # Checkout, runtime install, and source proofs have separate fixture boundaries.
            condition = step.get("if", "${{ true }}")
            prefix, suffix = "${{ github.ref == '", "' }}"
            if not condition.startswith(prefix) or not condition.endswith(suffix):
                raise AssertionError("Unsupported qualifier condition: " + condition)
            if contexts["github.ref"] != condition[len(prefix):-len(suffix)]:
                continue
            environment = self.env.copy()
            for key, value in step.get("env", {}).items():
                expression = value.removeprefix("${{ ").removesuffix(" }}")
                environment[key] = contexts[expression]
            command = command.replace("node tools/verify-release-candidate.mjs", "node " + str(ROOT / "tools/verify-release-candidate.mjs"))
            result = subprocess.run(["bash", "-c", command], cwd=self.work, env=environment, capture_output=True, text=True)
            if result.returncode != 0:
                return result, sentinel
        # Stand-in for entering the dependent protected production job, before
        # credentials or publication. It runs only when preproduction succeeds.
        subprocess.run(["bash", "-c", "touch production-entered"], cwd=self.work, check=True)
        return result, sentinel

    def assert_invalid_candidate_cannot_enter_production(self, workflow):
        result, sentinel = self.drive_preproduction(workflow)
        self.assertFalse(sentinel.exists(), "An invalid maintenance candidate entered production.")
        self.assertIsNotNone(result)
        self.assertNotEqual(result.returncode, 0)

    def test_actual_workflow_condition_blocks_invalid_candidate_and_detects_main_mutation(self):
        self.candidate("docs: explain technical receivers")
        workflow = self.workflow()
        self.assert_invalid_candidate_cannot_enter_production(workflow)
        qualifier = next(step for step in workflow["jobs"]["verify-source"]["steps"]
                         if "node tools/verify-release-candidate.mjs" in step.get("run", ""))
        original = qualifier["if"]
        qualifier["if"] = original.replace("refs/heads/" + BRANCH, "refs/heads/main")
        self.assertNotEqual(qualifier["if"], original)
        with self.assertRaisesRegex(AssertionError, "entered production"):
            self.assert_invalid_candidate_cannot_enter_production(workflow)

    def test_actual_workflow_retains_structured_native_qualification(self):
        self.candidate()
        workflow = self.workflow()
        result, sentinel = self.drive_preproduction(workflow)
        self.assertEqual(result.returncode, 0, result.stderr)
        self.assertTrue(sentinel.exists())
        receipt = self.work / ".hexalith/release-evidence/qualification/preproduction.json"
        qualification = json.loads(receipt.read_text())
        self.assertEqual(qualification["version"], "3.130.2")
        self.assertRegex(qualification["configuration"]["sha256"], r"^[0-9a-f]{64}$")
        self.assertEqual(qualification["native"]["semanticRelease"], "25.0.9")
        upload = next(step for step in workflow["jobs"]["verify-source"]["steps"]
                      if step.get("name") == "Retain structured release qualification")
        self.assertTrue(upload["uses"].startswith("actions/upload-artifact@"))
        self.assertEqual(upload["with"]["path"], ".hexalith/release-evidence/qualification/**")

    def test_preproduction_workflow_is_read_only_and_qualifies_before_production(self):
        workflow = (ROOT / ".github/workflows/release.yml").read_text()
        preproduction = workflow.split("  verify-source:", 1)[1].split("  release:", 1)[0]
        self.assertIn("fetch-depth: 0", preproduction)
        self.assertIn("npm ci --ignore-scripts", preproduction)
        self.assertIn("node tools/verify-release-candidate.mjs", preproduction)
        self.assertNotIn("write", preproduction)
        self.assertNotIn("environment:", preproduction)
        self.assertNotIn("NUGET", preproduction)
        self.assertNotIn("semantic-release\n", preproduction)
        self.assertIn("needs: verify-source", workflow)


if __name__ == "__main__":
    unittest.main()
