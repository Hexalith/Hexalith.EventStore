using System.Security.Cryptography;
using System.Text.Json;

using Hexalith.EventStore.Aspire;
using Hexalith.EventStore.Client.Routing;
using Hexalith.EventStore.Contracts.Routing;
using Hexalith.EventStore.ServiceDefaults.Routing;

namespace Hexalith.EventStore.AppHost.Tests.Configuration;

/// <summary>File custody controls are separate from the real authority activation and native Aspire proofs.</summary>
public sealed class DevelopmentRouteCatalogCustodyTests
{
    [Theory]
    [InlineData("valid")]
    [InlineData("key-material")]
    [InlineData("active-key")]
    [InlineData("authority-cell")]
    [InlineData("parallel-owner")]
    [InlineData("permissions")]
    public void OwnerProfileNeverReplacesRetainedMaterialOrSharesAnActiveOwner(string scenario)
    {
        string directory = Directory.CreateTempSubdirectory("catalog-custody-test-").FullName;
        var options = Options(Path.Combine(directory, "custody"));
        try
        {
            using (var first = new DevelopmentRouteCatalogCustody(options))
            {
                if (scenario == "parallel-owner") Should.Throw<IOException>(() => new DevelopmentRouteCatalogCustody(options));
            }
            byte[] original = File.ReadAllBytes(Path.Combine(options.CustodyDirectory, "owner.json"));
            var next = scenario switch
            {
                "key-material" => options with { DigestKeys = new Dictionary<string, string> { ["story415"] = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)) } },
                "active-key" => options with { ActiveDigestKeyGeneration = "new-key" },
                "authority-cell" => options with { AuthorityStateKey = "different-cell" },
                _ => options,
            };
            if (scenario == "permissions" && !OperatingSystem.IsWindows())
                File.SetUnixFileMode(Path.Combine(options.CustodyDirectory, "owner.json"), UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.GroupRead);
            if (scenario is "key-material" or "active-key" or "authority-cell" || scenario == "permissions" && !OperatingSystem.IsWindows())
                Should.Throw<InvalidOperationException>(() => new DevelopmentRouteCatalogCustody(next));
            else { using var resumed = new DevelopmentRouteCatalogCustody(next); resumed.PredecessorBytes.ShouldBeNull(); }
            File.ReadAllBytes(Path.Combine(options.CustodyDirectory, "owner.json")).ShouldBe(original);
        }
        finally { Directory.Delete(directory, true); }
    }

    [Theory]
    [InlineData("valid")]
    [InlineData("changed-file")]
    [InlineData("missing-file")]
    [InlineData("extra-file")]
    [InlineData("missing-commit")]
    public void StoppedGenerationRetainsCompletePrivateInputsAndRefusesIncompleteOrChangedCustody(string scenario)
    {
        string directory = Directory.CreateTempSubdirectory("catalog-custody-files-").FullName;
        var options = Options(Path.Combine(directory, "custody"));
        string run = Path.Combine(directory, "run");
        if (OperatingSystem.IsWindows()) Directory.CreateDirectory(run);
        else Directory.CreateDirectory(run, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        try
        {
            var route = DevelopmentCatalogProbeDeclarations.Routes.Single();
            var operation = DevelopmentCatalogProbeDeclarations.Operations.Single();
            var catalog = VersionedRouteCatalogBuilder.Build(new(1, "Development", [route], [operation],
                [new(route.RouteId, "domain", "process", "v1", false, "*")],
                [DevelopmentIdempotencyBindings.Bind(DevelopmentCatalogProbeDeclarations.Commands.Single(), 1, "domain")]));
            string root = RouteCatalogCodec.ComputeRootDigest(catalog);
            var host = new RouteCatalogHostIdentity("physical-domain", "domain", new string('a', 64), new string('b', 64), new string('c', 64));
            var activation = new RouteCatalogActivation("owned-attempt", RouteCatalogCodec.Encode(catalog), root, null, [host], "Development-fixture", "");
            activation = activation with { TrustProof = RouteCatalogActivationBinding.ComputeDigest(activation) };
            var readback = new RouteCatalogGenerationReadback(1, 1, root, catalog.RouteFacetDigest, catalog.IdempotencyFacetDigest, "Development", [host], true);
            DevelopmentRouteCatalogCustody.WriteNew(Path.Combine(run, "catalog.json"), activation.CanonicalCatalog);
            DevelopmentRouteCatalogCustody.WriteNew(Path.Combine(run, "activation.json"), JsonSerializer.SerializeToUtf8Bytes(activation));
            DevelopmentRouteCatalogCustody.WriteNew(Path.Combine(run, "authority.backend.json"), "private-effective-backend"u8.ToArray());
            DevelopmentRouteCatalogCustody.WriteNew(Path.Combine(run, "authority.component.json"), "actual-rendered-component"u8.ToArray());
            DevelopmentRouteCatalogCustody.WriteNew(Path.Combine(run, "authority.dependencies.json"), "actual-private-backend-dependencies"u8.ToArray());
            if (scenario != "missing-commit") DevelopmentRouteCatalogCustody.WriteNew(Path.Combine(run, host.HostId + ".committed.json"), JsonSerializer.SerializeToUtf8Bytes(readback));
            using (var custody = new DevelopmentRouteCatalogCustody(options))
            {
                if (scenario == "missing-commit")
                {
                    Should.Throw<InvalidOperationException>(() => custody.Retain(run));
                    File.Exists(Path.Combine(options.CustodyDirectory, "current.json")).ShouldBeFalse();
                    File.Exists(Path.Combine(run, "catalog.json")).ShouldBeTrue();
                    return;
                }
                custody.Retain(run);
            }
            string retained = Directory.GetDirectories(options.CustodyDirectory, "generation-*").Single();
            if (scenario == "changed-file") File.WriteAllText(Path.Combine(retained, "authority.dependencies.json"), "changed-private-backend-value");
            if (scenario == "missing-file") File.Delete(Path.Combine(retained, "authority.component.json"));
            if (scenario == "extra-file") DevelopmentRouteCatalogCustody.WriteNew(Path.Combine(retained, "unreviewed.json"), "extra"u8.ToArray());
            if (scenario != "valid") Should.Throw<InvalidOperationException>(() => new DevelopmentRouteCatalogCustody(options));
            else
            {
                using var restored = new DevelopmentRouteCatalogCustody(options);
                restored.PredecessorBytes.ShouldBe(activation.CanonicalCatalog); restored.PredecessorRootDigest.ShouldBe(root);
                restored.BackendBytes.ShouldBe("private-effective-backend"u8.ToArray());
                if (!OperatingSystem.IsWindows())
                    foreach (string file in Directory.GetFiles(retained)) File.GetUnixFileMode(file).ShouldBe(UnixFileMode.UserRead | UnixFileMode.UserWrite);
            }
        }
        finally { Directory.Delete(directory, true); }
    }

    private static DevelopmentRouteCatalogCompositionOptions Options(string directory) => new(directory, "routing-owner", "owner-cell", "story415",
        new Dictionary<string, string> { ["story415"] = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)) }, "story415");
}
