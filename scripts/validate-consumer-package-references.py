#!/usr/bin/env python3
"""Restore and build isolated package-only consumers for the release inventory."""

from __future__ import annotations

import argparse
import json
import os
import pathlib
import subprocess
import sys
import tempfile
import textwrap
from xml.sax.saxutils import quoteattr


ROOT = pathlib.Path(__file__).resolve().parents[1]
sys.path.insert(0, str(ROOT / "tools"))

from release_package_contract import (  # noqa: E402
    TOOL_PACKAGE_IDS,
    PackageMetadata,
    validate_package_directory,
)


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
            <OutputType>Exe</OutputType>
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
    probes = {
        "Hexalith.EventStore.Contracts": textwrap.dedent("""\
            using Hexalith.EventStore.Contracts.Streams;
            using Hexalith.EventStore.Contracts.Effects;

            var effectIdentity = new EffectIdentity("tenant-a", "widget", "source-1", 1,
                EffectKindCatalog.DateResume, "widget", "item-1", 0);
            var effectId = EffectIdentityCodec.ComputeEffectId(effectIdentity);
            var effectMessage = EffectIdentityCodec.ComputeMessageId(effectIdentity);
            var effect = new TrustedEffectSubmission(effectIdentity, "CreateWidget", [123, 125],
                effectMessage, effectMessage);
            var provenance = new TrustedEffectContext("synthetic-worker", "synthetic-date-resume",
                "source-cause", "synthetic-delegation");
            if (effectId.Length != 52 || effect.MessageId != "wrk-" + effectId
                || provenance.Workload != "synthetic-worker")
                throw new InvalidOperationException("Trusted effect public identity API changed.");

            var request = new StreamReadRequest("tenant-a", "widget", "item-1");
            var page = new StreamReadPage("tenant-a", "widget", "item-1", [],
                new StreamReadMetadata(0, null, null, 0, 0, false, null));
            if (StreamReadPageValidator.ValidateAndGetNextSequence(request, page) != 0)
                throw new InvalidOperationException("The exclusive cursor contract changed.");
            """),
        "Hexalith.EventStore.Client": textwrap.dedent("""\
            using Hexalith.EventStore.Client.Projections;
            using Hexalith.EventStore.Client.Effects;
            using Hexalith.EventStore.Contracts.Effects;
            using System.Text.Json;

            var syntheticIdentity = new EffectIdentity("tenant-a", "widget", "source-1", 1,
                EffectKindCatalog.DateResume, "widget", "item-1", 0);
            string syntheticMessage = EffectIdentityCodec.ComputeMessageId(syntheticIdentity);
            ITrustedEffectSubmitter effectSubmitter = new HttpTrustedEffectSubmitter(
                new HttpClient(new SyntheticEffectHandler()) { BaseAddress = new Uri("https://example.invalid/") });
            var syntheticResult = await effectSubmitter.SubmitAsync(
                new TrustedEffectSubmission(syntheticIdentity, "CreateWidget", [123, 125],
                    syntheticMessage, syntheticMessage),
                new TrustedEffectContext("synthetic-worker", "synthetic-date-resume",
                    "source-cause", "synthetic-delegation"));
            if (syntheticResult.EffectId != EffectIdentityCodec.ComputeEffectId(syntheticIdentity)
                || syntheticResult.Disposition != TrustedEffectDisposition.NoOp
                || EffectIdentityCodec.Version != 1)
                throw new InvalidOperationException("Trusted effect SDK API changed.");

            var store = new ProbeStore();
            var coordinator = new SharedProjectionEpochCoordinator(store, store);
            var scope = new SharedProjectionScope("statestore", "tenant-a", "widget", "widget-index", ["ordinary"]);
            var lease = await coordinator.RegisterWriterAsync(scope, "ordinary");
            if (await coordinator.JournalAsync(scope, lease, new SharedProjectionDelivery("item-1", 1, [1], [1]))
                != SharedProjectionJournalResult.Journaled)
                throw new InvalidOperationException("Initial delivery was not journaled.");
            if (await coordinator.CatchUpAsync(scope, (_, _, _) =>
                Task.FromResult<IReadOnlyList<ReadModelBatchOperation>>([
                    ReadModelBatchOperation.Write("index", new Counter(1), ReadModelBatchConcurrency.LastWrite)])) != 1)
                throw new InvalidOperationException("Initial catch-up did not drain the journal.");

            if (await coordinator.BeginAsync(scope, "rebuild", "inventory", new Dictionary<string, long> {
                ["item-1"] = 1,
            }) != 1)
                throw new InvalidOperationException("Capture did not advance the epoch.");
            lease = await coordinator.RefreshLeaseAsync(scope, "ordinary");
            if (await coordinator.JournalAsync(scope, lease, new SharedProjectionDelivery("item-1", 2, [2], [2]))
                != SharedProjectionJournalResult.Journaled)
                throw new InvalidOperationException("Post-capture delivery was not journaled.");
            _ = await coordinator.StageAsync(scope, "rebuild", [ReadModelBatchOperation.Write(
                "index", new Counter(1), ReadModelBatchConcurrency.LastWrite)]);
            await coordinator.CommitAsync(scope, "rebuild");
            if (await coordinator.CatchUpAsync(scope, (_, _, _) =>
                Task.FromResult<IReadOnlyList<ReadModelBatchOperation>>([
                    ReadModelBatchOperation.Write("index", new Counter(2), ReadModelBatchConcurrency.LastWrite)])) != 1)
                throw new InvalidOperationException("Committed catch-up did not drain the journal.");
            var selected = await coordinator.ReadAsync<Counter>(scope, "index");
            if (selected.Generation != 1 || selected.IsStale || selected.Value?.Value != 2
                || (await coordinator.GetCheckpointAsync(scope, "item-1"))?.Position != 2)
                throw new InvalidOperationException("The selected generation lost acknowledged delivery.");
            if (await coordinator.JournalAsync(scope, lease, new SharedProjectionDelivery("item-1", 2, [2], [2]))
                != SharedProjectionJournalResult.AlreadyJournaled)
                throw new InvalidOperationException("Identical redelivery was not idempotent.");

            var bounded = new SharedProjectionScope("statestore", "tenant-bound", "widget", "widget-index", ["ordinary"]);
            var boundedLease = await coordinator.RegisterWriterAsync(bounded, "ordinary");
            for (int position = 1; position <= 128; position++) {
                if (await coordinator.JournalAsync(bounded, boundedLease,
                    new SharedProjectionDelivery("stream", position, [(byte)position], [1]))
                    != SharedProjectionJournalResult.Journaled)
                    throw new InvalidOperationException("The bounded journal rejected an in-range position.");
            }
            if (await coordinator.JournalAsync(bounded, boundedLease,
                new SharedProjectionDelivery("stream", 129, [129], [1]))
                != SharedProjectionJournalResult.Backpressure
                || (await coordinator.GetStatusAsync(bounded)).PendingDeliveryCount != 128)
                throw new InvalidOperationException("The bounded journal accepted an over-limit position.");

            record Counter(int Value);

            sealed class SyntheticEffectHandler : HttpMessageHandler {
                protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,
                    CancellationToken cancellationToken) {
                    if (request.Method != HttpMethod.Post
                        || request.RequestUri?.AbsolutePath != "/api/v1/trusted-effects")
                        throw new InvalidOperationException("Trusted effect SDK route changed.");
                    string effectId = EffectIdentityCodec.ComputeEffectId(new EffectIdentity(
                        "tenant-a", "widget", "source-1", 1, EffectKindCatalog.DateResume,
                        "widget", "item-1", 0));
                    return Task.FromResult(new HttpResponseMessage(System.Net.HttpStatusCode.OK) {
                        Content = System.Net.Http.Json.JsonContent.Create(
                            new TrustedEffectResult(effectId, TrustedEffectDisposition.NoOp, false, null)),
                    });
                }
            }

            sealed class ProbeStore : IReadModelStore, IReadModelBatchStore {
                private readonly Dictionary<string, (byte[] Value, string ETag)> _values = new(StringComparer.Ordinal);
                private readonly object _gate = new();
                private long _revision;
                private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

                public Task<ReadModelEntry<T>> GetAsync<T>(string storeName, string key,
                    CancellationToken cancellationToken = default) where T : class {
                    cancellationToken.ThrowIfCancellationRequested();
                    lock (_gate) {
                        return Task.FromResult(_values.TryGetValue(storeName + ":" + key, out var entry)
                            ? new ReadModelEntry<T>(JsonSerializer.Deserialize<T>(entry.Value, Json), entry.ETag)
                            : new ReadModelEntry<T>(null, null));
                    }
                }

                public Task SaveAsync<T>(string storeName, string key, T value,
                    CancellationToken cancellationToken = default) where T : class {
                    cancellationToken.ThrowIfCancellationRequested();
                    lock (_gate) {
                        _values[storeName + ":" + key] = (JsonSerializer.SerializeToUtf8Bytes(value, Json),
                            (++_revision).ToString());
                    }
                    return Task.CompletedTask;
                }

                public Task<bool> TrySaveAsync<T>(string storeName, string key, T value, string etag,
                    CancellationToken cancellationToken = default) where T : class {
                    cancellationToken.ThrowIfCancellationRequested();
                    lock (_gate) {
                        string stateKey = storeName + ":" + key;
                        bool exists = _values.TryGetValue(stateKey, out var prior);
                        if (exists ? prior.ETag != etag : etag.Length != 0)
                            return Task.FromResult(false);
                        _values[stateKey] = (JsonSerializer.SerializeToUtf8Bytes(value, Json),
                            (++_revision).ToString());
                        return Task.FromResult(true);
                    }
                }

                public Task<ReadModelBatchResult> ExecuteAsync(ReadModelBatch batch,
                    CancellationToken cancellationToken = default) {
                    cancellationToken.ThrowIfCancellationRequested();
                    lock (_gate) {
                        foreach (var operation in batch.Operations) {
                            string stateKey = batch.Scope.StoreName + ":" + operation.Key;
                            if (operation.Kind == ReadModelBatchOperationKind.Write)
                                _values[stateKey] = (operation.CanonicalValue.ToArray(), (++_revision).ToString());
                            else
                                _values.Remove(stateKey);
                        }
                    }
                    return Task.FromResult(ReadModelBatchResult.Completed(batch.Scope.ComputeScopeHash()));
                }
            }
            """),
        "Hexalith.EventStore.DomainService": textwrap.dedent("""\
            using Hexalith.EventStore.Contracts.Projections;
            using Hexalith.EventStore.DomainService;
            using Microsoft.Extensions.DependencyInjection;

            var request = new ProjectionRequest("tenant-a", "widget", "item-1", []);
            using var provider = new ServiceCollection().BuildServiceProvider();
            if (DomainProjectionDispatcher.Project(provider, request) is not null)
                throw new InvalidOperationException("An unregistered projection route was admitted.");
            """),
    }
    (consumer_dir / "ConsumerProbe.cs").write_text(
        probes.get(
            package_id,
            "namespace Hexalith.EventStore.PackageConsumer;\n\npublic static class ConsumerProbe { public static void Main() {} }\n",
        ),
        encoding="utf-8",
    )
    return project_path


def run(
    command: list[str],
    cwd: pathlib.Path = ROOT,
    packages_folder: pathlib.Path | None = None,
) -> None:
    """Run one consumer command and preserve its diagnostic output."""

    env = None
    if packages_folder is not None:
        env = {**os.environ, "NUGET_PACKAGES": str(packages_folder)}
    completed = subprocess.run(command, cwd=cwd, check=False, env=env)
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
    """Restore and run one isolated package consumer with no project references."""

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
        if package.package_id in {
            "Hexalith.EventStore.Contracts",
            "Hexalith.EventStore.Client",
            "Hexalith.EventStore.DomainService",
        }:
            run(["dotnet", "run", "--no-build", "--configuration", "Release", "--project", str(project_path)])
        assert_assets_use_packages(project_path, package.package_id, package.version)


def validate_dotnet_tool_package(
    package: PackageMetadata,
    package_path: pathlib.Path,
) -> None:
    """Install one tool package in its own manifest and source boundary."""

    with tempfile.TemporaryDirectory(prefix="eventstore-package-tool-consumer-") as temp_dir_name:
        consumer_dir = pathlib.Path(temp_dir_name)
        config_path = write_nuget_config(consumer_dir, package_path)
        # `dotnet tool install` has no `--packages` switch, so the global packages
        # folder is redirected instead. Without it a cached archive at the same
        # fixed CI version would satisfy the install and the archive under test
        # would never be read.
        packages_folder = consumer_dir / "packages"
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
            packages_folder=packages_folder,
        )


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("package_directory", help="Directory containing manifest-built .nupkg files.")
    args = parser.parse_args()

    package_dir = pathlib.Path(args.package_directory)
    package_path = package_dir if package_dir.is_absolute() else ROOT / package_dir
    packages, version = validate_package_directory(package_path)
    # Route on the manifest tool contract, not on the archive's own <packageTypes>:
    # a library archive that declared DotnetTool would otherwise skip the
    # restore/build proof entirely and only be installed as a tool.
    tool_packages = [package for package in packages if package.package_id in TOOL_PACKAGE_IDS]
    library_packages = [package for package in packages if package.package_id not in TOOL_PACKAGE_IDS]

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
