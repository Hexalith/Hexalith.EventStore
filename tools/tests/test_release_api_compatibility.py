"""Offline API-gate controls; the real control requires the pinned tool and SDK.

Install Microsoft.DotNet.ApiCompat.Tool 11.0.100 before running this suite, or set
RELEASE_API_COMPAT_TOOL_DLL to that version's tool DLL. No package installation
or network request is performed by these tests.
"""

import base64
import hashlib
import importlib.util
import json
import os
from pathlib import Path
import subprocess
import sys
import tempfile
import unittest
from unittest.mock import patch
from zipfile import ZipFile


ROOT = Path(__file__).resolve().parents[2]
sys.path.insert(0, str(ROOT / "tools"))
SPEC = importlib.util.spec_from_file_location("release_api_compatibility", ROOT / "tools/verify-release-api-compatibility.py")
gate = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(gate)


class ArchiveContractTests(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory(prefix="release-api-offline-")
        self.addCleanup(self.temp.cleanup)
        self.work = Path(self.temp.name)
        self.source = "b" * 40
        self.version = "5.0.0-test"

    def archive(self, name, *, version=None, source=None, embedded_id=None, duplicate=False):
        path = self.work / f"{name}.{self.version}.nupkg"
        with ZipFile(path, "w") as archive:
            archive.writestr("package.nuspec", f"""<package><metadata>
                <id>{embedded_id or name}</id><version>{version or self.version}</version>
                <repository type="git" url="{gate.REPOSITORY_URL}" commit="{source or self.source}"/>
                </metadata></package>""")
            archive.writestr(gate.member(name, "net11.0"), b"unit archive; real managed DLLs are tested separately")
            if duplicate:
                archive.writestr("other.nuspec", "<package/>")
        return path

    def inventory(self):
        for name in gate.PACKAGE_IDS:
            self.archive(name)

    def test_complete_inventory_accepts_all_fourteen_canonical_identities(self):
        self.inventory()
        result = gate.candidate_inventory(self.work, self.version, self.source)
        self.assertEqual(set(result), gate.PACKAGE_IDS)
        self.assertEqual(len(result), 14)

    def test_missing_family_refuses_before_comparison(self):
        self.inventory()
        next(self.work.glob("*.nupkg")).unlink()
        with self.assertRaisesRegex(ValueError, "exactly all 14 families"):
            gate.candidate_inventory(self.work, self.version, self.source)

    def test_unexpected_family_or_renamed_file_refuses(self):
        self.inventory()
        self.archive("Hexalith.EventStore.Unexpected")
        with self.assertRaisesRegex(ValueError, "unexpected"):
            gate.candidate_inventory(self.work, self.version, self.source)

    def test_wrong_package_id_version_and_source_each_refuse(self):
        self.inventory()
        for fault in ({"embedded_id": gate.TOOL}, {"version": "4.2.0"}, {"source": "c" * 40}):
            with self.subTest(fault=fault):
                self.archive(gate.GENERATOR, **fault)
                with self.assertRaisesRegex(ValueError, "identity"):
                    gate.candidate_inventory(self.work, self.version, self.source)
                self.archive(gate.GENERATOR)

    def test_duplicate_metadata_and_missing_api_assembly_refuse(self):
        path = self.archive(gate.GENERATOR, duplicate=True)
        with self.assertRaisesRegex(ValueError, "one nuspec"):
            gate.metadata(path, gate.GENERATOR, self.version, self.source)
        path = self.archive(gate.GENERATOR)
        with self.assertRaisesRegex(ValueError, "Missing or repeated API assembly"):
            gate.extract_member(path, "lib/net11.0/missing.dll", self.work / "missing.dll")

    def test_different_resolved_dll_versions_are_never_unioned(self):
        a, b = self.work / "a", self.work / "b"
        a.mkdir()
        b.mkdir()
        (a / "Dependency.dll").write_bytes(b"version one")
        (b / "Dependency.dll").write_bytes(b"version two")
        with self.assertRaisesRegex(ValueError, "Ambiguous resolved DLL"):
            gate.stage_references([a / "Dependency.dll", b / "Dependency.dll"], self.work / "refs")

    def test_identical_duplicate_and_missing_resolved_references(self):
        dll = self.work / "Dependency.dll"
        dll.write_bytes(b"identical")
        self.assertEqual(len(gate.stage_references([dll, dll], self.work / "refs")), 1)
        with self.assertRaisesRegex(ValueError, "Missing/non-DLL"):
            gate.stage_references([self.work / "absent.dll"], self.work / "missing")

    def test_explicit_consumer_package_selection_precedes_framework_fallback(self):
        package = self.work / "package/Contract.dll"
        framework = self.work / "framework/Contract.dll"
        other = self.work / "framework/System.Runtime.dll"
        self.assertEqual(gate.with_framework([package], [framework, other]), [package, other])

    def test_zero_exit_with_unresolved_reference_is_a_failure(self):
        result = subprocess.CompletedProcess([], 0, "Could not resolve reference 'Missing'", "")
        with patch.object(gate, "run", return_value=result):
            with self.assertRaisesRegex(ValueError, "API compatibility refused"):
                gate.compare(["apicompat"], self.work / "a.dll", self.work / "b.dll",
                             self.work / "old", self.work / "new", self.work / "compare.log")

    def test_every_comparison_enforces_public_parameter_names(self):
        def execute(command, cwd, log, *, check):
            self.assertIn("--enable-rule-cannot-change-parameter-name", command)
            log.write_text("compatible control")
            return subprocess.CompletedProcess(command, 0, "", "")
        with patch.object(gate, "run", side_effect=execute):
            result = gate.compare(["apicompat"], self.work / "a.dll", self.work / "b.dll",
                                  self.work / "old", self.work / "new", self.work / "compare.log")
            self.assertEqual(result["exit"], 0)

    def test_early_failure_preserves_diagnostics_in_the_uploaded_default_directory(self):
        arguments = ["verify-release-api-compatibility.py", str(self.work / "packages"), self.version,
                     "--repository-root", str(self.work)]
        with patch.object(sys, "argv", arguments), patch.object(gate, "verify", side_effect=ValueError("invalid candidate source")):
            with self.assertRaisesRegex(ValueError, "invalid candidate source"):
                gate.main()
        retained = self.work / ".hexalith/release-evidence/api-compatibility"
        failures = list(retained.glob("run-*/failure.json"))
        self.assertEqual(len(failures), 1)
        result = json.loads(failures[0].read_text())
        self.assertEqual(result["status"], "fail")
        self.assertEqual(result["error"], "invalid candidate source")
        workflow = (ROOT / ".github/workflows/release.yml").read_text()
        self.assertIn(".hexalith/release-evidence/**", workflow)
        self.assertIn("!.hexalith/release-evidence/api-compatibility/**/scratch/**", workflow)

    def test_explicit_evidence_directory_preserves_failure_and_never_overwrites_prior_evidence(self):
        evidence = self.work / "evidence"
        arguments = ["verify-release-api-compatibility.py", str(self.work / "packages"), self.version,
                     "--repository-root", str(self.work), "--work-directory", str(evidence)]
        with patch.object(sys, "argv", arguments), patch.object(gate, "verify", side_effect=ValueError("signature refused")) as verify:
            with self.assertRaisesRegex(ValueError, "signature refused"):
                gate.main()
            original = (evidence / "failure.json").read_bytes()
            with self.assertRaisesRegex(ValueError, "must be empty"):
                gate.main()
            self.assertEqual(verify.call_count, 1)
            self.assertEqual((evidence / "failure.json").read_bytes(), original)

    def test_official_hash_mismatch_refuses_even_for_a_cached_archive(self):
        archive = self.archive(gate.GENERATOR)
        registration = {"catalogEntry": "https://api.nuget.org/v3/catalog0/data/fixture.json"}
        catalog = {"id": gate.GENERATOR, "version": self.version, "packageHashAlgorithm": "SHA512",
                   "packageHash": base64.b64encode(hashlib.sha512(b"different archive").digest()).decode(), "listed": True}
        with patch.object(gate, "fetch", side_effect=[json.dumps(registration).encode(), json.dumps(catalog).encode(), archive.read_bytes()]):
            with self.assertRaisesRegex(ValueError, "Official SHA512 mismatch"):
                gate.official_archive(gate.GENERATOR, self.version, self.work / "out.nupkg", archive)

    def test_official_catalog_cannot_redirect_to_another_authority(self):
        with patch.object(gate, "fetch", return_value=json.dumps({"catalogEntry": "https://example.invalid/data"}).encode()):
            with self.assertRaisesRegex(ValueError, "catalog authority"):
                gate.official_archive(gate.GENERATOR, self.version, self.work / "out.nupkg")

    def test_official_archive_accepts_only_matching_hash_identity_and_successful_signature(self):
        archive = self.archive(gate.GENERATOR)
        registration = {"catalogEntry": "https://api.nuget.org/v3/catalog0/data/fixture.json"}
        catalog = {"id": gate.GENERATOR, "version": self.version, "packageHashAlgorithm": "SHA512",
                   "packageHash": base64.b64encode(hashlib.sha512(archive.read_bytes()).digest()).decode(), "listed": True}
        for signature_passes in (True, False):
            with self.subTest(signature_passes=signature_passes):
                def signature(command, cwd, log):
                    self.assertEqual(command[:4], ["dotnet", "nuget", "verify", "--all"])
                    if not signature_passes:
                        raise ValueError("Signature refused")
                    log.write_text("unit signature runner; no public proof claimed")
                with patch.object(gate, "fetch", side_effect=[json.dumps(registration).encode(), json.dumps(catalog).encode()]), patch.object(gate, "run", side_effect=signature):
                    if signature_passes:
                        result = gate.official_archive(gate.GENERATOR, self.version, self.work / "out.nupkg", archive, self.source)
                        self.assertEqual(result["sha256"], gate.digest(archive))
                    else:
                        with self.assertRaisesRegex(ValueError, "Signature refused"):
                            gate.official_archive(gate.GENERATOR, self.version, self.work / "out.nupkg", archive, self.source)

    def test_resolved_transitive_families_must_use_exact_candidate_archive_bytes(self):
        archive = self.archive(gate.GENERATOR)
        cached = self.work / "cache/generator.nupkg"
        cached.parent.mkdir()
        name = gate.GENERATOR.lower() + "." + self.version + ".nupkg"
        cached = cached.parent / name
        assets = {"libraries": {gate.GENERATOR + "/" + self.version: {"path": "cache"}}}
        cached.write_bytes(archive.read_bytes())
        gate.verify_restored_candidates(assets, self.work, self.work, self.version)
        cached.write_bytes(b"same-version different package")
        with self.assertRaisesRegex(ValueError, "exact candidate archive"):
            gate.verify_restored_candidates(assets, self.work, self.work, self.version)
        with self.assertRaisesRegex(ValueError, "different EventStore inventory/version"):
            gate.verify_restored_candidates({"libraries": {gate.GENERATOR + "/4.2.0": {"path": "cache"}}}, self.work, self.work, self.version)

    def test_only_resolved_runtime_dependencies_fill_missing_reference_names(self):
        assets = {"libraries": {"Library/2.0.0": {"path": "library/2.0.0"}},
                  "targets": {"net11.0": {"Library/2.0.0": {"type": "package", "runtime": {
                      "lib/net11.0/AlreadyCompiled.dll": {}, "lib/net11.0/RuntimeOnly.dll": {}}}}}}
        selected = [self.work / "ref/AlreadyCompiled.dll"]
        self.assertEqual(gate.runtime_only_references(assets, "net11.0", self.work, selected),
                         [self.work / "library/2.0.0/lib/net11.0/RuntimeOnly.dll"])

    def test_canonical_baseline_tag_is_checked_before_candidate_source(self):
        with patch.object(gate, "git", return_value="d" * 40):
            with self.assertRaisesRegex(ValueError, "canonical v4.2.0"):
                gate.verify_source(ROOT, self.source)

    def test_prepare_gate_is_after_validation_and_before_publish(self):
        config = json.loads((ROOT / ".releaserc.json").read_text())
        execution = next(plugin[1] for plugin in config["plugins"] if isinstance(plugin, list) and plugin[0] == "@semantic-release/exec")
        preparation = execution["prepareCmd"].split(" && ")
        self.assertEqual(len(preparation), 3)
        self.assertIn("pack-release-packages.py", preparation[0])
        self.assertIn("validate-release-packages.py", preparation[1])
        self.assertEqual(preparation[2], "python3 tools/verify-release-api-compatibility.py ./nupkgs ${nextRelease.version}")
        self.assertIn("dotnet nuget push", execution["publishCmd"])


class RealApiCompatTests(unittest.TestCase):
    def test_actual_compatible_removed_member_and_named_argument_controls(self):
        cache = Path(os.environ.get("NUGET_PACKAGES", str(Path.home() / ".nuget/packages")))
        tool = Path(os.environ.get("RELEASE_API_COMPAT_TOOL_DLL", str(cache / "microsoft.dotnet.apicompat.tool" / gate.APICOMPAT_VERSION / "tools/net10.0/any/Microsoft.DotNet.ApiCompat.Tool.dll")))
        self.assertTrue(tool.is_file(), "Offline real control requires Microsoft.DotNet.ApiCompat.Tool " + gate.APICOMPAT_VERSION)
        command = ["dotnet", "exec", "--roll-forward", "Major", str(tool)]
        version = subprocess.check_output([*command, "--version"], text=True).strip()
        self.assertTrue(version.startswith(gate.APICOMPAT_VERSION), version)
        sdk = subprocess.check_output(["dotnet", "--version"], cwd=ROOT, text=True).strip()
        lines = subprocess.check_output(["dotnet", "--list-sdks"], text=True).splitlines()
        selected = next(line for line in lines if line.startswith(sdk + " ["))
        dotnet_root = Path(selected.split("[", 1)[1].rstrip("]")).parent
        frameworks = {}
        for tfm, package_version in gate.FRAMEWORK_VERSIONS.items():
            directory = dotnet_root / "packs/Microsoft.NETCore.App.Ref" / package_version / "ref" / tfm
            self.assertTrue(directory.is_dir(), f"Offline real control requires reference pack {package_version}")
            frameworks[tfm] = list(directory.glob("*.dll"))
        with tempfile.TemporaryDirectory(prefix="release api real control ") as directory:
            root = Path(directory)
            evidence = root / "evidence"
            scratch = root / "scratch"
            result = gate.control(command, frameworks, scratch, evidence=evidence)
            self.assertEqual(result["compatible"], "pass")
            self.assertEqual(result["removedMember"], "refused CP0002")
            self.assertEqual(result["renamedParameter"], "refused CP0017")
            self.assertIn("RequiredMember", (evidence / "removed.log").read_text())
            renamed = (evidence / "renamed.log").read_text()
            self.assertIn("tenantId", renamed)
            self.assertIn("accountId", renamed)
            inventory = json.loads((evidence / "reference-inventory.json").read_text())
            self.assertEqual(len(inventory["assemblies"]), 4)
            self.assertTrue(all(len(row["sha256"]) == 64 for row in inventory["assemblies"]))
            self.assertFalse(list(evidence.rglob("*.dll")))
            self.assertEqual(len(list(scratch.rglob("Control.dll"))), 4)


if __name__ == "__main__":
    unittest.main()
