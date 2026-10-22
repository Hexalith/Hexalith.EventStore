namespace Hexalith.EventStore.Contracts.Tests.Packaging;

/// <summary>Guards the mandatory Release package consumer proof and its named public seams.</summary>
public sealed class ProcessPackageContractTests
{
    /// <summary>Ensures CI runs the checked-in Release inventory proof without an optional switch.</summary>
    [Fact]
    public async Task RequiredReleasePackageGateIncludesHostedProcessExecution()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "tools", "release-packages.json")))
        {
            directory = directory.Parent;
        }

        string root = directory?.FullName ?? throw new InvalidOperationException("EventStore repository root was not found.");
        string workflow = await File.ReadAllTextAsync(Path.Combine(root, ".github", "workflows", "ci.yml"), TestContext.Current.CancellationToken);
        string gate = await File.ReadAllTextAsync(Path.Combine(root, "scripts", "verify-process-packages.py"), TestContext.Current.CancellationToken);
        string consumers = await File.ReadAllTextAsync(Path.Combine(root, "scripts", "validate-consumer-package-references.py"), TestContext.Current.CancellationToken);

        workflow.ShouldContain("python3 scripts/verify-process-packages.py");
        gate.ShouldContain("scripts/pack-release-packages.py");
        gate.ShouldContain("Hexalith.EventStore.DomainService");
        consumers.ShouldContain("AddEventStoreProcesses<SyntheticProcessCatalog, SyntheticProcessTranslator>");
        consumers.ShouldContain("await processHost.StartAsync");
        consumers.ShouldContain("Hosted checkpointed process did not advance");
        consumers.ShouldNotContain("Hexalith.Works", Case.Insensitive);
    }
}
