using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Hexalith.EventStore.Contracts.Tests.Events;

/// <summary>Checks the current-amendment gate accounts for historical obligations without conferring activation.</summary>
public sealed class EventEvolutionObligationAuditTests
{
    /// <summary>Runs the CI gate and independently checks its complete open obligation and timed refusal results.</summary>
    [Fact]
    public async Task CurrentAmendmentGateKeepsEveryObligationOpenAndKillsPolicyMutants()
    {
        using JsonDocument result = await RunGateAsync(FindRepositoryRoot(), "--mutations");
        JsonElement root = result.RootElement;
        root.GetProperty("result").GetString().ShouldBe("passed");
        root.GetProperty("v2_writes").GetString().ShouldBe("fenced");
        JsonElement audit = root.GetProperty("obligation_audit");
        audit.GetProperty("activation_authority").GetBoolean().ShouldBeFalse();
        audit.GetProperty("historical_approval_sha256").GetString()
            .ShouldBe("bc1625e3b8147fb0bc2cd9491fad8379a95c1ce0b597eba70f8d29f2fee3b050");
        audit.GetProperty("open_obligations").EnumerateArray().Select(item => item.GetString())
            .ShouldBe(Enumerable.Range(1, 20).Select(index => $"O-{index:00}"));
        audit.GetProperty("replaced_provider_requirements").EnumerateArray().Select(item => item.GetString())
            .ShouldBe(["O-10", "O-13", "O-14", "O-17", "O-18", "O-20"]);
        audit.GetProperty("current_amendment_sha256").EnumerateObject().Select(item => item.Name).Order()
            .ShouldBe(new[]
            {
                "_bmad-output/implementation-artifacts/story-6-6-dapr-only-amendment.md",
                "_bmad-output/implementation-artifacts/story-6-6-trusted-code-amendment.md",
            }.Order());

        JsonElement[] mutations = root.GetProperty("mutations").EnumerateArray().ToArray();
        mutations.Select(item => item.GetProperty("mutation").GetString()).ShouldBe(new[]
        {
            "application-sql", "application-sql-factory", "server-npgsql", "catalog-npgsql", "persister-save",
            "v2-admission", "v2-comment-spoof", "v2-block-comment-spoof", "v2-string-spoof",
            "v2-disabled-branch", "v2-preprocessor-spoof", "historical-approval-pin", "historical-reviewed-input",
            "missing-dapr-amendment", "changed-trusted-code-amendment", "missing-obligation", "duplicate-obligation",
            "changed-obligation-source", "superseded-provider-assurance", "unavailable-proof-enabled",
            "premature-obligation-closure", "historical-approval-as-current",
        });
        foreach (JsonElement mutation in mutations)
        {
            mutation.GetProperty("result").GetString().ShouldBe("rejected");
            mutation.GetProperty("timeout_seconds").GetInt32().ShouldBe(10);
        }
    }

    /// <summary>Proves missing or changed inputs refuse while unrelated files and Git metadata remain outside focused binding.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task FocusedInputBindingAllowsUnrelatedChangesAndRefusesMissingOrChangedAmendments(bool removeAmendment)
    {
        string repository = FindRepositoryRoot();
        string temporary = Path.Combine(Path.GetTempPath(), $"eventstore-evolution-audit-{Guid.NewGuid():N}");
        try
        {
            CopyGateInputs(repository, temporary);

            string gitlink = Path.Combine(temporary, "references/Hexalith.Builds/.git");
            await File.WriteAllTextAsync(gitlink, "gitdir: unrelated-new-gitlink\n");
            await File.WriteAllTextAsync(Path.Combine(temporary, "unrelated-owner-note.md"), "Owner work stays editable.\n");
            using JsonDocument positive = await RunGateAsync(temporary);
            positive.RootElement.GetProperty("result").GetString().ShouldBe("passed");

            string amendment = Path.Combine(temporary, "_bmad-output/implementation-artifacts/story-6-6-trusted-code-amendment.md");
            if (removeAmendment) { File.Delete(amendment); }
            else
            {
                string source = await File.ReadAllTextAsync(amendment);
                await File.WriteAllTextAsync(amendment, source.Replace("\n", "\r\n", StringComparison.Ordinal));
            }
            using JsonDocument negative = await RunGateAsync(temporary, expectedExitCode: 2);
            negative.RootElement.GetProperty("check").GetString().ShouldBe("current-amendment-binding");
        }
        finally
        {
            if (Directory.Exists(temporary)) { Directory.Delete(temporary, recursive: true); }
        }
    }

    /// <summary>Checks malformed audit objects return a typed refusal instead of accepting incomplete qualification claims.</summary>
    [Theory]
    [InlineData("root-array", "current-obligation-accounting")]
    [InlineData("boolean-version", "current-obligation-accounting")]
    [InlineData("approval-array", "historical-approval-binding")]
    [InlineData("amendment-object", "current-amendment-binding")]
    [InlineData("amendment-null", "current-amendment-binding")]
    [InlineData("obligations-object", "current-obligation-accounting")]
    [InlineData("obligation-null", "current-obligation-accounting")]
    [InlineData("evidence-null", "current-obligation-disposition")]
    public async Task MalformedAuditSchemaRefusesAtItsOwningBoundary(string malformed, string expectedCheck)
    {
        string temporary = Path.Combine(Path.GetTempPath(), $"eventstore-evolution-audit-schema-{Guid.NewGuid():N}");
        try
        {
            CopyGateInputs(FindRepositoryRoot(), temporary);
            string path = Path.Combine(temporary, "_bmad-output/implementation-artifacts/6-6-obligation-audit.json");
            JsonNode audit = JsonNode.Parse(await File.ReadAllTextAsync(path))!;
            switch (malformed)
            {
                case "root-array": audit = new JsonArray(); break;
                case "boolean-version": audit["schemaVersion"] = true; break;
                case "approval-array": audit["historicalApproval"] = new JsonArray(); break;
                case "amendment-object": audit["currentAmendments"] = new JsonObject(); break;
                case "amendment-null": audit["currentAmendments"]![0] = null; break;
                case "obligations-object": audit["obligations"] = new JsonObject(); break;
                case "obligation-null": audit["obligations"]![0] = null; break;
                case "evidence-null": audit["obligations"]![0]!["requiredEvidence"] = null; break;
                default: throw new ArgumentOutOfRangeException(nameof(malformed));
            }

            await File.WriteAllTextAsync(path, audit.ToJsonString());
            using JsonDocument result = await RunGateAsync(temporary, expectedExitCode: 2);
            result.RootElement.GetProperty("result").GetString().ShouldBe("failed");
            result.RootElement.GetProperty("check").GetString().ShouldBe(expectedCheck);
        }
        finally
        {
            if (Directory.Exists(temporary)) { Directory.Delete(temporary, recursive: true); }
        }
    }

    private static void CopyGateInputs(string repository, string temporary)
    {
        // Copy only the gate's actual inputs. No whole-tree inventory, Git history,
        // project build output or nested submodule is needed for this audit.
        foreach (string relative in new[]
        {
            "scripts/verify-event-evolution.py",
            "src/Hexalith.EventStore.Server/Hexalith.EventStore.Server.csproj",
            "src/Hexalith.EventStore.Server/Events/EventPersister.cs",
            "references/Hexalith.Builds/Props/Directory.Packages.props",
            "_bmad-output/implementation-artifacts/6-6-obligation-audit.json",
            "_bmad-output/implementation-artifacts/spec-event-versioning-upcasting.md",
            "_bmad-output/implementation-artifacts/story-6-6-dapr-only-amendment.md",
            "_bmad-output/implementation-artifacts/story-6-6-trusted-code-amendment.md",
        })
        {
            string target = Path.Combine(temporary, relative);
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            File.Copy(Path.Combine(repository, relative), target);
        }
    }

    private static async Task<JsonDocument> RunGateAsync(string repository, string? argument = null, int expectedExitCode = 0)
    {
        var start = new ProcessStartInfo("python3")
        {
            WorkingDirectory = repository,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        start.ArgumentList.Add(Path.Combine(repository, "scripts/verify-event-evolution.py"));
        if (argument is not null) { start.ArgumentList.Add(argument); }
        using Process process = Process.Start(start)!;
        Task<string> output = process.StandardOutput.ReadToEndAsync();
        Task<string> error = process.StandardError.ReadToEndAsync();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(40));
        try { await process.WaitForExitAsync(timeout.Token); }
        catch (OperationCanceledException)
        {
            process.Kill(entireProcessTree: true);
            throw;
        }

        process.ExitCode.ShouldBe(expectedExitCode, await error);
        return JsonDocument.Parse(await output);
    }

    private static string FindRepositoryRoot()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "Hexalith.EventStore.slnx")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Cannot find the EventStore repository for current-amendment verification.");
    }
}
