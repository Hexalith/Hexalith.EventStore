namespace Hexalith.EventStore.Admin.UI.Tests;

/// <summary>
/// Pins published Admin discovery and callable-inventory copy to the executable surface.
/// </summary>
public class AdminSurfaceDocumentationTests
{
    [Fact]
    public void ConfigurationReference_DocumentsDevelopmentOnlyAdminDiscovery()
    {
        string catalog = ReadDoc("docs", "guides", "configuration-reference.md");

        catalog.ShouldContain("`EventStore:Admin:OpenApi`");
        catalog.ShouldContain("| `Enabled` | bool | `false` |");
        catalog.ShouldContain("Production never maps those routes");
        catalog.ShouldContain("`EventStore:Admin:OpenApi:Enabled`");
        catalog.ShouldNotContain("| `EventStore:Admin:OpenApi:Enabled` | bool | `true` |");
    }

    [Fact]
    public void PublishedAdminInventory_MatchesCallableSurface()
    {
        string contracts = ReadDoc("docs", "brownfield", "api-contracts.md");
        string inventory = ReadDoc("docs", "brownfield", "component-inventory.md");
        string overview = ReadDoc("docs", "brownfield", "project-overview.md");

        contracts.ShouldContain("`EventStore:Admin:OpenApi:Enabled`");
        contracts.ShouldContain("Development");
        inventory.ShouldContain("`backup-trigger`");
        inventory.ShouldContain("does not prove execution");
        inventory.ShouldContain("`projection-pause`");
        inventory.ShouldContain("`projection-resume`");
        inventory.ShouldContain("`projection-reset`");
        inventory.ShouldContain("`projection-replay`");
        inventory.ShouldContain("`consistency-trigger`");
        inventory.ShouldContain("`consistency-cancel`");
        inventory.ShouldContain("confirm=true");
        inventory.ShouldContain("ExitCodes.Error");
        inventory.ShouldContain("ConfirmationFacts");
        inventory.ShouldContain("InitiatorFocusService");
        inventory.ShouldNotContain("`export-stream`");
        inventory.ShouldNotContain("`import-stream`");
        overview.ShouldContain("seven confirmation-gated writes");
        overview.ShouldContain("explicitly unavailable");
    }

    private static string ReadDoc(params string[] relativePath)
        => File.ReadAllText(Path.Combine([Dw5TestPaths.RepoRoot(), .. relativePath]));
}
