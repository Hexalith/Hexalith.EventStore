"""Regression tests for scripts/check-committed-files.sh.

Every case runs the real guard against a disposable Git repository, in both the
pre-commit (--staged) and CI (all tracked files) modes.
"""

import os
import shutil
import subprocess
import tempfile
import unittest
from pathlib import Path


ROOT = Path(__file__).resolve().parents[2]
GUARD = ROOT / "scripts" / "check-committed-files.sh"
LIMIT = 1_048_576


class CheckCommittedFilesTests(unittest.TestCase):
    def setUp(self):
        self.temporary = tempfile.TemporaryDirectory(prefix="check-committed-files-")
        self.addCleanup(self.temporary.cleanup)
        self.repo = Path(self.temporary.name)
        # Isolate from the caller's Git configuration, hooks and any hook-provided index.
        self.env = {key: value for key, value in os.environ.items() if not key.startswith("GIT_")}
        self.env.update({
            "GIT_CONFIG_GLOBAL": os.devnull,
            "GIT_CONFIG_NOSYSTEM": "1",
            "GIT_AUTHOR_NAME": "Guard Test",
            "GIT_AUTHOR_EMAIL": "guard@example.invalid",
            "GIT_COMMITTER_NAME": "Guard Test",
            "GIT_COMMITTER_EMAIL": "guard@example.invalid",
        })
        self.git("init", "--quiet")
        self.write(".gitignore", "_bmad-output/**/evidence/\ndocs/evidence/\n")
        self.write("README.md", "baseline\n")
        self.git("add", "--", ".gitignore", "README.md")
        self.git("commit", "--quiet", "-m", "test: baseline")

    def git(self, *arguments):
        return subprocess.run(
            ["git", *arguments], cwd=self.repo, env=self.env, check=True,
            stdout=subprocess.PIPE, stderr=subprocess.PIPE, text=True,
        ).stdout

    def write(self, relative, content):
        path = self.repo / relative
        path.parent.mkdir(parents=True, exist_ok=True)
        if isinstance(content, bytes):
            path.write_bytes(content)
        else:
            path.write_text(content, encoding="utf-8")

    def guard(self, *arguments, env=None):
        return subprocess.run(
            ["bash", str(GUARD), *arguments], cwd=self.repo, env=env or self.env,
            stdout=subprocess.PIPE, stderr=subprocess.PIPE, text=True,
        )

    def assert_both_modes(self, expected_code):
        staged = self.guard("--staged")
        self.assertEqual(expected_code, staged.returncode, staged.stdout + staged.stderr)
        tracked = self.guard()
        self.assertEqual(expected_code, tracked.returncode, tracked.stdout + tracked.stderr)
        return staged, tracked

    def test_normal_small_source_and_doc_files_pass(self):
        self.write("src/Example.cs", "namespace Example;\n")
        self.write("docs/guide.md", "# Guide\n")
        self.git("add", "--", "src/Example.cs", "docs/guide.md")

        staged, tracked = self.assert_both_modes(0)

        self.assertIn("2 checked", staged.stdout)
        self.assertIn("tracked files pass", tracked.stdout)

    def test_force_added_evidence_paths_fail_and_are_listed(self):
        for relative in (
            "_bmad-output/x/evidence/a.json",
            "_bmad-output/evidence/top.json",
            "_bmad-output/implementation-artifacts/evidence/story/deep/b.log",
            "docs/evidence/a.md",
        ):
            with self.subTest(path=relative):
                self.write(relative, "{}\n")
                # The ignore rule holds, so only a forced add can stage the path.
                self.assertNotIn(relative, self.git("status", "--porcelain", "--untracked-files=all"))
                self.git("add", "-f", "--", relative)

                staged, tracked = self.assert_both_modes(1)

                for result in (staged, tracked):
                    self.assertIn("evidence path must not be committed: " + relative, result.stderr)
                    self.assertIn("Fix:", result.stderr)
                    self.assertIn("git rm --cached", result.stderr)
                self.git("rm", "--cached", "--quiet", "--", relative)

    def test_evidence_folder_match_is_case_insensitive(self):
        for relative in ("docs/Evidence/x.md", "_bmad-output/a/EVIDENCE/x.json"):
            with self.subTest(path=relative):
                self.write(relative, "{}\n")
                self.git("add", "-f", "--", relative)

                staged, tracked = self.assert_both_modes(1)

                for result in (staged, tracked):
                    self.assertIn("evidence path must not be committed: " + relative, result.stderr)
                self.git("rm", "--cached", "--quiet", "--", relative)

    def test_staged_rename_into_evidence_folder_fails_on_destination(self):
        self.write("notes/run.md", "small\n")
        self.git("add", "--", "notes/run.md")
        self.git("commit", "--quiet", "-m", "test: small file")
        (self.repo / "docs" / "evidence").mkdir(parents=True)
        self.git("mv", "--", "notes/run.md", "docs/evidence/run.md")

        staged = self.guard("--staged")

        self.assertEqual(1, staged.returncode, staged.stdout + staged.stderr)
        self.assertIn("evidence path must not be committed: docs/evidence/run.md", staged.stderr)

    def test_git_listing_failure_is_an_error_not_a_pass(self):
        real_git = shutil.which("git", path=self.env.get("PATH"))
        self.assertIsNotNone(real_git)
        shim = Path(self.temporary.name) / "shim"
        shim.mkdir()
        # Delegates to the real git except for the two listing commands the guard depends on.
        (shim / "git").write_text(
            "#!/bin/sh\n"
            "for argument in \"$@\"; do\n"
            "  case \"$argument\" in ls-files|diff) echo 'shim: listing failed' >&2; exit 128 ;; esac\n"
            "done\n"
            f"exec '{real_git}' \"$@\"\n",
            encoding="utf-8",
        )
        (shim / "git").chmod(0o755)
        env = dict(self.env, PATH=str(shim) + os.pathsep + self.env.get("PATH", ""))
        self.write("docs/evidence/a.md", "a\n")
        self.git("add", "-f", "--", "docs/evidence/a.md")

        for arguments in (("--staged",), ()):
            with self.subTest(arguments=arguments):
                result = self.guard(*arguments, env=env)

                self.assertEqual(2, result.returncode, result.stdout + result.stderr)
                self.assertIn("could not list", result.stderr)
                self.assertNotIn("pass", result.stdout)

    def test_evidence_like_names_outside_the_rule_pass(self):
        self.write("docs/evidence-policy.md", "policy\n")
        self.write("src/Evidence/Model.cs", "namespace Evidence;\n")
        self.write("evidence/notes.md", "outside _bmad-output and docs\n")
        self.git("add", "--", "docs/evidence-policy.md", "src/Evidence/Model.cs", "evidence/notes.md")

        self.assert_both_modes(0)

    def test_file_over_one_mebibyte_fails_with_path_and_size(self):
        self.write("assets/huge.bin", b"\0" * (LIMIT + 1))
        self.git("add", "--", "assets/huge.bin")

        staged, tracked = self.assert_both_modes(1)

        for result in (staged, tracked):
            self.assertIn(f"file exceeds {LIMIT} bytes: assets/huge.bin ({LIMIT + 1} bytes)", result.stderr)
            self.assertIn("Fix:", result.stderr)

    def test_file_of_exactly_one_mebibyte_passes(self):
        self.write("assets/limit.bin", b"\0" * LIMIT)
        self.git("add", "--", "assets/limit.bin")

        self.assert_both_modes(0)

    def test_allowlisted_large_files_pass(self):
        for relative in (
            "docs/assets/quickstart-demo.gif",
            "_bmad-output/implementation-artifacts/spec-event-versioning-upcasting.md",
        ):
            self.write(relative, b"x" * (LIMIT + 10))
            self.git("add", "--", relative)

        self.assert_both_modes(0)

    def test_allowlist_does_not_exempt_evidence_or_other_large_paths(self):
        self.write("docs/assets/other-demo.gif", b"x" * (LIMIT + 10))
        self.git("add", "--", "docs/assets/other-demo.gif")

        staged, tracked = self.assert_both_modes(1)

        for result in (staged, tracked):
            self.assertIn("docs/assets/other-demo.gif", result.stderr)

    def test_gitlink_is_ignored(self):
        commit = self.git("rev-parse", "HEAD").strip()
        # Submodule pointers are index entries of mode 160000 with no blob to measure.
        self.git("update-index", "--add", "--cacheinfo", f"160000,{commit},references/Module")
        self.git("update-index", "--add", "--cacheinfo", f"160000,{commit},_bmad-output/x/evidence/module")

        staged, tracked = self.assert_both_modes(0)

        self.assertIn("0 checked", staged.stdout)

    def test_deletions_are_ignored(self):
        self.write("assets/huge.bin", b"\0" * (LIMIT + 1))
        self.write("docs/evidence/old.md", "old\n")
        self.git("add", "-f", "--", "assets/huge.bin", "docs/evidence/old.md")
        self.git("commit", "--quiet", "--no-verify", "-m", "test: legacy violations")
        self.git("rm", "--quiet", "--", "assets/huge.bin")
        self.git("rm", "--quiet", "--cached", "--", "docs/evidence/old.md")

        staged, tracked = self.assert_both_modes(0)

        self.assertIn("0 checked", staged.stdout)

    def test_staged_mode_checks_only_staged_changes(self):
        self.write("assets/huge.bin", b"\0" * (LIMIT + 1))
        self.git("add", "--", "assets/huge.bin")
        self.git("commit", "--quiet", "--no-verify", "-m", "test: legacy large file")
        self.write("docs/guide.md", "# Guide\n")
        self.git("add", "--", "docs/guide.md")

        staged = self.guard("--staged")
        tracked = self.guard()

        self.assertEqual(0, staged.returncode, staged.stderr)
        self.assertEqual(1, tracked.returncode, tracked.stderr)
        self.assertIn("assets/huge.bin", tracked.stderr)

    def test_staged_modification_that_grows_a_file_fails(self):
        self.write("README.md", "x" * (LIMIT + 1))
        self.git("add", "--", "README.md")

        staged = self.guard("--staged")

        self.assertEqual(1, staged.returncode, staged.stderr)
        self.assertIn("README.md", staged.stderr)

    def test_staged_mode_works_before_the_first_commit(self):
        fresh = Path(self.temporary.name) / "fresh"
        fresh.mkdir()
        subprocess.run(["git", "init", "--quiet"], cwd=fresh, env=self.env, check=True)
        (fresh / "docs" / "evidence").mkdir(parents=True)
        (fresh / "docs" / "evidence" / "a.md").write_text("a\n", encoding="utf-8")
        subprocess.run(["git", "add", "--", "docs/evidence/a.md"], cwd=fresh, env=self.env, check=True)

        result = subprocess.run(
            ["bash", str(GUARD), "--staged"], cwd=fresh, env=self.env,
            stdout=subprocess.PIPE, stderr=subprocess.PIPE, text=True,
        )

        self.assertEqual(1, result.returncode, result.stderr)
        self.assertIn("docs/evidence/a.md", result.stderr)

    def test_unknown_argument_is_a_usage_error(self):
        result = self.guard("--everything")

        self.assertEqual(2, result.returncode)
        self.assertIn("Usage:", result.stderr)


class GuardWiringTests(unittest.TestCase):
    def test_commit_and_merge_hooks_run_the_staged_guard(self):
        # A merge commit runs pre-merge-commit, not pre-commit, so both hooks carry the guard.
        for name in ("pre-commit", "pre-merge-commit"):
            with self.subTest(hook=name):
                hook = (ROOT / ".husky" / name).read_bytes()

                self.assertNotIn(b"\r", hook)
                self.assertEqual(b"#!/bin/sh\nbash scripts/check-committed-files.sh --staged\n", hook)
                if os.name == "posix":
                    self.assertTrue(os.access(ROOT / ".husky" / name, os.X_OK))

    def test_required_live_sidecar_job_runs_the_guard_and_repository_checks_first(self):
        # ci / build-and-test is the shared Hexalith.Builds reusable workflow and accepts no
        # caller steps, so the required live-sidecar job hosts these checks right after checkout.
        workflow = (ROOT / ".github" / "workflows" / "integration.yml").read_text(encoding="utf-8")
        job = workflow[workflow.index("  live-sidecar:"):]
        steps = (
            "run: bash scripts/check-committed-files.sh\n",
            "-p 'test_check_committed_files.py'",
            "-p 'test_dapr_broker_qualification.py'",
        )

        positions = [job.index(step) for step in steps]
        self.assertLess(job.index("- name: Checkout repository"), positions[0])
        self.assertEqual(sorted(positions), positions)
        self.assertLess(positions[-1], job.index("- name: Initialize Hexalith.Builds submodule"))
        ci = (ROOT / ".github" / "workflows" / "ci.yml").read_text(encoding="utf-8")
        self.assertNotIn("check-committed-files", ci)

    def test_evidence_folders_are_ignored(self):
        ignore = (ROOT / ".gitignore").read_text(encoding="utf-8").splitlines()

        self.assertIn("_bmad-output/**/evidence/", ignore)
        self.assertIn("docs/evidence/", ignore)


if __name__ == "__main__":
    unittest.main()
