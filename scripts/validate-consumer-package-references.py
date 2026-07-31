#!/usr/bin/env python3
"""Restore and build isolated package-only consumers for the release inventory."""

from __future__ import annotations

import argparse
import json
import pathlib
import subprocess
import sys
import tempfile
import textwrap
from xml.sax.saxutils import quoteattr


ROOT = pathlib.Path(__file__).resolve().parents[1]
sys.path.insert(0, str(ROOT / "tools"))

from release_package_contract import PackageMetadata, validate_package_directory  # noqa: E402


def write_nuget_config(consumer_dir: pathlib.Path, package_source: pathlib.Path) -> pathlib.Path:
    """Write an isolated source list containing only release packages and NuGet.org."""

    config_path = consumer_dir / "nuget.config"
    config_path.write_text(
        textwrap.dedent(f"""\
        <?xml version="1.0" encoding="utf-8"?>
        <configuration>
          <packageSources>
            <clear />
            <add key="local-release-packages" value={quoteattr(str(package_source))} />
            <add key="nuget.org" value="https://api.nuget.org/v3/index.json" />
          </packageSources>
          <packageSourceMapping>
            <packageSource key="local-release-packages">
              <package pattern="Hexalith.EventStore" />
              <package pattern="Hexalith.EventStore.*" />
            </packageSource>
            <packageSource key="nuget.org">
              <package pattern="*" />
            </packageSource>
          </packageSourceMapping>
        </configuration>
        """),
        encoding="utf-8",
    )
    return config_path


def write_consumer_project(
    consumer_dir: pathlib.Path,
    package_id: str,
    version: str,
) -> pathlib.Path:
    """Create a consumer with exactly one direct manifest package reference."""

    (consumer_dir / "Directory.Packages.props").write_text(
        textwrap.dedent(f"""\
        <Project>
          <PropertyGroup>
            <ManagePackageVersionsCentrally>true</ManagePackageVersionsCentrally>
          </PropertyGroup>
          <ItemGroup>
            <PackageVersion Include="{package_id}" Version="{version}" />
          </ItemGroup>
        </Project>
        """),
        encoding="utf-8",
    )
    project_path = consumer_dir / "PackageConsumer.csproj"
    project_path.write_text(
        textwrap.dedent(f"""\
        <Project Sdk="Microsoft.NET.Sdk">
          <PropertyGroup>
            <TargetFramework>net10.0</TargetFramework>
            <ImplicitUsings>enable</ImplicitUsings>
            <Nullable>enable</Nullable>
          </PropertyGroup>
          <ItemGroup>
            <PackageReference Include="{package_id}" />
          </ItemGroup>
        </Project>
        """),
        encoding="utf-8",
    )
    (consumer_dir / "ConsumerProbe.cs").write_text(
        "namespace Hexalith.EventStore.PackageConsumer;\n\npublic sealed class ConsumerProbe;\n",
        encoding="utf-8",
    )
    return project_path


def run(command: list[str], cwd: pathlib.Path = ROOT) -> None:
    """Run one consumer command and preserve its diagnostic output."""

    completed = subprocess.run(command, cwd=cwd, check=False)
    if completed.returncode != 0:
        raise subprocess.CalledProcessError(completed.returncode, completed.args)


def assert_assets_use_packages(project_path: pathlib.Path, package_id: str, version: str) -> None:
    """Reject project-backed or unresolved direct references in restored assets."""

    assets_path = project_path.parent / "obj" / "project.assets.json"
    with assets_path.open("r", encoding="utf-8") as handle:
        assets = json.load(handle)

    libraries = assets.get("libraries")
    if not isinstance(libraries, dict):
        raise ValueError(f"Consumer assets file is missing libraries: {assets_path}")

    project_libraries = [
        name
        for name, value in libraries.items()
        if isinstance(value, dict) and value.get("type") == "project"
    ]
    if project_libraries:
        raise ValueError(
            "Consumer restore resolved project references instead of packages: "
            + ", ".join(sorted(project_libraries))
        )

    library_keys = {key.casefold() for key in libraries}
    expected_key = f"{package_id}/{version}".casefold()
    if expected_key not in library_keys:
        raise ValueError(f"Consumer restore did not resolve {package_id} at {version}.")


def validate_library_package(
    package: PackageMetadata,
    package_path: pathlib.Path,
) -> None:
    """Restore and build one library without injecting sibling direct references."""

    with tempfile.TemporaryDirectory(prefix="eventstore-package-consumer-") as temp_dir_name:
        consumer_dir = pathlib.Path(temp_dir_name)
        project_path = write_consumer_project(consumer_dir, package.package_id, package.version)
        config_path = write_nuget_config(consumer_dir, package_path)
        run(
            [
                "dotnet",
                "restore",
                str(project_path),
                "--configfile",
                str(config_path),
                "--packages",
                str(consumer_dir / "packages"),
                "-p:UseHexalithProjectReferences=false",
            ]
        )
        run(
            [
                "dotnet",
                "build",
                str(project_path),
                "--no-restore",
                "--configuration",
                "Release",
                "-p:UseHexalithProjectReferences=false",
            ]
        )
        assert_assets_use_packages(project_path, package.package_id, package.version)


def validate_dotnet_tool_package(
    package: PackageMetadata,
    package_path: pathlib.Path,
) -> None:
    """Install one tool package in its own manifest and source boundary."""

    with tempfile.TemporaryDirectory(prefix="eventstore-package-tool-consumer-") as temp_dir_name:
        consumer_dir = pathlib.Path(temp_dir_name)
        config_path = write_nuget_config(consumer_dir, package_path)
        run(["dotnet", "new", "tool-manifest", "--force"], cwd=consumer_dir)
        run(
            [
                "dotnet",
                "tool",
                "install",
                package.package_id,
                "--version",
                package.version,
                "--configfile",
                str(config_path),
            ],
            cwd=consumer_dir,
        )


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("package_directory", help="Directory containing manifest-built .nupkg files.")
    args = parser.parse_args()

    package_dir = pathlib.Path(args.package_directory)
    package_path = package_dir if package_dir.is_absolute() else ROOT / package_dir
    packages, version = validate_package_directory(package_path)
    tool_packages = [package for package in packages if "DotnetTool" in package.package_types]
    library_packages = [package for package in packages if package not in tool_packages]

    for package in library_packages:
        print(f"Validating isolated package-only consumer for {package.package_id}...", flush=True)
        try:
            validate_library_package(package, package_path)
        except Exception as error:
            raise ValueError(f"Package-only consumer failed for {package.package_id}: {error}") from error

    for package in tool_packages:
        print(f"Validating isolated tool consumer for {package.package_id}...", flush=True)
        try:
            validate_dotnet_tool_package(package, package_path)
        except Exception as error:
            raise ValueError(f"Tool-package consumer failed for {package.package_id}: {error}") from error

    print(
        f"Validated {len(library_packages)} isolated package-only consumers and "
        f"{len(tool_packages)} isolated tool consumers at version {version}."
    )
    return 0


if __name__ == "__main__":
    try:
        raise SystemExit(main())
    except Exception as error:  # noqa: BLE001 - CI should print the exact release validation failure.
        print(f"validate-consumer-package-references: {error}", file=sys.stderr)
        raise SystemExit(1)
