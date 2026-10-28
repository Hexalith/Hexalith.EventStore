"""Keep unpublished preparation dependencies outside publishable package metadata."""

from pathlib import Path
import sys
import tempfile
import unittest
from zipfile import ZipFile

ROOT = Path(__file__).resolve().parents[2]
sys.path.insert(0, str(ROOT / "tools"))
from release_package_contract import read_package_metadata


class PublicationDependencyGuardTests(unittest.TestCase):
    def read_dependency(self, directory, version):
        path = Path(directory) / "published.nupkg"
        with ZipFile(path, "w") as archive:
            archive.writestr("published.nuspec", f"""<package><metadata>
                <id>Hexalith.EventStore.Server</id><version>3.128.3</version>
                <dependencies><group targetFramework="net10.0">
                <dependency id="Hexalith.Tenants.Contracts" version="{version}"/>
                </group></dependencies></metadata></package>""")
        return read_package_metadata(path)

    def test_published_dependency_ranges_remain_accepted(self):
        with tempfile.TemporaryDirectory() as directory:
            for version in ("5.10.1", "[5.10.1,)", "[5.10.1,6.0.0)", "5.10.1-rc.1"):
                with self.subTest(version=version):
                    package = self.read_dependency(directory, version)
                    self.assertEqual(package.dependencies[0].version, version)

    def test_unpublished_candidate_dependencies_are_rejected_in_every_range_spelling(self):
        candidate = "5.10.1-story512." + "a" * 40
        with tempfile.TemporaryDirectory() as directory:
            for version in (candidate, "[" + candidate + ",)", "[" + candidate + "]", candidate.upper()):
                with self.subTest(version=version):
                    with self.assertRaisesRegex(ValueError, "unpublished catalog candidate dependency"):
                        self.read_dependency(directory, version)


if __name__ == "__main__":
    unittest.main()
