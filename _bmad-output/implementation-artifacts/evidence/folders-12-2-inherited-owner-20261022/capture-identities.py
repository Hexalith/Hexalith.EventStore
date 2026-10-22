"""Bind the uncommitted source graph and local test runtime images by exact bytes."""
import hashlib
import json
import pathlib
import subprocess

root = pathlib.Path.cwd()
destination = pathlib.Path(__file__).resolve().parent


def git(*args):
    return subprocess.check_output(["git", *args], text=True)


def identity(path):
    data = path.read_bytes()
    return dict(path=str(path), size=len(data), sha256=hashlib.sha256(data).hexdigest())


sources = []
for name in git("ls-files", "src", "tests").splitlines():
    path = root / name
    if path.is_file() and path.suffix in (".cs", ".csproj", ".json", ".props", ".targets"):
        sources.append(identity(path))
for name in git("ls-files", "--others", "--exclude-standard", "src", "tests").splitlines():
    path = root / name
    if path.is_file():
        sources.append(identity(path))
configuration = [identity(root / name) for name in
                 ("global.json", "Directory.Build.props", "Directory.Build.targets",
                  "Directory.Packages.props", ".editorconfig", ".gitattributes", ".gitmodules")]
binaries = []
for project in ("Client.Tests", "DomainService.Tests", "Server.LiveSidecar.Tests"):
    directory = root / "tests" / ("Hexalith.EventStore." + project) / "bin/Debug/net10.0"
    for path in sorted(directory.glob("Hexalith.*")):
        if path.suffix in (".dll", ".pdb", ".json"):
            binaries.append(identity(path))
payload = dict(baseline_commit=git("rev-parse", "HEAD").strip(),
               status=git("status", "--short"),
               dependency_revisions=git("submodule", "status"),
               sources=sources, configuration=configuration, binaries=binaries)
baseline_source = subprocess.check_output(["git", "show", payload['baseline_commit']
    + ":src/Hexalith.EventStore.Client/Projections/SharedProjectionEpochCoordinator.cs"])
payload['baseline_coordinator_source_sha256'] = hashlib.sha256(baseline_source).hexdigest()
(destination / "source-binary-identities.json").write_text(json.dumps(payload, indent=2) + "\n")
(destination / "final-source.patch").write_text(git("diff", "--binary", "--", "src", "tests"))
print(json.dumps(dict(source_count=len(sources), binary_count=len(binaries),
                      manifest=identity(destination / "source-binary-identities.json"))))
