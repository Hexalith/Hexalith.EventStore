using System.Text;

namespace Hexalith.EventStore.Contracts.Tests.Packaging;

/// <summary>Authenticates the later inherited-owner captures without changing the original inventory seal.</summary>
public sealed partial class ContractsPackageDependencyTests
{
    private const string InheritedOwnerMsBuildInventorySha256 =
        "167bd2d4402c2a6bb8fa37d7f417ff2074ccdff8b52bd263e3c7b75523cdd927";

    private static readonly string[] InheritedOwnerMsBuildPaths =
    [
        "_bmad-output/implementation-artifacts/evidence/folders-12-2-inherited-owner-20261022/tiny-repro/Corrected.csproj",
        "_bmad-output/implementation-artifacts/evidence/folders-12-2-inherited-owner-20261022/tiny-repro/Diagnostic.csproj",
    ];

    /// <summary>Requires exact raw bytes and membership for both later reproduction projects.</summary>
    [Fact]
    public void InheritedOwnerFrozenMsBuildInventory_AuthenticatesOriginalCaptures()
        => VerifyFrozenMsBuildInventory(FindRepositoryRoot(), InheritedOwnerMsBuildPaths, 2, InheritedOwnerMsBuildInventorySha256);

    /// <summary>Rejects mutations instead of extending an evidence exemption to changed files.</summary>
    /// <param name="mutation">The byte, membership or link substitution to reject.</param>
    [Theory]
    [InlineData("changed")]
    [InlineData("added")]
    [InlineData("removed")]
    [InlineData("renamed")]
    [InlineData("symlink")]
    public void InheritedOwnerFrozenMsBuildInventory_RejectsChanges(string mutation)
    {
        string repository = FindRepositoryRoot();
        string root = Path.Combine(Path.GetTempPath(), "inherited-owner-evidence-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            foreach (string path in InheritedOwnerMsBuildPaths)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(Path.Combine(root, path))!);
                File.Copy(Path.Combine(repository, path), Path.Combine(root, path));
            }

            VerifyFrozenMsBuildInventory(root, InheritedOwnerMsBuildPaths, 2, InheritedOwnerMsBuildInventorySha256);
            string[] paths = [.. InheritedOwnerMsBuildPaths];
            string first = Path.Combine(root, paths[0]);
            switch (mutation)
            {
                case "changed":
                    File.AppendAllText(first, "\n<!-- changed -->", Encoding.UTF8);
                    break;
                case "added":
                    File.Copy(first, first + ".props");
                    paths = [.. paths, paths[0] + ".props"];
                    break;
                case "removed":
                    paths = [paths[1]];
                    break;
                case "renamed":
                    File.Move(first, first + ".props");
                    paths = [paths[0] + ".props", paths[1]];
                    break;
                case "symlink":
                    string external = Path.Combine(root, "original.props");
                    File.Copy(first, external);
                    File.Delete(first);
                    File.CreateSymbolicLink(first, external);
                    break;
            }

            Should.Throw<Shouldly.ShouldAssertException>(() => VerifyFrozenMsBuildInventory(root, paths, 2, InheritedOwnerMsBuildInventorySha256));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }
}
