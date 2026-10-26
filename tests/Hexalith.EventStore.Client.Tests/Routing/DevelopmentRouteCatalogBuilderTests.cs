using System.Diagnostics;
using System.Text;
using System.Text.Json;

using Hexalith.EventStore.Client.Routing;
using Hexalith.EventStore.Contracts.Routing;
using Hexalith.EventStore.Queries;

using Shouldly;

namespace Hexalith.EventStore.Client.Tests.Routing;

public class DevelopmentRouteCatalogBuilderTests
{
    [Fact]
    public void ExplicitFixturesBuildTheGoldenCatalogRepeatedly()
    {
        DevelopmentRouteCatalogInput input = ReadFixture();
        byte[] expected = File.ReadAllBytes(FixturePath("CanonicalCatalog.json"));
        RouteCatalogCodec.Encode(DevelopmentRouteCatalogBuilder.Build(input)).ShouldBe(expected);
        RouteCatalogCodec.Encode(DevelopmentRouteCatalogBuilder.Build(input)).ShouldBe(expected);
        RouteCatalogEnvelope result = DevelopmentRouteCatalogBuilder.Build(input);
        result.Routes.Single(r => r.RouteId == "counter-get").Method.ShouldBe(DaprDomainQueryInvoker.QueryMethodName);
        result.Routes.Single(r => r.RouteId == "counter-create").Method.ShouldBe("process");
    }

    [Theory]
    [InlineData("missing-idempotency")]
    [InlineData("missing-binding")]
    [InlineData("missing-declaration")]
    [InlineData("duplicate-declaration")]
    [InlineData("duplicate-binding")]
    [InlineData("missing-version")]
    [InlineData("null-declaration")]
    public void MissingOrAmbiguousInputsCannotProduceAnEnvelope(string scenario)
    {
        DevelopmentRouteCatalogInput valid = ReadFixture();
        DevelopmentRouteCatalogBuilder.Build(valid);
        DevelopmentRouteCatalogInput invalid = scenario switch
        {
            "missing-idempotency" => valid with { Idempotency = [] },
            "missing-binding" => valid with { Bindings = [] },
            "missing-declaration" => valid with { Declarations = [] },
            "duplicate-declaration" => valid with { Declarations = [.. valid.Declarations, valid.Declarations[0]] },
            "duplicate-binding" => valid with { Bindings = [.. valid.Bindings, valid.Bindings[0]] },
            "missing-version" => valid with { Declarations = [valid.Declarations[0] with { MessageIdVersion = null }, .. valid.Declarations.Skip(1)] },
            _ => valid with { Declarations = [null!] },
        };
        Should.Throw<RouteCatalogValidationException>(() => DevelopmentRouteCatalogBuilder.Build(invalid));
    }

    [Fact]
    public void ClosedConstructionInputRejectsSecretFieldsDuplicateFieldsAndMissingBooleans()
    {
        string input = File.ReadAllText(FixturePath("CompleteInput.json"));
        DevelopmentRouteCatalogBuilder.ReadInput(Encoding.UTF8.GetBytes(input));
        foreach (string invalid in new[]
        {
            input.Insert(1, "\"secretValue\":\"never-print-this\","),
            input.Insert(1, "\"generation\":7,"),
            input.Replace("\"allowRuntimeOverrides\": false,", "", StringComparison.Ordinal),
        })
        {
            Should.Throw<RouteCatalogValidationException>(() => DevelopmentRouteCatalogBuilder.ReadInput(Encoding.UTF8.GetBytes(invalid)))
                .Message.ShouldNotContain("never-print-this");
        }
    }

    [Fact]
    public void RealDevelopmentConfigurationReportsAllFourBindingsAndExplicitInventoryLimits()
    {
        DevelopmentCatalogInspection report = InspectRealConfiguration();
        report.Bindings.Count.ShouldBe(4);
        report.Bindings.Select(b => (b.Domain, b.AppId, b.TenantPattern)).ShouldBe(new[]
        {
            ("counter", "sample", "*"), ("greeting", "sample", "*"),
            ("global-administrators", "tenants", "system"), ("tenants", "tenants", "system"),
        });
        report.Bindings.ShouldAllBe(b => b.CommandMethod == "process" && b.QueryMethod == DaprDomainQueryInvoker.QueryMethodName && b.ContractVersion == "v1");
        report.MissingInputs.Count.ShouldBe(52);
        report.MissingInputs.Count(d => d.Code == "idempotency-adapter-and-operation").ShouldBe(4);
        report.MissingInputs.Count(d => d.Code == "platform-namespace-and-exact-human-permission").ShouldBe(2);
        report.InventoryLimit.ShouldContain("runtime parity remains unverified");
        report.InventoryLimit.ShouldContain("No environment");
        report.InventoryLimit.ShouldContain("adapter registrations");
    }

    [Fact]
    public void InspectionMergesSafeFieldsAndNeverSerializesKeyMaterial()
    {
        string baseLayer = """
            {"EventStore":{"DomainServices":{"Registrations":{"*|counter|v1":{"TenantId":"*","Domain":"counter","AppId":"old","MethodName":"custom-process","Version":"v1"}}},"Idempotency":{"DigestKeys":{"g1":"never-print-this"},"SecretValue":"never-print-this"}}}
            """;
        string overlay = """
            {"EventStore":{"DomainServices":{"Registrations":{"*|counter|v1":{"AppId":"sample"}}}}}
            """;
        DevelopmentCatalogInspection report = DevelopmentRouteCatalogBuilder.Inspect([Encoding.UTF8.GetBytes(baseLayer), Encoding.UTF8.GetBytes(overlay)]);
        report.Bindings.Single().AppId.ShouldBe("sample");
        report.Bindings.Single().CommandMethod.ShouldBe("custom-process");
        report.Bindings.Single().QueryMethod.ShouldBe("query");
        JsonSerializer.Serialize(report).ShouldNotContain("never-print-this");
    }

    [Fact]
    public async Task FileToolGeneratesAndValidatesFixturesAndRefusesIncompleteInputs()
    {
        string directory = Path.Combine(Path.GetTempPath(), "route-catalog-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            string output = Path.Combine(directory, "catalog.json");
            (int exit, string log) = await RunToolAsync("generate", FixturePath("CompleteInput.json"), output);
            exit.ShouldBe(0, log);
            byte[] expected = File.ReadAllBytes(FixturePath("CanonicalCatalog.json"));
            File.ReadAllBytes(output).ShouldBe(expected);
            string root = RouteCatalogCodec.ComputeRootDigest(RouteCatalogCodec.Decode(expected));
            log.ShouldContain(root);
            (exit, log) = await RunToolAsync("validate", output, root);
            exit.ShouldBe(0, log);
            (exit, log) = await RunToolAsync("validate", output, new string('0', 64));
            exit.ShouldBe(2, log);
            log.ShouldContain("root-digest-mismatch");

            string incomplete = Path.Combine(directory, "incomplete.json");
            File.WriteAllText(incomplete, JsonSerializer.Serialize(ReadFixture() with { Idempotency = [] }, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase }));
            string absent = Path.Combine(directory, "must-not-exist.json");
            (exit, log) = await RunToolAsync("generate", incomplete, absent);
            exit.ShouldBe(2, log);
            File.Exists(absent).ShouldBeFalse();
            (exit, log) = await RunToolAsync("generate", incomplete, output);
            exit.ShouldBe(2, log);
            File.ReadAllBytes(output).ShouldBe(expected);

            (exit, log) = await RunToolAsync("inspect", SettingsPath("appsettings.json"), SettingsPath("appsettings.Development.json"));
            exit.ShouldBe(2, log);
            log.ShouldContain("idempotency-adapter-and-operation");
            log.ShouldContain("runtime parity remains unverified");
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    private static DevelopmentRouteCatalogInput ReadFixture() => DevelopmentRouteCatalogBuilder.ReadInput(File.ReadAllBytes(FixturePath("CompleteInput.json")));

    private static DevelopmentCatalogInspection InspectRealConfiguration() => DevelopmentRouteCatalogBuilder.Inspect(
        [File.ReadAllBytes(SettingsPath("appsettings.json")), File.ReadAllBytes(SettingsPath("appsettings.Development.json"))]);

    private static string FixturePath(string name) => Path.Combine(FindRoot(), "tests", "Hexalith.EventStore.Contracts.Tests", "Routing", "Fixtures", name);

    private static string SettingsPath(string name) => Path.Combine(FindRoot(), "src", "Hexalith.EventStore", name);

    private static string FindRoot()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Hexalith.EventStore.slnx")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? throw new InvalidOperationException("Repository root unavailable.");
    }

    private static async Task<(int Exit, string Log)> RunToolAsync(params string[] arguments)
    {
        var start = new ProcessStartInfo("dotnet") { WorkingDirectory = FindRoot(), RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false };
        foreach (string argument in new[] { "run", "--file", "tools/routing-catalog.cs", "--no-launch-profile", "-v", "quiet" })
        {
            start.ArgumentList.Add(argument);
        }

#if DEBUG
        start.ArgumentList.Add("-c=Debug");
        start.ArgumentList.Add("-p:UseHexalithProjectReferences=true");
#else
        start.ArgumentList.Add("-c=Release");
        start.ArgumentList.Add("-p:UseHexalithProjectReferences=false");
#endif
        start.ArgumentList.Add("--");
        foreach (string argument in arguments)
        {
            start.ArgumentList.Add(argument);
        }

        using Process process = Process.Start(start)!;
        Task<string> stdout = process.StandardOutput.ReadToEndAsync();
        Task<string> stderr = process.StandardError.ReadToEndAsync();
        using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(3));
        try
        {
            await process.WaitForExitAsync(timeout.Token);
        }
        catch (OperationCanceledException)
        {
            process.Kill(entireProcessTree: true);
            throw;
        }

        return (process.ExitCode, await stdout + await stderr);
    }
}
