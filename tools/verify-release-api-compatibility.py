#!/usr/bin/env python3
"""Compare all release APIs with the signed public 4.2.0 baseline.

The runtime contract deliberately moves from net10.0 to net11.0. Assembly
comparison preserves its public members without claiming old-runtime support.
Each family gets its own freshly resolved candidate consumer context. The old
side uses the old framework references and the selected current external APIs;
the new side uses the new framework references. No suppression or cache-wide
assembly search is permitted.
"""

from __future__ import annotations

import argparse
import base64
import gzip
import hashlib
import json
import os
from pathlib import Path
import re
import shutil
import subprocess
import sys
import tempfile
import urllib.parse
import urllib.request
import xml.etree.ElementTree as ET
from zipfile import ZipFile

from release_package_contract import load_release_manifest


ROOT = Path(__file__).resolve().parents[1]
BASELINE_VERSION = "4.2.0"
BASELINE_COMMIT = "aa16227bd07f0564a3026d619a5abd973e02656f"
REPOSITORY_URL = "https://github.com/Hexalith/Hexalith.EventStore"
APICOMPAT_VERSION = "11.0.100"
FRAMEWORK_VERSIONS = {"net10.0": "10.0.15", "net11.0": "11.0.0"}
PACKAGE_IDS = frozenset("Hexalith.EventStore." + name for name in (
    "Contracts", "Client", "Server", "SignalR", "Testing", "Testing.Integration",
    "Aspire", "ServiceDefaults", "DomainService", "RestApi.Generators", "Gateway",
    "Admin.Abstractions", "Admin.Cli", "Admin.Server",
))
GENERATOR = "Hexalith.EventStore.RestApi.Generators"
TOOL = "Hexalith.EventStore.Admin.Cli"
UNRESOLVED = re.compile(r"could not resolve|unable to resolve|cannot resolve|CP100[12]", re.I)


def digest(path: Path) -> str:
    return hashlib.sha256(path.read_bytes()).hexdigest()


def run(command: list[str], cwd: Path, log: Path, *, check: bool = True) -> subprocess.CompletedProcess:
    """Retain exact commands/output; every subprocess has a finite deadline."""
    result = subprocess.run(command, cwd=cwd, capture_output=True, text=True, timeout=600)
    log.parent.mkdir(parents=True, exist_ok=True)
    log.write_text(json.dumps(command) + "\n" + result.stdout + result.stderr, encoding="utf-8")
    if check and result.returncode:
        raise ValueError(f"Command exited {result.returncode}; see {log}:\n{result.stdout[-3000:]}{result.stderr[-3000:]}")
    return result


def git(root: Path, *arguments: str) -> str:
    return subprocess.check_output(["git", *arguments], cwd=root, text=True, stderr=subprocess.PIPE).strip()


def verify_source(root: Path, candidate_commit: str | None) -> str:
    """Require canonical Git identities, including the immutable public tag."""
    if git(root, "rev-parse", f"v{BASELINE_VERSION}^{{commit}}") != BASELINE_COMMIT:
        raise ValueError("The canonical v4.2.0 Git tag does not match the pinned public baseline.")
    source = candidate_commit or git(root, "rev-parse", "HEAD")
    if not re.fullmatch(r"[0-9a-f]{40}", source) or git(root, "rev-parse", source + "^{commit}") != source:
        raise ValueError("Candidate source must be an existing canonical full Git commit.")
    subprocess.run(["git", "merge-base", "--is-ancestor", BASELINE_COMMIT, source], cwd=root, check=True)
    return source


def metadata(archive: Path, package_id: str, version: str, source: str | None = None) -> None:
    with ZipFile(archive) as package:
        names = package.namelist()
        if len(names) != len(set(names)):
            raise ValueError(f"Duplicate archive members: {archive}")
        specs = [name for name in names if name.endswith(".nuspec")]
        if len(specs) != 1:
            raise ValueError(f"Expected exactly one nuspec: {archive}")
        document = ET.fromstring(package.read(specs[0]))
        entries = document.findall("{*}metadata")
        if len(entries) != 1:
            raise ValueError(f"Expected exactly one metadata element: {archive}")
        entry = entries[0]
        if entry.findtext("{*}id") != package_id or entry.findtext("{*}version") != version:
            raise ValueError(f"Wrong package identity/version: {archive}")
        if source is not None:
            repositories = entry.findall("{*}repository")
            expected = {"type": "git", "url": REPOSITORY_URL, "commit": source}
            if len(repositories) != 1 or any(repositories[0].get(key) != value for key, value in expected.items()):
                raise ValueError(f"Wrong repository/source identity: {archive}")


def candidate_inventory(directory: Path, version: str, source: str) -> dict[str, Path]:
    expected = {f"{name}.{version}.nupkg": name for name in PACKAGE_IDS}
    archives = {p.name: p for p in directory.iterdir() if p.is_file() and p.suffix.lower() == ".nupkg"}
    if archives.keys() != expected.keys():
        raise ValueError(f"Candidate must contain exactly all 14 families; missing={sorted(expected.keys() - archives.keys())}, unexpected={sorted(archives.keys() - expected.keys())}")
    result = {}
    for filename, name in expected.items():
        metadata(archives[filename], name, version, source)
        result[name] = archives[filename]
    return result


def fetch(url: str) -> bytes:
    request = urllib.request.Request(url, headers={"User-Agent": "Hexalith.EventStore.API-Gate/1"})
    with urllib.request.urlopen(request, timeout=120) as response:
        body = response.read()
    return gzip.decompress(body) if body.startswith(b"\x1f\x8b") else body


def official_archive(package_id: str, version: str, output: Path, cached: Path | None = None,
                     source: str | None = None, *, signature_log: Path | None = None) -> dict:
    """Authenticate bytes from the official catalog, metadata and NuGet signatures."""
    lower = package_id.lower()
    registration_url = f"https://api.nuget.org/v3/registration5-gz-semver2/{lower}/{version}.json"
    registration = json.loads(fetch(registration_url))
    catalog_url = registration["catalogEntry"]
    parsed = urllib.parse.urlparse(catalog_url)
    if parsed.scheme != "https" or parsed.netloc != "api.nuget.org" or not parsed.path.startswith("/v3/catalog0/"):
        raise ValueError(f"Unexpected official catalog authority: {catalog_url}")
    catalog = json.loads(fetch(catalog_url))
    if catalog.get("id", "").lower() != lower or catalog.get("version") != version or catalog.get("packageHashAlgorithm") != "SHA512" or not catalog.get("listed"):
        raise ValueError(f"Wrong official catalog identity/hash/listing: {package_id} {version}")
    expected = catalog["packageHash"]
    body = cached.read_bytes() if cached is not None and cached.is_file() else b""
    url = f"https://api.nuget.org/v3-flatcontainer/{lower}/{version}/{lower}.{version}.nupkg"
    if base64.b64encode(hashlib.sha512(body).digest()).decode() != expected:
        body = fetch(url)
    if base64.b64encode(hashlib.sha512(body).digest()).decode() != expected:
        raise ValueError(f"Official SHA512 mismatch: {package_id} {version}")
    output.parent.mkdir(parents=True, exist_ok=True)
    output.write_bytes(body)
    metadata(output, package_id, version, source)
    log = signature_log or output.with_suffix(".signature.log")
    run(["dotnet", "nuget", "verify", "--all", str(output)], output.parent, log)
    return {"id": package_id, "version": version, "archive": str(output), "sha256": digest(output),
            "officialUrl": url, "catalogUrl": catalog_url, "officialSha512": expected,
            "sourceCommit": source, "signatureLog": str(log), "signatureLogSha256": digest(log)}


def extract_member(archive: Path, member: str, output: Path) -> Path:
    with ZipFile(archive) as package:
        if package.namelist().count(member) != 1:
            raise ValueError(f"Missing or repeated API assembly {member}: {archive}")
        output.parent.mkdir(parents=True, exist_ok=True)
        output.write_bytes(package.read(member))
    return output


def member(package_id: str, framework: str) -> str:
    prefix = "analyzers/dotnet/cs" if package_id == GENERATOR else f"tools/{framework}/any" if package_id == TOOL else f"lib/{framework}"
    return f"{prefix}/{package_id}.dll"


def extract_directory(archive: Path, prefix: str, output: Path) -> list[Path]:
    """Only read one known reference directory; never extract arbitrary zip paths."""
    paths = []
    with ZipFile(archive) as package:
        for name in package.namelist():
            relative = name.removeprefix(prefix)
            if name.startswith(prefix) and "/" not in relative and relative.endswith(".dll"):
                paths.append(extract_member(archive, name, output / relative))
    if not paths:
        raise ValueError(f"No reference DLLs in {archive}:{prefix}")
    return paths


def stage_references(files: list[Path], output: Path) -> list[dict]:
    """Flatten only a resolved context, refusing ambiguous simple DLL names."""
    output.mkdir(parents=True, exist_ok=True)
    selected: dict[str, tuple[Path, str]] = {}
    for path in files:
        if not path.is_file() or path.suffix.lower() != ".dll":
            raise ValueError(f"Missing/non-DLL resolved reference: {path}")
        key, sha = path.name.casefold(), digest(path)
        if key in selected and selected[key][1] != sha:
            raise ValueError(f"Ambiguous resolved DLL {path.name}: {selected[key][0]} and {path}")
        selected[key] = path, sha
    rows = []
    for path, sha in selected.values():
        target = output / path.name
        shutil.copyfile(path, target)
        rows.append({"name": path.name, "source": str(path), "sha256": sha})
    return rows


def with_framework(selected: list[Path], framework: list[Path]) -> list[Path]:
    """Keep explicit package selections; framework contracts fill remaining names.

    ResolveReferences can select an app-local package such as DataProtection
    over a same-named shared-framework reference. Do not reintroduce that losing
    framework assembly when replacing the old/new framework context. Ambiguous
    files within either selected set still fail in stage_references.
    """
    names = {path.name.casefold() for path in selected}
    return selected + [path for path in framework if path.name.casefold() not in names]


def project_context(root: Path, project: Path, work: Path, catalog: Path | None = None) -> dict:
    # Evaluation only: do not run targets or mutate the owning checkout's obj/bin.
    command = ["dotnet", "msbuild", str(project), "-getItem:PackageReference,PackageVersion,FrameworkReference,ProjectReference",
               "-getProperty:TargetFramework", "-p:Configuration=Release", "-p:UseHexalithProjectReferences=false",
               "-p:UseNuGetDeps=true", "-nologo", "-nr:false"]
    if catalog is not None:
        command += ["-p:HexalithBuildPackageVersions1=" + str(catalog),
                    "-p:Hexalith1BuildPackageProps=" + str(catalog)]
    result = run(command, root, work / "project-evaluation.log")
    return json.loads(result.stdout[result.stdout.index("{"):])


def verify_restored_candidates(assets: dict, cache: Path, feed: Path, version: str) -> None:
    """Prove resolution used these candidate bytes, including transitive families."""
    for identity, library in assets["libraries"].items():
        name, selected_version = identity.rsplit("/", 1)
        if name.startswith("Hexalith.EventStore."):
            if name not in PACKAGE_IDS or selected_version != version:
                raise ValueError(f"Resolved a different EventStore inventory/version: {identity}")
            restored = cache / library["path"] / f"{name.lower()}.{selected_version.lower()}.nupkg"
            candidate = feed / f"{name}.{version}.nupkg"
            if not restored.is_file() or digest(restored) != digest(candidate):
                raise ValueError(f"Resolved package differs from the exact candidate archive: {identity}")


def runtime_only_references(assets: dict, framework: str, cache: Path, compiled: list[Path]) -> list[Path]:
    """Include runtime-only dependencies from this resolved target, never a cache scan.

    Some packages deliberately publish a compile `_._` while another dependency
    still references their implementation (for example Microsoft.NET.StringTools).
    APICompat must resolve those too. Existing compiler selections remain the
    API authority when a package also ships a different implementation DLL.
    """
    names = {path.name.casefold() for path in compiled}
    result = []
    for identity, node in assets["targets"][framework].items():
        if node.get("type") != "package":
            continue
        for relative in node.get("runtime", {}):
            path = cache / assets["libraries"][identity]["path"] / relative
            if path.suffix.lower() == ".dll" and path.name.casefold() not in names:
                result.append(path)
    return result


def resolve_context(package_id: str, project: Path, root: Path, version: str, feed: Path,
                    work: Path, cache: Path, manifest_projects: dict[Path, str],
                    catalog: Path | None = None, *, scratch: Path | None = None) -> tuple[list[Path], str]:
    work.mkdir(parents=True, exist_ok=True)
    build = scratch or work
    build.mkdir(parents=True, exist_ok=True)
    context = project_context(root, project, work, catalog)
    framework = "netstandard2.0" if package_id == GENERATOR else "net11.0"
    if context["Properties"]["TargetFramework"] != framework:
        raise ValueError(f"Wrong active target for {package_id}; expected {framework}")
    versions = {row["Identity"]: row["Version"] for row in context["Items"]["PackageVersion"]}
    versions.update({name: version for name in PACKAGE_IDS})
    central = ET.Element("Project")
    settings = ET.SubElement(central, "PropertyGroup")
    for name in ("ManagePackageVersionsCentrally", "CentralPackageTransitivePinningEnabled"):
        ET.SubElement(settings, name).text = "true"
    items = ET.SubElement(central, "ItemGroup")
    for name, selected in sorted(versions.items()):
        ET.SubElement(items, "PackageVersion", Include=name, Version=selected)
    ET.ElementTree(central).write(build / "Directory.Packages.props", encoding="utf-8", xml_declaration=True)
    if build != work:
        shutil.copyfile(build / "Directory.Packages.props", work / "Directory.Packages.props")
    consumer = ET.Element("Project", Sdk="Microsoft.NET.Sdk")
    settings = ET.SubElement(consumer, "PropertyGroup")
    for name, value in {"TargetFramework": framework, "RestorePackagesPath": str(cache),
                        "RestoreConfigFile": str(feed.parent / "NuGet.Config"), "NuGetAudit": "true",
                        "NuGetAuditMode": "all", "NuGetAuditLevel": "low", "TreatWarningsAsErrors": "true"}.items():
        ET.SubElement(settings, name).text = value
    items = ET.SubElement(consumer, "ItemGroup")
    if package_id not in (TOOL, GENERATOR):
        ET.SubElement(items, "PackageReference", Include=package_id)
    else:
        # Generator packages suppress their Roslyn dependencies; tool packages cannot
        # be consumed with PackageReference. Resolve their actual declared contexts.
        for row in context["Items"]["PackageReference"]:
            if row.get("IsImplicitlyDefined", "false").lower() == "true":
                continue
            name = row["Identity"]
            if name not in versions:
                raise ValueError(f"Missing central version for {name}")
            ET.SubElement(items, "PackageReference", Include=name)
        for row in context["Items"]["ProjectReference"]:
            referenced = Path(row["FullPath"]).resolve()
            if referenced not in manifest_projects:
                raise ValueError(f"Unexpected non-inventory source edge: {referenced}")
            ET.SubElement(items, "PackageReference", Include=manifest_projects[referenced])
    for row in context["Items"]["FrameworkReference"]:
        if row["Identity"] != "Microsoft.NETCore.App":
            ET.SubElement(items, "FrameworkReference", Include=row["Identity"])
    probe = build / "Context.csproj"
    ET.ElementTree(consumer).write(probe, encoding="utf-8", xml_declaration=True)
    if build != work:
        shutil.copyfile(probe, work / "Context.csproj")
    command = ["dotnet", "msbuild", str(probe), "-restore", "-target:ResolveReferences", "-getItem:ReferencePath",
               "-getProperty:TargetFramework", "-p:Configuration=Release", "-warnaserror", "-m:1", "-nr:false", "-nologo"]
    result = run(command, build, work / "references.log")
    data = json.loads(result.stdout[result.stdout.index("{"):])
    assets_file = build / "obj/project.assets.json"
    assets = json.loads(assets_file.read_text(encoding="utf-8"))
    if build != work:
        shutil.copyfile(assets_file, work / "resolved-assets.json")
    verify_restored_candidates(assets, cache, feed, version)
    paths = [Path(row["Identity"]) for row in data["Items"]["ReferencePath"]]
    paths += runtime_only_references(assets, framework, cache, paths)
    if not paths or any(not path.is_file() for path in paths):
        raise ValueError(f"Empty/incomplete resolved reference context: {package_id}")
    return paths, framework


def compare(tool: list[str], left: Path, right: Path, left_refs: Path, right_refs: Path, log: Path) -> dict:
    command = [*tool, "--left", str(left), "--right", str(right),
               "--left-assembly-references", str(left_refs), "--right-assembly-references", str(right_refs),
               "--enable-rule-cannot-change-parameter-name"]
    result = run(command, log.parent, log, check=False)
    text = result.stdout + result.stderr
    if result.returncode or UNRESOLVED.search(text):
        raise ValueError(f"API compatibility refused {left.name}; exit={result.returncode}; see {log}:\n{text[-4000:]}")
    return {"command": command, "exit": result.returncode, "log": str(log), "logSha256": digest(log)}


def control(tool: list[str], frameworks: dict[str, list[Path]], work: Path, *, evidence: Path | None = None) -> dict:
    """Real cross-framework compatible, removed-member and named-argument controls."""
    evidence = evidence or work
    evidence.mkdir(parents=True, exist_ok=True)
    built = []
    for name, framework, required, parameter in (
        ("baseline", "net10.0", True, "tenantId"), ("compatible", "net11.0", True, "tenantId"),
        ("removed", "net11.0", False, "tenantId"), ("renamed", "net11.0", True, "accountId"),
    ):
        directory = work / name
        directory.mkdir(parents=True)
        sources = directory / "Control.cs"
        sources.write_text("public class Contract { " + ("public int RequiredMember() => 42; " if required else "")
                           + f"public string Find(string {parameter}) => {parameter}; }}", encoding="utf-8")
        response = directory / "compile.rsp"
        response.write_text("\n".join(["-target:library", "-nologo", f'-out:"{directory / "Control.dll"}"',
                                       *[f'-reference:"{p}"' for p in frameworks[framework]], f'"{sources}"']), encoding="utf-8")
        logs = evidence / name
        logs.mkdir(parents=True, exist_ok=True)
        if logs != directory:
            shutil.copyfile(sources, logs / sources.name)
            shutil.copyfile(response, logs / response.name)
        sdk = run(["dotnet", "--list-sdks"], directory, logs / "sdk.log").stdout.splitlines()
        selected = run(["dotnet", "--version"], ROOT, logs / "sdk-version.log").stdout.strip()
        matches = [line for line in sdk if line.startswith(selected + " [")]
        if len(matches) != 1:
            raise ValueError("Could not locate the selected SDK compiler for the real API control.")
        compiler = Path(matches[0].split("[", 1)[1].rstrip("]")) / selected / "Roslyn/bincore/csc.dll"
        run(["dotnet", str(compiler), "@" + str(response)], directory, logs / "build.log")
        built.append(directory / "Control.dll")
    old_refs, new_refs = work / "old-refs", work / "new-refs"
    old_rows = stage_references(frameworks["net10.0"], old_refs)
    new_rows = stage_references(frameworks["net11.0"], new_refs)
    (evidence / "reference-inventory.json").write_text(json.dumps({"leftReferences": old_rows,
        "rightReferences": new_rows, "assemblies": [{"path": str(path), "sha256": digest(path)} for path in built]}, indent=2) + "\n", encoding="utf-8")
    compare(tool, built[0], built[1], old_refs, new_refs, evidence / "compatible.log")
    try:
        compare(tool, built[0], built[2], old_refs, new_refs, evidence / "removed.log")
    except ValueError:
        text = (evidence / "removed.log").read_text(encoding="utf-8")
        if "CP0002" not in text or "RequiredMember" not in text or UNRESOLVED.search(text):
            raise ValueError("Removed-member control failed for a reason other than genuine CP0002.")
    else:
        raise ValueError("APICompat accepted a genuinely removed public member.")
    try:
        compare(tool, built[0], built[3], old_refs, new_refs, evidence / "renamed.log")
    except ValueError:
        text = (evidence / "renamed.log").read_text(encoding="utf-8")
        if "CP0017" not in text or "tenantId" not in text or "accountId" not in text or UNRESOLVED.search(text):
            raise ValueError("Named-argument control failed for a reason other than genuine CP0017.")
    else:
        raise ValueError("APICompat accepted a genuinely renamed public parameter.")
    return {"compatible": "pass", "removedMember": "refused CP0002", "renamedParameter": "refused CP0017", "directory": str(evidence)}


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("package_directory", type=Path)
    parser.add_argument("version")
    parser.add_argument("--repository-root", type=Path, default=ROOT)
    parser.add_argument("--candidate-commit", help="Full source identity for an explicit private-pack verification; defaults to HEAD.")
    parser.add_argument("--baseline-directory", type=Path, help="Optional archive cache; official catalog and signatures are still verified.")
    parser.add_argument("--package-catalog", type=Path, help="Explicit current catalog for private producer verification; release preparation uses its tracked catalog.")
    parser.add_argument("--work-directory", type=Path, help="New or empty evidence directory; defaults beneath the repository's uploaded .hexalith/release-evidence/api-compatibility directory.")
    args = parser.parse_args()
    if args.work_directory:
        work = args.work_directory.resolve()
        if work.exists() and any(work.iterdir()):
            raise ValueError(f"API evidence directory must be empty: {work}")
        work.mkdir(parents=True, exist_ok=True)
    else:
        parent = args.repository_root.resolve() / ".hexalith/release-evidence/api-compatibility"
        parent.mkdir(parents=True, exist_ok=True)
        work = Path(tempfile.mkdtemp(prefix="run-", dir=parent))
    print(f"API gate evidence: {work}", flush=True)
    try:
        return verify(args, work)
    except Exception as error:
        failure = {"status": "fail", "errorType": type(error).__name__, "error": str(error),
                   "candidateVersion": args.version, "repositoryRoot": str(args.repository_root.resolve())}
        (work / "failure.json").write_text(json.dumps(failure, indent=2) + "\n", encoding="utf-8")
        receipt_file = work / "receipt.json"
        if receipt_file.is_file():
            receipt = json.loads(receipt_file.read_text(encoding="utf-8"))
            receipt.update(failure)
            receipt_file.write_text(json.dumps(receipt, indent=2) + "\n", encoding="utf-8")
        raise


def verify(args: argparse.Namespace, work: Path) -> int:
    """Keep every diagnostic in the release workflow's retained evidence tree."""
    root, directory = args.repository_root.resolve(), args.package_directory.resolve()
    source = verify_source(root, args.candidate_commit)
    manifest = load_release_manifest(root / "tools/release-packages.json", root)
    if {p.package_id for p in manifest} != PACKAGE_IDS:
        raise ValueError("The release manifest must retain exactly all 14 API baseline families.")
    candidates = candidate_inventory(directory, args.version, source)
    original_hashes = {name: digest(path) for name, path in candidates.items()}
    # The workflow excludes only this cache subtree from its evidence upload.
    # Logs, identities, source/project inputs and hash inventories stay outside it.
    scratch = work / "scratch"
    scratch.mkdir()
    # Keep temporary consumers on the repository-selected SDK and stop unrelated
    # Directory.Build files above the work directory from changing their graph.
    shutil.copyfile(root / "global.json", work / "global.json")
    shutil.copyfile(root / "global.json", scratch / "global.json")
    for filename in ("Directory.Build.props", "Directory.Build.targets"):
        (work / filename).write_text("<Project/>\n", encoding="utf-8")
        (scratch / filename).write_text("<Project/>\n", encoding="utf-8")
    feed = scratch / "candidate-feed"
    feed.mkdir()
    for name, path in candidates.items():
        shutil.copyfile(path, feed / path.name)
        candidates[name] = feed / path.name
    config = ET.Element("configuration")
    sources = ET.SubElement(config, "packageSources")
    ET.SubElement(sources, "clear")
    ET.SubElement(sources, "add", key="candidate", value=str(feed))
    ET.SubElement(sources, "add", key="nuget.org", value="https://api.nuget.org/v3/index.json")
    ET.ElementTree(config).write(scratch / "NuGet.Config", encoding="utf-8", xml_declaration=True)
    shutil.copyfile(scratch / "NuGet.Config", work / "NuGet.Config")
    cache = Path(os.environ.get("NUGET_PACKAGES", str(Path.home() / ".nuget/packages")))
    receipt = {"baselineVersion": BASELINE_VERSION, "baselineSource": BASELINE_COMMIT, "candidateVersion": args.version,
               "candidateSource": source, "apiCompatVersion": APICOMPAT_VERSION, "scope": "Public API metadata preservation; intentional net10-to-net11 runtime break; generator stays netstandard2.0.",
               "baseline": [], "frameworks": [], "packages": [], "status": "in-progress"}
    receipt["selectedSdk"] = run(["dotnet", "--version"], scratch, work / "selected-sdk.log").stdout.strip()
    receipt["globalJsonSha256"] = digest(work / "global.json")
    if args.package_catalog:
        args.package_catalog = args.package_catalog.resolve()
        receipt["explicitPackageCatalog"] = {"path": str(args.package_catalog), "sha256": digest(args.package_catalog)}
    (work / "receipt.json").write_text(json.dumps(receipt, indent=2) + "\n", encoding="utf-8")
    left, right = scratch / "baseline-assemblies", scratch / "candidate-assemblies"
    for package in manifest:
        name = package.package_id
        cached = args.baseline_directory / f"{name}.{BASELINE_VERSION}.nupkg" if args.baseline_directory else cache / name.lower() / BASELINE_VERSION / f"{name.lower()}.{BASELINE_VERSION}.nupkg"
        archive = scratch / "baseline" / f"{name}.{BASELINE_VERSION}.nupkg"
        receipt["baseline"].append(official_archive(name, BASELINE_VERSION, archive, cached, BASELINE_COMMIT,
            signature_log=work / "signatures" / f"{name}.{BASELINE_VERSION}.log"))
        (work / "receipt.json").write_text(json.dumps(receipt, indent=2) + "\n", encoding="utf-8")
        extract_member(archive, member(name, "net10.0"), left / f"{name}.dll")
        extract_member(candidates[name], member(name, "net11.0"), right / f"{name}.dll")
    tool_name = "Microsoft.DotNet.ApiCompat.Tool"
    tool_archive = scratch / "tool.nupkg"
    receipt["tool"] = official_archive(tool_name, APICOMPAT_VERSION, tool_archive,
        cache / tool_name.lower() / APICOMPAT_VERSION / f"{tool_name.lower()}.{APICOMPAT_VERSION}.nupkg",
        signature_log=work / "signatures" / f"{tool_name}.{APICOMPAT_VERSION}.log")
    (work / "receipt.json").write_text(json.dumps(receipt, indent=2) + "\n", encoding="utf-8")
    with ZipFile(tool_archive) as archive:
        for name in archive.namelist():
            relative = Path(name)
            if relative.is_absolute() or ".." in relative.parts:
                raise ValueError("Unsafe APICompat archive path.")
            if name.startswith("tools/net10.0/any/") and not name.endswith("/"):
                path = scratch / "apicompat" / relative
                path.parent.mkdir(parents=True, exist_ok=True)
                path.write_bytes(archive.read(name))
    tool = ["dotnet", "exec", "--roll-forward", "Major", str(scratch / "apicompat/tools/net10.0/any/Microsoft.DotNet.ApiCompat.Tool.dll")]
    selected = run([*tool, "--version"], scratch, work / "tool-version.log").stdout.strip()
    if not re.match(r"^(?:Microsoft\.DotNet\.ApiCompat\s+v)?11\.0\.100(?:\+|$)", selected):
        raise ValueError(f"Wrong APICompat version: {selected}")
    frameworks = {}
    for framework, version in FRAMEWORK_VERSIONS.items():
        files = []
        for name in ("Microsoft.NETCore.App.Ref", "Microsoft.AspNetCore.App.Ref"):
            archive = scratch / f"{name}.{version}.nupkg"
            receipt["frameworks"].append(official_archive(name, version, archive, cache / name.lower() / version / f"{name.lower()}.{version}.nupkg",
                signature_log=work / "signatures" / f"{name}.{version}.log"))
            (work / "receipt.json").write_text(json.dumps(receipt, indent=2) + "\n", encoding="utf-8")
            files.extend(extract_directory(archive, f"ref/{framework}/", scratch / "frameworks" / framework / name))
        frameworks[framework] = files
    receipt["control"] = control(tool, frameworks, scratch / "controls", evidence=work / "controls")
    (work / "receipt.json").write_text(json.dumps(receipt, indent=2) + "\n", encoding="utf-8")
    project_ids = {p.project_path.resolve(): p.package_id for p in manifest}
    for package in manifest:
        name = package.package_id
        current, framework = resolve_context(name, package.project_path, root, args.version, feed,
                                            work / "contexts" / name, scratch / "packages", project_ids, args.package_catalog,
                                            scratch=scratch / "contexts" / name)
        # Remove actual framework-pack references before selecting the respective
        # old/new framework. Everything else is from this one resolved consumer.
        external = [path for path in current if "packs" not in path.parts or not any(
            part in ("Microsoft.NETCore.App.Ref", "Microsoft.AspNetCore.App.Ref") for part in path.parts)]
        old_refs, new_refs = [], []
        for path in external:
            family = path.stem
            old_refs.append(left / path.name if family in PACKAGE_IDS else path)
            new_refs.append(right / path.name if family in PACKAGE_IDS else path)
        if framework != "netstandard2.0":
            old_refs = with_framework(old_refs, frameworks["net10.0"])
            new_refs = with_framework(new_refs, frameworks["net11.0"])
        context = work / "contexts" / name
        reference_cache = scratch / "contexts" / name
        old_rows = stage_references(old_refs, reference_cache / "left-refs")
        new_rows = stage_references(new_refs, reference_cache / "right-refs")
        (context / "reference-inventory.json").write_text(json.dumps({"leftReferences": old_rows,
            "rightReferences": new_rows}, indent=2) + "\n", encoding="utf-8")
        result = compare(tool, left / f"{name}.dll", right / f"{name}.dll", reference_cache / "left-refs", reference_cache / "right-refs", context / "apicompat.log")
        receipt["packages"].append({"id": name, "candidateArchiveSha256": original_hashes[name],
            "baselineAssemblySha256": digest(left / f"{name}.dll"), "candidateAssemblySha256": digest(right / f"{name}.dll"),
            "sourceProjectEvaluationSha256": digest(context / "project-evaluation.log"),
            "currentResolvedReferencesSha256": digest(context / "references.log"),
            "currentCentralVersionsSha256": digest(context / "Directory.Packages.props"),
            "targetFramework": framework, "leftReferences": old_rows, "rightReferences": new_rows, **result})
        (work / "receipt.json").write_text(json.dumps(receipt, indent=2) + "\n", encoding="utf-8")
        print(f"API compatible: {name}", flush=True)
    if {name: digest(path) for name, path in candidate_inventory(directory, args.version, source).items()} != original_hashes:
        raise ValueError("Candidate packages changed during API verification.")
    if verify_source(root, args.candidate_commit) != source:
        raise ValueError("Candidate source changed during API verification.")
    if args.package_catalog and digest(args.package_catalog) != receipt["explicitPackageCatalog"]["sha256"]:
        raise ValueError("Selected package catalog changed during API verification.")
    receipt["status"] = "pass"
    (work / "receipt.json").write_text(json.dumps(receipt, indent=2) + "\n", encoding="utf-8")
    print(f"Verified all 14 families and real compatible/removed-member/renamed-parameter controls; receipt: {work / 'receipt.json'}")
    return 0


if __name__ == "__main__":
    try:
        raise SystemExit(main())
    except Exception as error:  # noqa: BLE001 - publication must fail with its precise blocker.
        print(f"verify-release-api-compatibility: {error}", file=sys.stderr)
        raise SystemExit(1)
