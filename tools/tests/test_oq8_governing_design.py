"""Offline authority checks using real imported bytes in disposable checkouts."""

import hashlib
import importlib.util
import os
from pathlib import Path
import re
import shutil
import subprocess
import sys
import tempfile
import unittest


ROOT = Path(__file__).resolve().parents[2]
SCRIPT = "tools/validate-oq8-governing-design.py"
SPEC = importlib.util.spec_from_file_location("oq8_governing_design", ROOT / SCRIPT)
CHECK = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(CHECK)
# Required coverage and approved pins are independent of the implementation's inventory.
PROVENANCE = "docs/architecture/oq8/README.md"
STORY = "_bmad-output/implementation-artifacts/spec-4-17-oq8-governing-design-authority-import.md"
WORKFLOW = ".github/workflows/oq8-governing-design.yml"
ARTIFACTS = (
    PROVENANCE,
    "_bmad-output/planning-artifacts/prd.md",
    "_bmad-output/planning-artifacts/architecture.md",
    "_bmad-output/planning-artifacts/epics.md",
    STORY,
    WORKFLOW,
)
IMPORTED = ("docs/architecture/oq8/oq8-idempotency-design.md", "docs/architecture/oq8/LICENSE")
INPUTS = (*IMPORTED, *ARTIFACTS)
REQUIRED_IDENTITY = {
    "source-repository": "github.com/Hexalith/Hexalith.Folders",
    "source-path": "docs/exit-criteria/oq8-idempotency-design.md",
    "source-commit": "a9cfea91c8a987ef7a836c216e633a92321fc3c2",
    "design-version": "1.0.0",
    "design-sha256": "1a55b0302e91233e12db91e6e245f0a22d6bf13fcf6cdf5ee0cbe5759f08dcd8",
    "local-design": "docs/architecture/oq8/oq8-idempotency-design.md",
}
REQUIRED_PERMISSION = {
    "source-license-path": "LICENSE",
    "source-license-commit": "a9cfea91c8a987ef7a836c216e633a92321fc3c2",
    "source-license-sha256": "6af66baf053023d456706de321e3cea6a6edbb288ffec10f673186b1befe7ac2",
    "local-license": "docs/architecture/oq8/LICENSE",
    "permission-basis": "MIT License; Copyright (c) 2026 Hexalith; copying associated documentation with the copyright and permission notice retained.",
}
REQUIRED_CI_FIELDS = {
    "OQ8_SOURCE_REPOSITORY": "source-repository",
    "OQ8_SOURCE_PATH": "source-path",
    "OQ8_SOURCE_COMMIT": "source-commit",
    "OQ8_DESIGN_VERSION": "design-version",
    "OQ8_DESIGN_SHA256": "design-sha256",
    "OQ8_LOCAL_DESIGN": "local-design",
}
CHECKOUT = """      - uses: actions/checkout@3d3c42e5aac5ba805825da76410c181273ba90b1 # v7.0.1
        with:
          persist-credentials: false
          submodules: false
"""
LIVE_SCRIPT = """status=0
python3 -B tools/validate-oq8-governing-design.py || status=$?
source_sha="$(git rev-parse HEAD)"
workflow_digest="$(sha256sum .github/workflows/oq8-governing-design.yml | cut -d ' ' -f 1)"
result=FAIL
if [ "$status" -eq 0 ]; then
  result=PASS
fi
printf 'OQ8 governing-design availability: %s\\nSource SHA: %s\\nWorkflow SHA-256: %s\\n' "$result" "$source_sha" "$workflow_digest" | tee -a "$GITHUB_STEP_SUMMARY"
exit "$status"
"""


class GoverningDesignTests(unittest.TestCase):
    def setUp(self):
        self.temporary = tempfile.TemporaryDirectory()
        self.addCleanup(self.temporary.cleanup)
        self.root = Path(self.temporary.name) / "standalone-eventstore"
        for relative in (*INPUTS, SCRIPT):
            destination = self.root / relative
            destination.parent.mkdir(parents=True, exist_ok=True)
            shutil.copyfile(ROOT / relative, destination)

    def failures(self, relative, field=None):
        errors = CHECK.validate(self.root)
        self.assertTrue(errors, "mutation unexpectedly passed")
        self.assertTrue(any(relative in error and (field is None or field in error)
                            for error in errors), errors)
        result = self.cli()
        self.assertEqual(result.returncode, 1, result.stdout)
        self.assertIn(relative, result.stderr)
        if field is not None:
            self.assertIn(field, result.stderr)
        self.assertNotIn("availability verified", result.stdout)

    def mutate_field(self, relative, key, replacement):
        path = self.root / relative
        content = path.read_text()
        prefix = "#" if relative == CHECK.WORKFLOW else "<!--"
        start = content.index(prefix + " oq8-governing-design:begin")
        end = content.index(prefix + " oq8-governing-design:end", start)
        block = content[start:end]
        pattern = rf"(?m)^([ \t]*){re.escape(key)}: [^\n]+\n"
        new_line = "" if replacement is None else rf"\g<1>{key}: {replacement}\n"
        changed, count = re.subn(pattern, new_line, block)
        self.assertEqual(count, 1, (relative, key))
        path.write_text(content[:start] + changed + content[end:])

    def cli(self, *arguments):
        return subprocess.run([sys.executable, "-B", str(self.root / SCRIPT), *arguments],
                              cwd=self.temporary.name, capture_output=True, text=True, check=False)

    def test_original_git_object_bytes_and_fixed_pins(self):
        self.assertEqual(CHECK.IDENTITY, REQUIRED_IDENTITY)
        self.assertEqual(CHECK.PERMISSION, REQUIRED_PERMISSION)
        self.assertEqual(CHECK.PERMISSION["source-license-commit"], CHECK.IDENTITY["source-commit"])
        self.assertEqual(CHECK.DOCUMENTS, ARTIFACTS[:-1])
        self.assertEqual(CHECK.PROVENANCE, PROVENANCE)
        self.assertEqual(CHECK.STORY, STORY)
        self.assertEqual(CHECK.WORKFLOW, WORKFLOW)
        self.assertEqual(CHECK.CI_FIELDS, REQUIRED_CI_FIELDS)
        self.assertEqual(hashlib.sha256((self.root / IMPORTED[0]).read_bytes()).hexdigest(),
                         "1a55b0302e91233e12db91e6e245f0a22d6bf13fcf6cdf5ee0cbe5759f08dcd8")
        self.assertEqual(hashlib.sha256((self.root / IMPORTED[1]).read_bytes()).hexdigest(),
                         "6af66baf053023d456706de321e3cea6a6edbb288ffec10f673186b1befe7ac2")
        self.assertEqual((self.root / IMPORTED[0]).stat().st_size, 10474)
        self.assertEqual((self.root / IMPORTED[1]).stat().st_size, 1065)

    def test_standalone_checkout_passes_without_folders_git_or_evidence(self):
        self.assertFalse((self.root / "references").exists())
        self.assertFalse((self.root / ".git").exists())
        self.assertEqual(CHECK.validate(self.root), [])

    def test_cli_default_root_is_script_repository_and_reports_limited_scope(self):
        result = self.cli()
        self.assertEqual(result.returncode, 0, result.stderr)
        self.assertIn("availability verified", result.stdout)
        self.assertIn("runtime conformance", result.stdout)
        self.assertIn("separate gates", result.stdout)

    def test_cli_explicit_root_passes(self):
        result = self.cli("--root", str(self.root))
        self.assertEqual(result.returncode, 0, result.stderr)

    def test_cli_failure_names_each_missing_artifact(self):
        for relative in INPUTS:
            with self.subTest(artifact=relative):
                path = self.root / relative
                original = path.read_bytes()
                path.unlink()
                result = self.cli()
                self.assertEqual(result.returncode, 1, result.stdout)
                self.assertIn(relative, result.stderr)
                self.assertNotIn("availability verified", result.stdout)
                path.write_bytes(original)

    def test_cli_bad_root_and_invalid_usage_fail(self):
        self.assertEqual(self.cli("--root", str(self.root / "absent")).returncode, 1)
        self.assertEqual(self.cli("--unknown-option").returncode, 2)

    def test_coordinated_identity_drift_cannot_repin_validator(self):
        for key in CHECK.IDENTITY:
            with self.subTest(field=key):
                for relative in ARTIFACTS:
                    workflow_key = next((ci for ci, field in CHECK.CI_FIELDS.items() if field == key), key)
                    self.mutate_field(relative, workflow_key if relative == CHECK.WORKFLOW else key, "coordinated-drift")
                for relative in ARTIFACTS:
                    self.failures(relative)
                for relative in ARTIFACTS:
                    shutil.copyfile(ROOT / relative, self.root / relative)

    def test_coordinated_bytes_digest_and_permission_drift_fails_fixed_pins(self):
        for relative, key in ((IMPORTED[0], "design-sha256"),
                              (IMPORTED[1], "source-license-sha256")):
            (self.root / relative).write_bytes(b"replacement authority\n")
            digest = hashlib.sha256((self.root / relative).read_bytes()).hexdigest()
            if key == "design-sha256":
                for artifact in ARTIFACTS:
                    self.mutate_field(artifact, "OQ8_DESIGN_SHA256" if artifact == CHECK.WORKFLOW else key, digest)
            else:
                self.mutate_field(CHECK.PROVENANCE, key, digest)
            self.failures(relative)

    def test_frozen_story_identity_cannot_drift_behind_current_binding(self):
        path = self.root / CHECK.STORY
        original = path.read_text()
        for key, value in CHECK.IDENTITY.items():
            if key == "local-design":
                continue
            with self.subTest(field=key):
                path.write_text(original.replace(f"`{value}`", "`identity-drift`", 1))
                self.failures(CHECK.STORY, "frozen source identity")
        path.write_text(original.replace("<frozen-after-approval", "<missing-frozen-section", 1))
        self.failures(CHECK.STORY, "frozen source identity")

    def test_duplicate_current_binding_fails_for_every_artifact(self):
        for relative in ARTIFACTS:
            with self.subTest(artifact=relative):
                path = self.root / relative
                original = path.read_text()
                path.write_text(original + "\n" + original)
                self.failures(relative)
                path.write_text(original)

    def test_missing_binding_fails_even_with_pins_elsewhere(self):
        for relative in ARTIFACTS:
            with self.subTest(artifact=relative):
                path = self.root / relative
                original = path.read_text()
                path.write_text(original.replace("oq8-governing-design:begin", "obsolete-binding:begin"))
                self.failures(relative)
                path.write_text(original)

    def test_reversed_binding_markers_fail_for_every_artifact(self):
        for relative in ARTIFACTS:
            with self.subTest(artifact=relative):
                path = self.root / relative
                original = path.read_text()
                start = "# oq8-governing-design:begin" if relative == WORKFLOW else "<!-- oq8-governing-design:begin -->"
                end = "# oq8-governing-design:end" if relative == WORKFLOW else "<!-- oq8-governing-design:end -->"
                prefix, tail = original.split(start, 1)
                block = tail.split(end, 1)[0]
                path.write_text(prefix + end + "\n" + start + block)
                self.failures(relative, "end marker must follow begin")
                path.write_text(original)

    def test_crlf_metadata_passes_without_normalizing_imported_bytes(self):
        for relative in ARTIFACTS:
            path = self.root / relative
            path.write_bytes(path.read_bytes().replace(b"\r\n", b"\n").replace(b"\n", b"\r\n"))
        self.assertEqual(CHECK.validate(self.root), [])
        result = self.cli()
        self.assertEqual(result.returncode, 0, result.stderr)
        for relative, expected in ((IMPORTED[0], REQUIRED_IDENTITY["design-sha256"]),
                                   (IMPORTED[1], REQUIRED_PERMISSION["source-license-sha256"])):
            self.assertEqual(hashlib.sha256((self.root / relative).read_bytes()).hexdigest(), expected)

    def test_duplicate_and_unknown_fields_fail(self):
        for relative in ARTIFACTS:
            for extra in ("unexpected-field: ignored", "source-repository: repeated"):
                with self.subTest(artifact=relative, extra=extra):
                    path = self.root / relative
                    original = path.read_text()
                    marker = "# oq8-governing-design:end" if relative == WORKFLOW else "```\n<!-- oq8-governing-design:end -->"
                    if relative == CHECK.WORKFLOW:
                        extra = extra.replace("source-repository", "OQ8_SOURCE_REPOSITORY")
                    path.write_text(original.replace(marker, "      " + extra + "\n" + marker))
                    self.failures(relative)
                    path.write_text(original)

    def test_malformed_and_invalid_utf8_bindings_fail(self):
        for relative in ARTIFACTS:
            with self.subTest(artifact=relative):
                path = self.root / relative
                original = path.read_bytes()
                path.write_bytes(original.replace(b": github.com/Hexalith/Hexalith.Folders", b":\nmalformed", 1))
                self.failures(relative)
                path.write_bytes(original + b"\xff")
                self.failures(relative)
                path.write_bytes(original)

    def test_symbolic_authority_and_parent_directory_fail(self):
        external = Path(self.temporary.name) / "external-design.md"
        path = self.root / IMPORTED[0]
        path.replace(external)
        path.symlink_to(external)
        self.failures(IMPORTED[0])
        path.unlink()
        external.replace(path)
        directory = self.root / "docs/architecture/oq8"
        external_directory = Path(self.temporary.name) / "external-oq8"
        directory.replace(external_directory)
        directory.symlink_to(external_directory, target_is_directory=True)
        self.failures(IMPORTED[0])

    def assert_workflow_contract(self, content):
        # Match only this small workflow's declared structure and executable slots.
        # Exact allowlists reject overrides/skips without interpreting general YAML.
        preamble = """name: OQ8 governing-design availability

on:
  push:
    branches: [main, release/parties-sdk-3.130.2]
  pull_request:
    branches: [main, release/parties-sdk-3.130.2]
  workflow_dispatch:

permissions:
  contents: read

# oq8-governing-design:begin
env:
"""
        preamble += "".join(f"  {key}: {REQUIRED_IDENTITY[field]}\n"
                            for key, field in REQUIRED_CI_FIELDS.items())
        preamble += "# oq8-governing-design:end\n\njobs:\n"
        self.assertTrue(content.startswith(preamble), "unexpected triggers, permissions or identity environment")
        jobs = content[len(preamble):]
        headers = list(re.finditer(r"(?m)^  ([a-z-]+):\n", jobs))
        self.assertEqual([header[1] for header in headers], ["fixtures", "live-evaluation"])
        self.assertEqual(headers[0].start(), 0)
        bodies = {}
        for index, header in enumerate(headers):
            end = headers[index + 1].start() if index + 1 < len(headers) else len(jobs)
            bodies[header[1]] = jobs[header.end():end].rstrip() + "\n"
        for job, title in (("fixtures", "fixtures"), ("live-evaluation", "live evaluation")):
            prefix = (f"    name: OQ8 governing-design {title}\n"
                      "    runs-on: ubuntu-24.04\n"
                      "    timeout-minutes: 5\n"
                      "    steps:\n" + CHECKOUT)
            self.assertTrue(bodies[job].startswith(prefix), f"{job}: job or checkout inputs changed")
            bodies[job] = bodies[job][len(prefix):]
        self.assertEqual(bodies["fixtures"],
                         "      - name: Test exact identity and rejection paths\n"
                         "        run: python3 -B -m unittest discover -s tools/tests -p 'test_oq8_governing_design.py'\n")
        live_prefix = ("      - name: Evaluate and report current authority availability\n"
                       "        shell: bash\n"
                       "        run: |\n")
        self.assertTrue(bodies["live-evaluation"].startswith(live_prefix))
        lines = bodies["live-evaluation"][len(live_prefix):].splitlines(keepends=True)
        self.assertTrue(all(line.startswith("          ") for line in lines))
        script = "".join(line[10:] for line in lines)
        self.assertEqual(script, LIVE_SCRIPT)
        return script

    def test_workflow_has_independent_executable_fixture_and_live_jobs(self):
        self.assert_workflow_contract((self.root / WORKFLOW).read_text())

    def test_workflow_contract_rejects_skips_echoes_permissions_and_checkout_overrides(self):
        original = (self.root / WORKFLOW).read_text()
        mutations = (
            ("run: python3 -B -m unittest", "run: echo python3 -B -m unittest"),
            ("          python3 -B tools/validate", "          echo python3 -B tools/validate"),
            ("    steps:", "    if: false\n    steps:"),
            ("        shell: bash", "        if: false\n        shell: bash"),
            ("  live-evaluation:\n", "  live-evaluation:\n    needs: fixtures\n"),
            ("  contents: read", "  contents: write"),
            ("  contents: read", "  contents: read\n  actions: write"),
            ("    steps:", "    permissions:\n      contents: write\n    steps:"),
            ("          submodules: false", "          submodules: true"),
            ("          persist-credentials: false", "          persist-credentials: true"),
            ("          submodules: false", "          submodules: false\n          token: ${{ secrets.OVERRIDE_TOKEN }}"),
            ("          submodules: false", "          submodules: false\n          ssh-key: ${{ secrets.OVERRIDE_KEY }}"),
            ("          result=FAIL", "          result=PASS"),
            ("source_sha=\"$(git rev-parse HEAD)\"", "source_sha=unbound"),
            ("workflow_digest=\"$(sha256sum", "workflow_digest=\"$(echo sha256sum"),
            ("tee -a \"$GITHUB_STEP_SUMMARY\"", "cat"),
            ("          exit \"$status\"", "          exit 0"),
        )
        for old, new in mutations:
            with self.subTest(mutation=new):
                self.assertIn(old, original)
                with self.assertRaises(AssertionError):
                    self.assert_workflow_contract(original.replace(old, new, 1))

    def test_live_job_publishes_bound_pass_and_fail_results(self):
        script = self.assert_workflow_contract((self.root / WORKFLOW).read_text())
        tools = Path(self.temporary.name) / "bin"
        tools.mkdir()
        source_sha = "0123456789abcdef0123456789abcdef01234567"
        git = tools / "git"
        git.write_text('#!/bin/sh\n[ "$*" = "rev-parse HEAD" ] || exit 2\nprintf "%s\\n" "' + source_sha + '"\n')
        git.chmod(0o755)
        summary = Path(self.temporary.name) / "summary.md"
        environment = dict(os.environ, PATH=str(tools) + os.pathsep + os.environ["PATH"],
                           GITHUB_STEP_SUMMARY=str(summary))
        workflow_digest = hashlib.sha256((self.root / WORKFLOW).read_bytes()).hexdigest()
        for expected, code in (("PASS", 0), ("FAIL", 1)):
            with self.subTest(result=expected):
                if expected == "FAIL":
                    (self.root / IMPORTED[0]).write_bytes(b"changed governing design\n")
                result = subprocess.run(["bash", "-e", "-o", "pipefail", "-c", script],
                                        cwd=self.root, env=environment, capture_output=True,
                                        text=True, check=False)
                self.assertEqual(result.returncode, code, result.stderr)
                report = (f"OQ8 governing-design availability: {expected}\n"
                          f"Source SHA: {source_sha}\nWorkflow SHA-256: {workflow_digest}\n")
                self.assertEqual(summary.read_text(), report)
                self.assertIn(report, result.stdout)
                if expected == "FAIL":
                    self.assertIn(IMPORTED[0], result.stderr)
                    self.assertNotIn("availability verified", result.stdout)
                    self.assertNotIn("availability: PASS", result.stdout)
                summary.unlink()

    def test_exact_import_editorconfig_exceptions_preserve_existing_rules(self):
        content = (ROOT / ".editorconfig").read_text()
        expected = {"*": "crlf", "*.py": "lf", **{"/" + name: "lf" for name in IMPORTED}}
        for section, ending in expected.items():
            with self.subTest(section=section):
                matches = re.findall(r"(?ms)^\[" + re.escape(section) + r"\]\n(.*?)(?=^\[|\Z)", content)
                self.assertEqual(len(matches), 1)
                self.assertEqual(re.findall(r"(?m)^end_of_line = (\w+)$", matches[0]), [ending])

    def test_import_attributes_disable_text_conversion(self):
        content = (ROOT / ".gitattributes").read_text()
        for relative in IMPORTED:
            self.assertIn(f"{relative} -text", content.splitlines())


def field_test(relative, key, missing):
    def test(self):
        self.mutate_field(relative, key, None if missing else "identity-drift")
        self.failures(relative, key)
    return test


# Each governed field/artifact pair is an individually reported test case.
for artifact_index, artifact in enumerate(ARTIFACTS):
    fields = dict(REQUIRED_CI_FIELDS) if artifact == WORKFLOW else dict(REQUIRED_IDENTITY)
    if artifact == PROVENANCE:
        fields.update(REQUIRED_PERMISSION)
    for field in fields:
        for missing in (False, True):
            name = f"test_{'missing' if missing else 'changed'}_artifact_{artifact_index}_{field.replace('-', '_')}"
            setattr(GoverningDesignTests, name, field_test(artifact, field, missing))

for imported_index, imported in enumerate(IMPORTED):
    for mutation in ("missing", "edited", "truncated", "newline_normalized"):
        def test_bytes(self, relative=imported, mode=mutation):
            path = self.root / relative
            original = path.read_bytes()
            if mode == "missing":
                path.unlink()
            elif mode == "edited":
                path.write_bytes(original + b"edited\n")
            elif mode == "truncated":
                path.write_bytes(original[:len(original) // 2])
            else:
                path.write_bytes(original.replace(b"\n", b"\r\n"))
            self.failures(relative)
        setattr(GoverningDesignTests, f"test_imported_{imported_index}_{mutation}", test_bytes)


if __name__ == "__main__":
    unittest.main()
