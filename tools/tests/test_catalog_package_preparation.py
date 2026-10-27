"""Exercise package provenance, unpublished-dependency rejection and consumer isolation."""

import importlib.util
import json
from pathlib import Path
import subprocess
import sys
import tempfile
import unittest
from zipfile import ZipFile


ROOT = Path(__file__).resolve().parents[2]
sys.path.insert(0, str(ROOT / "tools"))
SPEC = importlib.util.spec_from_file_location("catalog_package_preparation", ROOT / "tools/prepare-catalog-packages.py")
PREPARATION = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(PREPARATION)
from release_package_contract import read_package_metadata


class CatalogPackagePreparationTests(unittest.TestCase):
    def package(self, directory, *, source="a" * 40, dependency="3.128.0-story512.a"):
        path = Path(directory) / "candidate.nupkg"
        with ZipFile(path, "w") as archive:
            archive.writestr("candidate.nuspec", f"""<package><metadata>
                <id>Hexalith.Tenants.Contracts</id><version>5.10.1-story512.a</version>
                <repository type="git" url="https://github.com/Hexalith/Hexalith.Tenants" commit="{source}"/>
                <dependencies><group targetFramework="net10.0"><dependency id="Hexalith.EventStore.Contracts" version="{dependency}"/></group></dependencies>
                </metadata></package>""")
        return path

    def test_candidate_identity_binds_full_source_dependency_and_hash(self):
        with tempfile.TemporaryDirectory() as directory:
            path = self.package(directory)
            identity = PREPARATION.package_identity(path, "Hexalith.Tenants.Contracts", "5.10.1-story512.a", "a" * 40,
                {"Hexalith.EventStore.Contracts": "3.128.0-story512.a"})
            self.assertEqual(identity["source"], "a" * 40)
            self.assertRegex(identity["sha256"], "^[0-9a-f]{64}$")
            for source, dependency in (("b" * 40, "3.128.0-story512.a"), ("a" * 40, "3.128.2")):
                with self.assertRaises(ValueError):
                    PREPARATION.package_identity(self.package(directory, source=source, dependency=dependency),
                        "Hexalith.Tenants.Contracts", "5.10.1-story512.a", "a" * 40,
                        {"Hexalith.EventStore.Contracts": "3.128.0-story512.a"})

    def test_published_artifact_reader_refuses_unpublished_candidates(self):
        with tempfile.TemporaryDirectory() as directory:
            with self.assertRaisesRegex(ValueError, "unpublished catalog candidate"):
                read_package_metadata(self.package(directory))
            published = read_package_metadata(self.package(directory, dependency="3.128.2"))
            self.assertEqual(published.dependencies[0].version, "3.128.2")

    def test_environment_is_single_line_and_contains_only_needed_properties(self):
        lines = PREPARATION.environment_lines(Path("/tmp/catalog-feed"), "5.10.1-story512.a")
        self.assertEqual({line.split("=", 1)[0] for line in lines.splitlines()},
                         {"CatalogCandidateTenantsVersion", "RestoreAdditionalProjectSources", "NUGET_PACKAGES"})
        with self.assertRaises(ValueError):
            PREPARATION.environment_lines(Path("/tmp/feed\nVersion=999"), "5.10.1-story512.a")

    def evaluate(self, project, *arguments):
        output = subprocess.check_output(["dotnet", "msbuild", project, "-nologo", "-getItem:PackageVersion",
            "-p:UseHexalithProjectReferences=false", "-p:CatalogCandidateTenantsVersion=5.10.1-story512.control", *arguments],
            cwd=ROOT, text=True)
        return {item["Identity"]: item["Version"] for item in json.loads(output)["Items"]["PackageVersion"]}

    def test_candidate_pins_apply_to_every_nonpackable_consumer(self):
        for name in PREPARATION.CONSUMERS:
            folder = "tools" if name.endswith("CatalogConstruction") else "tests"
            project = "tools/routing-catalog.cs" if name == "routing-catalog.cs" else f"{folder}/{name}/{name}.csproj"
            if name == "routing-catalog.cs":
                output = subprocess.check_output(["dotnet", "build", project, "-v", "quiet", "-getItem:PackageVersion", "-p:UseHexalithProjectReferences=false", "-p:CatalogCandidateTenantsVersion=5.10.1-story512.control"], cwd=ROOT, text=True)
                versions = {item["Identity"]: item["Version"] for item in json.loads(output)["Items"]["PackageVersion"]}
            else:
                versions = self.evaluate(project)
            self.assertEqual(versions["Hexalith.Tenants.Contracts"], "5.10.1-story512.control", name)
            self.assertEqual(versions["Hexalith.Tenants.Server"], "5.10.1-story512.control", name)

    def test_every_published_package_retains_published_tenants_pins(self):
        manifest = json.loads((ROOT / "tools/release-packages.json").read_text())
        for package in manifest["packages"]:
            versions = self.evaluate(package["project"])
            self.assertNotIn("-story512.", versions["Hexalith.Tenants.Contracts"], package["id"])
            self.assertNotIn("-story512.", versions["Hexalith.Tenants.Server"], package["id"])

    def test_accidentally_packable_candidate_consumer_is_rejected(self):
        result = subprocess.run(["dotnet", "msbuild", "tools/Hexalith.EventStore.CatalogConstruction/Hexalith.EventStore.CatalogConstruction.csproj",
            "-nologo", "-t:CollectPackageReferences", "-p:IsPackable=true", "-p:UseHexalithProjectReferences=false",
            "-p:CatalogCandidateTenantsVersion=5.10.1-story512.control"], cwd=ROOT, text=True, capture_output=True)
        self.assertNotEqual(result.returncode, 0)
        self.assertIn("restricted to nonpackable consumers", result.stdout)


if __name__ == "__main__":
    unittest.main()
