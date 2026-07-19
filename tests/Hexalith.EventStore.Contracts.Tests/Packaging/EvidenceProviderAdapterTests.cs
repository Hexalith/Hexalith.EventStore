using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Hexalith.EventStore.Contracts.Tests.Packaging;

/// <summary>
/// Verifies the Story 1.20 evidence-provider adapter is executable and fail closed.
/// </summary>
public sealed class EvidenceProviderAdapterTests
{
    private const string AdapterRelativePath = "tools/evidence-provider-adapters/azure-immutable-blob-v1.sh";

    /// <summary>
    /// Verifies authenticated version-bound download and locked immutability proof generation.
    /// </summary>
    [Fact]
    public async Task AzureAdapterBindsTheExactVersionHashAndLockedRetention()
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        string root = FindRepositoryRoot();
        string adapter = Path.Combine(root, AdapterRelativePath);
        File.Exists(adapter).ShouldBeTrue();
        File.GetUnixFileMode(adapter).HasFlag(UnixFileMode.UserExecute).ShouldBeTrue();

        string temporary = Directory.CreateTempSubdirectory("eventstore-evidence-adapter-").FullName;
        try
        {
            string payload = Path.Combine(temporary, "payload.tar.gz");
            await File.WriteAllTextAsync(payload, "immutable evidence", Encoding.UTF8);
            string payloadHash = Convert.ToHexStringLower(SHA256.HashData(await File.ReadAllBytesAsync(payload)));
            string fakeAz = Path.Combine(temporary, "az");
            string fakeCurl = Path.Combine(temporary, "curl");
            string downloaded = Path.Combine(temporary, "downloaded.tar.gz");
            string proof = Path.Combine(temporary, "proof.json");

            await File.WriteAllTextAsync(
                fakeAz,
                $$$"""
                #!/usr/bin/env bash
                set -euo pipefail
                test "$1 $2" = 'account get-access-token'
                printf '%s\n' 'contract-test-token'
                """.Replace("\r\n", "\n", StringComparison.Ordinal));
            File.SetUnixFileMode(fakeAz, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
            await File.WriteAllTextAsync(
                fakeCurl,
                $$$"""
                #!/usr/bin/env bash
                set -euo pipefail
                value_after() {
                  local wanted="$1"
                  shift
                  while test "$#" -gt 0; do
                    if test "$1" = "$wanted"; then printf '%s\n' "$2"; return; fi
                    shift
                  done
                  return 1
                }
                test "$(value_after --header "$@" | head -n 1)" = 'Authorization: Bearer contract-test-token'
                [[ "${!#}" == *'versionid=2026-07-19T20%3A00%3A00.0000000Z' ]]
                output="$(value_after --output "$@")"
                if [[ " $* " == *' --head '* ]]; then
                  headers="$(value_after --dump-header "$@")"
                  printf '%s\r\n' \
                    'HTTP/1.1 200 OK' \
                    'x-ms-version-id: 2026-07-19T20:00:00.0000000Z' \
                    'x-ms-meta-sha256: {{{payloadHash}}}' \
                    'x-ms-immutability-policy-until-date: Wed, 20 Jul 2033 20:00:00 GMT' \
                    'x-ms-immutability-policy-mode: Locked' \
                    '' > "$headers"
                else
                  cp -- '{{{payload}}}' "$output"
                fi
                """.Replace("\r\n", "\n", StringComparison.Ordinal));
            File.SetUnixFileMode(fakeCurl, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);

            await RunAsync(adapter, temporary, "download", downloaded);
            File.ReadAllBytes(downloaded).ShouldBe(await File.ReadAllBytesAsync(payload));

            await RunAsync(adapter, temporary, "describe", proof);
            using JsonDocument document = JsonDocument.Parse(await File.ReadAllTextAsync(proof));
            JsonElement rootElement = document.RootElement;
            rootElement.GetProperty("provider").GetProperty("authenticated_api").GetBoolean().ShouldBeTrue();
            rootElement.GetProperty("object").GetProperty("sha256").GetString().ShouldBe(payloadHash);
            rootElement.GetProperty("object").GetProperty("version").GetString()
                .ShouldBe("2026-07-19T20:00:00.0000000Z");
            rootElement.GetProperty("policy").GetProperty("locked").GetBoolean().ShouldBeTrue();
            rootElement.GetProperty("policy").GetProperty("retention_until").GetString()
                .ShouldBe("2033-07-20T20:00:00Z");
        }
        finally
        {
            Directory.Delete(temporary, recursive: true);
        }
    }

    private static async Task RunAsync(string adapter, string path, string command, string output)
    {
        ProcessStartInfo start = new("bash")
        {
            RedirectStandardError = true,
            RedirectStandardOutput = true,
            UseShellExecute = false,
        };
        start.Environment["PATH"] = $"{path}:{Environment.GetEnvironmentVariable("PATH")}";
        start.ArgumentList.Add(adapter);
        start.ArgumentList.Add(command);
        start.ArgumentList.Add("--object-url");
        start.ArgumentList.Add("https://storyproof.blob.core.windows.net/evidence/story-1-20.tar.gz");
        start.ArgumentList.Add("--object-version");
        start.ArgumentList.Add("2026-07-19T20:00:00.0000000Z");
        start.ArgumentList.Add("--output");
        start.ArgumentList.Add(output);

        using Process process = Process.Start(start).ShouldNotBeNull();
        string error = await process.StandardError.ReadToEndAsync();
        string standardOutput = await process.StandardOutput.ReadToEndAsync();
        await process.WaitForExitAsync();
        process.ExitCode.ShouldBe(0, $"adapter failed: {error}\n{standardOutput}");
    }

    private static string FindRepositoryRoot()
    {
        foreach (string startPath in new[] { Directory.GetCurrentDirectory(), AppContext.BaseDirectory }.Distinct())
        {
            for (DirectoryInfo? directory = new(startPath); directory is not null; directory = directory.Parent)
            {
                if (File.Exists(Path.Combine(directory.FullName, "AGENTS.md"))
                    && Directory.Exists(Path.Combine(directory.FullName, "src", "Hexalith.EventStore.Contracts")))
                {
                    return directory.FullName;
                }
            }
        }

        throw new DirectoryNotFoundException("Could not locate repository root.");
    }
}
