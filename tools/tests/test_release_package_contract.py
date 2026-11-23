"""Keep unpublished preparation dependencies outside publishable package metadata."""

import os
from pathlib import Path
import subprocess
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

    def test_resolved_references_distinguish_project_outputs_from_nuget_packages(self):
        with tempfile.TemporaryDirectory() as scratch:
            root = Path(scratch)
            (root / "global.json").write_bytes((ROOT / "global.json").read_bytes())
            feed = root / "feed"
            feed.mkdir()
            (root / "NuGet.Config").write_text(
                '<configuration><packageSources><clear/><add key="test" value="feed"/></packageSources></configuration>'
            )
            env = {**os.environ, "NUGET_PACKAGES": str(root / "packages")}

            def project(name, content=""):
                folder = root / name
                folder.mkdir()
                path = folder / (name + ".csproj")
                path.write_text('<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup>'
                                '<TargetFramework>net11.0</TargetFramework></PropertyGroup>'
                                + content + '</Project>')
                return path

            package = project("Hexalith.EventStore.Packaged")
            source = project("Hexalith.EventStore.FromSource", '<PropertyGroup><Version>9.0.0</Version></PropertyGroup>')
            middle = project("Middle", f'<ItemGroup><ProjectReference Include="{source}"/></ItemGroup>')
            consumer = project("Consumer", f'<ItemGroup><ProjectReference Include="{middle}"/>'
                               '<PackageReference Include="Hexalith.EventStore.Packaged" Version="1.2.3"/></ItemGroup>'
                               f'<Import Project="{ROOT / "Directory.Build.targets"}"/>')
            subprocess.run(["dotnet", "pack", str(package), "-o", str(feed), "-p:Version=1.2.3"],
                           cwd=root, env=env, check=True, stdout=subprocess.PIPE, stderr=subprocess.STDOUT)
            for selected, valid in (("1.2.3", True), ("1.2.4", False)):
                with self.subTest(selected=selected):
                    result = subprocess.run(["dotnet", "build", str(consumer), "-c", "Release",
                                             "-p:HexalithEventStoreVersion=" + selected],
                                            cwd=root, env=env, text=True, stdout=subprocess.PIPE, stderr=subprocess.STDOUT)
                    if valid:
                        self.assertEqual(0, result.returncode, result.stdout)
                    else:
                        self.assertNotEqual(0, result.returncode)
                        self.assertIn("resolved Hexalith.EventStore.Packaged 1.2.3. "
                                      "The selected EventStore version is 1.2.4.", result.stdout)


if __name__ == "__main__":
    unittest.main()
